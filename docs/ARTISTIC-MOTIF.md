# Batshit Technology: Artistic Motif

This document is the visual and tonal reference for everything Batshit
Technology adds to RimWorld: textures, icons, effects, and the words players
read. Follow it for all new material, so that every gadget looks like it came
out of the same slightly unhinged workshop. When something new does not fit
the rules below, extend this document in the same change rather than
improvising.

## The Idea in One Line

Sensible industrial steel, barely containing something magenta and wrong.

Each device is a competent piece of RimWorld engineering (riveted plates,
vents, a control panel, a hazard marking where one belongs) built around a
single glowing source of batshittium that should not be there. The joke is
that the engineering is earnest and the purpose is unhinged.

## Rendering Style

- **View.** Straight top-down, as RimWorld draws buildings. No perspective
  and no visible side walls; depth comes from shading alone.
- **Line.** Every shape carries a dark outline (`OUTLINE`, see below), about
  4 px on a 256 px sprite for the outer silhouette and 2 to 3 px for interior
  parts. Fine details (rivets, lights) take 1 px.
- **Shading.** Flat cel shading with light from the top left: housings run
  a vertical gradient from `STEEL_LIGHT` at the top to `STEEL_DARK` at the
  bottom; crystals have a lit left face and a dark right face; glossy parts
  get one white specular highlight at the upper left.
- **Glow.** Light sources are a soft Gaussian halo drawn on its own layer
  under the lit object (`glow()` in the generator). Glow is reserved for
  batshittium, screens and status lights.
- **Production.** Art is drawn procedurally by `Tools/make_textures.py` at
  four times its final size and downsampled with Lanczos filtering, for
  clean anti-aliased edges. Hand-drawn replacements must keep each file's
  path and size and should match the outline weight and palette.

## Palette

Exact values live as constants at the top of `Tools/make_textures.py`;
reuse them by name rather than inventing near-duplicates.

| Role | Constant | RGB |
|------|----------|-----|
| Outline | `OUTLINE` | 30, 26, 36 |
| Steel, lit | `STEEL_LIGHT` | 178, 182, 190 |
| Steel | `STEEL` | 140, 144, 152 |
| Steel, shaded | `STEEL_DARK` | 98, 101, 110 |
| Steel, recess | `STEEL_DEEP` | 62, 64, 72 |
| Batshittium, highlight | `BS_LIGHT` | 250, 170, 255 |
| Batshittium | `BS_MID` | 205, 80, 235 |
| Batshittium, shade | `BS_DARK` | 118, 30, 160 |
| Batshittium, depth | `BS_DEEP` | 58, 12, 88 |
| Batshittium, white-hot core | `BS_CORE` | 255, 236, 255 |
| Batshittium glow | `BS_GLOW` | 225, 80, 255 |
| Hazard yellow | `HAZARD` | 232, 190, 40 |
| Danger red | `DANGER_RED` | 214, 52, 52 |
| Refrigeration | `FROST_LIGHT`, `FROST`, `FROST_DEEP` | 214, 242, 255 to 70, 130, 170 |
| Status screen | (panel green) | 70, 205, 145 |
| Self-destruct warning | (`CompSelfDestruct`) | 255, 158, 26, then 255, 26, 15 |
| Rock | `ROCK_LIGHT`, `ROCK` | 150, 142, 140; 116, 110, 110 |

Colour rules:

- **Magenta means batshittium, and nothing else.** Anything powered by,
  made of or contaminated with it glows magenta; nothing else does.
- **One focal glow per device.** A building has one batshittium source
  (core, chamber, warhead, headpiece). Secondary magenta is limited to thin
  accents: cables, a pipe, a lit edge.
- **Functional accents only.** Yellow and black stripes mark danger
  (reactor, Ballista, the replicator's self-destruct). Red is a button or
  warning light. Frost blue is refrigeration. Panel green is a working
  screen. Never use these as decoration.
- **Countdowns pulse.** A device counting down to its own destruction
  wears a one-cell warning band that fades in and out in yellow-orange,
  then turns red and pulses four times as fast in the final seconds. Any
  new countdown should reuse it (CompSelfDestruct) rather than invent one.
- **Every death is distinct.** Each device blows up in a way that echoes
  its job: the Transmogrifier spews rock, the Brain Squeezer squeezes
  brains, Ballista hardware cooks off its bolts. A new device needs its own
  pulverization, not a generic explosion.
- **Steel stays neutral grey.** Do not tint housings; the glow does the
  talking.

## Shape Vocabulary

Reuse these parts so that devices rhyme with one another. Each has a
helper in the generator.

- **Chassis.** A rounded rectangle with the steel gradient, an inner
  recessed panel, rivets at the four inner corners, and a darker *front
  band* along the bottom edge (`transmogrifier`, `artificer`,
  `random_replicator`).
- **Front band.** The south edge faces the building's interaction cell.
  Put the things a colonist touches there: a control panel (dark bezel,
  green screen, a few coloured lights), a hatch, a button.
- **Porthole.** A riveted steel ring around a glowing batshittium orb
  (`orb()`), seen in the Transmogrifier, Reactor and Replicator. Contents
  inside a porthole show what the device does: rock swirling into
  batshittium, a bat in the reactor core, a meal half replicated.
- **Batshittium itself.** Faceted crystal shards (`shard()`) with a
  glinting edge and the occasional four-point sparkle (`sparkle()`).
- **Bats.** The bat silhouette (`bat()`) is the house mark. Use it at
  most once per device and only where it reads as a joke on close
  inspection, never as a logo stamped on top.
- **Hazard.** Striped borders or patches (`hazard_patch()`) for devices
  or controls that can kill you. A device gets one hazard element unless
  danger is its entire purpose (the Reactor, the Ballista).
- **Plumbing.** Pipes and cables are an outlined stroke with a steel or
  batshittium-dark core, connecting the parts that exchange matter.

## Composition

- **Sizes.** 256 px for a 2 by 2 building, 128 px for a 1 by 1 building
  and for items, 64 px for projectiles. Leave a margin of about 10 px (at
  256) inside the tile so outlines are not clipped.
- **Orientation.** Rotatable buildings use one `Graphic_Single` sprite
  that RimWorld rotates, so draw them facing north with the front band at
  the bottom. Projectiles point north.
- **Read at a glance.** The device's purpose must be legible zoomed out:
  check every new sprite downsampled to 64 px. Big shapes first; details
  that vanish at 64 px are a bonus, not the message.
- **Show the function.** Each device shows its job literally: the
  Transmogrifier has a hopper of rock and an output tray, the Artificer an
  arm over a jig and a tray of bolts, the Replicator a refrigerated
  compartment of stored food.

## Words: Descriptions, Labels and Monologues

The same idea applies to text. Write labels and descriptions as a
deadpan technical manual: accurate numbers, plain sentences and practical
advice, with one dry line that admits how bad an idea this is ("Build it
far from home."). Labels are lowercase nouns, as in vanilla. Inner
monologue and flavour lines (the Brain Squeezer) may be openly disturbing
but stay funny, never cruel, and never break the fourth wall.

## Adding New Material

1. Read this document and look at the existing sprites side by side.
2. Add a function to `Tools/make_textures.py` built from the shared
   helpers and palette constants; give new reusable parts their own
   helper.
3. Name the texture `BT_<Thing>` and put it in the vanilla folder for its
   category (`Things/Building/Power`, `Things/Item/Resource`, and so on);
   the XML regression tests reject defs that draw anything else.
4. Render it, inspect it at full size and at 64 px, and compare it with
   its neighbours in the same category.
5. If the new thing needs a colour, part or rule that is not here, add it
   to this document in the same commit.
