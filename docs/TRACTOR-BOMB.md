# The Tractor Bomb and the Tractor Barn

Status: specified, being built on branch `feature/tractor-bomb` (see
`ROADMAP.md`). Specified by Matt "Breakpoint" Heck on 2026-10-13, who
answered every open question the same day. A few smaller details are
still marked *(proposal)*; they stand unless he says otherwise. Roadmap
steps 2 to 7, 9 and 10 are built (see "As Built" below); the Tractor
Barn (8) is next.

## The Tractor Bomb

An expendable robot that hunts for enemies and becomes a singularity
bomb when it finds them.

- **Looks:** a little steel farm tractor, the pun intended. A magenta
  tractor-beam emitter sits where the exhaust stack should be, and it
  carries one hazard band. When it emplaces, its four tires flip out flat
  like stabiliser pads (specified 2026-10-13), so a planted bomb is
  unmistakable from one still driving.
- **Patrol:** it wanders the map looking for hostile forces, within a
  vanilla allowed area set per bomb, which defaults to the whole map. Draw
  an area to fence it in.
- **Approach:** when it finds any, it moves to within 15 tiles of them.
  "Hostile forces" means pawns hostile to the player that it can see; it
  retargets if they move before it has emplaced (built).
- **Emplacement:** it permanently emplaces itself, becoming a fixed bomb
  that cannot move again. The tires flip out flat over about half a second
  (built).
- **Protest:** if a colonist is inside its pull radius when it is ready to
  emplace, it beeps and flashes in protest. After three extra seconds,
  too bad: it emplaces anyway.
  - The flashing is a lamp on the tractor blinking red and yellow.
  - The beeps use a vanilla sound until the mod's sound pipeline exists
    (built, with vanilla's `TrapArm` click).
- **Countdown:** 3 to 5 seconds (rolled), under the shared self-destruct
  warning band, yellow-orange then red, as the motif requires of every
  countdown.
- **Detonation:** a singularity effect like the Ergonomic Singularity
  Grenade's. Everything within 10 tiles is sucked in to within 1.5 tiles
  of the centre, then it explodes with a 5-tile radius. It is built on
  the shared implosion extracted from the grenade, which the Colony Bomb
  reuses later.
- **Research:** a new project, Tractor Bomb, after the Ergonomic
  Singularity Grenade.
- **Cost:** 60 steel, 3 components and 5 batshittium per bomb.
  Expendable should feel expendable.

Implementation (proposed, then built; see "As Built"):

- **As a pawn:** a player-owned robot of its own race, with no needs and
  no mood, running its own think tree: patrol, acquire a target, approach,
  protest if needed, emplace.
  - It is not a mechanoid, so Biotech's mechanitor rules never apply.
    Allowed areas work through its player settings. No drafting or
    Harmony is needed.
- **Emplacing** despawns the robot and spawns an emplaced-bomb building
  in its place, which runs the countdown.
- **If destroyed before it emplaces,** it gets a death of its own, per
  the motif: a sputtering miniature vortex that sucks in its own parts,
  and nothing else, before popping.
- **The implosion** is built (roadmap step 2): `Implosion`
  (`Source/Implosion.cs`), set by Core's `ImplosionSettings`, with a pull
  radius of 10 and a gather radius of 1.5. The emplaced bomb owns it,
  ticks it and saves it, and explodes as soon as it reports everything
  `AllGathered` or `Settled` (nothing more can move), or at a cap of its
  own: half again its drag time (built).
  - The pile holds one item stack per cell: nine within 1.5 tiles, since
    the planted bomb lets things pass over it. Any more stacks wait just
    outside, the bomb goes off once they settle, and its blast catches
    them (built).

### Everyone Else Runs

Most pawns and critters are smart enough to flee from an armed or firing
Tractor Bomb, unless they are already too close, or berserk, rabid,
enraged and the like (specified 2026-10-13).

- **The vanilla flee is too short.** Vanilla already makes pawns sprint
  from an explosive that is about to go off
  (`JobGiver_FleePotentialExplosion`, through
  `Pawn_MindState.knownExploder`), but only to 9 tiles, short of the
  10-tile pull. It also applies only to humanlike-intelligence pawns.
- **A mod flee job giver** (built): `JobGiver_FleeTractorBomb`,
  inserted into the Humanlike and Animal think trees by an XML patch, not
  Harmony. As built, it goes into their constant trees (see "As Built").
  - When an emplaced bomb arms, every pawn that can see it and stands
    within its pull radius plus 8 tiles sprints to a cell outside that
    distance. The distance follows the bomb's dialled pull radius.
  - Animals count, as "critters" requires.
  - It sits below the mental-state branch of the think tree, so pawns who
    are berserk, manhunting (rabid or enraged), or otherwise out of their
    minds never flee. Neither do downed pawns that cannot crawl, or pawns
    under a forced player order.
- **Already too close:** a pawn inside the bomb's blast radius when it
  arms freezes where it stands. Its status line (the job report under
  its name) changes to one of the lines below, picked at random. Anyone
  who can run, does. Once the pull begins, everyone inside the pull
  radius is caught anyway, as with the grenade.

### Making Peace

The twenty status lines for a pawn frozen inside the blast radius. Like
vanilla job reports, they are lowercase and read after the pawn's name.
They live in the job def's XML, so they can be translated.

1. making peace with their impending doom
2. reconsidering every decision that led here
3. composing a very short memoir
4. wishing they had read the manual
5. accepting the tractor's terms
6. saying goodbye to a favourite chair
7. counting down along with it
8. hoping it's a dud
9. bargaining with physics
10. regretting not running sooner
11. thinking about the good times
12. admiring the engineering, briefly
13. standing very, very still
14. pretending to be furniture
15. revising their will, mentally
16. squinting hopefully at the countdown
17. apologising to everyone they ever knew
18. wondering who built this thing
19. finding inner peace on a deadline
20. waving at the tractor

## Tractor Bomb Dial-A-Yield

A further research project, after Tractor Bomb. Once it is researched,
each Tractor Bomb has two sliders, independent of each other: its
tractor-beam (pull) radius, 3 to 20 tiles, and its blast radius, 2 to 10
tiles. The defaults stay 10 and 5.

## As Built

Built on 2026-10-13 (roadmap steps 3 to 7, 9 and 10). Where the
build settled a detail, it is recorded here; the decisions log
(`docs/AI-DECISIONS.txt`) has the reasons.

- **Research.** `BT_Research_TractorBomb` (1,500) follows the Ergonomic
  Singularity Grenade; `BT_Research_TractorBombDialAYield` (1,000)
  follows it. Both sit to the grenade's right on the Batshit Technology
  tab.
- **The robot** (`Source/TractorBomb.cs`, `TractorBombAI.cs`;
  `Defs/ThingDefs/Pawns_TractorBomb.xml`). Race and pawn kind
  `BT_TractorBomb`, with a flesh type of its own (`BT_TractorSteel`):
  not a mechanoid, an animal or a humanlike, so no mechanitor, bandwidth
  or overseer rules apply even with Biotech, and it has no needs, no
  mood and no corpse. ToolUser, as vanilla's mechanoids and Odyssey's
  drones are. Its body is a chassis with a control unit, an engine, four
  wheels and the emitter; losing three wheels wrecks it rather than
  leaving it downed. It moves at 3.6 tiles a second.
  - **Patrol:** walks to random reachable spots within 30 tiles, inside
    its allowed area. The area is vanilla's per-pawn allowed area, which
    every player pawn has; since vanilla shows that menu only for
    colonists, animals and mechs, the robot has its own "Patrol:" button
    that opens vanilla's area menu (Unrestricted, any area, Manage
    areas) for the whole selection.
  - **Hunt:** it sees a hostile pawn within 45 tiles in line of sight,
    in explored ground, standing and a real threat (not downed, dormant
    or invisible). It drives to the reachable cell nearest itself that
    is within 15 tiles of the nearest one, inside its area; it never
    leaves its area to chase. It looks again every half second,
    following or switching targets.
  - **Protest:** decided once, when it is ready: a colonist (free or
    enslaved) inside its pull radius. Three seconds of a beep every half
    second (vanilla's `TrapArm` click, for now) and a lamp on its control
    box blinking red and yellow six times a second, then it emplaces.
  - **Emplacing** takes the robot off the map for good, as a Gauranlen
    dryad returning to its tree is, and spawns the bomb facing the way
    the robot did.
  - **Inspect pane:** its yield, its patrol area, its job ("patrolling",
    "closing in on ...", "protesting ...") and any protest timer; Health
    and Log tabs.
  - **Building one** is temporary: a bill, "assemble tractor bomb", at
    vanilla's machining table (60 steel, 3 components, 5 batshittium,
    3,000 work) whose robot drives out beside the crafter. The Artificer
    takes no bills (it is fed batshittium only), so the machining table
    stands in until the barn.
- **The emplaced bomb** (`Source/TractorBombEmplaced.cs`). Its tyres flip
  flat in half a second (three drawn frames), then it rolls a countdown
  of 3 to 5 seconds, every tick equally likely, under the shared warning
  band (`CountdownWarningBand`, shared with the self-destructs) and its
  rings. The implosion pulls at the bomb's pull radius, gathers to 1.5
  tiles, drags at the grenade's rates and rips fixtures loose as the
  grenade does; the bomb explodes (Bomb damage, 65) as soon as
  everything is gathered or settled, or at 450 ticks.
  - **The pile:** pawns and items pass over the planted bomb, like
    sandbags, so its own cell is part of the pile: nine item stacks fit
    within 1.5 tiles, pawns without limit. Stacks beyond nine that cannot
    merge wait just outside, about 2 tiles out, and the 5-tile blast
    catches them. The smoke test stages eleven such stacks.
  - **It cannot be stopped:** it takes no damage, cannot be
    deconstructed or claimed, and no other implosion rips it loose.
- **Dial-A-Yield.** Whole tiles: tractor beam 3 to 20, blast 2 to 10.
  Each robot has the two sliders, and so does a planted bomb until its
  pull starts; with several selected, one slider sets them all. Before
  the project is researched a bomb uses 10 and 5, whatever was dialled.
- **The death** (`Source/TractorBombDeath.cs`). Its six parts (four
  tyres, a piece of the hood, the emitter) burst out up to about 2 tiles,
  hang, and spiral back into a sputtering vortex over about two seconds;
  then it pops. Nothing else is touched. There is no corpse and no
  "Death" letter, only a message.
- **Running away** (`Source/TractorBombFlee.cs`,
  `Patches/BT_TractorBomb_Flee.xml`; the arithmetic is Core's
  `TractorBombFlee`). `JobGiver_FleeTractorBomb` is patched into
  vanilla's *constant* think trees for humanlikes, animals (dryads
  included) and insects, the ones asked every half second; the main
  trees are asked only when a job ends, too late for a three-second
  countdown. It sits first inside the gate that already turns away pawns
  in a mental state (berserk, manhunting, every other), drafted pawns,
  downed pawns that cannot crawl, and burning or sleeping ones. Pawns
  under a forced order, stunned pawns and the robots themselves are
  turned away too. Every pawn and critter race in the game and its DLCs
  runs one of the three trees; mechanoids, drones, babies and Anomaly's
  entities do not run.
  - **Who reacts:** anyone in a clear line of sight of an armed bomb
    (counting down or pulling) within its pull radius plus 8 tiles: 18 at
    the default yield, 11 to 28 as dialled.
  - **Running:** a sprint to the nearest cell, by steps, more than a tile
    beyond that distance from every bomb in sight, that vanilla would
    accept as a flee destination (reachable, standable, unclaimed, not a
    doorway or dangerous ground). A pawn with nowhere to go carries on.
    Its status reads "running from a tractor bomb.", and colonists show
    vanilla's fleeing icon.
  - **Too close:** a pawn inside the blast radius when it first reacts
    freezes where it stands, facing the bomb, until the bomb goes off or
    its pull moves the pawn. A pawn already running keeps running. Its
    status is one of the twenty "Making Peace" lines, kept in the freeze
    job's XML (translatable as
    `BT_TractorBombFreeze.modExtensions.0.lines`), chosen per pawn from
    its id and the bomb's, so it never changes while that bomb holds it.
    Other factions' frozen pawns show their lines too. A player can still
    draft a colonist out of it.
  - **The pull:** once it starts, anyone inside it is the implosion's,
    as with the grenade; it is given no job, and the pull's first tug
    ends its flee or freeze.
  - **Vanilla's own flee:** `JobGiver_FleePotentialExplosion` never fires
    for the bomb, which does not announce itself the vanilla way. The
    mod's giver runs ahead of it, and a humanlike that also knows of a
    vanilla explosive about to go off within 9 tiles picks a cell clear
    of both.
- **For the next steps.** The barn makes robots with
  `TractorBombUtility.MakeRobot` or `SpawnRobot`, and counts a faction's
  bombs, planted ones included, with `CountOnMap`. Armed bombs are found
  with `TractorBombUtility.ArmedBombs(map)` (`Building_TractorBomb.IsArmed`:
  counting down or imploding), and `ImplodeNow` cuts a countdown short
  (the DEV button and the smoke test).

## The Tractor Barn

Tractor Bombs are produced in the Tractor Barn, which builds and repairs
them autonomously. It costs 250 steel, 8 components and 20 batshittium.

- **Bills:** the barn takes ordinary bills, such as "Do X times" or "Do
  until you have X". "Do until you have X" counts the player's Tractor
  Bombs on the map, emplaced ones included, so the barn keeps a standing
  patrol topped up.
- **No worker:** haulers bring the ingredients into the barn, and then it
  builds the bomb on its own. Biotech's mech gestator works the same way
  (`Building_WorkTableAutonomous`). Whether that class and its jobs work
  without Biotech must be checked first; if they don't, the barn copies
  the Artificer's own autonomous pattern.
- **On demand:** a "Build one now" button simply adds a "Do once" bill,
  so there is only one mechanism to learn.
- **Repair** *(proposal)*: a damaged Tractor Bomb (below about 50% health)
  returns to the barn when it has no target, docks, and is repaired over
  time without a worker. It uses a little steel.

## Decisions

Answered by Matt "Breakpoint" Heck on 2026-10-13:

1. **Research:** after the Ergonomic Singularity Grenade.
2. **Colonists in the radius:** a protest of beeping and flashing, then
   it emplaces anyway after three extra seconds.
3. **Dial-A-Yield:** pull 3 to 20 tiles, blast 2 to 10.
4. **The barn:** autonomous bills, with "Build one now" adding a bill.
5. **Costs:** 60 steel, 3 components and 5 batshittium per bomb; 250
   steel, 8 components and 20 batshittium for the barn.
6. **Patrol:** an allowed area per bomb, defaulting to the whole map.
7. **Too close:** inside the blast radius, frozen, with one of twenty
   status lines; anyone who can run, does.
8. **Looks:** a little farm tractor whose tires flip out flat when it
   emplaces.
9. **Hostiles react individually** (answered after step 6, which made
   every hostile flee and so let most targets escape). Each hostile
   pawn, on first seeing an armed bomb, rolls once, with a 33% chance
   of each reaction:
   - **flees at once,** as built;
   - **hesitates,** noticing about 2 s late, so a 3 to 5 s countdown
     leaves little time and many are caught *(the 2 s is a proposal)*;
   - **ignores it entirely,** carrying on with its assault.
   
   Everyone else (colonists, guests, neutrals and animals) bails at
   once. The roll is kept per pawn per bomb, so a hostile does not
   re-roll every half second. To be built with step 8.
