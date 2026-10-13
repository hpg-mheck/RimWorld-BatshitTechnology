# The Showcase and AnyRim

This document is the plan for the in-game showcase and the design of
AnyRim, the small test town the showcase builds in the map's north-west.
Matt "Breakpoint" Heck asked for AnyRim on 2026-10-13: "somewhere to go
test stuff". Where this document and the code disagree, fix whichever is
wrong in the same change.

## Running It

```powershell
.\Tools\smoke-test.ps1 -Showcase
```

The script builds nothing itself. It starts RimWorld on a quick-test map
with the `-btshowcase` flag; `Source/Showcase.cs` does the rest and leaves
the game running. Windows shown to the operator open maximized at 2x UI
scale. The game log carries cues for screen-capture tooling, so it never
has to guess at timings: `[BT-SHOWCASE] READY`, `ANYRIM`, `THROW` and
`TOUR`.

## The Map

The quick test always generates a 250 by 250 map on a random tile, biome
and terrain. Its centre is (125,125). RimWorld's x grows east and z grows
north.

| Area | Where | Contents |
|------|-------|----------|
| Demo base | Centre, within 16 tiles | Every device, working |
| Ballista range | East of the base | Volleys at (185,125) |
| Grenade range | Clearing at (100,125) | Junk pile, two colonists |
| Self-destruct pads | Two corners at centre ±75 | A replicator, a reactor |
| AnyRim | North-west, about (55,163) | The test town |

## Timeline

Ticks run at 60 a second at normal speed; the reactor's barrage slows the
game below real time, which is why capture tools wait for log cues.

| Tick | Event |
|------|-------|
| 0 | Base and AnyRim built; fog cleared; `READY` and `ANYRIM` logged |
| +300 | The replicator demo arms, camera on it (10 s countdown) |
| +1500 | The reactor demo arms, camera on it |
| +2700 | A colonist throws a singularity grenade; `THROW` logged |
| +3300 | Camera tours AnyRim's welcome sign; `TOUR` logged |

Before each self-destruct arms, colonists within its blast radius plus 8
are moved back to the base, so no demo kills a colonist.

## Safety Rules

- **Demo corners keep away from AnyRim.** The replicator's reach,
  counting the fires and secondary blasts it starts, is 83.6 tiles; the
  reactor's barrage reaches 74.6. A corner is a candidate only if it is
  more than 87.6 tiles (the larger reach plus a 4-tile margin) from every
  part of AnyRim, tapering roads included. The site search keeps the
  north-east and south-east corners clear by the same rule, measured the
  same way, so both demos run; the most open of the two gets the
  replicator. If ever only one corner qualifies, the showcase logs it and
  skips the reactor demo, and its opening message says so.
- **AnyRim keeps away from the base and the grenade range:** at least 10
  tiles from the base's cleared square and 15 from the grenade clearing.
- **Fire cannot walk in.** A fire spreads to adjacent cells or throws a
  spark two cells. AnyRim is ringed by a 4-tile firebreak of bare ground,
  and its roads are non-flammable. Its own lawns and wooden houses burn
  normally once something inside the ring sets them alight.

## AnyRim

### The Idea

Cold War legend had the Soviets training spies in replica American towns:
TIME reported "Iowa in the Ukraine" in 1959, and Nelson DeMille's *The
Charm School* made it a novel. The real thing was in Nevada:

- **Operation Doorstep, 1953, "Doom Town".** Two furnished colonial
  houses stood 3,500 and 7,500 feet from the tower.
  - Mannequin families were dressed by JCPenney: Dad reading by the
    window, Mom in the kitchen.
  - Fresh food was flown in for the dinner tables.
  - The near house was destroyed in 2⅓ seconds on camera.
- **Operation Cue, 1955, "Survival Town".** Ten houses were built in
  matched pairs of wood frame, brick and concrete, at a near and a far
  distance. A utility substation, a radio station, cars and a bank vault
  stood among them.
  - The canned food and the beer survived and were taste-tested.
  - The brick house became "little more than a heap of rubble"; a
    basement shelter stood intact under it.

AnyRim is that town, transplanted to the Rim and shrunk to one street: an
earnest little community that knows exactly what it is for. Its jokes are
in what is placed where, in the deadpan voice of
`docs/ARTISTIC-MOTIF.md`, and every gag doubles as a test fixture.

### The Plan

Coordinates below are local: (0,0) is the crossroads, which is also
Ground Zero. The site search prefers map cell (55,163); on open ground
it settles a few tiles away, near (51,166), because candidates lie on a
2-tile grid and the Ballista yard must stay 15 tiles from the grenade
clearing.

```
                        N
            +-----------+-----------+
            |  AnyRim Memorial      |        z +56..+66
            |  Gardens (10 graves)  |
            +-----------+-----------+
                        | Main Street dead-ends at the cemetery
   [W4 Graves & Sons    |   [E4 AnyRim Savings
    Funeral Home]       |    & Loan, vault]           z +35..+43
                        |
   [W3 Knott house]     |   [E3 Damage house]         z +21..+29
   shelter, Daisy's pen |   lawn chairs, Priscilla's pen
                        |
   [W2 Now house]       |   [E2 Town Hall & Fire]     z  +7..+15
 Substation ====== Doorstep Lane ==(X)============= east arm ...
 No. 1                  | Ground Zero                 z 0
   [W1 Five-and-Dime    |   [E1 Public Works] [Ballista yard]
    & Lunch Counter]    |                             z -15..-7
                        |
                 Welcome sign (east verge)
                        :  road tapers to dirt
```

### Streets

- **Main Street:** carriageway x −2..+2, z −21..+47, concrete painted
  dark grey, with a pastel yellow centre line on x 0 (2 cells painted,
  2 not).
- **Doorstep Lane,** the cross street: carriageway z −2..+2,
  x −20..+20, painted the same, centre line on z 0.
- **Sidewalks:** paved tile on x ±3 and z ±3.
- **Ground Zero roundel** at the crossroads: gold tile within radius
  1.5, marble flagstone within 3, slate flagstone within 4.5.

- **The roundel** is laid only on street and sidewalk cells. It doubles
  as a range card: every distance in town is measured from it, and the
  centre lines stop short of it.
- **Doorstep Lane** is named for Operation Doorstep. Its verges stay
  bare, a firebreak between the south pair and the rest of town.
- **Tapers.** The roads run on a little past the town, then taper off:
  - South: broken asphalt z −26..−22 (5 wide narrowing to 3); gravel
    z −31..−27 (3, then 2); packed dirt z −36..−32, one wide, wandering a
    tile left or right, with every third cell left natural; then
    nothing.
  - North: broken asphalt z +48..+50, gravel +51..+53, packed dirt
    +54..+55, ending at the cemetery gate at (0,+56).
  - West arm: broken asphalt x −24..−21, gravel x −26..−25, ending at the
    substation gate at (−27,0).
  - East arm: broken asphalt x +21..+26, gravel x +27..+32, packed dirt
    x +33..+40, wandering, gaps; then nothing.
  - The wander and gaps come from a seeded random stream, so the same
    seed always gives the same town.

### Lots

Every building is 9 tiles along the street. The street-facing wall (the
facade) stands 7 tiles from the street's centre line: x −7 on the west,
x +7 on the east. In front of it lie a 3-deep front yard (lawn, a wooden
picket fence on the sidewalk edge, a gate, a paved walk to the front
door) and the sidewalk. Facing fronts are 13 tiles apart.

| Lot | Outer footprint | Building |
|-----|-----------------|----------|
| W1 | x −15..−7, z −15..−7 | AnyRim Five-and-Dime & Lunch Counter |
| E1 | x +7..+17, z −15..−7 | AnyRim Light, Power & Public Works |
| W2 | x −15..−7, z +7..+15 | The Now residence (wood) |
| E2 | x +7..+17, z +7..+15 | Town Hall & Volunteer Fire Department |
| W3 | x −15..−7, z +21..+29 | The Knott residence (brick) |
| E3 | x +7..+15, z +21..+29 | The Damage residence (steel) |
| W4 | x −17..−7, z +35..+43 | Graves & Sons Funeral Home |
| E4 | x +7..+15, z +35..+43 | AnyRim Savings & Loan |

Side yards between lots are 3 to 5 tiles and hold gravel driveways and
pens. Outbuildings:

- **Substation No. 1:** an unroofed 11 by 11 steel fence ring at
  x −37..−27, z −5..+5, gate at (−27,0), with the reactor at (−32,0).
- **Ballista yard:** a fenced, unroofed 9 by 11 yard behind Public
  Works, x +19..+27, z −16..−6, gate on its west side, ringed with
  sandbags.
- **The Knott shelter:** a 5 by 5 granite-block shelter behind the Knott
  house, x −23..−19, z +23..+27. Its steel door faces west, toward the
  reactor.
- **AnyRim Memorial Gardens:** a wooden-fenced cemetery at x −6..+6,
  z +56..+66, gate at (0,+56).
- **News Nob:** eight stools behind a row of sandbags east of town,
  about (+45..+52, +12), named for the Nevada press point. Colonists can
  watch from there.

### Buildings

The three residences share one floor plan (Plan A), mirrored for the east
side, so that they compare directly as test pieces:

- **Living room** (street side, 3 by 7 inside): a 2 by 2 table with two
  dining chairs, two armchairs facing a tube television (switched off:
  nothing good is on), an end table, a standing lamp, and a potted rose.
- **Bedroom** and **kitchen** (3 by 3 each) behind a partition with two
  doors. The kitchen has an ancient refrigerator, sink and stove, a small
  shelf of ten survival meals (Operation Cue's pantry test) and a six-pack
  of beer.

Each building, with its walls and floor:

- **W1, AnyRim Five-and-Dime & Lunch Counter** (limestone blocks,
  limestone tile):
  - three aisles of shelves: chocolate, survival meals and pemmican;
    beer, kibble and herbal medicine; cloth;
  - six Ergonomic Singularity Grenades on a small shelf by the till,
    where impulse purchases belong;
  - a lunch counter (two 1 by 2 tables, four stools, an electric stove);
  - Bea's bed in the stockroom;
  - a vending machine at the door, a shopping cart in the gravel lot.
- **E1, AnyRim Light, Power & Public Works** (steel, concrete; an
  autodoor):
  - the Transmogrifier, its bill suspended, with granite chunks by its
    hopper;
  - the Artificer, fuelled and switched off (left running, its bolts
    would be hauled 100 tiles to the base's magazines);
  - a 3 by 3 "employee wellness" room with the Brain Squeezer (switched
    off) and a potted plant;
  - the Random Replicator, still in its crate;
  - a shelf of 30 batshittium;
  - Morty's bed, a desk and two lamps.
- **Ballista yard** (steel fence, sandbags): the Batshit Ballista,
  switched off, loaded with 16 painful bolts, and two chained magazines
  (200 painful, 100 really painful).
- **Substation No. 1** (steel fence): the Batshittium Reactor, fuelled,
  auto-refuel off, and a small shelf of 10 batshittium. No batteries.
- **W2, the Now house** (wood, wood plank):
  - Plan A with two single beds, for Wendy and her lodger;
  - dinner for two (fine meals) on the table;
  - one armchair turned to the window, with a novel on the end table
    beside it: the evening paper;
  - a rusted car in the driveway, a postbox and a porch light.
- **E2, Town Hall & Volunteer Fire Department** (wood, wood plank):
  - a council chamber with a 2 by 4 table and eight dining chairs, one
    per resident;
  - the mayor's office: desk, armchair, bed and lamp;
  - a fire bay with three firefoam poppers;
  - Engine No. 1 (a rusted truck) in the side yard, and a hydrant on the
    corner.
- **W3, the Knott house** (sandstone blocks, which read as brick;
  mustard carpet):
  - Plan A with a double bed;
  - the shelter out back: a bed, a shelf of 20 survival meals, beer, a
    lamp, and a thick rock roof;
  - Daisy the Boomalope in a fenced pen with hay;
  - a potato patch.
- **E3, the Damage house** (steel, metal tile):
  - Plan A with a double bed;
  - three armchairs on the front lawn, facing the street, beside an end
    table with a six-pack;
  - a rusted car frame in the driveway;
  - Priscilla the Pig in a fenced pen.
- **W4, Graves & Sons Funeral Home** (marble blocks, burgundy carpet; an
  ornate front door):
  - a showroom of three sarcophagi (marble, steel and wood);
  - a viewing room: two rows of chairs facing an empty sarcophagus,
    between potted daylilies;
  - Barry's bed in the back office.
- **E4, AnyRim Savings & Loan** (granite blocks, marble tile):
  - two marble columns by the door, and a teller counter;
  - a plasteel vault, with a plasteel door, holding 2,000 silver and 50
    gold;
  - an ATM on the front path;
  - nobody lives here.

Around town:

- Lawns of grass, one grown oak per house, potted roses at front doors,
  picket fences and gates, and postboxes.
- Powered standing lamps at the lot lines on alternating sides of Main
  Street, and a dead ancient lamppost at each end of town.
- Hydrants on two corners of the crossroads.
- Cars stand at measured distances from Ground Zero (about 12, 12, 21
  and 33 tiles), as Doorstep's cars did.
- Ten graves in the cemetery: eight reserved, one for each resident, and
  two unreserved; two oak trees.

### Residents

Eight residents, each with a home and a reserved plot. The nickname is
what shows above a pawn on the map.

| Name | Age | Home |
|------|-----|------|
| Hugh "Lame Duck" Moribund | 63 | Town Hall |
| Wendy "Any Minute" Now | 34 | Now house |
| Ernest "Next" Inline | 41 | Now house |
| Shirley "Probably Fine" Knott | 52 | Knott house |
| Constance "Collateral" Damage | 46 | Damage house |
| Barry "Six Feet" Graves | 58 | Funeral home |
| Bea "Right Back" Shortly | 29 | Five-and-Dime |
| Morty "Two Weeks" Tallis | 64 | Public Works |

- **Hugh** is mayor, fire chief and notary, in a bowler hat.
- **Wendy** keeps dinner warm. **Ernest** is her lodger, reading by the
  window in a cowboy hat.
- **Shirley** wears a foil helmet and owns a shelter rated for one.
- **Constance** watches every test from her lawn chair.
- **Barry** has reserved a plot for everyone, himself included; duster
  and bowler.
- **Bea** runs the till beside the grenades.
- **Morty** is two weeks from retirement, in a hard hat.

How they behave:

- **Faction:** RimWorld's hidden, neutral Ancients faction. Harming its
  members costs no goodwill and sends no letters, so the town can be
  blown up freely. Residents never turn hostile.
- **Daily life:** each household gets its own lord,
  `LordJob_AnyRimResident`. It assigns a custom duty,
  `BT_AnyRimResident`, which has no combat in it. Residents:
  - wander within 8 tiles of their front door;
  - never get hungry (civic composure sees to it; the kitchen meals are
    set dressing, as the Doom Town food was);
  - sleep in their own beds;
  - seek shelter from dangerous temperatures within 16 tiles of home,
    and no further.
  
  The lord keeps downed residents, so nobody wanders off the map.
- **Composure:** a hediff, `BT_AnyRimComposure` ("civic composure"),
  blocks mental breaks, holds mood steady and stops hunger, and residents
  never panic and flee. They do still dodge a grenade they can see.
- **Generation:** from a custom kind, `BT_AnyRimResident`:
  - adults with the fixed names, genders and ages above;
  - industrial clothes plus the hats named, and no weapons;
  - no royal titles, and baseliners only.
- **Census:** when a resident dies, a quiet message (not a letter) reads
  "AnyRim census revised to 7. The sign has been updated. Please remain
  calm."

### Signs and Labels

There is no sign building in RimWorld, so signs are a small structure (a
wall between two columns) with text drawn above it by
`MapComponent_AnyRim`. Text shows at middle zoom or closer, in neutral
colours, since magenta is reserved for batshittium.

- **Welcome sign,** at the south entrance on the east verge, (+5,−24):
  WELCOME TO ANYRIM / A Typical Rim Community / Pop. 8, subject to change
  / Est. this morning. Re-est. as needed. At zero the population line
  reads "Pop. 0. Thank you for your participation."
- **Exit sign,** on the east arm, (+31,−4): NOW LEAVING ANYRIM / Come
  back soon. We will try to be here.
- **Cemetery gate:** ANYRIM MEMORIAL GARDENS / Reservations only.
- **Substation gate:** SUBSTATION No. 1 / Capacity 250 kW. Demand: a few.
- **Main Street,** just north of the crossing: EVACUATION ROUTE: NORTH
  (north is the cemetery).
- **Public Works door:** DAYS WITHOUT INCIDENT: n, reset whenever a
  resident dies.
- **Ground Zero,** on the roundel: GROUND ZERO.

### Ownership and Housekeeping

Colonists must not commute 100 tiles to use, haul from or fight fires in
the town, so:

- **Owned by the Ancients:** town structures and furniture. Residents use
  only beds of their own faction, colonists only theirs, and Ancients
  doors open for anyone who isn't hostile.
- **Owned by the player,** so their buttons work: the Batshit devices,
  the Ballista and its magazines. The Ballista and magazines accept no
  hauled bolts, so colony haulers never carry ammunition to town; they
  hold what they were built with.
- **Power:** RimWorld's power nets do not check faction, so the
  player's reactor lights the Ancients' lamps. Conduits run from the
  substation along Doorstep Lane's north sidewalk, up both Main Street
  sidewalks, and into every building and the Ballista yard.
- **Things left off,** so that nothing draws colonists over: every TV,
  the Brain Squeezer, the Artificer and the Ballista are switched off.
  The reactor and Artificer have auto-refuel off, and the
  Transmogrifier's bill is suspended.
- **Forbidden:** every loose item in town. Colonists and haulers ignore
  forbidden things. Unforbid what you want to take (the grenades, for
  instance).
- **Home area:** player buildings extend it as they spawn, so the town
  is removed from it afterwards. Colonists do not fight fires, repair or
  clean in AnyRim.
- **Cold climates:** each house gets a heater on the town grid if the
  outdoor temperature is below 5 °C when the town is built.

### Heat, Fire and Cook-Off Rules

These are checked by unit tests on the plan:

- **Reactor heat:** no roofed cell within 13 tiles of the reactor. It
  heats every enclosed room within 12 tiles to 200 °C. Its yard is
  unroofed, and RimWorld treats a room 25% or more unroofed as outdoors.
- **Cook-off reach:** the Ballista yard is more than 30 tiles from the
  reactor. A magazine's death blast throws bolts up to 25 to 30 tiles, so
  one test cannot chain into a town-wide barrage by accident.
- **The crated replicator:** it stays in its crate because, installed, a
  stray hit could set off its 75-tile blast, which reaches the base. To
  try it, install it at the north end of town.
- **The barrage control:** the Knott shelter's thick rock roof swallows
  overhead bolts, while constructed roofs cave in under them. A lone
  5 by 5 thick roof is far too small to attract an infestation.

### What to Test Where

- **Five-and-Dime:** the singularity grenade (shelves of small stacks
  scatter well), and the grenades themselves.
- **The three houses:** the same plan in wood, brick and steel at
  matched distances, for the grenade, the Ballista and the barrage.
- **Town Hall:** fire, with wooden walls and floors and real firefoam
  poppers.
- **Funeral home:** marble, the weakest stone, and three sarcophagi in
  three materials.
- **Savings & Loan:** a nested plasteel vault. Does the singularity rip a
  plasteel door loose?
- **Knott shelter:** the barrage control, under its thick roof.
- **Substation:** the reactor's self-destruct, 768 bolts over the whole
  town.
- **Public Works:** three death blasts in one shed (rock spew, brain
  squeeze, molten spray), with the Ballista yard next door.
- **Daisy the Boomalope:** chain reactions.
- **News Nob:** watching from a safe distance.

### Site Selection and Ground Work

The quick-test map is random, so the town finds its own ground:

- **Candidates:** crossroads positions on a 2-tile grid near the preferred
  spot (55,163), within x 45..60 and z 150..177 on a 250 map. They must
  keep the whole town inside the map with a 6-tile margin and obey the
  keep-away rules in "Safety Rules".
- **Rejected outright,** measured from the cells the town actually
  occupies (its parts), not from a rectangle around it:
  - anything indestructible (steam geysers, craters) within one cell of
    a building, which would otherwise lose that building;
  - lava within 6 tiles;
  - hostile pawns within 15 tiles, hostile buildings within 35, ancient
    vents within 20.

  An indestructible feature elsewhere (in a lawn, on a road) simply
  stays, and the town builds around it. Profoundly indestructible terrain
  features may even add character (Matt "Breakpoint" Heck, 2026-10-13).
- **Score:** the rest are scored by the work needed to level them:
  - deep water 6, shallow water and marsh 3, soft sand 2;
  - natural rock 4, thick rock roof 2, thin roof 1, other ruins 1;
  - 10 per tile of distance from the preferred spot.
  
  The cheapest wins. If none qualifies, the showcase logs
  `[BT-SHOWCASE] AnyRim skipped` and carries on without it.
- **Levelling the town and its 4-tile firebreak.** Only the cells the
  town occupies are touched: each part grown by the firebreak, and the
  taper cells actually laid. The empty land inside the town's bounding
  rectangle is left alone.
  1. Unfog the area first, so that breaking into a fogged cave sends no
     "area revealed" letter.
  2. Move pawns out.
  3. Remove roofs before rock, so that nothing collapses.
  4. Destroy everything destroyable, with no leavings or blasts.
  5. Clear any pending roof collapses.
  6. Remove player zone cells from the cleared ground.
  7. Turn ground without heavy affordance (water, marsh, mud, soft sand)
     into soil; other natural ground stays, so each run gives a desert,
     tundra or forest AnyRim.
  8. Lay the planned surfaces, then build.

## Implementation

The town is data first, so that its geometry can be unit tested without
RimWorld:

- **`Core/TownGeometry.cs`:** cells, boxes, facings, and RimWorld's
  footprint rule for multi-cell things. The rule is mirrored from
  `GenAdj.OccupiedRect` and checked against known cases.
- **`Core/TownPlan.cs`:** the whole town as declarative lists in local
  coordinates:
  - terrain (def and paint), roofs and placements (def, stuff, cell,
    facing, owner, forbidden, count or growth);
  - buildings, residents, labels and key points;
  - `TownPlan.AnyRim(seed, palette)` builds AnyRim;
  - `Avoiding(obstacles)` drops whatever an obstacle would overlap.
  
  The Colony Bomb will reuse the plan at other sizes and quality tiers.
- **`Core/TownPalette.cs`:** street surfaces, walls and furniture
  quality for the six quality tiers of `docs/COLONY-BOMB.md`. AnyRim is
  Medium.
- **`Core/TownSite.cs`:** the site scorer, over a cost grid the game
  fills in.
- **`Core/AnyRimText.cs`:** the sign, census and message texts.
- **`Source/AnyRim.cs`:**
  - finds a site, levels it, and executes the plan in order (terrain,
    roofs, buildings, furniture, items, plants, animals, residents);
  - wires power, clears the home area, and registers the town, saving
    the obstacles it built around so the exact town (signs included) is
    known after a load;
  - `AnyRim.Demolish` takes it down again, over the same cells. It
    removes only the town's own residents and animals: a resident the
    player recruited or an animal the player tamed stays.
- **`Source/AnyRimResidents.cs`:** the lord job, its toil, and resident
  generation.
- **`Source/MapComponent_AnyRim.cs`:** the saved town record, the census,
  and the sign and label text.
- **`Defs/Misc/BT_AnyRim.xml`:** the duty, pawn kind and composure
  hediff.
- **Debug actions,** under "Mods" in the dev-mode Actions menu: "Build
  AnyRim here" (click a crossroads; it levels whatever stands there),
  "Build AnyRim (best site)" and "Remove AnyRim". The operator can use
  them in his own dev-mode games. The best site keeps the town 10 tiles
  from the colony's buildings, and its reactor more than 78.6 tiles away
  (its barrage's 74.6 plus a margin).

### Tests

- **Unit tests** (Core) of the plan:
  - nothing overlaps that shouldn't; every placement lies inside its
    room;
  - doors and the cells in front of them are clear; every room is
    reachable from the street;
  - the cross street runs north of the first pair; each side has 3 to 5
    lots;
  - the tapers narrow and end; everything lies inside the bounds;
  - the heat and cook-off rules hold;
  - the same seed gives the same town;
  - eight residents with nicknames of 16 characters or fewer;
  - the footprint rule; `Avoiding`; the site scorer; the texts.
- **XML tests:** the new defs follow the house rules, and every def name
  the plan uses exists in RimWorld's Core data (when the game is
  installed) or in the mod.
- **Smoke test** (`TestAnyRim`, run after the non-destructive tests and
  torn down before the destructive ones):
  - the town builds at a scored site;
  - every placement spawned where planned;
  - houses are enclosed, roofed rooms reachable from the street;
  - no roof collapse pending before the first tick, and every roofed
    cell still roofed after it; the firebreak holds no plants;
  - the devices are powered; the reactor's heat ring holds no room;
  - eight named, non-colonist residents, each in a lord;
  - after 600 ticks each is within 20 tiles of its own home;
  - the census drops by one when a resident dies;
  - demolition leaves nothing behind;
  - nothing logs an error.
- **Showcase:** a screen capture at the `TOUR` cue.

## Known Limits

- Residents in period clothes may suffer heatstroke in a desert map; the
  houses have heaters, not coolers.
- Raiders are hostile to the Ancients and will shoot residents. That is
  acceptable on a test range.
- Daisy the Boomalope can be killed by a predator or lightning, and will
  explode, before anyone tests anything.
- The firefoam poppers in the fire hall will douse nearby test fires.
  That is intended, but bear it in mind when testing fire on the Now
  house opposite.
