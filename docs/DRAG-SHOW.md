# The Drag Show

Status: specified and backlogged, not yet built. Specified by Matt
"Breakpoint" Heck on 2026-10-13. Points his specification leaves open are
marked *(proposal)* and listed under "Open Questions" and in
`docs/AI-requests-for-humans.txt`.

## The Idea

A ritual in which the audience is, quite literally, dragged to the stage.
The joke is the pun, and it is affectionate. The emcee is the star, the
audience has a wonderful time, and nobody is the butt of anything except
physics. Text follows the motif's rule: funny, never cruel.

## What It Needs

- **A stage:** at least a 2 by 5 area of **Event Stage** tiles, a new
  floor made of tasteful wood, laid as one continuous rectangle. A larger
  rectangle counts (minimum sizes, specified 2026-10-13), in either
  orientation *(proposal)*.
- **Center Stage:** a ritual marker placed on the stage. It can be placed
  only on Event Stage tiles.
- **Seating:** an Event Seating area, a block of at least 5 by 5
  adjacent **Shitty Folding Chairs** (a minimum, specified 2026-10-13).
  The two-thirds threshold counts the chairs actually there.
  - The chairs cost 5 steel each and are very uncomfortable.
  - *(Proposal:)* they must face the stage, as vanilla seats face a
    table or television.
- **House Seats:** a ritual marker placed on the seating area.
- **An emcee:** a male colonist wearing a **Prom Dress**, or a female
  colonist wearing a **Luchador Onesie**. Both are new apparel.
- **Relax Historical Accuracy:** a checkbox *(proposal: on the Center
  Stage marker)*. When it is checked, any pawn can emcee, whatever they
  wear and whatever their gender.

## The Show

1. **Begin Drag Show.** With an eligible emcee available, the Center
   Stage marker offers this trigger.
2. **Waiting.** The emcee proceeds to Center Stage and waits patiently
   for the seats to fill. The colony comes to watch and takes seats,
   using the vanilla gathering behaviour that parties use.
3. **Showtime.** As soon as at least two thirds of the seats are full (17
   of 25 in the minimum block):
   - the stage lights up magenta;
   - exciting disco music plays;
   - the emcee begins to dance.
4. **The drag.** After a few seconds of that *(proposal: about 6 s)*, a
   mild singularity takes the room. Everyone in it is dragged to center
   stage, together with all the furniture and loose objects.
   - Building materials (walls, doors, columns, floors, the stage itself)
     are unaffected.
   - Dragged pawns suffer mild to moderate injuries (specified
     2026-10-13; *proposal:* blunt damage of roughly 3 to 12 points,
     bruises and the odd cracked rib, never anything that downs a
     healthy pawn on its own).
   - They also gain a mild recreation buff *(proposal: a recreation boost
     and a short good-mood memory with a line of its own)*.

## Implementation Notes

*(Proposals:)*

- **Base game first, Ideology on top** (decided 2026-10-13). Vanilla's
  ritual system belongs to Ideology, and the mod depends only on Core.
  - **The show** is built on Core's gathering system, the one parties
    use (`GatheringDef`, a lord job for the audience), with the markers'
    buttons standing in for a ritual's. Everyone gets it.
  - **With Ideology active,** a thin extra layer also offers it as a
    ritual: the begin-ritual window, an emcee role whose requirement is
    the wardrobe rule (dropped by Relax Historical Accuracy), a quality
    score, and an outcome table with room for jokes. The drag, stage and
    seating are shared.
- **Open to every pawn, whatever their ideoligion** (decided 2026-10-13).
  No colony has to adopt anything to hold a show, and any pawn may attend
  or emcee.
  - *(Proposal:)* if Ideology's ritual window needs a ritual precept,
    the mod adds the show to every ideoligion, including newly made
    ones, with no obligations, no mood penalty for skipping it, and no
    conflict with any meme.
  - Otherwise, the ritual layer starts the show without a precept.
  - Which of the two works is checked in the decompile first.
- **The singularity** reuses the shared implosion from the Tractor Bomb
  (`Source/Implosion.cs`). It needs two settings the implosion does not
  have yet:
  - an area limited to the stage's room instead of a radius;
  - a rip rule that uninstalls furniture but never touches structure
    (walls, doors, columns, floors).

  It is mild: a slower drag, and pawns are set down rather than stunned.
- **If the stage is outdoors,** "the room" is the whole outdoors, so the
  drag is limited to a radius *(proposal: 15 tiles)* in that case.
- **Sound:** disco music needs the mod's sound pipeline (backlogged for
  the Colony Bomb). It must be an original composition in the style.
  Until then the show runs with a vanilla sound or none.
- **Dancing:** a lively sway and bob drawn on the emcee, unless a vanilla
  dance animation suits.
- **Art,** per the motif:
  - the Event Stage floor, the folding chair and the two markers;
  - the Prom Dress and the Luchador Onesie, which need worn sprites for
    every body type and direction, the biggest art job here;
  - the magenta stage light, the one place the show glows, because
    batshittium powers it.
- **Tests:**
  - Core: stage and seating shape validation, the two-thirds threshold
    and emcee eligibility.
  - XML: the new defs.
  - Smoke:
    - a show with 17 seated pawns starts and drags the room;
    - walls and floors survive;
    - furniture and items end at center stage;
    - injuries stay mild;
    - the buff is applied;
    - nothing logs an error.

## Open Questions

1. Must the chairs face the stage? (Sizes are minimums: answered.)
2. Where does "Relax Historical Accuracy" live: on the Center Stage
   marker (proposed), or as a mod setting?
3. An outdoor stage: limit the drag to a radius (15 tiles proposed)?
4. Injuries are mild to moderate (answered); is roughly 3 to 12 blunt
   damage right, with a recreation boost and a short good-mood memory?
5. Research and costs: which project unlocks it, and what do the stage
   tiles, markers and the two outfits cost? The chairs are 5 steel.
6. Disco music before the sound pipeline exists: silence, a vanilla
   stand-in, or wait for the pipeline?
