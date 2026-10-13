# Batshit Technology (RimWorld 1.6) — v0.8

A collection of ridiculous, misguided, and insanely overpowered RimWorld
gadgets and mods, done as a very entertaining exercise to see how well
Claude could be taught to do rapid-prototyping on Windows for a complex
plugin architecture. Share and enjoy.

Everything lives in its own research tab, **Batshit Technology**:

- **Batshittium**: an element made from ordinary rock chunks, ten to one, in
  the **Batshittium Transmogrifier**.
- **Batshittium Reactor**: 250,000 W from one unit of batshittium for a
  random 0.1 to 36 hours. It sheds enough waste heat to cook any room within
  12 tiles.
- **Random Replicator**: fabricates a random food item every six hours into
  a refrigerated compartment that haulers empty into your stockpiles. It
  also has a pawn-armed self-destruct with a ~75-tile blast.
- **Brain Squeezer**: a chair colonists sit in for moderate recreation. It
  leaves them calmer (a mood boost and a day of lower mental break risk)
  and slightly rearranged.
- **Batshittium Ballista**: a turret throwing volleys of up to 256
  insanely inaccurate, terrifyingly destructive bolts up to 325 tiles,
  every five seconds, fed by chained magazines and an automated
  Artificer that makes the bolts.

## Build

1. Install Visual Studio 2022 (".NET desktop development" workload) or the
   .NET SDK 8 or later.
2. Set the `RIMWORLD_DIR` environment variable to your RimWorld install
   folder, for example
   `C:\Program Files (x86)\Steam\steamapps\common\RimWorld`. The build stops
   with a clear error if it is missing.
3. Build `BatshitTechnology.sln` (Release). The output in `Assemblies/` is
   `BatshitTechnology.dll` plus `BatshitTechnology.Core.dll`, the game-free
   helper library; RimWorld needs both.

From a terminal:

    MSBuild.exe Source\BatshitTechnology.csproj -restore -p:Configuration=Release

## Tests

`Tests/BatshitTechnology.Tests` holds xUnit tests for the Core library and
XML regression tests for the Defs (including the corrected v0.2 values, so
those bugs cannot quietly return). Run them with:

    dotnet test Tests/BatshitTechnology.Tests/BatshitTechnology.Tests.csproj

## Install

Put this folder in RimWorld's `Mods` folder (a directory junction works for
development), then enable **Batshit Technology** in the mod list. It needs
no other mods.

To see everything at once without touching your own saves or mod list, run
`.\Tools\smoke-test.ps1 -Showcase` (PowerShell, after building). It starts
RimWorld on a quick-test map with an isolated data folder and builds a
working demo base: reactor, stocked replicator, Transmogrifier with a bill,
a colonist in the Brain Squeezer, the Artificer, and a Ballista firing
explosive volleys from chained magazines.

## Research

The **Batshit Technology** tab:

| Project              | Cost | Requires             | Unlocks              |
|----------------------|------|----------------------|----------------------|
| Batshittium Lore     | 500  | nothing              | the early devices    |
| Batshittium Arcana   | 750  | Batshittium Lore     | the projects below   |
| Painful Bolts        | 1000 | Arcana               | kinetic bolts        |
| Really Painful Bolts | 1000 | Painful Bolts        | explosive bolts      |
| Batshittium Artificer| 1000 | Arcana               | the Artificer        |
| Ballista Magazine    | 1000 | Arcana               | the magazine         |
| Batshittium Ballista | 1000 | Arcana, Painful Bolts| the Ballista         |

Batshittium Lore unlocks the Transmogrifier, Reactor, Random Replicator
and Brain Squeezer.

Both are tech level Neolithic, the lowest a player faction can be, so every
colony pays the listed cost.

## Batshittium and the Transmogrifier

- **Batshittium**: a raw resource, 40 silver, stacks to 75.
- **Batshittium Transmogrifier** (Production, 2×2, 300 W): a work table with
  one bill, **transmogrify rock chunks**, which turns ten chunks of any stone
  into one batshittium. Crafters work it. It needs power.

## Batshittium Reactor

- Power tab, 2×2, not rotatable. It produces **250,000 W** while a unit of
  batshittium burns. Each unit lasts a random **0.1 to 36 in-game hours**,
  and nobody can predict which. (Dev mode shows the time left.)
- It holds two units, one burning and one waiting, and haulers keep it
  loaded, so it runs continuously while you have batshittium. Switching it
  off pauses the burn.
- **Waste heat**: 1,000 heat per second, spread over every room within
  **12 tiles** and weighted toward the reactor; outdoor cells' share
  dissipates. Nearby rooms heat up whether the reactor is inside or out, up
  to a cap of 200 °C. That is lethal, but below the 240 °C at which things
  start igniting on their own. The 12-tile ring shows while placing the
  reactor and when it is selected. Build it well away from anywhere people
  live.

## Brain Squeezer

- Joy tab, 1×1 chair, 150 W, costs one batshittium among other things.
  Colonists use it on their own for recreation (its own joy kind, "brain
  squeezing", at television-like rates), one at a time, for about an
  in-game hour.
- While squeezed, they mutter one of 28 inner-monologue lines every few
  seconds ("That... that feels pretty weird...", "Is that supposed to feel
  like... that...", "My politics just left the building.").
- Afterwards: a +6 mood memory for a day, labelled in the mood panel with
  the last thing they said, and a day of calm (mental break threshold -5
  percent), so fewer breakdowns, riots and arguments. Another session
  restarts both.
- The lines are in `Defs/Misc/BT_BrainSqueezer.xml`, under the
  `BT_BrainSqueezed` thought's `MonologueExtension`; add or edit freely.

## Batshittium Ballista

- **Batshittium Ballista** (Security, 2×2): fires one volley every five
  seconds at enemies in range, or at a target you pick. Its gizmos set the
  **range** (10 to 325 tiles, the largest normal map size), the
  **projectile count** (1 to 256) and the **ammunition**. Each bolt lands
  at a random spot around the target; the scatter grows with distance
  (2 tiles plus a quarter-tile per tile of range). Bolts arc overhead, so
  build it under open sky.
- **Power**: range and count multiply ("units" = count × range ÷ 10), and
  the draw rises in a straight line from 50 W standby and 1,000 W per
  volley (1 bolt at 10 tiles) to 62.5 kW standby and 990 kW per volley
  (256 bolts at 325 tiles). That is a quarter of one Batshittium Reactor at
  standby, and four reactors (with room for the magazines) at full fire.
  The firing draw lasts one second per volley; if the grid's surplus plus
  its batteries can't cover it, the Ballista holds fire and says so.
- **Bolts**: **Painful** (kinetic, 45 damage, high armor penetration) and
  **Really Painful** (explosive, radius 3.9). The Ballista holds 16 itself.
- **Ballista Magazine** (Security, 1×1): holds 2,560 bolts and feeds any
  Ballista it shares an edge with, or reaches through a chain of other
  magazines; one magazine can serve several Ballistas. Volleys drain the
  magazines first (nearest link first) and keep the internal 16 as a
  reserve. 5 W idle, 500 W while feeding a volley; unpowered magazines
  don't feed. Selecting a Ballista outlines the magazines it can use.
- **Batshittium Artificer** (Production, 2×2, 400 W): automated. Haulers
  load it with batshittium (it holds 10) and it works one unit an hour
  into 10 Painful or 5 Really Painful bolts, dropped at its interaction
  spot for haulers to carry to magazines. Its gizmo picks the bolt type
  among those you have researched.
- Haulers keep the Ballista (priority Critical) and magazines (Important)
  stocked; each has a storage tab to filter bolt types.

## Random Replicator

- Architect → Production → **Random Replicator**.
- Every six hours it fabricates one random meal or raw food into its
  compartment. Meals are five times likelier than any single raw food.
  Production pauses while the compartment is full (ten items) or the device
  has no power.
- **Hauling**: the compartment is a storage with **Low** priority by
  default, the same mechanism as vanilla bookcases. Haulers move its food to
  any better storage, such as your freezer, and colonists eat it from there.
  Raise the priority in its **Storage** tab to keep food in the device as a
  refrigerated buffer. Food unticked in the tab's filter gets hauled out.
  Right-click it with a colonist selected to order a haul manually.
- **Refrigeration** toggle (on by default; +50 W, a quarter of a vanilla
  cooler): while on and powered, stored food is held in stasis and doesn't
  age or rot. When it is off, or without power, stored food spoils at the
  temperature of the room around the device.
- **Auto-eject** toggle (off by default): new food drops at the interaction
  spot instead of being stored. Use it when no stockpile takes the food.
  Haulers only move stored food to *better* storage, and nobody eats
  straight from the device.
- **Eject all** drops everything at the interaction spot in front of the
  device. **Eject item...** lets you pick a single stack.
- **Power-cut letter**: if the device loses power while refrigerating food,
  you get a letter once the outage has lasted a few seconds. Switching it
  off yourself does not trigger one.
- Deconstructing, uninstalling or destroying the device spills its stored
  food. A self-destruct destroys it.

### Self-destruct

- Select the replicator to see its blast radius (red ring) and flee radius
  (yellow ring).
- **Arm self-destruct** orders a colonist with **Basic** work enabled to
  walk to the interaction spot and arm it (about two seconds).
- Once armed, a map-wide "Self-destruct armed" letter appears. The
  ten-second countdown shows in the inspect pane, with a hissing alarm,
  camera shake and a flashing blast ring visible even when the replicator is
  not selected. Pawns within 25 tiles who are not drafted run away.
- At zero: a Bomb explosion of radius 75, plus an 80 percent chance to start
  a fire in every cell of that radius. The device and its stored food are
  destroyed.
- **Cancel self-destruct order** withdraws the order before anyone arms it.
  **Abort self-destruct** stops a running countdown.
- It never triggers on its own; it only arms after an explicit player
  order.

## Tuning

All values are XML. The replicator's are in
`Defs/ThingDefs/Buildings_RandomReplicator.xml`, under
`CompProperties_RandomReplicator`:

| Field                         | Default   | Meaning                       |
|-------------------------------|-----------|-------------------------------|
| ticksPerSpawn                 | 15000     | 6 h cadence (60,000 per day)  |
| storageCapacity               | 10        | items held; full pauses it    |
| allowedThingCategoryDefs      | meals+raw | plus allowed/disallowed defs  |
| mealWeight                    | 5         | meal odds vs. raw food        |
| requirePower                  | true      | unpowered: no output or chill |
| refrigerationPowerConsumption | 50        | extra W while refrigerating   |
| sendPowerCutLetters           | true      | letter if chilled food at risk|
| selfDestructArmingTicks       | 120       | pawn work to arm (2 s)        |
| selfDestructSeconds           | 10        | countdown length              |
| selfDestructRadius            | 75        | blast radius (max ~79.8)      |
| selfDestructDamage            | -1        | damage; -1 = Bomb default     |
| selfDestructFireChance        | 0.8       | per-cell ignite chance        |
| selfDestructFireSize          | 0.3       | size of each started fire     |
| selfDestructFleeRadius        | 25        | pawns this close flee         |

The storage tab's default priority and filter are under
`<building><defaultStorageSettings>` in the same file.

The reactor's are in `Defs/ThingDefs/Buildings_Batshittium.xml`, under
`CompProperties_BatshittiumReactor`. Its output is `basePowerConsumption`
(-250000; negative means output), and its fuel buffer is `fuelCapacity` (2):

| Field              | Default | Meaning                                   |
|--------------------|---------|-------------------------------------------|
| burnHoursRange     | 0.1~36  | in-game hours one unit burns              |
| heatPerSecond      | 1000    | total waste heat (a vanilla heater: 21)   |
| heatRadius         | 12      | rooms within this many tiles get a share  |
| heatMaxTemperature | 200     | °C cap; things auto-ignite above 240      |

The Transmogrifier's ratio and work are in `Defs/RecipeDefs/BT_Recipes.xml`.

## Textures

All of the mod's art is generated by `Tools/make_textures.py` (needs
Pillow: `pip install pillow`) and follows one visual language, described in
`docs/ARTISTIC-MOTIF.md`. These are placeholders: replace any PNG under
`Textures/` with hand-drawn art at the same path and size, keeping to the
motif.

## History

- **v0.1**: food replicator.
- **v0.2**: self-destruct added (RimWorld 1.5).
- **v0.3**: v0.2 restored and ported to 1.6. Fixes: the work giver pointed
  at a work type that does not exist (`Flick`), so nobody could arm the
  device; invalid DesignationDef and JobDef fields; a missing interaction
  cell; a non-looping alarm sound; pawns sent into `PanicFlee`, which makes
  them leave the map; gizmos with no icons. Self-destruct numbers moved into
  XML.
- **v0.4**: refrigerated internal storage (ten items) emptied by haulers,
  refrigeration and auto-eject toggles, eject gizmos, blast and flee ring
  overlays, power-cut and "Self-destruct armed" letters. Arming time,
  explosion damage and fire size moved into XML. (The original v0.4 code was
  lost; this is a rebuild from its feature list.)
- **v0.5**: Batshit Technology research tab (Batshittium Lore, 500;
  Batshittium Arcana, 750), which now unlocks the replicator; Batshittium
  resource; Batshittium Transmogrifier (ten rock chunks to one
  batshittium); Batshittium Reactor (250,000 W, random 0.1 to 36 hours per
  unit, area waste heat); custom textures for the new items. Merged with the
  Codex repository work (AGENTS.md, docs, Core library and tests), which now
  builds and runs; in-game smoke test.
- **v0.6**: Brain Squeezer (recreation chair, monologue, mood memory and a
  day of calm).
- **v0.7**: Batshittium Ballista system: the Ballista (volleys, selectable
  range and count, range-and-count power with a firing burst), Painful and
  Really Painful bolts, the Ballista Magazine (edge-chained feeding), the
  automated Batshittium Artificer, and five research projects under
  Batshittium Arcana.
- **v0.8**: renamed from Random Replicator to Batshit Technology and moved
  to its own repository, history included. New package ID
  `breakpoint8088.BatshitTechnology`; def names now start with `BT_`
  (the replicator itself is `BT_RandomReplicator`), and the code namespace
  and assemblies are `BatshitTechnology`. Saves from v0.7 and earlier do not
  carry over.

MIT license for code (see `LICENSE`). RimWorld © Ludeon Studios. Uses vanilla
textures and sounds by reference (not redistributed).
