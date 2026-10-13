# The Batshit Blimp and the Whoops Drive

Status: designed and backlogged, not yet implemented. Specified by Matt
"Breakpoint" Heck on 2026-10-13. Points his specification leaves open are
marked *(proposal)* and listed under "Open Questions" and in
`docs/AI-requests-for-humans.txt`.

## Summary

The Batshit Blimp is an airship that works like a pack animal or a
truck. It carries up to 50 items and two passengers, who can shoot from
it. It shrugs off weather and falls to bullets. It flies at a few
altitudes, bobs on the air currents, casts a shadow that follows the sun,
and gets blown about by strong wind. The Whoops Drive, an optional
upgrade, lets it warp across the map, somewhat frequently to its intended
destination; a miss into solid rock blasts a landing hole first.

## The Blimp

### What It Is

- **A vehicle that works like a pack animal or a truck,** whichever is
  easier to build and more consistent with RimWorld's mechanics.
  *(Proposal:)* on the ground it is a player-owned pawn of its own race,
  with no needs (food, rest or mood), that carries cargo like a pack
  animal and can join caravans. In the air it is a separate in-flight
  thing that holds the blimp, its passengers and its cargo, in the way
  vanilla's skyfallers and shuttles hold their contents.
  - This keeps the flying blimp off RimWorld's path grid, which assumes
    walkers, so it can cross mountains and water freely.
  - It is controlled by its own buttons, not by drafting, because vanilla
    lets the player draft only humanlikes and overseen mechanoids. The
    buttons are: Fly to, Take off, Land, Rise, Descend, and, with the
    upgrade, Warp.
- **Cargo:** up to 50 items. *(Proposal:)* 50 stacks; RimWorld usually
  measures carrying capacity by mass, so the alternative is a mass limit.
- **Passengers:** up to two, boarding and leaving by float-menu orders
  while it is landed or hovering low *(proposal)*. Both can fire their
  ranged weapons from it, from its position and altitude. No pilot is
  needed *(proposal)*: it flies itself.
  - The same "shoot from inside" mechanism is backlogged for the Colony
    Deployment Shelter, so it is built once and shared.
- **Size and art** *(proposal)*: about 3 by 2 tiles landed. Drawn per the
  motif: a riveted steel gondola under a gasbag, with batshittium as the
  lifting agent and the one focal glow, a faint magenta through the
  gasbag's seams.

### Durability

- **Weather:** robust against the elements; rain, snow, toxic fallout and
  temperature do not harm it.
- **Bullets:** vulnerable to small arms fire. While airborne it can be
  targeted and hit like a pawn.
- **Forced landing below 75% health:** it descends and lands, following
  the landing rules below.
- **Zero health:** it explodes violently, with its own death per the
  motif *(proposal: the gasbag bursts in a magenta fireball, scattering
  burning cargo and shrapnel)*. Passengers are thrown clear and hurt.
- **Repair:** most pawns can repair it while it is landed *(proposal: any
  colonist capable of construction, using steel and components)*.

### Altitude

Three or four altitude levels; *(proposal)* four, chosen to fit RimWorld's
2.5D view, in which height is shown by drawing a thing further up the
screen than the cell it stands over:

| Level | Draw offset | Hit chance against it | Wind effect |
|-------|-------------|-----------------------|-------------|
| Landed | 0 | As a pawn | None |
| Low | +0.5 tiles | 100% | Light |
| Cruise | +1.5 tiles | 80% | Moderate |
| High | +3 tiles | 60% | Strong |

- The blimp's true position is the cell under it. Its shadow is drawn
  there, and that is where it is hit and where it lands.
- Passengers' shots at higher altitudes suffer the same accuracy factor
  *(proposal)*.
- Take off, land, rise and descend move one level at a time, taking a
  few seconds each.

### Look and Motion

- **Idle bob and weave:** while airborne it drifts slowly and calmly, as
  if on air currents: a small, slow sway of a fraction of a tile and a
  few degrees of yaw, driven by smooth noise rather than a regular sine,
  so it never looks mechanical.
- **Shadow:** an ellipse on the ground under it, if feasible. RimWorld
  already casts building and plant shadows along a sun vector that
  changes with the time of day (`GenCelestial`):
  - The blimp's shadow is offset along that vector in proportion to its
    altitude.
  - It grows softer and slightly larger with height, and fades as the
    daylight fades.
  - It sits directly under the blimp at noon and stretches out at dawn
    and dusk.

### Wind

- Strong enough wind displaces the blimp, more at higher altitudes.
- Vanilla wind has a speed but no direction, so each map gets a
  prevailing wind direction that wanders slowly *(proposal)*, shown in
  the blimp's inspect pane.
- Displacement begins above a wind speed threshold *(proposal: 1.0, a
  strong breeze)*.
- **Blown near the map edge:** within about 10 tiles *(proposal)*, it
  lands rapidly with a "Forced landing!" event, and the landing damages
  it.

### Landing Rules

- **Over passable ground:** it lands where it is.
- **Over impassable terrain:** a forced or ordered landing over mountain,
  deep water or another impassable cell sends it to the closest
  acceptable landing site. It flies there, losing altitude along the way.
- **No acceptable site within 15 tiles:** it crash-lands and explodes,
  violently as usual.

## The Whoops Drive

- **What it is:** an optional upgrade to the blimp, unlocked by the
  research project Awkward Secrets of the Universe (itself after
  Substandard Advanced Materials).
- **Warp:** an order to jump to a target cell on the map. The blimp
  arrives instantly.
  - It arrives somewhat frequently at its intended destination
    *(proposal: 60% of the time)*.
  - Otherwise it arrives somewhere else, scattered around the target,
    further the longer the jump *(proposal)*.
- **A miss into impassable terrain,** inside a mountain for example:
  - **The blast:** before the blimp appears, an area of radius 3 is
    blasted out of the way with an impressive effect. It does no damage
    to the blimp or its occupants.
  - **Rubble:** whatever was there is replaced with appropriate rubble
    for the original terrain and objects: rock walls become rough floor
    and chunks of the same stone; buildings leave slag and their
    leavings; trees leave wood *(proposal for the specifics)*.
  - **Bystanders:** any pawns or animals in range are set on fire and
    moderately injured.

## Implementation Notes

These are risks to settle first:

- **Control and pathing:** drafting non-humanlikes and flying over
  impassable cells both fight vanilla's assumptions. That is why the
  proposal splits a landed pawn from an in-flight thing driven by
  buttons. Harmony is acceptable if necessary but not preferable (Matt
  "Breakpoint" Heck, 2026-10-13): prefer subclasses, comps and the mod's
  own checks, and patch only what cannot be reached any other way.
- **Being targeted in the air:** the in-flight thing must register as an
  attack target so that raiders shoot at it.
- **Shooting from inside:** a proxy verb fires each passenger's own
  weapon from the blimp, with the passenger's skill and the altitude
  factor. It is shared with the Colony Deployment Shelter's backlog item.
- **Caravans:** the landed blimp joins caravans as a pack animal. Flying
  between world tiles is out of scope unless asked for.
- **Tests:**
  - Core unit tests: the altitude table, the landing-site search radius
    rule, the warp scatter and the shadow offset.
  - Smoke checks: take off, fly, land over a mountain (redirected), crash
    with no site in 15 tiles, forced landing below 75%, explosion at 0,
    a warp into rock (blast, rubble, bystanders burning, blimp unhurt),
    and the wind's edge landing.

## Open Questions

1. Cargo: 50 stacks, or a mass limit as RimWorld usually uses?
2. Passengers: board while landed only, or also while hovering low? Is a
   pilot needed?
3. Altitude: four levels with the table above?
4. Repair: who counts as "most pawns", and what does repair cost?
5. Wind: is a wandering prevailing direction acceptable, given vanilla
   has none?
6. Whoops Drive: about 60% on target?
7. Does the blimp ever fly between world-map tiles, or only within a map
   (joining caravans as a pack animal when landed)?
8. How is the blimp built, and which research unlocks it?
