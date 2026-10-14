// Batshit Technology - in-game smoke test.
// Runs only when RimWorld starts with -btsmoketest (Tools/smoke-test.ps1
// passes it with -quicktest). Builds each device on the quick-test map,
// exercises it with real game ticks (and builds, checks and demolishes
// AnyRim, the test town, before the destructive tests), logs
// "[BT-SMOKE] PASS/FAIL" lines and
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
using Verse.AI.Group;
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

        /// <summary>
        /// How far from its household's home (its lord's duty cell) an
        /// AnyRim resident may be after 600 ticks: the duty's 16-tile flag
        /// radius plus 4 for pathing around walls.
        /// </summary>
        private const float AnyRimHomeReach = 20f;

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
            SilenceStoryteller(map);
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
            TestAnyRim(map); // builds the test town, checks it, takes it down

            // Destructive tests from here on. Remove the other devices
            // quietly first: a 5% damage roll on the reactor would otherwise
            // set off its barrage in the middle of the replicator's blast.
            ClearDevicesExcept(map, replicator);
            TestDetonation(replicator);
            TestReactorBarrage(map, center);
            TestDamageTrigger(map, center);
            TestArmedDeviceDestroyed(map, center);
            TestSingularityGrenade(map, center);
            QuietTheMap(map);
            TestImplosionGathers(map, center);
            TestTractorBomb(map, center);
            TestDeathBlasts(map, center);
            TestNoErrorsLogged();
        }

        /// <summary>
        /// Stops the storyteller for the rest of the run, so no raid or
        /// other incident lands in the middle of a check, and notes any
        /// hostiles already on the map. A raid on one random quick-test map
        /// (2026-10-13) sent pirates pathing round a 325-tile Ballista,
        /// which logged an avoid-grid error, and was the likely reason the
        /// Artificer's bolts went missing. Quick-test maps are throwaway.
        /// </summary>
        /// <param name="map">The quick-test map.</param>
        private static void SilenceStoryteller(Map map)
        {
            int hostiles = map.mapPawns.AllPawnsSpawned
                .Count(p => p.HostileTo(Faction.OfPlayer));
            Storyteller teller = Find.Storyteller;
            teller?.storytellerComps?.Clear();
            teller?.incidentQueue?.Clear();
            Log.Message("[BT-SMOKE] NOTE storyteller silenced for the run; "
                + $"{hostiles} hostile pawns on the map at the start");
        }

        /// <summary>
        /// Calms the map before a check that something settles: every fire
        /// put out, no hostiles, and every colonist drafted and holding
        /// fire where it stands, so nobody fights fires or wanders through.
        /// On one map with 1,920 fires an implosion never settled within
        /// its cap, most likely because colonists and animals kept walking
        /// into its reach.
        /// </summary>
        /// <param name="map">The map.</param>
        private static void QuietTheMap(Map map)
        {
            foreach (Thing fire in map.listerThings.ThingsOfDef(ThingDefOf.Fire)
                .ToList())
            {
                fire.Destroy();
            }

            foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p.HostileTo(Faction.OfPlayer))
                {
                    p.Destroy(DestroyMode.Vanish);
                }
                else if (p.IsColonist && p.drafter != null && !p.Dead)
                {
                    p.jobs?.StopAll();
                    p.drafter.Drafted = true;
                    p.drafter.FireAtWill = false;
                }
            }
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
                $"colonist at {pawn.Position}, squeezer at {squeezer.Position}; "
                    + $"job now {pawn.CurJobDef?.defName ?? "none"}, downed "
                    + $"{pawn.Downed}, mental state {pawn.MentalStateDef?.defName}, "
                    + $"drafted {pawn.Drafted}, can reach "
                    + $"{pawn.CanReach(squeezer, PathEndMode.OnCell, Danger.Deadly)}, "
                    + $"reserved by {map.reservationManager.FirstRespectedReserver(squeezer, pawn)?.LabelShort ?? "nobody"}");
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

            // Raiders path round dangerous turrets on an avoid grid that
            // spans the turret's range plus 4, which cannot pass the
            // engine's ~79.8-tile radial limit: "Not enough squares to get
            // to radius 329", seen when a raid met a Ballista set to 325.
            // The grid regenerates whenever raiders path; force it here.
            var avoidMark = ErrorMark();
            map.avoidGrid.Regenerate();
            Check("raiders' avoid grid survives a 325-tile Ballista",
                !NewErrorSince(avoidMark), "an error was logged (see the log)");
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

            // Haulers carry the bolts off to the Ballista and its magazines
            // as they appear (a run saw +0 when one held all twenty at the
            // check), and the Ballista fires painful bolts at anything
            // hostile in range; count both.
            int firedBefore = Building_BatshittiumBallista.BoltsFired;
            Tick(comp.Props.ticksPerUnit + 10);
            int fired = Building_BatshittiumBallista.BoltsFired - firedBefore;
            int made = TotalBolts(map, painful) + fired - before;
            Check("it works the next unit on its own",
                made == 20 && !fuel.HasFuel,
                $"bolts +{made} ({CarriedBolts(map, painful)} carried, "
                    + $"{fired} fired), fuel {fuel.Fuel}");
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
        /// Every bolt of a type on the map: loose stacks, what Ballistas and
        /// magazines hold, and what pawns are carrying or have pocketed, so
        /// hauling between them doesn't change it.
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
            return loose + stored + CarriedBolts(map, bolt);
        }

        /// <summary>
        /// Bolts of a type that pawns on the map are carrying or have in
        /// their inventories.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="bolt">The bolt type.</param>
        /// <returns>How many.</returns>
        private static int CarriedBolts(Map map, ThingDef bolt) =>
            map.mapPawns.AllPawnsSpawned.Sum(p =>
                (p.carryTracker?.CarriedThing?.def == bolt
                    ? p.carryTracker.CarriedThing.stackCount : 0)
                + (p.inventory?.innerContainer?.TotalStackCountOfDef(bolt) ?? 0));

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
                // Forbidden, so no hauler carries one off mid-test (a run
                // saw 6 of 7 on the map: one was in a colonist's hands).
                package.SetForbidden(true, warnOnFail: false);
                packages.Add(package);
            }

            var mark = ErrorMark();
            Tick(600);
            Check("uninstalled devices tick in their packages without errors",
                !NewErrorSince(mark) && packages.All(t => t.Spawned),
                $"error logged {NewErrorSince(mark)}, packages on the map "
                    + $"{packages.Count(t => t.Spawned)}/{packages.Count}; missing: "
                    + string.Join(", ", packages.Where(t => !t.Spawned).Select(t =>
                        $"{t.LabelCap} (destroyed {t.Destroyed}, held by "
                        + $"{t.ParentHolder?.GetType().Name ?? "nothing"})")));
            foreach (Thing package in packages)
            {
                package.Destroy(DestroyMode.Vanish);
            }
        }

        /// <summary>
        /// AnyRim (docs/SHOWCASE.md, "Tests"): the town builds at a scored
        /// site with a clean report, then is checked and taken down again
        /// before the destructive tests, whose blasts would reach it.
        /// Nothing in the whole test may log an error.
        /// </summary>
        /// <param name="map">The quick-test map.</param>
        private void TestAnyRim(Map map)
        {
            var mark = ErrorMark();
            AnyRimBuildReport report;
            try
            {
                // The showcase's windows first, then anywhere on the map:
                // this test is about the town, and an infested north-west
                // (one map had an insect hive there) is the showcase's
                // documented reason to skip it, not a fault.
                var windows = Showcase.AnyRimWindows(map);
                windows.Add(CellRect.WholeMap(map));
                report = new AnyRimBuildReport();
                foreach (CellRect window in windows)
                {
                    report = AnyRim.BuildBest(
                        map, AnyRim.DefaultSeed, window,
                        Showcase.AnyRimPreferredSite(map), Showcase.AnyRimKeepAways(map.Center));
                    if (report.Anchor.IsValid)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Check("AnyRim builds", false, e.ToString());
                return;
            }

            MapComponent_AnyRim record = map.GetComponent<MapComponent_AnyRim>();
            bool built = report.Anchor.IsValid && record != null && record.Built;
            Check("AnyRim builds at a scored site", built, "no site qualified");
            if (!built)
            {
                return;
            }

            // Checked against what was built: the plan reduced around any
            // indestructible things, not the full plan.
            TownPlan plan = report.Plan ?? record.Plan;
            if (plan == null)
            {
                Check("AnyRim's executed plan is known", false, "neither the report nor the record has it");
                AnyRim.Demolish(map);
                return;
            }

            Log.Message(
                $"[BT-SMOKE] AnyRim at ({report.Anchor.x},{report.Anchor.z}): {report.Summary()}");
            Check("AnyRim build report is clean", report.Clean,
                string.Join("; ", report.Failures.Take(5)));
            var residents = record.Residents.ToList();
            var animals = record.Animals.ToList();
            try
            {
                CheckAnyRimBuilt(map, plan, report.Anchor, record);
                CheckAnyRimLiving(map, plan, report.Anchor, record);
            }
            catch (Exception e)
            {
                Check("AnyRim checks ran without an exception", false, e.ToString());
            }
            finally
            {
                CheckAnyRimDemolished(map, plan, record, residents.Concat(animals).ToList());
            }

            Check("AnyRim logged no errors", !NewErrorSince(mark), "an error was logged (see above)");
        }

        /// <summary>
        /// The freshly built town: every placement where planned, enclosed
        /// roofed rooms reachable from the street, no roof collapse (none
        /// pending before any tick, and every roofed cell nearby still
        /// roofed after two), a bare firebreak, powered devices (the town
        /// reactor refuelled first), and no room in the reactor's heat
        /// ring.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="plan">The plan built (reduced around obstacles).</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <param name="record">The town record.</param>
        private void CheckAnyRimBuilt(Map map, TownPlan plan, IntVec3 anchor, MapComponent_AnyRim record)
        {
            // Before any tick: a mark left by the build drops on the first
            // one (MapPreTick), and a thin roof leaves no rubble behind.
            CellRect nearby = record.LevelledArea.ExpandedBy(8).ClipInsideMap(map);
            int pending = map.roofCollapseBuffer.CellsMarkedToCollapse.Count(nearby.Contains);
            bool cold = map.mapTemperature.OutdoorTemp < AnyRim.ColdBelowCelsius;
            var missing = plan.Placements
                .Where(p => cold || !p.OnlyIfCold)
                .Where(p => !SpawnedAsPlanned(map, anchor, p, record))
                .Select(p => $"{p.Def} at {p.At}")
                .ToList();
            Check("every AnyRim placement spawned where planned", missing.Count == 0,
                $"{missing.Count} missing, e.g. {string.Join(", ", missing.Take(5))}");

            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            var badRooms = new List<string>();
            foreach (BuildingPlan building in plan.Buildings.Where(b => b.Enclosed))
            {
                foreach (Box box in building.Rooms)
                {
                    string problem = RoomProblem(map, anchor, building, box);
                    if (problem != null)
                    {
                        badRooms.Add($"{building.Key} {box}: {problem}");
                    }
                }
            }

            Check("AnyRim's rooms are enclosed, roofed and reachable from the street",
                badRooms.Count == 0, string.Join("; ", badRooms.Take(5)));

            List<IntVec3> roofed = nearby.Cells.Where(c => c.Roofed(map)).ToList();
            Tick(2);
            CellRect footprint = record.Bounds;
            List<IntVec3> lost = roofed.Where(c => !c.Roofed(map)).ToList();
            int collapsed = map.listerThings.ThingsOfDef(ThingDefOf.CollapsedRocks)
                .Count(t => nearby.Contains(t.Position));
            Check("no roof collapses around AnyRim",
                pending == 0 && lost.Count == 0 && collapsed == 0,
                $"{pending} cells marked before any tick, {lost.Count} of {roofed.Count} roofed cells "
                    + $"lost in two ticks (e.g. {string.Join(", ", lost.Take(3))}), "
                    + $"{collapsed} collapsed rocks");

            // The firebreak: cleared ground the town does not build on.
            var partCells = new HashSet<Cell>(plan.Parts.SelectMany(p => p.Cells()));
            int plantsInFirebreak = plan.ClearedCells()
                .Where(c => !partCells.Contains(c))
                .Select(c => AnyRim.ToMap(anchor, c))
                .Where(c => c.InBounds(map))
                .Count(c => c.GetPlant(map) != null);
            Check("AnyRim's firebreak holds no plants", plantsInFirebreak == 0,
                $"{plantsInFirebreak} plants");

            Thing reactor = plan.Reactor.HasValue
                ? AnyRim.ToMap(anchor, plan.Reactor.Value).GetThingList(map)
                    .FirstOrDefault(t => t.def.defName == "BT_BatshittiumReactor")
                : null;
            CompRefuelable fuel = reactor?.TryGetComp<CompRefuelable>();
            if (fuel != null && fuel.Fuel < fuel.Props.fuelCapacity)
            {
                fuel.Refuel(fuel.Props.fuelCapacity);
            }

            Thing[] consumers = map.listerBuildings.allBuildingsColonist
                .Concat(map.listerBuildings.allBuildingsNonColonist)
                .Where(b => footprint.Contains(b.Position) && FlickUtility.WantsToBeOn(b)
                    && b.TryGetComp<CompPowerTrader>() != null
                    && !(b.TryGetComp<CompPowerTrader>() is CompPowerPlant))
                .Cast<Thing>()
                .ToArray();
            if (consumers.Length > 0)
            {
                WaitForPower(consumers);
            }

            var unpowered = consumers.Where(t => !t.TryGetComp<CompPowerTrader>().PowerOn).ToList();
            Check("AnyRim's devices and lamps are powered", consumers.Length > 0 && unpowered.Count == 0,
                $"{unpowered.Count} of {consumers.Length} unpowered, e.g. "
                    + string.Join(", ", unpowered.Take(3).Select(t => $"{t.def.defName} ({PowerDetail(t)})")));

            if (reactor != null)
            {
                var heated = GenRadial.RadialCellsAround(reactor.Position, 12f, true)
                    .Where(c => c.InBounds(map))
                    .Select(c => c.GetRoom(map))
                    .Where(r => r != null && !r.UsesOutdoorTemperature)
                    .Distinct()
                    .Count();
                Check("no room within the town reactor's heat ring", heated == 0, $"{heated} rooms");
            }
        }

        /// <summary>
        /// The residents: eight named non-colonists, each in a lord, each
        /// still within <see cref="AnyRimHomeReach"/> tiles of its own
        /// household's home after 600 ticks; then the census drops by one
        /// when one is killed, and the incident clock restarts.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="plan">The plan built.</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <param name="record">The town record.</param>
        private void CheckAnyRimLiving(Map map, TownPlan plan, IntVec3 anchor, MapComponent_AnyRim record)
        {
            List<Pawn> residents = record.Residents.ToList();
            var nicks = plan.Residents.Select(r => r.Nick).ToList();
            bool named = residents.Count == plan.Residents.Count
                && residents.Select(p => (p.Name as NameTriple)?.Nick).SequenceEqual(nicks);
            Check("AnyRim has its eight named residents",
                plan.Residents.Count == 8 && named,
                $"{residents.Count} residents: {string.Join(", ", residents.Select(p => p.LabelShort))}");
            Check("AnyRim's residents are not colonists and each has a lord",
                residents.All(p => !p.IsColonist && p.Faction != Faction.OfPlayer && p.GetLord() != null),
                string.Join(", ", residents.Select(p => $"{p.LabelShort}: {p.Faction}, lord {p.GetLord() != null}")));

            Tick(600);
            var strays = new List<string>();
            foreach (Pawn p in residents)
            {
                var household = p.GetLord()?.LordJob as LordJob_AnyRimResident;
                if (p.Dead || !p.Spawned || household == null)
                {
                    strays.Add($"{p.LabelShort}: dead {p.Dead}, spawned {p.Spawned}, "
                        + $"household {household != null}");
                    continue;
                }

                float distance = p.Position.DistanceTo(household.Home);
                if (distance > AnyRimHomeReach)
                {
                    strays.Add($"{p.LabelShort} {distance:F1} tiles from home at {p.Position}");
                }
            }

            Check("AnyRim's residents are still in town after 600 ticks", strays.Count == 0,
                string.Join(", ", strays));

            Pawn victim = residents.FirstOrDefault(p => !p.Dead && p.Spawned);
            if (victim == null)
            {
                Check("AnyRim's census counts a death", false, "no living resident to test with");
                return;
            }

            int before = record.Population;
            int killedAt = Find.TickManager.TicksGame;
            victim.Kill(null);
            Tick(31); // one census interval
            Check("AnyRim's census drops by one when a resident dies",
                record.Population == before - 1 && record.LastIncidentTick >= killedAt,
                $"population {before} -> {record.Population}, last incident tick "
                    + $"{record.LastIncidentTick} (killed at {killedAt})");
        }

        /// <summary>
        /// Takes the town down and checks nothing is left: no town pawns or
        /// corpses, no destroyable thing on the ground it cleared, none of
        /// its stockpile zones, no household lord, and an empty record.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="plan">The plan built (reduced around obstacles).</param>
        /// <param name="record">The town record.</param>
        /// <param name="townPawns">The residents and animals, captured before.</param>
        private void CheckAnyRimDemolished(
            Map map, TownPlan plan, MapComponent_AnyRim record, List<Pawn> townPawns)
        {
            IntVec3 anchor = record.Anchor;
            List<IntVec3> cleared = AnyRim.ClearedCells(map, plan, anchor);
            List<IntVec3> stockpiles = plan.Stockpiles
                .SelectMany(s => AnyRim.ToMap(anchor, s.Area).ClipInsideMap(map).Cells)
                .ToList();
            bool removed = AnyRim.Demolish(map);
            var left = cleared
                .SelectMany(c => c.GetThingList(map))
                .Where(t => t.def.destroyable && !(t is Pawn))
                .Select(t => t.def.defName)
                .Distinct()
                .ToList();
            bool pawnsGone = townPawns.All(p => !p.SpawnedOrAnyParentSpawned
                && (p.Corpse == null || !p.Corpse.Spawned));
            bool lordsGone = !map.lordManager.lords.Any(l => l.LordJob is LordJob_AnyRimResident);
            bool zonesGone = stockpiles.All(c => map.zoneManager.ZoneAt(c) == null);
            Check("AnyRim demolition leaves nothing behind",
                removed && !record.Built && record.Population == 0 && left.Count == 0 && pawnsGone
                    && lordsGone && zonesGone,
                $"removed {removed}, built {record.Built}, population {record.Population}, "
                    + $"pawns gone {pawnsGone}, lords gone {lordsGone}, zones gone {zonesGone}, "
                    + $"left: {string.Join(", ", left.Take(8))}");
        }

        /// <summary>Whether a placement's thing is where the plan put it.</summary>
        /// <param name="map">The map.</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <param name="placement">The placement.</param>
        /// <param name="record">The town record, for animals.</param>
        /// <returns>True if found.</returns>
        private static bool SpawnedAsPlanned(
            Map map, IntVec3 anchor, Placement placement, MapComponent_AnyRim record)
        {
            if (placement.Kind == PlacementKind.Animal)
            {
                return record.Animals.Any(a => a.Spawned && a.kindDef.defName == placement.Def
                    && (placement.Name == null || a.Name?.ToStringShort == placement.Name));
            }

            IntVec3 cell = AnyRim.ToMap(anchor, placement.At);
            return cell.InBounds(map) && cell.GetThingList(map).Any(t =>
                placement.Kind == PlacementKind.Crate
                    ? t is MinifiedThing crate && crate.InnerThing?.def.defName == placement.Def
                    : t.def.defName == placement.Def && t.Position == cell);
        }

        /// <summary>
        /// What is wrong with one planned room, or null: it must be a room
        /// that does not use the outdoor temperature, roofed over its whole
        /// interior, and reachable through doors from the building's street
        /// cell.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <param name="building">The building.</param>
        /// <param name="interior">The room's interior, town-local.</param>
        /// <returns>The problem, or null.</returns>
        private static string RoomProblem(Map map, IntVec3 anchor, BuildingPlan building, Box interior)
        {
            CellRect rect = AnyRim.ToMap(anchor, interior);
            IntVec3 inside = rect.Cells.FirstOrDefault(c => c.InBounds(map) && c.Standable(map));
            if (!rect.Contains(inside) || !inside.Standable(map))
            {
                return "no standable cell";
            }

            Room room = inside.GetRoom(map);
            if (room == null || room.UsesOutdoorTemperature || room.TouchesMapEdge)
            {
                return "not enclosed";
            }

            if (rect.Cells.Any(c => !c.Roofed(map)))
            {
                return "not fully roofed";
            }

            if (building.StreetCell.HasValue && !map.reachability.CanReach(
                    AnyRim.ToMap(anchor, building.StreetCell.Value), inside, PathEndMode.OnCell,
                    TraverseParms.For(TraverseMode.PassDoors)))
            {
                return "unreachable from the street";
            }

            return null;
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
            // How much ground beyond half the radius can burn at all: on a
            // barren or rain-soaked quick-test map there may be little, and
            // the fire check scales to it (one map gave 35 fires in all).
            float halfRadius = props.blastRadius / 2f;
            int flammableOuter = GenRadial.RadialCellsAround(center, props.blastRadius, true)
                .Count(c => c.InBounds(map) && !c.InHorDistOf(center, halfRadius)
                    && FireUtility.ChanceToStartFireIn(c, map) > 0f);
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
            // Fires are lit on 80% of burnable cells; ask for 100, or a fifth
            // of the burnable ground where there is less than 500 cells of it.
            int wanted = Mathf.Min(100, flammableOuter / 5);
            Check("fires survive the shockwave out to its edge",
                outerFires >= wanted && (flammableOuter < 25 || outerFires > 0),
                $"fires in radius {fires.Count}, beyond half radius {outerFires} "
                    + $"(wanted {wanted}; {flammableOuter} burnable cells out there)");

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
        /// Spawns wild animals in a ring of spots across a blast's radius,
        /// for checks that need creatures under the bolts.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The blast's centre.</param>
        /// <param name="radius">The blast's radius.</param>
        /// <returns>The animals spawned (fewer where no cell was standable).</returns>
        private static List<Pawn> SpawnWildAnimalsAround(Map map, IntVec3 center, float radius)
        {
            var animals = new List<Pawn>();
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Muffalo")
                ?? DefDatabase<PawnKindDef>.AllDefs.First(k => k.RaceProps.Animal);
            for (int i = 0; i < 24; i++)
            {
                float angle = i * 15f;
                float distance = radius * (0.2f + (0.7f * (i % 4) / 3f));
                IntVec3 spot = center + (Vector3Utility.FromAngleFlat(angle) * distance).ToIntVec3();
                if (!spot.InBounds(map)
                    || !CellFinder.TryFindRandomCellNear(spot, map, 4, c => c.Standable(map), out IntVec3 cell))
                {
                    continue;
                }

                animals.Add((Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(kind, null), cell, map));
            }

            return animals;
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
        /// The shared implosion, hosted by the runner itself as the Tractor
        /// Bomb will host it: 10 tiles in to within 1.5, with ten seconds to
        /// run. It rips a stool loose and drags a colonist and a spread of
        /// items into the 3 by 3 block around its centre, shoving nothing
        /// away and losing nothing. Four of the items converge on one line,
        /// so the last must slip sideways into the pile. It reports
        /// everything gathered well before its time is up, and settles once
        /// nothing more can move.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestImplosionGathers(Map map, IntVec3 center)
        {
            IntVec3 at = Clamp(map, center + new IntVec3(45, 0, -45));
            PrepareGround(map, CellRect.CenteredOn(at, 14));
            var loose = new List<Thing>();
            foreach (var (def, x, z) in new[]
            {
                (ThingDefOf.Steel, 8, 0), (ThingDefOf.Steel, 6, -6),
                (ThingDefOf.WoodLog, 0, 3), (ThingDefOf.Plasteel, 0, 6),
                (ThingDefOf.Silver, 0, 9), (ThingDefOf.Gold, -7, -2),
                (ThingDefOf.ComponentIndustrial, -4, 7),
            })
            {
                Thing item = ThingMaker.MakeThing(def);
                item.stackCount = Mathf.Min(10, def.stackLimit);
                IntVec3 spot = at + new IntVec3(x, 0, z);
                loose.Add(GenSpawn.Spawn(item, spot, map));
            }

            Dictionary<ThingDef, int> amounts = ItemAmounts(map, at, loose);
            ThingDef stoolDef = DefDatabase<ThingDef>.GetNamed("Stool");
            Thing stool = GenSpawn.Spawn(
                ThingMaker.MakeThing(stoolDef, ThingDefOf.WoodLog),
                at + new IntVec3(-5, 0, -5), map);
            Pawn pawn = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(p => !p.Dead && !p.Downed);
            if (pawn != null)
            {
                pawn.jobs.StopAll();
                pawn.Position = at + new IntVec3(3, 0, -8);
                pawn.Notify_Teleported();
                pawn.drafter.Drafted = true; // stand still until it's seized
            }

            var settings = new ImplosionSettings
            {
                gatherRadius = 1.5f,
                pullTicks = 600,
            };
            var implosion = new Implosion(at, settings);
            var nearest = loose.ToDictionary(
                t => t, t => t.Position.DistanceTo(at));
            int pushedAway = 0;
            bool sawOutstanding = false;
            int gatheredAt = -1;
            int settledAt = -1;
            List<string> stragglers = null;
            // The host's cap: half again its intended length, at the end
            // rate.
            int cap = implosion.PullTicks * 3 / 2;
            while (implosion.ElapsedTicks < cap && settledAt < 0)
            {
                Tick(1);
                implosion.Tick(map);
                foreach (Thing t in loose.Where(t => t.Spawned))
                {
                    float d = t.Position.DistanceTo(at);
                    if (d > nearest[t] + 0.01f)
                    {
                        pushedAway++;
                    }

                    nearest[t] = Mathf.Min(nearest[t], d);
                }

                sawOutstanding |= implosion.StepsTaken > 0
                    && !implosion.AllGathered
                    && implosion.ElapsedTicks > settings.ripTicks;
                if (gatheredAt < 0 && implosion.AllGathered)
                {
                    gatheredAt = implosion.ElapsedTicks;
                    stragglers = Stragglers(map, implosion);
                }

                if (gatheredAt >= 0 && implosion.Settled)
                {
                    settledAt = implosion.ElapsedTicks;
                }
            }

            stragglers ??= Stragglers(map, implosion);
            string movers = settledAt < 0 ? Movers(map, implosion, 60) : "";
            Check("an implosion drags everything within 10 tiles to within 1.5",
                gatheredAt >= 0 && stragglers.Count == 0,
                $"gathered at tick {gatheredAt}; outside 1.5 tiles: "
                    + string.Join(", ", stragglers));
            Check("an implosion reports everything gathered early, then settles",
                sawOutstanding && gatheredAt >= 0
                    && gatheredAt < implosion.PullTicks
                    && settledAt >= gatheredAt,
                $"outstanding seen {sawOutstanding}, gathered at {gatheredAt}, "
                    + $"settled at {settledAt}, of {implosion.PullTicks} ticks; "
                    + $"still moving: {movers}");
            Dictionary<ThingDef, int> after = ItemAmounts(map, at, loose);
            Check("an implosion shoves nothing away and loses nothing",
                pushedAway == 0 && amounts.All(kv =>
                    after.TryGetValue(kv.Key, out int n) && n >= kv.Value),
                $"{pushedAway} moves away from the centre; "
                    + string.Join(", ", amounts.Select(kv =>
                        $"{kv.Key.defName} {kv.Value}->"
                        + $"{(after.TryGetValue(kv.Key, out int n) ? n : 0)}")));
            Check("an implosion rips fixtures loose and catches pawns",
                !stool.Spawned && implosion.Uninstalled >= 1
                    && (pawn == null || implosion.Caught.Contains(pawn)),
                $"stool spawned {stool.Spawned}, "
                    + $"uninstalled {implosion.Uninstalled}, "
                    + $"caught {implosion.CaughtCount}");
            if (pawn != null && pawn.Spawned && pawn.drafter != null)
            {
                pawn.drafter.Drafted = false;
            }
        }

        /// <summary>
        /// The pawns and items within an implosion's reach that lie outside
        /// its gather radius, described for a failure message.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="implosion">The implosion.</param>
        /// <returns>
        /// One "label (x, z)" entry per straggler, offsets from the centre.
        /// </returns>
        private static List<string> Stragglers(Map map, Implosion implosion)
        {
            IntVec3 c = implosion.Center;
            return Pullables(map, implosion)
                .Where(t => !t.Position.InHorDistOf(c, implosion.GatherRadius))
                .Select(t => $"{t.LabelShort} "
                    + $"({t.Position.x - c.x}, {t.Position.z - c.z})")
                .ToList();
        }

        /// <summary>The pawns and items within an implosion's reach.</summary>
        /// <param name="map">The map.</param>
        /// <param name="implosion">The implosion.</param>
        /// <returns>The things it pulls.</returns>
        private static List<Thing> Pullables(Map map, Implosion implosion) =>
            GenRadial.RadialDistinctThingsAround(
                    implosion.Center, map, implosion.PullRadius, true)
                .Where(t => t is Pawn || t.def.category == ThingCategory.Item)
                .ToList();

        /// <summary>
        /// Ticks an implosion that should have settled a while longer and
        /// names what still moves in its reach, for a failure message:
        /// each pawn or item that changes cell, or arrives, with its def
        /// and faction.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="implosion">The implosion.</param>
        /// <param name="ticks">How long to watch.</param>
        /// <returns>Up to a dozen moves, offsets from the centre.</returns>
        private static string Movers(Map map, Implosion implosion, int ticks)
        {
            IntVec3 c = implosion.Center;
            var was = Pullables(map, implosion)
                .ToDictionary(t => t, t => t.Position);
            var moves = new List<string>();
            for (int i = 0; i < ticks && moves.Count < 12; i++)
            {
                Tick(1);
                implosion.Tick(map);
                var now = Pullables(map, implosion)
                    .ToDictionary(t => t, t => t.Position);
                foreach (var kv in now)
                {
                    bool known = was.TryGetValue(kv.Key, out IntVec3 from);
                    if (known && from == kv.Value)
                    {
                        continue;
                    }

                    string start = known
                        ? $"({from.x - c.x}, {from.z - c.z})" : "outside";
                    moves.Add($"{kv.Key.LabelShort} [{kv.Key.def.defName}, "
                        + $"{kv.Key.Faction?.Name ?? "no faction"}, stunned "
                        + $"{(kv.Key as Pawn)?.stances?.stunner?.Stunned}] "
                        + $"{start} -> ({kv.Value.x - c.x}, {kv.Value.z - c.z})"
                        + $" at tick {implosion.ElapsedTicks}");
                }

                was = now;
            }

            return moves.Count == 0 ? "nothing" : string.Join("; ", moves);
        }

        // ===== Tractor Bomb (docs/TRACTOR-BOMB.md) =====

        /// <summary>
        /// How far from the middle of the map the Tractor Bomb scenarios
        /// clear their stage, in tiles.
        /// </summary>
        private const int TractorStageRadius = 32;

        /// <summary>
        /// The Tractor Bomb, on a cleared stage at the middle of the map:
        /// the robot itself, its patrol, the temporary recipe, a hunt from
        /// patrol to explosion, a protest, Dial-A-Yield and a robot's death.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBomb(Map map, IntVec3 center)
        {
            var mark = ErrorMark();
            ResearchProjectDef dial =
                BT_TractorBombDefOf.BT_Research_TractorBombDialAYield;
            Check("Dial-A-Yield starts unresearched", !dial.IsFinished);
            Find.ResearchManager.FinishProject(
                BT_TractorBombDefOf.BT_Research_TractorBomb,
                doCompletionDialog: false, doCompletionLetter: false);

            TestTractorBombRobot(map, center);
            TestTractorBombPatrol(map, center);
            TestTractorBombRecipe(map, center);
            TestTractorBombHunt(map, center);
            TestTractorBombProtest(map, center);
            TestTractorBombDialAYield(map, center);
            TestTractorBombDeath(map, center);
            Check("the Tractor Bomb scenarios logged no errors",
                !NewErrorSince(mark), "an error was logged (see the log)");
        }

        /// <summary>
        /// Clears a stage for the Tractor Bomb: concrete under open sky,
        /// no fog, no fires anywhere, no hostiles, no wild animals near it,
        /// and every colonist drafted (holding fire) well away from it, so
        /// nobody wanders into a pull radius or a protest uninvited.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="stage">The stage's middle.</param>
        private static void ClearTractorStage(Map map, IntVec3 stage)
        {
            PrepareGround(map, CellRect.CenteredOn(stage, TractorStageRadius));
            foreach (IntVec3 c in CellRect.CenteredOn(stage, TractorStageRadius))
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

            IntVec3 hold = Clamp(map, stage + new IntVec3(0, 0, 70));
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                bool near = p.Position.InHorDistOf(
                    stage, TractorStageRadius + 25);
                if (p.HostileTo(Faction.OfPlayer)
                    || (near && p.RaceProps.Animal && p.Faction == null)
                    || p.TryGetComp<CompTractorBomb>() != null)
                {
                    p.Destroy(DestroyMode.Vanish);
                }
                else if (p.IsColonist && !p.Dead)
                {
                    HoldColonist(map, p, hold);
                }
            }
        }

        /// <summary>
        /// Drafts a colonist, holding fire, and stands it near a spot.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="p">The colonist.</param>
        /// <param name="spot">Where to stand it.</param>
        private static void HoldColonist(Map map, Pawn p, IntVec3 spot)
        {
            if (!CellFinder.TryFindRandomCellNear(
                spot, map, 10, c => c.Standable(map) && !c.Fogged(map),
                out IntVec3 cell))
            {
                cell = spot;
            }

            p.jobs?.StopAll();
            p.Position = cell;
            p.Notify_Teleported();
            if (p.drafter != null)
            {
                p.drafter.Drafted = true;
                p.drafter.FireAtWill = false;
            }
        }

        /// <summary>
        /// A colonist for a scenario, drafted and holding fire at a spot:
        /// one of the colony's, if any is still standing after the earlier
        /// blasts, or a fresh one. A sabotage run showed the protest and
        /// recipe checks passing vacuously once every colonist was dead.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="spot">Where to stand it.</param>
        /// <returns>The colonist, spawned.</returns>
        private static Pawn TestColonist(Map map, IntVec3 spot)
        {
            Pawn p = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                c => !c.Dead && !c.Downed && !c.InMentalState);
            if (p == null)
            {
                p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    PawnKindDefOf.Colonist, Faction.OfPlayer,
                    forceGenerateNewPawn: true, dontGiveWeapon: true));
                GenSpawn.Spawn(p, spot, map);
            }

            HoldColonist(map, p, spot);
            return p;
        }

        /// <summary>
        /// A hostile raider who will not move or shoot: unarmed and stunned
        /// for longer than any scenario lasts.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="at">Where to put it.</param>
        /// <returns>The hostile, spawned.</returns>
        private static Pawn SpawnStunnedHostile(Map map, IntVec3 at)
        {
            Faction enemy = Find.FactionManager.RandomEnemyFaction(
                allowNonHumanlike: false) ?? Faction.OfMechanoids;
            PawnKindDef kind = enemy.def.humanlikeFaction
                ? PawnKindDefOf.Drifter
                : DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther");
            Pawn hostile = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind, enemy, forceGenerateNewPawn: true, dontGiveWeapon: true));
            GenSpawn.Spawn(hostile, at, map);
            hostile.stances.stunner.StunFor(
                100000, null, addBattleLog: false, showMote: false);
            return hostile;
        }

        /// <summary>A fresh player robot, exactly where asked.</summary>
        /// <param name="map">The map.</param>
        /// <param name="at">Where to put it.</param>
        /// <returns>The robot, spawned.</returns>
        private static Pawn SpawnTestRobot(Map map, IntVec3 at) =>
            (Pawn)GenSpawn.Spawn(
                TractorBombUtility.MakeRobot(Faction.OfPlayer), at, map);

        /// <summary>
        /// Ticks until a robot has emplaced, or a cap passes, noting when it
        /// was ready and when its bomb appeared.
        /// </summary>
        /// <param name="robot">The robot.</param>
        /// <param name="maxTicks">The cap.</param>
        /// <param name="approached">Whether it was ever seen approaching.</param>
        /// <returns>Its bomb, or null if none appeared.</returns>
        private static Building_TractorBomb WaitForEmplacement(
            Pawn robot, int maxTicks, out bool approached)
        {
            approached = false;
            Map map = robot.Map;
            for (int i = 0; i < maxTicks && robot.Spawned; i++)
            {
                approached |= robot.CurJobDef
                    == BT_TractorBombDefOf.BT_TractorBombApproach;
                Tick(1);
            }

            return map.listerThings
                .ThingsOfDef(BT_TractorBombDefOf.BT_TractorBombEmplaced)
                .OfType<Building_TractorBomb>()
                .OrderByDescending(b => b.EmplacedTick)
                .FirstOrDefault(b => b.Spawned);
        }

        /// <summary>
        /// The robot as a pawn: its own race and not a mechanoid (no
        /// mechanitor rules, even with Biotech), no needs and no mood, a
        /// per-pawn allowed area the player can set, selectable, with a
        /// sensible inspect pane and controls, and no yield sliders before
        /// Dial-A-Yield, which also leaves a dialled yield at the defaults.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombRobot(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            Pawn robot = SpawnTestRobot(map, center);
            var comp = robot.TryGetComp<CompTractorBomb>();
            Tick(30);
            Check("a tractor bomb is a player robot of its own race",
                robot.def == BT_TractorBombDefOf.BT_TractorBomb
                    && robot.Faction == Faction.OfPlayer && comp != null
                    && !robot.RaceProps.IsMechanoid && !robot.IsColonyMech
                    && !robot.RaceProps.Animal && !robot.RaceProps.Humanlike
                    && !robot.IsColonist && robot.mechanitor == null,
                $"mech {robot.RaceProps.IsMechanoid}, colony mech "
                    + $"{robot.IsColonyMech}, animal {robot.RaceProps.Animal}");
            Check("a tractor bomb has no needs and no mood",
                (robot.needs?.AllNeeds.Count ?? 0) == 0
                    && robot.needs?.mood == null,
                string.Join(", ", robot.needs?.AllNeeds.Select(n => n.def.defName)
                    ?? Enumerable.Empty<string>()));
            Check("a tractor bomb has a player-settable allowed area",
                robot.playerSettings != null
                    && robot.playerSettings.SupportsAllowedAreas
                    && robot.playerSettings.RespectsAllowedArea
                    && robot.playerSettings.AreaRestrictionInPawnCurrentMap
                        == null);

            List<Gizmo> gizmos = robot.GetGizmos().ToList();
            string inspect = robot.GetInspectString();
            Check("a tractor bomb is selectable, with a sensible inspect pane",
                robot.def.selectable && inspect.Contains("Tractor beam 10 tiles")
                    && inspect.Contains("Patrols:")
                    && inspect.Contains("atrolling")
                    && gizmos.OfType<Command_TractorBombArea>().Any(),
                $"inspect \"{inspect.Replace('\n', '|')}\", "
                    + $"{gizmos.Count} gizmos");

            comp.PullRadius = 4f;
            comp.BlastRadius = 3f;
            Check("before Dial-A-Yield a robot has no sliders and the defaults",
                !gizmos.OfType<Gizmo_TractorBombYield>().Any()
                    && comp.PullRadius == 10f && comp.BlastRadius == 5f,
                $"pull {comp.PullRadius}, blast {comp.BlastRadius}");
            robot.Destroy(DestroyMode.Vanish);
        }

        /// <summary>
        /// With nobody about, a robot patrols: it keeps moving, and stays
        /// inside the allowed area the player gives it.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombPatrol(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            if (!map.areaManager.TryMakeNewAllowed(out Area_Allowed area))
            {
                Check("a robot patrols inside its allowed area", false,
                    "could not make an allowed area");
                return;
            }

            CellRect pen = CellRect.CenteredOn(center + new IntVec3(10, 0, 0), 7);
            foreach (IntVec3 c in pen)
            {
                area[c] = true;
            }

            Pawn robot = SpawnTestRobot(map, pen.CenterCell);
            robot.playerSettings.AreaRestrictionInPawnCurrentMap = area;
            var visited = new HashSet<IntVec3>();
            int outside = 0;
            bool patrolled = false;
            for (int i = 0; i < 1800; i++)
            {
                Tick(1);
                patrolled |= robot.CurJobDef
                    == BT_TractorBombDefOf.BT_TractorBombPatrol;
                if (i % 10 == 0)
                {
                    visited.Add(robot.Position);
                    if (!area[robot.Position])
                    {
                        outside++;
                    }
                }
            }

            Check("a robot patrols inside its allowed area",
                patrolled && visited.Count >= 8 && outside == 0,
                $"patrol job seen {patrolled}, {visited.Count} cells visited, "
                    + $"{outside} samples outside the area");
            robot.Destroy(DestroyMode.Vanish);
            area.Delete();
        }

        /// <summary>
        /// The temporary recipe at the machining table: when it completes, a
        /// robot drives out beside the crafter.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombRecipe(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            RecipeDef recipe =
                DefDatabase<RecipeDef>.GetNamed("BT_AssembleTractorBomb");
            Pawn crafter = TestColonist(map, center + new IntVec3(8, 0, 8));
            int before = TractorBombUtility.CountOnMap(map, Faction.OfPlayer);
            recipe.Worker.Notify_IterationCompleted(crafter, new List<Thing>());
            Pawn robot = map.mapPawns.AllPawnsSpawned.FirstOrDefault(
                p => p.TryGetComp<CompTractorBomb>() != null);
            Check("the temporary recipe drives a robot out beside the crafter",
                robot != null
                    && robot.Position.InHorDistOf(crafter.Position, 5f)
                    && TractorBombUtility.CountOnMap(map, Faction.OfPlayer)
                        == before + 1
                    && DefDatabase<ThingDef>.GetNamed("TableMachining")
                        .AllRecipes.Contains(recipe),
                $"robot {robot?.Position}, crafter {crafter.Position}");
            robot?.Destroy(DestroyMode.Vanish);
        }

        /// <summary>
        /// A hunt from patrol to explosion. A robot sees a stunned raider
        /// 37 tiles off, drives to within 15 tiles and emplaces at once
        /// (no colonist about). Its bomb counts down 3 to 5 s, then pulls
        /// everything within 10 tiles to within 1.5 (eleven stacks that
        /// cannot merge, so two must wait just outside the pile) and
        /// explodes over 5 tiles, catching what is left outside the pile.
        /// The raider at 15 tiles is out of reach and stays put.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombHunt(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            Pawn robot = SpawnTestRobot(map, center + new IntVec3(-25, 0, 0));
            Pawn quarry = SpawnStunnedHostile(map, center + new IntVec3(12, 0, 0));
            float startDistance = robot.Position.DistanceTo(quarry.Position);
            Building_TractorBomb bomb = WaitForEmplacement(
                robot, 1500, out bool approached);
            float distance = bomb == null ? -1f
                : bomb.Position.DistanceTo(quarry.Position);
            Check("a robot finds a hostile and emplaces within 15 tiles",
                bomb != null && approached && startDistance > 30f
                    && bomb.Position.InHorDistOf(quarry.Position, 15f),
                $"bomb {bomb != null}, approached {approached}, "
                    + $"{startDistance:F1} -> {distance:F1} tiles");
            if (bomb == null)
            {
                return;
            }

            Check("with no colonist in reach it emplaces at once",
                bomb.ProtestTicks == 0 && bomb.ReadyTick >= 0
                    && bomb.EmplacedTick - bomb.ReadyTick <= 2,
                $"protest {bomb.ProtestTicks}, ready {bomb.ReadyTick}, "
                    + $"emplaced {bomb.EmplacedTick}");
            // Not armed while its tyres flip; armed (and found by the flee
            // step's query) once its countdown starts.
            bool armedEarly = bomb.IsArmed
                || TractorBombUtility.ArmedBombs(map).Contains(bomb);
            bool defaults = bomb.PullRadius == 10f && bomb.BlastRadius == 5f;
            bool armedLater = false;

            // Stage the pull while its tyres flip: eleven stacks that cannot
            // merge, a second raider inside the reach and one item outside.
            IntVec3 eye = bomb.Position;
            var stacks = new List<Thing>();
            ThingDef[] kinds =
            {
                ThingDefOf.Steel, ThingDefOf.WoodLog, ThingDefOf.Plasteel,
                ThingDefOf.Silver, ThingDefOf.Gold, ThingDefOf.Cloth,
                ThingDefOf.ComponentIndustrial, ThingDefOf.MedicineHerbal,
                ThingDefOf.Uranium, ThingDefOf.Jade, ThingDefOf.Chemfuel,
            };
            for (int i = 0; i < kinds.Length; i++)
            {
                IntVec3 spot = eye + (Vector3Utility.FromAngleFlat(i * 360f
                    / kinds.Length) * (3f + (i % 4) * 2f)).ToIntVec3();
                Thing item = ThingMaker.MakeThing(kinds[i]);
                item.stackCount = Mathf.Min(5, kinds[i].stackLimit);
                stacks.Add(GenSpawn.Spawn(item, spot, map));
            }

            Pawn second = SpawnStunnedHostile(map, eye + new IntVec3(0, 0, 7));
            Thing outsider = GenSpawn.Spawn(
                ThingMaker.MakeThing(ThingDefOf.Steel), eye + new IntVec3(0, 0, -12),
                map);
            IntVec3 outsiderAt = outsider.Position;
            IntVec3 quarryAt = quarry.Position;
            var tracked = new List<Thing>(stacks) { second };
            var last = tracked.ToDictionary(t => t, t => t.Position);
            int explosions = Building_TractorBomb.Explosions;
            for (int i = 0; i < 1500 && bomb.Spawned; i++)
            {
                foreach (Thing t in tracked.Where(t => t.Spawned))
                {
                    last[t] = t.Position;
                }

                armedLater |= bomb.CurrentStage
                        == Building_TractorBomb.Stage.CountingDown
                    && bomb.IsArmed
                    && TractorBombUtility.ArmedBombs(map).Contains(bomb);
                Tick(1);
            }

            Check("a bomb is armed once its countdown starts, for the flee step",
                !armedEarly && armedLater && defaults,
                $"armed while flipping {armedEarly}, while counting down "
                    + $"{armedLater}, default yield {defaults}");

            int countdown = bomb.ImplosionStartedTick - bomb.CountdownStartedTick;
            Check("the countdown is 3 to 5 s",
                bomb.CountdownTicks >= 180 && bomb.CountdownTicks <= 300
                    && countdown == bomb.CountdownTicks
                    && bomb.CountdownStartedTick - bomb.EmplacedTick == 30,
                $"rolled {bomb.CountdownTicks}, ran {countdown} ticks, "
                    + $"flip {bomb.CountdownStartedTick - bomb.EmplacedTick}");

            Implosion implosion = bomb.Implosion;
            float blast = bomb.BlastRadius;
            int gathered = last.Values.Count(c => c.InHorDistOf(eye, 1.5f));
            var strays = last
                .Where(kv => !kv.Value.InHorDistOf(eye, 1.5f)
                    && !kv.Value.InHorDistOf(eye, blast))
                .Select(kv => $"{kv.Key.LabelShort} ({kv.Value.x - eye.x}, "
                    + $"{kv.Value.z - eye.z})")
                .ToList();
            Check("everything within the pull ends within 1.5 tiles or is caught",
                implosion != null && implosion.PullRadius == 10f
                    && implosion.GatherRadius == 1.5f && strays.Count == 0
                    && gathered >= 9,
                $"{gathered} of {tracked.Count} within 1.5 tiles; outside the "
                    + $"blast: {string.Join(", ", strays)}");
            Check("nothing beyond the pull radius is dragged",
                (!outsider.Spawned || outsider.Position == outsiderAt)
                    && (!quarry.Spawned || quarry.Position == quarryAt),
                $"steel {outsiderAt} -> {outsider.Position}, raider "
                    + $"{quarryAt} -> {quarry.Position}");
            bool hurt = second.Dead
                || second.health.hediffSet.GetInjuredParts().Any();
            Check("the bomb explodes over 5 tiles",
                bomb.Destroyed && Building_TractorBomb.Explosions == explosions + 1
                    && Building_TractorBomb.LastBlastRadius == 5f && hurt,
                $"destroyed {bomb.Destroyed}, explosions "
                    + $"+{Building_TractorBomb.Explosions - explosions}, radius "
                    + $"{Building_TractorBomb.LastBlastRadius}, raider hurt {hurt}");
            Tick(60);
        }

        /// <summary>
        /// A colonist inside the pull radius when the robot is ready: it
        /// protests for about three seconds, then emplaces anyway.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombProtest(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            Pawn robot = SpawnTestRobot(map, center + new IntVec3(-6, 0, 0));
            Pawn colonist = TestColonist(
                map, robot.Position + new IntVec3(0, 0, 4));

            SpawnStunnedHostile(map, center + new IntVec3(6, 0, 0));
            var comp = robot.TryGetComp<CompTractorBomb>();
            bool protested = false;
            Building_TractorBomb bomb = null;
            for (int i = 0; i < 600 && robot.Spawned; i++)
            {
                Tick(1);
                protested |= robot.Spawned && comp.Protesting;
            }

            bomb = map.listerThings
                .ThingsOfDef(BT_TractorBombDefOf.BT_TractorBombEmplaced)
                .OfType<Building_TractorBomb>().FirstOrDefault(b => b.Spawned);
            int delay = bomb == null ? -1 : bomb.EmplacedTick - bomb.ReadyTick;
            Check("a colonist in the pull radius delays emplacing about 3 s",
                protested && bomb != null && delay >= 170 && delay <= 190,
                $"protested {protested}, bomb {bomb != null}, delay {delay}, "
                    + $"colonist {colonist.Position.DistanceTo(robot.Position):F1}"
                    + " tiles off");
            Check("after its protest it emplaces anyway",
                bomb != null && bomb.ProtestTicks >= 170,
                $"bomb {bomb != null}, protest {bomb?.ProtestTicks} ticks");

            // Get the colonist clear before the pull, and let it go off.
            HoldColonist(map, colonist, Clamp(map, center + new IntVec3(0, 0, 70)));

            for (int i = 0; i < 1200 && bomb != null && bomb.Spawned; i++)
            {
                Tick(1);
            }

            Tick(60);
        }

        /// <summary>
        /// Dial-A-Yield: once researched, a robot and its bomb have both
        /// sliders; dialled radii are clamped to range, carry over to the
        /// bomb, set its implosion's reach and its blast, and the bomb's
        /// sliders go once it starts to pull. Here the tractor beam is
        /// dialled down to 4 tiles and the blast to 3: an item 3 tiles out
        /// is pulled in, one 6 tiles out is left alone.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombDialAYield(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            Find.ResearchManager.FinishProject(
                BT_TractorBombDefOf.BT_Research_TractorBombDialAYield,
                doCompletionDialog: false, doCompletionLetter: false);
            Pawn robot = SpawnTestRobot(map, center + new IntVec3(-6, 0, 0));
            var comp = robot.TryGetComp<CompTractorBomb>();
            var sliders = comp.CompGetGizmosExtra()
                .OfType<Gizmo_TractorBombYield>().ToList();
            comp.PullRadius = 25f;
            comp.BlastRadius = 0.5f;
            bool clamped = comp.PullRadius == 20f && comp.BlastRadius == 2f;
            var merged = new Gizmo_TractorBombYield(
                comp, TractorBombYieldKind.Pull);
            merged.MergeWith(new Gizmo_TractorBombYield(
                new CompTractorBomb(), TractorBombYieldKind.Pull));
            comp.PullRadius = 4f;
            comp.BlastRadius = 3f;
            Check("Dial-A-Yield gives robots two clamped, groupable sliders",
                sliders.Count == 2 && clamped && merged.Bombs.Count == 2
                    && comp.PullRadius == 4f && comp.BlastRadius == 3f,
                $"{sliders.Count} sliders, clamped {clamped}, "
                    + $"merged {merged.Bombs.Count}");

            IntVec3 at = robot.Position;
            Thing near = GenSpawn.Spawn(
                ThingMaker.MakeThing(ThingDefOf.Steel), at + new IntVec3(0, 0, 3), map);
            Thing far = GenSpawn.Spawn(
                ThingMaker.MakeThing(ThingDefOf.WoodLog), at + new IntVec3(0, 0, -6),
                map);
            IntVec3 farAt = far.Position;
            SpawnStunnedHostile(map, center + new IntVec3(6, 0, 0));
            Building_TractorBomb bomb = WaitForEmplacement(robot, 600, out _);
            bool bombSliders = bomb != null && bomb.GetGizmos()
                .OfType<Gizmo_TractorBombYield>().Count() == 2;
            IntVec3 nearLast = near.Position;
            bool slidersGone = false;
            for (int i = 0; i < 1200 && bomb != null && bomb.Spawned; i++)
            {
                if (near.Spawned)
                {
                    nearLast = near.Position;
                }

                if (bomb.CurrentStage == Building_TractorBomb.Stage.Imploding)
                {
                    slidersGone |= !bomb.GetGizmos()
                        .OfType<Gizmo_TractorBombYield>().Any();
                }

                Tick(1);
            }

            Check("Dial-A-Yield changes the radii",
                bomb != null && bomb.Implosion?.PullRadius == 4f
                    && Building_TractorBomb.LastBlastRadius == 3f
                    && nearLast.InHorDistOf(at, 1.5f)
                    && far.Spawned && far.Position == farAt,
                $"pull {bomb?.Implosion?.PullRadius}, blast "
                    + $"{Building_TractorBomb.LastBlastRadius}, item at 3 ended "
                    + $"{nearLast - at}, item at 6 moved {far.Position != farAt}");
            Check("a planted bomb can be dialled until it starts to pull",
                bombSliders && slidersGone,
                $"sliders while counting down {bombSliders}, gone while "
                    + $"pulling {slidersGone}");
            Tick(60);
        }

        /// <summary>
        /// A robot destroyed before it emplaces gets its own death: a
        /// sputtering vortex that sucks in its own parts and nothing else,
        /// then pops, with no corpse and no death letter. One that loses
        /// three wheels cannot move, and is wrecked rather than downed.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombDeath(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            Pawn robot = SpawnTestRobot(map, center);
            IntVec3 at = robot.Position;
            Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
            steel.stackCount = 20;
            steel = GenSpawn.Spawn(steel, at + new IntVec3(2, 0, 0), map);
            IntVec3 steelAt = steel.Position;
            Pawn colonist = TestColonist(map, at + new IntVec3(-3, 0, 0));
            IntVec3 colonistAt = colonist.Position;
            int injuries = colonist.health.hediffSet.GetInjuredParts().Count;

            int letters = Find.LetterStack.LettersListForReading.Count;
            int wrecked = CompTractorBomb.Wrecked;
            int pops = TractorBombDeathVortex.Pops;
            robot.Kill(new DamageInfo(DamageDefOf.Bomb, 50f));
            var vortex = map.listerThings
                .ThingsOfDef(BT_TractorBombDefOf.BT_TractorBombDeathVortex)
                .FirstOrDefault(v => v.Position == at);
            bool corpse = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                .OfType<Corpse>().Any(c => c.InnerPawn == robot);
            Check("killing a robot before it emplaces sets off its own death",
                robot.Dead && vortex != null && !corpse
                    && CompTractorBomb.Wrecked == wrecked + 1
                    && Find.LetterStack.LettersListForReading.Count == letters,
                $"dead {robot.Dead}, vortex {vortex != null}, corpse {corpse}, "
                    + $"letters +{Find.LetterStack.LettersListForReading.Count - letters}");
            Tick(DeathVortexMath.TotalTicks + 5);
            bool untouched = steel.Spawned && steel.Position == steelAt
                && steel.stackCount == 20 && colonist.Position == colonistAt
                && !colonist.stances.stunner.Stunned
                && colonist.health.hediffSet.GetInjuredParts().Count
                    == injuries;
            Check("its vortex sucks in its own parts, nothing else, and pops",
                vortex != null && vortex.Destroyed
                    && TractorBombDeathVortex.Pops == pops + 1 && untouched,
                $"vortex gone {vortex?.Destroyed}, pops "
                    + $"+{TractorBombDeathVortex.Pops - pops}, others untouched "
                    + $"{untouched}");

            // Shot off its wheels: it cannot move, so it is wrecked.
            Pawn crippled = SpawnTestRobot(map, center + new IntVec3(8, 0, 0));
            foreach (BodyPartRecord wheel in crippled.health.hediffSet
                .GetNotMissingParts()
                .Where(p => p.def.defName == "BT_TractorWheel").Take(3).ToList())
            {
                crippled.TakeDamage(new DamageInfo(
                    DamageDefOf.Blunt, 999f, 999f, hitPart: wheel));
            }

            Tick(2);
            Check("a robot that loses its wheels is wrecked, not left downed",
                crippled.Dead && CompTractorBomb.Wrecked == wrecked + 2,
                $"dead {crippled.Dead}, downed {crippled.Downed}");
            Tick(DeathVortexMath.TotalTicks + 5);
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
            // Half the bolts are painful ones, which fly as bullets, and wild
            // animals stand around it: vanilla makes an animal flee from the
            // launcher of a bullet that lands near it (Notify_ClamorImpact),
            // which threw a NullReferenceException while cook-off bolts had
            // no launcher. Seen in the showcase, when AnyRim's Ballista yard
            // went up beside the pens.
            at = Clamp(map, center + new IntVec3(0, 0, -45));
            PrepareGround(map, CellRect.CenteredOn(at, 10));
            launched = aftermath.BoltsLaunched;
            loose = LooseBolts(map);
            var animals = SpawnWildAnimalsAround(map, at, 25f);
            var mark = ErrorMark();
            KillAndCheck(map, aftermath, "BT_BallistaMagazine", at,
                "a destroyed magazine cooks off its cap and pulverizes the rest",
                () => aftermath.BoltsLaunched - launched == 160 && LooseBolts(map) <= loose,
                () => $"launched {aftermath.BoltsLaunched - launched}, loose {loose}->{LooseBolts(map)}",
                t =>
                {
                    AddBolts(((Building_BallistaMagazine)t).Storage, explosive, 150);
                    AddBolts(((Building_BallistaMagazine)t).Storage, BoltDef("BT_BoltPainful"), 150);
                });
            Tick(240); // let the bolts land among the animals
            Check("cook-off bolts landing near animals log no errors", !NewErrorSince(mark),
                $"an error was logged ({animals.Count} animals around the magazine)");
            foreach (Pawn animal in animals.Where(a => a.Spawned))
            {
                animal.Destroy(DestroyMode.Vanish);
            }

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
