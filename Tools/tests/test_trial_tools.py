"""Tests for Tools/tour-trials.ps1 and the path handling of smoke-test.ps1.

Run from the repository root:

    python -m unittest discover -s Tools/tests -v

They drive the PowerShell scripts themselves, under every PowerShell
edition found (PowerShell 7's pwsh and Windows PowerShell 5.1's
powershell; the scripts promise both), without starting RimWorld: the
results are rebuilt with -Collect from trial folders made here, a run is
started against a fake RimWorld folder and a mod root with no build, so
smoke-test.ps1 stops at once, and tour-trials.ps1 is dot-sourced to call
its functions. Each test works in its own folder under the repository's
.local/tmp/, removed afterwards. Skipped where no PowerShell is found.
"""

from __future__ import annotations

import csv
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
TRIALS = REPO / "Tools" / "tour-trials.ps1"
SMOKE = REPO / "Tools" / "smoke-test.ps1"


def editions() -> list[str]:
    """PowerShell executables found on this machine.

    Returns:
        The paths of pwsh and powershell, those that exist.
    """
    found = []
    for name in ("pwsh", "powershell"):
        path = shutil.which(name)
        if path:
            found.append(path)
    return found


def run_ps(shell: str, args: list[str], cwd: Path,
           timeout: int = 180) -> subprocess.CompletedProcess:
    """Runs PowerShell with arguments and captures its output.

    Args:
        shell: The PowerShell executable.
        args: Its arguments after -NoProfile -ExecutionPolicy Bypass.
        cwd: The working folder.
        timeout: Seconds before giving up.

    Returns:
        The finished process, with stdout and stderr as text.
    """
    return subprocess.run(
        [shell, "-NoProfile", "-ExecutionPolicy", "Bypass", *args],
        cwd=str(cwd), capture_output=True, text=True, timeout=timeout,
        encoding="utf-8", errors="replace")


class Workspace(unittest.TestCase):
    """Gives each test its own folder under .local/tmp, and a shell list."""

    def setUp(self) -> None:
        """Makes the folder and finds the shells, or skips."""
        self.shells = editions()
        if not self.shells:
            self.skipTest("no PowerShell found")
        root = REPO / ".local" / "tmp"
        root.mkdir(parents=True, exist_ok=True)
        self.work = Path(tempfile.mkdtemp(prefix="test-trial-tools-", dir=root))

    def tearDown(self) -> None:
        """Removes the folder made for the test."""
        shutil.rmtree(self.work, ignore_errors=True)

    def fake_rimworld(self) -> Path:
        """A folder with an empty RimWorldWin64.exe, which no run starts.

        Returns:
            The folder.
        """
        folder = self.work / "FakeRimWorld"
        folder.mkdir(exist_ok=True)
        (folder / "RimWorldWin64.exe").write_bytes(b"")
        return folder

    def fake_mod_root(self) -> Path:
        """A mod root with no build, so smoke-test.ps1 stops before linking.

        Returns:
            The folder.
        """
        folder = self.work / "FakeMod"
        folder.mkdir(exist_ok=True)
        return folder


def write_run(folder: Path, name: str, seed: str, *, map_hash: str | None,
              stage_hash: str | None, exit_line: str | None = None,
              problem: str | None = None, summary: dict | None = None,
              log: bool = True) -> None:
    """Writes one trial's folder as tour-trials.ps1 and the game would.

    Args:
        folder: The trials folder.
        name: The run's folder name.
        seed: The seed in trial.json and the SEED cue.
        map_hash: The MAPHASH cue's hex, or None for none.
        stage_hash: The STAGEHASH cue's hex, or None for none.
        exit_line: exit.txt's text, or None for no exit record.
        problem: problem.txt's text, or None.
        summary: The SUMMARY's JSON object, or None for no SUMMARY.
        log: Whether the run has a Player.log at all.
    """
    run = folder / name
    run.mkdir(parents=True)
    trial = {"name": name, "seed": seed, "repeat": 1,
             "started": "2026-10-14T20:00:00.0000000-07:00"}
    (run / "trial.json").write_text(json.dumps(trial), encoding="utf-8")
    if exit_line is not None:
        (run / "exit.txt").write_text(exit_line, encoding="utf-8")
    if problem is not None:
        (run / "problem.txt").write_text(problem, encoding="utf-8")
    if not log:
        return
    lines = [f"[BT-SHOWCASE] SEED {seed} (command line)"]
    if map_hash:
        lines.append(f"[BT-SHOWCASE] MAPHASH {map_hash} (1 cells, 1 things, 1 pawns)")
    if stage_hash:
        lines.append(f"[BT-SHOWCASE] STAGEHASH {stage_hash} (1 cells, 1 things, 1 pawns)")
    if summary is not None:
        lines.append("[BT-TOUR] SUMMARY " + json.dumps(summary))
        lines.append("[BT-SMOKE] DONE passed=18 failed=0")
    (run / "Player.log").write_text("\n".join(lines) + "\n", encoding="utf-8")


class CollectTests(unittest.TestCase):
    """-Collect rebuilds results honestly from trial folders.

    The fixture runs are made and collected once per PowerShell edition;
    each test checks one behaviour in every edition's results.
    """

    work: Path
    results: dict[str, tuple[str, dict[str, dict]]]

    @classmethod
    def setUpClass(cls) -> None:
        """Makes the fixture runs and collects them under each edition."""
        shells = editions()
        if not shells:
            raise unittest.SkipTest("no PowerShell found")
        root = REPO / ".local" / "tmp"
        root.mkdir(parents=True, exist_ok=True)
        cls.work = Path(tempfile.mkdtemp(prefix="test-trial-tools-", dir=root))
        cls.results = {Path(shell).stem: cls.collect(shell) for shell in shells}

    @classmethod
    def tearDownClass(cls) -> None:
        """Removes the folder made for the fixtures."""
        shutil.rmtree(cls.work, ignore_errors=True)

    @classmethod
    def collect(cls, shell: str) -> tuple[str, dict[str, dict]]:
        """Builds the fixture runs and collects them.

        Args:
            shell: The PowerShell executable.

        Returns:
            The script's printed output, and results.csv's rows by run.
        """
        trials = cls.work / f"trials-{Path(shell).stem}"
        ok = {"version": 3, "finished": True, "unpinnedLayouts": 0}
        # alpha: one run with hashes, one that left no log at all.
        write_run(trials, "alpha-r1", "alpha", map_hash="aaaa000000000001",
                  stage_hash="bbbb000000000001", summary=ok,
                  exit_line="0 2026-10-14T20:05:00.0000000-07:00")
        write_run(trials, "alpha-r2", "alpha", map_hash=None, stage_hash=None,
                  log=False)
        # beta: two runs, neither with a hash.
        write_run(trials, "beta-r1", "beta", map_hash=None, stage_hash=None, log=False)
        write_run(trials, "beta-r2", "beta", map_hash=None, stage_hash=None, log=False)
        # gamma: two agree, one has none.
        for i in (1, 2):
            write_run(trials, f"gamma-r{i}", "gamma", map_hash="cccc000000000001",
                      stage_hash="dddd000000000001", summary=ok)
        write_run(trials, "gamma-r3", "gamma", map_hash=None, stage_hash=None, log=False)
        # delta: two that differ.
        write_run(trials, "delta-r1", "delta", map_hash="eeee000000000001",
                  stage_hash="ffff000000000001", summary=ok)
        write_run(trials, "delta-r2", "delta", map_hash="eeee000000000002",
                  stage_hash="ffff000000000001", summary=ok)
        # Omega and omega: two seeds that differ only in case, not repeats.
        write_run(trials, "case1-r1", "Omega", map_hash="1111000000000001",
                  stage_hash="2222000000000001", summary=ok)
        write_run(trials, "case2-r1", "omega", map_hash="1111000000000002",
                  stage_hash="2222000000000002", summary=ok)
        # A run the watcher stopped: its reason and end recorded by it.
        write_run(trials, "stopped-r1", "stopped", map_hash=None, stage_hash=None,
                  problem="timed out after 1740 s; stopped",
                  exit_line="-1 2026-10-14T20:29:00.0000000-07:00")
        # A map with a layout whose rooms are not pinned.
        layout = dict(ok, unpinnedLayouts=1, landmark="AncientGarrison")
        write_run(trials, "ruins-r1", "ruins", map_hash="3333000000000001",
                  stage_hash="4444000000000001", summary=layout,
                  exit_line="0 2026-10-14T20:07:30.0000000-07:00")
        result = run_ps(shell, ["-File", str(TRIALS), "-Collect", trials.name],
                        cwd=cls.work)
        out = result.stdout + result.stderr
        rows: dict[str, dict] = {}
        csv_path = trials / "results.csv"
        if csv_path.exists():
            with open(csv_path, encoding="utf-8-sig", newline="") as f:
                rows = {r["run"]: r for r in csv.DictReader(f)}
        return out, rows

    def editions(self) -> list[tuple[str, str, dict[str, dict]]]:
        """Each edition's name, output and rows, for a test to walk.

        Returns:
            The edition, the output and the rows by run, per edition.
        """
        return [(edition, out, rows)
                for edition, (out, rows) in self.results.items()]

    def test_one_hash_is_not_called_same(self) -> None:
        """A seed with one hash among its runs is not compared."""
        for edition, out, _ in self.editions():
            with self.subTest(edition=edition):
                self.assertIn(
                    "alpha: 2 runs; MAPHASH not compared (1 of 2 runs; 1 without "
                    "one); STAGEHASH not compared (1 of 2 runs; 1 without one)", out)

    def test_no_hash_is_not_called_differs(self) -> None:
        """A seed none of whose runs logged a hash is not compared."""
        for edition, out, _ in self.editions():
            with self.subTest(edition=edition):
                self.assertIn(
                    "beta: 2 runs; MAPHASH not compared (0 of 2 runs; 2 without one)",
                    out)

    def test_agreeing_and_differing_hashes(self) -> None:
        """Runs that agree are the same; runs that disagree differ."""
        for edition, out, _ in self.editions():
            with self.subTest(edition=edition):
                self.assertIn(
                    "gamma: 3 runs; MAPHASH same (2 of 3 runs; 1 without one)", out)
                self.assertIn(
                    "delta: 2 runs; MAPHASH DIFFERS (2 values in 2 of 2 runs); "
                    "STAGEHASH same (2 of 2 runs)", out)

    def test_seeds_differing_in_case_are_not_repeats(self) -> None:
        """Omega and omega are two seeds, so neither is a repeat."""
        for edition, out, _ in self.editions():
            with self.subTest(edition=edition):
                self.assertNotIn("Omega:", out)
                self.assertNotIn("omega:", out)

    def test_wall_seconds_need_a_recorded_end(self) -> None:
        """No recorded end, no wall time; a recorded one gives it."""
        for edition, _, rows in self.editions():
            with self.subTest(edition=edition):
                self.assertEqual("300", rows["alpha-r1"]["wallSeconds"])
                self.assertEqual("", rows["gamma-r1"]["wallSeconds"])
                self.assertEqual("", rows["alpha-r2"]["wallSeconds"])

    def test_a_stopped_runs_problem_is_read_back(self) -> None:
        """problem.txt and the watcher's exit.txt rebuild the row."""
        for edition, _, rows in self.editions():
            with self.subTest(edition=edition):
                self.assertEqual("timed out after 1740 s; stopped",
                                 rows["stopped-r1"]["error"])
                self.assertEqual("1740", rows["stopped-r1"]["wallSeconds"])
                self.assertEqual("-1", rows["stopped-r1"]["exitCode"])

    def test_maps_with_layouts_are_listed(self) -> None:
        """A run whose map holds a layout is named, with its landmark."""
        for edition, out, _ in self.editions():
            with self.subTest(edition=edition):
                self.assertIn("== Maps holding layouts", out)
                self.assertIn("ruins-r1 (seed ruins): 1 layout(s); landmark "
                              "AncientGarrison", out)
                self.assertNotIn("gamma-r1 (seed gamma)", out)


class StartTests(Workspace):
    """What tour-trials.ps1 does before and as it starts runs."""

    def test_duplicate_seeds_are_refused(self) -> None:
        """A seed given twice, or twice but for case, starts nothing."""
        rimworld = self.fake_rimworld()
        mod = self.fake_mod_root()
        cases = [("a,A", "a"), ("tour-01", "tour-01")]
        for shell in self.shells:
            for i, (seeds, named) in enumerate(cases):
                with self.subTest(shell=Path(shell).stem, seeds=seeds):
                    out_dir = self.work / f"dupes-{Path(shell).stem}-{i}"
                    args = ["-File", str(TRIALS), "-Seeds", seeds, "-RimWorld",
                            str(rimworld), "-ModRoot", str(mod), "-OutDir",
                            str(out_dir), "-TimeoutSeconds", "30"]
                    if seeds == "tour-01":
                        args += ["-Count", "1"]
                    result = run_ps(shell, args, cwd=self.work)
                    out = result.stdout + result.stderr
                    self.assertNotEqual(0, result.returncode)
                    self.assertIn("Seeds repeat", out)
                    self.assertIn(named, out)
                    self.assertFalse(out_dir.exists(), "no run was started")

    def test_relative_out_dir_is_made_absolute(self) -> None:
        """A relative -OutDir reaches the run, and its data folder, whole."""
        rimworld = self.fake_rimworld()
        mod = self.fake_mod_root()
        for shell in self.shells:
            with self.subTest(shell=Path(shell).stem):
                relative = Path("rel") / f"trials-{Path(shell).stem}"
                result = run_ps(shell, [
                    "-File", str(TRIALS), "-Seeds", "solo", "-Parallel", "1",
                    "-RimWorld", str(rimworld), "-ModRoot", str(mod),
                    "-OutDir", str(relative), "-TimeoutSeconds", "30"],
                    cwd=self.work)
                out = result.stdout + result.stderr
                absolute = self.work / relative
                self.assertIn(f"into {absolute}", out)
                smoke_out = (absolute / "solo-r1" / "smoke-test.out.txt").read_text(
                    encoding="utf-8", errors="replace")
                self.assertIn(f"Data folder: {absolute / 'solo-r1'}", smoke_out)
                self.assertIn("Build the mod first", smoke_out)
                self.assertTrue((absolute / "results.csv").exists())


class SmokeScriptTests(Workspace):
    """smoke-test.ps1 hands RimWorld absolute paths."""

    def test_relative_data_folder_is_made_absolute(self) -> None:
        """A relative -DataFolder is taken from the current folder."""
        for shell in self.shells:
            with self.subTest(shell=Path(shell).stem):
                result = run_ps(shell, [
                    "-File", str(SMOKE), "-DataFolder", r"rel\data",
                    "-RimWorld", str(self.work / "NoRimWorldHere")], cwd=self.work)
                out = result.stdout + result.stderr
                self.assertNotEqual(0, result.returncode)
                self.assertIn(f"Data folder: {self.work / 'rel' / 'data'}", out)
                self.assertIn("RimWorld not found", out)


class GameTrackingTests(Workspace):
    """Only a game tied to a run by pid and start time is ever stopped."""

    def test_own_game_needs_a_recorded_start(self) -> None:
        """No start time recorded, or a gone pid, is never the run's game."""
        out_file = self.work / "smoke-test.out.txt"
        # 999999 is no Windows pid: they are multiples of four.
        out_file.write_text("Started RimWorld (pid 999999); log: x\n", encoding="utf-8")
        script = (
            f". '{TRIALS}'\n"
            "$t = [datetime]'2026-10-14T20:00:00'\n"
            "$game = [pscustomobject]@{ Id = 4242; ProcessName = 'RimWorldWin64'; "
            "StartTime = $t }\n"
            "$run = { param($started, $gone) [pscustomobject]@{ GamePid = 4242; "
            "GameStarted = $started; GameGone = $gone } }\n"
            "'none=' + (Test-OwnGame $game (& $run $null $false))\n"
            "'same=' + (Test-OwnGame $game (& $run $t $false))\n"
            "'other=' + (Test-OwnGame $game (& $run $t.AddSeconds(1) $false))\n"
            "'gone=' + (Test-OwnGame $game (& $run $t $true))\n"
            "$active = [pscustomobject]@{ GamePid = $null; GameStarted = $null; "
            f"GameGone = $false; Out = '{out_file}' }}\n"
            "Update-GamePid $active\n"
            "'pid=' + $active.GamePid + ' gone=' + $active.GameGone\n")
        for shell in self.shells:
            with self.subTest(shell=Path(shell).stem):
                result = run_ps(shell, ["-Command", script], cwd=self.work)
                out = result.stdout + result.stderr
                self.assertIn("none=False", out)
                self.assertIn("same=True", out)
                self.assertIn("other=False", out)
                self.assertIn("gone=False", out)
                self.assertIn("pid=999999 gone=True", out)


if __name__ == "__main__":
    unittest.main()
