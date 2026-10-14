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
| 3000 | Tractor Bomb | `feature/tractor-bomb` | **Now** (slice 12000) |
| 4000 | Showcase API | `trunk` | Done |
| 5000 | Sound pipeline | `feature/audio` | Next |
| 6000 | Colony Bomb and Shelter | `feature/colony-bomb` | Planned |
| 7000 | Mirror Mirror | `feature/mirror-mirror` | Planned |
| 8000 | The Drag Show | `feature/drag-show` | Planned |
| 9000 | Delivery Locker and Drone | `feature/delivery-locker` | Planned |
| 10000 | Stand-Up Comedy | `feature/stand-up-comedy` | Planned |
| 11000 | Antiparticle Board | `feature/antiparticle-board` | Planned |
| 12000 | Firing positions | `feature/firing-positions` | Planned |
| 13000 | Batshit Blimp | `feature/blimp` | Planned |

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

## Now: Phase 3000, Tractor Bomb

Branch: `feature/tractor-bomb`. Specification: `docs/TRACTOR-BOMB.md`
(its "As Built" section describes what slices 1000 to 11000 delivered).

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
| 12000 | Directed tour, docs, merge into `trunk` | Now |

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
