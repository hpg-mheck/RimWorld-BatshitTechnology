"""Tests for Tools/make_textures.py: the showcase preview and the command line.

Run from the repository root:

    python -m unittest discover -s Tools/tests -v

Needs Pillow, as the generator does, at the version pinned in
Tools/requirements.txt (pip install -r Tools/requirements.txt, or put a
local copy on PYTHONPATH). Without Pillow these tests are skipped, so a
contributor without it still gets a green run; verification runs set
BT_REQUIRE_PILLOW=1, which turns a missing Pillow into an error instead.
Only a missing Pillow skips: any other import failure in make_textures.py
fails loudly. The tests render into a temporary folder, never into
Textures/.
"""

from __future__ import annotations

import contextlib
import inspect
import io
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parent.parent
REPO = TOOLS.parent
sys.path.insert(0, str(TOOLS))

try:
    import PIL
    from PIL import Image, ImageChops
except ImportError:  # Pillow missing: skip, unless a run demands it
    PIL = None
if PIL is None and os.environ.get('BT_REQUIRE_PILLOW'):
    raise RuntimeError('BT_REQUIRE_PILLOW is set, but Pillow is not installed')

mt = None
if PIL is not None:
    import make_textures as mt  # noqa: E402  a broken import now errors

SPRITES = (
    'Things/Mote/BT_SingularityVortexOuter.png',
    'Things/Mote/BT_SingularityVortex.png',
    'Things/Building/Production/BT_RandomReplicator.png',
    'Things/Building/Power/BT_BatshittiumReactor.png',
    'Things/Building/Production/BT_BatshittiumTransmogrifier.png',
    'Things/Building/Production/BT_BatshittiumArtificer.png',
    'Things/Building/Joy/BT_BrainSqueezer.png',
    'Things/Building/Security/BT_BatshittiumBallista_Base.png',
    'Things/Building/Security/BT_BatshittiumBallista_Top.png',
    'Things/Building/Security/BT_BallistaMagazine.png',
)

PREVIEW = 'UI/Showcases/BT_Showcase.png'


def pinned_pillow() -> str:
    """The Pillow version Tools/requirements.txt pins, or 'unpinned'."""
    for line in (TOOLS / 'requirements.txt').read_text(encoding='utf-8').splitlines():
        line = line.strip()
        if line.lower().startswith('pillow=='):
            return line.split('==', 1)[1]
    return 'unpinned'


class TemporaryTextures(unittest.TestCase):
    """Points the generator at a temporary Textures folder holding copies of
    the committed sprites, and puts it back afterwards."""

    def setUp(self) -> None:
        """Copy the committed sprites into a temporary Textures folder."""
        self.temp = tempfile.mkdtemp()
        for rel in SPRITES:
            target = Path(self.temp, rel)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy(REPO / 'Textures' / rel, target)
        self.saved = mt.TEXTURES
        mt.TEXTURES = self.temp

    def tearDown(self) -> None:
        """Restore the generator's folder and remove the temporary one."""
        mt.TEXTURES = self.saved
        shutil.rmtree(self.temp, ignore_errors=True)

    def files(self) -> set[str]:
        """Every file under the temporary Textures folder, relative to it."""
        root = Path(self.temp)
        return {str(p.relative_to(root)).replace(os.sep, '/')
                for p in root.rglob('*') if p.is_file()}

    def run_main(self, *names: str) -> tuple[int, str]:
        """Runs the command line with names; returns its status and stderr."""
        err = io.StringIO()
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(err):
            status = mt.main(list(names))
        return status, err.getvalue()


@unittest.skipIf(PIL is None, 'Pillow is not installed')
class ShowcasePreviewTests(TemporaryTextures):
    """The preview renders at its documented size from the mod's sprites."""

    def test_preview_is_512_by_288_rgba(self) -> None:
        """The preview is the 16:9 size the menus are laid out for."""
        mt.showcase_preview()
        with Image.open(Path(self.temp, PREVIEW)) as im:
            self.assertEqual((512, 288), im.size)
            self.assertEqual('RGBA', im.mode)
            self.assertEqual(255, im.getpixel((0, 0))[3])  # opaque, framed

    def test_preview_matches_the_committed_texture(self) -> None:
        """The committed preview is what the generator makes from the
        committed sprites, so neither drifts from the other. The match is
        exact, since Pillow is pinned; a failure names the region that
        differs, how much, and the Pillow versions running and pinned."""
        mt.showcase_preview()
        made = Path(self.temp, PREVIEW)
        with Image.open(made) as a, Image.open(REPO / 'Textures' / PREVIEW) as b:
            self.assertEqual(a.size, b.size)
            diff = ImageChops.difference(a.convert('RGBA'), b.convert('RGBA'))
            box = diff.getbbox()
            self.assertIsNone(
                box,
                f'preview differs in {box}; per-channel extrema {diff.getextrema()}; '
                f'Pillow {PIL.__version__} running, {pinned_pillow()} pinned')

    def test_preview_needs_the_sprites(self) -> None:
        """Without its sprites the preview refuses, rather than drawing a
        picture with holes in it."""
        os.remove(Path(self.temp, SPRITES[0]))
        with self.assertRaises(FileNotFoundError):
            mt.showcase_preview()


@unittest.skipIf(PIL is None, 'Pillow is not installed')
class CommandLineTests(TemporaryTextures):
    """The command line runs only real generators, and checks every name
    before drawing anything."""

    def test_a_misspelt_name_is_refused_with_usage(self) -> None:
        """A typo exits with status 2 and a usage line, and draws nothing."""
        before = self.files()
        status, err = self.run_main('showcase_previw')
        self.assertEqual(2, status)
        self.assertIn('usage: python Tools/make_textures.py', err)
        self.assertIn('showcase_preview', err)
        self.assertEqual(before, self.files())

    def test_a_helper_is_not_a_generator(self) -> None:
        """Module-level helpers and imports are not runnable by name."""
        for name in ('save', 'glow', 'place', 'os', 'main'):
            with self.subTest(name=name):
                status, _ = self.run_main(name)
                self.assertEqual(2, status)
        self.assertNotIn(PREVIEW, self.files())

    def test_names_are_checked_before_anything_runs(self) -> None:
        """A good name before a bad one is not drawn either."""
        status, _ = self.run_main('showcase_preview', 'nope')
        self.assertEqual(2, status)
        self.assertNotIn(PREVIEW, self.files())

    def test_a_known_name_runs(self) -> None:
        """A known name draws its texture and exits with status 0."""
        status, err = self.run_main('showcase_preview')
        self.assertEqual(0, status)
        self.assertEqual('', err)
        self.assertIn(PREVIEW, self.files())

    def test_every_generator_takes_no_arguments(self) -> None:
        """Every listed generator runs with no arguments, as the command
        line calls it, and the preview comes last, after its sprites."""
        for name, fn in mt.GENERATORS.items():
            with self.subTest(name=name):
                required = [p for p in inspect.signature(fn).parameters.values()
                            if p.default is inspect.Parameter.empty
                            and p.kind in (p.POSITIONAL_ONLY, p.POSITIONAL_OR_KEYWORD)]
                self.assertEqual([], required)
        self.assertEqual('showcase_preview', list(mt.GENERATORS)[-1])


if __name__ == '__main__':
    unittest.main()
