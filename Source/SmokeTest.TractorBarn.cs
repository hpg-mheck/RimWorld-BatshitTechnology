// Batshit Technology - in-game smoke checks for the Tractor Barn
// (docs/TRACTOR-BOMB.md, "The Tractor Barn"; roadmap step 8). Part of the
// -btsmoketest runner (SmokeTest.cs), run with the other Tractor Bomb
// scenarios on the cleared stage at the middle of the map: "Build one now"
// adds a bill; a hauler loads the barn and leaves, and the barn builds the
// robot alone, with the barn's settings; "Do until you have X" stops at X,
// counting a planted bomb; a damaged robot drives home, docks and is
// repaired with a little steel; and, once Dial-A-Yield is researched, the
// barn's settings are dialled from its own buttons and "Apply to every
// tractor" copies them to the robots and the planted bombs not yet
// pulling.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BatshitTechnology
{
    /// <summary>The runner's Tractor Barn scenarios.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// The Tractor Barn's scenarios, and that none of them logs an
        /// error.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBarn(Map map, IntVec3 center)
        {
            var mark = ErrorMark();
            TestTractorBarnBuilds(map, center);
            TestTractorBarnUntilX(map, center);
            TestTractorBarnRepair(map, center);
            Check("the Tractor Barn scenarios logged no errors",
                !NewErrorSince(mark), "an error was logged (see the log)");
        }

        /// <summary>
        /// "Build one now" adds a "Do 1 times" bill. A colonist who may
        /// only haul carries that bill's parts into the barn and throws the
        /// switch; then the colonist leaves the map, and with nobody left
        /// to work, the barn builds the robot itself in about an hour and
        /// drives it out of the door. The parts are used up, the bill is
        /// done and the count of bombs on the map goes up by one. The barn
        /// is set to Nosy, Popular and Block Party first, and the robot
        /// leaves with them (before Dial-A-Yield it still pulls and blasts
        /// at the defaults).
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBarnBuilds(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            Building_TractorBarn barn = TractorStaging.SpawnBarn(map, center);
            var production = new TractorBombSettings(
                TractorBombProximity.Nosy, TractorBombPull.Popular,
                TractorBombBlast.BlockParty);
            barn.SetSettings(production);
            IntVec3 door = barn.InteractionCell;
            List<Thing> parts =
                SpawnRobotParts(map, door + new IntVec3(8, 0, -2));
            int countBefore =
                TractorBombUtility.CountOnMap(map, Faction.OfPlayer);
            var robotsBefore = new HashSet<Pawn>(Robots(map));

            Command_Action button = barn.GetGizmos().OfType<Command_Action>()
                .FirstOrDefault(c => c.defaultLabel == "Build one now");
            int billsBefore = barn.BillStack.Count;
            button?.action?.Invoke();
            var bill = barn.BillStack.Bills.LastOrDefault() as Bill_Autonomous;
            Check("\"Build one now\" adds a \"Do 1 times\" bill",
                button != null && barn.BillStack.Count == billsBefore + 1
                    && bill != null
                    && bill.recipe == BT_TractorBarnDefOf.BT_BuildTractorBomb
                    && bill.repeatMode == BillRepeatModeDefOf.RepeatCount
                    && bill.repeatCount == 1,
                $"button {button != null}, bills {billsBefore} -> "
                    + $"{barn.BillStack.Count}, bill {bill?.GetType().Name} "
                    + $"{bill?.repeatMode?.defName} {bill?.repeatCount}");
            if (bill == null)
            {
                return;
            }

            Pawn hauler = FreshHauler(map, door + new IntVec3(-4, 0, -4));
            bool hauled = false;
            int loadTicks = 0;
            for (; loadTicks < 4000 && bill.State == FormingState.Gathering;
                loadTicks++)
            {
                Tick(1);
                hauled |= hauler.CurJobDef == JobDefOf.DoBill
                    && hauler.CurJob.targetA.Thing == barn;
            }

            bool forming = bill.State == FormingState.Forming;
            Dictionary<ThingDef, int> loaded = Loaded(barn);
            bool fullLoad = loaded.Count == 3
                && Amount(loaded, ThingDefOf.Steel) == 60
                && Amount(loaded, ThingDefOf.ComponentIndustrial) == 3
                && Amount(loaded, BatshittiumDef) == 5;

            // Nobody to work from here on: the hauler leaves the map.
            RemoveTestPawns(hauler);
            int built = Building_TractorBarn.Built;
            int formTicks = 0;
            for (; formTicks < 3000 && Building_TractorBarn.Built == built;
                formTicks++)
            {
                Tick(1);
            }

            Pawn robot = Robots(map).FirstOrDefault(
                p => !robotsBefore.Contains(p));
            Check("the barn builds a robot from a bill with hauled "
                    + "ingredients and no worker",
                hauled && forming && fullLoad
                    && Building_TractorBarn.Built == built + 1
                    && formTicks >= 2400 && robot != null
                    && robot.Position.InHorDistOf(door, 5f)
                    && TractorBombUtility.CountOnMap(map, Faction.OfPlayer)
                        == countBefore + 1
                    && barn.innerContainer.Count == 0
                    && bill.repeatCount == 0 && !bill.ShouldDoNow()
                    && parts.All(t => t.Destroyed),
                $"hauled {hauled} in {loadTicks} ticks, forming {forming}, "
                    + $"loaded [{Describe(loaded)}], built "
                    + $"+{Building_TractorBarn.Built - built} after "
                    + $"{formTicks} ticks, robot {robot?.Position} (door "
                    + $"{door}), count {countBefore} -> "
                    + $"{TractorBombUtility.CountOnMap(map, Faction.OfPlayer)}, "
                    + $"left inside {barn.innerContainer.Count}, bill "
                    + $"{bill.repeatCount}x, parts used "
                    + $"{parts.Count(t => t.Destroyed)}/{parts.Count}");
            CompTractorBomb comp = robot?.TryGetComp<CompTractorBomb>();
            string pane = barn.GetInspectString();
            Check("a new robot leaves the barn with the barn's settings",
                comp != null && comp.Settings == production
                    && comp.Proximity == TractorBombProximity.Nosy
                    && comp.PullRadius == 15f && comp.BlastRadius == 7.5f
                    && pane.Contains("New tractors: Nosy, Neighborly, Firework"),
                $"robot {comp?.Settings}, in effect {comp?.PullRadius}/"
                    + $"{comp?.BlastRadius}; barn pane "
                    + $"\"{pane.Replace('\n', '|')}\"");
        }

        /// <summary>
        /// Once Dial-A-Yield is researched, the barn's three buttons set
        /// what its new robots get, and "Apply to every tractor" gives
        /// those settings to every robot of the player's on the map and to
        /// every planted bomb whose pull has not started (its pull and
        /// blast), but not to a bomb already pulling. The pulling bomb
        /// stands 40 tiles off with a 6-tile pull, out of everyone's way.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBarnApplyToEveryTractor(Map map, IntVec3 center)
        {
            var mark = ErrorMark();
            ClearTractorStage(map, center);
            Building_TractorBarn barn = TractorStaging.SpawnBarn(map, center);
            Pawn first = TractorStaging.SpawnRobot(
                map, center + new IntVec3(-10, 0, 8));
            Pawn second = TractorStaging.SpawnRobot(
                map, center + new IntVec3(10, 0, 8));
            Building_TractorBomb waiting = PlantTestBomb(
                map, center + new IntVec3(0, 0, -12), 60f);
            Building_TractorBomb pulling = PlantTestBomb(
                map, center + new IntVec3(40, 0, 0), 60f);
            pulling.Settings = new TractorBombSettings(
                TractorBombProximity.Prudent, TractorBombPull.Intimate,
                TractorBombBlast.BirthdayCake);
            waiting.StartCountdown();
            pulling.StartCountdown();
            pulling.ImplodeNow();
            Tick(2);

            List<Command_TractorBombDial> dials = barn.GetGizmos()
                .OfType<Command_TractorBombDial>().ToList();
            dials.FirstOrDefault(d => d.Dial == TractorBombDial.Proximity)
                ?.Apply((int)TractorBombProximity.Timid);
            dials.FirstOrDefault(d => d.Dial == TractorBombDial.Pull)
                ?.Apply((int)TractorBombPull.Popular);
            dials.FirstOrDefault(d => d.Dial == TractorBombDial.Blast)
                ?.Apply((int)TractorBombBlast.BlockParty);
            var wanted = new TractorBombSettings(
                TractorBombProximity.Timid, TractorBombPull.Popular,
                TractorBombBlast.BlockParty);
            Command_Action apply = barn.GetGizmos().OfType<Command_Action>()
                .FirstOrDefault(c => c.defaultLabel == "Apply to every tractor");
            apply?.action?.Invoke();

            CompTractorBomb a = first.TryGetComp<CompTractorBomb>();
            CompTractorBomb b = second.TryGetComp<CompTractorBomb>();
            bool robotsSet = a.Settings == wanted && b.Settings == wanted;
            bool waitingSet = waiting.Settings.Pull == TractorBombPull.Popular
                && waiting.Settings.Blast == TractorBombBlast.BlockParty
                && waiting.PullRadius == 22.5f && waiting.BlastRadius == 12f;
            bool pullingKept = pulling.Settings.Pull == TractorBombPull.Intimate
                && pulling.Settings.Blast == TractorBombBlast.BirthdayCake
                && pulling.CurrentStage == Building_TractorBomb.Stage.Imploding;
            Check("the barn's buttons set its settings, and \"Apply to every "
                    + "tractor\" copies them to robots and bombs not yet pulling",
                dials.Count == 3 && barn.Settings == wanted && apply != null
                    && robotsSet && waitingSet && pullingKept,
                $"{dials.Count} barn buttons, barn {barn.Settings}, apply "
                    + $"{apply != null}; robots {a.Settings} and {b.Settings}; "
                    + $"waiting bomb {waiting.Settings.Pull}/"
                    + $"{waiting.Settings.Blast}; pulling bomb "
                    + $"{pulling.Settings.Pull}/{pulling.Settings.Blast} "
                    + $"({pulling.CurrentStage})");

            foreach (Building_TractorBomb bomb in new[] { waiting, pulling })
            {
                if (bomb.Spawned)
                {
                    bomb.Destroy(DestroyMode.Vanish);
                }
            }

            RemoveTestPawns(first, second);
            if (barn.Spawned)
            {
                barn.Destroy(DestroyMode.Vanish);
            }

            Tick(10);
            Check("the barn's settings log no errors", !NewErrorSince(mark),
                "an error was logged (see the log)");
        }

        /// <summary>
        /// "Do until you have X" counts the player's robots and planted
        /// bombs on the map. With one patrolling robot and one bomb planted
        /// far off (counting down ten minutes), X is set one above what is
        /// on the map, counted here independently of the barn. Parts for
        /// two robots lie ready and a hauler stands by; the barn must build
        /// exactly one and then stay idle. (Building is hurried along once
        /// it starts: its timing is checked above.)
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBarnUntilX(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            Building_TractorBarn barn = TractorStaging.SpawnBarn(map, center);
            IntVec3 door = barn.InteractionCell;
            Building_TractorBomb planted = PlantTestBomb(
                map, Clamp(map, center + new IntVec3(-28, 0, 26)), 600f);
            Pawn patrol = TractorStaging.SpawnRobot(map, center + new IntVec3(-12, 0, -14));
            Tick(40); // the planted bomb's tyres go down
            int robots = Robots(map).Count();
            int plantedCount = PlantedBombs(map).Count();
            int target = robots + plantedCount + 1;

            var bill = barn.AddBuildBill() as Bill_Production;
            if (bill == null)
            {
                Check("\"Do until you have X\" stops at X, counting planted "
                    + "bombs", false, "the barn would not take a bill");
                return;
            }

            bill.repeatMode = BillRepeatModeDefOf.TargetCount;
            bill.targetCount = target;
            RecipeWorkerCounter counter = bill.recipe.WorkerCounter;
            bool canCount = counter.CanCountProducts(bill);
            int counted = counter.CountProducts(bill);
            List<Thing> first =
                SpawnRobotParts(map, door + new IntVec3(8, 0, -2));
            List<Thing> second =
                SpawnRobotParts(map, door + new IntVec3(8, 0, -6));
            Pawn hauler = FreshHauler(map, door + new IntVec3(-4, 0, -4));
            int built = Building_TractorBarn.Built;
            for (int i = 0; i < 4000 && Building_TractorBarn.Built == built;
                i++)
            {
                Tick(1);
                Hurry(barn);
            }

            bool again = false;
            for (int i = 0; i < 900; i++)
            {
                Tick(1);
                Hurry(barn);
                again |= barn.CurrentBill != null
                    || (hauler.CurJobDef == JobDefOf.DoBill
                        && hauler.CurJob.targetA.Thing == barn);
            }

            int robotsAfter = Robots(map).Count();
            int plantedAfter = PlantedBombs(map).Count();
            Check("\"Do until you have X\" stops at X, counting planted bombs",
                canCount && plantedCount == 1
                    && counted == robots + plantedCount
                    && Building_TractorBarn.Built == built + 1 && !again
                    && robotsAfter + plantedAfter == target
                    && counter.CountProducts(bill) == target
                    && !bill.ShouldDoNow()
                    && barn.innerContainer.Count == 0,
                $"robots {robots} + planted {plantedCount} -> X {target}; "
                    + $"barn counted {counted} (can count {canCount}); built "
                    + $"+{Building_TractorBarn.Built - built}, started again "
                    + $"{again}; now robots {robotsAfter} + planted "
                    + $"{plantedAfter}, barn counts "
                    + $"{counter.CountProducts(bill)}; loaded since "
                    + $"{barn.innerContainer.Count}; first set used "
                    + $"{first.All(t => t.Destroyed)}, second set lying "
                    + $"about {second.Count(t => t.Spawned)}/{second.Count}");
            planted.Destroy(DestroyMode.Vanish);
            RemoveTestPawns(hauler, patrol);
        }

        /// <summary>
        /// A robot with a wheel shot off and injuries enough to bring it
        /// below half health, and nobody to hunt, drives home to the barn,
        /// parks at its door and is mended there: every injury healed and
        /// the wheel refitted, for exactly the steel Core's arithmetic
        /// says (a little), taken from the repair hopper, in about the
        /// time it says. Then it drives off on patrol.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBarnRepair(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            Building_TractorBarn barn = TractorStaging.SpawnBarn(map, center);
            CompRefuelable hopper = barn.RepairHopper;
            Pawn robot = TractorStaging.SpawnRobot(map, center + new IntVec3(14, 0, 9));
            BreakRobot(robot);
            float healthBefore =
                robot.health.summaryHealth.SummaryHealthPercent;
            List<double> injuries = robot.health.hediffSet.hediffs
                .OfType<Hediff_Injury>().Select(h => (double)h.Severity)
                .ToList();
            double injury = injuries.Sum();
            int missing = robot.health.hediffSet.hediffs
                .OfType<Hediff_MissingPart>().Count();
            double expectedSteel =
                TractorBarnRepair.SteelFor(injuries, missing);
            long expectedTicks =
                TractorBarnRepair.TicksFor(injuries, missing);
            float steelBefore = hopper?.Fuel ?? 0f;
            bool docked = false;
            bool mended = false;
            string report = null;
            int dockedAt = -1;
            int mendedAt = -1;
            for (int i = 0; i < 4000 && robot.Spawned; i++)
            {
                Tick(1);
                var driver = robot.jobs?.curDriver as JobDriver_TractorBombDock;
                if (!docked && driver != null && driver.Docked
                    && robot.Position == barn.InteractionCell)
                {
                    docked = true;
                    dockedAt = i;
                }

                if (docked && report == null && driver != null && i > dockedAt + 5)
                {
                    report = driver.GetReport();
                }

                if (docked && Whole(robot))
                {
                    mended = true;
                    mendedAt = i;
                    break;
                }
            }

            Tick(120);
            bool drivenOff = robot.Spawned
                && robot.CurJobDef != BT_TractorBarnDefOf.BT_TractorBombDock;
            float steelUsed = steelBefore - (hopper?.Fuel ?? 0f);
            long took = mendedAt - dockedAt;
            Check("a damaged robot docks at the barn and is repaired with a "
                    + "little steel",
                healthBefore < 0.5f && missing == 1 && docked && mended
                    && robot.health.summaryHealth.SummaryHealthPercent > 0.999f
                    && Mathf.Abs(steelUsed - (float)expectedSteel) < 0.05f
                    && steelUsed > 0f && steelUsed <= 12f
                    && System.Math.Abs(took - expectedTicks) <= 90
                    && report != null && report.Contains("repaired")
                    && drivenOff,
                $"health {healthBefore:P0} with {injury:F1} hp of injury and "
                    + $"{missing} part missing; docked {docked} at tick "
                    + $"{dockedAt}, mended {mended} after {took} ticks "
                    + $"(expected {expectedTicks}), health now "
                    + $"{robot.health.summaryHealth.SummaryHealthPercent:P0}; "
                    + $"steel {steelUsed:F2} (expected {expectedSteel:F2}); "
                    + $"report \"{report}\"; then {robot.CurJobDef?.defName}");
            RemoveTestPawns(robot);
        }

        /// <summary>
        /// Spawns one robot's parts in three stacks near a spot: 60 steel,
        /// 3 components and 5 batshittium.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="near">Where to put them.</param>
        /// <returns>The stacks.</returns>
        private static List<Thing> SpawnRobotParts(Map map, IntVec3 near)
        {
            var stacks = new List<Thing>();
            (ThingDef Def, int Count)[] parts =
            {
                (ThingDefOf.Steel, 60), (ThingDefOf.ComponentIndustrial, 3),
                (BatshittiumDef, 5),
            };
            for (int i = 0; i < parts.Length; i++)
            {
                Thing stack = ThingMaker.MakeThing(parts[i].Def);
                stack.stackCount = parts[i].Count;
                stacks.Add(GenSpawn.Spawn(
                    stack, near + new IntVec3(i * 2, 0, 0), map));
            }

            return stacks;
        }

        /// <summary>
        /// A brand-new colonist who may only haul: every other work type
        /// off, hauling first. One who cannot haul is passed over.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="spot">Where to put it.</param>
        /// <returns>The colonist, spawned and undrafted.</returns>
        private static Pawn FreshHauler(Map map, IntVec3 spot)
        {
            Pawn p = null;
            for (int i = 0; i < 20 && p == null; i++)
            {
                Pawn candidate = PawnGenerator.GeneratePawn(
                    new PawnGenerationRequest(
                        PawnKindDefOf.Colonist, Faction.OfPlayer,
                        forceGenerateNewPawn: true, dontGiveWeapon: true));
                if (candidate.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
                {
                    Find.WorldPawns.PassToWorld(
                        candidate, PawnDiscardDecideMode.Discard);
                    continue;
                }

                p = candidate;
            }

            GenSpawn.Spawn(p, spot, map);
            TractorStaging.Refresh(p);
            p.workSettings.EnableAndInitialize();
            foreach (WorkTypeDef work in DefDatabase<WorkTypeDef>.AllDefs)
            {
                if (!p.WorkTypeIsDisabled(work))
                {
                    p.workSettings.SetPriority(work, 0);
                }
            }

            p.workSettings.SetPriority(WorkTypeDefOf.Hauling, 1);
            return p;
        }

        /// <summary>
        /// Shoots a robot about: one wheel gone, then injuries to its
        /// chassis, emitter and remaining wheels, never more than two
        /// thirds of any part, until its health is below half.
        /// </summary>
        /// <param name="robot">The robot.</param>
        private static void BreakRobot(Pawn robot)
        {
            List<BodyPartRecord> parts =
                robot.health.hediffSet.GetNotMissingParts().ToList();
            BodyPartRecord wheel = parts.FirstOrDefault(
                p => p.def.defName == "BT_TractorWheel");
            if (wheel != null)
            {
                robot.health.AddHediff(HediffDefOf.MissingBodyPart, wheel);
            }

            float[] shares = { 0.65f, 0.6f, 0.3f };
            foreach (BodyPartRecord part in robot.health.hediffSet
                .GetNotMissingParts().ToList())
            {
                if (robot.Dead || robot.health.summaryHealth
                    .SummaryHealthPercent < 0.5f)
                {
                    break;
                }

                float share = part.def.defName == "BT_TractorChassis"
                    ? shares[0]
                    : part.def.defName == "BT_TractorBeamEmitter"
                        ? shares[1]
                        : part.def.defName == "BT_TractorWheel"
                            ? shares[2]
                            : 0f;
                if (share <= 0f)
                {
                    continue;
                }

                var cut = (Hediff_Injury)HediffMaker.MakeHediff(
                    HediffDefOf.Cut, robot, part);
                cut.Severity = part.def.GetMaxHealth(robot) * share;
                robot.health.AddHediff(cut, part);
            }
        }

        /// <summary>Whether a robot has no injury and no missing part.</summary>
        /// <param name="robot">The robot.</param>
        /// <returns>True if it is whole.</returns>
        private static bool Whole(Pawn robot) =>
            !robot.health.hediffSet.hediffs.Any(
                h => h is Hediff_Injury || h is Hediff_MissingPart);

        /// <summary>
        /// Hurries a barn's build along to its last ticks, once it has
        /// started: how long a build takes is checked elsewhere.
        /// </summary>
        /// <param name="barn">The barn.</param>
        private static void Hurry(Building_TractorBarn barn)
        {
            Bill_Autonomous bill = barn.CurrentBill;
            if (bill != null && bill.State == FormingState.Forming
                && bill.formingTicks > 2f)
            {
                bill.formingTicks = 2f;
            }
        }

        /// <summary>The player's robots on the map.</summary>
        /// <param name="map">The map.</param>
        /// <returns>The robots.</returns>
        private static IEnumerable<Pawn> Robots(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p =>
                p.Faction == Faction.OfPlayer
                && p.def == BT_TractorBombDefOf.BT_TractorBomb).ToList();

        /// <summary>The player's planted bombs on the map.</summary>
        /// <param name="map">The map.</param>
        /// <returns>The bombs.</returns>
        private static IEnumerable<Thing> PlantedBombs(Map map) =>
            map.listerThings.AllThings.Where(t =>
                t is Building_TractorBomb && t.Spawned
                && t.Faction == Faction.OfPlayer).ToList();

        /// <summary>What is loaded into a barn, by kind.</summary>
        /// <param name="barn">The barn.</param>
        /// <returns>The amount of each kind.</returns>
        private static Dictionary<ThingDef, int> Loaded(
            Building_TractorBarn barn) =>
            barn.innerContainer.GroupBy(t => t.def)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.stackCount));

        /// <summary>One kind's amount, or 0.</summary>
        /// <param name="amounts">The amounts.</param>
        /// <param name="def">The kind.</param>
        /// <returns>The amount.</returns>
        private static int Amount(Dictionary<ThingDef, int> amounts, ThingDef def) =>
            def != null && amounts.TryGetValue(def, out int n) ? n : 0;

        /// <summary>Amounts as text, for a check's detail.</summary>
        /// <param name="amounts">The amounts.</param>
        /// <returns>The text.</returns>
        private static string Describe(Dictionary<ThingDef, int> amounts) =>
            string.Join(", ", amounts.Select(kv => $"{kv.Key.defName} {kv.Value}"));

        /// <summary>Batshittium's def.</summary>
        private static ThingDef BatshittiumDef =>
            DefDatabase<ThingDef>.GetNamed("BT_Batshittium");
    }
}
