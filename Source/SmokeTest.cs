// Batshit Technology - in-game smoke test.
// Runs only when RimWorld starts with -btsmoketest (Tools/smoke-test.ps1
// passes it with -quicktest). Builds each device on the quick-test map,
// exercises it with real game ticks, logs "[BT-SMOKE] PASS/FAIL" lines and
// a "[BT-SMOKE] DONE" summary, then quits (unless -btsmoketestkeep is also
// passed). Without the flag it does nothing.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Verse;
using RimWorld;
using Verse.AI;
using UnityEngine;

namespace BatshitTechnology
{
    /// <summary>
    /// Drives the in-game smoke test once the quick-test map is up.
    /// </summary>
    public class GameComponent_SmokeTest : GameComponent
    {
        private const string Flag = "btsmoketest";
        private const string KeepOpenFlag = "btsmoketestkeep";
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
            bool showcase = GenCommandLine.CommandLineArgPassed(Showcase.Flag);
            if (done || Current.ProgramState != ProgramState.Playing
                || Find.CurrentMap == null
                || !(showcase || GenCommandLine.CommandLineArgPassed(Flag)))
            {
                return;
            }

            if (++framesWithMap < SettleFrames)
            {
                return;
            }

            done = true;
            if (showcase)
            {
                Showcase.Build(Find.CurrentMap); // and leave the game running
                return;
            }

            try
            {
                Run(Find.CurrentMap);
            }
            catch (Exception e)
            {
                Check("runner completed without an exception", false, e.ToString());
            }

            Log.Message($"[BT-SMOKE] DONE passed={passed} failed={failed}");
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

            Thing reactor = Spawn(map, "BT_BatshittiumReactor", center);
            var fuel = reactor.TryGetComp<CompRefuelable>();
            fuel.Refuel(2f);
            Thing replicator =
                Spawn(map, "BT_RandomReplicator", center + new IntVec3(-5, 0, 0));
            Thing bench = Spawn(
                map, "BT_BatshittiumTransmogrifier", center + new IntVec3(5, 0, 0));
            // A power net switches waiting consumers on one at a time, every
            // 200 / (number waiting) ticks, so give it a while.
            WaitForPower(replicator, bench);

            TestReactorPower(reactor, fuel);
            TestTransmogrifier(bench);
            TestReplicatorStorage(map, replicator);
            TestReactorHeat(map, center);
            TestBrainSqueezer(map, center); // while the reactor still has fuel
            TestBallista(map, center);
            TestArtificer(map, center);
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
                bench.def.AllRecipes.Any(r => r.defName == "BT_TransmogrifyRockChunks"));
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
        /// A colonist sits in the squeezer, mutters, and comes away with the
        /// mood memory (labelled with a monologue line), the calm hediff and
        /// more joy.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The reactor's position.</param>
        private void TestBrainSqueezer(Map map, IntVec3 center)
        {
            Thing squeezer =
                Spawn(map, "BT_BrainSqueezer", center + new IntVec3(0, 0, -5));
            WaitForPower(squeezer);
            Check("brain squeezer is powered",
                squeezer.TryGetComp<CompPowerTrader>().PowerOn, PowerDetail(squeezer));

            Pawn pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                p => !p.Downed && !p.InMentalState && p.needs?.joy != null);
            if (pawn == null)
            {
                Check("a colonist is free to be squeezed", false, "no free colonist");
                return;
            }

            pawn.needs.joy.CurLevel = 0.2f;
            int mutteredBefore = JobDriver_UseBrainSqueezer.MonologuesShown;
            JobDef useSqueezer = BT_BrainSqueezerDefOf.BT_UseBrainSqueezer;
            pawn.jobs.StartJob(
                JobMaker.MakeJob(useSqueezer, squeezer, squeezer.Position),
                JobCondition.InterruptForced);
            bool sat = false;
            for (int t = 0; t < 4000 && pawn.CurJobDef == useSqueezer; t += 10)
            {
                Tick(10);
                sat |= pawn.Position == squeezer.Position;
            }

            Check("the colonist sat in the squeezer", sat,
                $"colonist at {pawn.Position}, squeezer at {squeezer.Position}");
            Check("the colonist muttered monologue lines",
                JobDriver_UseBrainSqueezer.MonologuesShown > mutteredBefore,
                "no lines shown");
            var thought = pawn.needs.mood.thoughts.memories
                .GetFirstMemoryOfDef(BT_BrainSqueezerDefOf.BT_BrainSqueezed)
                as Thought_BrainSqueezed;
            List<string> lines = BT_BrainSqueezerDefOf.BT_BrainSqueezed
                .GetModExtension<MonologueExtension>().lines;
            Check("the colonist got the memory, labelled with a monologue line",
                thought != null && lines.Contains(thought.Line),
                thought == null ? "no memory" : $"line '{thought.Line}'");
            Check("the colonist got a day of calm",
                pawn.health.hediffSet.HasHediff(BT_BrainSqueezerDefOf.BT_BrainSqueezedCalm));
            Check("the colonist gained joy", pawn.needs.joy.CurLevel > 0.2f,
                $"joy {pawn.needs.joy.CurLevel:F2}");
        }

        /// <summary>
        /// Magazine chaining, standby and burst power, volleys from a forced
        /// target, magazines drained before the internal reserve, explosive
        /// ammo, and holding fire when the grid cannot supply a volley.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The reactor's position.</param>
        private void TestBallista(Map map, IntVec3 center)
        {
            var ballista = (Building_BatshittiumBallista)Spawn(
                map, "BT_BatshittiumBallista", center + new IntVec3(4, 0, -5));
            var chained1 = (Building_BallistaMagazine)Spawn(
                map, "BT_BallistaMagazine", center + new IntVec3(6, 0, -5));
            var chained2 = (Building_BallistaMagazine)Spawn(
                map, "BT_BallistaMagazine", center + new IntVec3(7, 0, -5));
            var detached = (Building_BallistaMagazine)Spawn(
                map, "BT_BallistaMagazine", center + new IntVec3(-3, 0, -3));
            WaitForPower(ballista, chained1, chained2, detached);
            Check("ballista and magazines are powered",
                ballista.GetComp<CompPowerTrader>().PowerOn && chained2.CanFeed && detached.CanFeed,
                PowerDetail(ballista));

            List<Building_BallistaMagazine> connected = ballista.ConnectedMagazines();
            Check("chained magazines connect; a detached one does not",
                connected.Contains(chained1) && connected.Contains(chained2)
                    && !connected.Contains(detached),
                $"{connected.Count} connected");

            var power = ballista.GetComp<CompPowerTrader>();
            ballista.SetRange(30);
            ballista.SetCount(4);
            Tick(2);
            Check("standby draw follows range x count",
                Mathf.Abs(-power.PowerOutput - ballista.StandbyWatts) < 0.5f,
                $"draw {-power.PowerOutput:F1} W, expected {ballista.StandbyWatts:F1} W");

            ThingDef painful = BT_BallistaDefOf.BT_BoltPainful;
            AddBolts(ballista.InternalMagazine, painful, 16);
            AddBolts(chained2.Storage, painful, 100);
            int volleysBefore = Building_BatshittiumBallista.VolleysFired;
            int boltsBefore = Building_BatshittiumBallista.BoltsFired;
            ballista.OrderAttack(new LocalTargetInfo(ballista.Position + new IntVec3(25, 0, 0)));
            float peakDraw = 0f;
            bool magazineFed = false;
            for (int t = 0; t < 900; t++)
            {
                Tick(1);
                peakDraw = Mathf.Max(peakDraw, -power.PowerOutput);
                magazineFed |= chained2.IsFeeding;
            }

            int volleys = Building_BatshittiumBallista.VolleysFired - volleysBefore;
            int bolts = Building_BatshittiumBallista.BoltsFired - boltsBefore;
            Check("volleys fire about every five seconds", volleys >= 2, $"{volleys} volleys in 15 s");
            Check("each volley fires the selected count", volleys > 0 && bolts == volleys * 4,
                $"{bolts} bolts in {volleys} volleys");
            Check("magazines are drained before the internal reserve",
                ballista.InternalMagazine.CountOf(painful) == 16
                    && chained2.Storage.CountOf(painful) == 100 - bolts,
                $"internal {ballista.InternalMagazine.CountOf(painful)}, "
                    + $"magazine {chained2.Storage.CountOf(painful)}");
            Check("a volley draws the firing burst",
                Mathf.Abs(peakDraw - ballista.FiringWatts) < 0.5f,
                $"peak {peakDraw:F0} W, expected {ballista.FiringWatts:F0} W");
            Check("a feeding magazine draws feeding power", magazineFed);

            ThingDef reallyPainful = BT_BallistaDefOf.BT_BoltReallyPainful;
            ballista.SetAmmo(reallyPainful);
            AddBolts(chained1.Storage, reallyPainful, 20);
            Tick(700);
            Check("explosive bolts fire when selected",
                chained1.Storage.CountOf(reallyPainful) < 20,
                $"{chained1.Storage.CountOf(reallyPainful)} of 20 left");

            // A maxed volley needs 990 kW for a second; one reactor makes 250 kW
            // and there are no batteries, so the Ballista must hold fire.
            ballista.SetAmmo(painful);
            ballista.SetCount(256);
            ballista.SetRange(325);
            int heldBefore = Building_BatshittiumBallista.VolleysHeldForPower;
            int left = chained2.Storage.CountOf(painful);
            Tick(700);
            Check("holds fire when the grid cannot supply a volley",
                Building_BatshittiumBallista.VolleysHeldForPower > heldBefore
                    && chained2.Storage.CountOf(painful) == left,
                $"held {Building_BatshittiumBallista.VolleysHeldForPower - heldBefore}, "
                    + $"bolts {left} -> {chained2.Storage.CountOf(painful)}");
            ballista.OrderAttack(LocalTargetInfo.Invalid);
            ballista.SetCount(4);
            ballista.SetRange(30);
        }

        /// <summary>
        /// The Artificer idles until a bolt type is researched, then turns
        /// each unit of batshittium into bolts, by hand and on its own.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The reactor's position.</param>
        private void TestArtificer(Map map, IntVec3 center)
        {
            Thing artificer = Spawn(map, "BT_BatshittiumArtificer", center + new IntVec3(3, 0, 3));
            var comp = artificer.TryGetComp<CompBatshittiumArtificer>();
            var fuel = artificer.TryGetComp<CompRefuelable>();
            WaitForPower(artificer);
            Check("artificer idles until a bolt type is researched",
                comp.Current == null || comp.Current.research.IsFinished);

            foreach (ArtificerOutput output in comp.Props.outputs)
            {
                if (output.research != null && !output.research.IsFinished)
                {
                    Find.ResearchManager.FinishProject(
                        output.research, doCompletionDialog: false, doCompletionLetter: false);
                }
            }

            ThingDef painful = BT_BallistaDefOf.BT_BoltPainful;
            fuel.Refuel(2f);
            comp.Select(painful);
            int before = TotalBolts(map, painful);
            comp.FinishUnit();
            Check("one unit becomes ten painful bolts",
                TotalBolts(map, painful) - before == 10 && fuel.Fuel == 1f,
                $"bolts +{TotalBolts(map, painful) - before}, fuel {fuel.Fuel}");

            Tick(comp.Props.ticksPerUnit + 10);
            Check("it works the next unit on its own",
                TotalBolts(map, painful) - before == 20 && !fuel.HasFuel,
                $"bolts +{TotalBolts(map, painful) - before}, fuel {fuel.Fuel}");
        }

        /// <summary>Puts bolts straight into a bolt store.</summary>
        /// <param name="storage">The store.</param>
        /// <param name="bolt">The bolt type.</param>
        /// <param name="count">How many.</param>
        private static void AddBolts(BoltStorage storage, ThingDef bolt, int count)
        {
            Thing bolts = ThingMaker.MakeThing(bolt);
            bolts.stackCount = count;
            storage.Container.TryAdd(bolts);
        }

        /// <summary>
        /// Every bolt of a type on the map: loose stacks plus what Ballistas
        /// and magazines hold, so hauling between them doesn't change it.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="bolt">The bolt type.</param>
        /// <returns>The total.</returns>
        private static int TotalBolts(Map map, ThingDef bolt)
        {
            int loose = map.listerThings.ThingsOfDef(bolt).Sum(t => t.stackCount);
            int stored = map.listerThings.AllThings
                .Select(t => t is Building_BatshittiumBallista b ? b.InternalMagazine
                    : t is Building_BallistaMagazine m ? m.Storage : null)
                .Where(s => s != null)
                .Sum(s => s.CountOf(bolt));
            return loose + stored;
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
            Log.Message($"[BT-SMOKE] TIMING countdown+blast {countdownMs} ms; "
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
                Log.Message($"[BT-SMOKE] PASS {name}");
            }
            else
            {
                failed++;
                Log.Warning($"[BT-SMOKE] FAIL {name}: {detail}");
            }
        }
    }
}
