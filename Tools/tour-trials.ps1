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
# folder under .local\tmp; a relative one is taken from the current
# folder and made absolute, since RimWorld starts in its own folder).
# A seed may appear once (compared ignoring case, as folder names are);
# -Repeat runs it more than once.
# A run that crashes, hangs or times out gets a row saying so; the others
# carry on. Only games this script started (by the pid each run reports,
# and that process's start time) are ever stopped. Each run's folder keeps
# trial.json (its name, seed and start) and exit.txt (its exit code and
# end), and problem.txt when the watcher stopped it, so -Collect can
# rebuild the results; a run with no recorded end gets no wallSeconds.
# Seeds whose maps hold layouts (ancient structures, ruins) are listed:
# their rooms are not pinned, so their MAPHASH may differ between
# processes (docs/SHOWCASE-API.md, "Seeds"); repeat them to see.
# Exits 1 if any run left no SUMMARY, else 0.
# Dot-sourced (. .\Tools\tour-trials.ps1) it only defines its functions,
# for Tools\tests\test_trial_tools.py.
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

# A path made absolute against the current folder, as PowerShell's own
# cmdlets would take it, whether or not it exists yet. Paths go to other
# processes (the hidden shells, and RimWorld, which starts in its own
# folder and would resolve a relative one against that).
function Resolve-FullPath([string]$path) {
  if (-not $path) { return $path }
  $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($path)
}

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
    Started = $started; GamePid = $null; GameStarted = $null; GameGone = $false
  }
}

# A process's start time, or $null if it cannot be read (gone, or not
# ours to read).
function Get-StartTime($process) {
  if (-not $process) { return $null }
  try { $process.StartTime } catch { $null }
}

# The pid of the game a run started, from its "Started RimWorld (pid N)"
# line, once it has one, with that process's start time. A pid whose
# process is already gone, or is not RimWorld, or whose start time cannot
# be read, is marked gone and never stopped: Windows may have given the
# number to another process, a sibling trial's game or the operator's own.
function Update-GamePid($active) {
  if ($active.GamePid -or -not (Test-Path $active.Out)) { return }
  $hit = Select-String -Path $active.Out -Pattern 'Started RimWorld \(pid (\d+)\)' `
    -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($hit) {
    $active.GamePid = [int]$hit.Matches[0].Groups[1].Value
    $game = Get-Process -Id $active.GamePid -ErrorAction SilentlyContinue
    $started = if ($game -and $game.ProcessName -eq 'RimWorldWin64') { Get-StartTime $game }
    if ($started) { $active.GameStarted = $started } else { $active.GameGone = $true }
  }
}

# Whether a process is the game a run started: the pid it reported, still
# RimWorld, and started at the time recorded for it. With no recorded
# start time it is never taken for the run's game.
function Test-OwnGame($process, $active) {
  if (-not $process -or -not $active.GamePid -or $active.GameGone -or
      -not $active.GameStarted) { return $false }
  $started = Get-StartTime $process
  [bool]($process.Id -eq $active.GamePid -and $process.ProcessName -eq 'RimWorldWin64' -and
    $started -and $started -eq $active.GameStarted)
}

# Stops a run's game, only if that pid is still the RimWorld this run
# started (a pid can be reused once a process has gone).
function Stop-TrialGame($active) {
  if (-not $active.GamePid -or $active.GameGone) { return }
  $game = Get-Process -Id $active.GamePid -ErrorAction SilentlyContinue
  if (Test-OwnGame $game $active) {
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
# record), problem.txt (what the watcher saw when it stopped the run), its
# game's log, and the problem the watcher saw now, if any. -Live, as the
# watcher calls it for a run that has just ended, takes now as the end
# when none is recorded; -Collect does not, and leaves wallSeconds empty.
function Get-TrialRow([string]$folder, [string]$problem, $exitCode, [switch]$Live) {
  $trial = $null
  $trialFile = Join-Path $folder 'trial.json'
  if (Test-Path $trialFile) { $trial = Get-Content -Raw $trialFile | ConvertFrom-Json }
  $name = if ($trial) { $trial.name } else { Split-Path $folder -Leaf }
  $started = if ($trial) { ConvertTo-Time $trial.started } else { $null }
  $ended = if ($Live) { Get-Date } else { $null }
  $exitFile = Join-Path $folder 'exit.txt'
  $exitRecorded = Test-Path $exitFile
  if ($exitRecorded) {
    $parts = (Get-Content -Raw $exitFile).Trim() -split ' ', 2
    if ($null -eq $exitCode) { $exitCode = [int]$parts[0] }
    if ($parts.Count -gt 1) { $ended = ConvertTo-Time $parts[1] }
  }
  $problemFile = Join-Path $folder 'problem.txt'
  if (-not $problem -and (Test-Path $problemFile)) {
    $problem = (Get-Content -Raw $problemFile).Trim()
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
    wallSeconds = $(if ($started -and $ended) { [math]::Round(($ended - $started).TotalSeconds) } else { $null })
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

# How the runs of one seed compare on one fingerprint, from the values the
# runs logged (a run that logged none adds nothing) and how many runs
# there were: "same" only when at least two runs carry one and all agree
# (compared exactly); runs without one are counted, never compared; with
# fewer than two carrying one, "not compared".
function Get-RepeatVerdict([object[]]$values, [int]$runs) {
  $have = @($values | Where-Object { $_ })
  $without = $runs - $have.Count
  $distinct = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
  foreach ($v in $have) { $null = $distinct.Add([string]$v) }
  $note = "$($have.Count) of $runs runs" + $(if ($without -gt 0) { "; $without without one" } else { '' })
  if ($have.Count -lt 2) { return "not compared ($note)" }
  if ($distinct.Count -eq 1) { return "same ($note)" }
  "DIFFERS ($($distinct.Count) values in $note)"
}

# Writes results.json (each row with its SUMMARY whole) and results.csv
# (the SUMMARY flattened, every row with the same columns), and prints
# the table, the repeats, and the runs whose maps hold layouts.
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

  # Seeds are case-sensitive (Alpha and alpha are two seeds), so are their
  # groups; a run with no seed at all is no repeat of anything.
  $repeated = @($ordered | Where-Object { $_.Row['seed'] } |
    Group-Object -CaseSensitive { $_.Row['seed'] } | Where-Object { $_.Count -gt 1 })
  if ($repeated.Count -gt 0) {
    '== Repeats of a seed'
    foreach ($g in $repeated) {
      $maps = Get-RepeatVerdict @($g.Group | ForEach-Object { $_.Row['mapHash'] }) $g.Count
      $stages = Get-RepeatVerdict @($g.Group | ForEach-Object { $_.Row['stageHash'] }) $g.Count
      $starts = @($g.Group | ForEach-Object { $_.Row['startState'] } | Where-Object { $_ })
      "  $($g.Name): $($g.Count) runs; MAPHASH $maps; STAGEHASH $stages" +
        $(if ($starts.Count -gt 0) {
            '; start state ' + (Get-RepeatVerdict $starts $g.Count)
          } else { '' })
    }
  }

  $layouts = @($ordered | Where-Object {
      $_.Row.Contains('unpinnedLayouts') -and $_.Row['unpinnedLayouts'] -and
      [int]$_.Row['unpinnedLayouts'] -gt 0 })
  if ($layouts.Count -gt 0) {
    '== Maps holding layouts, whose rooms a seed does not pin (MAPHASH may differ'
    '   in another process; repeat the seed with -Repeat 2 to see)'
    foreach ($res in $layouts) {
      $r = $res.Row
      "  $($r['run']) (seed $($r['seed'])): $($r['unpinnedLayouts']) layout(s)" +
        $(if ($r.Contains('landmark') -and $r['landmark']) { "; landmark $($r['landmark'])" } else { '' })
    }
  }
  "Results: $(Join-Path $OutDir 'results.csv') and results.json"
  @($ordered | Where-Object { -not $_.Summary }).Count
}

# Dot-sourced, for the tests: the functions only.
if ($MyInvocation.InvocationName -eq '.') { return }

if ($Collect) {
  # Rebuilds the results from an existing trials folder's runs.
  $OutDir = (Resolve-Path (Resolve-FullPath $Collect)).Path
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
# A seed given twice, or two seeds that differ only in case, would put two
# live games in one data folder (folder names ignore case on Windows).
$seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$twice = @($seedList | Where-Object { -not $seen.Add($_) })
if ($twice.Count -gt 0) {
  throw "Seeds repeat (compared ignoring case, as folder names are): " +
    "$(($twice | Select-Object -Unique) -join ', '). Give each seed once; " +
    "-Repeat runs a seed more than once."
}
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
$names = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($run in $plan) {
  if (-not $names.Add($run.Name)) {
    throw "Two runs would share the folder '$($run.Name)'; rename a seed."
  }
}

$smoke = Join-Path $PSScriptRoot 'smoke-test.ps1'
$shell = (Get-Process -Id $PID).Path
$ModRoot = Resolve-FullPath $ModRoot
$RimWorld = Resolve-FullPath $RimWorld
if (-not $OutDir) {
  $OutDir = Join-Path $ModRoot ('.local\tmp\tour-trials-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$OutDir = Resolve-FullPath $OutDir
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
        Stop-Process -Id $a.Proc.Id -Force -ErrorAction SilentlyContinue
        $a.Proc.WaitForExit(10000) | Out-Null
        Stop-TrialGame $a
        # The shell was stopped before it could record its end: the watcher
        # records it, and why, so -Collect rebuilds the same row.
        $code = if ($a.Proc.HasExited) { $a.Proc.ExitCode } else { -1 }
        Set-Content -LiteralPath (Join-Path $a.Folder 'problem.txt') -Value $problem
        if (-not (Test-Path (Join-Path $a.Folder 'exit.txt'))) {
          Set-Content -LiteralPath (Join-Path $a.Folder 'exit.txt') `
            -Value ("$code " + (Get-Date).ToString('o'))
        }
      } else {
        Stop-TrialGame $a  # smoke-test.ps1 stops its own; this is belt and braces
      }
      $exit = if ($a.Proc.HasExited) { $a.Proc.ExitCode } else { $null }
      $result = Get-TrialRow $a.Folder $problem $exit -Live
      $results.Add($result)
      $active.Remove($a) | Out-Null
      $r = $result.Row
      "  ended   $($r['run']): exit $($r['exitCode']), passed $($r['passed']), " +
        "failed $($r['failed']), $($r['wallSeconds']) s" +
        $(if ($r['error']) { "; $($r['error'])" } else { '' })
    }
  }
} finally {
  # On Ctrl+C or a failure: stop each run's shell first, so it starts no
  # game, then read the pid of any game it did start (its line may have
  # come since the last poll), then stop that game; stopping a shell does
  # not stop the RimWorld it started.
  foreach ($a in $active.ToArray()) {
    if (-not $a.Proc.HasExited) { Stop-Process -Id $a.Proc.Id -Force -ErrorAction SilentlyContinue }
    Update-GamePid $a
    Stop-TrialGame $a
  }
}

$missing = Write-Results $results
$missing | Select-Object -SkipLast 1
exit [int](($missing | Select-Object -Last 1) -gt 0)
