// RandomReplicator Self-Destruct system
// Pawn job + workgiver + DefOfs + textures + countdown overlay. Countdown and detonation live in CompRandomReplicator.

using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace RandomReplicator
{
    [DefOf]
    public static class RR_DesignationDefOf
    {
        public static DesignationDef RR_SelfDestruct;
        static RR_DesignationDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(RR_DesignationDefOf)); }
    }

    [DefOf]
    public static class RR_JobDefOf
    {
        public static JobDef RR_ArmSelfDestruct;
        static RR_JobDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(RR_JobDefOf)); }
    }

    // Vanilla textures, loaded on the main thread at startup.
    [StaticConstructorOnStartup]
    public static class RR_Textures
    {
        public static readonly Texture2D ArmSelfDestruct = ContentFinder<Texture2D>.Get("UI/Commands/Detonate");
        public static readonly Texture2D Cancel = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
        public static readonly Texture2D Eject = ContentFinder<Texture2D>.Get("UI/Commands/PodEject");
        public static readonly Texture2D Refrigeration = ContentFinder<Texture2D>.Get("UI/Commands/TempLower");
    }

    // Draws a flashing blast ring around every replicator counting down on the visible map, selected or not.
    // Buildings draw from the static map mesh, so their comps get no per-frame draw call of their own.
    public class MapComponent_SelfDestructOverlay : MapComponent
    {
        private readonly List<CompRandomReplicator> countingDown = new List<CompRandomReplicator>();

        public MapComponent_SelfDestructOverlay(Map map) : base(map) { }

        public void Register(CompRandomReplicator comp)
        {
            if (!countingDown.Contains(comp)) countingDown.Add(comp);
        }

        public void Deregister(CompRandomReplicator comp) => countingDown.Remove(comp);

        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap) return;
            for (int i = 0; i < countingDown.Count; i++) countingDown[i].DrawCountdownRing();
        }
    }

    public class WorkGiver_ArmSelfDestruct : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(RR_DesignationDefOf.RR_SelfDestruct);
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (var des in pawn.Map.designationManager.SpawnedDesignationsOfDef(RR_DesignationDefOf.RR_SelfDestruct))
            {
                var t = des.target.Thing;
                if (t != null && !t.Destroyed) yield return t;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Map == null || t.IsForbidden(pawn)) return false;
            if (t.Map.designationManager.DesignationOn(t, RR_DesignationDefOf.RR_SelfDestruct) == null) return false;
            if (!pawn.CanReserve(t, 1, -1, null, forced)) return false;

            var comp = t.TryGetComp<CompRandomReplicator>();
            return comp != null && !comp.IsSelfDestructCountingDown;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(RR_JobDefOf.RR_ArmSelfDestruct, t);
        }
    }

    public class JobDriver_ArmSelfDestruct : JobDriver
    {
        public Thing TargetThing => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetThing, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnThingMissingDesignation(TargetIndex.A, RR_DesignationDefOf.RR_SelfDestruct);

            int armingTicks = TargetThing.TryGetComp<CompRandomReplicator>()?.Props.selfDestructArmingTicks ?? 120;
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_General.WaitWith(TargetIndex.A, armingTicks, useProgressBar: true, face: TargetIndex.A, pathEndMode: PathEndMode.InteractionCell);

            Toil arm = ToilMaker.MakeToil("ArmSelfDestruct");
            arm.initAction = () =>
            {
                TargetThing.Map.designationManager.TryRemoveDesignationOn(TargetThing, RR_DesignationDefOf.RR_SelfDestruct);
                TargetThing.TryGetComp<CompRandomReplicator>()?.ArmSelfDestruct();
            };
            arm.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return arm;
        }
    }
}
