# Batshit Technology Roadmap

**The Future: Sooner. Cheaper. Funnier. And ludicrously dangerous.**

This is the plan for what comes next, in order. Specifications live in
`docs/`; the task-level backlog is `docs/AI-TODO.txt`; questions waiting
on Matt "Breakpoint" Heck are in `docs/AI-requests-for-humans.txt`.

## How Work Is Done

- **One feature, one branch.** Each feature is built on its own branch,
  `feature/<name>`, cut from `trunk`. It merges back into `trunk` when it
  is complete: unit tests, XML tests, the Def validator and the in-game
  smoke test all pass, and the docs are updated.
- **Specification first.** A feature's design document is settled, with
  its open questions answered or their proposals accepted, before the
  code grows past groundwork.
- **Tests with the code.** Pure logic goes in `BatshitTechnology.Core`
  with unit tests; game-side behaviour gets smoke-runner checks, each
  proved to fail on the old code first.
- **The showcase grows with the mod.** A finished feature gets a demo in
  `Tools/smoke-test.ps1 -Showcase` where it makes sense.

## Done

- **v0.8:** the reactor, Random Replicator, Transmogrifier, Brain Squeezer,
  Batshit Ballista with magazines and Artificer, the Ergonomic Singularity
  Grenade, self-destructs and death blasts.
- **AnyRim:** the showcase's test town (`docs/SHOWCASE.md`).

## Now: The Tractor Bomb

Branch: `feature/tractor-bomb`. Specification: `docs/TRACTOR-BOMB.md`.

An expendable robot that patrols for enemies, closes to 15 tiles,
emplaces itself, counts down 3 to 5 seconds, drags everything within 10
tiles to within 1.5 of itself, and explodes over 5 tiles. Dial-A-Yield
makes both radii adjustable per bomb. The Tractor Barn builds and
repairs bombs on its own. Everyone with sense runs.

### Steps

1. **Settle the specification.** Answer the open questions below; fold
   the answers into `docs/TRACTOR-BOMB.md`.
2. **Groundwork: the reusable implosion.** Extract the singularity
   grenade's pull (seizing pawns, ripping fixtures loose, dragging items
   without shoving or losing them) into a shared implosion with its own
   pull radius, gather radius and timing, and move the grenade onto it.
   The Colony Bomb reuses it later. The grenade's smoke checks must still
   pass unchanged.
3. **Research.** Tractor Bomb, and Tractor Bomb Dial-A-Yield after it, on
   the Batshit Technology tab.
4. **The robot.** A player-owned robot race with no needs or mood and its
   own think tree: patrol, spot a hostile, approach to 15 tiles, emplace.
   Not a mechanoid, so Biotech's mechanitor rules never apply. No Harmony
   (see the Harmony policy in `docs/AI-DECISIONS.txt`).
5. **The emplaced bomb.** A building that replaces the robot where it
   stops: a rolled 3 to 5 second countdown under the shared warning band,
   then the implosion and a 5-tile blast.
6. **Running away.** A flee job giver, added to the humanlike and animal
   think trees by XML patch, that sends anyone who can see an armed bomb
   and is not in a mental state beyond its pull radius.
7. **Dial-A-Yield.** Two sliders per bomb, pull and blast, once
   researched.
8. **The Tractor Barn.** Autonomous production on ordinary bills ("Do
   until you have X" counts bombs on the map), a "Build one now" button,
   and docking repair for damaged bombs.
9. **Deaths.** A robot destroyed before it emplaces gets its own death
   per the motif: a sputtering vortex that eats its own parts and pops.
10. **Art.** Robot, emplaced bomb and barn sprites through
    `Tools/make_textures.py`, per `docs/ARTISTIC-MOTIF.md`, with any new
    rule added to the motif.
11. **Tests.**
    - Core: implosion gathering and timing, Dial-A-Yield clamps, the
      countdown roll, the flee distance.
    - XML: the new defs, research placement, the think-tree patch.
    - Smoke:
      - the barn builds a bomb from a bill;
      - a bomb finds a hostile, stops within 15 tiles, emplaces, counts
        down, pulls everything in its radius to within 1.5 tiles and
        explodes;
      - pawns and animals outside the "too close" range flee, and
        berserk ones don't;
      - the interlock, Dial-A-Yield and repair docking work;
      - nothing logs an error.
12. **Showcase and docs.** A Tractor Bomb demo, the README, the decisions
    log, and a merge into `trunk`.

### Done When

All of the steps above are complete, every test passes, the showcase
demo runs clean, and `feature/tractor-bomb` is merged into `trunk`.

### Open Questions

Each has a proposed answer in `docs/TRACTOR-BOMB.md`:

1. Which research does Tractor Bomb follow? Proposed: the Ergonomic
   Singularity Grenade.
2. Should a bomb refuse to emplace while a colonist is inside its pull
   radius? Should that be switchable, or is friendly fire the point?
3. Dial-A-Yield ranges: proposed 3 to 20 tiles for the pull and 1.9 to
   9.9 for the blast.
4. The barn: bills worked autonomously, with "Build one now" adding a
   bill (proposed), or something else?
5. Ingredients and costs for a Tractor Bomb and the barn.
6. Do patrols stay inside the home area or roam the whole map?
7. "Already too close": inside the blast radius (proposed) or the pull
   radius?
8. What does it look like? The name invites a small farm tractor with a
   tractor-beam emitter on top.

## Next

In recommended order, each on its own branch:

1. **Sound pipeline** (`feature/audio`): `Tools/make_sounds.py`,
   SoundDefs, and an audio section in the motif. Needed by the Colony
   Bomb and Mirror Mirror.
2. **Colony Bomb family and Colony Deployment Shelter**
   (`feature/colony-bomb`, `docs/COLONY-BOMB.md`). Groundwork first:
   - town size and quality tiers for the AnyRim generator;
   - the implosion from the Tractor Bomb, at larger radii;
   - one shared immunity check that every blast and pull consults.
   
   Then the bombs (variants, Artificer Manual/Auto toggle, arming,
   disarming, the countdown and its audio, the sequence, the town
   reveal, Terror-Joy) and the shelter. Its showcase demo ends with
   "Holy shit!" and "I know, right?".
3. **Mirror Mirror** (`feature/mirror-mirror`, `docs/MIRROR-MIRROR.md`):
   the recreation mirror and its mirror-universe duplicates.
4. **Antiparticle Board** (`feature/antiparticle-board`): the material
   and its research chain, Substandard Advanced Materials and Awkward
   Secrets of the Universe.
5. **Firing positions** (`feature/firing-positions`): pawns shooting out
   of a thing that holds them. Built for the blimp; the shelter adopts
   it.
6. **Batshit Blimp and Whoops Drive** (`feature/blimp`,
   `docs/BATSHIT-BLIMP.md`): the hardest engine work (flight, being shot
   at in the air, shooting from it), and the most open questions.

## Tuning and Upkeep

Between features, or alongside them:

- **The singularity grenade's blast** (65 damage) tends to kill the
  volunteer it just comforted; Shary died in the showcase.
- **Fire and self-destructs:** fire counts as damage, so a burning
  self-destruct device rolls its 5% trigger every fire tick and will
  almost certainly go off. Possibly the right amount of batshit.
- **Numbers from play:** the reactor's heat and the Ballista.
- **RimWorld 1.7 beta** ("Waylith" engine): a trial build and smoke run;
  stay on 1.6 until 1.7 is released.
- **Art:** hand-drawn replacements for the procedural sprites, if wanted.
