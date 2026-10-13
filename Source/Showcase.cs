// RandomReplicator - in-game showcase.
// Runs only when RimWorld starts with -rrshowcase (Tools/smoke-test.ps1
// -Showcase). On the quick-test map it finishes the mod's research and builds
// a working demo base: a fuelled reactor powering a stocked replicator, the
// Transmogrifier with a bill and rock chunks, a colonist in the Brain
// Squeezer, the Artificer making bolts, and a Ballista with chained,
// loaded magazines firing explosive volleys at open ground. Then it leaves
// the game running.

using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace RandomReplicator
{
    /// <summary>Builds the showcase base.</summary>
    internal static class Showcase
    {
        /// <summary>The command-line flag that requests the showcase.</summary>
        internal const string Flag = "rrshowcase";

        /// <summary>Builds the demo base around the map's center.</summary>
        /// <param name="map">The quick-test map.</param>
        internal static void Build(Map map)
        {
            FinishResearch();
            DebugSettings.godMode = true; // show the DEV buttons
            IntVec3 center = map.Center;
            Clear(map, CellRect.CenteredOn(center, 16));

            Thing reactor = Spawn(map, "RR_BatshittiumReactor", center);
            reactor.TryGetComp<CompRefuelable>().Refuel(2f);

            var replicator = (Building_RandomReplicator)Spawn(
                map, "RandomReplicator", center + new IntVec3(-5, 0, 0));
            var replicatorComp = replicator.GetComp<CompRandomReplicator>();
            for (int i = 0; i < 6; i++)
            {
                replicatorComp.ReplicateNow();
            }

            var bench = (Building_WorkTable)Spawn(
                map, "RR_BatshittiumTransmogrifier", center + new IntVec3(5, 0, 0));
            bench.billStack.AddBill(
                DefDatabase<RecipeDef>.GetNamed("RR_TransmogrifyRockChunks").MakeNewBill());
            ThingDef chunk = DefDatabase<ThingDef>.GetNamed("ChunkGranite");
            for (int i = 0; i < 20; i++)
            {
                GenPlace.TryPlaceThing(
                    ThingMaker.MakeThing(chunk), center + new IntVec3(9, 0, -2),
                    map, ThingPlaceMode.Near);
            }

            Thing squeezer = Spawn(map, "RR_BrainSqueezer", center + new IntVec3(0, 0, -5));

            Thing artificer = Spawn(map, "RR_BatshittiumArtificer", center + new IntVec3(3, 0, 3));
            artificer.TryGetComp<CompRefuelable>().Refuel(10f);

            BuildBallista(map, center);

            // Stock for haulers: batshittium to refuel the reactor and
            // Artificer, and a stockpile for replicated food.
            Thing batshittium = ThingMaker.MakeThing(
                DefDatabase<ThingDef>.GetNamed("RR_Batshittium"));
            batshittium.stackCount = 30;
            GenPlace.TryPlaceThing(batshittium, center + new IntVec3(-3, 0, 4), map,
                ThingPlaceMode.Near);
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(zone);
            foreach (IntVec3 c in CellRect.CenteredOn(center + new IntVec3(-9, 0, -9), 2))
            {
                zone.AddCell(c);
            }

            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            SendColonistToSqueezer(map, squeezer);
            Find.CameraDriver.JumpToCurrentMapLoc(center);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            Messages.Message(
                "Random Replicator showcase: everything is built and running. "
                    + "Select things to see their buttons and status.",
                MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>
        /// A Ballista with three chained magazines of explosive bolts, firing
        /// 16-bolt volleys at open ground 60 tiles east.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The base's center.</param>
        private static void BuildBallista(Map map, IntVec3 center)
        {
            var ballista = (Building_BatshittiumBallista)Spawn(
                map, "RR_BatshittiumBallista", center + new IntVec3(4, 0, -5));
            ThingDef explosive = RR_BallistaDefOf.RR_BoltReallyPainful;
            Fill(ballista.InternalMagazine, explosive, 16);
            foreach (IntVec3 offset in new[] { new IntVec3(6, 0, -5), new IntVec3(7, 0, -5),
                new IntVec3(7, 0, -6) })
            {
                var magazine = (Building_BallistaMagazine)Spawn(
                    map, "RR_BallistaMagazine", center + offset);
                Fill(magazine.Storage, explosive, 400);
                Fill(magazine.Storage, RR_BallistaDefOf.RR_BoltPainful, 200);
            }

            ballista.SetAmmo(explosive);
            ballista.SetCount(16);
            ballista.SetRange(75);
            IntVec3 target = center + new IntVec3(60, 0, 0);
            target.x = System.Math.Min(target.x, map.Size.x - 10);
            ballista.OrderAttack(new LocalTargetInfo(target));
        }

        /// <summary>Starts a free colonist on a session in the squeezer.</summary>
        /// <param name="map">The map.</param>
        /// <param name="squeezer">The Brain Squeezer.</param>
        private static void SendColonistToSqueezer(Map map, Thing squeezer)
        {
            Pawn pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                p => !p.Downed && !p.InMentalState && p.needs?.joy != null);
            if (pawn == null)
            {
                return;
            }

            pawn.needs.joy.CurLevel = 0.2f;
            pawn.jobs.StartJob(
                JobMaker.MakeJob(RR_BrainSqueezerDefOf.RR_UseBrainSqueezer, squeezer,
                    squeezer.Position),
                JobCondition.InterruptForced);
        }

        /// <summary>Finishes every project in the Batshit Technology tab.</summary>
        private static void FinishResearch()
        {
            foreach (ResearchProjectDef project in DefDatabase<ResearchProjectDef>.AllDefs
                .Where(p => p.tab?.defName == "RR_BatshitTechnology" && !p.IsFinished)
                .OrderBy(p => p.researchViewX))
            {
                Find.ResearchManager.FinishProject(
                    project, doCompletionDialog: false, doCompletionLetter: false);
            }
        }

        /// <summary>Clears a square to concrete, keeping pawns.</summary>
        /// <param name="map">The map.</param>
        /// <param name="rect">The area.</param>
        private static void Clear(Map map, CellRect rect)
        {
            foreach (IntVec3 c in rect)
            {
                if (!c.InBounds(map))
                {
                    continue;
                }

                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (!(t is Pawn))
                    {
                        t.Destroy();
                    }
                }

                map.terrainGrid.SetTerrain(c, TerrainDefOf.Concrete);
                map.roofGrid.SetRoof(c, null);
            }
        }

        /// <summary>Spawns a player-owned building.</summary>
        /// <param name="map">The map.</param>
        /// <param name="defName">The ThingDef.</param>
        /// <param name="at">Where.</param>
        /// <returns>The building.</returns>
        private static Thing Spawn(Map map, string defName, IntVec3 at)
        {
            Thing thing = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName));
            thing.SetFaction(Faction.OfPlayer);
            return GenSpawn.Spawn(thing, at, map, WipeMode.Vanish);
        }

        /// <summary>Puts bolts straight into a bolt store.</summary>
        /// <param name="storage">The store.</param>
        /// <param name="bolt">The bolt type.</param>
        /// <param name="count">How many; split into full stacks.</param>
        private static void Fill(BoltStorage storage, ThingDef bolt, int count)
        {
            while (count > 0)
            {
                Thing stack = ThingMaker.MakeThing(bolt);
                stack.stackCount = System.Math.Min(count, bolt.stackLimit);
                count -= stack.stackCount;
                storage.Container.TryAdd(stack);
            }
        }
    }
}
