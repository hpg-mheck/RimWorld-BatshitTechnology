"""Generate the mod's starter textures into Textures/.

RimWorld-style top-down art drawn procedurally: dark outlines, flat steel shading, and a magenta
glow for anything batshittium. These are placeholders to build and playtest with; replace any PNG
with hand-drawn art at the same path and size.

Requires Pillow (pip install pillow). Run from anywhere:  python Tools/make_textures.py
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

TEXTURES = os.path.normpath(os.path.join(os.path.dirname(__file__), '..', 'Textures'))
SS = 4  # draw at 4x and downsample, for anti-aliased edges

OUTLINE = (30, 26, 36, 255)
# Batshittium
BS_LIGHT = (250, 170, 255, 255)
BS_MID = (205, 80, 235, 255)
BS_DARK = (118, 30, 160, 255)
BS_DEEP = (58, 12, 88, 255)
BS_CORE = (255, 236, 255, 255)
BS_GLOW = (225, 80, 255)
# Steel
STEEL_LIGHT = (178, 182, 190, 255)
STEEL = (140, 144, 152, 255)
STEEL_DARK = (98, 101, 110, 255)
STEEL_DEEP = (62, 64, 72, 255)
# Rock
ROCK_LIGHT = (150, 142, 140, 255)
ROCK = (116, 110, 110, 255)


def s(*values):
    """Final-pixel coordinates to the supersampled canvas."""
    return [v * SS for v in values]


def pts(points):
    return [(x * SS, y * SS) for x, y in points]


def draw(img):
    return ImageDraw.Draw(img, 'RGBA')  # 'RGBA' so translucent fills blend instead of overwrite


def new_canvas(size):
    return Image.new('RGBA', (size * SS, size * SS), (0, 0, 0, 0))


def save(img, size, rel_path):
    path = os.path.join(TEXTURES, rel_path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.resize((size, size), Image.LANCZOS).save(path)
    print('wrote', os.path.relpath(path, os.path.dirname(TEXTURES)))


def glow(img, shape_fn, color, blur, alpha=170):
    """Draw shape_fn on its own layer, blur it, and lay it over img."""
    layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
    shape_fn(draw(layer), (*color, alpha))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur * SS)))


def gradient_fill(img, mask_fn, top, bottom):
    """Fill the region mask_fn draws with a top-to-bottom gradient."""
    w, h = img.size
    column = Image.new('RGBA', (1, h))
    for y in range(h):
        t = y / (h - 1)
        column.putpixel((0, y), tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)))
    mask = Image.new('L', img.size, 0)
    mask_fn(ImageDraw.Draw(mask))
    img.paste(column.resize((w, h)), (0, 0), mask)


def orb(d, cx, cy, r, edge, center, steps=48):
    """Radial gradient disc, edge color at the rim fading to center color."""
    for i in range(steps, 0, -1):
        t = i / steps
        color = tuple(int(c + (e - c) * t) for e, c in zip(edge, center))
        rr = r * t
        d.ellipse(s(cx - rr, cy - rr, cx + rr, cy + rr), fill=color)


def circle(d, cx, cy, r, **kwargs):
    if 'width' in kwargs:
        kwargs['width'] = kwargs['width'] * SS
    d.ellipse(s(cx - r, cy - r, cx + r, cy + r), **kwargs)


def shard(d, base, height, width, angle_deg):
    """One batshittium crystal: lit left face, shaded right face, ridge, outline."""
    bx, by = base
    a = math.radians(angle_deg)
    local = {'bl': (-width / 2, 0), 'tl': (-width / 2, -height * 0.72), 'tip': (0, -height),
             'tr': (width / 2, -height * 0.72), 'br': (width / 2, 0), 'bot': (0, height * 0.14)}

    def at(name):
        x, y = local[name]
        return (bx + x * math.cos(a) - y * math.sin(a), by + x * math.sin(a) + y * math.cos(a))

    d.polygon(pts([at('bl'), at('tl'), at('tip'), at('bot')]), fill=BS_LIGHT)
    d.polygon(pts([at('tip'), at('tr'), at('br'), at('bot')]), fill=BS_DARK)
    d.line(pts([at('tip'), at('bot')]), fill=BS_MID, width=2 * SS)
    pairs = ((at('bl'), at('bot')), (at('tl'), at('tip')))
    edge_in = [(p[0] * 0.8 + q[0] * 0.2, p[1] * 0.8 + q[1] * 0.2) for p, q in pairs]
    d.line(pts(edge_in), fill=(255, 255, 255, 150), width=2 * SS)  # glint along the lit edge
    d.polygon(pts([at(k) for k in ('bl', 'tl', 'tip', 'tr', 'br', 'bot')]), outline=OUTLINE, width=3 * SS)


def sparkle(d, cx, cy, r):
    d.polygon(pts([(cx, cy - r), (cx + r * 0.25, cy - r * 0.25), (cx + r, cy), (cx + r * 0.25, cy + r * 0.25),
                   (cx, cy + r), (cx - r * 0.25, cy + r * 0.25), (cx - r, cy), (cx - r * 0.25, cy - r * 0.25)]),
              fill=(255, 245, 255, 230))


def bat(d, cx, cy, scale, color):
    """Bat silhouette, for the reactor core."""
    right = [(0, -9), (5, -7), (12, -13), (24, -15), (36, -9), (31, -3), (25, 1), (19, -2), (13, 4), (6, 2), (0, 11)]
    left = [(-x, y) for x, y in reversed(right[1:-1])]
    d.polygon(pts([(cx + x * scale, cy + y * scale) for x, y in right + left]), fill=color)


def batshittium_item():
    size = 128
    img = new_canvas(size)
    glow(img, lambda d, c: d.ellipse(s(26, 30, 102, 108), fill=c), BS_GLOW, blur=9, alpha=150)
    d = draw(img)
    # the rock it came out of
    d.ellipse(s(24, 88, 104, 114), fill=ROCK, outline=OUTLINE, width=3 * SS)
    d.ellipse(s(34, 90, 70, 102), fill=ROCK_LIGHT)
    shard(d, (44, 102), 46, 22, -24)
    shard(d, (86, 102), 54, 22, 20)
    shard(d, (63, 101), 74, 28, -3)
    sparkle(d, 95, 38, 7)
    sparkle(d, 34, 52, 5)
    save(img, size, 'Things/Item/Resource/RR_Batshittium.png')


def transmogrifier():
    size = 256
    img = new_canvas(size)
    d = draw(img)
    body = lambda m: m.rounded_rectangle(s(12, 20, 244, 240), radius=26 * SS, fill=255)
    gradient_fill(img, body, STEEL_LIGHT, STEEL_DARK)
    d.rounded_rectangle(s(12, 196, 244, 240), radius=26 * SS, fill=STEEL_DARK)  # front face
    d.rounded_rectangle(s(12, 20, 244, 240), radius=26 * SS, outline=OUTLINE, width=4 * SS)
    d.rounded_rectangle(s(26, 32, 230, 194), radius=18 * SS, fill=STEEL, outline=STEEL_DEEP, width=2 * SS)
    for x, y in ((34, 40), (222, 40), (34, 186), (222, 186)):
        circle(d, x, y, 3.5, fill=STEEL_LIGHT, outline=OUTLINE, width=1)
    for y in (46, 56, 66, 76):  # vents
        d.line(s(196, y, 224, y), fill=STEEL_DEEP, width=3 * SS)

    # input hopper with rock chunks
    d.polygon(pts([(30, 40), (86, 40), (78, 98), (38, 98)]), fill=STEEL_DEEP, outline=OUTLINE, width=3 * SS)
    d.polygon(pts([(38, 46), (78, 46), (72, 90), (44, 90)]), fill=(40, 38, 44, 255))
    for x, y, r, c in ((50, 60, 8, ROCK), (66, 56, 8, ROCK_LIGHT), (58, 76, 7, ROCK_LIGHT), (70, 74, 6, ROCK)):
        circle(d, x, y, r, fill=c, outline=OUTLINE, width=2)
    d.line(s(78, 86, 98, 104), fill=OUTLINE, width=15 * SS)  # feed pipe
    d.line(s(78, 86, 98, 104), fill=STEEL_DARK, width=9 * SS)

    # transmogrification chamber
    circle(d, 142, 112, 60, fill=STEEL_DEEP, outline=OUTLINE, width=4)
    for i in range(10):
        a = math.radians(i * 36 + 18)
        circle(d, 142 + 55 * math.cos(a), 112 + 55 * math.sin(a), 3, fill=STEEL_LIGHT, outline=OUTLINE, width=1)
    glow(img, lambda g, c: g.ellipse(s(94, 64, 190, 160), fill=c), BS_GLOW, blur=7, alpha=200)
    d = draw(img)
    orb(d, 142, 112, 47, BS_DEEP, BS_MID)
    for arm in range(3):  # swirl
        spiral = [(142 + r * math.cos(arm * 2.094 + r * 0.11),
                   112 + r * math.sin(arm * 2.094 + r * 0.11)) for r in range(6, 46, 2)]
        d.line(pts(spiral), fill=(*BS_LIGHT[:3], 210), width=4 * SS, joint='curve')
    glow(img, lambda g, c: g.ellipse(s(130, 100, 154, 124), fill=c), (255, 230, 255), blur=4, alpha=255)
    d = draw(img)
    circle(d, 142, 112, 47, outline=OUTLINE, width=3)

    # output tray with a fresh unit
    d.line(s(194, 128, 210, 140), fill=OUTLINE, width=13 * SS)
    d.line(s(194, 128, 210, 140), fill=BS_DARK, width=7 * SS)
    d.rounded_rectangle(s(198, 124, 238, 180), radius=6 * SS, fill=STEEL_DEEP, outline=OUTLINE, width=3 * SS)
    glow(img, lambda g, c: g.ellipse(s(204, 136, 232, 176), fill=c), BS_GLOW, blur=5, alpha=150)
    d = draw(img)
    shard(d, (218, 172), 32, 13, 6)

    # control panel on the front, facing the operator
    d.rounded_rectangle(s(92, 204, 172, 234), radius=4 * SS, fill=(36, 40, 46, 255), outline=OUTLINE, width=2 * SS)
    glow(img, lambda g, c: g.rectangle(s(98, 209, 140, 229), fill=c), (80, 230, 160), blur=3, alpha=180)
    d = draw(img)
    d.rectangle(s(99, 210, 139, 228), fill=(70, 205, 145, 255), outline=OUTLINE, width=1 * SS)
    for x, y, c in ((150, 214, (220, 60, 60, 255)), (162, 214, (235, 200, 60, 255)),
                    (150, 225, (70, 200, 90, 255)), (162, 225, BS_MID)):
        circle(d, x, y, 3.5, fill=c, outline=OUTLINE, width=1)
    save(img, size, 'Things/Building/Production/RR_BatshittiumTransmogrifier.png')


def reactor():
    size = 256
    img = new_canvas(size)
    # hazard-striped border
    stripes = Image.new('RGBA', img.size, (232, 190, 40, 255))
    sd = draw(stripes)
    for x in range(-size, 2 * size, 28):
        sd.line(s(x, 0, x - size, size), fill=(34, 32, 30, 255), width=11 * SS)
    ring = Image.new('L', img.size, 0)
    rd = ImageDraw.Draw(ring)
    rd.rounded_rectangle(s(8, 8, 248, 248), radius=16 * SS, fill=255)
    rd.rounded_rectangle(s(30, 30, 226, 226), radius=12 * SS, fill=0)
    img.paste(stripes, (0, 0), ring)
    d = draw(img)
    d.rounded_rectangle(s(8, 8, 248, 248), radius=16 * SS, outline=OUTLINE, width=4 * SS)
    d.rounded_rectangle(s(30, 30, 226, 226), radius=12 * SS, fill=STEEL_DEEP, outline=OUTLINE, width=3 * SS)

    # cooling fins
    for i in range(12):
        a = math.radians(i * 30 + 15)
        ca, sa = math.cos(a), math.sin(a)
        corners = [(128 + r * ca - w * sa, 128 + r * sa + w * ca) for r, w in ((62, -8), (96, -8), (96, 8), (62, 8))]
        d.polygon(pts(corners), fill=STEEL_DARK, outline=OUTLINE, width=3 * SS)

    # containment housing
    gradient_fill(img, lambda m: m.ellipse(s(54, 54, 202, 202), fill=255), STEEL_LIGHT, STEEL_DARK)
    d = draw(img)
    circle(d, 128, 128, 74, outline=OUTLINE, width=4)
    circle(d, 128, 128, 60, fill=STEEL_DEEP, outline=OUTLINE, width=3)
    for i in range(12):
        a = math.radians(i * 30)
        circle(d, 128 + 67 * math.cos(a), 128 + 67 * math.sin(a), 3.5, fill=STEEL_LIGHT, outline=OUTLINE, width=1)

    # the core
    glow(img, lambda g, c: g.ellipse(s(66, 66, 190, 190), fill=c), BS_GLOW, blur=10, alpha=230)
    d = draw(img)
    orb(d, 128, 128, 50, BS_DARK, BS_CORE)
    bat(d, 128, 132, 1.25, (70, 14, 100, 215))
    d.ellipse(s(102, 98, 122, 110), fill=(255, 255, 255, 150))  # specular highlight
    circle(d, 128, 128, 50, outline=BS_DEEP, width=2)
    save(img, size, 'Things/Building/Power/RR_BatshittiumReactor.png')


def brain_squeezer():
    """A padded chair seen from above, facing north (backrest at the bottom),
    under a glowing headpiece with clamp arms."""
    size = 128
    img = new_canvas(size)
    d = draw(img)
    cushion = (118, 96, 138, 255)
    cushion_dark = (88, 68, 106, 255)
    for x0 in (16, 98):  # armrests
        d.rounded_rectangle(s(x0, 30, x0 + 14, 104), radius=5 * SS, fill=STEEL_DARK,
                            outline=OUTLINE, width=3 * SS)
    d.rounded_rectangle(s(28, 26, 100, 100), radius=12 * SS, fill=cushion,
                        outline=OUTLINE, width=3 * SS)
    d.rounded_rectangle(s(36, 34, 92, 74), radius=8 * SS, fill=cushion_dark)  # seat dent
    d.rounded_rectangle(s(22, 92, 106, 118), radius=8 * SS, fill=STEEL_DARK,
                        outline=OUTLINE, width=3 * SS)  # backrest
    for x in (44, 84):  # cables from backrest to headpiece
        d.line(s(x, 110, 64 + (x - 64) * 0.4, 96), fill=OUTLINE, width=5 * SS)
        d.line(s(x, 110, 64 + (x - 64) * 0.4, 96), fill=BS_DARK, width=2 * SS)

    # the headpiece, over where the sitter's head goes
    glow(img, lambda g, c: g.ellipse(s(36, 62, 92, 118), fill=c), BS_GLOW, blur=6, alpha=170)
    d = draw(img)
    for angle in (35, 145, 215, 325):  # clamp arms with pads
        a = math.radians(angle)
        x1, y1 = 64 + 30 * math.cos(a), 90 + 30 * math.sin(a)
        d.line(s(64 + 18 * math.cos(a), 90 + 18 * math.sin(a), x1, y1), fill=OUTLINE, width=6 * SS)
        d.line(s(64 + 18 * math.cos(a), 90 + 18 * math.sin(a), x1, y1), fill=STEEL_LIGHT, width=3 * SS)
        circle(d, x1, y1, 4.5, fill=cushion_dark, outline=OUTLINE, width=2)
    circle(d, 64, 90, 21, fill=STEEL, outline=OUTLINE, width=3)
    circle(d, 64, 90, 16, outline=(*BS_LIGHT[:3], 230), width=3)  # glowing band
    orb(d, 64, 90, 9, BS_DARK, BS_CORE)
    d.ellipse(s(55, 80, 63, 85), fill=(255, 255, 255, 140))  # glint
    save(img, size, 'Things/Building/Joy/RR_BrainSqueezer.png')


if __name__ == '__main__':
    batshittium_item()
    transmogrifier()
    reactor()
    brain_squeezer()
