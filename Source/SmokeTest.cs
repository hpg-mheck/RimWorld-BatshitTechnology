// RandomReplicator - in-game smoke test.
// Runs only when RimWorld starts with -rrsmoketest (Tools/smoke-test.ps1
// passes it with -quicktest). Builds each device on the quick-test map,
// exercises it with real game ticks, logs "[RR-SMOKE] PASS/FAIL" lines and
// a "[RR-SMOKE] DONE" summary, then quits (unless -rrsmoketestkeep is also
// passed). Without the flag it does nothing.

using System;
using System.Diagnostics;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace RandomReplicator
{
    /// <summary>
    /// Drives the in-game smoke test once the quick-test map is up.
    /// </summary>
    public class GameComponent_SmokeTest : GameComponent
    {
        private const string Flag = "rrsmoketest";
        private const string KeepOpenFlag = "rrsmoketestkeep";
        private const int SettleFrames = 120;

        private bool done;
        private int framesWithMap;
        private int passed;
        private int failed;

        /// <summary>Created by RimWorld for every game.</summary>
        /// <param name="game">The game this component belongs to.</param>
        public GameComponent_SmokeTest(Game game)
        {
        }

        /// <summary>
        /// Once per game, after the map has settled for a couple of
        /// seconds, runs the test and quits.
        /// </summary>
        public override void GameComponentUpdate()
        {
            if (done || Current.ProgramState != ProgramState.Playing
                || Find.CurrentMap == null
                || !GenCommandLine.CommandLineArgPassed(Flag))
            {
                return;
            }

            if (++framesWithMap < SettleFrames)
            {
                return;
            }

            done = true;
            try
            {
                Run(Find.CurrentMap);
            }
            catch (Exception e)
            {
                Check("runner completed without an exception", false, e.ToString());
            }

            Log.Message($"[RR-SMOKE] DONE passed={passed} failed={failed}");
            if (!GenCommandLine.CommandLineArgPassed(KeepOpenFlag))
            {
                Root.Shutdown();
            }
        }

        /// <summary>Runs every scenario, destructive ones last.</summary>
        /// <param name="map">The quick-test map.</param>
        private void Run(Map map)
        {
            IntVec3 center = map.Center;
            PrepareGround(map, CellRect.CenteredOn(center, 14));

            Thing reactor = Spawn(map, "RR_BatshittiumReactor", center);
            var fuel = reactor.TryGetComp<CompRefuelable>();
            fuel.Refuel(2f);
            Thing replicator =
                Spawn(map, "RandomReplicator", center + new IntVec3(-5, 0, 0));
            Thing bench = Spawn(
                map, "RR_BatshittiumTransmogrifier", center + new IntVec3(5, 0, 0));
            // A power net switches waiting consumers on one at a time, every
            // 200 / (number waiting) ticks, so give it a while.
            WaitForPower(replicator, bench);

            TestReactorPower(reactor, fuel);
            TestTransmogrifier(bench);
            TestReplicatorStorage(map, replicator);
            TestReactorHeat(map, center);
            TestReactorBurnCycle(reactor, fuel);
            TestSelfDestructAbort(replicator);
            TestDetonation(replicator);
        }

        /// <summary>The fuelled reactor produces 250 kW and is burning.</summary>
        /// <param name="reactor">The reactor.</param>
        /// <param name="fuel">Its refuelable comp.</param>
        private void TestReactorPower(Thing reactor, CompRefuelable fuel)
        {
            var plant = reactor.TryGetComp<CompPowerPlant>();
            var comp = reactor.TryGetComp<CompBatshittiumReactor>();
            Check("reactor produces 250 kW", plant.PowerOutput == 250000f,
                $"output {plant.PowerOutput}");
            Check("reactor is burning a unit", comp.BurnTicksLeft > 0,
                $"burnTicksLeft {comp.BurnTicksLeft}");
            Check("reactor keeps both units until a burn ends", fuel.Fuel == 2f,
                $"fuel {fuel.Fuel}");
        }

        /// <summary>
        /// Ticks until every consumer is switched on, or 1200 ticks pass.
        /// </summary>
        /// <param name="consumers">The powered buildings to wait for.</param>
        private static void WaitForPower(params Thing[] consumers)
        {
            for (int waited = 0; waited < 1200; waited += 20)
            {
                // RimWorld wires new buildings into power nets once per
                // frame (Map.MapUpdate), not per tick. This test ticks
                // inside a single frame, so do that step itself.
                consumers[0].Map.powerNetManager.UpdatePowerNetsAndConnections_First();
                if (consumers.All(t => t.TryGetComp<CompPowerTrader>().PowerOn))
                {
                    return;
                }

                Tick(20);
            }
        }

        /// <summary>Describes a consumer's grid connection, for failures.</summary>
        /// <param name="thing">The powered building.</param>
        /// <returns>Connection and net surplus.</returns>
        private static string PowerDetail(Thing thing)
        {
            PowerNet net = thing.TryGetComp<CompPowerTrader>().PowerNet;
            return net == null
                ? "not connected to any power net"
                : $"connected; net surplus {net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick:F0} W";
        }

        /// <summary>The transmogrifier is powered and offers its recipe.</summary>
        /// <param name="bench">The transmogrifier.</param>
        private void TestTransmogrifier(Thing bench)
        {
            var table = (Building_WorkTable)bench;
            Check("transmogrifier is powered",
                bench.TryGetComp<CompPowerTrader>().PowerOn, PowerDetail(bench));
            Check("transmogrifier can take bills", table.CurrentlyUsableForBills());
            Check("transmogrifier offers its recipe",
                bench.def.AllRecipes.Any(r => r.defName == "RR_TransmogrifyRockChunks"));
        }

        /// <summary>
        /// Replication, chilling, the power draw, hauling and ejection.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="replicator">The replicator.</param>
        private void TestReplicatorStorage(Map map, Thing replicator)
        {
            var building = (Building_RandomReplicator)replicator;
            var comp = replicator.TryGetComp<CompRandomReplicator>();
            var power = replicator.TryGetComp<CompPowerTrader>();
            ThingOwner storage = building.GetDirectlyHeldThings();

            Check("replicator is powered", power.PowerOn, PowerDetail(replicator));
            Check("replicator draws 100 W with refrigeration on",
                power.PowerOutput == -100f, $"output {power.PowerOutput}");

            comp.ReplicateNow();
            Check("replicating stores one food item",
                storage.TotalStackCount == 1 && storage[0].def.IsIngestible,
                $"stored {storage.ContentsString}");
            Check("stored food is chilled (suspended)", building.IsContentsSuspended);

            comp.Refrigeration = false;
            Tick(2);
            Check("refrigeration off drops the draw to 50 W",
                power.PowerOutput == -50f, $"output {power.PowerOutput}");
            Check("refrigeration off stops chilling", !building.IsContentsSuspended);
            comp.Refrigeration = true;
            Tick(2);

            // A better stockpile makes the stored food haulable.
            var zone = new Zone_Stockpile(
                StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(zone);
            foreach (IntVec3 c in CellRect.CenteredOn(
                replicator.Position + new IntVec3(0, 0, -6), 1))
            {
                zone.AddCell(c);
            }

            Tick(60);
            Thing stored = storage.Any ? storage[0] : null;
            Check("haulers see stored food once a better stockpile exists",
                stored == null
                    || map.listerHaulables.ThingsPotentiallyNeedingHauling()
                        .Contains(stored),
                "stored food not listed as haulable");

            comp.ReplicateNow();
            int storedBefore = storage.TotalStackCount;
            comp.EjectAllNow();
            Check("eject all empties storage onto the map",
                storedBefore > 0 && !storage.Any,
                $"before {storedBefore}, after {storage.TotalStackCount}");

            comp.AutoEject = true;
            comp.ReplicateNow();
            Check("auto-eject drops new food instead of storing it", !storage.Any);
            comp.AutoEject = false;
        }

        /// <summary>
        /// A small roofed room near the burning reactor heats up.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The reactor's position.</param>
        private void TestReactorHeat(Map map, IntVec3 center)
        {
            CellRect room = CellRect.CenteredOn(center + new IntVec3(0, 0, 6), 1);
            foreach (IntVec3 c in room.ExpandedBy(1).EdgeCells)
            {
                Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
                wall.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(wall, c, map, WipeMode.Vanish);
            }

            foreach (IntVec3 c in room.ExpandedBy(1))
            {
                map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            }

            Tick(10); // rooms rebuild
            Room target = room.CenterCell.GetRoom(map);
            float before = target?.Temperature ?? float.NaN;
            Tick(1000);
            target = room.CenterCell.GetRoom(map);
            float after = target?.Temperature ?? float.NaN;
            Check("reactor heats a nearby room",
                target != null && !target.UsesOutdoorTemperature && after > before + 5f,
                $"room {(target == null ? "missing" : "ok")}, {before:F1} C -> {after:F1} C");
        }

        /// <summary>
        /// Ending a burn spends one unit and starts the next; ending the
        /// last leaves the reactor idle at 0 W.
        /// </summary>
        /// <param name="reactor">The reactor.</param>
        /// <param name="fuel">Its refuelable comp.</param>
        private void TestReactorBurnCycle(Thing reactor, CompRefuelable fuel)
        {
            var comp = reactor.TryGetComp<CompBatshittiumReactor>();
            var plant = reactor.TryGetComp<CompPowerPlant>();

            comp.EndBurnNow();
            Tick(3);
            Check("a finished burn spends one unit", fuel.Fuel == 1f, $"fuel {fuel.Fuel}");
            Check("the next unit starts burning at once",
                comp.BurnTicksLeft > 0 && plant.PowerOutput == 250000f,
                $"burnTicksLeft {comp.BurnTicksLeft}, output {plant.PowerOutput}");

            comp.EndBurnNow();
            Tick(3);
            Check("with no fuel left the reactor idles at 0 W",
                !fuel.HasFuel && plant.PowerOutput == 0f,
                $"fuel {fuel.Fuel}, output {plant.PowerOutput}");
        }

        /// <summary>Arming starts the countdown; aborting stops it.</summary>
        /// <param name="replicator">The replicator.</param>
        private void TestSelfDestructAbort(Thing replicator)
        {
            var comp = replicator.TryGetComp<CompRandomReplicator>();
            comp.ArmSelfDestruct();
            Tick(30);
            Check("arming starts the countdown", comp.IsSelfDestructCountingDown);
            comp.AbortSelfDestruct();
            Check("aborting stops the countdown", !comp.IsSelfDestructCountingDown);
        }

        /// <summary>
        /// A full countdown destroys the replicator. Also times the blast
        /// and the fires that follow, as a performance check.
        /// </summary>
        /// <param name="replicator">The replicator.</param>
        private void TestDetonation(Thing replicator)
        {
            var comp = replicator.TryGetComp<CompRandomReplicator>();
            comp.ArmSelfDestruct();
            var watch = Stopwatch.StartNew();
            Tick(Mathf.RoundToInt(comp.Props.selfDestructSeconds * 60f) + 10);
            long countdownMs = watch.ElapsedMilliseconds;
            Check("the countdown ends in detonation", replicator.Destroyed);
            watch.Restart();
            Tick(600);
            Log.Message($"[RR-SMOKE] TIMING countdown+blast {countdownMs} ms; "
                + $"next 600 ticks of fire {watch.ElapsedMilliseconds} ms");
        }

        /// <summary>Clears a square to concrete with nothing on it.</summary>
        /// <param name="map">The map.</param>
        /// <param name="rect">The area to clear.</param>
        private static void PrepareGround(Map map, CellRect rect)
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
        /// <param name="defName">The ThingDef to spawn.</param>
        /// <param name="at">Where to spawn it.</param>
        /// <returns>The spawned thing.</returns>
        private static Thing Spawn(Map map, string defName, IntVec3 at)
        {
            Thing thing = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName));
            thing.SetFaction(Faction.OfPlayer);
            return GenSpawn.Spawn(thing, at, map, WipeMode.Vanish);
        }

        /// <summary>Advances the game by whole ticks, synchronously.</summary>
        /// <param name="ticks">How many ticks to run.</param>
        private static void Tick(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                Find.TickManager.DoSingleTick();
            }
        }

        /// <summary>Logs one PASS or FAIL line and counts it.</summary>
        /// <param name="name">What was checked.</param>
        /// <param name="ok">Whether it held.</param>
        /// <param name="detail">What was observed, for failures.</param>
        private void Check(string name, bool ok, string detail = null)
        {
            if (ok)
            {
                passed++;
                Log.Message($"[RR-SMOKE] PASS {name}");
            }
            else
            {
                failed++;
                Log.Warning($"[RR-SMOKE] FAIL {name}: {detail}");
            }
        }
    }
}
