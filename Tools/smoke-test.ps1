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
#                                         # sound, and leave it running
# Runs left open for a person to watch (-Showcase, -KeepOpen) get a
# maximized window and 2x UI scale; plain test runs stay small and muted.
# Defaults: the repository this script sits in, $env:RIMWORLD_DIR, and a
# data folder under %TEMP%. The first run links the repository into
# RimWorld's Mods folder (a directory junction).
param(
  [string]$ModRoot = (Split-Path $PSScriptRoot -Parent),
  [string]$RimWorld = $env:RIMWORLD_DIR,
  [string]$DataFolder = (Join-Path $env:TEMP 'BatshitTechnology-smoke'),
  [int]$TimeoutSeconds = 600,
  [switch]$KeepOpen,
  [switch]$Showcase
)
$ErrorActionPreference = 'Stop'
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
$forViewing = $Showcase -or $KeepOpen
$volume = if ($Showcase) { '0.6' } else { '0' }
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
$runnerFlags = if ($Showcase) { @('-showcase=BT_Showcase') } else { @('-quicktest', '-btsmoketest') }
$gameArgs = @("-savedatafolder=`"$DataFolder`"") + $runnerFlags + @(
  '-logFile', "`"$log`"", '-screen-fullscreen', '0',
  '-screen-width', "$width", '-screen-height', "$height")
if ($KeepOpen) { $gameArgs += '-btsmoketestkeep' }
$exe = Join-Path $RimWorld 'RimWorldWin64.exe'
$proc = Start-Process $exe -ArgumentList $gameArgs -WorkingDirectory $RimWorld -PassThru
"Started RimWorld (pid $($proc.Id)); log: $log"

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

if ($Showcase) {
  'Showcase: waiting for the demo base to build...'
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline -and -not $proc.HasExited) {
    Start-Sleep -Seconds 3
    if ((Test-Path $log) -and
        (Select-String -Path $log -SimpleMatch '[BT-SHOWCASE] READY' -Quiet)) {
      Maximize-Game
      'Showcase is running. Close the game when done.'
      exit 0
    }
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
if (-not $done) { "  (no DONE line: timed out after $TimeoutSeconds s, or the game exited)" }

# Errors: RimWorld logs errors and exceptions as plain lines; skip Unity's
# stack-trace and file-location noise.
# Also RimWorld's warning about statically held assets that are not loaded
# at startup on the main thread: a texture held that way may never load.
$errorPattern = 'Exception|\bError\b|XML error|Config error|Could not (resolve|find|load)|needs a StaticConstructorOnStartup'
$tracePattern = '^\s*(at |\(Filename|UnityEngine\.|Verse\.Log:)'
$modPattern = 'BT_|BatshitTechnology|RandomReplicator|Batshitt'
# An error is the mod's if its own line or its stack trace (the "at ..."
# lines that follow) mentions the mod: "Exception ticking MinifiedThing123"
# names no mod, but its trace runs through BatshitTechnology code.
$errors = @()
$modErrors = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
  $line = $lines[$i]
  if ($line -notmatch $errorPattern -or $line -match $tracePattern -or
      $line.StartsWith("[BT-SMOKE]")) { continue }  # the runner's own lines
  $trace = @()
  for ($j = $i + 1; $j -lt $lines.Count -and $lines[$j] -match $tracePattern; $j++) {
    $trace += $lines[$j]
  }
  $errors += $line
  if ($line -match $modPattern -or ($trace -match $modPattern)) { $modErrors += $line }
}
$errors = @($errors | Select-Object -Unique)
$modErrors = @($modErrors | Select-Object -Unique)
''
"== Log errors: $(@($errors).Count) total, $($modErrors.Count) mentioning this mod"
$errors | Select-Object -First 40 | % { "  $_" }

$failed = ($lines | Select-String -SimpleMatch '[BT-SMOKE] FAIL').Count
exit [int](-not $done -or $failed -gt 0 -or $modErrors.Count -gt 0)
