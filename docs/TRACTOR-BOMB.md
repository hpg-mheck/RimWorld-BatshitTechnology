# The Tractor Bomb and the Tractor Barn

Status: specified, being built on branch `feature/tractor-bomb` (see
`ROADMAP.md`). Specified by Matt "Breakpoint" Heck on 2026-10-13, who
answered every open question the same day. A few smaller details are
still marked *(proposal)*; they stand unless he says otherwise. Roadmap
steps 2 to 10 are built, the Tractor Barn (8) last (see "As Built"
below), with the decisions taken after the core build applied alongside
it, and so is step 11's multi-bomb physics (2026-10-14). On 2026-10-14 the
operator replaced Disposition and the Dial-A-Yield sliders with three named
settings ("Named Settings", below; built in slice 16000).

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
- **Approach:** when it finds any, it moves to within 15 tiles of them,
  or closer, as its Proximity says (built; see "Named Settings"). "Hostile
  forces" means pawns hostile to the player that it can see; it retargets
  if they move before it has emplaced (built).
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
  Grenade's. Everything within its pull is sucked in to within 1.5 tiles
  of the centre, then it explodes. As specified the pull was 10 tiles and
  the blast 5; since the named settings (2026-10-14) a bomb pulls 15
  tiles (Neighborly) and blasts 7.5 (Firework) unless set otherwise. It
  is built on the shared implosion extracted from the grenade, which the
  Colony Bomb reuses later.
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
  ticks it and saves it, and explodes when it reports itself `Over`:
  never before its 2-second minimum, then as soon as everything is
  `AllGathered` or `Settled` (nothing more can move), and at its
  8-second maximum however things stand (built; see "Multi-bomb
  physics" under "As Built").
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
  `Pawn_MindState.knownExploder`), but only to 9 tiles, short of even
  the 10-tile pull first specified. It also applies only to
  humanlike-intelligence pawns.
- **A mod flee job giver** (built): `JobGiver_FleeTractorBomb`,
  inserted into the Humanlike and Animal think trees by an XML patch, not
  Harmony. As built, it goes into their constant trees (see "As Built").
  - When an emplaced bomb arms, every pawn that can see it and stands
    within its pull radius plus 8 tiles sprints to a cell outside that
    distance. The distance follows the bomb's Pull setting.
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

A further research project, after Tractor Bomb. As first specified, once
it was researched each Tractor Bomb had two sliders: its tractor-beam
(pull) radius, 3 to 20 tiles, and its blast radius, 2 to 10 tiles, with
defaults of 10 and 5. The named settings below replaced them on
2026-10-14; Dial-A-Yield now unlocks Pull and Blast.

## Named Settings

Specified by the operator on 2026-10-14 ("three amusing names for each
of three settings"), with one amendment the same day: "keep the default in
the middle. However, bump up the middle and large settings by 50% each."
Built in slice 16000.

- **Proximity** (how close it drives): **Timid** stops 15 tiles out;
  **Prudent** (the default) at 15 tiles or one inside its pull, whichever
  is nearer; **Nosy** drives up to 3 tiles.
- **Pull** (the tractor beam): **Intimate**, 6 tiles; **Neighborly** (the
  default), 15; **Popular**, 22.5.
- **Blast:** **Birthday Cake**, 3 tiles; **Firework** (the default), 7.5;
  **Block Party**, 12.

- **Proximity** replaces Disposition and behaves the same: Timid was
  Cautious, Prudent was Moderate. It needs no research. Prudent stops 14
  tiles out at Neighborly, 5 at Intimate and 15 at Popular (its 15-tile
  emplacing range is nearer than 21.5).
- **Pull and Blast** need Tractor Bomb Dial-A-Yield; before it, every bomb
  pulls and blasts at the defaults, whatever is set. There are no custom
  values, only the three stops. The radii are exact: RimWorld's radial
  patterns and explosions take fractional radii.
- **Per unit:** a robot shows a button for each setting it may change; a
  planted bomb shows Pull and Blast until its pull starts. A click opens a
  menu of the three names with their sizes ("Popular, 22.5 tiles";
  "Prudent, stops 14 tiles out"). With several selected, the buttons
  group, and the choice applies to all of them.
- **Production defaults:** the Tractor Barn shows the same three buttons,
  which set what its new robots leave with. Robots from anywhere else
  take the defaults.
- **All units at once:** the barn's "Apply to every tractor" button copies
  its settings to every robot of its owner on its map, and to every
  planted bomb whose pull has not started (as far as each may be changed:
  before Dial-A-Yield, only a robot's Proximity).
- **The inspect pane** names the settings ("Prudent, Popular, Block
  Party"), then their sizes and the robot's stop.
- **Saves:** an older save's dialled numbers snap to the nearest stop (a
  pull of 3 to 10 tiles is Intimate, 11 to 18 Neighborly, 19 and 20
  Popular; a blast of 2 to 5 is Birthday Cake, 6 to 9 Firework, 10 Block
  Party). A radius the old version left at its default it never wrote,
  so that setting loads at the new default. Cautious, Moderate and Nosy
  load as Timid, Prudent and Nosy.

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
    is within its stopping distance of the nearest one, inside its area;
    it never leaves its area to chase. It looks again every half second,
    following or switching targets.
  - **Proximity** (Disposition until 2026-10-14; see "Named
    Settings"): how close it drives before it plants itself, saved with
    it. **Timid** stops 15 tiles out. **Prudent**, the default, stops one
    tile inside its tractor beam or 15 tiles out, whichever is nearer: 14
    tiles at the default pull. **Nosy** drives right up, to 3 tiles. The
    arithmetic is Core's `TractorBombApproach`. Once planted the setting
    no longer matters, so a planted bomb has none; the robot's inspect
    pane says how far it stops.
  - **Protest:** decided once, when it is ready: a colonist (free or
    enslaved) inside its pull radius. Three seconds of a beep every half
    second (vanilla's `TrapArm` click, for now) and a lamp on its control
    box blinking red and yellow six times a second, then it emplaces.
  - **Emplacing** takes the robot off the map for good, as a Gauranlen
    dryad returning to its tree is, and spawns the bomb facing the way
    the robot did.
  - **Inspect pane:** its settings by name, their sizes and its stop,
    its patrol area, its job ("patrolling", "closing in on ...",
    "protesting ...") and any protest timer; Health and Log tabs.
  - **Building one:** the Tractor Barn builds them (below). The
    machining-table stopgap that stood in until the barn is gone.
- **The emplaced bomb** (`Source/TractorBombEmplaced.cs`). Its tyres flip
  flat in half a second (three drawn frames), then it rolls a countdown
  of 3 to 5 seconds, every tick equally likely, under the shared warning
  band (`CountdownWarningBand`, shared with the self-destructs) and its
  rings. The implosion pulls at the bomb's pull radius, gathers to 1.5
  tiles, drags at the grenade's rates and rips fixtures loose as the
  grenade does; the bomb explodes (Bomb damage, 65) when its pull is
  over: not before 2 seconds (120 ticks), then as soon as everything is
  gathered or settled, and at 8 seconds (480 ticks) however things
  stand. The two limits are the implosion settings' `minDurationTicks`
  and `maxDurationTicks` in the bomb's XML.
  - **The pile:** pawns and items pass over the planted bomb, like
    sandbags, so its own cell is part of the pile: nine item stacks fit
    within 1.5 tiles, pawns without limit. Stacks beyond nine that cannot
    merge wait just outside, about 2 tiles out, and the blast (7.5 tiles
    at Firework; 3 at Birthday Cake still reaches them) catches them. The
    smoke test stages eleven such stacks.
  - **It cannot be stopped, only hurried** (decided after the core
    build): a hit that harms (a bullet, a blast, fire) before its pull
    starts, while its tyres flip or it counts down, starts the pull on
    the next tick; a hit while it pulls sets off its blast on the next
    tick. So one bomb's blast sets off a planted neighbour. It has no hit
    points to lose, so damage never destroys it, and damage that harms
    nothing (an EMP, a stun, firefoam) does nothing. It cannot be
    deconstructed or claimed, and no other implosion's pull rips it
    loose (`IImplosionProof`, which is about the pull, not damage). A
    robot's death vortex is only for robots wrecked before they plant;
    a hit planted bomb leaves none. The player sees a message, and DEV
    mode has a "Hit it" button.
- **Named settings** (slice 16000, replacing the Dial-A-Yield sliders
  and the Disposition button; see "Named Settings"). Core's
  `TractorBombYield` (the stops and radii, the effective stops before
  research, old saves' snapping), `TractorBombApproach` (Proximity's
  stops, old Dispositions) and `TractorBombDials` (the names, menu
  entries and pane summary, with `TractorBombSettings` holding all
  three). In game, `Command_TractorBombDial` (one button per setting,
  menu of three, grouped across a selection), `ITractorBombDials`
  (robot, planted bomb, barn) and `TractorBombSaves.LoadLegacy`. Robots
  and bombs save `proximity`, `pull` and `blast` by name. Icons:
  `UI/Commands/BT_<Setting><Stop>` and `BT_ApplyToEveryTractor`, from
  `Tools/make_textures.py`.
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
    (counting down or pulling) within its pull radius plus 8 tiles: 23 at
    the default pull, 14 at Intimate and 30.5 at Popular.
  - **Hostiles react individually** (Decisions 9): anyone not hostile to
    the bomb (colonists, guests, neutrals, wild animals) reacts at once.
    A pawn hostile to it (a raider, or an animal or insect of a hostile
    faction) rolls once, on its first sight of that bomb, a third each:
    it **flees** at once; it **hesitates**, reacting two seconds after it
    first saw the bomb (on its next half-second think, so 2 to 2.5 s);
    or it **ignores** the bomb entirely and carries on with its assault,
    until the pull, if it is in it, takes it anyway. The bomb keeps each
    pawn's roll and first sighting (saved), so nothing re-rolls every
    half second, and a pawn already running keeps running. The roll
    hashes the pawn's id, the bomb's id and a seed the bomb draws when
    planted (Core's `TractorBombHostiles`). Pawns in a mental state still
    never react.
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
- **The Tractor Barn** (roadmap step 8; `Source/TractorBarn.cs`,
  `Defs/ThingDefs/Buildings_TractorBarn.xml`). A 3 by 3 steel barn on the
  Production tab, behind the Tractor Bomb project: 250 steel, 8
  components and 20 batshittium. Its door, the interaction cell, is the
  cell south of its middle. It needs no power.
  - **Production:** vanilla's autonomous work table
    (`Building_WorkTableAutonomous`, Biotech's mech-gestator pattern).
    The decompile shows that it, `Bill_Autonomous` and the bill job's
    hauling into its container are base-game code with no DLC check (the
    only Biotech check on that path is the gestator's own job report), so
    the barn uses them with Core alone. Its one recipe,
    `BT_BuildTractorBomb` (60 steel, 3 components, 5 batshittium), has a
    forming time of one in-game hour (2,500 ticks), which makes its bills
    autonomous.
  - **No worker:** a hauler takes the bill (`BT_DoBillsTractorBarn`,
    Hauling, as vanilla's crematorium and campfire bills are), carries
    the parts into the barn and throws the switch (a second's work). The
    barn then builds the robot alone, drives it out of the door, uses up
    the parts and closes the bill's iteration itself, so no crafter ever
    comes to finish it (vanilla's gestator needs one).
  - **Bills:** ordinary ones. "Do until you have X" counts the barn
    owner's robots and planted bombs on the map
    (`RecipeWorkerCounter_TractorBombs`, through
    `TractorBombUtility.CountOnMap`), and the bill dialog says so. "Build
    one now" adds a "Do 1 times" bill. The bill shows the robot as its
    icon.
  - **Repair:** a robot below half health (Core's `TractorBarnRepair`)
    with nobody to hunt drives home (`JobGiver_TractorBombRepair`, after
    the hunt in its main think tree, and in its constant tree, where it
    breaks off a patrol leg at once) to the nearest of its barns whose
    door is free, leaving its patrol area if it must. One robot docks at
    a time; another keeps patrolling until the door is free. It parks at
    the door (`BT_TractorBombDock`) and the barn mends one hit point of
    its lightest injury every half second, then refits any missing part
    (a wheel, the emitter) one per step, as vanilla's mech repair does: a
    fifth of a unit of steel per hit point, two units per part. A robot
    at half health takes about 8 steel and 20 seconds. A hostile in sight
    breaks it off. Its status reads "returning to the tractor barn for
    repairs.", "being repaired at the tractor barn." or "waiting at the
    tractor barn for repair steel."
  - **Settings:** the barn's Proximity, Pull and Blast buttons (Pull and
    Blast once Dial-A-Yield is researched) set what its new robots leave
    with, saved with the barn and shown on its pane ("New tractors:
    Prudent, Neighborly, Firework"). "Apply to every tractor" copies them
    to its owner's robots on the map and to planted bombs not yet
    pulling, and says how many it set.
  - **Repair hopper:** the steel for repairs, a vanilla refuelable store
    of 20 that haulers top up below 75%. A work table with one takes no
    bills while it is empty, so the player can neither switch refuelling
    off nor dial its target down; while it is empty the barn starts no
    new bomb (one already building finishes) and mends nothing.
  - **Its death:** destroyed, it blows its doors off (a 3.9-tile Bomb
    blast, 25) and 6 to 10 spare tyres stampede out up to 14 tiles,
    bowling over what they land on (Blunt, 12).
- **Multi-bomb physics** (roadmap step 11, built 2026-10-14). Several
  pulls at once, without changing anything when there is only one.
  - **Absolute durations:** every implosion's settings carry a minimum
    and a maximum (`ImplosionSettings.minDurationTicks`,
    `maxDurationTicks`; Core's `ImplosionMath.IsOver`). Before the
    minimum it keeps pulling even if everything is gathered or settled;
    at the maximum it ends however things stand. The Tractor Bomb runs 2
    to 8 seconds (its build used a 7.5-second cap); the grenade's minimum
    and maximum are both its five-second ramp, so it ends exactly when
    it always did.
  - **Superposition** (`Source/ImplosionField.cs`, Core's
    `PullSuperposition`): the drag belongs to the map
    (`MapComponent_Implosions`). Each implosion still seizes pawns, rips
    fixtures and keeps its own clock, but the first to tick in a game
    tick drags every caught pawn and item once, under all of them. A
    thing feels the sum of every pull that reaches it, a direction
    toward each centre scaled by that pull's current rate, and steps one
    cell along the net direction at the net rate (a clock of its own).
    The pulls add up as a potential (each rate times the distance to its
    centre): a step must go strictly downhill, which under one pull is
    exactly "strictly closer to its centre", and the allowed steps are
    tried in order of how well they line up with the net direction, so
    the best is that direction rounded to a grid step. Items still merge
    or wait, never spawning into a full cell; pawns any pull reaches are
    held stunned while any pull holds them.
  - **Balance points:** pulls of constant strength cancel all along the
    line between two equal centres, not just at its midpoint, so things
    in the overlap are drawn onto that line and creep along it. A creeping
    thing keeps both pulls unsettled, so a tug-of-war runs to the
    8-second maximum; when one bomb goes off, its pull closes at once and
    everything lurches toward the survivor.
  - **Random bias jitter:** every pull calculation for a thing is turned
    by a few degrees and scaled by a few per cent. The persistent part is
    hashed from the thing's id and each implosion's seed (drawn from
    RimWorld's `Rand` when it opens): a turn of up to 6 degrees, and a
    strength set by the thing's own preference order among the pulls it
    feels, 4 per cent apart (with two, one at plus 2 per cent and one at
    minus 2). A fresh flicker from `Rand` on every step adds up to 3
    degrees and 1 per cent. Two flickers can never close the bias's gap,
    so at a balance point the bias always decides which pull wins, and
    the turns, under 22.5 degrees together, never point a thing away
    from a lone pull nor slow its gathering by a step.
  - **One pull alone** drags exactly as before: at its own rate (a
    newly caught thing joins its clock), only ever closer to its centre,
    and in as many steps (the cell path may differ by the jitter). An
    implosion tallies on its own clock: outstanding things (outside its
    gather radius) and restless ones (not yet tried, or moved at their
    last try).
  - **Stretching** (the roadmap's optional joke about spaghettification):
    a pawn held between strong opposing pulls takes stretching damage
    once a second (`BT_Stretch`, leaving a `BT_StretchMark`, "stretch
    mark"; armour does not help; "{0} has been spaghettified."). The
    damage follows how hard the pulls strain against each other (Core's
    `PullSuperposition.Tension`: their rates less their net pull): none
    up to 6 cells a second, then a point per 6 beyond, rounded up, at
    most 4; two Tractor Bombs head on at full pull do 3 a second.
  - **Shelves wait to be ripped** (found by the twin-bomb test): a
    fixture holding more stacks on a cell than bare ground can (a shelf)
    is ripped loose only once the drag has taken its extra stacks off
    it. Ripped at once, RimWorld re-placed the extra stacks near, away
    from the pull as often as not, and would drop any it found no room
    for. This applies to the grenade too.
  - **Chain reactions** behave: a bomb that goes off closes its pull,
    which leaves the drag at the next tick; its blast sets off a planted
    neighbour, pulling or not. `IImplosionProof` (a planted bomb) is now
    exempt from the drag as well as from being ripped loose.
- **For the next steps.** Robots are made with
  `TractorBombUtility.MakeRobot` or `SpawnRobot`, and a faction's bombs,
  planted ones included, are counted with `CountOnMap`. Armed bombs are
  found with `TractorBombUtility.ArmedBombs(map)`
  (`Building_TractorBomb.IsArmed`: counting down or imploding), and
  `ImplodeNow` cuts a countdown short (the DEV button and the smoke
  test).

## The Tractor Barn

Tractor Bombs are produced in the Tractor Barn, which builds and repairs
them autonomously. It costs 250 steel, 8 components and 20 batshittium.
Built as specified (see "As Built").

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
- **Repair:** a damaged Tractor Bomb (below about 50% health) returns
  to the barn when it has no target, docks, and is repaired over time
  without a worker. It uses a little steel.

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
10. **Named settings** (2026-10-14, replacing decision 3 and Disposition):
    Proximity (Timid, Prudent, Nosy), Pull (Intimate, Neighborly,
    Popular) and Blast (Birthday Cake, Firework, Block Party), the default
    in the middle, the middle and large sizes raised by half: pull 6, 15
    and 22.5 tiles; blast 3, 7.5 and 12. Settable per unit, for a whole
    selection, as the barn's production defaults, and for every tractor at
    once. See "Named Settings".
