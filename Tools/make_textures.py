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
    """Yellow-and-black hazard stripes clipped to a rounded rectangle."""
    stripes = Image.new('RGBA', img.size, HAZARD)
    sd = draw(stripes)
    for x in range(-256, 512, 10):
        sd.line(s(x, 0, x - 256, 256), fill=(34, 32, 30, 255), width=4 * SS)
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
