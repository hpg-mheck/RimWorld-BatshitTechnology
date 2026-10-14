# The Interstellar Delivery Locker and Drone

Status: specified and backlogged, not yet built. Specified by Matt
"Breakpoint" Heck on 2026-10-13. A semi-vanilla object: mostly one of
the motif's rare mundane exceptions, with one small Batshit habit.
Points his specification leaves open are marked *(proposal)* and listed
under "Open Questions" and in `docs/AI-requests-for-humans.txt`.

## The Locker

- **Visits:** pawns occasionally walk up to it and punch in a beeping
  code. There is a click and a swinging hinge, then a SLAM.
- **What they take:** each visit, they walk away with one of several
  mundane objects:

  | Item | What it is |
  |------|------------|
  | Water Bottle | A beverage. Drinking it leaves an Empty Water Bottle. |
  | Lifestyle Magazine | No perks or abilities. It just piles up. |
  | Witty T-Shirt | Apparel. |
  | Ill-Fitting Hat | Apparel. |
  | Incorrectly Sized Battery | An object. |

- **Comedy:** the comedy showcase needs one Water Bottle per comic
  (`docs/STAND-UP-COMEDY.md`).

## The Drone

- **Deliveries:** the Interstellar Delivery Drone, a janky-looking
  quadrotor, delivers the items by landing on top of the locker.
- **Destruction:** if it is destroyed, it goes off with an impressive
  blast radius, and another one shows up tomorrow.

## Proposals

- **Visits:** a pawn checks the locker when a delivery is waiting.
  - It is a short job, taken from idle time, at most once a day per
    pawn.
  - Items it brings back go to storage, like any other haul.
- **Deliveries:** the drone comes once a day with a few items, picked by
  weights favouring water bottles and magazines. It is free, a
  subscription nobody remembers signing up for.
- **Capacity:** about ten items. When the locker is full, the drone
  hovers, sighs and leaves.
- **The items:**
  - The Water Bottle is a small recreation drink.
  - The Lifestyle Magazine is an item worth almost nothing, which no
    one reads.
  - The Witty T-Shirt is an ordinary shirt whose label is a different
    weak pun each time.
  - The Ill-Fitting Hat is a hat with a small comfort penalty.
  - The Incorrectly Sized Battery is a resource that fits nothing and
    sells for one silver.
- **The blast:** the drone runs on a tiny batshittium cell, its one
  magenta accent, which explains everything. The blast is about 6
  tiles of bomb damage. A new drone arrives one game day after the
  last one was destroyed.
- **Research:** none. Like the Junk Drawer, it has no technology
  requirement.
- **Sound:** the beeping code, click, hinge and slam use vanilla
  stand-ins until the sound pipeline makes originals.
- **Art:** vanilla style, per the motif's mundane-exception rule. The
  locker is a grey parcel locker with a keypad. The drone is a
  four-rotor delivery drone with one rotor guard held on by gaffer tape.

## Open Questions

1. How often do pawns visit, and does a delivery need to be waiting?
2. How often does the drone deliver, how much, and is it free?
3. What does each item do (the proposals above)?
4. The drone's blast: about 6 tiles (proposed)? And from a batshittium
   cell?
5. Research: none (proposed)?
