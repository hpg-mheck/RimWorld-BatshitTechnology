// Batshit Technology - the Tractor Barn (docs/TRACTOR-BOMB.md, "The
// Tractor Barn"; roadmap step 8). It builds Tractor Bombs on its own and
// repairs the damaged ones that come home.
//
// Production is vanilla's autonomous work table (Building_WorkTableAutonomous,
// Biotech's mech gestator pattern, whose code is in the base game and works
// with Core alone). Its bills are ordinary ones, "Do X times" or "Do until
// you have X", the second counting the faction's robots and planted bombs
// on the map (RecipeWorkerCounter_TractorBombs). A hauler takes the bill:
// it carries the ingredients into the barn and throws the switch, which
// starts the build (the bill's "forming"); the barn then builds the robot
// by itself, and when it is done drives it out of the door and closes the
// bill's iteration itself, so no crafter ever comes. "Build one now" adds a
// "Do 1 times" bill.
//
// Repair: a robot below half health with nobody to hunt drives home
// (JobGiver_TractorBombRepair), parks at the barn's door
// (JobDriver_TractorBombDock) and is mended a hit point at a time, steel
// coming out of the barn's repair hopper, which haulers keep stocked (a
// vanilla CompRefuelable). The rates are Core's TractorBarnRepair.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using BatshitTechnology.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace BatshitTechnology
{
    /// <summary>The Tractor Barn's defs, bound by RimWorld at load.</summary>
    [DefOf]
    public static class BT_TractorBarnDefOf
    {
        /// <summary>The barn.</summary>
        public static ThingDef BT_TractorBarn;

        /// <summary>Its one recipe: a tractor bomb.</summary>
        public static RecipeDef BT_BuildTractorBomb;

        /// <summary>A damaged robot driving home and parking for repair.</summary>
        public static JobDef BT_TractorBombDock;

        /// <summary>Lets RimWorld bind the fields above at load.</summary>
        static BT_TractorBarnDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_TractorBarnDefOf));
        }
    }

    /// <summary>What one repair step came to.</summary>
    public enum TractorBarnRepairResult
    {
        /// <summary>
        /// Nothing to do with it: no barn on the map, or no robot there.
        /// </summary>
        Invalid,

        /// <summary>It mended something; more remains.</summary>
        Working,

        /// <summary>The hopper cannot pay for the next step.</summary>
        NoSteel,

        /// <summary>The robot is whole.</summary>
        Done,
    }

    /// <summary>
    /// The Tractor Barn: an autonomous work table that builds tractor bombs
    /// from hauled-in ingredients with no crafter, and a repair dock for
    /// the damaged ones.
    /// </summary>
    public class Building_TractorBarn : Building_WorkTableAutonomous
    {
        private CompRefuelable hopper;

        /// <summary>Robots the barns have built this session (diagnostics).</summary>
        public static int Built { get; private set; }

        /// <summary>
        /// Steel the barns have spent on repairs this session
        /// (diagnostics).
        /// </summary>
        public static float RepairSteelUsed { get; private set; }

        /// <summary>The repair hopper: steel for mending robots.</summary>
        public CompRefuelable RepairHopper =>
            hopper ??= GetComp<CompRefuelable>();

        /// <summary>The bill it is working on, if any.</summary>
        public Bill_Autonomous CurrentBill => activeBill;

        /// <summary>
        /// The robot parked at its door being repaired, or null.
        /// </summary>
        public Pawn DockedRobot =>
            Spawned ? DockClaimants().FirstOrDefault(
                p => p.Position == InteractionCell) : null;

        /// <summary>Caches the repair hopper.</summary>
        /// <param name="map">The map.</param>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            hopper = GetComp<CompRefuelable>();
        }

        /// <summary>
        /// A new "Do 1 times" bill for a tractor bomb, at the bottom of its
        /// list: what "Build one now" does.
        /// </summary>
        /// <returns>The bill, or null if it has no such recipe.</returns>
        public Bill AddBuildBill()
        {
            RecipeDef recipe = BT_TractorBarnDefOf.BT_BuildTractorBomb;
            if (recipe == null || !def.AllRecipes.Contains(recipe))
            {
                return null;
            }

            Bill bill = recipe.MakeNewBill();
            if (bill is Bill_Production production)
            {
                production.repeatMode = BillRepeatModeDefOf.RepeatCount;
                production.repeatCount = 1;
            }

            billStack.AddBill(bill);
            return bill;
        }

        /// <summary>
        /// The build is done: the robot drives out of the door, the
        /// ingredients are used up, and the bill's iteration closes here,
        /// so no crafter ever has to come and finish it.
        /// </summary>
        public override void Notify_FormingCompleted()
        {
            Bill_Autonomous bill = activeBill;
            var used = innerContainer.ToList();
            Faction owner = Faction ?? Faction.OfPlayer;
            Pawn robot = Spawned
                ? TractorBombUtility.SpawnRobot(Map, InteractionCell, owner)
                : null;
            innerContainer.ClearAndDestroyContents();
            if (robot != null)
            {
                Built++;
                robot.Rotation = Rot4.South;
                if (owner == Faction.OfPlayer)
                {
                    Messages.Message(
                        $"{LabelCap} has built a tractor bomb. It is already "
                            + "looking for someone to drive at.",
                        robot, MessageTypeDefOf.PositiveEvent);
                }
            }

            // Resets the bill, counts the iteration (a "Do X times" bill
            // goes down by one) and frees the barn for its next bill.
            bill?.Notify_IterationCompleted(null, used);
            if (activeBill == bill)
            {
                activeBill = null;
            }
        }

        /// <summary>
        /// One tick of the build. Should a bill ever sit finished (a save
        /// edited by hand), it is closed here rather than waiting for a
        /// crafter who is never coming.
        /// </summary>
        protected override void Tick()
        {
            base.Tick();
            if (Spawned && activeBill != null
                && activeBill.State == FormingState.Formed)
            {
                Notify_FormingCompleted();
            }
        }

        /// <summary>
        /// One repair step on a robot at the door: mends one hit point of
        /// its lightest injury or, once none is left, refits one missing
        /// part, paying for it from the repair hopper.
        /// </summary>
        /// <param name="robot">The robot.</param>
        /// <returns>What came of it.</returns>
        public TractorBarnRepairResult RepairStep(Pawn robot)
        {
            if (!Spawned || robot == null || robot.Dead || !robot.Spawned
                || robot.Map != Map)
            {
                return TractorBarnRepairResult.Invalid;
            }

            Hediff next = NextRepair(robot);
            if (next == null)
            {
                return TractorBarnRepairResult.Done;
            }

            bool refit = next is Hediff_MissingPart;
            float cost = TractorBarnRepair.StepCost(refit);
            CompRefuelable steel = RepairHopper;
            if (steel == null
                || !TractorBarnRepair.CanAfford(steel.Fuel, cost))
            {
                return TractorBarnRepairResult.NoSteel;
            }

            steel.ConsumeFuel(cost);
            RepairSteelUsed += cost;
            if (refit)
            {
                robot.health.RemoveHediff(next);
            }
            else
            {
                next.Heal(TractorBarnRepair.HitPointsPerStep);
            }

            if (robot.IsHashIntervalTick(TractorBarnRepair.TicksPerStep * 2))
            {
                FleckMaker.ThrowMicroSparks(robot.DrawPos, Map);
            }

            return NextRepair(robot) == null
                ? TractorBarnRepairResult.Done
                : TractorBarnRepairResult.Working;
        }

        /// <summary>
        /// Whether a robot may park at the door: no other robot is on its
        /// way there or parked there.
        /// </summary>
        /// <param name="robot">The robot asking.</param>
        /// <returns>True if it may.</returns>
        public bool IsDockFreeFor(Pawn robot) =>
            Spawned && !DockClaimants().Any(p => p != robot);

        /// <summary>
        /// What the barn is doing: loading, building or repairing.
        /// </summary>
        /// <returns>The extra inspect lines, or null.</returns>
        protected override string GetInspectStringExtra()
        {
            var text = new StringBuilder();
            if (activeBill != null
                && activeBill.State == FormingState.Gathering)
            {
                text.AppendLine("Loading a tractor bomb's parts:");
                activeBill.AppendCurrentIngredientCount(text);
            }
            else if (activeBill != null
                && activeBill.State == FormingState.Forming)
            {
                text.AppendLine("Building a tractor bomb.");
            }

            Pawn docked = DockedRobot;
            if (docked != null)
            {
                bool broke = !TractorBarnRepair.CanAfford(
                    RepairHopper?.Fuel ?? 0f,
                    TractorBarnRepair.StepCost(false));
                text.AppendLine(broke
                    ? $"Waiting for repair steel to mend {docked.LabelShort}."
                    : $"Repairing {docked.LabelShort}: "
                        + docked.health.summaryHealth.SummaryHealthPercent
                            .ToStringPercent()
                        + " health.");
            }

            return text.ToString().TrimEndNewlines();
        }

        /// <summary>
        /// Its controls: "Build one now", which adds a "Do 1 times" bill.
        /// </summary>
        /// <returns>The gizmos.</returns>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            if (Faction != Faction.OfPlayer)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "Build one now",
                defaultDesc = "Add a bill to build one tractor bomb: the same "
                    + "as a \"Do 1 times\" bill on the Bills tab. Haulers "
                    + "bring the parts, and the barn does the rest.",
                icon = BT_TractorBombDefOf.BT_TractorBomb?.uiIcon
                    ?? BaseContent.BadTex,
                action = () => AddBuildBill(),
            };
        }

        /// <summary>
        /// The next thing to repair: the lightest injury first, then a
        /// missing part whose parent is whole, as vanilla's mech repair
        /// does.
        /// </summary>
        /// <param name="robot">The robot.</param>
        /// <returns>The hediff, or null if it is whole.</returns>
        private static Hediff NextRepair(Pawn robot)
        {
            List<Hediff> hediffs = robot.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return null;
            }

            Hediff lightest = null;
            foreach (Hediff h in hediffs)
            {
                if (h is Hediff_Injury
                    && (lightest == null || h.Severity < lightest.Severity))
                {
                    lightest = h;
                }
            }

            if (lightest != null)
            {
                return lightest;
            }

            foreach (Hediff h in hediffs)
            {
                if (h is Hediff_MissingPart && (h.Part?.parent == null
                    || !robot.health.hediffSet.PartIsMissing(h.Part.parent)))
                {
                    return h;
                }
            }

            return null;
        }

        /// <summary>
        /// The robots on this map heading for, or parked at, this barn's
        /// door.
        /// </summary>
        /// <returns>The robots.</returns>
        private IEnumerable<Pawn> DockClaimants()
        {
            if (Map == null)
            {
                yield break;
            }

            foreach (Pawn p in Map.mapPawns.AllPawnsSpawned)
            {
                Job job = p.CurJob;
                if (job != null
                    && job.def == BT_TractorBarnDefOf.BT_TractorBombDock
                    && job.targetA.Thing == this)
                {
                    yield return p;
                }
            }
        }
    }

    /// <summary>
    /// Counts what "Do until you have X" means at the Tractor Barn: the
    /// barn owner's tractor bombs on its map, robots and planted bombs
    /// alike (<see cref="TractorBombUtility.CountOnMap"/>), so the barn
    /// keeps a standing patrol topped up.
    /// </summary>
    public class RecipeWorkerCounter_TractorBombs : RecipeWorkerCounter
    {
        /// <summary>It can always count.</summary>
        /// <param name="bill">The bill.</param>
        /// <returns>True.</returns>
        public override bool CanCountProducts(Bill_Production bill) => true;

        /// <summary>The owner's robots and planted bombs on the map.</summary>
        /// <param name="bill">The bill.</param>
        /// <returns>How many.</returns>
        public override int CountProducts(Bill_Production bill)
        {
            Map map = bill?.Map;
            Faction owner = (bill?.billStack?.billGiver as Thing)?.Faction
                ?? Faction.OfPlayer;
            return TractorBombUtility.CountOnMap(map, owner);
        }

        /// <summary>What is counted, for the bill dialog.</summary>
        /// <param name="bill">The bill.</param>
        /// <returns>The description.</returns>
        public override string ProductsDescription(Bill_Production bill) =>
            "tractor bombs on this map, planted ones included";

        /// <summary>
        /// A robot drives itself away, so no stockpile setting can be
        /// wrong for it.
        /// </summary>
        /// <param name="bill">The bill.</param>
        /// <param name="slotGroup">The stockpile.</param>
        /// <returns>True.</returns>
        public override bool CanPossiblyStore(
            Bill_Production bill, ISlotGroup slotGroup) => true;
    }

    /// <summary>
    /// A damaged robot with nobody to hunt goes home: below half health
    /// (Core's <see cref="TractorBarnRepair.ShouldSeekRepair"/>), it drives
    /// to the nearest barn of its own whose door is free, leaving its patrol
    /// area if it must.
    /// </summary>
    public class JobGiver_TractorBombRepair : ThinkNode_JobGiver
    {
        /// <summary>
        /// In the constant tree: only break off a patrol leg or an idle
        /// wait, so a robot shot mid-leg turns for home at once.
        /// </summary>
        public bool interruptOnly;

        /// <summary>Copies the node, its setting included.</summary>
        /// <param name="resolve">Whether to resolve the copy.</param>
        /// <returns>The copy.</returns>
        public override ThinkNode DeepCopy(bool resolve = true)
        {
            var copy = (JobGiver_TractorBombRepair)base.DeepCopy(resolve);
            copy.interruptOnly = interruptOnly;
            return copy;
        }

        /// <summary>A docking job, or none.</summary>
        /// <param name="pawn">The robot.</param>
        /// <returns>The job, or null.</returns>
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.TryGetComp<CompTractorBomb>() == null || !pawn.Spawned
                || pawn.Downed)
            {
                return null;
            }

            JobDef current = pawn.CurJobDef;
            if (current == BT_TractorBarnDefOf.BT_TractorBombDock
                || (interruptOnly
                    && current != BT_TractorBombDefOf.BT_TractorBombPatrol
                    && current != JobDefOf.Wait))
            {
                return null;
            }

            if (!TractorBarnRepair.ShouldSeekRepair(
                pawn.health.summaryHealth.SummaryHealthPercent))
            {
                return null;
            }

            Building_TractorBarn barn = TractorBarnUtility.FindDock(pawn);
            if (barn == null)
            {
                return null;
            }

            Job dock = JobMaker.MakeJob(
                BT_TractorBarnDefOf.BT_TractorBombDock, barn);
            dock.locomotionUrgency = LocomotionUrgency.Jog;
            return dock;
        }
    }

    /// <summary>
    /// Drives home to a barn (target A), parks at its door and is repaired
    /// until whole, waiting there if the repair hopper runs dry. Spotting a
    /// hostile breaks it off (the constant tree's hunt).
    /// </summary>
    public class JobDriver_TractorBombDock : JobDriver
    {
        private int stepTicks;
        private bool waitingForSteel;

        /// <summary>The barn.</summary>
        private Building_TractorBarn Barn =>
            job.targetA.Thing as Building_TractorBarn;

        /// <summary>
        /// Whether it is parked at the door (its second toil).
        /// </summary>
        public bool Docked => CurToilIndex >= 1;

        /// <summary>
        /// Nothing to reserve: the barn's haulers share its door.
        /// </summary>
        /// <param name="errorOnFailed">Unused.</param>
        /// <returns>True.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            true;

        /// <summary>Its status line: on the way, mending, or waiting.</summary>
        /// <returns>The job report.</returns>
        public override string GetReport()
        {
            if (!Docked)
            {
                return base.GetReport();
            }

            return waitingForSteel
                ? "waiting at the tractor barn for repair steel."
                : "being repaired at the tractor barn.";
        }

        /// <summary>Saves its step timer.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stepTicks, "stepTicks");
            Scribe_Values.Look(ref waitingForSteel, "waitingForSteel");
        }

        /// <summary>Drive to the door, then park and be mended.</summary>
        /// <returns>The toils.</returns>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Barn == null || !Barn.Spawned
                || Barn.Faction != pawn.Faction || Barn.IsBurning());
            yield return Toils_Goto.GotoThing(
                TargetIndex.A, PathEndMode.InteractionCell);

            Toil docked = ToilMaker.MakeToil("TractorBombDocked");
            docked.initAction = () =>
            {
                pawn.pather?.StopDead();
                stepTicks = 0;
            };
            docked.tickAction = () =>
            {
                Building_TractorBarn barn = Barn;
                if (barn == null)
                {
                    return;
                }

                pawn.rotationTracker.FaceCell(barn.Position);
                if (++stepTicks < TractorBarnRepair.TicksPerStep)
                {
                    return;
                }

                stepTicks = 0;
                switch (barn.RepairStep(pawn))
                {
                    case TractorBarnRepairResult.Done:
                        waitingForSteel = false;
                        EndJobWith(JobCondition.Succeeded);
                        break;
                    case TractorBarnRepairResult.NoSteel:
                        waitingForSteel = true;
                        break;
                    case TractorBarnRepairResult.Invalid:
                        EndJobWith(JobCondition.Incompletable);
                        break;
                    default:
                        waitingForSteel = false;
                        break;
                }
            };
            docked.handlingFacing = true;
            docked.defaultCompleteMode = ToilCompleteMode.Never;
            yield return docked;
        }
    }

    /// <summary>Finding a robot a barn to go home to.</summary>
    public static class TractorBarnUtility
    {
        /// <summary>
        /// The nearest barn of the robot's own faction on its map, not on
        /// fire, whose door is free (or already the robot's) and that it
        /// can reach.
        /// </summary>
        /// <param name="robot">The robot.</param>
        /// <returns>The barn, or null if there is none.</returns>
        public static Building_TractorBarn FindDock(Pawn robot)
        {
            Map map = robot?.Map;
            if (map == null || robot.Faction == null)
            {
                return null;
            }

            Building_TractorBarn best = null;
            int bestDistance = int.MaxValue;
            foreach (Thing t in map.listerThings.ThingsOfDef(
                BT_TractorBarnDefOf.BT_TractorBarn))
            {
                if (!(t is Building_TractorBarn barn) || !barn.Spawned
                    || barn.Faction != robot.Faction || barn.IsBurning()
                    || !barn.IsDockFreeFor(robot))
                {
                    continue;
                }

                int distance = (barn.InteractionCell - robot.Position)
                    .LengthHorizontalSquared;
                if (distance < bestDistance && robot.CanReach(
                    barn.InteractionCell, PathEndMode.OnCell, Danger.Deadly))
                {
                    best = barn;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
