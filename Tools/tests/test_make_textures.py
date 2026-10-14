"""Tests for the showcase preview in Tools/make_textures.py.

Run from the repository root:

    python -m unittest discover -s Tools/tests -v

Needs Pillow, as the generator does; without it the tests are skipped.
They render into a temporary folder, never into Textures/.
"""

from __future__ import annotations

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
    import make_textures as mt  # noqa: E402  (path set above)
    from PIL import Image
except ImportError:  # Pillow missing: skip, as the docstring says
    mt = None

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


@unittest.skipIf(mt is None, 'Pillow is not installed')
class ShowcasePreviewTests(unittest.TestCase):
    """The preview renders at its documented size from the mod's sprites."""

    def setUp(self) -> None:
        """Point the generator at a temporary Textures folder holding
        copies of the committed sprites."""
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

    def test_preview_is_512_by_288_rgba(self) -> None:
        """The preview is the 16:9 size the menus are laid out for."""
        mt.showcase_preview()
        with Image.open(Path(self.temp, 'UI/Showcases/BT_Showcase.png')) as im:
            self.assertEqual((512, 288), im.size)
            self.assertEqual('RGBA', im.mode)
            self.assertEqual(255, im.getpixel((0, 0))[3])  # opaque, framed

    def test_preview_matches_the_committed_texture(self) -> None:
        """The committed preview is what the generator makes from the
        committed sprites, so neither drifts from the other."""
        mt.showcase_preview()
        made = Path(self.temp, 'UI/Showcases/BT_Showcase.png')
        with Image.open(made) as a, \
                Image.open(REPO / 'Textures/UI/Showcases/BT_Showcase.png') as b:
            self.assertEqual(a.size, b.size)
            self.assertEqual(a.convert("RGBA").tobytes(), b.convert("RGBA").tobytes())

    def test_preview_needs_the_sprites(self) -> None:
        """Without its sprites the preview refuses, rather than drawing a
        picture with holes in it."""
        os.remove(Path(self.temp, SPRITES[0]))
        with self.assertRaises(FileNotFoundError):
            mt.showcase_preview()


if __name__ == '__main__':
    unittest.main()
