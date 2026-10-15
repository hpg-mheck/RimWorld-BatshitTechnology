// Batshit Technology - the tour group (docs/SHOWCASE.md, "Tourists";
// slice 19000c, request 21 as the operator settled it on 2026-10-15).
// Three to five visitors of BT_ShowcaseTourists, a hidden faction made
// for showcases (Defs/Misc/BT_Tourists.xml), arrive with the tour's stage
// near the base. One lord keeps them (a non-colonist without one walks
// off the map), and its duty has no combat in it: each tourist wanders
// near a spot the director sets. The director walks them from scene to
// scene (Core's TourGroupPlan says where): sightseers to the stampede's
// keep-away line and beside the pads (where they stay for the look when
// the device is armed), the volunteer into the grenade's pile, the stray
// into the tractor's pull. When the showcase finishes,
// stops or fails, every tourist goes, alive or dead, with corpse and
// clothes, and the group says what became of each.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace BatshitTechnology
{
    /// <summary>The tour group's defs, bound by RimWorld at load.</summary>
    [DefOf]
    public static class BT_TouristDefOf
    {
        /// <summary>The hidden faction made for showcases.</summary>
        public static FactionDef BT_ShowcaseTourists;

        /// <summary>
        /// The tourists' pawn kind. It shares its defName with the duty
        /// (they are different def types), so the field is aliased.
        /// </summary>
        [DefAlias("BT_ShowcaseTourist")]
        public static PawnKindDef BT_ShowcaseTouristKind;

        /// <summary>The tourists' duty: wander near a spot, no combat.</summary>
        [DefAlias("BT_ShowcaseTourist")]
        public static DutyDef BT_ShowcaseTouristDuty;

        /// <summary>Tour composure: no mental breaks, steady mood, no hunger.</summary>
        public static HediffDef BT_TourComposure;

        /// <summary>Lets RimWorld bind the fields above at load.</summary>
        static BT_TouristDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_TouristDefOf));
        }
    }

    /// <summary>
    /// The tour group: who arrived, in what role, where each is sent, and
    /// what became of them. One per tour run, kept by the stage.
    /// </summary>
    public sealed class TourGroup
    {
        /// <summary>The tourists, in arrival order (their roles follow it).</summary>
        private readonly List<Pawn> members = new List<Pawn>();

        /// <summary>The clothes each tourist arrived in, to tidy up after.</summary>
        private readonly List<Thing> belongings = new List<Thing>();

        /// <summary>Each tourist's fate at dismissal, by arrival order.</summary>
        private readonly List<string> fates = new List<string>();

        /// <summary>The group's lord.</summary>
        private Lord lord;

        /// <summary>Creates an empty group on a map.</summary>
        /// <param name="map">The tour's map.</param>
        /// <param name="faction">The tourists' faction.</param>
        /// <param name="rest">Where the group waits between scenes.</param>
        private TourGroup(Map map, Faction faction, IntVec3 rest)
        {
            Map = map;
            Faction = faction;
            RestSpot = rest;
        }

        /// <summary>Gets the tour's map.</summary>
        public Map Map { get; }

        /// <summary>Gets the tourists' faction.</summary>
        public Faction Faction { get; }

        /// <summary>Gets where the group waits between scenes.</summary>
        public IntVec3 RestSpot { get; }

        /// <summary>Gets the tourists, in arrival order.</summary>
        public IReadOnlyList<Pawn> Members => members;

        /// <summary>Gets the grenade's volunteer, or null.</summary>
        public Pawn Volunteer => WithRole(TouristRole.Volunteer).FirstOrDefault();

        /// <summary>Gets the tractor scene's stray, or null.</summary>
        public Pawn Stray => WithRole(TouristRole.Stray).FirstOrDefault();

        /// <summary>Gets the sightseers, in arrival order.</summary>
        public IReadOnlyList<Pawn> Sightseers =>
            WithRole(TouristRole.Sightseer).ToList();

        /// <summary>Gets whether the group has been dismissed.</summary>
        public bool Dismissed { get; private set; }

        /// <summary>
        /// Gets what became of the group, counted as it was dismissed;
        /// null before.
        /// </summary>
        public TourGroupCount? AtDismissal { get; private set; }

        /// <summary>
        /// Gets each tourist's fate at dismissal ("standing", "downed",
        /// "dead" or "gone"), in arrival order; empty before.
        /// </summary>
        public IReadOnlyList<string> Fates => fates;

        /// <summary>Whether a pawn is one of this group's tourists.</summary>
        /// <param name="p">The pawn.</param>
        /// <returns>True if it arrived with the group.</returns>
        public bool Contains(Pawn p) => p != null && members.Contains(p);

        /// <summary>A tourist's role, by arrival order.</summary>
        /// <param name="p">The tourist.</param>
        /// <returns>The role, or null for a pawn not of the group.</returns>
        public TouristRole? RoleOf(Pawn p)
        {
            int i = p == null ? -1 : members.IndexOf(p);
            return i < 0 ? (TouristRole?)null : TourGroupPlan.RoleOf(i);
        }

        /// <summary>Whether a pawn is alive, on the map and on its feet.</summary>
        /// <param name="p">The pawn, or null.</param>
        /// <returns>True if it can play a part.</returns>
        public static bool Standing(Pawn p) =>
            p != null && p.Spawned && !p.Dead && !p.Destroyed && !p.Downed;

        /// <summary>
        /// A tourist's fate: "standing", "downed", "dead" (a corpse) or
        /// "gone" (destroyed without one).
        /// </summary>
        /// <param name="p">The tourist.</param>
        /// <returns>The fate.</returns>
        public static string Fate(Pawn p)
        {
            if (p == null)
            {
                return "gone";
            }

            if (p.Dead)
            {
                Corpse corpse = p.Corpse;
                return corpse != null && !corpse.Destroyed ? "dead" : "gone";
            }

            return p.Destroyed ? "gone" : p.Downed ? "downed" : "standing";
        }

        /// <summary>Counts the group's fates now.</summary>
        /// <returns>The count.</returns>
        public TourGroupCount Count()
        {
            List<string> now = members.Select(Fate).ToList();
            return new TourGroupCount(
                members.Count,
                now.Count(f => f == "standing"),
                now.Count(f => f == "downed"),
                now.Count(f => f == "dead"),
                now.Count(f => f == "gone"));
        }

        /// <summary>
        /// The tour group arrives: makes the faction if this game has none
        /// yet, then generates three to five tourists (the number drawn
        /// from the session's seeded staging) and stands them near the
        /// base, under one lord. Never throws: a failure is logged, and
        /// whoever had arrived is sent home, so the tour runs without them.
        /// </summary>
        /// <param name="map">The tour's map.</param>
        /// <param name="baseCentre">The base's centre.</param>
        /// <returns>The group, or null if none could come.</returns>
        internal static TourGroup Arrive(Map map, IntVec3 baseCentre)
        {
            if (map == null || !baseCentre.IsValid)
            {
                return null;
            }

            TourGroup group = null;
            try
            {
                Faction faction = TouristFaction();
                if (faction == null)
                {
                    Log.Warning($"{ShowcaseLog.Prefix} no tourists: the tour "
                        + "group's faction could not be made");
                    return null;
                }

                IntVec3 rest = (baseCentre + new IntVec3(
                    TourGroupPlan.Rest.X, 0, TourGroupPlan.Rest.Z)).ClampInsideMap(map);
                group = new TourGroup(map, faction, rest);
                int size = TourGroupPlan.Size(
                    Rand.RangeInclusive(TourGroupPlan.MinSize, TourGroupPlan.MaxSize));
                for (int i = 0; i < size; i++)
                {
                    group.SpawnOne();
                }

                if (group.members.Count == 0)
                {
                    return null;
                }

                var job = new LordJob_ShowcaseTourists();
                foreach (Pawn p in group.members)
                {
                    job.SetSpot(p, rest, TourGroupPlan.RestRadius);
                }

                group.lord = LordMaker.MakeNewLord(faction, job, map, group.members);
                Log.Message($"{ShowcaseLog.Prefix} tourists arrived: "
                    + string.Join(", ", group.members.Select(p =>
                        $"{p.LabelShort} ({group.RoleOf(p)})")));
                return group;
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} the tour group could not "
                    + $"arrive: {e}");
                group?.Dismiss("a failed arrival");
                return null;
            }
        }

        /// <summary>
        /// The tourists' faction in this game, made the first time a tour
        /// group arrives (as FactionGenerator makes a required one).
        /// </summary>
        /// <returns>The faction, or null if it could not be made.</returns>
        internal static Faction TouristFaction()
        {
            FactionDef def = BT_TouristDefOf.BT_ShowcaseTourists;
            Faction faction = Find.FactionManager.FirstFactionOfDef(def);
            if (faction != null)
            {
                return faction;
            }

            FactionGenerator.CreateFactionAndAddToManager(def);
            return Find.FactionManager.FirstFactionOfDef(def);
        }

        /// <summary>
        /// Generates one tourist: an adult in day clothes, unarmed, with
        /// empty pockets, tour composure and no urge to flee; spawns it
        /// near the rest spot.
        /// </summary>
        private void SpawnOne()
        {
            var request = new PawnGenerationRequest(
                BT_TouristDefOf.BT_ShowcaseTouristKind, Faction,
                PawnGenerationContext.NonPlayer, tile: Map.Tile,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                colonistRelationChanceFactor: 0f,
                forceAddFreeWarmLayerIfNeeded: true, allowPregnant: false,
                allowFood: false, allowAddictions: false, forbidAnyTitle: true,
                forcedXenotype: ModsConfig.BiotechActive ? XenotypeDefOf.Baseliner : null,
                developmentalStages: DevelopmentalStage.Adult, dontGiveWeapon: true);
            request.ForceNoIdeoGear = true;
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            pawn.equipment?.DestroyAllEquipment();
            pawn.inventory?.DestroyAll();
            pawn.health.AddHediff(BT_TouristDefOf.BT_TourComposure);
            if (pawn.mindState?.mentalStateHandler != null)
            {
                pawn.mindState.mentalStateHandler.neverFleeIndividual = true;
            }

            if (!CellFinder.TryFindRandomCellNear(
                    RestSpot, Map, (int)TourGroupPlan.RestRadius + 2,
                    c => c.Standable(Map) && c.GetFirstPawn(Map) == null,
                    out IntVec3 cell))
            {
                cell = RestSpot;
            }

            GenSpawn.Spawn(pawn, cell, Map);
            members.Add(pawn);
            belongings.AddRange(pawn.apparel?.WornApparel
                ?? Enumerable.Empty<Apparel>());
        }

        /// <summary>The tourists with a role, in arrival order.</summary>
        /// <param name="role">The role.</param>
        /// <returns>The tourists.</returns>
        private IEnumerable<Pawn> WithRole(TouristRole role) =>
            members.Where((p, i) => TourGroupPlan.RoleOf(i) == role);

        /// <summary>
        /// Sends a tourist to a spot to wander near: it walks there when
        /// it is within <see cref="TourGroupPlan.WalkTiles"/>, and is cut
        /// there otherwise (a walk across the map takes longer than a
        /// scene's intro and would cross another scene's danger). A
        /// downed or dead tourist stays where it is.
        /// </summary>
        /// <param name="p">The tourist.</param>
        /// <param name="spot">The spot.</param>
        /// <param name="radius">How far it may wander from it.</param>
        internal void Send(Pawn p, IntVec3 spot, float radius)
        {
            if (!Standing(p) || !Contains(p) || !spot.IsValid)
            {
                return;
            }

            spot = spot.ClampInsideMap(Map);
            if (!p.Position.InHorDistOf(spot, TourGroupPlan.WalkTiles)
                && CellFinder.TryFindRandomCellNear(
                    spot, Map, Mathf.CeilToInt(radius) + 2,
                    c => c.Standable(Map) && c.GetFirstPawn(Map) == null,
                    out IntVec3 cell))
            {
                Teleport(p, cell);
            }

            SetSpot(p, spot, radius);
        }

        /// <summary>
        /// Puts a tourist exactly on a cell, if it can stand there (else
        /// near it), to wait within a tile of it: the volunteer in the
        /// pile, the stray in the pull.
        /// </summary>
        /// <param name="p">The tourist.</param>
        /// <param name="cell">The cell.</param>
        /// <returns>True if it was placed.</returns>
        internal bool Place(Pawn p, IntVec3 cell)
        {
            if (!Standing(p) || !Contains(p) || !cell.IsValid || !cell.InBounds(Map))
            {
                return false;
            }

            if (!cell.Standable(Map) && !CellFinder.TryFindRandomCellNear(
                    cell, Map, 3, c => c.Standable(Map), out cell))
            {
                return false;
            }

            Teleport(p, cell);
            SetSpot(p, cell, TourGroupPlan.SpotRadius);
            return true;
        }

        /// <summary>
        /// Sends every standing tourist but those named to the rest spot.
        /// </summary>
        /// <param name="except">Tourists a scene has placed.</param>
        internal void RestAll(params Pawn[] except)
        {
            foreach (Pawn p in members)
            {
                if (except == null || !except.Contains(p))
                {
                    Send(p, RestSpot, TourGroupPlan.RestRadius);
                }
            }
        }

        /// <summary>
        /// Keeps tourists for a close look: any of them running (a flee
        /// job, which a self-destruct gives everyone near it each second)
        /// stops, and its duty walks it back to its spot. Tour composure:
        /// flight is not observed while the demonstration is on.
        /// </summary>
        /// <param name="looking">The tourists staying.</param>
        internal void StayForTheLook(IEnumerable<Pawn> looking)
        {
            foreach (Pawn p in looking ?? Enumerable.Empty<Pawn>())
            {
                if (Standing(p) && Contains(p) && p.CurJobDef == JobDefOf.Flee)
                {
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }

        /// <summary>
        /// Sends a tourist to a spot at a jog, at once, to wait near it
        /// (the stray, out of the robot's pull).
        /// </summary>
        /// <param name="p">The tourist.</param>
        /// <param name="spot">Where to go.</param>
        internal void Hurry(Pawn p, IntVec3 spot)
        {
            if (!Standing(p) || !Contains(p) || !spot.IsValid)
            {
                return;
            }

            SetSpot(p, spot, TourGroupPlan.RestRadius);
            Job job = JobMaker.MakeJob(JobDefOf.Goto, spot);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            p.jobs?.StartJob(job, JobCondition.InterruptForced);
        }

        /// <summary>
        /// Sends the group home: every tourist goes, alive, downed or
        /// dead, with its corpse and the clothes it arrived in, and the
        /// lord with them. Counts the fates first and logs them. Safe to
        /// call more than once; later calls do nothing.
        /// </summary>
        /// <param name="why">Why, for the log: finished, stopped, failed.</param>
        /// <returns>What became of the group.</returns>
        internal TourGroupCount Dismiss(string why)
        {
            if (Dismissed)
            {
                return AtDismissal ?? Count();
            }

            Dismissed = true;
            TourGroupCount count = Count();
            AtDismissal = count;
            fates.Clear();
            fates.AddRange(members.Select(Fate));
            foreach (Pawn p in members)
            {
                try
                {
                    Remove(p);
                }
                catch (Exception e)
                {
                    Log.Warning($"{ShowcaseLog.Prefix} tourist "
                        + $"{p?.LabelShort} could not be sent home: {e.Message}");
                }
            }

            foreach (Thing t in belongings)
            {
                if (t != null && !t.Destroyed && t.Spawned)
                {
                    t.Destroy(DestroyMode.Vanish);
                }
            }

            if (lord != null && Map?.lordManager != null
                && Map.lordManager.lords.Contains(lord))
            {
                Map.lordManager.RemoveLord(lord);
            }

            Log.Message($"{ShowcaseLog.Prefix} tourists dismissed ({why}): {count}");
            return count;
        }

        /// <summary>
        /// Whether any of the group, or its corpse or clothes, is still on
        /// the map: what the verifier checks after dismissal.
        /// </summary>
        /// <returns>What is left, or an empty list.</returns>
        internal List<string> LeftOnMap()
        {
            var left = new List<string>();
            foreach (Pawn p in members)
            {
                if (p != null && p.Spawned)
                {
                    left.Add($"{p.LabelShort} at {p.Position}");
                }

                Corpse corpse = p?.Corpse;
                if (corpse != null && !corpse.Destroyed && corpse.Spawned)
                {
                    left.Add($"{p.LabelShort}'s corpse at {corpse.Position}");
                }
            }

            left.AddRange(belongings
                .Where(t => t != null && !t.Destroyed && t.Spawned)
                .Select(t => $"{t.LabelShort} at {t.Position}"));
            if (Map != null)
            {
                left.AddRange(Map.mapPawns.AllPawnsSpawned
                    .Where(p => p.Faction == Faction && !members.Contains(p))
                    .Select(p => $"stranger {p.LabelShort} at {p.Position}"));
            }

            return left;
        }

        /// <summary>
        /// Removes one tourist: its corpse if dead, else the tourist.
        /// </summary>
        /// <param name="p">The tourist.</param>
        private static void Remove(Pawn p)
        {
            if (p == null)
            {
                return;
            }

            if (p.Dead)
            {
                Corpse corpse = p.Corpse;
                if (corpse != null && !corpse.Destroyed)
                {
                    corpse.Destroy(DestroyMode.Vanish);
                }

                return;
            }

            if (!p.Destroyed)
            {
                p.Destroy(DestroyMode.Vanish);
            }
        }

        /// <summary>Records a tourist's spot with the lord.</summary>
        /// <param name="p">The tourist.</param>
        /// <param name="spot">The spot.</param>
        /// <param name="radius">How far it may wander from it.</param>
        private void SetSpot(Pawn p, IntVec3 spot, float radius) =>
            (lord?.LordJob as LordJob_ShowcaseTourists)?.SetSpot(p, spot, radius);

        /// <summary>Moves a pawn, interrupting whatever it was doing.</summary>
        /// <param name="p">The pawn.</param>
        /// <param name="cell">Where to put it.</param>
        private static void Teleport(Pawn p, IntVec3 cell)
        {
            p.jobs?.StopAll();
            p.Position = cell;
            p.Notify_Teleported();
        }
    }

    /// <summary>
    /// The tour group's lord: one toil that gives each tourist the
    /// BT_ShowcaseTourist duty at its own spot. It keeps downed members
    /// (vanilla lords drop them), never adds a flee toil, and never takes
    /// in other pawns.
    /// </summary>
    public class LordJob_ShowcaseTourists : LordJob
    {
        /// <summary>Saved: each tourist's spot.</summary>
        private Dictionary<Pawn, IntVec3> spots = new Dictionary<Pawn, IntVec3>();

        /// <summary>Saved: how far each tourist wanders from its spot.</summary>
        private Dictionary<Pawn, float> radii = new Dictionary<Pawn, float>();

        /// <summary>Scribe's working list of spot keys.</summary>
        private List<Pawn> spotKeys;

        /// <summary>Scribe's working list of spot values.</summary>
        private List<IntVec3> spotValues;

        /// <summary>Scribe's working list of radius keys.</summary>
        private List<Pawn> radiusKeys;

        /// <summary>Scribe's working list of radius values.</summary>
        private List<float> radiusValues;

        /// <summary>Tourists never panic and flee as a group.</summary>
        public override bool AddFleeToil => false;

        /// <summary>The group is fixed; dev-mode spawns never join it.</summary>
        public override bool CanAutoAddPawns => false;

        /// <summary>Builds the one-toil graph.</summary>
        /// <returns>The graph.</returns>
        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_ShowcaseTourists());
            return graph;
        }

        /// <summary>Keeps members who are downed; anyone else lost leaves.</summary>
        /// <param name="p">The pawn.</param>
        /// <param name="reason">Why it was lost.</param>
        /// <returns>False for incapacitated pawns.</returns>
        public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason) =>
            reason != PawnLostCondition.Incapped;

        /// <summary>
        /// Sets a tourist's spot and wander radius (held to 1..12, as
        /// RimWorld warns above 12), and gives the duty at once.
        /// </summary>
        /// <param name="p">The tourist.</param>
        /// <param name="spot">The spot.</param>
        /// <param name="radius">How far it may wander.</param>
        internal void SetSpot(Pawn p, IntVec3 spot, float radius)
        {
            if (p == null)
            {
                return;
            }

            spots[p] = spot;
            radii[p] = Mathf.Clamp(radius, 1f, 12f);
            lord?.CurLordToil?.UpdateAllDuties();
        }

        /// <summary>A tourist's spot, or where it stands if it has none.</summary>
        /// <param name="p">The tourist.</param>
        /// <returns>The cell, or Invalid.</returns>
        internal IntVec3 SpotOf(Pawn p) =>
            p != null && spots.TryGetValue(p, out IntVec3 c) ? c
                : p != null && p.Spawned ? p.Position : IntVec3.Invalid;

        /// <summary>How far a tourist may wander from its spot.</summary>
        /// <param name="p">The tourist.</param>
        /// <returns>The radius, in tiles.</returns>
        internal float RadiusOf(Pawn p) =>
            p != null && radii.TryGetValue(p, out float r) ? r : TourGroupPlan.RestRadius;

        /// <summary>Saves and loads the spots and radii.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(
                ref spots, "spots", LookMode.Reference, LookMode.Value,
                ref spotKeys, ref spotValues);
            Scribe_Collections.Look(
                ref radii, "radii", LookMode.Reference, LookMode.Value,
                ref radiusKeys, ref radiusValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                spots ??= new Dictionary<Pawn, IntVec3>();
                radii ??= new Dictionary<Pawn, float>();
                spots.RemoveAll(kv => kv.Key == null);
                radii.RemoveAll(kv => kv.Key == null);
            }
        }
    }

    /// <summary>
    /// The tour group lord's only toil: every tourist gets the
    /// BT_ShowcaseTourist duty at its own spot.
    /// </summary>
    public class LordToil_ShowcaseTourists : LordToil
    {
        /// <summary>Gives every tourist its duty.</summary>
        public override void UpdateAllDuties()
        {
            if (!(lord?.LordJob is LordJob_ShowcaseTourists job))
            {
                return;
            }

            foreach (Pawn pawn in lord.ownedPawns)
            {
                IntVec3 spot = job.SpotOf(pawn);
                if (pawn.mindState == null || !spot.IsValid)
                {
                    continue;
                }

                pawn.mindState.duty = new PawnDuty(
                    BT_TouristDefOf.BT_ShowcaseTouristDuty, spot)
                {
                    wanderRadius = job.RadiusOf(pawn),
                };
            }
        }
    }
}
