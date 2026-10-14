// Batshit Technology - the Tractor Bomb's mind.
// Its think tree (Defs/ThinkTreeDefs/BT_TractorBomb_ThinkTrees.xml) runs:
// hunt (JobGiver_TractorBombHunt: emplace if a hostile it can see is
// within its disposition's stopping distance, else approach it), otherwise
// patrol its allowed area
// (JobGiver_TractorBombPatrol). The constant tree runs the hunt every
// half second, so a patrolling robot breaks off the moment it spots one.
// JobDriver_TractorBombApproach closes in and retargets as hostiles move;
// JobDriver_TractorBombEmplace protests if need be, then emplaces. Below
// half health with nobody to hunt, it goes home to its Tractor Barn for
// repairs (TractorBarn.cs). The barn builds new ones.

using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BatshitTechnology
{
    /// <summary>
    /// The hunt: emplace if a hostile the robot can see is within its
    /// stopping distance (Cautious 15 tiles, Moderate one inside its
    /// tractor beam, Nosy 3), otherwise close in on the nearest one it can
    /// reach.
    /// </summary>
    public class JobGiver_TractorBombHunt : ThinkNode_JobGiver
    {
        /// <summary>
        /// In the constant tree: only break off a patrol or an idle wait;
        /// never second-guess an approach (its driver retargets) or an
        /// emplacement (which cannot be undone).
        /// </summary>
        public bool interruptOnly;

        /// <summary>Copies the node, its setting included.</summary>
        /// <param name="resolve">Whether to resolve the copy.</param>
        /// <returns>The copy.</returns>
        public override ThinkNode DeepCopy(bool resolve = true)
        {
            var copy = (JobGiver_TractorBombHunt)base.DeepCopy(resolve);
            copy.interruptOnly = interruptOnly;
            return copy;
        }

        /// <summary>An emplace or approach job, or none.</summary>
        /// <param name="pawn">The robot.</param>
        /// <returns>The job, or null.</returns>
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompTractorBomb comp = pawn?.TryGetComp<CompTractorBomb>();
            if (comp == null || !pawn.Spawned || pawn.Downed)
            {
                return null;
            }

            JobDef current = pawn.CurJobDef;
            if (current == BT_TractorBombDefOf.BT_TractorBombEmplace
                || (interruptOnly
                    && current == BT_TractorBombDefOf.BT_TractorBombApproach))
            {
                return null;
            }

            if (!TractorBombUtility.TryFindQuarry(
                pawn, comp, out Pawn target, out IntVec3 cell))
            {
                return null;
            }

            if (cell == pawn.Position)
            {
                return JobMaker.MakeJob(
                    BT_TractorBombDefOf.BT_TractorBombEmplace, target);
            }

            Job approach = JobMaker.MakeJob(
                BT_TractorBombDefOf.BT_TractorBombApproach, target, cell);
            approach.locomotionUrgency = LocomotionUrgency.Jog;
            return approach;
        }
    }

    /// <summary>
    /// The patrol: a walk to a random spot in its allowed area (the whole
    /// map by default), looking out for hostiles on the way.
    /// </summary>
    public class JobGiver_TractorBombPatrol : ThinkNode_JobGiver
    {
        /// <summary>How long it idles when there is nowhere to go.</summary>
        private const int WaitTicks = 120;

        /// <summary>A patrol leg, or a short wait.</summary>
        /// <param name="pawn">The robot.</param>
        /// <returns>The job.</returns>
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompTractorBomb comp = pawn?.TryGetComp<CompTractorBomb>();
            if (comp == null || !pawn.Spawned || pawn.Downed)
            {
                return null;
            }

            if (TractorBombUtility.TryFindPatrolCell(
                pawn, comp.Props.patrolLegRadius, out IntVec3 cell))
            {
                Job leg = JobMaker.MakeJob(
                    BT_TractorBombDefOf.BT_TractorBombPatrol, cell);
                leg.locomotionUrgency = LocomotionUrgency.Walk;
                return leg;
            }

            Job wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = WaitTicks;
            return wait;
        }
    }

    /// <summary>
    /// Closes in on a hostile (target A), driving to a cell within range
    /// (target B). Every half second it looks again: it retargets to the
    /// nearest hostile it can see, follows it if it moved, stops as soon
    /// as one is in range (the hunt then emplaces), and gives up if none
    /// is left.
    /// </summary>
    public class JobDriver_TractorBombApproach : JobDriver
    {
        /// <summary>Ticks between looks.</summary>
        private const int RecheckTicks = 30;

        /// <summary>Nothing to reserve: it only drives.</summary>
        /// <param name="errorOnFailed">Unused.</param>
        /// <returns>True.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            true;

        /// <summary>One toil: drive, looking again as it goes.</summary>
        /// <returns>The toil.</returns>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompTractorBomb comp = pawn.TryGetComp<CompTractorBomb>();
            this.FailOn(() => comp == null);
            Toil drive = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            drive.AddPreTickAction(() =>
            {
                if (pawn.IsHashIntervalTick(RecheckTicks))
                {
                    LookAgain(comp);
                }
            });
            yield return drive;
        }

        /// <summary>
        /// Retargets, follows, stops in range, or gives up.
        /// </summary>
        /// <param name="comp">The robot's comp.</param>
        private void LookAgain(CompTractorBomb comp)
        {
            if (comp == null || !pawn.Spawned)
            {
                return;
            }

            if (!TractorBombUtility.TryFindQuarry(
                pawn, comp, out Pawn target, out IntVec3 cell))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            if (cell == pawn.Position)
            {
                EndJobWith(JobCondition.Succeeded); // the hunt emplaces
                return;
            }

            if (target != job.targetA.Thing || cell != job.targetB.Cell)
            {
                job.targetA = target;
                job.targetB = cell;
                pawn.pather.StartPath(cell, PathEndMode.OnCell);
            }
        }
    }

    /// <summary>
    /// Ready to emplace: stands still, protests for three seconds if a
    /// colonist is inside its pull radius (beeping, its lamp blinking red
    /// and yellow), then emplaces anyway. Nothing interrupts it but its
    /// death.
    /// </summary>
    public class JobDriver_TractorBombEmplace : JobDriver
    {
        /// <summary>Nothing to reserve.</summary>
        /// <param name="errorOnFailed">Unused.</param>
        /// <returns>True.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            true;

        /// <summary>
        /// "Protesting" while it protests, else "emplacing".
        /// </summary>
        /// <returns>The job report.</returns>
        public override string GetReport() =>
            pawn.TryGetComp<CompTractorBomb>()?.Protesting == true
                ? "protesting: a colonist is inside its tractor beam."
                : base.GetReport();

        /// <summary>Stop and protest if need be, then emplace.</summary>
        /// <returns>The toils.</returns>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompTractorBomb comp = pawn.TryGetComp<CompTractorBomb>();
            this.FailOn(() => comp == null);

            Toil ready = ToilMaker.MakeToil("TractorBombReady");
            ready.initAction = () =>
            {
                pawn.pather.StopDead();
                comp.BeginEmplacing();
                if (comp.TickProtest())
                {
                    ReadyForNextToil(); // no protest: emplace at once
                }
            };
            ready.tickAction = () =>
            {
                if (comp.TickProtest())
                {
                    ReadyForNextToil();
                }
            };
            ready.defaultCompleteMode = ToilCompleteMode.Never;
            yield return ready;

            Toil emplace = ToilMaker.MakeToil("TractorBombEmplace");
            emplace.initAction = () => comp.Emplace();
            emplace.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return emplace;
        }
    }
}
