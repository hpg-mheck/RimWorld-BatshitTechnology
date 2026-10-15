# Tour trials. Runs the directed tour's verifier (Tools\smoke-test.ps1
# -Showcase -Verify, every scene: -TourSkip '' by default) over a list of
# seeds, several games at once, each in its own data folder under a fresh
# .local\tmp\tour-trials-<stamp>\ directory, and gathers what each run's
# verifier logged ([BT-TOUR] SUMMARY, one line of JSON, and DONE) into
# results.json and results.csv there. Prints a short table, and for any
# seed run more than once whether its MAPHASH and STAGEHASH repeated.
#
# Build the mod first. Usage (PowerShell):
#   .\Tools\tour-trials.ps1 -Count 32               # tour-01 .. tour-32
#   .\Tools\tour-trials.ps1 -Seeds alpha,beta -Repeat 2
#   .\Tools\tour-trials.ps1 -Seeds alpha -RandomRuns 1   # plus one unseeded
#                                                   # run (it logs its seed)
#   .\Tools\tour-trials.ps1 -Count 4 -Parallel 2 -Shots
#   .\Tools\tour-trials.ps1 -Collect .local\tmp\tour-trials-<stamp>
#                                                   # rebuild the results
#                                                   # from a trials folder
# Options: -Parallel (games at once, default 4), -Repeat (runs per seed,
# default 1), -Shots (each run saves its frames to <run>\shots\),
# -TourSkip (scenes forced to decline; default none), -TimeoutSeconds (per
# run, passed to smoke-test.ps1; default 1500), -OutDir (default a fresh
# folder under .local\tmp).
# A run that crashes, hangs or times out gets a row saying so; the others
# carry on. Only games this script started (by the pid each run reports)
# are ever stopped. Each run's folder keeps trial.json (its name, seed and
# start) and exit.txt (its exit code and end), so -Collect can rebuild the
# results. Exits 1 if any run left no SUMMARY, else 0.
# The CSV flattens the SUMMARY: nested fields are dotted (scenes.home.tps),
# lists are joined with ';'. docs/AI-TESTING-STRATEGY.txt lists the fields.
# Windows PowerShell 5.1 and PowerShell 7 both run it.
param(
  [string[]]$Seeds = @(),
  [int]$Count = 0,
  [int]$RandomRuns = 0,
  [int]$Parallel = 4,
  [int]$Repeat = 1,
  [switch]$Shots,
  [string]$TourSkip = '',
  [int]$TimeoutSeconds = 1500,
  [string]$ModRoot = (Split-Path $PSScriptRoot -Parent),
  [string]$RimWorld = $env:RIMWORLD_DIR,
  [string]$OutDir = '',
  [string]$Collect = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

# Starts one run: smoke-test.ps1 in a hidden shell of this edition, with
# its output in <run>\smoke-test.out.txt. -EncodedCommand keeps an empty
# -TourSkip and the paths intact, whatever they hold. The run records its
# exit code and end in exit.txt itself, so a crash of this script loses
# nothing.
function Start-Trial($run) {
  $folder = Join-Path $OutDir $run.Name
  New-Item -ItemType Directory -Force $folder | Out-Null
  $started = Get-Date
  [pscustomobject]@{ name = $run.Name; seed = $run.Seed; repeat = $run.Repeat;
    started = $started.ToString('o') } | ConvertTo-Json |
    Set-Content -Encoding UTF8 (Join-Path $folder 'trial.json')
  $q = { param($t) "'" + ($t -replace "'", "''") + "'" }
  $call = "& $(& $q $smoke) -Showcase -Verify -TourSkip $(& $q $TourSkip) " +
    "-DataFolder $(& $q $folder) -TimeoutSeconds $TimeoutSeconds " +
    "-RimWorld $(& $q $RimWorld) -ModRoot $(& $q $ModRoot)"
  if ($run.Seed) { $call += " -Seed $(& $q $run.Seed)" }
  if ($Shots) { $call += ' -Shots' }
  $exitFile = & $q (Join-Path $folder 'exit.txt')
  $command = "try { $call; `$c = `$LASTEXITCODE } " +
    "catch { Write-Output ('THROWN: ' + `$_); `$c = 2 }; " +
    "Set-Content -LiteralPath $exitFile -Value (`"`$c `" + (Get-Date).ToString('o')); exit `$c"
  $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
  $out = Join-Path $folder 'smoke-test.out.txt'
  $proc = Start-Process -FilePath $shell -PassThru -WindowStyle Hidden `
    -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded) `
    -RedirectStandardOutput $out -RedirectStandardError (Join-Path $folder 'smoke-test.err.txt')
  $null = $proc.Handle  # keeps ExitCode readable after the process ends
  [pscustomobject]@{
    Run = $run; Folder = $folder; Out = $out; Proc = $proc
    Started = $started; GamePid = $null; GameStarted = $null
  }
}

# The pid of the game a run started, from its "Started RimWorld (pid N)"
# line, once it has one.
function Update-GamePid($active) {
  if ($active.GamePid -or -not (Test-Path $active.Out)) { return }
  $hit = Select-String -Path $active.Out -Pattern 'Started RimWorld \(pid (\d+)\)' `
    -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($hit) {
    $active.GamePid = [int]$hit.Matches[0].Groups[1].Value
    $game = Get-Process -Id $active.GamePid -ErrorAction SilentlyContinue
    if ($game) { $active.GameStarted = $game.StartTime }
  }
}

# Stops a run's game, only if that pid is still the RimWorld this run
# started (a pid can be reused once a process has gone).
function Stop-TrialGame($active) {
  if (-not $active.GamePid) { return }
  $game = Get-Process -Id $active.GamePid -ErrorAction SilentlyContinue
  if ($game -and $game.ProcessName -eq 'RimWorldWin64' -and
      (-not $active.GameStarted -or $game.StartTime -eq $active.GameStarted)) {
    Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue
  }
}

# Adds a SUMMARY value to a row: nested objects as dotted names, lists
# joined with ';' (a list of objects as compact JSON).
function Add-Flat($row, [string]$name, $value) {
  if ($null -eq $value) { $row[$name] = $null; return }
  if ($value -is [System.Management.Automation.PSCustomObject]) {
    foreach ($p in $value.PSObject.Properties) {
      Add-Flat $row ($(if ($name) { "$name.$($p.Name)" } else { $p.Name })) $p.Value
    }
    return
  }
  if ($value -is [System.Array]) {
    $objects = @($value | Where-Object { $_ -is [System.Management.Automation.PSCustomObject] })
    if ($objects.Count -gt 0) {
      $row[$name] = ConvertTo-Json -InputObject $value -Compress -Depth 12
    } else {
      $row[$name] = ($value -join ';')
    }
    return
  }
  $row[$name] = $value
}

# A time written with ToString('o'), or one ConvertFrom-Json has already
# made a DateTime (PowerShell 7 does).
function ConvertTo-Time($value) {
  if ($value -is [DateTime]) { return $value }
  [DateTime]::Parse([string]$value, [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::RoundtripKind)
}

# The last line of a log matching a pattern, or $null.
function Get-LastMatch([string[]]$lines, [string]$pattern) {
  $hit = $null
  foreach ($l in $lines) { if ($l -match $pattern) { $hit = $l } }
  $hit
}

# One run's row, from its folder: trial.json and exit.txt (its own
# record), its game's log, and the problem the watcher saw, if any.
function Get-TrialRow([string]$folder, [string]$problem, $exitCode) {
  $trial = $null
  $trialFile = Join-Path $folder 'trial.json'
  if (Test-Path $trialFile) { $trial = Get-Content -Raw $trialFile | ConvertFrom-Json }
  $name = if ($trial) { $trial.name } else { Split-Path $folder -Leaf }
  $started = if ($trial) { ConvertTo-Time $trial.started } else { $null }
  $ended = Get-Date
  $exitFile = Join-Path $folder 'exit.txt'
  $exitRecorded = Test-Path $exitFile
  if ($exitRecorded) {
    $parts = (Get-Content -Raw $exitFile).Trim() -split ' ', 2
    if ($null -eq $exitCode) { $exitCode = [int]$parts[0] }
    if ($parts.Count -gt 1) { $ended = ConvertTo-Time $parts[1] }
  }

  $log = Join-Path $folder 'Player.log'
  $lines = if (Test-Path $log) { [string[]](Get-Content $log) } else { [string[]]@() }
  $summaryLine = Get-LastMatch $lines '^\[BT-TOUR\] SUMMARY '
  $summary = $null
  if ($summaryLine) {
    try { $summary = $summaryLine.Substring(18) | ConvertFrom-Json }
    catch { $problem = ((@($problem, "SUMMARY did not parse: $_") | Where-Object { $_ }) -join '; ') }
  }
  $done = Get-LastMatch $lines '^\[BT-SMOKE\] DONE passed=(\d+) failed=(\d+)'
  $seedCue = Get-LastMatch $lines '^\[BT-SHOWCASE\] SEED (\S+) \(([^)]+)\)'
  $mapCue = Get-LastMatch $lines '^\[BT-SHOWCASE\] MAPHASH (\S+)'
  $stageCue = Get-LastMatch $lines '^\[BT-SHOWCASE\] STAGEHASH (\S+)'
  if (-not $problem -and -not $summary) {
    $problem = if (-not (Test-Path $log)) { 'no game log' }
      elseif (-not $done) { 'no DONE line (the game crashed or timed out)' }
      elseif (-not $exitRecorded) { 'no exit record (the run was cut off)' }
      else { 'no SUMMARY line' }
  }

  $row = [ordered]@{
    run = $name
    seed = $(if ($seedCue -and $seedCue -match 'SEED (\S+) \(') { $Matches[1] } elseif ($trial) { $trial.seed } else { $null })
    seedSource = $(if ($seedCue -and $seedCue -match ' \(([^)]+)\)') { $Matches[1] } else { $null })
    repeat = $(if ($trial) { $trial.repeat } else { $null })
    exitCode = $exitCode
    error = $problem
    passed = $(if ($done -and $done -match 'passed=(\d+)') { [int]$Matches[1] } else { $null })
    failed = $(if ($done -and $done -match 'failed=(\d+)') { [int]$Matches[1] } else { $null })
    wallSeconds = $(if ($started) { [math]::Round(($ended - $started).TotalSeconds) } else { $null })
    mapHash = $(if ($mapCue) { ($mapCue -split ' ')[2] } else { $null })
    stageHash = $(if ($stageCue) { ($stageCue -split ' ')[2] } else { $null })
    finished = $null
  }
  if ($summary) {
    foreach ($p in $summary.PSObject.Properties) {
      if ($p.Name -in 'seed', 'seedSource', 'passed', 'failed', 'version', 'mapHash', 'stageHash') {
        continue
      }
      Add-Flat $row $p.Name $p.Value
    }
  }
  [pscustomobject]@{ Row = $row; Summary = $summary }
}

# Writes results.json (each row with its SUMMARY whole) and results.csv
# (the SUMMARY flattened, every row with the same columns), and prints
# the table and the repeats.
function Write-Results($results) {
  $ordered = @($results | Sort-Object { $_.Row['run'] })
  $json = @($ordered | ForEach-Object {
      $o = [ordered]@{}
      foreach ($k in 'run', 'seed', 'seedSource', 'repeat', 'exitCode', 'error', 'passed',
          'failed', 'wallSeconds', 'mapHash', 'stageHash') { $o[$k] = $_.Row[$k] }
      $o['summary'] = $_.Summary
      [pscustomobject]$o
    })
  ConvertTo-Json -InputObject $json -Depth 12 |
    Set-Content -Encoding UTF8 (Join-Path $OutDir 'results.json')
  $columns = New-Object System.Collections.Generic.List[string]
  foreach ($res in $ordered) {
    foreach ($k in $res.Row.Keys) { if (-not $columns.Contains($k)) { $columns.Add($k) } }
  }
  $ordered | ForEach-Object {
    $o = [ordered]@{}
    foreach ($c in $columns) { $o[$c] = $(if ($_.Row.Contains($c)) { $_.Row[$c] } else { $null }) }
    [pscustomobject]$o
  } | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $OutDir 'results.csv')

  ''
  '== Tour trials'
  $ordered | ForEach-Object {
    $r = $_.Row
    $short = { param($h) if ($h) { $h.Substring(0, [Math]::Min(8, $h.Length)) } }
    [pscustomobject]@{
      Run = $r['run']; Seed = $r['seed']; Exit = $r['exitCode']
      'Pass/Fail' = "$($r['passed'])/$($r['failed'])"; 'Wall s' = $r['wallSeconds']
      MapHash = & $short $r['mapHash']; StageHash = & $short $r['stageHash']
      Finished = $r['finished']; Error = $r['error']
    }
  } | Format-Table -AutoSize | Out-String -Width 220

  $repeated = @($ordered | Group-Object { $_.Row['seed'] } | Where-Object { $_.Count -gt 1 })
  if ($repeated.Count -gt 0) {
    '== Repeats of a seed'
    foreach ($g in $repeated) {
      $maps = @($g.Group | ForEach-Object { $_.Row['mapHash'] } | Select-Object -Unique)
      $stages = @($g.Group | ForEach-Object { $_.Row['stageHash'] } | Select-Object -Unique)
      "  $($g.Name): $($g.Count) runs; MAPHASH " +
        $(if ($maps.Count -eq 1) { 'same' } else { 'DIFFERS' }) + ', STAGEHASH ' +
        $(if ($stages.Count -eq 1) { 'same' } else { 'DIFFERS' })
    }
  }
  "Results: $(Join-Path $OutDir 'results.csv') and results.json"
  @($ordered | Where-Object { -not $_.Summary }).Count
}

if ($Collect) {
  # Rebuilds the results from an existing trials folder's runs.
  $OutDir = (Resolve-Path $Collect).Path
  $results = New-Object System.Collections.Generic.List[object]
  foreach ($dir in Get-ChildItem $OutDir -Directory) {
    if ((Test-Path (Join-Path $dir.FullName 'trial.json')) -or
        (Test-Path (Join-Path $dir.FullName 'Player.log'))) {
      $results.Add((Get-TrialRow $dir.FullName $null $null))
    }
  }
  $missing = Write-Results $results
  $missing | Select-Object -SkipLast 1
  exit [int](($missing | Select-Object -Last 1) -gt 0)
}

if (-not $RimWorld -or -not (Test-Path (Join-Path $RimWorld 'RimWorldWin64.exe'))) {
  throw "RimWorld not found at '$RimWorld'. Set RIMWORLD_DIR or pass -RimWorld."
}
if ($Parallel -lt 1) { throw '-Parallel must be at least 1.' }
if ($Repeat -lt 1) { throw '-Repeat must be at least 1.' }

# Seeds: -Seeds a,b,c (split here too, since powershell -File passes one
# string), then tour-01 .. tour-NN for -Count, then -RandomRuns unseeded.
$seedList = @($Seeds | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } |
  Where-Object { $_ })
foreach ($s in $seedList) {
  if ($s -cnotmatch '^[A-Za-z0-9._-]{1,64}$') {
    throw "Seed '$s' is not usable: 1 to 64 of A-Z, a-z, 0-9, '.', '_' and '-'."
  }
}
for ($i = 1; $i -le $Count; $i++) { $seedList += ('tour-{0:00}' -f $i) }
$plan = New-Object System.Collections.Generic.List[object]
foreach ($s in $seedList) {
  for ($r = 1; $r -le $Repeat; $r++) {
    $plan.Add([pscustomobject]@{ Name = "$s-r$r"; Seed = $s; Repeat = $r })
  }
}
for ($i = 1; $i -le $RandomRuns; $i++) {
  $plan.Add([pscustomobject]@{ Name = ('random{0:00}' -f $i); Seed = ''; Repeat = 1 })
}
if ($plan.Count -eq 0) { throw 'Nothing to run: pass -Seeds, -Count or -RandomRuns.' }

$smoke = Join-Path $PSScriptRoot 'smoke-test.ps1'
$shell = (Get-Process -Id $PID).Path
if (-not $OutDir) {
  $OutDir = Join-Path $ModRoot ('.local\tmp\tour-trials-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
if (Test-Path $OutDir) { throw "$OutDir exists already; trials need a fresh folder." }
New-Item -ItemType Directory -Force $OutDir | Out-Null
"Tour trials: $($plan.Count) runs, $Parallel at a time, into $OutDir"

$queue = New-Object System.Collections.Generic.Queue[object]
foreach ($run in $plan) { $queue.Enqueue($run) }
# Windows PowerShell 5.1 cannot wrap these generic lists in @() ("Argument
# types do not match"), so they are copied with ToArray to be walked.
$active = New-Object System.Collections.Generic.List[object]
$results = New-Object System.Collections.Generic.List[object]
$limit = [TimeSpan]::FromSeconds($TimeoutSeconds + 240)
try {
  while ($queue.Count -gt 0 -or $active.Count -gt 0) {
    while ($queue.Count -gt 0 -and $active.Count -lt $Parallel) {
      $next = Start-Trial $queue.Dequeue()
      $active.Add($next)
      "  started $($next.Run.Name)"
      Start-Sleep -Seconds 3  # staggers the games' heavy start-up
    }
    Start-Sleep -Seconds 5
    foreach ($a in $active.ToArray()) {
      Update-GamePid $a
      $problem = $null
      if (-not $a.Proc.HasExited) {
        if (((Get-Date) - $a.Started) -lt $limit) { continue }
        $problem = "timed out after $([int]$limit.TotalSeconds) s; stopped"
        Stop-TrialGame $a
        Stop-Process -Id $a.Proc.Id -Force -ErrorAction SilentlyContinue
        $a.Proc.WaitForExit(10000) | Out-Null
      } else {
        Stop-TrialGame $a  # smoke-test.ps1 stops its own; this is belt and braces
      }
      $exit = if ($a.Proc.HasExited) { $a.Proc.ExitCode } else { $null }
      $result = Get-TrialRow $a.Folder $problem $exit
      $results.Add($result)
      $active.Remove($a) | Out-Null
      $r = $result.Row
      "  ended   $($r['run']): exit $($r['exitCode']), passed $($r['passed']), " +
        "failed $($r['failed']), $($r['wallSeconds']) s" +
        $(if ($r['error']) { "; $($r['error'])" } else { '' })
    }
  }
} finally {
  foreach ($a in $active.ToArray()) {
    Stop-TrialGame $a
    if (-not $a.Proc.HasExited) { Stop-Process -Id $a.Proc.Id -Force -ErrorAction SilentlyContinue }
  }
}

$missing = Write-Results $results
$missing | Select-Object -SkipLast 1
exit [int](($missing | Select-Object -Last 1) -gt 0)
