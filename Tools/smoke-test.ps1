# In-game smoke test. Launches RimWorld with this mod in an isolated
# save-data folder (your own config, saves and mod list are untouched), on
# a quick-test map, with the mod's -rrsmoketest runner (Source/SmokeTest.cs)
# exercising every device. Then it reports the runner's PASS/FAIL lines and
# every error in the game log. Exits 1 on any failure or mod error.
#
# Build the mod first. Usage (PowerShell):
#   .\Tools\smoke-test.ps1                # run, wait, quit, report
#   .\Tools\smoke-test.ps1 -KeepOpen      # leave the game running after
# Defaults: the repository this script sits in, $env:RIMWORLD_DIR, and a
# data folder under %TEMP%. The first run links the repository into
# RimWorld's Mods folder (a directory junction).
param(
  [string]$ModRoot = (Split-Path $PSScriptRoot -Parent),
  [string]$RimWorld = $env:RIMWORLD_DIR,
  [string]$DataFolder = (Join-Path $env:TEMP 'RandomReplicator-smoke'),
  [int]$TimeoutSeconds = 600,
  [switch]$KeepOpen
)
$ErrorActionPreference = 'Stop'
if (-not $RimWorld -or -not (Test-Path (Join-Path $RimWorld 'RimWorldWin64.exe'))) {
  throw "RimWorld not found at '$RimWorld'. Set RIMWORLD_DIR or pass -RimWorld."
}
if (-not (Test-Path (Join-Path $ModRoot 'Assemblies\RandomReplicator.dll'))) {
  throw 'Build the mod first: Assemblies\RandomReplicator.dll is missing.'
}

# 1. Link the repository into Mods (once). Never replace something else.
$link = Join-Path $RimWorld 'Mods\RandomReplicator'
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
@'
<?xml version="1.0" encoding="utf-8"?>
<PrefsData>
  <volumeMaster>0</volumeMaster>
  <fullscreen>False</fullscreen>
  <screenWidth>1280</screenWidth>
  <screenHeight>720</screenHeight>
  <runInBackground>True</runInBackground>
  <devMode>True</devMode>
  <pauseOnError>False</pauseOnError>
  <langFolderName>English</langFolderName>
</PrefsData>
'@ | Set-Content -Encoding UTF8 (Join-Path $config 'Prefs.xml')

# 3. Launch and wait for the runner's DONE line.
$log = Join-Path $DataFolder 'Player.log'
Remove-Item $log -ErrorAction SilentlyContinue
$gameArgs = @("-savedatafolder=`"$DataFolder`"", '-quicktest', '-rrsmoketest',
  '-logFile', "`"$log`"", '-screen-fullscreen', '0',
  '-screen-width', '1280', '-screen-height', '720')
if ($KeepOpen) { $gameArgs += '-rrsmoketestkeep' }
$exe = Join-Path $RimWorld 'RimWorldWin64.exe'
$proc = Start-Process $exe -ArgumentList $gameArgs -WorkingDirectory $RimWorld -PassThru
"Started RimWorld (pid $($proc.Id)); log: $log"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$done = $false
while ((Get-Date) -lt $deadline -and -not $proc.HasExited) {
  Start-Sleep -Seconds 3
  if ((Test-Path $log) -and (Select-String -Path $log -SimpleMatch '[RR-SMOKE] DONE' -Quiet)) {
    $done = $true
    break
  }
}
if (-not $KeepOpen) {
  if (-not $proc.WaitForExit(60000)) { Stop-Process -Id $proc.Id -Force }
}

# 4. Report.
if (-not (Test-Path $log)) { throw "No log was written at $log." }
$lines = Get-Content $log
''
'== Smoke test results'
$lines | Select-String -SimpleMatch '[RR-SMOKE]' | % { "  $($_.Line)" }
if (-not $done) { "  (no DONE line: timed out after $TimeoutSeconds s, or the game exited)" }

# Errors: RimWorld logs errors and exceptions as plain lines; skip Unity's
# stack-trace and file-location noise.
$errorPattern = 'Exception|\bError\b|XML error|Config error|Could not (resolve|find|load)'
$errors = $lines | ? {
  $_ -match $errorPattern -and $_ -notmatch '^\s*(at |\(Filename|UnityEngine\.|Verse\.Log:)'
} | Select-Object -Unique
$modPattern = 'RR_|RandomReplicator|Batshitt'
$modErrors = @($errors | ? { $_ -match $modPattern })
''
"== Log errors: $(@($errors).Count) total, $($modErrors.Count) mentioning this mod"
$errors | Select-Object -First 40 | % { "  $_" }

$failed = ($lines | Select-String -SimpleMatch '[RR-SMOKE] FAIL').Count
exit [int](-not $done -or $failed -gt 0 -or $modErrors.Count -gt 0)
