// RandomReplicator Self-Destruct system
// Pawn job + workgiver + DefOfs + textures + countdown overlay. Countdown and
// detonation live in CompRandomReplicator.

using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace RandomReplicator
{
    /// <summary>The self-destruct order's designation.</summary>
    [DefOf]
    public static class RR_DesignationDefOf
    {
        /// <summary>Marks a replicator for a colonist to arm.</summary>
        public static DesignationDef RR_SelfDestruct;

        /// <summary>Lets RimWorld bind the field above at load.</summary>
        static RR_DesignationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RR_DesignationDefOf));
        }
    }

    /// <summary>The self-destruct arming job.</summary>
    [DefOf]
    public static class RR_JobDefOf
    {
        /// <summary>A colonist arming a replicator's self-destruct.</summary>
        public static JobDef RR_ArmSelfDestruct;

        /// <summary>Lets RimWorld bind the field above at load.</summary>
        static RR_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RR_JobDefOf));
        }
    }

    /// <summary>Vanilla textures, loaded on the main thread at startup.</summary>
    [StaticConstructorOnStartup]
    public static class RR_Textures
    {
        /// <summary>Arm self-destruct.</summary>
        public static readonly Texture2D ArmSelfDestruct =
            ContentFinder<Texture2D>.Get("UI/Commands/Detonate");

        /// <summary>Cancel the order or abort the countdown.</summary>
        public static readonly Texture2D Cancel =
            ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        /// <summary>Eject food, and the auto-eject toggle.</summary>
        public static readonly Texture2D Eject =
            ContentFinder<Texture2D>.Get("UI/Commands/PodEject");

        /// <summary>The refrigeration toggle.</summary>
        public static readonly Texture2D Refrigeration =
            ContentFinder<Texture2D>.Get("UI/Commands/TempLower");

        /// <summary>The Ballista's range picker.</summary>
        public static readonly Texture2D BallistaRange =
            ContentFinder<Texture2D>.Get("UI/Commands/Attack");

        /// <summary>The Ballista's projectile-count picker.</summary>
        public static readonly Texture2D BallistaCount =
            ContentFinder<Texture2D>.Get("UI/Commands/SquadAttack");
    }

    /// <summary>
    /// Draws a flashing blast ring around every replicator counting down on
    /// the visible map, selected or not. Buildings draw from the static map
    /// mesh, so their comps get no per-frame draw call of their own.
    /// </summary>
    public class MapComponent_SelfDestructOverlay : MapComponent
    {
        private readonly List<CompRandomReplicator> countingDown =
            new List<CompRandomReplicator>();

        /// <summary>Created by RimWorld for every map.</summary>
        /// <param name="map">The map this component belongs to.</param>
        public MapComponent_SelfDestructOverlay(Map map)
            : base(map)
        {
        }

        /// <summary>Starts drawing a replicator's countdown ring.</summary>
        /// <param name="comp">The replicator counting down.</param>
        public void Register(CompRandomReplicator comp)
        {
            if (!countingDown.Contains(comp))
            {
                countingDown.Add(comp);
            }
        }

        /// <summary>Stops drawing a replicator's countdown ring.</summary>
        /// <param name="comp">The replicator no longer counting down.</param>
        public void Deregister(CompRandomReplicator comp) =>
            countingDown.Remove(comp);

        /// <summary>Draws every registered ring on the visible map.</summary>
        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap)
            {
                return;
            }

            for (int i = 0; i < countingDown.Count; i++)
            {
                countingDown[i].DrawCountdownRing();
            }
        }
    }

    /// <summary>
    /// Finds replicators designated for self-destruct and has a colonist
    /// arm them from the interaction spot.
    /// </summary>
    public class WorkGiver_ArmSelfDestruct : WorkGiver_Scanner
    {
        /// <summary>Scans artificial buildings.</summary>
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        /// <summary>Colonists work from the interaction cell.</summary>
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        /// <summary>Skips the scan when nothing is designated.</summary>
        /// <param name="pawn">The colonist looking for work.</param>
        /// <param name="forced">True for a player-forced job.</param>
        /// <returns>True when there is no work.</returns>
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(
                RR_DesignationDefOf.RR_SelfDestruct);
        }

        /// <summary>Only the designated replicators.</summary>
        /// <param name="pawn">The colonist looking for work.</param>
        /// <returns>Designated, intact things.</returns>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            IEnumerable<Designation> designations =
                pawn.Map.designationManager.SpawnedDesignationsOfDef(
                    RR_DesignationDefOf.RR_SelfDestruct);
            foreach (var des in designations)
            {
                var t = des.target.Thing;
                if (t != null && !t.Destroyed)
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Whether this colonist can arm this replicator now: designated,
        /// reachable, reservable, and not already counting down.
        /// </summary>
        /// <param name="pawn">The colonist.</param>
        /// <param name="t">The replicator.</param>
        /// <param name="forced">True for a player-forced job.</param>
        /// <returns>True if there is a job to do.</returns>
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Map == null || t.IsForbidden(pawn))
            {
                return false;
            }

            var designation = t.Map.designationManager.DesignationOn(
                t, RR_DesignationDefOf.RR_SelfDestruct);
            if (designation == null || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            var comp = t.TryGetComp<CompRandomReplicator>();
            return comp != null && !comp.IsSelfDestructCountingDown;
        }

        /// <summary>Creates the arming job.</summary>
        /// <param name="pawn">The colonist.</param>
        /// <param name="t">The replicator.</param>
        /// <param name="forced">True for a player-forced job.</param>
        /// <returns>The job.</returns>
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(RR_JobDefOf.RR_ArmSelfDestruct, t);
        }
    }

    /// <summary>
    /// Walks to the interaction spot, works for the arming time, then
    /// starts the countdown and clears the order.
    /// </summary>
    public class JobDriver_ArmSelfDestruct : JobDriver
    {
        /// <summary>The replicator being armed.</summary>
        public Thing TargetThing => job.targetA.Thing;

        /// <summary>Reserves the replicator so only one colonist arms it.</summary>
        /// <param name="errorOnFailed">Log an error if it fails.</param>
        /// <returns>True if reserved.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetThing, job, 1, -1, null, errorOnFailed);
        }

        /// <summary>Go there, work, arm.</summary>
        /// <returns>The job's toils.</returns>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnThingMissingDesignation(
                TargetIndex.A, RR_DesignationDefOf.RR_SelfDestruct);

            int armingTicks = TargetThing.TryGetComp<CompRandomReplicator>()?
                .Props.selfDestructArmingTicks ?? 120;
            yield return Toils_Goto.GotoThing(
                TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_General.WaitWith(
                TargetIndex.A, armingTicks, useProgressBar: true,
                face: TargetIndex.A, pathEndMode: PathEndMode.InteractionCell);

            Toil arm = ToilMaker.MakeToil("ArmSelfDestruct");
            arm.initAction = () =>
            {
                TargetThing.Map.designationManager.TryRemoveDesignationOn(
                    TargetThing, RR_DesignationDefOf.RR_SelfDestruct);
                TargetThing.TryGetComp<CompRandomReplicator>()?.ArmSelfDestruct();
            };
            arm.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return arm;
        }
    }
}
