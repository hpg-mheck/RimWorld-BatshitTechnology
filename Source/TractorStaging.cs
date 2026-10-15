// Batshit Technology - staging for Tractor Bomb scenes, shared by the
// smoke runner (SmokeTest.cs and its Tractor parts) and the directed tour
// (docs/SHOWCASE.md): a cleared yard, the barn, a robot, a party of
// raiders, and the twin junkyard laid out from Core's TwinJunkyardLayout.
// Moved out of the smoke runner in slice 12000, so the tour stages what
// the smoke checks prove.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace BatshitTechnology
{
    /// <summary>Stages Tractor Bomb scenes on a map.</summary>
    internal static class TractorStaging
    {
        /// <summary>
        /// How far beyond a yard's half-size wild animals are removed, in
        /// tiles, so none wanders into a pull uninvited.
        /// </summary>
        internal const int AnimalMargin = 25;

        /// <summary>A map cell from a centre and a Core offset.</summary>
        /// <param name="centre">The centre.</param>
        /// <param name="offset">The offset, as (x,z).</param>
        /// <returns>The cell.</returns>
        internal static IntVec3 At(IntVec3 centre, Cell offset) =>
            centre + new IntVec3(offset.X, 0, offset.Z);

        /// <summary>
        /// Clears an area to concrete under open sky, with nothing on it
        /// but pawns. Things that cannot be destroyed (steam geysers) stay:
        /// trying logs an error.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="rect">The area.</param>
        internal static void LevelGround(Map map, CellRect rect)
        {
            foreach (IntVec3 c in rect)
            {
                if (!c.InBounds(map))
                {
                    continue;
                }

                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (!(t is Pawn) && t.def.destroyable)
                    {
                        t.Destroy();
                    }
                }

                map.terrainGrid.SetTerrain(c, TerrainDefOf.Concrete);
                map.roofGrid.SetRoof(c, null);
            }
        }

        /// <summary>
        /// Clears a yard for the Tractor Bomb: level and unfogged, no fire
        /// anywhere on the map, no hostiles (never the player's own pawns,
        /// even berserk ones) and no robots anywhere, and no wild animals
        /// near it. Colonists are left where they are; the caller moves
        /// them.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="yard">The yard.</param>
        internal static void ClearYard(Map map, CellRect yard)
        {
            LevelGround(map, yard);
            foreach (IntVec3 c in yard)
            {
                if (c.InBounds(map))
                {
                    map.fogGrid.Unfog(c);
                }
            }

            foreach (Thing fire in map.listerThings.ThingsOfDef(ThingDefOf.Fire)
                .ToList())
            {
                fire.Destroy();
            }

            IntVec3 centre = yard.CenterCell;
            float near = Mathf.Max(yard.Width, yard.Height) / 2 + AnimalMargin;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                // The player's own pawns (colonists, slaves, animals,
                // prisoners and guests) are never removed, even when a
                // mental state, a prison break or a rebellion makes them
                // HostileTo the player (a berserk colonist is).
                bool ours = p.Faction == Faction.OfPlayer
                    || p.HostFaction == Faction.OfPlayer;
                if ((!ours && p.HostileTo(Faction.OfPlayer))
                    || (p.RaceProps.Animal && p.Faction == null
                        && p.Position.InHorDistOf(centre, near))
                    || p.TryGetComp<CompTractorBomb>() != null)
                {
                    p.Destroy(DestroyMode.Vanish);
                }
            }
        }

        /// <summary>
        /// Spawns a player's Tractor Barn with its repair hopper full, its
        /// door facing south.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="at">Its centre.</param>
        /// <returns>The barn.</returns>
        internal static Building_TractorBarn SpawnBarn(Map map, IntVec3 at)
        {
            Thing thing = ThingMaker.MakeThing(
                DefDatabase<ThingDef>.GetNamed("BT_TractorBarn"));
            thing.SetFaction(Faction.OfPlayer);
            var barn = (Building_TractorBarn)GenSpawn.Spawn(
                thing, at, map, WipeMode.Vanish);
            CompRefuelable hopper = barn.RepairHopper;
            hopper?.Refuel(hopper.Props.fuelCapacity);
            return barn;
        }

        /// <summary>A fresh player robot, exactly where asked.</summary>
        /// <param name="map">The map.</param>
        /// <param name="at">Where to put it.</param>
        /// <returns>The robot, spawned.</returns>
        internal static Pawn SpawnRobot(Map map, IntVec3 at) =>
            (Pawn)GenSpawn.Spawn(
                TractorBombUtility.MakeRobot(Faction.OfPlayer), at, map);

        /// <summary>
        /// A party of raiders of a hostile faction, holding position round
        /// a centre (a defend-point lord that never flees): rested, fed and
        /// free of ailments, armed with rifles or with nothing, and stunned
        /// for longer than any scene lasts if asked.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="centre">The party's centre.</param>
        /// <param name="offsets">Where each raider stands, from the centre.</param>
        /// <param name="armed">Whether each carries a rifle.</param>
        /// <param name="stunned">Whether they start stunned.</param>
        /// <returns>The raiders, spawned; empty if no faction is hostile.</returns>
        internal static List<Pawn> SpawnArmy(
            Map map, IntVec3 centre, IEnumerable<Cell> offsets, bool armed,
            bool stunned)
        {
            var army = new List<Pawn>();
            Faction enemy = Find.FactionManager.RandomEnemyFaction(
                allowNonHumanlike: false);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(
                "Mercenary_Gunner") ?? PawnKindDefOf.Drifter;
            ThingDef rifle = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Gun_AssaultRifle");
            if (enemy == null || offsets == null)
            {
                return army;
            }

            foreach (Cell offset in offsets)
            {
                Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    kind, enemy, forceGenerateNewPawn: true,
                    mustBeCapableOfViolence: true, dontGiveWeapon: !armed));
                GenSpawn.Spawn(p, At(centre, offset), map);
                if (!armed)
                {
                    p.equipment?.DestroyAllEquipment();
                }
                else if (p.equipment != null && p.equipment.Primary == null
                    && rifle != null)
                {
                    p.equipment.AddEquipment(
                        (ThingWithComps)ThingMaker.MakeThing(rifle));
                }

                Refresh(p);
                Heal(p);
                if (stunned)
                {
                    p.stances.stunner.StunFor(
                        100000, null, addBattleLog: false, showMote: false);
                }

                army.Add(p);
            }

            if (army.Count > 0)
            {
                LordMaker.MakeNewLord(enemy, new LordJob_DefendPoint(
                    centre, wanderRadius: 2f, defendRadius: 6f,
                    addFleeToil: false), map, army);
            }

            return army;
        }

        /// <summary>
        /// Lays out the junkyard round a centre (Core's
        /// <see cref="TwinJunkyardLayout"/>): steel slag and rock chunks,
        /// loose stacks, small shelves (crates) of items, and ancient
        /// crates. A cell that is off the map, not standable or already
        /// holding an item is skipped, as is a def the game lacks.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="centre">The middle of the army.</param>
        /// <returns>
        /// The item defs laid out, whose totals must hold through the
        /// pulls.
        /// </returns>
        internal static HashSet<ThingDef> BuildJunkyard(Map map, IntVec3 centre)
        {
            ThingDef slag = DefDatabase<ThingDef>.GetNamedSilentFail(
                TwinJunkyardLayout.SlagDef);
            ThingDef rock = DefDatabase<ThingDef>.GetNamedSilentFail(
                TwinJunkyardLayout.RockDef) ?? DefDatabase<ThingDef>.AllDefs
                .FirstOrDefault(d => d.thingCategories?.Contains(
                    ThingCategoryDefOf.StoneChunks) == true);
            var defs = new HashSet<ThingDef>();

            // Lays one stack of a def at an offset, clamped to its stack
            // limit, unless the def is missing or the cell is unusable.
            // (A local function takes no XML documentation.)
            void Lay(ThingDef def, Cell offset, int count)
            {
                IntVec3 c = At(centre, offset);
                if (def == null || !c.InBounds(map) || !c.Standable(map)
                    || c.GetFirstItem(map) != null)
                {
                    return;
                }

                Thing item = ThingMaker.MakeThing(def);
                item.stackCount = Mathf.Clamp(count, 1, def.stackLimit);
                GenSpawn.Spawn(item, c, map);
                defs.Add(def);
            }

            foreach (Cell offset in TwinJunkyardLayout.Slag)
            {
                Lay(slag, offset, 1);
            }

            foreach (Cell offset in TwinJunkyardLayout.Rock)
            {
                Lay(rock, offset, 1);
            }

            foreach (LaidStack stack in TwinJunkyardLayout.Stacks)
            {
                Lay(DefDatabase<ThingDef>.GetNamedSilentFail(stack.DefName),
                    stack.At, stack.Count);
            }

            // Crates of items: small shelves, three stacks on each, which
            // the pulls rip loose from under their stacks.
            ThingDef shelf = DefDatabase<ThingDef>.GetNamedSilentFail(
                TwinJunkyardLayout.ShelfDef);
            foreach (Cell offset in TwinJunkyardLayout.Shelves)
            {
                IntVec3 c = At(centre, offset);
                if (shelf == null || !c.InBounds(map)
                    || c.GetFirstItem(map) != null)
                {
                    continue;
                }

                GenSpawn.Spawn(ThingMaker.MakeThing(shelf, ThingDefOf.Steel),
                    c, map);
                foreach (var (defName, count) in TwinJunkyardLayout.ShelfStock)
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(
                        defName);
                    if (def == null)
                    {
                        continue;
                    }

                    Thing item = ThingMaker.MakeThing(def);
                    item.stackCount = count;
                    GenSpawn.Spawn(item, c, map);
                    defs.Add(def);
                }
            }

            ThingDef crate = DefDatabase<ThingDef>.GetNamedSilentFail(
                TwinJunkyardLayout.CrateDef);
            foreach (Cell offset in TwinJunkyardLayout.Crates)
            {
                IntVec3 c = At(centre, offset);
                if (crate != null && c.InBounds(map)
                    && c.GetFirstItem(map) == null)
                {
                    GenSpawn.Spawn(ThingMaker.MakeThing(crate), c, map);
                }
            }

            return defs;
        }

        /// <summary>
        /// Lays the tour's junk round a yard's centre (Core's
        /// <see cref="TourYardJunk"/>): each stack where it says, chunks
        /// and items as one stack, a building (an ancient crate) as itself.
        /// A cell that is off the map, not standable or already holding an
        /// item or a building is skipped, as is a def the game lacks.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="centre">The yard's centre.</param>
        /// <param name="junk">The stacks, as offsets from the centre.</param>
        /// <returns>How many pieces were laid.</returns>
        internal static int LayJunk(
            Map map, IntVec3 centre, IEnumerable<LaidStack> junk)
        {
            int laid = 0;
            foreach (LaidStack stack in junk ?? Enumerable.Empty<LaidStack>())
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(stack.DefName);
                IntVec3 c = At(centre, stack.At);
                if (def == null || !c.InBounds(map) || !c.Standable(map)
                    || c.GetFirstItem(map) != null || c.GetFirstBuilding(map) != null)
                {
                    continue;
                }

                Thing thing = ThingMaker.MakeThing(
                    def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                if (def.category == ThingCategory.Item)
                {
                    thing.stackCount = Mathf.Clamp(stack.Count, 1, def.stackLimit);
                }

                GenSpawn.Spawn(thing, c, map);
                laid++;
            }

            return laid;
        }

        /// <summary>
        /// Rids a fresh pawn of whatever ailments it was generated with (a
        /// bad back, a missing toe, frailty), so how far it runs in a
        /// countdown depends on its legs, not on luck. A sabotage-free
        /// smoke run once failed the flee check by 0.7 tiles on a muffalo.
        /// </summary>
        /// <param name="p">The pawn.</param>
        internal static void Heal(Pawn p)
        {
            foreach (Hediff h in p.health.hediffSet.hediffs
                .Where(h => h.def.isBad).ToList())
            {
                p.health.RemoveHediff(h);
            }
        }

        /// <summary>
        /// Tops up a pawn's rest, food and joy, so it neither lies down to
        /// sleep (a sleeping pawn sees nothing) nor wanders off to eat.
        /// </summary>
        /// <param name="p">The pawn.</param>
        internal static void Refresh(Pawn p)
        {
            if (p.needs?.rest != null)
            {
                p.needs.rest.CurLevel = 1f;
            }

            if (p.needs?.food != null)
            {
                p.needs.food.CurLevel = p.needs.food.MaxLevel;
            }

            if (p.needs?.joy != null)
            {
                p.needs.joy.CurLevel = 1f;
            }
        }
    }
}
