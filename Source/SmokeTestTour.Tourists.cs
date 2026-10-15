// Batshit Technology - the tour verifier's tour group facts (slice
// 19000c; docs/SHOWCASE.md, "Tourists"). At READY: three to five
// tourists arrived, standing, of the hidden tourist faction (no goodwill,
// not hostile), none a colonist, with a volunteer and a stray. While the
// tour runs: each tourist lost (downed, dead or gone) is counted under
// the scene showing. At each scene's end: the grenade's volunteer and the
// tractor's stray were tourists, never colonists, and a scene that
// narrates its losses said so when it had one. After FINISHED: the group
// was sent home, every tourist accounted for, nothing of it left on the
// map, the faction never hostile, and every tourist the group lost was
// seen lost by the verifier. The SUMMARY's "tourists" holds the counts.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The tour verifier's tour group facts.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// The tour group at READY: three to five tourists, standing near
        /// the rest spot, of the hidden faction made for showcases (no
        /// goodwill, not hostile, never made with a world), none a
        /// colonist, with a volunteer and a stray.
        /// </summary>
        /// <param name="director">The tour's director.</param>
        private void CheckTouristsAtReady(BatshitTourDirector director)
        {
            TourGroup group = director.Stage?.Tourists;
            IReadOnlyList<Pawn> members = group?.Members ?? new List<Pawn>();
            Faction faction = group?.Faction;
            bool factionOk = faction != null && faction.Hidden && !faction.HasGoodwill
                && faction.def == BT_TouristDefOf.BT_ShowcaseTourists
                && faction.def.requiredCountAtGameStart == 0
                && !faction.HostileTo(Faction.OfPlayer);
            bool placed = group != null && members.All(p => TourGroup.Standing(p)
                && p.Faction == faction && !p.IsColonist
                && p.Position.InHorDistOf(group.RestSpot, TourGroupPlan.WalkTiles));
            Check("tour: a tour group of 3 to 5 tourists of the hidden faction "
                    + "waits near the base at READY",
                group != null && !group.Dismissed
                    && members.Count >= TourGroupPlan.MinSize
                    && members.Count <= TourGroupPlan.MaxSize
                    && factionOk && placed
                    && group.Volunteer != null && group.Stray != null,
                $"group {group != null}; {members.Count} tourists; faction "
                    + $"{faction?.def?.defName}/{faction?.Name} hidden {faction?.Hidden} "
                    + $"goodwill {faction?.HasGoodwill} hostile "
                    + $"{faction?.HostileTo(Faction.OfPlayer)}; "
                    + string.Join(", ", members.Select(p => $"{p.LabelShort} "
                        + $"({group.RoleOf(p)}) at {p.Position}, colonist "
                        + $"{p.IsColonist}")));
        }

        /// <summary>
        /// The tour group after FINISHED: sent home, every tourist counted
        /// once, nothing of it left on the map, its faction never hostile,
        /// and every tourist it lost seen lost by the verifier. Checked
        /// only when the tour finished.
        /// </summary>
        /// <param name="watch">What the run saw.</param>
        private void CheckTouristsGivenBack(TourWatch watch)
        {
            if (watch.FinishedTick < 0)
            {
                return; // "FINISHED reached" already fails
            }

            TourGroup group = watch.Director.Stage?.Tourists;
            TourGroupCount? count = group?.AtDismissal;
            List<string> left = group?.LeftOnMap() ?? new List<string>();
            List<string> unseen = group == null ? new List<string>()
                : watch.UnseenLosses(group);
            bool hostile = group?.Faction?.HostileTo(Faction.OfPlayer) ?? true;
            Check("tour: the tour group went home at FINISHED, every tourist "
                    + "accounted for and nothing of it left on the map",
                group != null && group.Dismissed && count.HasValue
                    && count.Value.AccountedFor
                    && count.Value.Arrived == group.Members.Count
                    && unseen.Count == 0 && left.Count == 0 && !hostile,
                $"dismissed {group?.Dismissed}; {count?.ToString() ?? "not counted"}; "
                    + $"the verifier saw {watch.TouristsLost} lost, missed "
                    + $"[{string.Join(", ", unseen)}]; left "
                    + $"[{string.Join(", ", left)}]; faction hostile {hostile}");
        }

        /// <summary>The verifier's view of the tour group.</summary>
        private sealed partial class TourWatch
        {
            /// <summary>Tourists seen lost, each counted once.</summary>
            private readonly HashSet<Pawn> touristsLost = new HashSet<Pawn>();

            /// <summary>Tourists lost, by the scene showing when they were.</summary>
            private readonly Dictionary<string, int> touristsLostByScene =
                new Dictionary<string, int>();

            /// <summary>Tourists lost in the scene showing.</summary>
            private int touristsLostInScene;

            /// <summary>The grenade's volunteer as cast, or null.</summary>
            private Pawn touristVolunteer;

            /// <summary>Gets how many tourists the verifier saw lost.</summary>
            public int TouristsLost => touristsLost.Count;

            /// <summary>
            /// The tourists the group dismissed downed, dead or gone whom
            /// the verifier never saw lost (a volunteer seen downed and
            /// then healed is seen lost and dismissed standing, which is
            /// fine; the other way round is not).
            /// </summary>
            /// <param name="group">The dismissed group.</param>
            /// <returns>Their names and fates; empty when none.</returns>
            public List<string> UnseenLosses(TourGroup group)
            {
                var unseen = new List<string>();
                for (int i = 0; i < group.Members.Count && i < group.Fates.Count; i++)
                {
                    Pawn p = group.Members[i];
                    if (group.Fates[i] != "standing" && !touristsLost.Contains(p))
                    {
                        unseen.Add($"{p?.LabelShort} {group.Fates[i]}");
                    }
                }

                return unseen;
            }

            /// <summary>
            /// Samples the group, until FINISHED: each tourist first seen
            /// downed, dead or gone is counted under the scene showing.
            /// </summary>
            private void SampleTourists()
            {
                TourGroup group = Director.Stage?.Tourists;
                if (group == null || FinishedTick >= 0 || group.Dismissed)
                {
                    return;
                }

                if (showing is GrenadeScene g && g.Volunteer != null)
                {
                    touristVolunteer = g.Volunteer;
                }

                foreach (Pawn p in group.Members)
                {
                    if (TourGroup.Standing(p) || !touristsLost.Add(p))
                    {
                        continue;
                    }

                    string key = showing?.key ?? "before the first scene";
                    touristsLostByScene[key] =
                        (touristsLostByScene.TryGetValue(key, out int n) ? n : 0) + 1;
                    touristsLostInScene++;
                    Log.Message($"[BT-SMOKE] NOTE {key}: tourist {p.LabelShort} "
                        + $"({group.RoleOf(p)}) lost: {TourGroup.Fate(p)}");
                }
            }

            /// <summary>Notes a scene beginning: no tourist lost in it yet.</summary>
            private void NoteTouristsSceneBegins()
            {
                touristsLostInScene = 0;
            }

            /// <summary>
            /// The tour group's facts at a scene's end: the grenade's
            /// volunteer and the tractor's stray were tourists, never
            /// colonists; a scene that narrates its losses played its
            /// guestsLost beat if it lost one.
            /// </summary>
            /// <param name="scene">The scene that ended.</param>
            private void TouristFacts(ShowcaseScene scene)
            {
                TourGroup group = Director.Stage?.Tourists;
                if (scene is GrenadeScene grenade)
                {
                    Pawn v = grenade.Volunteer;
                    Fact(scene, "the volunteer was a tourist, never a colonist ("
                            + (v == null ? "none" : $"{v.LabelShort}, colonist "
                                + $"{v.IsColonist}, tourist {group?.Contains(v)}") + ")",
                        v != null && !v.IsColonist && group != null && group.Contains(v));
                }

                if (scene is TractorBombScene tractor)
                {
                    Pawn s = tractor.StrayPlaced;
                    Fact(scene, "the stray was a tourist, never a colonist ("
                            + (s == null ? "none" : $"{s.LabelShort}, colonist "
                                + $"{s.IsColonist}, tourist {group?.Contains(s)}") + ")",
                        s != null && !s.IsColonist && group != null && group.Contains(s));
                }

                if (TourScript.NarratesGuestsLost(scene.key) && touristsLostInScene > 0)
                {
                    bool said = TourCues().Any(c => c.Cue == "BEAT" && c.Key == scene.key
                        && c.Detail?.Split(' ')[0] == TourScript.GuestsLost);
                    Fact(scene, $"a lost tourist was narrated ({touristsLostInScene} "
                            + $"lost, guestsLost beat {said})",
                        said);
                }
            }

            /// <summary>
            /// Adds the tour group to the SUMMARY: how many arrived, how
            /// many were lost and how (as dismissed), the losses by scene,
            /// the volunteer's and the stray's fates, and whether every
            /// tourist was accounted for with nothing left on the map.
            /// </summary>
            /// <param name="summary">The SUMMARY.</param>
            private void AddTouristsTo(JsonLine summary)
            {
                TourGroup group = Director.Stage?.Tourists;
                if (group == null)
                {
                    summary.Add("tourists", null);
                    return;
                }

                TourGroupCount count = group.AtDismissal ?? group.Count();
                var byScene = new JsonLine();
                foreach (KeyValuePair<string, int> kv in touristsLostByScene)
                {
                    byScene.Add(kv.Key, kv.Value);
                }

                summary.Add("tourists", new JsonLine
                {
                    { "arrived", count.Arrived },
                    { "lost", count.Lost },
                    { "standing", count.Standing },
                    { "downed", count.Downed },
                    { "dead", count.Dead },
                    { "gone", count.Gone },
                    { "lostByScene", byScene },
                    { "volunteer", touristVolunteer == null ? null : FateOf(group, touristVolunteer) },
                    { "stray", group.Stray == null ? null : FateOf(group, group.Stray) },
                    { "dismissed", group.Dismissed },
                    { "accounted", count.AccountedFor },
                    { "leftOnMap", group.Dismissed ? group.LeftOnMap().Count : (int?)null },
                });
            }

            /// <summary>
            /// A tourist's fate as dismissed, or now if not yet dismissed.
            /// </summary>
            /// <param name="group">The group.</param>
            /// <param name="p">The tourist.</param>
            /// <returns>The fate.</returns>
            private static string FateOf(TourGroup group, Pawn p)
            {
                int i = group.Members.ToList().IndexOf(p);
                return i >= 0 && i < group.Fates.Count ? group.Fates[i] : TourGroup.Fate(p);
            }
        }
    }
}
