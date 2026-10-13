# Random Replicator (RimWorld 1.6) — v0.5

Batshit technology for RimWorld, in its own research tab:

- **Batshittium**: an element made from ordinary rock chunks, 10 to 1, in the **Batshittium Transmogrifier**.
- **Batshittium Reactor**: 250,000 W from one unit of batshittium, for a random 0.1–36 hours. It sheds enough waste heat to cook any room within 12 tiles.
- **Random Replicator**: fabricates a random food item every 6 hours into a refrigerated compartment that haulers empty into your stockpiles. It also has a pawn-armed self-destruct with a ~75-tile blast.

## Build
1. Install Visual Studio 2022 (".NET desktop development" workload).
2. RimWorld is expected at `C:\Program Files (x86)\Steam\steamapps\common\RimWorld`. For any other location, set the `RIMWORLD_DIR` environment variable to your install folder.
3. Build `RandomReplicator.sln` (Release). The output is `Assemblies/RandomReplicator.dll`.

   From a terminal:
   `"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" Source\RandomReplicator.csproj -p:Configuration=Release`

## Install
Put this folder in RimWorld's `Mods` folder (a directory junction works for development), then enable **Random Replicator** in the mod list. It needs no other mods.

## Research
The **Batshit Technology** tab holds two projects:

| Project | Cost | Requires | Unlocks |
|---|---|---|---|
| **Batshittium Lore** | 500 | nothing | every device below, for now |
| **Batshittium Arcana** | 750 | Batshittium Lore | nothing yet |

Both are tech level Neolithic, the lowest a player faction can be, so every colony pays the listed cost.

## Batshittium and the Transmogrifier
- **Batshittium**: a raw resource, 40 silver, stacks to 75.
- **Batshittium Transmogrifier** (Production, 2×2, 300W): a work table with one bill, **transmogrify rock chunks**, which turns 10 chunks of any stone into 1 batshittium. Crafters work it. It needs power.

## Batshittium Reactor
- Power tab, 2×2, not rotatable. It produces **250,000 W** while a unit of batshittium burns. Each unit lasts a random **0.1 to 36 in-game hours**, and nobody can predict which. (Dev mode shows the time left in the inspect pane.)
- It holds two units, one burning and one waiting, and haulers keep it loaded, so it runs continuously while you have batshittium. Switching it off pauses the burn.
- **Waste heat**: 1000 heat/s, spread over every room within **12 tiles** and weighted toward the reactor; outdoor cells' share dissipates. Nearby rooms heat up whether the reactor is inside or out, up to a cap of 200°C. That's lethal, but below the 240°C at which things start igniting on their own. The 12-tile ring shows while placing the reactor and when it's selected. Build it well away from anywhere people live.

## Random Replicator
- Architect → Production → **Random Replicator**.
- Every 6h it fabricates one random meal or raw food into its compartment. Meals are 5× likelier than any single raw food. Production pauses while the compartment is full (10 items) or the device has no power.
- **Hauling**: the compartment is a storage with **Low** priority by default, the same mechanism as vanilla bookcases. Haulers move its food to any better storage, such as your freezer, and colonists eat it from there. Raise the priority in its **Storage** tab to keep food in the device as a refrigerated buffer. Food unticked in the tab's filter gets hauled out. Right-click it with a colonist selected to order a haul manually.
- **Refrigeration** toggle (on by default, +50W, a quarter of a vanilla cooler): while on and powered, stored food is held in stasis and doesn't age or rot. When it's off, or without power, stored food spoils at the temperature of the room around the device.
- **Auto-eject** toggle (off by default): new food drops at the interaction spot instead of being stored. Use it when no stockpile takes the food. Haulers only move stored food to *better* storage, and nobody eats straight from the device.
- **Eject all** drops everything at the interaction spot in front of the device. **Eject item...** lets you pick a single stack.
- **Power-cut letter**: if the device loses power while refrigerating food, you get a letter once the outage has lasted a few seconds. Switching it off yourself doesn't trigger one.
- Deconstructing, uninstalling or destroying the device spills its stored food. A self-destruct destroys it.

### Self-destruct
- Select the replicator to see its blast radius (red ring) and flee radius (yellow ring).
- **Arm self-destruct** orders a colonist with **Basic** work enabled to walk to the interaction spot and arm it (about 2 seconds).
- Once armed, a map-wide "Self-destruct armed" letter appears. The 10-second countdown shows in the inspect pane, with a hissing alarm, camera shake and a flashing blast ring visible even when the replicator isn't selected. Pawns within 25 tiles who aren't drafted run away.
- At zero: a Bomb explosion of radius 75, plus an 80% chance to start a fire in every cell of that radius. The device and its stored food are destroyed.
- **Cancel self-destruct order** withdraws the order before anyone arms it. **Abort self-destruct** stops a running countdown.
- It never triggers on its own; it only arms after an explicit player order.

## Tuning
All values are XML. The replicator's are in `Defs/ThingDefs/Buildings_RandomReplicator.xml`, under `CompProperties_RandomReplicator`:

| Field | Default | Meaning |
|---|---|---|
| `ticksPerSpawn` | 15000 | 6h production cadence (60,000 ticks/day) |
| `storageCapacity` | 10 | items held; production pauses when full |
| `allowedThingCategoryDefs` | FoodMeals, FoodRaw | also `allowedThingDefs` / `disallowedThingDefs` lists |
| `mealWeight` | 5 | meal odds relative to raw food |
| `requirePower` | true | unpowered: no production and no refrigeration |
| `refrigerationPowerConsumption` | 50 | extra W while refrigeration is on (a vanilla cooler is 200 W) |
| `sendPowerCutLetters` | true | letter when power is lost with refrigerated food stored |
| `selfDestructArmingTicks` | 120 | pawn work to arm it (2 seconds) |
| `selfDestructSeconds` | 10 | countdown length |
| `selfDestructRadius` | 75 | blast radius; the engine maximum is ~79.8 |
| `selfDestructDamage` | -1 | explosion damage; -1 uses Bomb's default |
| `selfDestructFireChance` | 0.8 | per-cell ignite chance |
| `selfDestructFireSize` | 0.3 | size of each started fire |
| `selfDestructFleeRadius` | 25 | pawns this close flee |

The storage tab's default priority and filter are under `<building><defaultStorageSettings>` in the same file.

The reactor's are in `Defs/ThingDefs/Buildings_Batshittium.xml`, under `CompProperties_BatshittiumReactor`. Its power output is `basePowerConsumption` (-250000, negative means output), and its fuel buffer is `fuelCapacity` (2):

| Field | Default | Meaning |
|---|---|---|
| `burnHoursRange` | 0.1~36 | in-game hours one unit burns |
| `heatPerSecond` | 1000 | total waste heat (a vanilla heater pushes 21) |
| `heatRadius` | 12 | rooms within this many tiles get a share |
| `heatMaxTemperature` | 200 | °C; stops heating a room here (things auto-ignite above 240) |

The Transmogrifier's ratio and work are in `Defs/RecipeDefs/RR_Recipes.xml`.

## Textures
The Batshittium, Transmogrifier and Reactor art is generated by `Tools/make_textures.py` (needs Pillow: `pip install pillow`). These are placeholders: replace any PNG under `Textures/` with hand-drawn art at the same path and size. The Random Replicator still borrows the vanilla Infinite Chemreactor sprite.

## History
- **v0.1**: food replicator.
- **v0.2**: self-destruct added (RimWorld 1.5).
- **v0.3**: v0.2 restored and ported to 1.6. Fixes: the work giver pointed at a work type that doesn't exist (`Flick`), so nobody could arm the device. Invalid DesignationDef/JobDef fields. A missing interaction cell. A non-looping alarm sound. Pawns sent into `PanicFlee`, which makes them leave the map. Gizmos with no icons. Self-destruct numbers moved into XML.
- **v0.4**: refrigerated internal storage (10) in place of dropping food on the floor, emptied by haulers (storage tab, Low priority). Refrigeration toggle (+50W). Auto-eject toggle. Eject gizmos. Blast and flee ring overlays, flashing during a countdown. Power-cut letters. "Self-destruct armed" letter. Arming time, explosion damage and fire size moved into XML. (The original v0.4 code was lost; this is a rebuild from its feature list.)
- **v0.5**: Batshit Technology research tab (Batshittium Lore 500, Batshittium Arcana 750), which now unlocks the replicator. Batshittium resource. Batshittium Transmogrifier (10 rock chunks → 1 batshittium). Batshittium Reactor (250,000 W, random 0.1–36 h per unit, area waste heat). Custom textures for the new items.

MIT-style license for code. RimWorld © Ludeon Studios. Uses vanilla textures and sounds by reference (not redistributed).
