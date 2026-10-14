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
  - They should face the stage, but it is not enforced (decided
    2026-10-13): most players will place them that way anyway. A chair
    facing elsewhere still counts toward the two-thirds threshold, and
    the House Seats marker's inspect pane notes how many face away.
- **House Seats:** a ritual marker placed on the seating area.
- **An emcee:** a male colonist wearing a **Prom Dress**, or a female
  colonist wearing a **Luchador Onesie**. Both are new apparel.
- **Relax Historical Accuracy:** a checkbox on the Center Stage marker
  (decided 2026-10-13), so each stage keeps its own setting. When it is checked, any pawn can emcee, whatever they
  wear and whatever their gender.

## The Outfits and Batshittium Monofilament

Specified 2026-10-13:

- **Any fabric.** The Prom Dress and the Luchador Onesie can be made from
  any fabric.
- **Batshittium Monofilament improves results.** It is a new fabric made
  at a new building, the **Batshittium Extruder**, unlocked by the first
  Batshit Technology research project, Batshittium Lore.

*(Proposals:)*

- **What "improves results" means.** As stuff, monofilament is a superior
  fabric: high beauty, good insulation and armour, light, tough. That
  works for any fabric apparel, not just these two.
  - On the two outfits it also gives the show a bonus: an emcee in
    monofilament makes a better show. That means more recreation for the
    audience, and a higher quality score when Ideology's ritual layer is
    active.
- **The Extruder.**
  - It turns batshittium into monofilament, about 1 batshittium to 25
    monofilament, with power and work. It is staffed like a normal bench
    (crafting), unlike the Artificer.
  - Its art follows the motif: a steel chassis drawing a glowing magenta
    thread from a batshittium porthole onto a spool.
- **The look.** Things made from monofilament carry a faint magenta
  glint, since magenta means batshittium. One thin accent, never a full
  glow.

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
   - **The kidney** (specified 2026-10-13): every participant, the emcee
     included, has a 1% chance of losing a kidney, unless they are
     already short one. The kidney just shows up on the floor.
     *(Proposals:)*
     - Only pawns with both natural kidneys can lose one.
     - The kidney is a vanilla Kidney item placed near its owner, never
       shoved into an occupied cell, and can be transplanted back.
     - A quiet message reads, in the house voice, "<name> appears to
       have mislaid a kidney. It is on the floor."
     - Animals dragged along are not participants.

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
  drag is capped at 15 tiles (decided 2026-10-13).
- **Trees absolutely go flying** (specified 2026-10-13), indoors or out:
  - *(Proposal:)* every tree in reach is uprooted and tumbles through the
    air to center stage, drawn with its own sprite and spinning as it
    goes.
  - It lands with a thud as its wood (the tree's harvest yield), giving
    a mild knock to anything it lands on.
  - Grass and small plants are simply flattened, as the singularity
    grenade does.
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

1. Injuries are mild to moderate (answered); is roughly 3 to 12 blunt
   damage right, with a recreation boost and a short good-mood memory?
2. Research and costs: which project unlocks the show, and what do the
   stage tiles, markers and the two outfits cost? The chairs are 5 steel.
3. Batshittium Monofilament: is "improves results" a better fabric (as
   proposed) plus a show bonus? What are the Extruder's inputs and
   yield (1 batshittium to 25 monofilament proposed)? Should
   monofilament be usable for every fabric item, or only these outfits?
4. Disco music before the sound pipeline exists: silence, a vanilla
   stand-in, or wait for the pipeline?
