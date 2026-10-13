# Colony Bomb and Colony Deployment Shelter

Status: designed and backlogged, not yet implemented. Specified by Matt
"Breakpoint" Heck on 2026-10-13, who answered the first round of
questions the same day. Where the specification is still silent or
ambiguous, this document proposes an answer and marks it *(proposal)*;
those points are also listed under "Open Questions" and in
`docs/AI-requests-for-humans.txt`.

## Summary

The Colony Bomb deploys a finished town by first deleting whatever was
there. A pawn emplaces it, a pawn arms it, and it counts down with a red
ring, a strobing warning triangle and three layers of alarm tones. Then it
goes quiet, sucks everything within its radius into the centre, blows a
raspberry, and erupts in hundreds of rolling blasts under a mushroom cloud.
When the fires die and the smoke clears, a fully built colony town stands
where the old ground was, of a size and quality set by the bomb's variant.

The Colony Deployment Shelter is the only way to sit through one. It is a
2 by 2 bunker for up to six pawns with a single-use forcefield that makes
it immune to every Batshit Technology effect, for 15 seconds, give or take
five. Then the bunker explodes.

## Variants

A bomb's full name is `[<quality>-Quality] [<size>]Colony Bomb`; both
prefixes are optional, and the plain "Colony Bomb" is Medium-Quality and
standard size. Examples: "Slum-Quality MiniColony Bomb", "Resort-Quality
MegaColony Bomb", "Epic-Quality Valu-Siz™ Colony Bomb".

### Size

| Size | Radius | The town it leaves |
|------|--------|--------------------|
| Mini | 9 * | One house, its yard and a stub of road |
| Standard | 15 | A short street, one or two lots a side |
| Mega | 35 * | AnyRim's size: main and cross street, 3 to 5 lots a side |
| Valu-Siz™ | 45% * | A grid of several blocks |

Radii are in tiles; \* marks a proposal. The Valu-Siz™ radius is 45% of
the map's shorter side: 112 tiles on a 250 by 250 map.

The Valu-Siz™ is meant to be colossal, leaving little safe ground on the
map. Its radius is relative to the map so that it stays colossal on every
map size. Every size counts down for 15 seconds *(proposal)*.

### Quality

Quality sets the town's materials, roads, furniture, stock and extras.
It is rolled when the bomb is crafted, from the crafter's skill, through
the vanilla quality comp. Bombs that turn up at random rather than being
crafted (from traders, quests or raiders, for example) get a random
quality. Vanilla's seven quality levels map onto six tiers:

| Tier | Vanilla | Walls | Roads | Furniture and stock |
|------|---------|-------|-------|---------------------|
| Slum | Awful | Wood, damaged, gaps | Packed dirt | Awful, sparse, filth |
| Low | Poor | Wood | Gravel | Poor, simple meals |
| Medium | Normal | Wood, sandstone | Concrete | As in AnyRim |
| High | Good | Granite | Concrete, lined | Good, gardens, fences |
| Resort | Excellent | Marble | Paved tile | Excellent, art, feasts |
| Epic | Masterwork+ | Gold, jade | Gold tile | Masterwork+, silver |

"Masterwork+" means Masterwork or Legendary.

**The Artificer gets a Manual/Auto toggle.** It currently makes its
output automatically, with no crafter, so there is no skill to roll
quality from:
- **Manual:** a pawn works the bench, and the bomb's quality is rolled
  from that pawn's Crafting skill.
- **Auto:** the bench works alone, as now, and quality tops out at the
  quality of the Artificer itself. The Artificer therefore gains a
  quality of its own, rolled from its builder's Construction skill.
  Below that cap, auto quality is rolled as for a crafter of middling
  skill (level 8) *(proposal)*.
- Existing Artificer output (bolts, singularity grenades) has no quality
  and is unaffected by the toggle *(proposal)*.

Labels follow the motif: lowercase, as in vanilla ("resort-quality
megacolony bomb"), except the trademark, which keeps its capitals
("Valu-Siz™ colony bomb"). If the game font lacks the ™ glyph, write
"(TM)".

## The Colony Bomb

### Emplacing and Arming

- The bomb is made at the Batshittium Artificer *(proposal)*, behind a new
  research project after the Ergonomic Singularity Grenade *(proposal)*,
  and comes out packaged, like an uninstalled building.
- A pawn emplaces it at a position the player selects, using the vanilla
  install order.
- Arming is a separate order: a pawn works at the bomb for about ten
  seconds. The bomb reuses the self-destruct arming job and designation
  (`CompSelfDestruct`, with `armingTicks` 600).
- As a Batshit device it inherits the house rule: any hit has a 5% chance
  to start the countdown, and half of those skip it.

### Disarming

An armed bomb can be *attempted* to be disarmed, by order, during its
countdown. A pawn works at it for about ten seconds *(proposal)*; then
the attempt resolves:

| Chance | Outcome |
|--------|---------|
| 50% | Nothing happens; the countdown carries on; try again |
| 25% | Success: the bomb crumbles into its component parts |
| 25% | Misfire: see below |

**Misfire.** The bomb leaves a water-filled crater the size of its blast
radius, with no implosion, shockwave or town *(proposal: deep water in
the middle, shallow water toward the rim, so the edge can be waded)*.
Everything in the crater sinks and is lost *(proposal)*; pawns caught in
it must swim to shore, if they can. A Colony Deployment Shelter inside
the crater shorts out and sinks, whether or not its forcefield is up. Its
pawns are left in the water to swim to shore, if they can, and then the
shelter disintegrates. Vanilla pawns cannot enter deep water, so swimming
needs its own mechanic *(proposal: a swim job that moves a pawn one cell
at a time toward the nearest shore, slowly; downed or immobile pawns
drown)*.

### Timeline

The times below are for the standard bomb; every size uses the same
sequence with its own radius.

| When | What happens |
|------|--------------|
| T-15 s | Red ring, warning triangle, bass tone each second |
| T-10 s | Add the rising DTMF tone, every 500 ms |
| T-5 s | Add the TTMF tone, 200 ms on and 50 ms off |
| T 0 | Every tone stops at once; a fading echo |
| 1 to 5 s (random) | Silence and a soft wind |
| Until done | The implosion drags everything to the centre |
| On arrival | A short, loud "THPPPT!!" |
| 1 to 3 s (random) | Nothing, without warning |
| About 3 s | Shockwave of rolling blasts; a mushroom cloud rises |
| About 10 s | Fires rage; the cloud spreads and dissipates |
| 5 to 10 s | The smoke slowly clears and reveals the new town |

### Countdown Visuals

- **Red warning ring** at the danger radius, pulsing. It reuses the pulse
  arithmetic in `Core.CountdownPulse` and is red (`DANGER_RED`) throughout.
- **Warning triangle.** A very large yellow-orange triangle with an
  exclamation point, over the bomb (about three tiles across). Its
  brightness cycles rapidly: it fades linearly from full brightness to
  black, then snaps back to full. (The specification calls this a
  triangle wave; the shape described is a falling sawtooth, and that shape
  is what will be built.) Period 0.5 s *(proposal)*.
- These depart from the motif's rule that every countdown reuses the
  self-destruct warning band. The operator specified them, and the motif
  document gains a "bombs" exception when this is built.

### Countdown Audio

This would be the mod's first custom audio. Sounds would be synthesized
by a new `Tools/make_sounds.py`, the audio twin of `make_textures.py`,
written as WAV files and wrapped in SoundDefs. The motif document gains an
audio section in the same change.

- **Bass warning tone,** every second from T-15: a bassy, sharp-attack
  tone of about 350 ms (a 55 Hz fundamental with harmonics) *(proposal)*.
- **Rising DTMF tone,** every 500 ms from T-10: a mid-range DTMF pair
  (770 and 1336 Hz, the keypad's "5") synthesized with triangle waves.
  Each beep is 1/20 octave higher than the one before, so the pitch climbs
  a full octave by T 0 *(proposal; see Open Questions)*. Each beep lasts
  250 ms.
- **High TTMF tone,** from T-5: 200 ms on, 50 ms off. "Tri-tone
  multi-frequency" extends DTMF with a third frequency group:
  - DTMF's two groups are 697, 770, 852 and 941 Hz, and 1209, 1336, 1477
    and 1633 Hz. Adjacent tones are about 1.105 apart, and the groups are
    1.285 apart.
  - Continuing that pattern gives a third group of 2098, 2319, 2562 and
    2831 Hz.
  - A TTMF symbol plays one tone from each group. The countdown plays the
    top row, 941, 1633 and 2831 Hz *(proposal)*, raised an octave if
    testing finds it not shrill enough.
- **T 0:** all three stop at once. A short echo of the last moment repeats
  three or four times, each about 6 dB quieter, 250 ms apart *(proposal)*.
- **The lull:** a soft wind fades in.
- **THPPPT:** short and loud, a comic raspberry (a buzzy low sawtooth
  with fast amplitude flutter) *(proposal)*.
- **Shockwave:** vanilla explosion sounds on the rolling blasts, under one
  deep boom.
- Sounds are scheduled on game ticks, so they speed up with game speed
  and stop when the game pauses, as vanilla sounds do.

### The Implosion

The same pull as the Ergonomic Singularity Grenade, over the bomb's
radius. The singularity's pull code would be extracted into a reusable
implosion that both use:

- Pawns and animals are seized at once and dragged in, held stunned.
- Items are dragged one cell at a time and pile at the centre, merging
  where they match (never shoved aside or lost; see the singularity
  fixes in `docs/AI-DECISIONS.txt`).
- Constructed buildings and installed furniture are uninstalled (if they
  can be) or demolished. Plants are uprooted.
- Natural rock and overhead mountain roof within the radius are blasted
  away, so that the town gets level ground *(proposal)*.
- It starts suddenly but slowly and accelerates. It ends when a full pass
  moves nothing, with a cap of 20 s for large radii *(proposal)*. Then
  comes the THPPPT.
- Anything the rules protect is left alone: an active Colony Deployment
  Shelter, things that cannot be destroyed (steam geysers), and the map
  edge.

### Shockwave, Cloud and Fires

- **Ground zero.** Everything gathered at the centre is destroyed outright
  when the shockwave starts: items, buildings, corpses, and the pawns and
  animals that were dragged in *(proposal)*.
- **Rolling blasts.** The wave front runs from the centre to the radius
  over about three seconds. Explosions fall at random points just behind
  it, as in the Ballista's big volleys:
  - about 200 for the standard bomb, scaling with area;
  - capped at 1,500 for the Valu-Siz™, for performance *(proposal)*.
  - They use `BombSuper`, because a vanilla `Bomb` wave puts out fires.
- **Mushroom cloud.** A stem and cap rise from the centre over about six
  seconds, then spread and fade over the next ten. It is drawn by an
  ethereal thing like the singularity field, scaled to the radius. New
  sprites per the motif: a smoke-grey cloud whose underside glows magenta
  from the batshittium fireball. They need new smoke palette constants.
- **Fires** are lit behind the wave, as in the replicator's self-destruct,
  and rage for about ten seconds.
- **Smoke.** Blinding smoke (the 1.6 gas grid) fills the radius as the
  fires peak.
- **Building the town.** All fires in the radius are put out, and the town
  is built under the smoke cover. It goes up a few lots per tick, to
  spread the cost. The smoke then thins until the town shows.

### The Town

The town comes from the generator built for AnyRim, the showcase's test
town (see `docs/SHOWCASE.md`). That generator therefore takes the area to
fill (a circle of the bomb's radius, clipped to the map), a quality tier,
a seed and a list of obstacles to build around. It scales from one house
up to a grid of blocks, as in the size table above. *(Proposals:)*

- The town belongs to the player who set off the bomb.
- It has no residents; the colonists move in.
- It is stocked according to its quality.
- The terrain within the radius is levelled: water and marsh become soil.

A finished town adds a lot of wealth at once, and RimWorld's storytellers
scale raids with colony wealth. That is the catch, and it is intended.

## The Colony Deployment Shelter

### The Bunker

- A constructed 2 by 2 bunker, behind the same research project
  *(proposal)*. It is drawn per the motif: a riveted steel hatch, a
  magenta field emitter and one hazard band.
- It holds up to six pawns. They enter by a float-menu order and leave
  through an "Evacuate" button that ejects everyone. Pawns inside are off
  the map, as in a cryptosleep casket, so blasts cannot reach them
  directly.
- **Entering while the forcefield is deployed** incinerates and
  annihilates the pawn and everything they carry and wear, with a
  disturbing =\*FOOMPF!\*= effect: no corpse, no gear, nothing left.
  Pawns already inside are safe. The float-menu order stays available,
  with a warning in its label *(proposal)*.
- Pawns inside cannot shoot. Shooting from inside is future work, through
  the general "firing positions" component backlogged in
  `docs/AI-TODO.txt` (shared with the Batshit Blimp). It would never be
  allowed while the forcefield is up.

### The Forcefield

- **Single use, deployed by hand.** It is deployed only by a button;
  there is no automatic deployment.
- **Looks unreliable.** A magenta overlay cloud, asymmetric, its shape
  forever shifting (several overlapping blob sprites driven by noise), its
  alpha fading at random.
- **Immunity.** While, and only while, the field is active, the bunker and
  everyone in it are immune to:
  - every Colony Bomb and Batshit Technology effect: the bomb's implosion
    and shockwave, self-destruct blasts and aftermaths, reactor barrages,
    death blasts, singularity pulls and rips, and bolts;
  - all ordinary damage (raiders' bullets, mortars, fire);
  - gravity and displacement effects (anything that pulls, pushes or
    teleports);
  - thermal effects (heat and cold);
  - environmental effects such as acid rain and toxic fallout.

  What is already inside a pawn carries on: sick or diseased pawns still
  suffer their illnesses, and bleeding continues. Every Batshit effect
  asks one shared check (for example `BatshitImmunity.Protects(thing)`),
  the bunker's damage handler absorbs everything while the field is up,
  and pawns inside are held at a comfortable temperature.
- **Duration.** It lasts 15 seconds, plus or minus five, of "lowest
  bidder" government contracting. That is a uniform 10 to 20 s, rolled
  when deployed and never shown. The inspect pane says only "Rated for
  15 s (±5 s)".
- **Failure warnings.** For the last 5 s *(proposal)* the field flickers
  hard, the self-destruct warning band appears around the bunker
  (yellow-orange, then red), an alert tone sounds, and a message says
  "Shelter forcefield failing: get out!". The game knows the rolled
  duration, so the warning is honest even though the player never knows
  the number.
- **Failure.** When the field expires, the bunker explodes, energetically
  but only within about three tiles. Pawns still inside are thrown out
  into the blast. As a Batshit device it needs its own blast: the field
  collapses inward with a magenta flash, then bursts *(proposal)*. If the
  bunker is destroyed before its field is ever deployed, it goes off the
  same way.

### Timing Gamble

From T 0 the bomb's sequence takes roughly 10 to 25 seconds before the
shockwave, depending on the lull, the pull and the pause. The field lasts
10 to 20. Deploy too early and the field may fail before the shockwave.
That is the game: watch the warnings and decide whether to stay in or
run.

### Colony Bomb Terror-Joy

Pawns who ride out a Colony Bomb deployment inside a shelter, and live,
come out changed. Added by Matt "Breakpoint" Heck on 2026-10-13.

- **When:** only if the bomb's blast effects actually touched the
  shelter while the pawns were inside it. It happens whether or not the
  forcefield was up; if it was not, they have bigger problems.
- **Terror:** first a temporary psychosis, about 30 seconds of violent
  insanity (a short berserk mental state, `BT_ColonyBombTerror`)
  *(proposal: 1,800 ticks, starting as the pawn leaves the shelter)*.
- **Joy:** then cathartic joy in its wake, a strong mood buff that lasts
  about five minutes *(proposal: five minutes at normal speed, 18,000
  ticks, about seven in-game hours)*, with a line of its own per the
  motif's Words rules.
- **Bookkeeping:** the bomb's sequence records which shelters its blasts
  reached, and the shelter marks everyone inside at that moment. Pawns
  who entered after the blast passed get nothing.

### Showcase Demo

When the bomb is built, the showcase (`docs/SHOWCASE.md`) gets a
Colony Bomb demo after its existing ones. Its punchline, added by Matt
"Breakpoint" Heck on 2026-10-13:

- Two colonists stand just outside the danger radius, on the side
  nearest the camera's resting point, a couple of tiles apart.
- When the smoke has fully cleared, the camera pans down toward them,
  but off-centre: the new town stays the main subject, with the pair near
  the bottom of the frame and their speech bubbles in view.
- Pawn 1 says "Holy shit!". A beat (about 1.5 s *(proposal)*). Pawn 2
  says "I know, right?".
- Vanilla's interaction bubbles show only icons, so the lines are drawn
  as speech-style text above each pawn, long enough to read (about 3 s
  each *(proposal)*). The log gets a capture cue, `[BT-SHOWCASE]
  HOLYSHIT`.
- The line is the operator's own, profanity included; the motif's
  "funny, never cruel" rule is satisfied.

## Interactions and Edge Cases

- **Destroyed mid-countdown:** the bomb follows the self-destruct rule; it
  goes off anyway. The sequence runs in a map component, as the
  self-destruct aftermaths do, so it outlives the bomb and survives saving
  and loading at any stage.
- **Overlapping bombs:** they run independently. A later bomb can flatten
  an earlier bomb's town.
- **Huge radii:** the Valu-Siz™ radius exceeds the engine's radial cell
  pattern (about 79.8 tiles), so it walks a clipped rectangle and filters
  cells by distance. Pulls, fires and blasts are spread over ticks and
  capped.
- **Other factions:** pawns caught in the radius are pulled and killed
  like anyone else. Goodwill consequences follow vanilla rules for
  explosions.

## Implementation Plan

1. **Shared groundwork:**
   - the AnyRim town generator, with size and quality tiers (in progress
     for the showcase);
   - a reusable implosion extracted from the singularity field;
   - the `BatshitImmunity` check wired into every existing effect;
   - the audio pipeline (`make_sounds.py`, SoundDefs, and a motif audio
     section).
2. **The Colony Bomb:**
   - defs: four sizes, with quality via the quality comp;
   - the recipe and research project;
   - arming and countdown visuals and audio;
   - the sequence map component, cloud and fires;
   - the town reveal.
3. **The Colony Deployment Shelter:**
   - the bunker and its entering, holding and evacuating;
   - the forcefield and its overlay;
   - the failure warnings and blast.
4. **Tests:**
   - Core unit tests: the countdown audio schedule, the sawtooth, the TTMF
     frequencies, the shelter's duration range, radius by size, the
     quality mapping, and the town fitting its circle around obstacles;
   - XML tests: variants, labels and sound files;
   - smoke checks: a standard bomb from arming to revealed town; an
     active shelter's pawns surviving it; the shelter's failure blast; no
     logged errors;
   - a showcase demo.
5. **Later:** shooting from inside the shelter.

## Open Questions

Answered on 2026-10-13 (now part of the specification above): quality
comes from the crafter's skill, with a Manual/Auto toggle on the
Artificer; the forcefield is deployed by hand only; it stops all
ordinary, displacement, thermal and environmental harm; and an armed bomb
can be disarmed at 50/25/25 odds.

Still open:

1. Sizes: are radii of 9, 15 and 35, and 45% of the map's shorter side,
   about right? Does every size count down for 15 s?
2. "Triangle pitch-rising DTMF": a triangle-wave tone whose pitch climbs
   beep by beep (proposed), or a pitch glide within each beep?
3. The new town: owned by the player, empty, and stocked by quality?
4. Ground zero: are the pulled-in pawns killed outright, and are their
   corpses left or vaporized?
5. Auto quality: below the bench's cap, roll as for a middling crafter
   (proposed), or always produce exactly the bench's quality?
6. Misfire crater: does everything in it sink and vanish, and how deep is
   it (proposed: deep in the middle, wadeable at the rim)?
7. Disarming: about ten seconds of work per attempt, like arming?
