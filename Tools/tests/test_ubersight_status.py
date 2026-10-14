"""Tests for Tools/ubersight_status.py.

Run from the repository root:

    python -m unittest discover -s Tools/tests -v

The compatibility test reads our output with Ubersight's own reader when an
Ubersight checkout is found (UBERSIGHT_SOURCE, or ../Ubersight/src beside
this repository) and importable on this platform; otherwise it is skipped.
"""

from __future__ import annotations

import datetime
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(TOOLS))

import ubersight_status as us  # noqa: E402  (path set above)


def rows(*values: str, kind: str = "slice") -> list[us.Row]:
    """Parse several rows.

    Args:
        *values: ``ID:STATE:TITLE`` texts.
        kind: "phase" or "slice".

    Returns:
        The parsed rows.
    """
    return [us.parse_row(value, kind) for value in values]


class ParseRowTests(unittest.TestCase):
    """``ID:STATE:TITLE`` parsing matches Ubersight's CLI."""

    def test_parses_and_strips(self) -> None:
        """A well-formed row parses, trimmed, and titles keep colons."""
        row = us.parse_row(" p1 : active : Build: the barn ", "phase")
        self.assertEqual(us.Row("p1", "active", "Build: the barn"), row)

    def test_rejects_bad_rows(self) -> None:
        """Missing parts, empty IDs or titles, and unknown states fail."""
        for bad in ("p1:active", ":active:x", "p1:running:x", "p1:done: ",
                    "p1:complete:x", "p1:deferred:x"):
            with self.subTest(bad=bad):
                with self.assertRaises(ValueError):
                    us.parse_row(bad, "slice")


class ValidateTests(unittest.TestCase):
    """The ``--write-status`` rules."""

    def test_accepts_a_normal_status(self) -> None:
        """One active phase and one active slice is valid."""
        active = us.validate(
            rows("a:done:A", "b:active:B", kind="phase"),
            rows("s:active:S", "t:pending:T"), complete=False, notes="")
        self.assertEqual("b", active.row_id)

    def test_rejects_rule_breaks(self) -> None:
        """Each of Ubersight's rules is enforced."""
        cases = [
            ([], rows("s:active:S"), False, "no phases"),
            (rows("a:done:A", kind="phase"), rows("s:active:S"), False,
             "no active phase"),
            (rows("a:active:A", "b:active:B", kind="phase"),
             rows("s:active:S"), False, "two active phases"),
            (rows("a:active:A", kind="phase"), [], False, "no slices"),
            (rows("a:active:A", kind="phase"),
             rows("s:active:S", "t:active:T"), False, "two active slices"),
            (rows("a:active:A", kind="phase"), rows("s:done:S"), False,
             "no active slice, not complete"),
        ]
        for phases, slices, complete, why in cases:
            with self.subTest(why=why):
                with self.assertRaises(ValueError):
                    us.validate(phases, slices, complete, "")

    def test_complete_needs_no_active_slice(self) -> None:
        """A complete status may have every slice done."""
        us.validate(rows("a:active:A", kind="phase"), rows("s:done:S"),
                    complete=True, notes="")

    def test_bounds_notes(self) -> None:
        """Overlong notes are refused."""
        with self.assertRaises(ValueError):
            us.validate(rows("a:active:A", kind="phase"), rows("s:active:S"),
                        complete=False, notes="x" * (us.MAX_NOTES + 1))


class PayloadTests(unittest.TestCase):
    """The document matches Ubersight's ``write_status_file``."""

    def test_keys_and_values(self) -> None:
        """Every key Ubersight writes, with the active phase summarised."""
        when = datetime.datetime(2026, 10, 13, 20, 30, 0,
                                 tzinfo=datetime.timezone.utc)
        payload = us.build_payload(
            rows("a:done:A", "b:active:B", kind="phase"),
            rows("s:active:S"), notes="  hello  ", now=when)
        self.assertEqual(
            {"schema", "updated_at", "phase", "phases", "slices", "notes",
             "blocked", "complete"}, set(payload))
        self.assertEqual("ubersight.status.v1", payload["schema"])
        self.assertEqual({"id": "b", "title": "B"}, payload["phase"])
        self.assertEqual({"id": "s", "title": "S", "state": "active"},
                         payload["slices"][0])
        self.assertEqual("hello", payload["notes"])
        self.assertFalse(payload["blocked"])
        self.assertFalse(payload["complete"])
        stamp = datetime.datetime.fromisoformat(payload["updated_at"])
        self.assertIsNotNone(stamp.tzinfo, "updated_at is timezone-aware")


class WriteTests(unittest.TestCase):
    """Atomic writing and the command line."""

    def test_writes_atomically_and_leaves_no_temp_file(self) -> None:
        """The file is replaced whole, and no temporary file remains."""
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "ubersight" / "status.json"
            code = us.main(["--phase-row", "p:active:P", "--slice",
                            "s:active:S", "--path", str(path)])
            self.assertEqual(0, code)
            data = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual("ubersight.status.v1", data["schema"])
            self.assertEqual([path.name], os.listdir(path.parent))

    def test_invalid_input_writes_nothing(self) -> None:
        """A rule break exits 2 and leaves no file."""
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "status.json"
            code = us.main(["--phase-row", "p:done:P", "--slice",
                            "s:active:S", "--path", str(path)])
            self.assertEqual(2, code)
            self.assertFalse(path.exists())


class UbersightCompatibilityTests(unittest.TestCase):
    """Ubersight's own reader accepts what we write."""

    @staticmethod
    def find_dashboard():
        """Import Ubersight's dashboard module, if a checkout is usable.

        Returns:
            The module, or None if no checkout was found or it cannot be
            imported on this platform.
        """
        candidates = []
        if os.environ.get("UBERSIGHT_SOURCE"):
            candidates.append(Path(os.environ["UBERSIGHT_SOURCE"]))
        repo = TOOLS.parent
        candidates.append(repo.parent / "Ubersight" / "src")
        for source in candidates:
            if (source / "ubersight" / "dashboard.py").exists():
                sys.path.insert(0, str(source))
                try:
                    from ubersight import dashboard  # type: ignore
                    return dashboard
                except Exception:  # noqa: BLE001  platform-specific imports
                    return None
                finally:
                    sys.path.remove(str(source))
        return None

    def test_ubersight_reads_our_status(self) -> None:
        """load_status parses our file into the same phases and slices."""
        dashboard = self.find_dashboard()
        if dashboard is None:
            self.skipTest("no importable Ubersight checkout found")
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "status.json"
            us.main(["--phase-row", "a:done:Alpha", "--phase-row",
                     "b:active:Beta", "--slice", "s:active:Sigma",
                     "--slice", "t:pending:Tau", "--notes", "note",
                     "--path", str(path)])
            status = dashboard.load_status(path)
            self.assertEqual("b", status.phase_id)
            self.assertEqual(["a", "b"], [p.phase_id for p in status.phases])
            self.assertEqual(["s", "t"], [s.slice_id for s in status.slices])
            self.assertEqual("note", status.notes)


if __name__ == "__main__":
    unittest.main()
