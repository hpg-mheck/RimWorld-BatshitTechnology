"""Generate the mod's starter textures into Textures/.

RimWorld-style top-down art drawn procedurally: dark outlines, flat steel shading, and a magenta
glow for anything batshittium. These are placeholders to build and playtest with; replace any PNG
with hand-drawn art at the same path and size.

Requires Pillow (pip install pillow). Run from anywhere:  python Tools/make_textures.py
"""
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

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
    save(img, size, 'Things/Item/Resource/BT_Batshittium.png')


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
    save(img, size, 'Things/Building/Production/BT_BatshittiumTransmogrifier.png')


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
    save(img, size, 'Things/Building/Power/BT_BatshittiumReactor.png')


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
    save(img, size, 'Things/Building/Joy/BT_BrainSqueezer.png')


HAZARD = (232, 190, 40, 255)


def bolt(d, x, y, length, angle_deg, explosive):
    """One Ballista bolt from its tail at (x, y), pointing along angle_deg
    (0 = north): shaft, fletching and either a steel head or a glowing
    batshittium warhead."""
    a = math.radians(angle_deg)
    ux, uy = math.sin(a), -math.cos(a)  # unit vector toward the tip
    px, py = -uy, ux                    # perpendicular
    tip = (x + ux * length, y + uy * length)
    d.line(pts([(x, y), tip]), fill=OUTLINE, width=7 * SS)
    d.line(pts([(x, y), tip]), fill=STEEL_LIGHT, width=4 * SS)
    for side in (-1, 1):  # fletching
        d.polygon(pts([(x + ux * 2, y + uy * 2),
                       (x + ux * 14 + px * side * 7, y + uy * 14 + py * side * 7),
                       (x + ux * 16, y + uy * 16)]),
                  fill=BS_DARK, outline=OUTLINE, width=2 * SS)
    hx, hy = tip
    if explosive:
        d.ellipse(s(hx - 8, hy - 8, hx + 8, hy + 8), fill=BS_MID, outline=OUTLINE, width=2 * SS)
        d.ellipse(s(hx - 4, hy - 5, hx + 1, hy), fill=BS_CORE)
        d.line(pts([(hx - px * 8, hy - py * 8), (hx + px * 8, hy + py * 8)]),
               fill=HAZARD, width=3 * SS)
    else:
        d.polygon(pts([(hx + ux * 12, hy + uy * 12), (hx + px * 6, hy + py * 6),
                       (hx - px * 6, hy - py * 6)]),
                  fill=STEEL, outline=OUTLINE, width=2 * SS)
        d.line(pts([(hx, hy), (hx + ux * 10, hy + uy * 10)]), fill=BS_LIGHT, width=2 * SS)


def bolt_item(explosive):
    """A small bundle of three bolts."""
    size = 128
    img = new_canvas(size)
    if explosive:
        glow(img, lambda g, c: g.ellipse(s(60, 10, 116, 66), fill=c), BS_GLOW, blur=8, alpha=150)
    d = draw(img)
    for x, y, ang in ((30, 112, 38), (44, 116, 30), (58, 118, 22)):
        bolt(d, x, y, 84, ang, explosive)
    d.rounded_rectangle(s(36, 84, 70, 96), radius=4 * SS, fill=(120, 92, 60, 255),
                        outline=OUTLINE, width=2 * SS)  # binding strap
    name = 'BT_BoltReallyPainful' if explosive else 'BT_BoltPainful'
    save(img, size, f'Things/Item/Resource/{name}.png')


def bolt_projectile(explosive):
    """A bolt in flight, pointing north (RimWorld rotates it to its heading)."""
    size = 64
    img = new_canvas(size)
    if explosive:
        glow(img, lambda g, c: g.ellipse(s(18, 2, 46, 30), fill=c), BS_GLOW, blur=4, alpha=200)
    bolt(draw(img), 32, 60, 42, 0, explosive)
    name = 'BT_BoltReallyPainful' if explosive else 'BT_BoltPainful'
    save(img, size, f'Things/Projectile/{name}.png')


def ballista_base():
    """2x2 plinth with a turntable; the launcher top is drawn over it."""
    size = 256
    img = new_canvas(size)
    d = draw(img)
    gradient_fill(img, lambda m: m.rounded_rectangle(s(10, 10, 246, 246), radius=20 * SS, fill=255),
                  STEEL, STEEL_DEEP)
    d = draw(img)
    d.rounded_rectangle(s(10, 10, 246, 246), radius=20 * SS, outline=OUTLINE, width=4 * SS)
    for cx, cy in ((34, 34), (222, 34), (34, 222), (222, 222)):  # hazard corners
        d.ellipse(s(cx - 14, cy - 14, cx + 14, cy + 14), fill=HAZARD, outline=OUTLINE, width=2 * SS)
        d.line(s(cx - 9, cy + 9, cx + 9, cy - 9), fill=OUTLINE, width=4 * SS)
    circle(d, 128, 128, 92, fill=STEEL_DARK, outline=OUTLINE, width=4)
    circle(d, 128, 128, 80, outline=(*BS_MID[:3], 220), width=3)  # glowing ring
    for i in range(16):
        a = math.radians(i * 22.5)
        circle(d, 128 + 86 * math.cos(a), 128 + 86 * math.sin(a), 3, fill=STEEL_LIGHT,
               outline=OUTLINE, width=1)
    circle(d, 128, 128, 56, fill=STEEL_DEEP, outline=OUTLINE, width=3)
    save(img, size, 'Things/Building/Security/BT_BatshittiumBallista_Base.png')


def ballista_top():
    """The launcher, pointing north: a long rail, bow limbs and a glowing
    batshittium core where the bolts are loaded."""
    size = 256
    img = new_canvas(size)
    glow(img, lambda g, c: g.ellipse(s(100, 120, 156, 176), fill=c), BS_GLOW, blur=8, alpha=200)
    d = draw(img)
    d.rounded_rectangle(s(116, 18, 140, 190), radius=8 * SS, fill=STEEL_DARK,
                        outline=OUTLINE, width=4 * SS)  # rail
    d.line(s(128, 26, 128, 182), fill=STEEL_LIGHT, width=3 * SS)
    for side in (-1, 1):  # bow limbs
        limb = [(128, 96), (128 + side * 46, 84), (128 + side * 92, 108),
                (128 + side * 96, 120), (128 + side * 52, 100), (128, 110)]
        d.polygon(pts(limb), fill=STEEL, outline=OUTLINE, width=3 * SS)
        d.line(pts([(128 + side * 94, 114), (128, 160)]), fill=(*BS_LIGHT[:3], 230),
               width=2 * SS)  # string
    for y in (40, 60):  # a loaded bolt's fletching visible on the rail
        d.polygon(pts([(122, y), (134, y), (128, y - 12)]), fill=BS_DARK, outline=OUTLINE,
                  width=2 * SS)
    circle(d, 128, 148, 24, fill=STEEL, outline=OUTLINE, width=4)
    orb(d, 128, 148, 15, BS_DARK, BS_CORE)
    save(img, size, 'Things/Building/Security/BT_BatshittiumBallista_Top.png')


def ballista_magazine():
    """1x1 ammo crate with bolts visible through the lid and a status light."""
    size = 128
    img = new_canvas(size)
    d = draw(img)
    gradient_fill(img, lambda m: m.rounded_rectangle(s(10, 14, 118, 118), radius=10 * SS, fill=255),
                  STEEL_LIGHT, STEEL_DARK)
    d = draw(img)
    d.rounded_rectangle(s(10, 14, 118, 118), radius=10 * SS, outline=OUTLINE, width=3 * SS)
    d.rounded_rectangle(s(22, 26, 106, 92), radius=6 * SS, fill=(40, 38, 44, 255),
                        outline=OUTLINE, width=2 * SS)
    for x in range(30, 104, 12):  # bolt tips in their racks
        d.line(s(x, 34, x, 84), fill=STEEL_LIGHT, width=4 * SS)
        d.polygon(pts([(x - 4, 38), (x + 4, 38), (x, 30)]), fill=BS_MID)
    for x in (16, 112):  # hazard edges
        d.rectangle(s(x - 4, 96, x + 4, 112), fill=HAZARD)
    glow(img, lambda g, c: g.ellipse(s(56, 98, 72, 114), fill=c), (80, 230, 160), blur=3, alpha=220)
    d = draw(img)
    circle(d, 64, 106, 5, fill=(80, 220, 150, 255), outline=OUTLINE, width=1)
    save(img, size, 'Things/Building/Security/BT_BallistaMagazine.png')


def artificer():
    """2x2 automated bolt works: batshittium hopper, an articulated arm over a
    jig, a tray of finished bolts and a front panel."""
    size = 256
    img = new_canvas(size)
    gradient_fill(img, lambda m: m.rounded_rectangle(s(12, 20, 244, 240), radius=24 * SS, fill=255),
                  STEEL_LIGHT, STEEL_DARK)
    d = draw(img)
    d.rounded_rectangle(s(12, 196, 244, 240), radius=24 * SS, fill=STEEL_DARK)
    d.rounded_rectangle(s(12, 20, 244, 240), radius=24 * SS, outline=OUTLINE, width=4 * SS)
    d.rounded_rectangle(s(26, 32, 230, 194), radius=16 * SS, fill=STEEL, outline=STEEL_DEEP,
                        width=2 * SS)
    # hopper of batshittium
    d.polygon(pts([(30, 40), (92, 40), (84, 100), (38, 100)]), fill=STEEL_DEEP, outline=OUTLINE,
              width=3 * SS)
    glow(img, lambda g, c: g.ellipse(s(40, 46, 84, 90), fill=c), BS_GLOW, blur=5, alpha=170)
    d = draw(img)
    for bx, by, h in ((50, 88, 26), (64, 90, 34), (76, 88, 24)):
        shard(d, (bx, by), h, 11, (bx - 64) * 0.8)
    # jig and arm
    d.rounded_rectangle(s(104, 100, 196, 130), radius=6 * SS, fill=STEEL_DEEP, outline=OUTLINE,
                        width=3 * SS)
    bolt(d, 112, 116, 72, 90, False)
    d.line(s(204, 44, 170, 76), fill=OUTLINE, width=12 * SS)
    d.line(s(204, 44, 170, 76), fill=HAZARD, width=7 * SS)
    d.line(s(170, 76, 150, 108), fill=OUTLINE, width=10 * SS)
    d.line(s(170, 76, 150, 108), fill=HAZARD, width=5 * SS)
    circle(d, 204, 44, 9, fill=STEEL_LIGHT, outline=OUTLINE, width=2)
    circle(d, 170, 76, 7, fill=STEEL_LIGHT, outline=OUTLINE, width=2)
    glow(img, lambda g, c: g.ellipse(s(140, 98, 160, 118), fill=c), BS_GLOW, blur=3, alpha=230)
    # finished bolts tray
    d = draw(img)
    d.rounded_rectangle(s(104, 140, 226, 186), radius=6 * SS, fill=(40, 38, 44, 255),
                        outline=OUTLINE, width=2 * SS)
    for y in (152, 166, 178):
        bolt(d, 112, y, 100, 90, y == 166)
    # control panel on the front
    d.rounded_rectangle(s(92, 204, 172, 234), radius=4 * SS, fill=(36, 40, 46, 255),
                        outline=OUTLINE, width=2 * SS)
    d.rectangle(s(99, 210, 139, 228), fill=(70, 205, 145, 255), outline=OUTLINE, width=1 * SS)
    for x, c in ((150, (220, 60, 60, 255)), (162, BS_MID)):
        circle(d, x, 219, 3.5, fill=c, outline=OUTLINE, width=1)
    save(img, size, 'Things/Building/Production/BT_BatshittiumArtificer.png')


FROST_LIGHT = (214, 242, 255, 255)
FROST = (128, 196, 232, 255)
FROST_DEEP = (70, 130, 170, 255)
DANGER_RED = (214, 52, 52, 255)


def hazard_patch(img, box, radius):
    """Yellow-and-black hazard stripes clipped to a rounded rectangle, on a
    canvas of any size."""
    stripes = Image.new('RGBA', img.size, HAZARD)
    sd = draw(stripes)
    n = max(256, img.size[1] // SS)  # 256 keeps the older sprites' stripes
    for x in range(-256, 2 * n, 10):
        sd.line(s(x, 0, x - n, n), fill=(34, 32, 30, 255), width=4 * SS)
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(s(*box), radius=radius * SS, fill=255)
    img.paste(stripes, (0, 0), mask)


def question_mark(d, x, y, scale, color):
    """A '?' glyph whose top-left is at (x, y), about 14 * scale tall."""
    hook = [(0, 3), (1, 0.8), (3.5, 0), (6, 0.8), (7, 3), (5.6, 5.2), (3.5, 6.4), (3.5, 9.4)]
    d.line(pts([(x + px * scale, y + py * scale) for px, py in hook]), fill=color,
           width=int(2 * scale) * SS, joint='curve')
    circle(d, x + 3.5 * scale, y + 12.6 * scale, 1.2 * scale, fill=color)


def snowflake(d, cx, cy, r):
    """Six-armed refrigeration emblem, outlined to read on steel."""
    for color, width in ((OUTLINE, 5), (FROST_LIGHT, 2)):
        for i in range(3):
            a = math.radians(i * 60 + 90)
            dx, dy = r * math.cos(a), r * math.sin(a)
            d.line(s(cx - dx, cy - dy, cx + dx, cy + dy), fill=color, width=width * SS)


def materializing_meal(img, cx, cy, r):
    """A meal on a plate, solid at the bottom and dissolving into magenta
    scanlines toward the top: food being replicated."""
    layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
    d = draw(layer)
    circle(d, cx, cy, r, fill=(232, 228, 220, 255), outline=OUTLINE, width=3)  # plate
    circle(d, cx, cy, r * 0.72, outline=(196, 190, 182, 255), width=2)          # rim
    for fx, fy, fr, color in ((-7, 4, 9, (176, 108, 58, 255)),   # roast
                              (8, 7, 7, (232, 150, 60, 255)),    # squash
                              (5, -8, 7, (110, 172, 72, 255)),   # greens
                              (-9, -7, 5, (226, 210, 150, 255))):  # bread
        circle(d, cx + fx, cy + fy, fr, fill=color, outline=OUTLINE, width=2)
    mask = Image.new('L', img.size, 0)
    md = ImageDraw.Draw(mask)
    top, bottom = cy - r - 2, cy + r + 2
    for y in range(int(top), int(bottom) + 1):
        t = (y - top) / (bottom - top)           # 0 at the top, 1 at the bottom
        solid = min(1.0, max(0.0, (t - 0.25) / 0.45))
        band = 1.0 if (y // 3) % 2 else 0.35       # scanline banding
        alpha = int(255 * (solid + (1 - solid) * 0.55 * band))
        md.rectangle(s(cx - r - 2, y, cx + r + 2, y + 1), fill=alpha)
    layer.putalpha(ImageChops.multiply(layer.getchannel('A'), mask))
    img.alpha_composite(layer)
    d = draw(img)
    for y in range(int(top) + 2, int(cy), 6):     # the replication beam's scanlines
        half = math.sqrt(max(0.0, (r + 6) ** 2 - (y - cy) ** 2))
        d.line(s(cx - half, y, cx + half, y), fill=(*BS_LIGHT[:3], 150), width=1 * SS)


def random_replicator():
    """2x2 food fabricator: a replication chamber with a meal half
    materialized, a frosted refrigerated compartment, a dispensing hatch and
    control panel on the front, and a guarded self-destruct button."""
    size = 256
    img = new_canvas(size)
    gradient_fill(img, lambda m: m.rounded_rectangle(s(12, 20, 244, 240), radius=26 * SS, fill=255),
                  STEEL_LIGHT, STEEL_DARK)
    d = draw(img)
    d.rounded_rectangle(s(12, 196, 244, 240), radius=26 * SS, fill=STEEL_DARK)  # front face
    d.rounded_rectangle(s(12, 20, 244, 240), radius=26 * SS, outline=OUTLINE, width=4 * SS)
    d.rounded_rectangle(s(26, 32, 230, 194), radius=18 * SS, fill=STEEL, outline=STEEL_DEEP,
                        width=2 * SS)
    for x, y in ((34, 40), (222, 40), (34, 186), (222, 186)):
        circle(d, x, y, 3.5, fill=STEEL_LIGHT, outline=OUTLINE, width=1)

    # feed pipe from the chamber to the compartment, drawn first so the
    # chamber and compartment cover its ends
    d.line(s(150, 112, 172, 112), fill=OUTLINE, width=15 * SS)
    d.line(s(150, 112, 172, 112), fill=BS_DARK, width=9 * SS)

    # replication chamber
    cx, cy = 96, 112
    circle(d, cx, cy, 62, fill=STEEL_DEEP, outline=OUTLINE, width=4)
    for i in range(10):
        a = math.radians(i * 36 + 18)
        circle(d, cx + 57 * math.cos(a), cy + 57 * math.sin(a), 3, fill=STEEL_LIGHT,
               outline=OUTLINE, width=1)
    glow(img, lambda g, c: g.ellipse(s(cx - 50, cy - 50, cx + 50, cy + 50), fill=c), BS_GLOW,
         blur=7, alpha=200)
    d = draw(img)
    orb(d, cx, cy, 49, BS_DEEP, BS_MID)
    materializing_meal(img, cx, cy + 6, 27)
    d = draw(img)
    sparkle(d, cx - 30, cy - 26, 6)
    sparkle(d, cx + 27, cy - 31, 4)
    circle(d, cx, cy, 49, outline=OUTLINE, width=3)

    # refrigerated compartment: frosted window, shelves of stored food
    d.rounded_rectangle(s(164, 40, 218, 150), radius=8 * SS, fill=STEEL_DEEP, outline=OUTLINE,
                        width=3 * SS)
    gradient_fill(img, lambda m: m.rounded_rectangle(s(171, 47, 211, 143), radius=5 * SS, fill=255),
                  FROST_LIGHT, FROST)
    d = draw(img)
    for y in (78, 110):
        d.line(s(171, y, 211, y), fill=FROST_DEEP, width=3 * SS)
    circle(d, 182, 68, 6, fill=(200, 60, 52, 255), outline=OUTLINE, width=2)        # apple
    circle(d, 198, 69, 5, fill=(236, 196, 70, 255), outline=OUTLINE, width=2)       # corn
    d.rounded_rectangle(s(177, 88, 189, 106), radius=2 * SS, fill=STEEL_LIGHT,
                        outline=OUTLINE, width=2 * SS)                                # tin
    circle(d, 200, 99, 6, fill=(232, 228, 220, 255), outline=OUTLINE, width=2)      # meal
    circle(d, 200, 99, 3, fill=(176, 108, 58, 255))
    d.ellipse(s(176, 118, 206, 136), fill=(150, 104, 70, 255), outline=OUTLINE, width=2 * SS)  # meat
    for fx, fy in ((173, 49), (209, 49), (173, 141), (209, 141)):    # frost in the corners
        d.polygon(pts([(fx, fy), (fx + (6 if fx < 190 else -6), fy),
                       (fx, fy + (6 if fy < 100 else -6))]), fill=(255, 255, 255, 190))
    d.rounded_rectangle(s(171, 47, 211, 143), radius=5 * SS, outline=OUTLINE, width=2 * SS)
    d.line(s(175, 52, 175, 72), fill=(255, 255, 255, 160), width=2 * SS)  # glass glint
    snowflake(d, 191, 170, 9)

    # front face: dispensing hatch, control panel, self-destruct
    d.rounded_rectangle(s(30, 204, 84, 232), radius=5 * SS, fill=(40, 38, 44, 255),
                        outline=OUTLINE, width=2 * SS)
    glow(img, lambda g, c: g.rectangle(s(36, 207, 78, 213), fill=c), BS_GLOW, blur=3, alpha=200)
    d = draw(img)
    d.line(s(37, 210, 77, 210), fill=BS_LIGHT, width=2 * SS)
    d.rounded_rectangle(s(96, 204, 168, 234), radius=4 * SS, fill=(36, 40, 46, 255),
                        outline=OUTLINE, width=2 * SS)
    glow(img, lambda g, c: g.rectangle(s(101, 209, 135, 229), fill=c), (80, 230, 160), blur=3,
         alpha=180)
    d = draw(img)
    d.rectangle(s(102, 210, 134, 228), fill=(70, 205, 145, 255), outline=OUTLINE, width=1 * SS)
    question_mark(d, 114, 211, 1.15, (20, 90, 60, 255))
    for x, y, c in ((146, 214, DANGER_RED), (158, 214, (235, 200, 60, 255)),
                    (146, 225, FROST), (158, 225, BS_MID)):
        circle(d, x, y, 3.5, fill=c, outline=OUTLINE, width=1)
    hazard_patch(img, (184, 202, 226, 236), 6)
    d = draw(img)
    d.rounded_rectangle(s(184, 202, 226, 236), radius=6 * SS, outline=OUTLINE, width=2 * SS)
    circle(d, 205, 219, 10, fill=DANGER_RED, outline=OUTLINE, width=2)
    d.ellipse(s(199, 212, 206, 217), fill=(255, 190, 190, 200))  # button glint
    circle(d, 205, 219, 14, outline=(220, 235, 245, 150), width=2)  # clear safety cover
    save(img, size, 'Things/Building/Production/BT_RandomReplicator.png')


CUSHION = (118, 96, 138, 255)        # the Brain Squeezer's padding
CUSHION_DARK = (88, 68, 106, 255)
VOID = (14, 6, 22, 255)              # a singularity's core


def singularity_core(img, cx, cy, r):
    """A tiny black hole: a dark core inside a glowing magenta accretion
    ring, with a hot spot where the ring turns toward the viewer."""
    glow(img, lambda g, c: g.ellipse(s(cx - r * 1.5, cy - r * 1.5, cx + r * 1.5, cy + r * 1.5), fill=c),
         BS_GLOW, blur=max(2, r * 0.5), alpha=220)
    d = draw(img)
    circle(d, cx, cy, r, fill=BS_MID)
    circle(d, cx, cy, r * 0.82, outline=BS_LIGHT, width=max(1, int(r * 0.18)))
    orb(d, cx, cy, r * 0.62, VOID, (40, 10, 60, 255), steps=24)
    d.ellipse(s(cx - r * 0.9, cy - r * 0.95, cx - r * 0.2, cy - r * 0.55), fill=(*BS_CORE[:3], 200))


def grenade_body(img, cx, cy, scale):
    """The Ergonomic Singularity Grenade, upright: a steel shell, a
    cushioned grip with finger grooves, a glass dome over the singularity,
    and a hazard-yellow lever and pin."""
    d = draw(img)
    def S(*v):
        return [cx + v[i] * scale if i % 2 == 0 else cy + v[i] * scale for i in range(len(v))]

    # lever and pin ring, behind the body
    lever = S(6, -30, 24, -24, 31, 10)
    d.line(s(*lever), fill=OUTLINE, width=max(3, int(9 * scale)) * SS, joint='curve')
    d.line(s(*lever), fill=HAZARD, width=max(1, int(5 * scale)) * SS, joint='curve')
    circle(d, cx - 16 * scale, cy - 30 * scale, 8 * scale, outline=OUTLINE, width=max(2, int(5 * scale)))
    circle(d, cx - 16 * scale, cy - 30 * scale, 8 * scale, outline=STEEL_LIGHT, width=max(1, int(2 * scale)))
    # steel shell
    gradient_fill(img, lambda m: m.ellipse(s(*S(-24, -30, 24, 40)), fill=255), STEEL_LIGHT, STEEL_DARK)
    d = draw(img)
    d.ellipse(s(*S(-24, -30, 24, 40)), outline=OUTLINE, width=max(2, int(4 * scale)) * SS)
    # cushioned, contoured grip with finger grooves
    d.rounded_rectangle(s(*S(-26, 2, 26, 36)), radius=int(12 * scale) * SS, fill=CUSHION,
                        outline=OUTLINE, width=max(2, int(3 * scale)) * SS)
    for gy in (10, 19, 28):
        d.line(s(*S(-22, gy, 22, gy)), fill=CUSHION_DARK, width=max(1, int(3 * scale)) * SS)
    d.line(s(*S(-18, 5, -18, 33)), fill=(*CUSHION[:3], 255), width=1)
    d.ellipse(s(*S(-20, 4, -8, 12)), fill=(170, 150, 190, 160))  # padding sheen
    # glass dome over the singularity
    circle(d, cx, cy - 12 * scale, 15 * scale, fill=(40, 34, 52, 255), outline=OUTLINE,
           width=max(2, int(3 * scale)))
    singularity_core(img, cx, cy - 12 * scale, 10 * scale)
    d = draw(img)
    d.arc(s(*S(-13, -25, 13, 1)), start=200, end=290, fill=(255, 255, 255, 170),
          width=max(1, int(2 * scale)) * SS)  # glass glint


def singularity_grenade_item():
    size = 128
    img = new_canvas(size)
    grenade_body(img, 60, 62, 1.35)
    save(img, size, 'Things/Item/Equipment/BT_ErgonomicSingularityGrenade.png')


def singularity_grenade_projectile():
    size = 64
    img = new_canvas(size)
    grenade_body(img, 32, 33, 0.62)
    save(img, size, 'Things/Projectile/BT_SingularityGrenade.png')


def singularity_vortex():
    """The singularity seen from above: magenta spiral arms winding into a
    black core inside a bright accretion ring. Drawn spinning in game, so
    it has no up."""
    size = 256
    img = new_canvas(size)
    c = size / 2
    # faint outer haze
    glow(img, lambda g, col: g.ellipse(s(c - 110, c - 110, c + 110, c + 110), fill=col),
         BS_GLOW, blur=18, alpha=70)
    # spiral arms, fading toward the rim
    arms = Image.new('RGBA', img.size, (0, 0, 0, 0))
    ad = draw(arms)
    for arm in range(4):
        base = arm * math.pi / 2
        for i in range(140):
            t = i / 140
            r = 18 + t * 100
            a = base + t * 3.4
            x, y = c + r * math.cos(a), c + r * math.sin(a)
            width = 14 * (1 - t) + 3
            alpha = int(230 * (1 - t) ** 1.2)
            color = BS_LIGHT if i % 9 < 2 else BS_MID
            ad.ellipse(s(x - width / 2, y - width / 2, x + width / 2, y + width / 2),
                       fill=(*color[:3], alpha))
    img.alpha_composite(arms.filter(ImageFilter.GaussianBlur(1.2 * SS)))
    # accretion ring and the void
    d = draw(img)
    glow(img, lambda g, col: g.ellipse(s(c - 34, c - 34, c + 34, c + 34), fill=col),
         BS_GLOW, blur=8, alpha=230)
    d = draw(img)
    circle(d, c, c, 26, outline=(*BS_CORE[:3], 255), width=5)
    circle(d, c, c, 22, fill=VOID)
    for k in range(10):  # sparks caught in the ring
        a = k * 0.63
        sparkle(d, c + 30 * math.cos(a), c + 30 * math.sin(a), 3 if k % 3 else 5)
    save(img, size, 'Things/Mote/BT_SingularityVortex.png')


def singularity_vortex_outer():
    """The singularity's outer layer, drawn larger beneath the main vortex
    and spun the other way: six thin, faint arms winding the opposite
    direction, with an empty middle so the main vortex's core shows."""
    size = 256
    img = new_canvas(size)
    c = size / 2
    arms = Image.new('RGBA', img.size, (0, 0, 0, 0))
    ad = draw(arms)
    for arm in range(6):
        base = arm * math.pi / 3
        for i in range(150):
            t = i / 150
            r = 40 + t * 82
            a = base - t * 2.6  # the other way round
            x, y = c + r * math.cos(a), c + r * math.sin(a)
            width = 7 * (1 - t) + 2
            alpha = int(150 * math.sin(math.pi * t) ** 0.8)  # fades in and out along the arm
            color = BS_LIGHT if i % 11 < 2 else BS_MID
            ad.ellipse(s(x - width / 2, y - width / 2, x + width / 2, y + width / 2),
                       fill=(*color[:3], alpha))
    img.alpha_composite(arms.filter(ImageFilter.GaussianBlur(2 * SS)))
    save(img, size, 'Things/Mote/BT_SingularityVortexOuter.png')


RUBBER = (46, 44, 52, 255)           # tyres
RUBBER_LIGHT = (78, 76, 86, 255)     # tyre treads, lit


class Frame:
    """Lays out a part drawn facing north (front up, at -y) in any of the
    four facings, about a centre. Every shape stays axis-aligned because
    the turns are quarter turns, and the light stays at the top left in
    every facing because gradients are applied in image space."""

    TURNS = {'north': 0, 'east': 1, 'south': 2, 'west': 3}

    def __init__(self, cx, cy, facing='north'):
        self.cx, self.cy = cx, cy
        self.turns = self.TURNS[facing]

    def p(self, x, y):
        """A point in the frame, in final-pixel image coordinates."""
        for _ in range(self.turns):
            x, y = -y, x  # a quarter turn clockwise, as seen on screen
        return (self.cx + x, self.cy + y)

    def box(self, x0, y0, x1, y1):
        """An axis-aligned box in the frame, as (left, top, right, bottom)."""
        (ax, ay), (bx, by) = self.p(x0, y0), self.p(x1, y1)
        return (min(ax, bx), min(ay, by), max(ax, bx), max(ay, by))


def tyre(img, f, x, y, tread_w, dia, flip):
    """A tractor tyre at (x, y) in the frame f: upright (flip 0), seen from
    above as a treaded bar tread_w wide and dia long; flipped out flat
    (flip 1), a ring round a steel hub, like a stabiliser pad; or part-way
    between."""
    t = max(0.0, min(1.0, flip))
    w = tread_w + (dia - tread_w) * math.sin(t * math.pi / 2)
    d = draw(img)
    box = f.box(x - w / 2, y - dia / 2, x + w / 2, y + dia / 2)
    radius = min(box[2] - box[0], box[3] - box[1]) / 2 * (0.35 + 0.65 * t)
    d.rounded_rectangle(s(*box), radius=radius * SS, fill=RUBBER,
                        outline=OUTLINE, width=3 * SS)
    if t < 0.6:  # tread bars across the upright tyre
        for k in range(-int(dia / 2) + 6, int(dia / 2) - 3, 8):
            a, b = f.p(x - w / 2 + 3, y + k), f.p(x + w / 2 - 3, y + k)
            d.line(s(*a, *b), fill=(*RUBBER_LIGHT[:3], int(255 * (1 - t))),
                   width=3 * SS)
    if t > 0.3:  # the rim and hub show as it lies down
        cx, cy = f.p(x, y)
        hub = min(w, dia) / 2 * 0.55
        circle(d, cx, cy, hub, fill=STEEL, outline=OUTLINE, width=2)
        circle(d, cx, cy, hub * 0.45, fill=STEEL_DEEP)
        for k in range(5):
            a = math.radians(k * 72 + 18)
            circle(d, cx + hub * 0.72 * math.cos(a), cy + hub * 0.72 * math.sin(a),
                   1.6, fill=STEEL_LIGHT)
        bw, bh = (box[2] - box[0]) / 2, (box[3] - box[1]) / 2
        for k in range(12):  # tread notches round the rim
            a = math.radians(k * 30)
            d.line(s(cx + bw * 0.80 * math.cos(a), cy + bh * 0.80 * math.sin(a),
                     cx + bw * 0.96 * math.cos(a), cy + bh * 0.96 * math.sin(a)),
                   fill=RUBBER_LIGHT, width=2 * SS)


def tractor_beam_emitter(img, cx, cy, r, power=1.0, halo=1.45):
    """The tractor-beam emitter, seen from above: a riveted steel collar
    (where the exhaust stack should be) round a glowing batshittium orb.
    The halo stays tight, so the steel around it stays grey."""
    h = r * halo
    glow(img, lambda g, c: g.ellipse(s(cx - h, cy - h, cx + h, cy + h), fill=c),
         BS_GLOW, blur=r * 0.4, alpha=int(110 + 80 * power))
    d = draw(img)
    circle(d, cx, cy, r, fill=STEEL, outline=OUTLINE, width=3)
    for k in range(8):
        a = math.radians(k * 45 + 22.5)
        circle(d, cx + r * 0.82 * math.cos(a), cy + r * 0.82 * math.sin(a), 1.4,
               fill=STEEL_LIGHT)
    orb(d, cx, cy, r * 0.62, BS_DARK, BS_CORE)
    d.ellipse(s(cx - r * 0.45, cy - r * 0.5, cx - r * 0.05, cy - r * 0.15),
              fill=(255, 255, 255, 150))  # specular


def tractor_bomb(img, facing='north', flip=0.0):
    """The Tractor Bomb, straight top-down on a 256 px canvas (2 tiles at
    128 px a tile): a little steel farm tractor, hood forward, big rear
    tyres under fenders, a control box where the seat would be with the
    protest lamp on it, one hazard band across the hood, and the magenta
    tractor-beam emitter where the exhaust stack should be. flip 0 drives
    on upright tyres; flip 1 is planted, its tyres flipped out flat as
    stabiliser pads on steel struts."""
    f = Frame(128, 132, facing)
    t = max(0.0, min(1.0, flip))
    d = draw(img)
    # where the tyres sit: upright, or out on struts once flipped flat
    rear_x, front_x = 52 + 30 * t, 40 + 22 * t
    rear_y, front_y = 42 + 4 * t, -50 - 10 * t
    for x0, y0, x1, y1, wdt in ((-rear_x, rear_y, rear_x, rear_y, 10),
                                (-front_x, front_y, front_x, front_y, 8)):
        a, b = f.p(x0, y0), f.p(x1, y1)
        d.line(s(*a, *b), fill=OUTLINE, width=(wdt + 4) * SS)
        d.line(s(*a, *b), fill=STEEL_DEEP, width=wdt * SS)
    for sx in (-1, 1):
        tyre(img, f, sx * rear_x, rear_y, 30, 76 - 4 * t, t)
        tyre(img, f, sx * front_x, front_y, 20, 44, t)
    # hood, steel, lit from the top left
    hood = f.box(-21, -86, 21, -2)
    gradient_fill(img, lambda m: m.rounded_rectangle(s(*hood), radius=10 * SS, fill=255),
                  STEEL_LIGHT, STEEL_DARK)
    d = draw(img)
    d.rounded_rectangle(s(*hood), radius=10 * SS, outline=OUTLINE, width=3 * SS)
    grille = f.box(-15, -84, 15, -74)
    d.rounded_rectangle(s(*grille), radius=3 * SS, fill=STEEL_DEEP, outline=OUTLINE,
                        width=2 * SS)
    for k in (-9, -3, 3, 9):  # grille slats
        a, b = f.p(k, -83), f.p(k, -75)
        d.line(s(*a, *b), fill=STEEL_DARK, width=2 * SS)
    for sx in (-1, 1):  # engine louvres down each side
        for k in range(-66, -46, 6):
            a, b = f.p(sx * 16, k), f.p(sx * 10, k)
            d.line(s(*a, *b), fill=STEEL_DEEP, width=2 * SS)
    # one hazard band across the hood, behind the emitter
    band = f.box(-21, -28, 21, -13)
    hazard_patch(img, band, 2)
    d = draw(img)
    d.rectangle(s(*band), outline=OUTLINE, width=2 * SS)
    # the footplate between the rear tyres
    body = f.box(-34, -6, 34, 70)
    gradient_fill(img, lambda m: m.rounded_rectangle(s(*body), radius=12 * SS, fill=255),
                  STEEL, STEEL_DEEP)
    d = draw(img)
    d.rounded_rectangle(s(*body), radius=12 * SS, outline=OUTLINE, width=3 * SS)
    if t < 0.5:  # fenders over the upright rear tyres
        for sx in (-1, 1):
            fender = f.box(sx * 34, 10, sx * 70, 44)
            d.rounded_rectangle(s(*fender), radius=8 * SS, fill=STEEL,
                                outline=OUTLINE, width=3 * SS)
            a, b = f.p(sx * 40, 16), f.p(sx * 64, 16)
            d.line(s(*a, *b), fill=STEEL_LIGHT, width=2 * SS)
    # the control box, where a driver would sit, with the protest lamp
    box = f.box(-19, 10, 19, 56)
    d.rounded_rectangle(s(*box), radius=6 * SS, fill=STEEL_DARK, outline=OUTLINE,
                        width=3 * SS)
    panel = f.box(-12, 34, 12, 50)
    d.rounded_rectangle(s(*panel), radius=3 * SS, fill=(36, 40, 46, 255),
                        outline=OUTLINE, width=1 * SS)
    lx, ly = f.p(-5, 42)
    d.rectangle(s(lx - 3, ly - 3, lx + 3, ly + 3), fill=(70, 205, 145, 255))  # status screen
    bx, by = f.p(5, 42)
    bat(d, bx, by, 0.18, (20, 22, 26, 230))  # the house mark, stencilled small
    lamp_x, lamp_y = f.p(0, 22)
    circle(d, lamp_x, lamp_y, 7, fill=(120, 30, 30, 255), outline=OUTLINE, width=2)
    circle(d, lamp_x, lamp_y, 4, fill=DANGER_RED)
    d.ellipse(s(lamp_x - 4, lamp_y - 5, lamp_x - 1, lamp_y - 2), fill=(255, 210, 210, 190))
    hitch = f.box(-7, 70, 7, 80)
    d.rectangle(s(*hitch), fill=STEEL_DEEP, outline=OUTLINE, width=2 * SS)
    # the emitter, where the exhaust stack should be
    ex, ey = f.p(0, -46)
    tractor_beam_emitter(img, ex, ey, 13, power=0.6 + 0.4 * t)


def tractor_bomb_pawn():
    """The robot in the three facings a pawn needs; RimWorld mirrors east
    for west."""
    for facing in ('north', 'east', 'south'):
        img = new_canvas(256)
        tractor_bomb(img, facing, 0.0)
        save(img, 256, f'Things/Pawn/Robot/BT_TractorBomb_{facing}.png')


def tractor_bomb_emplaced():
    """The planted bomb, tyres flipped out flat, and the three frames of
    the flip that the building shows as it emplaces."""
    img = new_canvas(256)
    tractor_bomb(img, 'north', 1.0)
    save(img, 256, 'Things/Building/Security/BT_TractorBombEmplaced.png')
    for i, t in enumerate((0.15, 0.45, 0.75), start=1):
        img = new_canvas(256)
        tractor_bomb(img, 'north', t)
        save(img, 256, f'Things/Building/Security/BT_TractorBombEmplaced_Flip{i}.png')


def tractor_bomb_parts():
    """What a wrecked robot's vortex sucks back in: a tyre, a piece of the
    hood with its hazard band, and the emitter, still glowing."""
    img = new_canvas(64)
    tyre(img, Frame(32, 32), 0, 0, 52, 52, 1.0)
    save(img, 64, 'Things/Mote/BT_TractorBombPart_Wheel.png')

    img = new_canvas(64)
    gradient_fill(img, lambda m: m.rounded_rectangle(s(12, 6, 52, 58), radius=8 * SS, fill=255),
                  STEEL_LIGHT, STEEL_DARK)
    hazard_patch(img, (12, 34, 52, 44), 1)
    d = draw(img)
    d.rounded_rectangle(s(12, 6, 52, 58), radius=8 * SS, outline=OUTLINE, width=3 * SS)
    for y in (14, 20, 26):  # louvres
        d.line(s(18, y, 26, y), fill=STEEL_DEEP, width=2 * SS)
    save(img, 64, 'Things/Mote/BT_TractorBombPart_Hood.png')

    img = new_canvas(64)
    tractor_beam_emitter(img, 32, 32, 15, power=1.0, halo=1.3)
    save(img, 64, 'Things/Mote/BT_TractorBombPart_Emitter.png')


def tractor_bomb_lamp():
    """The protest lamp's glow: a soft white disc, tinted red or yellow in
    game."""
    size = 64
    img = new_canvas(size)
    glow(img, lambda g, c: g.ellipse(s(16, 16, 48, 48), fill=c), (255, 255, 255),
         blur=7, alpha=255)
    d = draw(img)
    circle(d, 32, 32, 7, fill=(255, 255, 255, 255))
    save(img, size, 'Things/Mote/BT_TractorBombLamp.png')


def corrugated_plane(img, box, top, bottom, groove, step=10):
    """One plane of a corrugated steel roof: a gradient, ridged by grooves
    running across it, from eave to ridge."""
    x0, y0, x1, y1 = box
    gradient_fill(img, lambda m: m.rectangle(s(x0, y0, x1, y1), fill=255), top, bottom)
    d = draw(img)
    for y in range(int(y0) + step // 2, int(y1) - 2, step):
        d.line(s(x0 + 2, y, x1 - 2, y), fill=(*groove[:3], 150), width=2 * SS)
        d.line(s(x0 + 2, y + 2, x1 - 2, y + 2), fill=(255, 255, 255, 40), width=1 * SS)


def barn_door(d, x0, y0, x1, y1):
    """A barn door leaf, framed and cross-braced."""
    d.rectangle(s(x0, y0, x1, y1), fill=STEEL, outline=OUTLINE, width=3 * SS)
    d.rectangle(s(x0 + 5, y0 + 5, x1 - 5, y1 - 5), outline=STEEL_DEEP, width=3 * SS)
    d.line(s(x0 + 6, y0 + 6, x1 - 6, y1 - 6), fill=STEEL_DEEP, width=5 * SS)
    d.line(s(x0 + 6, y1 - 6, x1 - 6, y0 + 6), fill=STEEL_DEEP, width=5 * SS)
    d.line(s(x0 + 6, y0 + 6, x1 - 6, y0 + 6), fill=STEEL_LIGHT, width=1 * SS)


def roof_vent(img, cx, cy, r):
    """A round turbine vent on a roof: a riveted steel drum with swept
    blades, seen from above."""
    d = draw(img)
    circle(d, cx, cy, r, fill=STEEL, outline=OUTLINE, width=3)
    for k in range(8):
        a = math.radians(k * 45)
        b = a + math.radians(30)
        d.line(s(cx + r * 0.2 * math.cos(a), cy + r * 0.2 * math.sin(a),
                 cx + r * 0.85 * math.cos(b), cy + r * 0.85 * math.sin(b)),
               fill=STEEL_DEEP, width=2 * SS)
    circle(d, cx, cy, r * 0.25, fill=STEEL_LIGHT, outline=OUTLINE, width=1)
    d.arc(s(cx - r + 3, cy - r + 3, cx + r - 3, cy + r - 3), start=190, end=260,
          fill=(255, 255, 255, 120), width=2 * SS)  # lit rim, top left


def tractor_barn():
    """The Tractor Barn, 3 by 3 tiles (384 px): a corrugated steel barn seen
    from above, its gabled roof lit on the left, with two turbine vents and
    a cupola on the ridge whose louvres glow magenta from the assembly
    inside (the barn's one batshittium accent), a small bat weathervane on
    top. Its front wall, facing the door cell, has the big cross-braced
    double doors under a hazard lintel, a stack of spare tyres and the
    control panel a hauler uses."""
    size = 384
    img = new_canvas(size)
    left, top, right, bottom = 14, 16, 370, 372
    ridge, eave = 192, 296
    # the roof's two planes, lit from the top left, with sheet seams
    corrugated_plane(img, (left, top, ridge, eave), STEEL_LIGHT, STEEL, STEEL_DARK)
    corrugated_plane(img, (ridge, top, right, eave), STEEL, STEEL_DARK, STEEL_DEEP)
    d = draw(img)
    for x in (74, 134, 250, 310):
        d.line(s(x, top, x, eave), fill=(*OUTLINE[:3], 110), width=2 * SS)
    d.rectangle(s(ridge - 7, top, ridge + 7, eave), fill=STEEL_LIGHT, outline=OUTLINE,
                width=2 * SS)  # ridge cap
    for y in range(top + 18, eave - 6, 34):  # rivets along the eaves
        for x in (left + 9, right - 9):
            circle(d, x, y, 3, fill=STEEL_LIGHT, outline=OUTLINE, width=1)
    roof_vent(img, 284, 214, 17)
    roof_vent(img, 100, 214, 17)
    # the front wall, facing the door cell
    gradient_fill(img, lambda m: m.rectangle(s(left, eave, right, bottom), fill=255),
                  STEEL_DARK, STEEL_DEEP)
    d = draw(img)
    d.line(s(left, eave, right, eave), fill=OUTLINE, width=4 * SS)  # the eave's edge
    # the doors, under one hazard lintel
    hazard_patch(img, (128, eave + 4, 256, eave + 16), 2)
    d = draw(img)
    d.rectangle(s(128, eave + 4, 256, eave + 16), outline=OUTLINE, width=2 * SS)
    barn_door(d, 132, eave + 18, 192, bottom - 4)
    barn_door(d, 192, eave + 18, 252, bottom - 4)
    circle(d, 186, eave + 46, 3.5, fill=STEEL_LIGHT, outline=OUTLINE, width=1)  # handles
    circle(d, 198, eave + 46, 3.5, fill=STEEL_LIGHT, outline=OUTLINE, width=1)
    # spare tyres stacked by the doors: what flies out if it is destroyed
    tyre(img, Frame(58, 336), 0, 0, 50, 50, 1.0)
    tyre(img, Frame(100, 340), 0, 0, 40, 40, 1.0)
    # the control panel a hauler throws the switch on
    d = draw(img)
    d.rounded_rectangle(s(276, eave + 16, 352, bottom - 14), radius=4 * SS,
                        fill=(36, 40, 46, 255), outline=OUTLINE, width=2 * SS)
    d.rectangle(s(283, eave + 23, 321, bottom - 21), fill=(70, 205, 145, 255),
                outline=OUTLINE, width=1 * SS)  # status screen
    d.line(s(336, eave + 46, 342, eave + 26), fill=OUTLINE, width=6 * SS)  # the switch
    d.line(s(336, eave + 46, 342, eave + 26), fill=HAZARD, width=3 * SS)
    circle(d, 336, eave + 48, 4, fill=DANGER_RED, outline=OUTLINE, width=1)
    # the cupola on the ridge: magenta light leaking from its louvres
    cx, cy, half = ridge, 112, 32
    glow(img, lambda g, c: g.rectangle(s(cx - half - 10, cy - half - 10, cx + half + 10,
                                         cy + half + 10), fill=c), BS_GLOW, blur=8, alpha=210)
    d = draw(img)
    d.rectangle(s(cx - half, cy - half, cx + half, cy + half), fill=BS_DEEP, outline=OUTLINE,
                width=3 * SS)
    for k in range(-half + 8, half - 4, 8):  # glowing louvre slits on each side
        d.line(s(cx - half + 3, cy + k, cx - half + 9, cy + k), fill=BS_LIGHT, width=3 * SS)
        d.line(s(cx + half - 9, cy + k, cx + half - 3, cy + k), fill=BS_MID, width=3 * SS)
        d.line(s(cx + k, cy - half + 3, cx + k, cy - half + 9), fill=BS_LIGHT, width=3 * SS)
        d.line(s(cx + k, cy + half - 9, cx + k, cy + half - 3), fill=BS_MID, width=3 * SS)
    r = half - 10  # its pyramid roof: lit faces up and left
    apex = (cx, cy)
    corners = [(cx - r, cy - r), (cx + r, cy - r), (cx + r, cy + r), (cx - r, cy + r)]
    faces = (STEEL_LIGHT, STEEL_DARK, STEEL_DEEP, STEEL)  # top, right, bottom, left
    for (a, b), face in zip(zip(corners, corners[1:] + corners[:1]), faces):
        d.polygon(pts([a, b, apex]), fill=face, outline=OUTLINE, width=2 * SS)
    # the weathervane at the apex: a small bat, the house mark
    circle(d, cx, cy, 2.5, fill=STEEL_LIGHT, outline=OUTLINE, width=1)
    bat(d, cx, cy - 2, 0.3, (24, 22, 28, 255))
    # the outline last, and nothing outside its rounded corners
    d.rounded_rectangle(s(left, top, right, bottom), radius=12 * SS, outline=OUTLINE,
                        width=4 * SS)
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(s(left, top, right, bottom), radius=12 * SS,
                                           fill=255)
    img.putalpha(ImageChops.multiply(img.getchannel('A'), mask))
    save(img, size, 'Things/Building/Production/BT_TractorBarn.png')


def tractor_barn_tyre():
    """A spare tyre flung from a destroyed Tractor Barn, tumbling flat."""
    img = new_canvas(64)
    tyre(img, Frame(32, 32), 0, 0, 50, 50, 1.0)
    save(img, 64, 'Things/Projectile/BT_TractorBarnTyre.png')


def disposition_icon(name, gap):
    """A Disposition button (128 px): the tractor bomb seen from above,
    driving up a dashed track toward a red target reticle and stopping
    `gap` pixels short of it: far for Cautious, near for Moderate, nose to
    the ring for Nosy."""
    size = 128
    tx, ty, tr = 64, 21, 17
    tractor_h = 52
    stop = ty + tr + gap  # where the tractor's nose stops
    img = new_canvas(size)
    d = draw(img)
    for y in range(int(ty + tr + 4), int(stop) - 2, 8):  # the track still to go
        d.line(s(tx, y, tx, min(y + 4, stop - 2)), fill=(*STEEL_LIGHT[:3], 230),
               width=3 * SS)
    circle(d, tx, ty, tr, outline=OUTLINE, width=8)
    circle(d, tx, ty, tr, outline=DANGER_RED, width=5)
    for a, b in (((tx - tr - 7, ty), (tx - tr + 7, ty)), ((tx + tr - 7, ty), (tx + tr + 7, ty)),
                 ((tx, ty - tr - 7), (tx, ty - tr + 7))):
        d.line(s(*a, *b), fill=OUTLINE, width=6 * SS)
        d.line(s(*a, *b), fill=DANGER_RED, width=3 * SS)
    circle(d, tx, ty, 3.5, fill=DANGER_RED, outline=OUTLINE, width=1)
    final = img.resize((size, size), Image.LANCZOS)
    robot = new_canvas(256)
    tractor_bomb(robot, 'north', 0.0)
    robot = robot.resize((256, 256), Image.LANCZOS)
    robot = robot.crop(robot.getbbox())
    width = round(robot.size[0] * tractor_h / robot.size[1])
    robot = robot.resize((width, tractor_h), Image.LANCZOS)
    final.alpha_composite(robot, (int(tx - width / 2), int(stop)))
    path = os.path.join(TEXTURES, f'UI/Commands/BT_Disposition{name}.png')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    final.save(path)
    print('wrote', os.path.relpath(path, os.path.dirname(TEXTURES)))


def disposition_icons():
    """The three Disposition buttons; the tractor's nose stops 34, 15 and
    1 px short of the target, the tractor itself filling the rest."""
    disposition_icon('Cautious', 34)
    disposition_icon('Moderate', 15)
    disposition_icon('Nosy', 1)


if __name__ == '__main__':
    batshittium_item()
    transmogrifier()
    reactor()
    brain_squeezer()
    for explosive in (False, True):
        bolt_item(explosive)
        bolt_projectile(explosive)
    ballista_base()
    ballista_top()
    ballista_magazine()
    artificer()
    random_replicator()
    singularity_grenade_item()
    singularity_grenade_projectile()
    singularity_vortex()
    singularity_vortex_outer()
    tractor_bomb_pawn()
    tractor_bomb_emplaced()
    tractor_bomb_parts()
    tractor_bomb_lamp()
    tractor_barn()
    tractor_barn_tyre()
    disposition_icons()
