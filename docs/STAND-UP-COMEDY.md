# The Stand-Up Comedy Showcase

Status: specified and backlogged, not yet built. Specified by Matt
"Breakpoint" Heck on 2026-10-13 as another performance type, alongside
the Drag Show (`docs/DRAG-SHOW.md`). Points his specification leaves
open are marked *(proposal)* and listed under "Open Questions" and in
`docs/AI-requests-for-humans.txt`.

## What It Needs

- **Four to eight comics:** any pawns with a Social skill below 5. The
  comedy is, by construction, amateur.
- **A stool,** the vanilla one.
- **A Microphone Stand:** a new object. *(Proposal:)* a vanilla-style
  object (one of the motif's rare mundane exceptions), a chrome stand
  with a slightly bent boom.
- **One Water Bottle per comic,** from the Interstellar Delivery Locker
  (`docs/DELIVERY-LOCKER.md`). Comics sip between jokes, and each leaves
  an Empty Water Bottle behind.
- **A bouncer** (specified 2026-10-13), posted outside the theatre, next
  to its door, at a special piece of furniture called the **Velvet
  Rope**. See "The Bouncer" below.

## The Show *(proposals)*

The specification names the requirements; the running order is proposed.

- **Where:** on the Drag Show's Event Stage, with the microphone stand
  and stool on it in place of the Center Stage marker, before the same
  Event Seating. Both shows share one performance framework: the base
  game's gathering system, plus Ideology's ritual layer when active.
- **The running order:**
  1. The comics take turns at the microphone, perched on the stool.
     Each does a short set, about half a minute.
  2. Their jokes appear as speech text above them, one line at a time,
     drawn from a list of deliberately terrible one-liners. They are
     written in the motif's voice: funny for the player, never cruel to
     anyone on the map.
  3. The audience reacts in text: a polite laugh, a cough, one person
     clapping.
- **The outcome:** the audience gets a recreation boost, and a mixture
  of memories, "very funny and also true to life" (decided 2026-10-13).
  Each spectator remembers the same show differently. *(Proposal:)*
  each draws one memory from a range, weighted by the show's quality:
  - loved it;
  - hated it;
  - can't stop thinking about one joke;
  - laughed at the wrong moment;
  - still doesn't get the joke about the hat.
  
  Each comic gets a good memory for having done it, unless the set ended
  in a riot.
- **Research:** Batshit Entertainment, the same project as the Drag
  Show (approved 2026-10-13).

## The Bouncer

Specified 2026-10-13:

- **The post:** the comedy show needs a bouncer, posted outside the
  theatre next to the door, at the Velvet Rope.
- **Useless on purpose:** the bouncer is the one pawn who will not try to
  intervene in a fight. They stay at the rope, completely useless, until
  the show is over.
- **Everyone else outside:** other pawns outside the theatre may decide
  to run in when a fight breaks out, or not.

*(Proposals:)*

- **The Velvet Rope** is a 1 by 2 piece of furniture: two brass
  stanchions and a red velvet rope. It is one of the motif's rare
  mundane exceptions, drawn plainly, and costs 10 steel and 10 cloth
  under Batshit Entertainment.
  - It must stand outdoors, or in another room, within two tiles of a
    door into the theatre: the room holding the stage and seating.
  - Its inspect pane names the bouncer on duty.
- **The bouncer** can be any able pawn, chosen when the show starts. The
  comics, the emcee role and the audience are chosen separately. The
  show waits until the bouncer is at the rope.
- **During a fight:** the bouncer keeps standing at the rope, arms
  folded. Their status line reads "keeping the line orderly", and on a
  riot, "not getting involved".
- **Pawns outside who hear the fight:**
  - Each decides once whether to run in. Bloodlust or Brawler make it
    likely; Wimp or Kind make it unlikely; otherwise it is about even.
  - Those who run in join the brawl, or the gunfight.
  - The Spritzers foam them along with everyone else.

## Quality, and the Mob

Specified by Matt "Breakpoint" Heck on 2026-10-13:

- **Show quality:** when each comic finishes, their performance quality
  contributes to the quality of the whole show.
- **The mob:** if any comic is bad enough, they are immediately treated
  as hostile, and the entire audience attacks.

*(Proposals:)*

- **A comic's quality** is rolled when the set ends, from their Social
  skill (necessarily below 5), their mood, and a large helping of luck.
  The show's quality is the average of the sets, and it scales the
  audience's recreation and memory. With Ideology active, it feeds the
  ritual's quality.
- **"Bad enough"** means a set in roughly the bottom tenth, rolled per
  set (approved 2026-10-13). A full bill of eight comics should end in a
  riot now and then, not every time.
- **The attack:**
  - Every member of the audience who can fight goes for the comic at
    once, in vanilla's social-fight state (approved 2026-10-13): fists
    and whatever is in hand.
  - It stops when the comic is down, or flees past the seating.
  - The comic is "treated as hostile" for the length of the brawl, not
    turned into an enemy of the colony, so nobody's faction changes and
    the colony is not at war with itself afterwards.
- **The show ends on a riot:** no further comics, a sour memory for
  everyone ("Watched a comedy show turn into a riot"), and one last
  line of narration in the house voice: "The management thanks you for
  your patience."

### The Gunfight

A riot has a 1% chance of being a true gunfight instead (specified
2026-10-13).

- Each rioter goes into vanilla's murderous rage against the comic and
  uses whatever weapon they carry, guns included (approved
  2026-10-13).
- It is lethal, unlike the social fight, and ends like any murderous
  rage, or when the Spritzers go off.

## Riot Suppression Spritzers

Specified 2026-10-13: to deal with a potentially hostile audience, Riot
Suppression Spritzers may be installed in the corners of the room, or
really anywhere.

- **Trigger:** if any are available, they engage a few seconds after a
  fight starts.
- **Like a firefoam popper:** they are similar to vanilla's Firefoam
  Poppers, but dispense **Pacification Foam**.
- **The foam** ends hostility and knocks people out.

*(Proposals:)*

- **When it fires:** a Spritzer goes off a few seconds (about 3) after
  any fight starts within its radius (decided 2026-10-13). That covers
  comedy riots and gunfights, and also ordinary social fights, berserk
  pawns and murderous rages.
- **What it does:** a foam burst with a 5-tile radius (decided
  2026-10-13; a popper's 7 was too large).
  - It ends every hostile and fighting mental state it touches.
  - It puts everyone it reaches to sleep for an hour or two (approved
    2026-10-13), as anaesthetic does, guilty and innocent alike, the
    comic included.
  - It leaves a slick of pale foam on the floor.
- **Single use until reset** (decided 2026-10-13). Once it has fired, a
  pawn resets it with batshittium, a Water Bottle and an Incorrectly
  Sized Battery. *(Proposal:)* 1 batshittium, a reset job like
  refuelling, and a "Needs reset" overlay meanwhile. The battery finally
  has a use, and still does not fit; that is part of the reset.
- **Batshittium** makes the foam work, so it carries a faint magenta
  tint, the motif's one accent. Destroyed, a Spritzer spritzes
  everything near it, a death of its own as the motif requires.
- **Research:** Batshit Entertainment, with the shows it polices.

## Open Questions

1. Does it share the Drag Show's stage and seating (proposed), or run
   anywhere with a stool, a microphone stand and some seats? The
   proposal stands unless changed.
2. The Velvet Rope and bouncer details above (proposed): its size,
   placement rule and cost; who can be a bouncer; and how likely
   pawns outside are to run in?
