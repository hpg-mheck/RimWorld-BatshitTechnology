# The Tractor Bomb and the Tractor Barn

Status: designed and backlogged, not yet implemented. Specified by Matt
"Breakpoint" Heck on 2026-10-13. Points his specification leaves open are
marked *(proposal)* and listed under "Open Questions" and in
`docs/AI-requests-for-humans.txt`.

## The Tractor Bomb

An expendable robot that hunts for enemies and becomes a singularity
bomb when it finds them.

- **Patrol:** it wanders around the map, looking for hostile forces.
- **Approach:** when it finds any, it moves to within 15 tiles of them.
- **Emplacement:** it permanently emplaces itself, becoming a fixed bomb
  that cannot move again.
- **Countdown:** 3 to 5 seconds (rolled), wearing the shared
  self-destruct warning band, yellow-orange then red, as the motif
  requires of every countdown.
- **Detonation:** a singularity effect like the Ergonomic Singularity
  Grenade's. Everything within 10 tiles is sucked in to within 1.5 tiles
  of the centre, then it explodes with a 5-tile radius. It reuses the
  generalized implosion backlogged for the Colony Bomb.
- **Research:** the new project Tractor Bomb enables it *(proposal:
  after the Ergonomic Singularity Grenade)*.

*(Proposals:)*

- **As a pawn:** a player-owned robot of its own race, with no needs and
  no mood. It runs its own think tree: patrol, acquire a target, approach,
  emplace.
  - It is not a mechanoid, so Biotech's mechanitor rules never apply, and
    no drafting or Harmony is needed.
  - "Hostile forces" means pawns hostile to the player that it can see.
  - It retargets if they move before it has emplaced.
- **Emplacing** despawns the robot and spawns an emplaced-bomb building
  in its place, which runs the countdown.
- **Safety interlock:** the robot will not emplace while a colonist is
  inside its pull radius. The question is whether this should be
  switchable, or whether friendly fire is the point.
- **If destroyed before it emplaces,** it needs a death of its own per
  the motif: a sputtering miniature vortex that sucks in its own parts,
  and nothing else, before popping.

## Tractor Bomb Dial-A-Yield

A further research project. Once it is researched, each Tractor Bomb has
its tractor-beam radius and its blast radius adjustable independently, by
the player, through two sliders on the bomb. *(Proposal:)* the ranges are
3 to 20 tiles for the pull and 1.9 to 9.9 for the blast. The defaults stay
10 and 5.

## The Tractor Barn

Tractor Bombs are produced in the Tractor Barn, which builds and repairs
them autonomously. The specification allows either on demand or on
production bills. *(Proposal:)* use production bills, the vanilla way:

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
- **Repair:** a damaged Tractor Bomb (below about 50% health) returns to
  the barn when it has no target, docks, and is repaired over time
  without a worker. It uses a little of the bill's ingredients, or steel.

## Open Questions

1. Which research does Tractor Bomb follow? Proposed: the Ergonomic
   Singularity Grenade.
2. Safety interlock: refuse to emplace near colonists, switchable, or
   none?
3. Dial-A-Yield ranges: 3 to 20 for the pull and 1.9 to 9.9 for the
   blast?
4. The barn: bills worked autonomously, as proposed, with "Build one now"
   adding a bill?
5. Ingredients and costs for a Tractor Bomb and the barn?
6. Does a patrolling Tractor Bomb stay inside the home area or roam the
   whole map? The specification says the map; the home area would keep it
   out of trouble.
