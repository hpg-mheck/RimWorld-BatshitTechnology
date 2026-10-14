# The Tractor Bomb and the Tractor Barn

Status: specified, being built on branch `feature/tractor-bomb` (see
`ROADMAP.md`). Specified by Matt "Breakpoint" Heck on 2026-10-13, who
answered every open question the same day. A few smaller details are
still marked *(proposal)*; they stand unless he says otherwise.

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
  retargets if they move before it has emplaced *(proposal)*.
- **Emplacement:** it permanently emplaces itself, becoming a fixed bomb
  that cannot move again. The tires flip out flat over about half a second
  *(proposal)*.
- **Protest:** if a colonist is inside its pull radius when it is ready to
  emplace, it beeps and flashes in protest. After three extra seconds,
  too bad: it emplaces anyway.
  - The flashing is a lamp on the tractor blinking red and yellow.
  - The beeps use a vanilla sound until the mod's sound pipeline exists
    *(proposals)*.
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

Implementation *(proposals)*:

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

### Everyone Else Runs

Most pawns and critters are smart enough to flee from an armed or firing
Tractor Bomb, unless they are already too close, or berserk, rabid,
enraged and the like (specified 2026-10-13).

- **The vanilla flee is too short.** Vanilla already makes pawns sprint
  from an explosive that is about to go off
  (`JobGiver_FleePotentialExplosion`, through
  `Pawn_MindState.knownExploder`), but only to 9 tiles, short of the
  10-tile pull. It also applies only to humanlike-intelligence pawns.
- **A mod flee job giver** *(proposal)*: `JobGiver_FleeTractorBomb`,
  inserted into the Humanlike and Animal think trees by an XML patch, not
  Harmony.
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
