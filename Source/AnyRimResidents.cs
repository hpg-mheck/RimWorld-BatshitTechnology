// Batshit Technology - AnyRim's residents.
// Eight townsfolk on RimWorld's hidden, neutral Ancients faction, so
// blowing them up costs no goodwill and sends no letters. Each household
// gets its own lord (LordJob_AnyRimResident), whose duty
// (BT_AnyRimResident, Defs/Misc/BT_AnyRim.xml) has no combat in it:
// eat, sleep, get out of dangerous temperatures and wander near the front
// door. Without a lord a non-colonist walks off the map, and vanilla lords
// drop downed pawns, so this one keeps them. The civic composure hediff
// blocks mental breaks and pins mood, and residents never flee when hurt.
// See docs/SHOWCASE.md, "Residents".

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace BatshitTechnology
{
    /// <summary>AnyRim's resident defs.</summary>
    [DefOf]
    public static class BT_AnyRimDefOf
    {
        /// <summary>The residents' daily-life duty.</summary>
        public static DutyDef BT_AnyRimResident;

        /// <summary>
        /// The residents' pawn kind. It shares its defName with the duty
        /// (they are different def types), so the field is aliased.
        /// </summary>
        [DefAlias("BT_AnyRimResident")]
        public static PawnKindDef BT_AnyRimResidentKind;

        /// <summary>Civic composure: no mental breaks, steady mood.</summary>
        public static HediffDef BT_AnyRimComposure;

        /// <summary>Lets RimWorld bind the fields above at load.</summary>
        static BT_AnyRimDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_AnyRimDefOf));
        }
    }

    /// <summary>
    /// One AnyRim household's lord: a single toil that gives every member
    /// the resident duty around the household's front door, forever. It
    /// keeps downed members (vanilla lords drop them, and a lordless
    /// non-colonist walks off the map), never adds a flee toil, and never
    /// takes in other pawns.
    /// </summary>
    public class LordJob_AnyRimResident : LordJob
    {
        /// <summary>
        /// RimWorld warns that wander radii above a region's size (12)
        /// break wandering.
        /// </summary>
        public const float MaxWanderRadius = 12f;

        /// <summary>Saved: the household's duty cell.</summary>
        private IntVec3 home = IntVec3.Invalid;

        /// <summary>Saved: how far members wander from it.</summary>
        private float wanderRadius = AnyRimResidents.WanderRadius;

        /// <summary>Used by RimWorld when it loads a saved lord.</summary>
        public LordJob_AnyRimResident()
        {
        }

        /// <summary>Creates the job for one household.</summary>
        /// <param name="home">The cell members wander around (inside the front door).</param>
        /// <param name="wanderRadius">How far they wander; clamped to 1..12.</param>
        public LordJob_AnyRimResident(IntVec3 home, float wanderRadius)
        {
            this.home = home;
            this.wanderRadius = Mathf.Clamp(wanderRadius, 1f, MaxWanderRadius);
        }

        /// <summary>The household's duty cell.</summary>
        public IntVec3 Home => home;

        /// <summary>The household's wander radius.</summary>
        public float WanderRadius => wanderRadius;

        /// <summary>Residents never panic and flee as a group.</summary>
        public override bool AddFleeToil => false;

        /// <summary>Households are fixed; dev-mode spawns never join one.</summary>
        public override bool CanAutoAddPawns => false;

        /// <summary>Builds the one-toil graph.</summary>
        /// <returns>The graph.</returns>
        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_AnyRimResident(home, wanderRadius));
            return graph;
        }

        /// <summary>
        /// Keeps members who are downed, as vanilla's LordJob_WanderNest
        /// does; anyone lost any other way leaves the household.
        /// </summary>
        /// <param name="p">The pawn.</param>
        /// <param name="reason">Why it was lost.</param>
        /// <returns>False for incapacitated pawns.</returns>
        public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason) =>
            reason != PawnLostCondition.Incapped;

        /// <summary>Saves and loads the duty cell and wander radius.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref home, "home", IntVec3.Invalid);
            Scribe_Values.Look(ref wanderRadius, "wanderRadius", AnyRimResidents.WanderRadius);
        }
    }

    /// <summary>
    /// The household lord's only toil: every member gets the
    /// BT_AnyRimResident duty, focused on the household's front door.
    /// </summary>
    public class LordToil_AnyRimResident : LordToil
    {
        /// <summary>The duty cell.</summary>
        private readonly IntVec3 home;

        /// <summary>The wander radius.</summary>
        private readonly float wanderRadius;

        /// <summary>Creates the toil.</summary>
        /// <param name="home">The duty cell.</param>
        /// <param name="wanderRadius">The wander radius, at most 12.</param>
        public LordToil_AnyRimResident(IntVec3 home, float wanderRadius)
        {
            this.home = home;
            this.wanderRadius = Mathf.Min(wanderRadius, LordJob_AnyRimResident.MaxWanderRadius);
        }

        /// <summary>
        /// The squad flag: the duty forbids cells more than 16 tiles from
        /// the duty focus, which is this cell.
        /// </summary>
        public override IntVec3 FlagLoc => home;

        /// <summary>Gives every member the resident duty.</summary>
        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn?.mindState == null)
                {
                    continue;
                }

                pawn.mindState.duty = new PawnDuty(BT_AnyRimDefOf.BT_AnyRimResident, home)
                {
                    wanderRadius = wanderRadius,
                };
            }
        }
    }

    /// <summary>Generates, dresses, spawns and organises AnyRim's residents.</summary>
    internal static class AnyRimResidents
    {
        /// <summary>
        /// How far residents wander from their front door, as
        /// docs/SHOWCASE.md specifies.
        /// </summary>
        internal const float WanderRadius = 8f;

        /// <summary>Survival meals every resident carries.</summary>
        internal const int PocketMeals = 2;

        /// <summary>How far from the duty cell a resident may first appear.</summary>
        private const int SpawnSearchRadius = 4;

        /// <summary>
        /// The town's faction: the hidden, neutral Ancients. A quick-test
        /// world always has it; if it is missing, it is created, as
        /// FactionGenerator does for required factions.
        /// </summary>
        /// <returns>The faction, or null if it could not be made.</returns>
        internal static Faction TownFaction()
        {
            Faction faction = Faction.OfAncients;
            if (faction != null)
            {
                return faction;
            }

            try
            {
                FactionGenerator.CreateFactionAndAddToManager(FactionDefOf.Ancients);
            }
            catch (Exception e)
            {
                Log.Error($"[BT] AnyRim could not create the Ancients faction: {e}");
                return null;
            }

            return Faction.OfAncients;
        }

        /// <summary>
        /// Generates, dresses and spawns every resident of a plan, then
        /// gives each household (residents sharing a home) its own lord.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="anchor">The map cell of the plan's local (0,0).</param>
        /// <param name="plan">The town plan.</param>
        /// <param name="report">Collects counts and failures.</param>
        /// <returns>The spawned residents by <see cref="ResidentPlan.Key"/>.</returns>
        internal static Dictionary<string, Pawn> SpawnAll(
            Map map, IntVec3 anchor, TownPlan plan, AnyRimBuildReport report)
        {
            var spawned = new Dictionary<string, Pawn>(StringComparer.Ordinal);
            if (plan.Residents.Count == 0)
            {
                return spawned;
            }

            Faction faction = TownFaction();
            if (faction == null)
            {
                report.Fail("residents: no Ancients faction to put them in");
                return spawned;
            }

            foreach (IGrouping<string, ResidentPlan> household in
                plan.Residents.GroupBy(r => r.HomeKey, StringComparer.Ordinal))
            {
                var members = new List<Pawn>();
                IntVec3 home = AnyRim.ToMap(anchor, household.First().DutySpot);
                foreach (ResidentPlan resident in household)
                {
                    Pawn pawn = SpawnOne(map, anchor, resident, faction, report);
                    if (pawn == null)
                    {
                        continue;
                    }

                    if (spawned.ContainsKey(resident.Key))
                    {
                        report.Fail($"resident {resident.Key}: key used twice");
                    }

                    spawned[resident.Key] = pawn;
                    members.Add(pawn);
                }

                if (members.Count == 0)
                {
                    continue;
                }

                LordMaker.MakeNewLord(
                    faction, new LordJob_AnyRimResident(home, WanderRadius), map, members);
                report.Lords++;
            }

            report.Residents = spawned.Count;
            return spawned;
        }

        /// <summary>
        /// Gives a bed or grave to its resident, if the game allows it.
        /// Refusals are counted, not treated as failures: the town works
        /// without them.
        /// </summary>
        /// <param name="thing">The bed or grave.</param>
        /// <param name="pawn">The resident.</param>
        /// <param name="report">Collects the outcome.</param>
        internal static void Reserve(Thing thing, Pawn pawn, AnyRimBuildReport report)
        {
            CompAssignableToPawn comp = thing?.TryGetComp<CompAssignableToPawn>();
            if (comp == null || pawn?.ownership == null || !comp.HasFreeSlot
                || !comp.CanAssignTo(pawn).Accepted || comp.IdeoligionForbids(pawn))
            {
                report.ReservationsRefused++;
                return;
            }

            comp.TryAssignPawn(pawn);
            if (comp.AssignedPawnsForReading.Contains(pawn))
            {
                report.Reserved++;
            }
            else
            {
                report.ReservationsRefused++;
            }
        }

        /// <summary>Generates, dresses and spawns one resident.</summary>
        /// <param name="map">The map.</param>
        /// <param name="anchor">The map cell of the plan's local (0,0).</param>
        /// <param name="resident">The resident's plan.</param>
        /// <param name="faction">The town's faction.</param>
        /// <param name="report">Collects failures.</param>
        /// <returns>The spawned pawn, or null if generation failed.</returns>
        private static Pawn SpawnOne(
            Map map, IntVec3 anchor, ResidentPlan resident, Faction faction,
            AnyRimBuildReport report)
        {
            Pawn pawn;
            try
            {
                pawn = Generate(map, resident, faction, report);
            }
            catch (Exception e)
            {
                report.Fail($"resident {resident.Key}: generation threw {e.GetType().Name}: {e.Message}");
                return null;
            }

            IntVec3 spot = AnyRim.ToMap(anchor, resident.DutySpot);
            if (!spot.InBounds(map))
            {
                report.Fail($"resident {resident.Key}: duty spot {spot} is off the map");
                pawn.Destroy(DestroyMode.Vanish);
                return null;
            }

            if (!CellFinder.TryFindRandomCellNear(
                    spot, map, SpawnSearchRadius,
                    c => c.Standable(map) && c.GetFirstPawn(map) == null, out IntVec3 cell))
            {
                cell = spot;
            }

            GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
            if (!pawn.Spawned)
            {
                report.Fail($"resident {resident.Key}: did not spawn at {cell}");
                return null;
            }

            return pawn;
        }

        /// <summary>
        /// Generates a resident: an adult of the planned name, gender and
        /// biological age, in industrial clothes with the planned apparel
        /// on top, unarmed, untitled, a baseliner where Biotech is active,
        /// with survival meals in their pockets, civic composure, and no
        /// urge to flee.
        /// </summary>
        /// <param name="map">The map (for the tile).</param>
        /// <param name="resident">The resident's plan.</param>
        /// <param name="faction">The town's faction.</param>
        /// <param name="report">Collects apparel failures.</param>
        /// <returns>The unspawned pawn.</returns>
        private static Pawn Generate(
            Map map, ResidentPlan resident, Faction faction, AnyRimBuildReport report)
        {
            var request = new PawnGenerationRequest(
                BT_AnyRimDefOf.BT_AnyRimResidentKind, faction, PawnGenerationContext.NonPlayer,
                tile: map.Tile, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                colonistRelationChanceFactor: 0f, forceAddFreeWarmLayerIfNeeded: true,
                allowPregnant: false, allowFood: true, allowAddictions: false,
                fixedBiologicalAge: resident.Age,
                fixedGender: resident.Female ? Gender.Female : Gender.Male,
                forbidAnyTitle: true,
                forcedXenotype: ModsConfig.BiotechActive ? XenotypeDefOf.Baseliner : null,
                developmentalStages: DevelopmentalStage.Adult, dontGiveWeapon: true);
            request.ForceNoIdeoGear = true;
            Pawn pawn = PawnGenerator.GeneratePawn(request);

            // NameTriple trims first and last, and throws on null.
            pawn.Name = new NameTriple(
                resident.First ?? resident.Key, resident.Nick, resident.Last ?? resident.Key);
            pawn.equipment?.DestroyAllEquipment();
            Dress(pawn, resident, report);
            FillPockets(pawn);
            pawn.health.AddHediff(BT_AnyRimDefOf.BT_AnyRimComposure);
            if (pawn.mindState?.mentalStateHandler != null)
            {
                pawn.mindState.mentalStateHandler.neverFleeIndividual = true;
            }

            return pawn;
        }

        /// <summary>
        /// Puts the planned apparel on over the generated clothes; anything
        /// it cannot be worn with comes off.
        /// </summary>
        /// <param name="pawn">The unspawned resident.</param>
        /// <param name="resident">The resident's plan.</param>
        /// <param name="report">Collects failures.</param>
        private static void Dress(Pawn pawn, ResidentPlan resident, AnyRimBuildReport report)
        {
            if (pawn.apparel == null)
            {
                return;
            }

            foreach (string defName in resident.Apparel)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null || !def.IsApparel)
                {
                    report.Fail($"resident {resident.Key}: no apparel def {defName}");
                    continue;
                }

                ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                if (!(ThingMaker.MakeThing(def, stuff) is Apparel apparel)
                    || !ApparelUtility.HasPartsToWear(pawn, def)
                    || !apparel.PawnCanWear(pawn, ignoreGender: true))
                {
                    report.Fail($"resident {resident.Key}: cannot wear {defName}");
                    continue;
                }

                pawn.apparel.Wear(apparel, dropReplacedApparel: false);
            }
        }

        /// <summary>Tops a resident's pockets up to two survival meals.</summary>
        /// <param name="pawn">The unspawned resident.</param>
        private static void FillPockets(Pawn pawn)
        {
            ThingDef meal = ThingDefOf.MealSurvivalPack;
            if (pawn.inventory == null || meal == null)
            {
                return;
            }

            int missing = PocketMeals - pawn.inventory.Count(meal);
            if (missing <= 0)
            {
                return;
            }

            Thing stack = ThingMaker.MakeThing(meal);
            stack.stackCount = missing;
            pawn.inventory.TryAddItemNotForSale(stack);
        }
    }
}
