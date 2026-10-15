"""Tests for Tools/contact-sheet.ps1, which tiles a run's frames into sheets.

Run from the repository root:

    python -m unittest discover -s Tools/tests -v

They make small PNG frames here (with zlib, since this Python has no
imaging library), run the script under every PowerShell edition found
(pwsh and Windows PowerShell 5.1), and read the sheets' sizes back from
their PNG headers. Each test works in its own folder under the
repository's .local/tmp/, removed afterwards. Skipped where no PowerShell
is found.
"""

from __future__ import annotations

import shutil
import struct
import subprocess
import tempfile
import unittest
import zlib
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SHEET = REPO / "Tools" / "contact-sheet.ps1"


def editions() -> list[str]:
    """PowerShell executables found on this machine.

    Returns:
        The paths of pwsh and powershell, those that exist.
    """
    return [p for p in (shutil.which("pwsh"), shutil.which("powershell")) if p]


def write_png(path: Path, width: int, height: int,
              rgb: tuple[int, int, int]) -> None:
    """Writes a solid-colour RGB PNG.

    Args:
        path: Where to write it.
        width: Its width in pixels.
        height: Its height in pixels.
        rgb: Its colour.
    """
    row = b"\x00" + bytes(rgb) * width
    raw = row * height

    def chunk(kind: bytes, data: bytes) -> bytes:
        """One PNG chunk: length, type, data and CRC.

        Args:
            kind: The four-byte chunk type.
            data: The chunk's data.

        Returns:
            The chunk's bytes.
        """
        return (struct.pack(">I", len(data)) + kind + data
                + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF))

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
                     + chunk(b"IDAT", zlib.compress(raw))
                     + chunk(b"IEND", b""))


def png_size(path: Path) -> tuple[int, int]:
    """Reads a PNG's width and height from its header.

    Args:
        path: The PNG.

    Returns:
        Its width and height.
    """
    data = path.read_bytes()[:24]
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise AssertionError(f"{path} is not a PNG")
    return struct.unpack(">II", data[16:24])


def run_sheet(shell: str, args: list[str], cwd: Path) -> subprocess.CompletedProcess:
    """Runs contact-sheet.ps1 with arguments and captures its output.

    Args:
        shell: The PowerShell executable.
        args: The script's arguments.
        cwd: The working folder.

    Returns:
        The finished process, with stdout and stderr as text.
    """
    return subprocess.run(
        [shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(SHEET),
         *args],
        cwd=str(cwd), capture_output=True, text=True, timeout=120,
        encoding="utf-8", errors="replace")


class ContactSheetTests(unittest.TestCase):
    """contact-sheet.ps1 groups, lays out and labels frames."""

    FRAMES = ["01-tour-ready", "02-home-scene", "03-home-beat-reactor",
              "04-retire-reactor-scene", "05-retire-reactor-end-completed"]

    def setUp(self) -> None:
        """Makes a data folder with a shots folder of five frames, or skips."""
        self.shells = editions()
        if not self.shells:
            self.skipTest("no PowerShell found")
        root = REPO / ".local" / "tmp"
        root.mkdir(parents=True, exist_ok=True)
        self.work = Path(tempfile.mkdtemp(prefix="test-contact-sheet-", dir=root))
        self.shots = self.work / "run" / "shots"
        self.shots.mkdir(parents=True)
        for i, name in enumerate(self.FRAMES):
            write_png(self.shots / f"{name}.png", 64, 36, (40 * i, 90, 160))

    def tearDown(self) -> None:
        """Removes the folder made for the test."""
        shutil.rmtree(self.work, ignore_errors=True)

    def test_one_sheet_per_scene(self) -> None:
        """READY joins the first scene; a hyphenated scene stays whole."""
        for shell in self.shells:
            with self.subTest(shell=Path(shell).stem):
                out = self.work / f"sheets-{Path(shell).stem}"
                result = run_sheet(shell, ["-Path", str(self.work / "run"),
                                           "-OutDir", str(out), "-Columns", "2",
                                           "-TileWidth", "64"], self.work)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual(
                    ["01-home.png", "02-retire-reactor.png"],
                    sorted(p.name for p in out.glob("*.png")))
                # Three frames in two columns: two rows of 36 + 24 band,
                # with 8-pixel gaps around and between.
                self.assertEqual((2 * 64 + 3 * 8, 2 * 60 + 3 * 8),
                                 png_size(out / "01-home.png"))
                self.assertEqual((2 * 64 + 3 * 8, 60 + 2 * 8),
                                 png_size(out / "02-retire-reactor.png"))
                for name in self.FRAMES:
                    self.assertIn(f"  {name}", result.stdout)

    def test_fixed_count_per_sheet(self) -> None:
        """-PerSheet makes sheets of that many frames, the last fewer."""
        for shell in self.shells:
            with self.subTest(shell=Path(shell).stem):
                out = self.work / f"fixed-{Path(shell).stem}"
                result = run_sheet(shell, ["-Path", str(self.shots), "-OutDir",
                                           str(out), "-PerSheet", "2",
                                           "-TileWidth", "128"], self.work)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                sheets = sorted(p.name for p in out.glob("*.png"))
                self.assertEqual(["sheet-01.png", "sheet-02.png", "sheet-03.png"],
                                 sheets)
                # Frames scale to the tile width: 64 by 36 becomes 128 by 72.
                self.assertEqual((2 * 128 + 3 * 8, 72 + 24 + 2 * 8),
                                 png_size(out / "sheet-01.png"))
                self.assertEqual((128 + 2 * 8, 72 + 24 + 2 * 8),
                                 png_size(out / "sheet-03.png"))

    def test_sheets_go_beside_the_shots_by_default(self) -> None:
        """Without -OutDir, a sheets folder is made beside shots."""
        shell = self.shells[0]
        result = run_sheet(shell, ["-Path", "run"], self.work)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertTrue((self.work / "run" / "sheets" / "01-home.png").exists())

    def test_a_folder_without_frames_fails(self) -> None:
        """A folder with no PNG exits 1 and says so."""
        empty = self.work / "empty"
        empty.mkdir()
        for shell in self.shells:
            with self.subTest(shell=Path(shell).stem):
                result = run_sheet(shell, ["-Path", str(empty)], self.work)
                self.assertNotEqual(0, result.returncode)
                self.assertIn("No PNG frames", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
