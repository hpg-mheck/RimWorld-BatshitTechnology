# In-game smoke test. Launches RimWorld with this mod in an isolated
# save-data folder (your own config, saves and mod list are untouched), on
# a quick-test map, with the mod's -btsmoketest runner (Source/SmokeTest.cs)
# exercising every device. Then it reports the runner's PASS/FAIL lines and
# every error in the game log. Exits 1 on any failure or mod error.
#
# Build the mod first. Usage (PowerShell):
#   .\Tools\smoke-test.ps1                # run, wait, quit, report
#   .\Tools\smoke-test.ps1 -KeepOpen      # leave the game running after
#   .\Tools\smoke-test.ps1 -Showcase      # start the BT_Showcase showcase
#                                         # through the Showcase API (the
#                                         # main menu acts on
#                                         # -showcase=BT_Showcase), with
#                                         # sound, and leave it running;
#                                         # exits 1 if it is refused, fails,
#                                         # or logs a mod error before READY
#   .\Tools\smoke-test.ps1 -Showcase -Verify
#                                         # verify the tour instead: the
#                                         # runner's Source/SmokeTestTour.cs
#                                         # watches it at Superfast, logs
#                                         # PASS/FAIL, a one-line JSON
#                                         # [BT-TOUR] SUMMARY and DONE, and
#                                         # the game quits; -TourSkip names
#                                         # scenes forced to decline
#                                         # (default anyrim, which the
#                                         # raccoon scene follows; '' runs
#                                         # all ten)
#   .\Tools\smoke-test.ps1 -Showcase -Seed tour-07
#                                         # with -Showcase: generate from
#                                         # that seed (-showcaseseed=), over
#                                         # the def's own or a random one;
#                                         # 1 to 64 of A-Z a-z 0-9 . _ -
#   .\Tools\smoke-test.ps1 -Showcase -Verify -Shots
#                                         # also save the game's own frame
#                                         # at READY and each SCENE, BEAT
#                                         # and END cue, to
#                                         # <DataFolder>\shots\NN-*.png
# Every showcase logs the seed it used ([BT-SHOWCASE] SEED <text>
# (<source>)); pass that text to -Seed to repeat the run's map and stage.
# Tools\tour-trials.ps1 runs the verifier over many seeds in parallel.
# Runs left open for a person to watch (-Showcase without -Verify, and
# -KeepOpen) get a maximized window and 2x UI scale; test runs, -Verify
# included, stay small and muted.
# Defaults: the repository this script sits in, $env:RIMWORLD_DIR, and a
# data folder under %TEMP%. The first run links the repository into
# RimWorld's Mods folder (a directory junction).
param(
  [string]$ModRoot = (Split-Path $PSScriptRoot -Parent),
  [string]$RimWorld = $env:RIMWORLD_DIR,
  [string]$DataFolder = (Join-Path $env:TEMP 'BatshitTechnology-smoke'),
  # The Tractor Bomb's checks and the Showcase API's together take longer
  # than 600 s on a slow map.
  [int]$TimeoutSeconds = 900,
  [switch]$KeepOpen,
  [switch]$Showcase,
  # With -Showcase: verify the tour and quit, rather than leave it to watch.
  [switch]$Verify,
  # With -Verify: scene keys, comma-separated, forced to decline (G1).
  [string]$TourSkip = 'anyrim',
  # With -Showcase: the seed to generate from (-showcaseseed=<text>).
  [string]$Seed = '',
  # With -Verify: save the game's frame at the tour's cues (-btshots).
  [switch]$Shots
)
$ErrorActionPreference = 'Stop'
if ($Seed -and -not $Showcase) { throw '-Seed seeds a showcase; pass -Showcase too.' }
if ($Seed -and $Seed -cnotmatch '^[A-Za-z0-9._-]{1,64}$') {
  throw "-Seed '$Seed' is not a usable seed: 1 to 64 of A-Z, a-z, 0-9, '.', '_' and '-'."
}
if ($Shots -and -not $Verify) { throw '-Shots saves a verify run''s frames; pass -Verify too.' }
if (-not $RimWorld -or -not (Test-Path (Join-Path $RimWorld 'RimWorldWin64.exe'))) {
  throw "RimWorld not found at '$RimWorld'. Set RIMWORLD_DIR or pass -RimWorld."
}
if (-not (Test-Path (Join-Path $ModRoot 'Assemblies\BatshitTechnology.dll'))) {
  throw 'Build the mod first: Assemblies\BatshitTechnology.dll is missing.'
}

# 1. Link the repository into Mods (once). Never replace something else.
$link = Join-Path $RimWorld 'Mods\BatshitTechnology'
if (Test-Path $link) {
  $item = Get-Item $link -Force
  $target = if ($item.LinkType) { @($item.Target)[0] } else { $null }
  if (-not $target -or
      (Resolve-Path $target).Path.TrimEnd('\') -ne (Resolve-Path $ModRoot).Path.TrimEnd('\')) {
    throw "$link exists and is not a link to $ModRoot; move it aside first."
  }
} else {
  cmd /c mklink /J "$link" "$ModRoot" | Out-Null
  "Linked $link -> $ModRoot"
}

# 2. Isolated config: Harmony, Core, the installed DLCs and this mod;
# muted, windowed, running in the background, dev mode on.
$config = Join-Path $DataFolder 'Config'
New-Item -ItemType Directory -Force $config | Out-Null
$version = (Get-Content (Join-Path $RimWorld 'Version.txt') -TotalCount 1).Trim()
# DLCs must load in release order: later ones inherit from earlier ones'
# defs (Odyssey's VacskinGland from Royalty), and out of order the quick-test
# game fails to start.
$releaseOrder = 'Royalty', 'Ideology', 'Biotech', 'Anomaly', 'Odyssey'
$expansions = Get-ChildItem (Join-Path $RimWorld 'Data') -Directory |
  ? Name -ne 'Core' |
  Sort-Object { $i = [array]::IndexOf($releaseOrder, $_.Name); if ($i -lt 0) { 99 } else { $i } } |
  % { ([xml](Get-Content (Join-Path $_.FullName 'About\About.xml') -Raw)).ModMetaData.packageId.ToLower() }
$aboutXml = [xml](Get-Content (Join-Path $ModRoot 'About\About.xml') -Raw)
$modId = $aboutXml.ModMetaData.packageId.ToLower()
# Harmony (brrainz.harmony) is a hard dependency: it supplies 0Harmony.dll
# for the main menu's Showcases button. Find it by packageId among the
# local mods and the Steam Workshop items, and load it first, before Core,
# as its own About.xml asks (loadBefore Ludeon.RimWorld).
$harmonyId = 'brrainz.harmony'
$modDirs = @(Get-ChildItem (Join-Path $RimWorld 'Mods') -Directory -ErrorAction SilentlyContinue)
$workshop = Join-Path $RimWorld '..\..\workshop\content\294100'
if (Test-Path $workshop) { $modDirs += @(Get-ChildItem $workshop -Directory) }
$harmony = $modDirs | ? {
  $about = Join-Path $_.FullName 'About\About.xml'
  if (-not (Test-Path $about)) { return $false }
  try { ([xml](Get-Content $about -Raw)).ModMetaData.packageId -eq $harmonyId }
  catch { $false }
} | Select-Object -First 1
if (-not $harmony) {
  throw "Harmony ($harmonyId) is not installed. Subscribe to Steam Workshop " +
    "item 2009463077, or put the Harmony mod in RimWorld's Mods folder."
}
"Harmony: $($harmony.FullName)"
$li = { param($ids) ($ids | % { "    <li>$_</li>" }) -join "`n" }
@"
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>$version</version>
  <activeMods>
$(& $li (@($harmonyId, 'ludeon.rimworld') + $expansions + $modId))
  </activeMods>
  <knownExpansions>
$(& $li $expansions)
  </knownExpansions>
</ModsConfigData>
"@ | Set-Content -Encoding UTF8 (Join-Path $config 'ModsConfig.xml')
if ($Verify -and -not $Showcase) { throw '-Verify verifies the tour; pass -Showcase too.' }
# A verify run is a test run: small and muted unless kept open.
$watching = $Showcase -and -not $Verify
$forViewing = $watching -or $KeepOpen
$volume = if ($watching) { '0.6' } else { '0' }
$width, $height = if ($forViewing) { 1600, 900 } else { 1280, 720 }
$uiScale = if ($forViewing) { '2' } else { '1' }
@"
<?xml version="1.0" encoding="utf-8"?>
<PrefsData>
  <volumeMaster>$volume</volumeMaster>
  <fullscreen>False</fullscreen>
  <screenWidth>$width</screenWidth>
  <screenHeight>$height</screenHeight>
  <uiScale>$uiScale</uiScale>
  <runInBackground>True</runInBackground>
  <devMode>True</devMode>
  <pauseOnError>False</pauseOnError>
  <automaticPauseMode>Never</automaticPauseMode>
  <langFolderName>English</langFolderName>
</PrefsData>
"@ | Set-Content -Encoding UTF8 (Join-Path $config 'Prefs.xml')

# 3. Launch and wait for the runner's DONE line.
$log = Join-Path $DataFolder 'Player.log'
Remove-Item $log -ErrorAction SilentlyContinue
# A showcase starts through the Showcase API, as a player's would: the
# main menu acts on -showcase=<defName> and generates a fresh game for it.
# A smoke run skips the menu with -quicktest.
# @(...) keeps it an array: PowerShell unwraps a one-element array that an
# if expression returns, and += would then join strings.
$runnerFlags = @(if ($Showcase) { '-showcase=BT_Showcase' } else { '-quicktest', '-btsmoketest' })
if ($Verify) {
  $runnerFlags += '-bttourverify'
  if ($TourSkip) { $runnerFlags += "-bttourskip=$TourSkip" }
  if ($Shots) { $runnerFlags += '-btshots' }
}
if ($Seed) { $runnerFlags += "-showcaseseed=$Seed" }
$gameArgs = @("-savedatafolder=`"$DataFolder`"") + $runnerFlags + @(
  '-logFile', "`"$log`"", '-screen-fullscreen', '0',
  '-screen-width', "$width", '-screen-height', "$height")
if ($KeepOpen) { $gameArgs += '-btsmoketestkeep' }
$exe = Join-Path $RimWorld 'RimWorldWin64.exe'
$proc = Start-Process $exe -ArgumentList $gameArgs -WorkingDirectory $RimWorld -PassThru
"Started RimWorld (pid $($proc.Id)); log: $log"

# Errors: RimWorld logs errors and exceptions as plain lines; skip Unity's
# stack-trace and file-location noise.
# Also RimWorld's warning about statically held assets that are not loaded
# at startup on the main thread: a texture held that way may never load.
$errorPattern = 'Exception|\bError\b|XML error|Config error|Could not (resolve|find|load)|needs a StaticConstructorOnStartup'
$tracePattern = '^\s*(at |\(Filename|UnityEngine\.|Verse\.Log:)'
# RimWorld 1.6 writes a "[Ref 9E9A3C82]" line between an error and its
# stack trace; it belongs to the trace.
$refPattern = '^\[Ref [0-9A-F]+\]\s*$'
# The mod's names, and the Showcase API's log prefix, which its own errors
# carry.
$modPattern = 'BT_|BatshitTechnology|RandomReplicator|Batshitt|\[BT-SHOWCASE\]'

# Scans log lines for errors. An error is the mod's if its own line or its
# stack trace (the "at ..." lines that follow) mentions the mod:
# "Exception ticking MinifiedThing123" names no mod, but its trace runs
# through BatshitTechnology code. Errors the runner provoked on purpose
# (a test scene made to throw) are announced first in "[BT-SMOKE] EXPECTED
# <first line>" lines, and exactly those lines are left out.
function Find-LogErrors([string[]]$lines) {
  $expected = @{}
  foreach ($l in $lines) {
    if ($l.StartsWith('[BT-SMOKE] EXPECTED ')) { $expected[$l.Substring(20)] = $true }
  }
  $all = @()
  $mod = @()
  for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -notmatch $errorPattern -or $line -match $tracePattern -or
        $line -match $refPattern -or $expected.ContainsKey($line) -or
        $line.StartsWith("[BT-SMOKE]")) { continue }  # the runner's own lines
    $trace = @()
    for ($j = $i + 1; $j -lt $lines.Count -and
         ($lines[$j] -match $tracePattern -or $lines[$j] -match $refPattern); $j++) {
      $trace += $lines[$j]
    }
    $all += $line
    if ($line -match $modPattern -or ($trace -match $modPattern)) { $mod += $line }
  }
  [pscustomobject]@{
    All = @($all | Select-Object -Unique)
    Mod = @($mod | Select-Object -Unique)
    Expected = $expected.Count
  }
}

# Maximizes the game window. Done once the game is ready, so its own
# startup resolution change cannot undo it.
function Maximize-Game {
  if (-not ('BT.Win32' -as [type])) {
    Add-Type -Namespace BT -Name Win32 -MemberDefinition @'
[DllImport("user32.dll")]
public static extern bool ShowWindowAsync(System.IntPtr hWnd, int nCmdShow);
'@
  }
  $proc.Refresh()
  if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
    [BT.Win32]::ShowWindowAsync($proc.MainWindowHandle, 3) | Out-Null # SW_MAXIMIZE
  }
}

if ($watching) {
  # Waits for READY, then leaves the game running for the viewer. A
  # REFUSED or FAILED cue ends the wait at once (the showcase will never
  # come), and the log up to READY must hold no mod error.
  'Showcase: waiting for the demo base to build...'
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline -and -not $proc.HasExited) {
    Start-Sleep -Seconds 3
    if (-not (Test-Path $log)) { continue }
    $sofar = @(Get-Content $log)
    $end = $sofar | Select-String -Pattern '^\[BT-SHOWCASE\] (READY|REFUSED|FAILED)\b' |
      Select-Object -First 1
    if (-not $end) { continue }
    $before = if ($end.LineNumber -gt 1) { $sofar[0..($end.LineNumber - 2)] } else { @() }
    $scan = Find-LogErrors $before
    "== Log errors before $($end.Matches[0].Groups[1].Value): $(@($scan.All).Count) total, " +
      "$(@($scan.Mod).Count) mentioning this mod"
    $scan.All | Select-Object -First 40 | % { "  $_" }
    if ($end.Matches[0].Groups[1].Value -ne 'READY') {
      throw "The showcase did not start: $($end.Line); see $log."
    }
    Maximize-Game
    if (@($scan.Mod).Count -gt 0) {
      'Showcase is running, but the mod logged errors before READY (above).'
      exit 1
    }
    'Showcase is running. Close the game when done.'
    exit 0
  }
  throw "The showcase did not report READY within $TimeoutSeconds s; see $log."
}
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$done = $false
while ((Get-Date) -lt $deadline -and -not $proc.HasExited) {
  Start-Sleep -Seconds 3
  if ((Test-Path $log) -and (Select-String -Path $log -SimpleMatch '[BT-SMOKE] DONE' -Quiet)) {
    $done = $true
    break
  }
}
if ($KeepOpen) {
  Maximize-Game
} elseif (-not $proc.WaitForExit(60000)) {
  Stop-Process -Id $proc.Id -Force
}

# 4. Report.
if (-not (Test-Path $log)) { throw "No log was written at $log." }
$lines = Get-Content $log
''
'== Smoke test results'
$lines | Select-String -SimpleMatch '[BT-SMOKE]' | % { "  $($_.Line)" }
$lines | Select-String -SimpleMatch '[BT-TOUR] SUMMARY' | % { "  $($_.Line)" }
if (-not $done) { "  (no DONE line: timed out after $TimeoutSeconds s, or the game exited)" }

$scan = Find-LogErrors $lines
$errors = $scan.All
$modErrors = $scan.Mod
''
"== Log errors: $(@($errors).Count) total, $($modErrors.Count) mentioning this mod" +
  $(if ($scan.Expected) { " ($($scan.Expected) provoked on purpose, left out)" } else { '' })
$errors | Select-Object -First 40 | % { "  $_" }

$failed = ($lines | Select-String -SimpleMatch '[BT-SMOKE] FAIL').Count
exit [int](-not $done -or $failed -gt 0 -or $modErrors.Count -gt 0)
