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
using BatshitTechnology.Core;
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

        /// <summary>Lets the showcase run its timed steps.</summary>
        public override void GameComponentTick() => Showcase.Tick();

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
            TestMinifiedDevicesTick(map, center);

            // Destructive tests from here on. Remove the other devices
            // quietly first: a 5% damage roll on the reactor would otherwise
            // set off its barrage in the middle of the replicator's blast.
            ClearDevicesExcept(map, replicator);
            TestDetonation(replicator);
            TestReactorBarrage(map, center);
            TestDamageTrigger(map, center);
            TestArmedDeviceDestroyed(map, center);
            TestSingularityGrenade(map, center);
            TestDeathBlasts(map, center);
            TestNoErrorsLogged();
        }

        /// <summary>
        /// Nothing in the whole run logged an error, or a warning about the
        /// mod or about statically held assets. Read from RimWorld's own log
        /// queue, because Player.log writes errors as plain lines with no
        /// level and no stack trace (a "Tried to destroy non-destroyable
        /// thing" error from clearing ground went unnoticed that way).
        /// </summary>
        private void TestNoErrorsLogged()
        {
            var bad = Log.Messages
                .Where(m => m.type == LogMessageType.Error
                    || (m.type == LogMessageType.Warning
                        && (m.text.Contains("StaticConstructorOnStartup")
                            || m.text.Contains("BatshitTechnology") || m.text.Contains("BT_"))))
                .ToList();
            foreach (LogMessage m in bad)
            {
                string firstLine = m.text.Split('\n')[0];
                Log.Message($"[BT-SMOKE] LOGGED {m.type}: {firstLine}");
            }

            Check("nothing logged an error or a mod warning", bad.Count == 0,
                $"{bad.Count} logged (listed above)");
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

            // A unit burns for a random 0.1 to 36 hours, and the tests before
            // this one run for a couple of in-game hours, so about one run in
            // twenty the first unit has already burned out. Top up first.
            fuel.Refuel(fuel.Props.fuelCapacity - fuel.Fuel);
            Tick(1);

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
            var comp = replicator.TryGetComp<CompSelfDestruct>();
            comp.Arm();
            Tick(30);
            Check("arming starts the countdown", comp.IsCountingDown);
            comp.Abort();
            Check("aborting stops the countdown", !comp.IsCountingDown);
        }

        /// <summary>
        /// Halfway through a pull: the wall is down, the stool is loose, and
        /// the steel and colonist have been dragged in.
        /// </summary>
        /// <param name="field">The singularity.</param>
        /// <param name="wall">A wall in its reach.</param>
        /// <param name="stool">A stool in its reach.</param>
        /// <param name="steel">A steel stack in its reach.</param>
        /// <param name="pawn">A colonist in its reach, or null.</param>
        /// <param name="eye">Its centre.</param>
        /// <param name="steelBefore">The steel's starting distance.</param>
        /// <param name="pawnBefore">The colonist's starting distance.</param>
        private void CheckRipAndDrag(
            SingularityField field, Thing wall, Thing stool, Thing steel, Pawn pawn,
            IntVec3 eye, float steelBefore, float pawnBefore)
        {
            Check("the singularity rips a wall down and a stool loose",
                wall.Destroyed && !stool.Spawned && field.TornDown >= 1 && field.Uninstalled >= 1,
                $"wall gone {wall.Destroyed}, stool spawned {stool.Spawned}, "
                    + $"torn down {field.TornDown}, uninstalled {field.Uninstalled}");
            float steelAfter = steel.Spawned ? steel.Position.DistanceTo(eye) : 0f;
            float pawnAfter = pawn != null && pawn.Spawned ? pawn.Position.DistanceTo(eye) : 0f;
            Check("the singularity drags items and pawns in",
                steelAfter < steelBefore - 2f && (pawn == null || (pawnAfter < pawnBefore - 2f
                    && field.CaughtCount >= 1)),
                $"steel {steelBefore:F1}->{steelAfter:F1}, pawn {pawnBefore:F1}->{pawnAfter:F1}, "
                    + $"caught {field.CaughtCount}");
        }

        /// <summary>
        /// Uninstalled devices tick inside their packages in 1.6, off the
        /// map. Each device, fuelled where it takes fuel, is minified and
        /// ticked past the reactor's heat interval without logging an error.
        /// (Found in play: a reactor ripped loose by a singularity threw a
        /// NullReferenceException from its heat code every 250 ticks.)
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestMinifiedDevicesTick(Map map, IntVec3 center)
        {
            IntVec3 at = Clamp(map, center + new IntVec3(30, 0, -30));
            PrepareGround(map, CellRect.CenteredOn(at, 18));
            var packages = new List<Thing>();
            string[] devices =
            {
                "BT_BatshittiumReactor", "BT_BatshittiumArtificer", "BT_RandomReplicator",
                "BT_BatshittiumTransmogrifier", "BT_BrainSqueezer", "BT_BatshittiumBallista",
                "BT_BallistaMagazine",
            };
            for (int i = 0; i < devices.Length; i++)
            {
                // Each on its own spot: spawning a building wipes any item
                // under it, packages included.
                IntVec3 spot = at + new IntVec3((i - 3) * 5, 0, 0);
                Thing device = Spawn(map, devices[i], spot);
                device.TryGetComp<CompRefuelable>()?.Refuel(2f);
                Tick(2);
                MinifiedThing package = device.MakeMinified();
                GenPlace.TryPlaceThing(package, spot, map, ThingPlaceMode.Near);
                packages.Add(package);
            }

            var mark = ErrorMark();
            Tick(600);
            Check("uninstalled devices tick in their packages without errors",
                !NewErrorSince(mark) && packages.All(t => t.Spawned),
                $"error logged {NewErrorSince(mark)}, packages on the map "
                    + $"{packages.Count(t => t.Spawned)}/{packages.Count}");
            foreach (Thing package in packages)
            {
                package.Destroy(DestroyMode.Vanish);
            }
        }

        /// <summary>The newest logged error, and how many times it has repeated.</summary>
        /// <returns>A mark to compare against later.</returns>
        private static (LogMessage Last, int Repeats) ErrorMark()
        {
            LogMessage last = Log.Messages.LastOrDefault(m => m.type == LogMessageType.Error);
            return (last, last?.repeats ?? 0);
        }

        /// <summary>Whether an error has been logged since a mark.</summary>
        /// <param name="mark">From <see cref="ErrorMark"/>.</param>
        /// <returns>True if a new error (or a repeat) was logged.</returns>
        private static bool NewErrorSince((LogMessage Last, int Repeats) mark)
        {
            var now = ErrorMark();
            return now.Last != mark.Last || now.Repeats != mark.Repeats;
        }

        /// <summary>
        /// How much of each item's def lies within 12 tiles of a spot, counting
        /// stacks merged into others.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="at">The spot.</param>
        /// <param name="tracked">The items whose defs to count.</param>
        /// <returns>Total stack count per def.</returns>
        private static Dictionary<ThingDef, int> ItemAmounts(Map map, IntVec3 at, List<Thing> tracked)
        {
            var defs = tracked.Select(t => t.def).Distinct().ToList();
            return defs.ToDictionary(
                d => d,
                d => map.listerThings.ThingsOfDef(d)
                    .Where(t => t.Spawned && t.Position.InHorDistOf(at, 12f))
                    .Sum(t => t.stackCount));
        }

        /// <summary>
        /// Removes every Batshit device but one without triggering anything
        /// (Vanish sets off no self-destruct or death blast).
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="keep">The device to leave.</param>
        private static void ClearDevicesExcept(Map map, Thing keep)
        {
            foreach (Thing t in map.listerThings.AllThings
                .Where(t => t != keep && t.Spawned
                    && (t.TryGetComp<CompSelfDestruct>() != null
                        || t.TryGetComp<CompDeathBlast>() != null))
                .ToList())
            {
                t.Destroy(DestroyMode.Vanish);
            }
        }

        /// <summary>
        /// A full countdown destroys the replicator; fires survive the blast
        /// wave out to its edge, and secondary explosions go off inside the
        /// circle until the aftershocks end. Also times the blast and its
        /// aftermath, as a performance check.
        /// </summary>
        /// <param name="replicator">The replicator.</param>
        private void TestDetonation(Thing replicator)
        {
            Map map = replicator.Map;
            IntVec3 center = replicator.Position;
            var comp = replicator.TryGetComp<CompSelfDestruct>();
            var props = comp.Props;
            var bomb = (Detonation_Bomb)props.detonation;
            var aftermath = map.GetComponent<MapComponent_SelfDestructAftermath>();
            int firedBefore = aftermath.SecondariesFired;
            comp.Arm();
            var watch = Stopwatch.StartNew();
            Tick(Mathf.RoundToInt(props.countdownSeconds * 60f) + 10);
            long countdownMs = watch.ElapsedMilliseconds;
            Check("the countdown ends in detonation", replicator.Destroyed);
            Check("an aftermath follows the blast", aftermath.Active);

            // Before v0.8 the wave put out every fire lit at detonation.
            watch.Restart();
            int waveTicks = BlastSchedule.WaveTicks(props.blastRadius, 1d);
            Tick(waveTicks);
            float half = props.blastRadius / 2f;
            var fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire)
                .Where(f => f.Position.InHorDistOf(center, props.blastRadius))
                .ToList();
            int outerFires = fires.Count(f => !f.Position.InHorDistOf(center, half));
            Check("fires survive the shockwave out to its edge",
                outerFires >= 100,
                $"fires in radius {fires.Count}, beyond half radius {outerFires}");

            int aftershockTicks = Mathf.RoundToInt(bomb.aftershockSeconds * 60f);
            Tick(aftershockTicks + 30);
            long aftermathMs = watch.ElapsedMilliseconds;
            int fired = aftermath.SecondariesFired - firedBefore;
            int expected = Mathf.FloorToInt(
                bomb.secondariesPerSecond * (waveTicks + aftershockTicks) / 60f);
            Check("secondary explosions go off inside the blast circle",
                fired >= expected * 0.8f
                    && aftermath.MaxSecondaryDistance <= props.blastRadius + 1f,
                $"fired {fired} of ~{expected}, farthest {aftermath.MaxSecondaryDistance:F1}");
            Check("the aftermath ends", !aftermath.Active);

            watch.Restart();
            Tick(600);
            Log.Message($"[BT-SMOKE] TIMING countdown+blast {countdownMs} ms; "
                + $"wave+aftershocks {aftermathMs} ms ({fired} secondaries, "
                + $"{aftermath.FiresStarted} fires); "
                + $"next 600 ticks of fire {watch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// The reactor's self-destruct: the countdown destroys it, every
        /// volley of bolts is launched inside the blast radius, and the
        /// barrage ends. Times it, as a performance check.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">Where to build the reactor.</param>
        private void TestReactorBarrage(Map map, IntVec3 center)
        {
            PrepareGround(map, CellRect.CenteredOn(center, 3));
            Thing reactor = Spawn(map, "BT_BatshittiumReactor", center);
            var comp = reactor.TryGetComp<CompSelfDestruct>();
            var barrage = (Detonation_BoltBarrage)comp.Props.detonation;
            var aftermath = map.GetComponent<MapComponent_SelfDestructAftermath>();
            int launchedBefore = aftermath.BoltsLaunched;
            aftermath.MaxBoltDistance = 0f;

            comp.Arm();
            Tick(Mathf.RoundToInt(comp.Props.countdownSeconds * 60f) + 2);
            Check("the reactor's countdown ends in detonation", reactor.Destroyed);

            var watch = Stopwatch.StartNew();
            Tick((barrage.volleys - 1) * barrage.volleyIntervalTicks + 1);
            int launched = aftermath.BoltsLaunched - launchedBefore;
            int expected = barrage.volleys * barrage.boltsPerVolley;
            Check("the reactor launches every volley inside its blast radius",
                launched == expected
                    && aftermath.MaxBoltDistance <= comp.Props.blastRadius + 1f,
                $"launched {launched} of {expected}, farthest {aftermath.MaxBoltDistance:F1}");
            Tick(300); // let the last bolts land
            Check("the barrage ends", !aftermath.Active);
            Log.Message($"[BT-SMOKE] TIMING reactor barrage of {launched} bolts, "
                + $"launch to landing {watch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// Damage can trigger a self-destruct: hits on a reactor (1 damage
        /// each, so it survives) trigger it within a few hundred, and the
        /// result can still be aborted.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">Where to build the reactor.</param>
        private void TestDamageTrigger(Map map, IntVec3 center)
        {
            PrepareGround(map, CellRect.CenteredOn(center, 3));
            Thing reactor = Spawn(map, "BT_BatshittiumReactor", center);
            var comp = reactor.TryGetComp<CompSelfDestruct>();
            int before = CompSelfDestruct.DamageTriggers;
            int hits = 0;
            while (CompSelfDestruct.DamageTriggers == before && hits < 400)
            {
                reactor.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 1f));
                hits++;
            }

            Check("damage triggers the self-destruct",
                CompSelfDestruct.DamageTriggers > before && comp.IsCountingDown,
                $"{hits} hits, counting down {comp.IsCountingDown}");
            comp.Abort();
            reactor.Destroy(DestroyMode.Vanish);
        }

        /// <summary>
        /// An armed device destroyed by damage goes off instead of
        /// fizzling: a reactor killed mid-countdown launches its barrage.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">Where to build the reactor.</param>
        private void TestArmedDeviceDestroyed(Map map, IntVec3 center)
        {
            PrepareGround(map, CellRect.CenteredOn(center, 3));
            Thing reactor = Spawn(map, "BT_BatshittiumReactor", center);
            var comp = reactor.TryGetComp<CompSelfDestruct>();
            var barrage = (Detonation_BoltBarrage)comp.Props.detonation;
            var aftermath = map.GetComponent<MapComponent_SelfDestructAftermath>();
            int launchedBefore = aftermath.BoltsLaunched;
            comp.Arm();
            Tick(60);
            reactor.Kill();
            Tick((barrage.volleys - 1) * barrage.volleyIntervalTicks + 1);
            Check("an armed device destroyed by damage goes off",
                aftermath.BoltsLaunched - launchedBefore == barrage.volleys * barrage.boltsPerVolley,
                $"launched {aftermath.BoltsLaunched - launchedBefore}");
            Tick(300);
        }

        /// <summary>
        /// A thrown Ergonomic Singularity Grenade opens a singularity that
        /// rips a wall down and a stool loose, drags a steel stack and a
        /// colonist toward it, explodes on time, and leaves the colonist (if
        /// alive) with the ergonomic memory.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestSingularityGrenade(Map map, IntVec3 center)
        {
            IntVec3 at = Clamp(map, center + new IntVec3(-45, 0, -45));
            PrepareGround(map, CellRect.CenteredOn(at, 14));
            Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
            steel.stackCount = 30;
            GenSpawn.Spawn(steel, at + new IntVec3(8, 0, 0), map);

            // A spread of different items, plus a second steel stack to
            // merge, all drawn toward the same pile. Before v0.8's fix,
            // items pulled into full cells shoved their occupants aside (or
            // destroyed them), churning a line of items beside the centre.
            var loose = new List<Thing> { steel };
            foreach (var (def, offset) in new[]
            {
                (ThingDefOf.WoodLog, new IntVec3(-8, 0, 1)), (ThingDefOf.Plasteel, new IntVec3(6, 0, 5)),
                (ThingDefOf.ComponentIndustrial, new IntVec3(-3, 0, 8)), (ThingDefOf.Silver, new IntVec3(4, 0, -7)),
                (ThingDefOf.Gold, new IntVec3(-6, 0, -6)), (ThingDefOf.Cloth, new IntVec3(9, 0, 3)),
                (ThingDefOf.MedicineHerbal, new IntVec3(1, 0, -9)), (ThingDefOf.Steel, new IntVec3(7, 0, -1)),
            })
            {
                Thing item = ThingMaker.MakeThing(def);
                item.stackCount = Mathf.Min(10, def.stackLimit);
                loose.Add(GenSpawn.Spawn(item, at + offset, map));
            }

            Dictionary<ThingDef, int> amounts = ItemAmounts(map, at, loose);
            Thing wall = GenSpawn.Spawn(
                ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), at + new IntVec3(0, 0, 6), map);
            Thing stool = GenSpawn.Spawn(
                ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Stool"), ThingDefOf.WoodLog),
                at + new IntVec3(-5, 0, -4), map);
            Pawn pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Dead && !p.Downed);
            if (pawn != null)
            {
                pawn.jobs.StopAll();
                pawn.Position = at + new IntVec3(-7, 0, 3);
                pawn.Notify_Teleported();
                pawn.drafter.Drafted = true; // stand still until the grenade lands
            }

            IntVec3 from = at + new IntVec3(11, 0, -2);
            var grenade = (Projectile)GenSpawn.Spawn(
                DefDatabase<ThingDef>.GetNamed("BT_Projectile_SingularityGrenade"), from, map);
            grenade.Launch(null, from.ToVector3Shifted(), at, at, ProjectileHitFlags.IntendedTarget);
            SingularityField field = null;
            for (int i = 0; i < 300 && field == null; i++)
            {
                Tick(1);
                field = map.listerThings.ThingsOfDef(BT_SingularityDefOf.BT_SingularityField)
                    .OfType<SingularityField>().FirstOrDefault();
            }

            Check("a thrown singularity grenade opens a singularity", field != null);
            if (field == null)
            {
                return;
            }

            IntVec3 eye = field.Position;
            float steelBefore = steel.Position.DistanceTo(eye);
            float pawnBefore = pawn?.Position.DistanceTo(eye) ?? 0f;
            var nearest = loose.ToDictionary(t => t, t => t.Position.DistanceTo(eye));
            int pushedAway = 0;
            int watchTicks = field.Settings.pullTicks - 10; // to just before the explosion
            for (int i = 0; i < watchTicks; i++)
            {
                Tick(1);
                foreach (Thing t in loose.Where(t => t.Spawned))
                {
                    float d = t.Position.DistanceTo(eye);
                    if (d > nearest[t] + 0.01f)
                    {
                        pushedAway++;
                    }

                    nearest[t] = Mathf.Min(nearest[t], d);
                }

                if (i == 149)
                {
                    CheckRipAndDrag(field, wall, stool, steel, pawn, eye, steelBefore, pawnBefore);
                }
            }

            Check("pulled items never get shoved away from the singularity",
                pushedAway == 0, $"{pushedAway} moves away from the centre");
            Dictionary<ThingDef, int> after = ItemAmounts(map, at, loose);
            Check("pulled items are merged or piled, never lost",
                amounts.All(kv => after.TryGetValue(kv.Key, out int n) && n >= kv.Value), // the torn-down wall adds wood
                string.Join(", ", amounts.Select(kv =>
                    $"{kv.Key.defName} {kv.Value}->{(after.TryGetValue(kv.Key, out int n) ? n : 0)}")));

            int comforted = SingularityField.SurvivorsComforted;
            Tick(60);
            Check("the singularity explodes and closes", field.Destroyed && field.Exploded);
            bool thought = pawn?.needs?.mood?.thoughts?.memories?.Memories
                .Any(m => m.def.defName == "BT_ErgonomicallySingularized") ?? false;
            Check("caught survivors feel ergonomically singularized",
                pawn == null || pawn.Dead || (thought && SingularityField.SurvivorsComforted > comforted),
                $"dead {pawn?.Dead}, thought {thought}");
        }

        /// <summary>
        /// Devices without a self-destruct each set off their own blast when
        /// destroyed: the Transmogrifier spews rock, the Brain Squeezer
        /// stuns and squeezes a nearby colonist, a Ballista and a Magazine
        /// cook off their bolts (none spilled), and the Artificer sprays
        /// half-made bolts. Spaced well apart so nothing hits the next one.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestDeathBlasts(Map map, IntVec3 center)
        {
            var aftermath = map.GetComponent<MapComponent_SelfDestructAftermath>();
            ThingDef explosive = BoltDef("BT_BoltReallyPainful");

            // Transmogrifier: rock chunks thrown out.
            IntVec3 at = Clamp(map, center + new IntVec3(-45, 0, 0));
            PrepareGround(map, CellRect.CenteredOn(at, 10));
            int chunksBefore = Chunks(map, at);
            KillAndCheck(map, aftermath, "BT_BatshittiumTransmogrifier", at,
                "the transmogrifier spews rock when destroyed",
                () => Chunks(map, at) - chunksBefore >= 3,
                () => $"chunks +{Chunks(map, at) - chunksBefore}");

            // Brain Squeezer: a colonist nearby is stunned.
            at = Clamp(map, center + new IntVec3(45, 0, 0));
            PrepareGround(map, CellRect.CenteredOn(at, 10));
            Pawn pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Dead && !p.Downed);
            if (pawn != null)
            {
                pawn.jobs.StopAll();
                pawn.Position = at + new IntVec3(5, 0, 0);
                pawn.Notify_Teleported();
            }

            KillAndCheck(map, aftermath, "BT_BrainSqueezer", at,
                "the brain squeezer squeezes nearby brains when destroyed",
                () => pawn == null || pawn.stances.stunner.Stunned,
                () => pawn == null ? "no colonist" : $"stunned {pawn.stances.stunner.Stunned}");

            // Ballista: its internal magazine cooks off.
            at = Clamp(map, center + new IntVec3(0, 0, 45));
            PrepareGround(map, CellRect.CenteredOn(at, 10));
            int launched = aftermath.BoltsLaunched;
            int loose = LooseBolts(map); // earlier tests may have left some
            KillAndCheck(map, aftermath, "BT_BatshittiumBallista", at,
                "a destroyed ballista's bolts cook off",
                () => aftermath.BoltsLaunched - launched == 16 && LooseBolts(map) <= loose,
                () => $"launched {aftermath.BoltsLaunched - launched}, loose {loose}->{LooseBolts(map)}",
                t => AddBolts(((Building_BatshittiumBallista)t).InternalMagazine, explosive, 16));

            // Magazine: up to the cap cooks off, the rest are pulverized.
            at = Clamp(map, center + new IntVec3(0, 0, -45));
            PrepareGround(map, CellRect.CenteredOn(at, 10));
            launched = aftermath.BoltsLaunched;
            loose = LooseBolts(map);
            KillAndCheck(map, aftermath, "BT_BallistaMagazine", at,
                "a destroyed magazine cooks off its cap and pulverizes the rest",
                () => aftermath.BoltsLaunched - launched == 160 && LooseBolts(map) <= loose,
                () => $"launched {aftermath.BoltsLaunched - launched}, loose {loose}->{LooseBolts(map)}",
                t => AddBolts(((Building_BallistaMagazine)t).Storage, explosive, 300));

            // Artificer: a few half-made bolts fly.
            at = Clamp(map, center + new IntVec3(45, 0, 45));
            PrepareGround(map, CellRect.CenteredOn(at, 10));
            launched = aftermath.BoltsLaunched;
            KillAndCheck(map, aftermath, "BT_BatshittiumArtificer", at,
                "a destroyed artificer sprays half-made bolts",
                () => aftermath.BoltsLaunched - launched >= 3,
                () => $"launched {aftermath.BoltsLaunched - launched}");
            Tick(240); // let everything land
        }

        /// <summary>
        /// Spawns a device, optionally loads it, kills it, and checks that
        /// exactly one death blast went off plus the given condition.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="aftermath">The aftermath component, for its counters.</param>
        /// <param name="defName">The device.</param>
        /// <param name="at">Where to put it.</param>
        /// <param name="name">The check's name.</param>
        /// <param name="ok">The device-specific condition.</param>
        /// <param name="detail">What was observed, for failures.</param>
        /// <param name="load">Fills the device before it dies, or null.</param>
        private void KillAndCheck(
            Map map, MapComponent_SelfDestructAftermath aftermath, string defName,
            IntVec3 at, string name, Func<bool> ok, Func<string> detail,
            Action<Thing> load = null)
        {
            Thing device = Spawn(map, defName, at);
            load?.Invoke(device);
            int blasts = aftermath.DeathBlasts;
            device.Kill();
            Tick(2);
            Check(name, aftermath.DeathBlasts == blasts + 1 && ok(),
                $"death blasts +{aftermath.DeathBlasts - blasts}; {detail()}");
        }

        /// <summary>A bolt item def by name.</summary>
        /// <param name="defName">The def name.</param>
        /// <returns>The def.</returns>
        private static ThingDef BoltDef(string defName) =>
            DefDatabase<ThingDef>.GetNamed(defName);

        /// <summary>Rock chunks within 12 tiles of a spot.</summary>
        /// <param name="map">The map.</param>
        /// <param name="at">The spot.</param>
        /// <returns>How many.</returns>
        private static int Chunks(Map map, IntVec3 at) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.Chunk)
                .Count(t => t.Position.InHorDistOf(at, 12f));

        /// <summary>Bolt items lying loose on the map.</summary>
        /// <param name="map">The map.</param>
        /// <returns>How many bolts.</returns>
        private static int LooseBolts(Map map) =>
            map.listerThings.AllThings
                .Where(t => t.def.GetModExtension<BoltExtension>() != null && t.Spawned)
                .Sum(t => t.stackCount);

        /// <summary>Keeps a cell 12 tiles inside the map's edges.</summary>
        /// <param name="map">The map.</param>
        /// <param name="c">The cell.</param>
        /// <returns>The clamped cell.</returns>
        private static IntVec3 Clamp(Map map, IntVec3 c) =>
            new IntVec3(
                Mathf.Clamp(c.x, 12, map.Size.x - 13), 0,
                Mathf.Clamp(c.z, 12, map.Size.z - 13));

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
                    // Steam geysers and the like can't be destroyed; trying
                    // logs an error.
                    if (!(t is Pawn) && t.def.destroyable)
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
