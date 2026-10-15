// Batshit Technology - everyone else runs (docs/TRACTOR-BOMB.md,
// "Everyone Else Runs" and "Making Peace"; roadmap step 6).
// JobGiver_FleeTractorBomb sits first in vanilla's constant think trees
// for humanlikes, animals (dryads included) and insects
// (Patches/BT_TractorBomb_Flee.xml), inside the gate that already turns
// away pawns in a mental state (berserk, manhunting), drafted pawns,
// downed pawns that cannot crawl, and burning or sleeping ones. The
// constant tree is the one asked every half second; the main tree is only
// asked when a job ends, too late for a three-second countdown.
//
// A pawn that can see an armed bomb and stands within its pull radius
// plus 8 tiles sprints to the nearest cell it can reach beyond that
// (JobDriver_TractorBombFlee). Anyone not hostile to the bomb (colonists,
// guests, neutrals, animals) does so at once; a hostile rolls once, on its
// first sight of that bomb, whether it runs at once, hesitates about two
// seconds, or ignores the bomb and carries on with its assault (the bomb
// keeps the roll; Core's TractorBombHostiles). One already inside the blast radius
// freezes where it stands, its status one of the twenty "making peace"
// lines (JobDriver_TractorBombFreeze), until the bomb goes off or its pull
// seizes it. Once the pull starts, whoever is inside it belongs to the
// implosion and is given nothing. Vanilla's own flee from an explosive
// (JobGiver_FleePotentialExplosion: 9 tiles, humanlikes only) never fires
// for the bomb, which does not announce itself the vanilla way; when both
// apply, this giver runs first and picks a cell clear of both. The
// arithmetic is Core's TractorBombFlee.

using System.Collections.Generic;
using BatshitTechnology.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace BatshitTechnology
{
    /// <summary>
    /// The "making peace" lines a pawn frozen inside a Tractor Bomb's
    /// blast radius shows as its status (docs/TRACTOR-BOMB.md, "Making
    /// Peace"). They sit on the freeze job's def so they can be translated:
    /// DefInjected path <c>BT_TractorBombFreeze.modExtensions.0.lines</c>.
    /// </summary>
    public class TractorBombFreezeLines : DefModExtension
    {
        /// <summary>
        /// The lines: lowercase, read after the pawn's name, without the
        /// full stop a job report ends with (it is added).
        /// </summary>
        [MustTranslate]
        [TranslationCanChangeCount]
        public List<string> lines = new List<string>();

        /// <summary>Reports a missing, blank or repeated line.</summary>
        /// <returns>One message per problem.</returns>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (lines == null || lines.Count == 0)
            {
                yield return "tractor bomb freeze lines are missing";
                yield break;
            }

            var seen = new HashSet<string>();
            foreach (string line in lines)
            {
                if (line.NullOrEmpty() || line.Trim().Length == 0)
                {
                    yield return "a tractor bomb freeze line is blank";
                }
                else if (!seen.Add(line.Trim()))
                {
                    yield return $"tractor bomb freeze line repeated: {line}";
                }
            }
        }
    }

    /// <summary>
    /// One danger a pawn is running from: where it is, how far is far
    /// enough, and the bomb behind it (null for a vanilla explosive).
    /// </summary>
    public readonly struct TractorBombDanger
    {
        /// <summary>Records a danger.</summary>
        /// <param name="cell">Where it is.</param>
        /// <param name="fleeDistance">How far to run, in tiles.</param>
        /// <param name="bomb">The bomb, or null.</param>
        public TractorBombDanger(
            IntVec3 cell, double fleeDistance, Building_TractorBomb bomb)
        {
            Cell = cell;
            FleeDistance = fleeDistance;
            Bomb = bomb;
        }

        /// <summary>Where it is.</summary>
        public IntVec3 Cell { get; }

        /// <summary>How far to run from it, in tiles.</summary>
        public double FleeDistance { get; }

        /// <summary>The bomb, or null for a vanilla explosive.</summary>
        public Building_TractorBomb Bomb { get; }
    }

    /// <summary>
    /// Everyone else's reaction to an armed Tractor Bomb, given from the
    /// constant think trees every half second: run beyond its pull radius
    /// plus 8 tiles, or freeze inside its blast radius, or nothing. A
    /// hostile reacts only as its once-rolled reaction allows
    /// (<see cref="Building_TractorBomb.ShouldRunFrom"/>).
    /// </summary>
    public class JobGiver_FleeTractorBomb : ThinkNode_JobGiver
    {
        /// <summary>
        /// A flee or freeze job; the pawn's own current one while it still
        /// fits, so nothing below this node in the tree interrupts it;
        /// otherwise null.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <returns>The job, or null.</returns>
        protected override Job TryGiveJob(Pawn pawn)
        {
            // Asked of every pawn twice a second: the cheapest test first,
            // and almost always the last (no bomb on the map at all).
            if (pawn?.Map == null
                || pawn.Map.listerThings.ThingsOfDef(
                    BT_TractorBombDefOf.BT_TractorBombEmplaced).Count == 0
                || !TractorBombFleeUtility.MayReact(pawn))
            {
                return null;
            }

            Job current = pawn.CurJob;
            var dangers = new List<TractorBombDanger>();
            Building_TractorBomb nearest = null;
            Building_TractorBomb freezeFor = null;
            int nearestDistance = int.MaxValue;
            int freezeDistance = int.MaxValue;
            foreach (Building_TractorBomb bomb in
                TractorBombUtility.ArmedBombs(pawn.Map))
            {
                IntVec3 offset = pawn.Position - bomb.Position;
                bool pulling = bomb.CurrentStage
                    == Building_TractorBomb.Stage.Imploding;
                TractorBombReaction reaction = TractorBombFlee.React(
                    offset.x, offset.z, bomb.PullRadius, bomb.BlastRadius,
                    pulling);
                if (reaction == TractorBombReaction.None)
                {
                    continue;
                }

                if (reaction == TractorBombReaction.Caught)
                {
                    return null; // the implosion has it
                }

                bool reacting =
                    TractorBombFleeUtility.ReactingTo(current, bomb);
                if (!reacting && !TractorBombFleeUtility.Sees(pawn, bomb))
                {
                    continue;
                }

                // A hostile that ignores it, or has not yet noticed it,
                // carries on; everyone else reacts.
                if (!reacting && !bomb.ShouldRunFrom(pawn))
                {
                    continue;
                }

                dangers.Add(new TractorBombDanger(
                    bomb.Position,
                    TractorBombFlee.FleeDistance(
                        bomb.PullRadius, bomb.BlastRadius),
                    bomb));
                int distance = offset.LengthHorizontalSquared;
                if (distance < nearestDistance)
                {
                    nearest = bomb;
                    nearestDistance = distance;
                }

                if (reaction == TractorBombReaction.Freeze
                    && distance < freezeDistance)
                {
                    freezeFor = bomb;
                    freezeDistance = distance;
                }
            }

            return nearest == null
                ? null
                : Decide(pawn, current, dangers, nearest, freezeFor);
        }

        /// <summary>
        /// Keeps running, freezes, or picks somewhere to run to. A pawn
        /// already running keeps running, even through a blast radius: it
        /// froze or ran when it first saw the bomb.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <param name="current">Its current job, or null.</param>
        /// <param name="dangers">What it is running from.</param>
        /// <param name="nearest">The nearest of those bombs.</param>
        /// <param name="freezeFor">
        /// The nearest bomb whose blast radius it is inside, or null.
        /// </param>
        /// <returns>The job, or null.</returns>
        private static Job Decide(
            Pawn pawn, Job current, List<TractorBombDanger> dangers,
            Building_TractorBomb nearest, Building_TractorBomb freezeFor)
        {
            TractorBombFleeUtility.AddKnownExploder(pawn, dangers);
            bool fleeing =
                current?.def == BT_TractorBombDefOf.BT_TractorBombFlee;
            if (fleeing && TractorBombFleeUtility.IsClearOfAll(
                current.targetA.Cell, dangers))
            {
                return current;
            }

            JobDef freeze = BT_TractorBombDefOf.BT_TractorBombFreeze;
            if (freezeFor != null && !fleeing)
            {
                return current?.def == freeze
                    && current.targetA.Thing == freezeFor
                    ? current
                    : JobMaker.MakeJob(freeze, freezeFor);
            }

            if (!TractorBombFleeUtility.TryFindFleeCell(
                pawn, dangers, out IntVec3 cell))
            {
                // Hemmed in: a runner keeps going, anyone else carries on.
                return fleeing ? current : null;
            }

            Job flee = JobMaker.MakeJob(
                BT_TractorBombDefOf.BT_TractorBombFlee, cell, nearest);
            flee.locomotionUrgency = LocomotionUrgency.Sprint;
            return flee;
        }
    }

    /// <summary>
    /// Sprints to a cell clear of an armed bomb (target A), from the bomb
    /// (target B). It ends when the pawn arrives, or as soon as the bomb
    /// has gone off. The implosion's pull ends it by moving the pawn.
    /// </summary>
    public class JobDriver_TractorBombFlee : JobDriver
    {
        /// <summary>
        /// Claims the destination, so two runners do not pick one cell.
        /// </summary>
        /// <param name="errorOnFailed">Unused.</param>
        /// <returns>True.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.Map.pawnDestinationReservationManager.Reserve(
                pawn, job, job.targetA.Cell);
            return true;
        }

        /// <summary>
        /// One toil: run, a colonist showing vanilla's flee icon.
        /// </summary>
        /// <returns>The toil.</returns>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddEndCondition(() =>
                TractorBombFleeUtility.StillArmed(job.targetB.Thing)
                    ? JobCondition.Ongoing
                    : JobCondition.Succeeded);
            Toil run = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            run.AddPreInitAction(() =>
            {
                if (pawn.IsColonist)
                {
                    MoteMaker.MakeColonistActionOverlay(
                        pawn, ThingDefOf.Mote_ColonistFleeing);
                }
            });
            run.FailOn(() => pawn.Downed && !pawn.health.CanCrawl);
            yield return run;
        }
    }

    /// <summary>
    /// Too close to run: stands where it is, facing the bomb (target A),
    /// its status one of the "making peace" lines, until the bomb goes off
    /// or its pull seizes the pawn (moving a pawn ends its job).
    /// </summary>
    public class JobDriver_TractorBombFreeze : JobDriver
    {
        /// <summary>Nothing to reserve.</summary>
        /// <param name="errorOnFailed">Unused.</param>
        /// <returns>True.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            true;

        /// <summary>
        /// One of the "making peace" lines, the same one for as long as
        /// this bomb holds this pawn; the def's report if there are none.
        /// </summary>
        /// <returns>The job report.</returns>
        public override string GetReport() =>
            TractorBombFleeUtility.FreezeReport(
                pawn, job.targetA.Thing, job.def)
            ?? base.GetReport();

        /// <summary>One toil: stop, face the bomb, and wait.</summary>
        /// <returns>The toil.</returns>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddEndCondition(() =>
                TractorBombFleeUtility.StillArmed(job.targetA.Thing)
                    ? JobCondition.Ongoing
                    : JobCondition.Succeeded);
            Toil freeze = ToilMaker.MakeToil("TractorBombFreeze");
            freeze.initAction = () => pawn.pather?.StopDead();
            freeze.tickAction = () =>
            {
                if (job.targetA.Thing is Thing bomb && bomb.Spawned)
                {
                    pawn.rotationTracker.FaceTarget(bomb);
                }
            };
            freeze.handlingFacing = true;
            freeze.defaultCompleteMode = ToilCompleteMode.Never;
            yield return freeze;
        }
    }

    /// <summary>
    /// Shared helpers for running from Tractor Bombs: who may react, what
    /// they can see, where they can run to, and what a frozen pawn says.
    /// </summary>
    public static class TractorBombFleeUtility
    {
        /// <summary>
        /// How many clear cells the search collects, nearest first, before
        /// it checks which of them the pawn may run to.
        /// </summary>
        private const int MaxCandidates = 16;

        /// <summary>
        /// How many cells the search may visit: Core's
        /// <see cref="TractorBombFlee.SearchCells"/>, far more than the
        /// circle the longest flee distance (41.75 tiles) leaves, so only a
        /// hemmed-in pawn runs out.
        /// </summary>
        private static readonly int MaxCellsSearched =
            TractorBombFlee.SearchCells;

        /// <summary>
        /// Whether a pawn is in a state to run or freeze at all: spawned,
        /// awake, not in a mental state, not drafted and not under a
        /// player's forced order, not stunned, and not downed unless it
        /// can crawl. A tractor bomb robot never runs from its own kind.
        /// The think tree's gate checks most of this too; this keeps the
        /// giver honest wherever another mod puts it.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <returns>True if it may react.</returns>
        public static bool MayReact(Pawn pawn) =>
            pawn != null && pawn.Spawned && !pawn.Dead && pawn.Map != null
            && !pawn.InMentalState && !pawn.Drafted
            && (!pawn.Downed || pawn.health.CanCrawl)
            && pawn.Awake()
            && pawn.stances?.stunner?.Stunned != true
            && !PawnUtility.PlayerForcedJobNowOrSoon(pawn)
            && pawn.TryGetComp<CompTractorBomb>() == null;

        /// <summary>
        /// Whether a pawn can see a bomb: a clear line of sight.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <param name="bomb">The bomb.</param>
        /// <returns>True if it can.</returns>
        public static bool Sees(Pawn pawn, Thing bomb) =>
            pawn?.Map != null && bomb != null && bomb.Map == pawn.Map
            && GenSight.LineOfSightToThing(
                pawn.Position, bomb, pawn.Map, skipFirstCell: true);

        /// <summary>
        /// Whether a pawn's job is already running from, or frozen by, a
        /// bomb; it need not still see it.
        /// </summary>
        /// <param name="job">The job, or null.</param>
        /// <param name="bomb">The bomb.</param>
        /// <returns>True if it is.</returns>
        public static bool ReactingTo(Job job, Thing bomb) =>
            job != null && bomb != null
            && ((job.def == BT_TractorBombDefOf.BT_TractorBombFlee
                    && job.targetB.Thing == bomb)
                || (job.def == BT_TractorBombDefOf.BT_TractorBombFreeze
                    && job.targetA.Thing == bomb));

        /// <summary>
        /// Whether a thing is a bomb still armed on its map.
        /// </summary>
        /// <param name="thing">The thing, or null.</param>
        /// <returns>True if it is.</returns>
        public static bool StillArmed(Thing thing) =>
            thing is Building_TractorBomb bomb && bomb.Spawned
            && bomb.IsArmed;

        /// <summary>
        /// Adds the vanilla explosive a pawn already knows is about to go
        /// off (a lit grenade, a wick), if it is within vanilla's 9-tile
        /// flee distance, so one destination clears both.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <param name="dangers">The dangers, added to.</param>
        public static void AddKnownExploder(
            Pawn pawn, List<TractorBombDanger> dangers)
        {
            Thing exploder = pawn?.mindState?.knownExploder;
            if (dangers == null || exploder == null || !exploder.Spawned
                || exploder.Map != pawn.Map
                || exploder is Building_TractorBomb
                || !pawn.Position.InHorDistOf(
                    exploder.Position,
                    JobGiver_FleePotentialExplosion.FleeDist))
            {
                return;
            }

            dangers.Add(new TractorBombDanger(
                exploder.Position, JobGiver_FleePotentialExplosion.FleeDist,
                null));
        }

        /// <summary>
        /// Whether a cell is clear of every danger: more than a tile beyond
        /// each one's flee distance.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <param name="dangers">The dangers.</param>
        /// <returns>True if it is.</returns>
        public static bool IsClearOfAll(
            IntVec3 cell, List<TractorBombDanger> dangers)
        {
            if (!cell.IsValid || dangers == null)
            {
                return false;
            }

            foreach (TractorBombDanger danger in dangers)
            {
                if (!TractorBombFlee.IsClear(
                    cell.x - danger.Cell.x, cell.z - danger.Cell.z,
                    danger.FleeDistance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The nearest cell, by steps, that is clear of every danger and
        /// that the pawn may run to (vanilla's own test for a flee
        /// destination: standable, not claimed, not a doorway or dangerous
        /// ground, within reach of a rope, reachable).
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <param name="dangers">What it is running from.</param>
        /// <param name="cell">The destination.</param>
        /// <returns>False if there is none within the search.</returns>
        public static bool TryFindFleeCell(
            Pawn pawn, List<TractorBombDanger> dangers, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (pawn?.Map == null || dangers == null || dangers.Count == 0)
            {
                return false;
            }

            // Flood out from the pawn over ground it can cross, collecting
            // clear cells nearest first; then check them outside the flood
            // (reachability must not run inside the shared flood filler).
            Map map = pawn.Map;
            var candidates = new List<IntVec3>(MaxCandidates);
            map.floodFiller.FloodFill(
                pawn.Position,
                c => CanCross(c, map, pawn),
                c =>
                {
                    if (c.Standable(map) && IsClearOfAll(c, dangers))
                    {
                        candidates.Add(c);
                    }

                    return candidates.Count >= MaxCandidates;
                },
                MaxCellsSearched);
            foreach (IntVec3 c in candidates)
            {
                if (CellFinderLoose.CanFleeToLocation(pawn, c))
                {
                    cell = c;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The status of a pawn frozen by a bomb: one of the job def's
        /// "making peace" lines, picked per pawn and bomb, as a report.
        /// </summary>
        /// <param name="pawn">The frozen pawn.</param>
        /// <param name="bomb">The bomb; may be null.</param>
        /// <param name="def">The freeze job's def.</param>
        /// <returns>The report, or null if there are no lines.</returns>
        public static string FreezeReport(Pawn pawn, Thing bomb, JobDef def)
        {
            List<string> lines =
                def?.GetModExtension<TractorBombFreezeLines>()?.lines;
            if (pawn == null || lines == null || lines.Count == 0)
            {
                return null;
            }

            int index = TractorBombFlee.LineIndex(
                pawn.thingIDNumber, bomb?.thingIDNumber ?? 0, lines.Count);
            string report = TractorBombFlee.AsReport(lines[index]);
            return report.Length == 0 ? null : report;
        }

        /// <summary>
        /// Whether a running pawn can cross a cell: walkable for it, and
        /// not a door it cannot get through.
        /// </summary>
        /// <param name="c">The cell.</param>
        /// <param name="map">The map.</param>
        /// <param name="pawn">The pawn.</param>
        /// <returns>True if it can.</returns>
        private static bool CanCross(IntVec3 c, Map map, Pawn pawn)
        {
            if (!c.WalkableBy(map, pawn))
            {
                return false;
            }

            Building_Door door = c.GetDoor(map);
            return door == null || door.CanPhysicallyPass(pawn);
        }
    }
}
