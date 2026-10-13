# In-game smoke test. Launches RimWorld with this mod in an isolated
# save-data folder (your own config, saves and mod list are untouched), on
# a quick-test map, with the mod's -btsmoketest runner (Source/SmokeTest.cs)
# exercising every device. Then it reports the runner's PASS/FAIL lines and
# every error in the game log. Exits 1 on any failure or mod error.
#
# Build the mod first. Usage (PowerShell):
#   .\Tools\smoke-test.ps1                # run, wait, quit, report
#   .\Tools\smoke-test.ps1 -KeepOpen      # leave the game running after
#   .\Tools\smoke-test.ps1 -Showcase      # build a demo base instead, with
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

# 2. Isolated config: Core, the installed DLCs and this mod; muted,
# windowed, running in the background, dev mode on.
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
$li = { param($ids) ($ids | % { "    <li>$_</li>" }) -join "`n" }
@"
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>$version</version>
  <activeMods>
$(& $li (@('ludeon.rimworld') + $expansions + $modId))
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
  <langFolderName>English</langFolderName>
</PrefsData>
"@ | Set-Content -Encoding UTF8 (Join-Path $config 'Prefs.xml')

# 3. Launch and wait for the runner's DONE line.
$log = Join-Path $DataFolder 'Player.log'
Remove-Item $log -ErrorAction SilentlyContinue
$runnerFlag = if ($Showcase) { '-btshowcase' } else { '-btsmoketest' }
$gameArgs = @("-savedatafolder=`"$DataFolder`"", '-quicktest', $runnerFlag,
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
$errorPattern = 'Exception|\bError\b|XML error|Config error|Could not (resolve|find|load)'
$errors = $lines | ? {
  $_ -match $errorPattern -and $_ -notmatch '^\s*(at |\(Filename|UnityEngine\.|Verse\.Log:)'
} | Select-Object -Unique
$modPattern = 'BT_|BatshitTechnology|RandomReplicator|Batshitt'
$modErrors = @($errors | ? { $_ -match $modPattern })
''
"== Log errors: $(@($errors).Count) total, $($modErrors.Count) mentioning this mod"
$errors | Select-Object -First 40 | % { "  $_" }

$failed = ($lines | Select-String -SimpleMatch '[BT-SMOKE] FAIL').Count
exit [int](-not $done -or $failed -gt 0 -or $modErrors.Count -gt 0)
