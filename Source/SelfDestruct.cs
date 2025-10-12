using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RandomReplicator;

/// <summary>
/// Provides typed access to XML definitions consumed by the mod.
/// </summary>
[DefOf]
public static class RRDefOf
{
    public static JobDef RR_ArmSelfDestruct = null!;
    public static DesignationDef RR_SelfDestruct = null!;
}

/// <summary>
/// Assigns pawns to arm the replicator's self-destruct charge.
/// </summary>
public sealed class WorkGiver_ArmSelfDestruct : WorkGiver_Scanner
{
    /// <inheritdoc />
    public override PathEndMode PathEndMode => PathEndMode.Touch;

    /// <inheritdoc />
    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.designationManager
            .SpawnedDesignationsOfDef(RRDefOf.RR_SelfDestruct)
            .Select(designation => designation.target.Thing)
            .Where(thing => thing != null);
    }

    /// <inheritdoc />
    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return !pawn.Map.designationManager
            .AnySpawnedDesignationOfDef(RRDefOf.RR_SelfDestruct);
    }

    /// <inheritdoc />
    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Building building)
        {
            return false;
        }

        var comp = building.TryGetComp<CompRandomReplicator>();
        if (comp == null || comp.IsSelfDestructArmed)
        {
            return false;
        }

        if (!comp.HasDesignation())
        {
            return false;
        }

        if (!pawn.CanReserve(t, 1, -1, null, forced))
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return JobMaker.MakeJob(RRDefOf.RR_ArmSelfDestruct, t);
    }
}

/// <summary>
/// Drives the warmup interaction that arms the self-destruct charge.
/// </summary>
public sealed class JobDriver_ArmSelfDestruct : JobDriver
{
    private const int ArmDurationTicks = 180;

    private CompRandomReplicator? ReplicatorComp => TargetThingA?
        .TryGetComp<CompRandomReplicator>();

    /// <inheritdoc />
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(TargetThingA, job, 1, -1, null, errorOnFailed);
    }

    /// <inheritdoc />
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnThingMissingDesignation(TargetIndex.A, RRDefOf.RR_SelfDestruct);
        this.FailOn(() => ReplicatorComp == null || ReplicatorComp.IsSelfDestructArmed);

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        var wait = Toils_General.Wait(ArmDurationTicks);
        wait.WithProgressBarToilDelay(TargetIndex.A);
        wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        wait.PlaySustainerOrSound(() => SoundDefOf.EnergyShieldAbsorbDamage);
        wait.AddPreTickAction(() => pawn.rotationTracker.FaceTarget(TargetThingA));
        wait.AddFinishAction(() => ReplicatorComp?.ArmSelfDestruct(pawn));
        yield return wait;
    }
}
