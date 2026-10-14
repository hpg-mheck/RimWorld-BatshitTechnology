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
      - **twin bombs** (requested 2026-10-13): two Tractor Bombs
        approach a hostile army from opposite directions in the middle
        of a junkyard, so their fields overlap. *(Proposal:)* a raid's
        worth of armed hostiles, a dozen or more, holding position
        among the junk. The checks:
        - nothing is lost;
        - nothing ping-pongs between the two pulls forever;
        - both bombs go off;
        - nothing logs an error;
      - nothing logs an error.
    - **Multi-bomb physics** (requested 2026-10-13): the physics gets
      more complicated with several pulls, without changing anything
      when there is only one. *(Proposals:)*
      - **Superposition.** Everything caught feels the sum of every
        active pull: a direction toward each centre, scaled by that
        pull's current rate. Each drag step moves it one cell along the
        net direction, at the net rate.
      - **Random bias jitter** (requested 2026-10-13): every pull
        calculation includes a little random bias, so that things do not
        cleanly stall in the middle; the slight noise pushes them one way
        or the other. *(Proposal:)*
        - Each caught thing gets a small, persistent bias of its own (a
          few degrees and a few per cent).
        - Every step adds a fresh flicker of noise on top.
        - At a balance point, the bias decides which pull wins.
        - The noise is never large enough to point a thing away from a
          lone pull.
      - **The emergent effects:**
        - tug-of-war across the overlap;
        - things wobbling at the balance point between two equal pulls,
          then tipping off it toward one bomb or the other;
        - a sudden lurch toward the survivor when one pull ends.
      - **Stretching:** a pawn held between two strong pulls takes a
        little extra stretching damage, the stronger the opposing pulls,
        the worse. *(Optional; it is a joke about spaghettification.)*
      - **One pull alone** keeps today's expected results: things only
        ever approach the centre, nothing is lost or shoved away, and
        everything is gathered in the same time. With jitter the exact
        cell-by-cell path may differ slightly. The existing single-bomb
        and grenade smoke checks must pass unchanged, and Core unit tests
        prove that one jittered pull never moves anything away from its
        centre.
      - **More tests:**
        - Core: two equal pulls cancel at their midpoint without
          jitter, and with it a thing at the midpoint always tips off
          within a few steps; net directions round to grid steps;
        - smoke: in the twin-bomb junkyard, nothing is lost, nothing
          moves against the net pull, things in the overlap end in a
          pile or at a balance point, and nothing logs an error.
      - **Absolute timeouts** (requested 2026-10-13): every implosion
        gets a minimum and a maximum duration in its settings.
        - Before the minimum, it keeps pulling even if everything is
          already gathered or settled, so there is always a show.
        - At the maximum, it ends however things stand, so balance
          points and tug-of-war can never hold a bomb open for ever.
        - *(Proposals:)* the Tractor Bomb runs 2 s minimum and 8 s
          maximum (its build used a 7.5 s cap). The grenade keeps
          exactly 5 s, with minimum and maximum equal, so its results
          are unchanged.
        - Unit tests pin both ends.
      - **Chain reaction:** planted bombs are damageable, so one bomb's
        blast sets off the other. Damage during a countdown starts that
        bomb's pull at once; damage during its pull sets off its blast
        at once.
12. **Showcase and docs.** The showcase becomes a directed tour (see
    `docs/SHOWCASE.md`, "The Directed Tour"): it takes the camera, shows
    one major feature at a time with on-screen commentary, and the
    Tractor Bomb gets its own scene, ending on the twin bombs in a
    junkyard ("Two tractors are better than one."). Then the README, the
    decisions log, and a merge into `trunk`.

### Done When

All of the steps above are complete, every test passes, the showcase
demo runs clean, and `feature/tractor-bomb` is merged into `trunk`.

### Decisions

All open questions were answered on 2026-10-13 (details in
`docs/TRACTOR-BOMB.md`, "Decisions"):

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

Decided after the core build (2026-10-13), to apply alongside the barn in
step 8:

- **Disposition,** a per-bomb setting governing how close the robot
  drives before planting:
  - **Cautious:** stops 15 tiles out, as first written.
  - **Moderate** *(proposed default)*: stops at 15 tiles or one tile
    inside its pull radius, whichever is nearer, so a target standing
    still is caught.
  - **Nosy:** drives right up, to a few tiles.
  
  Dial-A-Yield moves the Moderate stop with the pull.
- **Planted bombs are damageable:** damage during the countdown cuts it
  short, and the bomb goes off where it stands.
- **The machining-table stopgap** for assembling robots stays until the
  barn replaces it.

## Next

In recommended order, each on its own branch:

0. **Showcase API** (`feature/showcase-api`, `docs/SHOWCASE-API.md`): a
   game API for showcases, so players can use them normally, and a
   **Showcases** button on the main menu (mods, then their showcases,
   then Start). Built after the Tractor Bomb's mechanics, so that its
   showcase step (the directed tour) is built on it. Inside Batshit
   Technology for now, to be split into a framework mod later; the menu
   button is the mod's first, approved Harmony patch.
1. **Sound pipeline** (`feature/audio`): `Tools/make_sounds.py`,
   SoundDefs, and an audio section in the motif. Needed by the Colony
   Bomb and Mirror Mirror. Once it works, it also makes the Drag Show's
   original disco track, which replaces the party music the show uses
   until then.
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
3a. **The Drag Show** (`feature/drag-show`, `docs/DRAG-SHOW.md`): a
    ritual whose audience is dragged, literally, to center stage. It
    needs the sound pipeline (for disco), the shared implosion (with a
    room-limited, furniture-only setting), and new apparel art.
3b. **The Interstellar Delivery Locker and Drone**
    (`feature/delivery-locker`, `docs/DELIVERY-LOCKER.md`): mundane
    deliveries and an impressively explosive drone. It comes before the
    comedy showcase, which needs its water bottles.
3c. **The Stand-Up Comedy Showcase** (`feature/stand-up-comedy`,
    `docs/STAND-UP-COMEDY.md`): amateur comedy on the Drag Show's
    performance framework.
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
