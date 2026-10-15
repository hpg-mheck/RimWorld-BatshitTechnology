// Batshit Technology - the tour's clean stage (slice 19000a).
// The stage clears the whole map's fog so every blast can be watched
// (Showcase.BuildSelfDestructDemo), and clearing the fog wakes what the
// map kept asleep: an ancient temple's dormant hives wake on their next
// tick-interval and spawn their first insects ("Sorne Geneline", the
// insects' fixed faction name), and its sleeping mechanoids and
// fleshbeasts wake at once (FogGrid.Unfog notifies every thing in the
// cell; RimWorld 1.6.4871, Hive.TickInterval, Pawn.Notify_Unfogged).
// So before the fog goes, the stage removes the map's own hostiles:
// hives, the egg sacs and cocoons that release an insect when killed or
// disturbed (on tour-17 the reactor's blast burst two egg sacs into
// larvae), hostile pawns, and the pawns sealed in ancient caskets, which
// a stray bolt would release with every casket of their group. Then it
// removes the trees whose death would hurt the colonists: every anima
// tree (its psychic scream and mood hit) and every tree in the volleys'
// zone. Everything goes with DestroyMode.Vanish, which leaves nothing,
// draws no scream and no mood (both on-destroy comps ignore Vanish),
// and no hive's jelly. Nothing here draws a random number. docs/SHOWCASE.md,
// "A Clean Stage".

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// Clears the tour's map of its own hostiles and of the trees whose
    /// death would hurt the colonists' mood, and counts what is left.
    /// </summary>
    internal static class StageClearing
    {
        /// <summary>
        /// Removes the map's own hostiles: every hive, every egg sac or
        /// cocoon (<see cref="Nests"/>), every spawned pawn
        /// hostile to the player (asleep, dormant or awake), and every
        /// pawn sealed in one of the map's ancient cryptosleep caskets. Removing
        /// the last hive gives the colonists vanilla's "defeated insect
        /// hive" memory, which they did not earn, so it is taken back. Call
        /// it before the fog is cleared and before any scene stages a
        /// hostile of its own.
        /// </summary>
        /// <param name="map">The tour's map.</param>
        /// <returns>
        /// How many hives, nests, hostile pawns and casket occupants went.
        /// </returns>
        internal static (int Hives, int Nests, int Pawns, int Occupants) ClearMapHostiles(
            Map map)
        {
            if (map == null)
            {
                return (0, 0, 0, 0);
            }

            int hives = 0;
            foreach (Thing hive in map.listerThings.ThingsOfDef(ThingDefOf.Hive).ToList())
            {
                if (!hive.Destroyed)
                {
                    hive.Destroy(DestroyMode.Vanish);
                    hives++;
                }
            }

            if (hives > 0)
            {
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    p.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(
                        ThoughtDefOf.DefeatedInsectHive);
                }
            }

            int nests = 0;
            foreach (Thing nest in Nests(map))
            {
                nest.Destroy(DestroyMode.Vanish);
                nests++;
            }

            int pawns = 0;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (!p.Destroyed && p.HostileTo(Faction.OfPlayer))
                {
                    p.Destroy(DestroyMode.Vanish);
                    pawns++;
                }
            }

            int occupants = 0;
            foreach (Building_AncientCryptosleepCasket casket in SealedCaskets(map))
            {
                foreach (Pawn p in Occupants(casket).ToList().Where(o => !o.Destroyed))
                {
                    p.Destroy(DestroyMode.Vanish);
                    occupants++;
                }
            }

            return (hives, nests, pawns, occupants);
        }

        /// <summary>
        /// Removes every tree whose death would give the map's pawns a
        /// thought (the anima tree's psychic scream), anywhere on the map,
        /// and every other tree in the volleys' zone. Vanishing leaves no
        /// stump and no leavings, and neither the scream, its sound nor
        /// the tree-lovers' mood follows.
        /// </summary>
        /// <param name="map">The tour's map.</param>
        /// <param name="zone">The volleys' zone, or null for none.</param>
        /// <returns>How many screaming trees and zone trees went.</returns>
        internal static (int Anima, int Zone) ClearTrees(Map map, CellRect? zone)
        {
            if (map == null)
            {
                return (0, 0);
            }

            int anima = 0;
            int inZone = 0;
            foreach (Thing thing in map.listerThings
                .ThingsInGroup(ThingRequestGroup.Plant).ToList())
            {
                if (thing.Destroyed || !(thing is Plant plant)
                    || plant.def.plant?.IsTree != true)
                {
                    continue;
                }

                if (Screams(plant))
                {
                    plant.Destroy(DestroyMode.Vanish);
                    anima++;
                }
                else if (zone.HasValue && zone.Value.Contains(plant.Position))
                {
                    plant.Destroy(DestroyMode.Vanish);
                    inZone++;
                }
            }

            return (anima, inZone);
        }

        /// <summary>
        /// What of the map's own hostiles is on the map now, dormant ones
        /// included: what the tour verifier checks at READY.
        /// </summary>
        /// <param name="map">The tour's map.</param>
        /// <returns>
        /// The hives (and how many sleep), the egg sacs and cocoons, the
        /// hostile pawns (and how many are dormant: inactive, or asleep in
        /// a dormancy comp), and the pawns in the map's ancient caskets.
        /// </returns>
        internal static (int Hives, int DormantHives, int Nests, int Pawns, int DormantPawns,
            int Occupants) CountMapHostiles(Map map)
        {
            if (map == null)
            {
                return (0, 0, 0, 0, 0, 0);
            }

            List<Thing> hives = map.listerThings.ThingsOfDef(ThingDefOf.Hive);
            int dormantHives = hives.OfType<Hive>().Count(h => h.CompDormant?.Awake == false);
            List<Pawn> hostile = map.mapPawns.AllPawnsSpawned
                .Where(p => !p.Dead && p.HostileTo(Faction.OfPlayer))
                .ToList();
            int dormant = hostile.Count(Dormant);
            int occupants = SealedCaskets(map).Sum(c => Occupants(c).Count());
            return (hives.Count, dormantHives, Nests(map).Count, hostile.Count, dormant,
                occupants);
        }

        /// <summary>
        /// The map's things, not the player's, that release a hostile
        /// pawn when killed (Odyssey's insect egg sacs:
        /// CompSpawnPawnOnDestroyed, which spawns nothing on Vanish) or
        /// when disturbed (stasis cocoons: CompPawnSpawnOnWakeup). Hives
        /// are counted on their own.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The nests, in the lister's order.</returns>
        internal static List<Thing> Nests(Map map) =>
            map.listerThings.AllThings
                .Where(t => !(t is Pawn) && !t.Destroyed && t.def != ThingDefOf.Hive
                    && t.Faction != Faction.OfPlayer
                    && (t.TryGetComp<CompSpawnPawnOnDestroyed>() != null
                        || t.TryGetComp<CompPawnSpawnOnWakeup>() != null))
                .ToList();

        /// <summary>
        /// How many trees on the map would scream if killed (the anima
        /// tree), and how many trees stand in a zone.
        /// </summary>
        /// <param name="map">The tour's map.</param>
        /// <param name="zone">The volleys' zone, or null.</param>
        /// <returns>The counts.</returns>
        internal static (int Anima, int Zone) CountTrees(Map map, CellRect? zone)
        {
            if (map == null)
            {
                return (0, 0);
            }

            List<Plant> trees = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                .OfType<Plant>()
                .Where(p => !p.Destroyed && p.def.plant?.IsTree == true)
                .ToList();
            return (trees.Count(Screams),
                zone.HasValue
                    ? trees.Count(t => !Screams(t) && zone.Value.Contains(t.Position))
                    : 0);
        }

        /// <summary>
        /// Whether a pawn is asleep in the map's keeping: inactive (a
        /// temple's sleepers) or held by a dormancy comp that is not awake
        /// (a mech cluster's).
        /// </summary>
        /// <param name="p">The pawn.</param>
        /// <returns>True if dormant.</returns>
        internal static bool Dormant(Pawn p) =>
            p?.mindState?.Active == false
            || p?.TryGetComp<CompCanBeDormant>()?.Awake == false;

        /// <summary>
        /// Whether a plant's death would give the map's pawns a thought:
        /// the anima tree (Royalty), or any plant with the same comp.
        /// </summary>
        /// <param name="plant">The plant.</param>
        /// <returns>True if killing it would scream.</returns>
        internal static bool Screams(Plant plant) =>
            plant != null
            && ((ThingDefOf.Plant_TreeAnima != null && plant.def == ThingDefOf.Plant_TreeAnima)
                || plant.GetComp<CompGiveThoughtToAllMapPawnsOnDestroy>() != null);

        /// <summary>
        /// The map's own ancient cryptosleep caskets (any the player does
        /// not own): damage from a faction's weapon opens one holding a
        /// pawn, and every unopened casket of its group with it
        /// (Building_AncientCryptosleepCasket.PreApplyDamage). Whether its
        /// contents are known is protected, and does not matter here: an
        /// opened casket holds no pawn.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The caskets.</returns>
        private static IEnumerable<Building_AncientCryptosleepCasket> SealedCaskets(Map map) =>
            map.listerThings.ThingsOfDef(ThingDefOf.AncientCryptosleepCasket)
                .OfType<Building_AncientCryptosleepCasket>()
                .Where(c => !c.Destroyed && c.Faction != Faction.OfPlayer)
                .ToList();

        /// <summary>The pawns sealed in a casket.</summary>
        /// <param name="casket">The casket.</param>
        /// <returns>The pawns.</returns>
        private static IEnumerable<Pawn> Occupants(Building_AncientCryptosleepCasket casket) =>
            casket.GetDirectlyHeldThings()?.OfType<Pawn>()
            ?? Enumerable.Empty<Pawn>();
    }
}
