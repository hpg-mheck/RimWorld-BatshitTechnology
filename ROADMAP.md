# Batshit Technology Roadmap

**The Future: Sooner. Cheaper. Funnier. And ludicrously dangerous.**

This is the plan for what comes next, in order. Specifications live in
`docs/`; the task-level backlog is `docs/AI-TODO.txt`; questions waiting
on Matt "Breakpoint" Heck are in `docs/AI-requests-for-humans.txt`.

## How Work Is Done

- **Phases and slices.** Each feature is a phase, numbered in thousands
  (1000, 2000, ...) so an unexpected phase can be wedged in later (2500)
  without renumbering. A phase's steps are its slices, also numbered in
  thousands and starting again at 1000 in every phase. Ubersight shows the
  same numbers (`AGENTS.md`, "FieldManual and Ubersight"), and this file is
  the recovery anchor for them.
- **One feature, one branch.** Each phase is built on its own branch,
  `feature/<name>`, cut from `trunk`. It merges back into `trunk` when it
  is complete: unit tests, XML tests, the Def validator and the in-game
  smoke test all pass, and the docs are updated.
- **Specification first.** A feature's design document is settled, with
  its open questions answered or their proposals accepted, before the
  code grows past groundwork.
- **Tests with the code.** Pure logic goes in `BatshitTechnology.Core`
  with unit tests; game-side behaviour gets smoke-runner checks, each
  proved to fail on the old code first.
- **The showcase grows with the mod.** A finished feature gets a scene in
  the showcase where it makes sense.

## Phases at a Glance

| Phase | Feature | Branch | State |
|---|---|---|---|
| 1000 | Foundations (v0.8) | `trunk` | Done |
| 2000 | AnyRim | `trunk` | Done |
| 3000 | Tractor Bomb | `trunk` | Done |
| 4000 | Showcase API | `trunk` | Done |
| 5000 | Sound pipeline | `feature/audio` | **Now** (slice 1000) |
| 6000 | Colony Bomb and Shelter | `feature/colony-bomb` | Planned |
| 7000 | Mirror Mirror | `feature/mirror-mirror` | Planned |
| 8000 | The Drag Show | `feature/drag-show` | Planned |
| 9000 | Delivery Locker and Drone | `feature/delivery-locker` | Planned |
| 10000 | Stand-Up Comedy | `feature/stand-up-comedy` | Planned |
| 11000 | Antiparticle Board | `feature/antiparticle-board` | Planned |
| 12000 | Firing positions | `feature/firing-positions` | Planned |
| 13000 | Batshit Blimp | `feature/blimp` | Planned |
| 14000 | All-Weather Battery | `feature/all-weather-battery` | Backlog |
| 15000 | Grenade Overcharge | `feature/grenade-overcharge` | Backlog |

## Done

- **Phase 1000, Foundations (v0.8):** the reactor, Random Replicator,
  Transmogrifier, Brain Squeezer, Batshit Ballista with magazines and
  Artificer, the Ergonomic Singularity Grenade, self-destructs and death
  blasts.
- **Phase 2000, AnyRim:** the showcase's test town (`docs/SHOWCASE.md`).
- **Phase 4000, Showcase API** (merged 2026-10-14; `docs/SHOWCASE-API.md`,
  "As Built"): a **Showcases** button at the bottom of the main menu's
  column (the mod's only Harmony patch), the mods, showcases and details
  windows, a data-driven director with captions and narration, throwaway
  showcase games with no autosave, `-showcase=<defName>`, and
  `BT_Showcase` wrapping today's demos. Built and adversarially reviewed by
  a workflow: 29 review findings, 34 of 35 skeptic checks upheld, all
  fixed (among them god mode leaking into the player's next colony).
  317 unit and XML tests; smoke 131/131 twice. Questions 14 to 16 in
  `docs/AI-requests-for-humans.txt` came out of it.
- **Phase 3000, Tractor Bomb** (merged 2026-10-15; `docs/TRACTOR-BOMB.md`,
  "As Built"; the tour in `docs/SHOWCASE.md`): the robot, the emplaced
  bomb and its implosion, flee and freeze, Dial-A-Yield's named settings
  (pulls of 9, 22.5 and 33.75 tiles), the Tractor Barn, Disposition and
  hostile reactions, deaths and art; and the directed tour that shows it
  off: ten narrated scenes on a pinned seed (tour-04), a clean stage, a
  quiet screen, tourists as its unfortunates, and cuts that keep to the
  map. 842 unit and XML tests; smoke 234/0; the tour 26/0 on tour-04
  and tour-17, each map and stage repeating across processes. Items 23
  and 24 of `docs/AI-requests-for-humans.txt` are open from its last
  night.

## Now: Phase 5000, Sound Pipeline

Branch: `feature/audio`, cut from `trunk` at the Phase 3000 merge.
Specification: `docs/SOUND-PIPELINE.md` (drafted 2026-10-14 for sounds
synthesized from code, with 13 open questions).

On 2026-10-15 the operator opened the phase, widening its sources:
"We're going to work with what we've got right now. Merge and proceed to
Phase 5000. Now, we have both sound effect and music needs. For the
music, since we're not doing dynamic composition during actual gameplay,
I'm open to either real-time synthesis, or, and probably preferable,
AI-guided composition resulting in high-quality output. I'm open to using
a service for this. So, slice 1000 here is a research task. Present me
with some options. Take some time. Budget, though, is $50 across the
whole thing, sound effects and music, if possible." The $50 is for
services, not for Claude's own use.

### Slices

| Slice | Work | State |
|---|---|---|
| 1000 | Research: sources for music and effects | Now |

The slices after 1000 are planned once the operator has chosen among the
options.

## Phase 3000, Tractor Bomb (Done)

Branch: `feature/tractor-bomb`, merged into `trunk` on 2026-10-15.
Specification: `docs/TRACTOR-BOMB.md` (its "As Built" section describes
what slices 1000 to 11000 delivered).

An expendable robot that patrols for enemies, drives in as far as its
Disposition allows, emplaces itself, counts down, drags everything in its
pull radius to itself, and explodes. Dial-A-Yield sets both radii per
bomb. The Tractor Barn builds and repairs bombs on its own. Everyone with
sense runs.

### Slices

| Slice | Work | State |
|---|---|---|
| 1000 | Settle the specification | Done |
| 2000 | Groundwork: the reusable implosion | Done |
| 3000 | Research | Done |
| 4000 | The robot | Done |
| 5000 | The emplaced bomb | Done |
| 6000 | Running away (flee and freeze) | Done |
| 7000 | Dial-A-Yield | Done |
| 8000 | The Tractor Barn, Disposition, hostile reactions | Done |
| 9000 | Deaths | Done |
| 10000 | Art | Done |
| 11000 | Tests and multi-bomb physics | Done |
| 12000 | Directed tour, docs | Done |
| 13000 | Narration overlay | Done |
| 14000 | Boomalope stampede | Done |
| 15000 | Raccoon Incident | Done |
| 16000 | Named yield settings | Done |
| 17000 | Operator's viewing | Done |
| 18000 | Showcase seeds, best of 32 | Done |
| 18500 | Pull radii up by half | Done |
| 19000 | Tourists, viewing feedback | Done |
| 20000 | Merge into `trunk` | Done |

Slice 11000 delivered the twin-bomb junkyard test and the multi-bomb
physics: superposed pulls with random bias jitter, absolute durations (the
Tractor Bomb 2 to 8 s, the grenade exactly 5 s), the chain reaction, and
stretching damage for pawns held between opposing pulls. 510 unit and XML
tests and 137 smoke checks pass. Request 13 in
`docs/AI-requests-for-humans.txt` asks for confirmation of its numbers.

Slice 12000: the showcase becomes a directed tour (`docs/SHOWCASE.md`,
"The Directed Tour") built on the Showcase API. It takes the camera, shows
one major feature at a time with on-screen commentary, and the Tractor
Bomb gets its own scene, ending on the twin bombs in a junkyard ("Two
tractors are better than one."). Then the README, the decisions log, and
the merge into `trunk`.

Built on 2026-10-14 (overnight, by a workflow): the Showcase API gained
scenes that decline a map (G1) and beats (G2); `BatshitTourDirector` plays
eight scenes with narration, the Tractor Bomb and the junkyard twins last;
`Tools/smoke-test.ps1 -Showcase -Verify` checks a tour run end to end.
Adversarially reviewed and fixed. 638 unit and XML tests, smoke 190/190,
tour verification 11/11. Open before the merge: the operator watches the
tour (`Tools/smoke-test.ps1 -Showcase`), answers requests 17 and 18 in
`docs/AI-requests-for-humans.txt`, and reviews the narration's wording;
two tour faults (colonist deaths) are in `docs/AI-TODO.txt`.

The operator watched it on 2026-10-14 and asked for slices 13000 to
16000, built the same day and recorded in the specifications
(`docs/SHOWCASE.md`, `docs/SHOWCASE-API.md`, `docs/TRACTOR-BOMB.md`):

- **13000, narration overlay:** one oversized line with a drop shadow,
  fading in and out, silent, in place of vanilla's message stack.
- **14000, boomalope stampede:** a new scene after Home Defence, in which
  one 256-bolt volley meets hundreds of boomalopes from the east.
- **15000, the Raccoon Incident:** built as its own scene after the
  junk-pile grenade, which stays ("test both").
- **16000, named yield settings:** Proximity (Timid, Prudent, Nosy), Pull
  (Intimate, Neighborly, Popular) and Blast (Birthday Cake, Firework,
  Block Party), set per robot, for every robot at once, and as the
  barn's production defaults. The tour's tractors go up a setting.
- **17000:** the operator watched again on 2026-10-14 and started a
  showcase from the main menu's Showcases button: "Pretty sweet. Needs
  work on the sequence, but the general capability definitely works."
- **18000, showcase seeds:** the operator, 2026-10-14: "Showcases
  should be able to specify the random number seed used for map
  generation, to get repeatable results. Implement this, and try 32
  variations of the demo scenario. Select the best one." A showcase
  def names its seed; the tour is tried on 32 seeds and pinned to the
  best. Done 2026-10-15: `BT_Showcase` pins `tour-04`, an arid
  shrubland (`docs/SHOWCASE.md`, "The Tour's Seed"); the tour-wide
  faults the trials showed are in `docs/AI-TODO.txt` for 19000.
- **18500, pull radii:** the operator, 2026-10-15: "expand the pull
  radii for all tractor levels by 50%." Intimate, Neighborly and
  Popular go from 6, 15 and 22.5 tiles to 9, 22.5 and 33.75; the
  tour's staging follows. Done 2026-10-15: old saves keep the stops
  they meant; the emplacing range (Timid, 15) stays, so Prudent stops
  at 15 at the two larger pulls (request 23 asks whether it should
  move); the tour's yard moves 11 tiles further south and its single
  tractor is Nosy, so its pull spares the barn
  (`docs/TRACTOR-BOMB.md`, "Named Settings"; `docs/SHOWCASE.md`).
- **19000, tourists:** a small party of visitors as the tour's
  unfortunates, in place of colonists (the colonists are already held
  clear of every blast until it is over); deferred from the review step,
  with its questions in `docs/AI-requests-for-humans.txt` item 21. The
  operator's feedback from the viewing goes in with it. On 2026-10-15
  the operator watched the tour on its pinned seed ("Not bad; good
  enough for now") and said "Fine, finish Phase 3000" to the plan:
  tourists (a hidden showcase-only faction; a tourist as the grenade's
  volunteer and the tractor's stray), a clean stage (map hostiles,
  the volley zone's trees, clear weather), less clutter (dev mode,
  the Learning helper, stale alerts, letters and messages), and the
  camera and framing faults the seed trials found. Step 19000a, a clean
  stage, done 2026-10-15: the stage removes the map's own hostiles
  before it clears the fog (the "Sorne Geneline" insects were a
  temple's hive, woken by the clearing), every anima tree and the
  volley zone's trees, and the Showcase API holds the weather clear
  (`docs/SHOWCASE.md`, "A Clean Stage"; `docs/SHOWCASE-API.md`, "The
  Held Weather"). Step 19000b, clutter and capture, done 2026-10-15: the
  Showcase API keeps the screen quiet for a def that asks (no alerts,
  Learning helper, letters or top-left messages; `docs/SHOWCASE-API.md`,
  "The Quiet Screen"), the tour lends no god mode, viewing runs turn dev
  mode and the Learning helper off, `-Presentation` verifies the tour at
  its own speed with the viewing prefs, frames are taken a few ticks
  after their cues, and `Tools\contact-sheet.ps1` tiles them into
  sheets. Step 19000c, tourists, done 2026-10-15: three to five
  visitors of a hidden faction made for showcases arrive with the stage
  and stand where the scenes can reach them (the stampede's keep-away
  line, beside the pads, the grenade's pile, the tractor's pull); the
  grenade's volunteer and the tractor's stray are tourists, the robot
  protests about a friendly guest as about a colonist, a lost guest is
  narrated, and the group goes home, alive or dead, when the showcase
  ends (`docs/SHOWCASE.md`, "Tourists"). Step 19000d, camera and
  framing, done 2026-10-15: each scene opens with a cut to its own
  place as its caption appears, no shot shows the black beyond the
  map (the Showcase API's `keepOnMap`), the raccoon's throw is framed
  clear of the caption bar, the tractor yard has junk for its pull,
  and the twins are framed closer (`docs/SHOWCASE.md`, "Camera and
  Framing"). What "needs work on the sequence" means is asked in
  `docs/AI-requests-for-humans.txt`, item 24. A read-only review of the
  night's work (two lenses) found five faults, none high; all were
  fixed (tourists sent home when a saved tour is loaded, inert yard
  junk, the README, the stampede's wording), and the implosion's smoke
  flake was traced and fixed on the way (`docs/AI-KNOWN-ISSUES.txt`).
- **20000, the merge into `trunk`,** done 2026-10-15 after the lead's
  own gate on the final code: build clean, 842 unit and XML tests,
  smoke 234/0, tour-04 and tour-17 26/0 twice each (MAPHASH and
  STAGEHASH repeating), and a presentation capture read on contact
  sheets.

### Decisions

Answered on 2026-10-13 (details in `docs/TRACTOR-BOMB.md`, "Decisions"):

- **Research:** after the Ergonomic Singularity Grenade.
- **Colonists in the radius:** the tractor beeps and flashes in protest
  for three extra seconds, then emplaces anyway.
- **Dial-A-Yield:** pull 3 to 20 tiles, blast 2 to 10.
- **The barn:** autonomous bills, with "Build one now" adding one.
- **Costs:** 60 steel, 3 components and 5 batshittium a bomb; 250 steel,
  8 components and 20 batshittium for the barn.
- **Patrol:** an allowed area per bomb, defaulting to the whole map.
- **Too close:** pawns inside the blast radius freeze, with one of twenty
  "making peace with their impending doom" status lines; anyone who can
  run, does.
- **Looks:** a little farm tractor with a magenta tractor-beam emitter;
  its tires flip out flat when it emplaces.
- **Disposition,** per bomb: Cautious stops 15 tiles out, Moderate (the
  default) at 15 tiles or one tile inside its pull radius, whichever is
  nearer, and Nosy drives right up.
- **Planted bombs are damageable:** damage during the countdown cuts it
  short, and the bomb goes off where it stands.
- **Hostiles react individually:** each rolls once per bomb, a third
  each: flee at once, hesitate about 2 s, or ignore it. Everyone else
  bails.

## Next

In order, each on its own branch:

- **Phase 5000, Sound pipeline** (`feature/audio`, `docs/SOUND-PIPELINE.md`,
  drafted 2026-10-14 with 13 open questions):
  `Tools/make_sounds.py`, SoundDefs, and an audio section in the motif.
  Needed by the Colony Bomb and Mirror Mirror. Once it works, it also
  makes the Drag Show's original disco track, which replaces the party
  music the show uses until then.
- **Phase 6000, Colony Bomb family and Colony Deployment Shelter**
  (`feature/colony-bomb`, `docs/COLONY-BOMB.md`). Groundwork first:
  - town size and quality tiers for the AnyRim generator;
  - the implosion from the Tractor Bomb, at larger radii;
  - one shared immunity check that every blast and pull consults.

  Then the bombs (variants, Artificer Manual/Auto toggle, arming,
  disarming, the countdown and its audio, the sequence, the town
  reveal, Terror-Joy) and the shelter. Its showcase scene ends with
  "Holy shit!" and "I know, right?".
- **Phase 7000, Mirror Mirror** (`feature/mirror-mirror`,
  `docs/MIRROR-MIRROR.md`): the recreation mirror and its mirror-universe
  duplicates.
- **Phase 8000, The Drag Show** (`feature/drag-show`,
  `docs/DRAG-SHOW.md`): a ritual whose audience is dragged, literally, to
  center stage. It needs the sound pipeline (for disco), the shared
  implosion (with a room-limited, furniture-only setting), and new apparel
  art.
- **Phase 9000, The Interstellar Delivery Locker and Drone**
  (`feature/delivery-locker`, `docs/DELIVERY-LOCKER.md`): mundane
  deliveries and an impressively explosive drone. It comes before the
  comedy showcase, which needs its water bottles.
- **Phase 10000, The Stand-Up Comedy Showcase**
  (`feature/stand-up-comedy`, `docs/STAND-UP-COMEDY.md`): amateur comedy
  on the Drag Show's performance framework.
- **Phase 11000, Antiparticle Board** (`feature/antiparticle-board`): the
  material and its research chain, Substandard Advanced Materials and
  Awkward Secrets of the Universe.
- **Phase 12000, Firing positions** (`feature/firing-positions`): pawns
  shooting out of a thing that holds them. Built for the blimp; the
  shelter adopts it.
- **Phase 13000, Batshit Blimp and Whoops Drive** (`feature/blimp`,
  `docs/BATSHIT-BLIMP.md`): the hardest engine work (flight, being shot at
  in the air, shooting from it), and the most open questions.
- **Phase 14000, All-Weather Battery** (`feature/all-weather-battery`;
  backlogged by Matt "Breakpoint" Heck on 2026-10-14): a battery that
  can stand out in the rain. Vanilla's batteries short-circuit in rain
  when unroofed and holding more than 100 Wd
  (`ShortCircuitUtility.TryShortCircuitInRain`), draining the net and
  exploding, which is why the tour's boomalope stampede (slice 14000)
  runs the Ballista from the reactor alone, at 76 tiles. With it, the
  stampede could store power for a longer reach. Design open: what it
  costs, and what it does instead of shorting out.
- **Phase 15000, Grenade Overcharge** (`feature/grenade-overcharge`;
  backlogged by Matt "Breakpoint" Heck on 2026-10-14): "the Ergonomic
  Singularity Grenade should have an Overcharge option. This takes five
  seconds, cannot be aborted, and doubles the radius and power of all
  effects." Today's grenade pulls over 10 tiles for exactly 5 s
  (`Defs/ThingDefs/Items_SingularityGrenade.xml`); overcharged,
  20 tiles at double strength and damage. The operator's design, the
  same day: the Overcharge button is simply a toggle. If it is active
  when the grenade is thrown, a five-second countdown locks the pawn in
  place and commits it to the throw, which happens when the countdown
  ends. If the pawn dies, is incapacitated, or drops the grenade (if
  practical) for some reason, the countdown keeps going, and the
  grenade detonates wherever it is when the countdown expires, without
  a throw. Showing it: "Needless to say, Ned should use the Overcharge
  option. Poorly." Ned "Overkill" Lessly overcharges in the tour's
  Raccoon Incident (Tractor Bomb slice 15000), and it goes about as well
  as his name suggests; the census revises accordingly.

## Tuning and Upkeep

Between phases, or alongside them (not phases themselves):

- **The singularity grenade's blast** (65 damage) tends to kill the
  volunteer it just comforted; Shary died in the showcase.
- **Fire and self-destructs:** fire counts as damage, so a burning
  self-destruct device rolls its 5% trigger every fire tick and will
  almost certainly go off. Possibly the right amount of batshit.
- **Numbers from play:** the reactor's heat and the Ballista.
- **RimWorld 1.7 beta** ("Waylith" engine): a trial build and smoke run;
  stay on 1.6 until 1.7 is released.
- **Art:** hand-drawn replacements for the procedural sprites, if wanted.
- **Def validation in the build:** done 2026-10-14 (the ValidateDefs
  target; `docs/AI-DECISIONS.txt`), documented in the RimWorld pack and
  offered to FieldManual as #3 (BT-ECR-2026-012).
- **Records migration:** proposal BT-PROP-2026-001 (moving these records
  into FieldManual's layout) awaits a decision; proposed for right after
  a phase merges.
