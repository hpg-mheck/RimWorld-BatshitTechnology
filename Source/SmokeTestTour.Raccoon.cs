// Batshit Technology - the tour verifier's watch on the Raccoon Incident
// (scene "raccoon"). The facts are measured from the world each tick
// rather than taken from the scene: the grenade's thrower from the
// projectile in the air, the census from AnyRim's own count at the tick
// the line was said, the raccoon's fate from the raccoon, the outcome
// from the END cue the director logged, and each colonist's injuries
// against those it had when the scene began. A colonist hurt within the
// incident's reach (Main Street grown by the throw and the pull, or near
// the raccoon) fails the scene; one hurt elsewhere (the colony is about
// 100 tiles away) is noted with where and how, since the incident cannot
// have done it.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The tour verifier's raccoon watch.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// What the verifier saw of one run of the Raccoon Incident: who
        /// threw the grenade, the census line and the count behind it, and
        /// the colonists' injuries.
        /// </summary>
        private sealed class RaccoonWatch
        {
            /// <summary>Each colonist's injuries as the scene began.</summary>
            private readonly Dictionary<Pawn, HashSet<Hediff>> injuries =
                new Dictionary<Pawn, HashSet<Hediff>>();

            /// <summary>
            /// Each colonist first seen hurt, downed or dead during the
            /// scene: what, where, and whether within the incident's reach.
            /// </summary>
            private readonly Dictionary<Pawn, (string What, bool InReach)> hurt =
                new Dictionary<Pawn, (string What, bool InReach)>();

            /// <summary>The tour's director.</summary>
            private readonly BatshitTourDirector director;

            /// <summary>Narration lines already looked at.</summary>
            private int linesSeen;

            /// <summary>
            /// Vanilla census messages in the stack as the scene began.
            /// </summary>
            private readonly HashSet<Message> stackBefore;

            /// <summary>
            /// Census messages seen in vanilla's stack during the scene.
            /// </summary>
            private readonly HashSet<string> stacked = new HashSet<string>();

            /// <summary>
            /// Starts watching a Raccoon Incident that has just begun.
            /// </summary>
            /// <param name="scene">The scene.</param>
            /// <param name="tour">The tour's director.</param>
            public RaccoonWatch(RaccoonScene scene, BatshitTourDirector tour)
            {
                Scene = scene;
                director = tour;
                linesSeen = tour.NarrationLog.Count;
                stackBefore = new HashSet<Message>(CensusMessages());
                Before = Record()?.Population ?? -1;
                foreach (Pawn p in tour.Map.mapPawns.FreeColonistsSpawned)
                {
                    injuries[p] = Injuries(p);
                }
            }

            /// <summary>Gets the scene watched.</summary>
            public RaccoonScene Scene { get; }

            /// <summary>Gets AnyRim's population as the scene began.</summary>
            public int Before { get; }

            /// <summary>
            /// Gets the name of whoever threw a singularity grenade during
            /// the scene, or null while no one has.
            /// </summary>
            public string Thrower { get; private set; }

            /// <summary>Gets whether the thrower was AnyRim's guard.</summary>
            public bool ThrownByGuard { get; private set; }

            /// <summary>
            /// Gets the census line said during the scene, or null.
            /// </summary>
            public string CensusSaid { get; private set; }

            /// <summary>
            /// Gets AnyRim's population at the tick the census was said.
            /// </summary>
            public int CensusCount { get; private set; } = -1;

            /// <summary>Samples the scene once.</summary>
            public void Sample()
            {
                Map map = director.Map;
                if (Thrower == null && map != null)
                {
                    Projectile thrown = map.listerThings
                        .ThingsInGroup(ThingRequestGroup.Projectile)
                        .OfType<Projectile>()
                        .FirstOrDefault(p =>
                            p.def.defName == Showcase.GrenadeProjectileDef);
                    if (thrown != null)
                    {
                        Thrower = thrown.Launcher?.LabelShort ?? "no one";
                        ThrownByGuard = thrown.Launcher is Pawn by
                            && ReferenceEquals(by, GuardInWorld());
                    }
                }

                IReadOnlyList<NarrationRecord> said = director.NarrationLog;
                for (; linesSeen < said.Count; linesSeen++)
                {
                    NarrationRecord line = said[linesSeen];
                    if (CensusSaid == null && line.Scene == TourScript.Raccoon
                        && line.Text.StartsWith(AnyRimText.TownName + " census"))
                    {
                        CensusSaid = line.Text;
                        CensusCount = Record()?.Population ?? -1;
                    }
                }

                foreach (Message m in CensusMessages())
                {
                    if (!stackBefore.Contains(m))
                    {
                        stacked.Add(m.text);
                    }
                }

                foreach (KeyValuePair<Pawn, HashSet<Hediff>> was in injuries)
                {
                    Pawn p = was.Key;
                    if (hurt.ContainsKey(p))
                    {
                        continue;
                    }

                    List<string> fresh = Injuries(p)
                        .Where(h => !was.Value.Contains(h))
                        .Select(h => $"{h.def.defName} ({h.Part?.def.defName ?? "whole"})")
                        .ToList();
                    if (fresh.Count == 0 && !p.Dead && !p.Destroyed && !p.Downed)
                    {
                        continue;
                    }

                    IntVec3 at = p.PositionHeld;
                    string state = p.Dead || p.Destroyed ? "dead"
                        : p.Downed ? "downed" : "hurt";
                    hurt[p] = ($"{p.LabelShort} {state} at {at}, doing "
                            + $"{p.CurJobDef?.defName ?? "nothing"}: "
                            + string.Join(", ", fresh.Take(4)),
                        InReach(at));
                }
            }

            /// <summary>
            /// Records the scene's facts at its end, through the verifier's
            /// fact recorder.
            /// </summary>
            /// <param name="fact">Records one fact: what, and whether it held.</param>
            /// <param name="outcome">The outcome the END cue named, or null.</param>
            public void Check(System.Action<string, bool> fact, string outcome)
            {
                fact($"Ned threw the grenade (thrower {Thrower ?? "none"})",
                    Thrower != null && ThrownByGuard);

                Pawn raccoon = Scene.Raccoon;
                string fate = raccoon == null
                    ? null
                    : TourScript.RaccoonOutcome(raccoon.Dead, raccoon.Destroyed);
                string state = raccoon == null ? "never came"
                    : raccoon.Dead ? "dead" : "alive";
                fact($"the outcome matches the raccoon's fate (END "
                        + $"{outcome ?? "none"}, raccoon {state})",
                    fate != null && outcome == fate);

                string wanted = CensusCount >= 0 && Before >= 0
                    ? AnyRimText.CensusReport(Before, CensusCount)
                    : null;
                fact($"the census reads the real count (said "
                        + $"\"{CensusSaid ?? "nothing"}\"; population "
                        + $"{Before} -> {CensusCount})",
                    wanted != null && CensusSaid == wanted);

                List<string> harmed = hurt.Values
                    .Where(h => h.InReach).Select(h => h.What).ToList();
                fact("no colonist harmed within the incident's reach "
                        + $"([{string.Join("; ", harmed)}])",
                    harmed.Count == 0);
                List<string> elsewhere = hurt.Values
                    .Where(h => !h.InReach).Select(h => h.What).ToList();
                if (elsewhere.Count > 0)
                {
                    Log.Message("[BT-SMOKE] NOTE raccoon: colonists hurt far from "
                        + $"AnyRim during the scene: {string.Join("; ", elsewhere)}");
                }
                bool readable = LiveMessagesField != null;
                fact("the census left vanilla's message stack alone ("
                        + (readable
                            ? $"stacked [{string.Join(" | ", stacked)}]"
                            : "Messages.liveMessages not found")
                        + ")",
                    readable && stacked.Count == 0);
            }

            /// <summary>
            /// AnyRim's census messages in vanilla's message stack now;
            /// empty when the stack cannot be read (Check then fails its
            /// fact rather than passing on an empty list).
            /// </summary>
            /// <returns>The messages.</returns>
            private static IEnumerable<Message> CensusMessages() =>
                LiveMessages().Where(m => m?.text != null
                    && m.text.StartsWith(AnyRimText.TownName + " census"));

            /// <summary>
            /// Whether a cell is within the incident's reach: Main Street
            /// from the raccoon's entry to the bank, grown by the grenade's
            /// throw and its pull (23 tiles), or within 5 tiles of the
            /// raccoon wherever it has got to. Anywhere, when the town's
            /// anchor is unknown.
            /// </summary>
            /// <param name="cell">The cell.</param>
            /// <returns>True if the incident could reach it.</returns>
            private bool InReach(IntVec3 cell)
            {
                IntVec3 anchor = director.Stage?.AnyRimAnchor ?? IntVec3.Invalid;
                if (!anchor.IsValid)
                {
                    return true;
                }

                int reach = (int)System.Math.Ceiling(
                    RaccoonIncident.ThrowRange + RaccoonIncident.PullRadius);
                IntVec3 south = AnyRim.ToMap(anchor, RaccoonIncident.Entry);
                IntVec3 north = AnyRim.ToMap(anchor, RaccoonIncident.BankWalk);
                CellRect street = CellRect.FromLimits(
                    System.Math.Min(south.x, north.x) - reach, south.z - reach,
                    System.Math.Max(south.x, north.x) + reach, north.z + reach);
                Pawn raccoon = Scene.Raccoon;
                return street.Contains(cell)
                    || (raccoon != null && raccoon.Spawned
                        && raccoon.Position.InHorDistOf(cell, 5f));
            }

            /// <summary>The tour map's AnyRim record, or null.</summary>
            /// <returns>The record.</returns>
            private MapComponent_AnyRim Record() =>
                director.Map?.GetComponent<MapComponent_AnyRim>();

            /// <summary>
            /// AnyRim's guard, found in the world by his planned name
            /// rather than asked of the scene.
            /// </summary>
            /// <returns>The guard, alive or dead, or null.</returns>
            private Pawn GuardInWorld() =>
                RaccoonScene.FindResident(
                    director.Stage, director.Map, RaccoonIncident.GuardKey);

            /// <summary>A pawn's injuries and lost parts now.</summary>
            /// <param name="p">The pawn.</param>
            /// <returns>The hediffs.</returns>
            private static HashSet<Hediff> Injuries(Pawn p) =>
                new HashSet<Hediff>(p?.health?.hediffSet?.hediffs?
                    .Where(h => h is Hediff_Injury || h is Hediff_MissingPart)
                    ?? Enumerable.Empty<Hediff>());
        }

        /// <summary>
        /// The outcome the last END cue of a scene named, read from the
        /// log as the narration check reads it.
        /// </summary>
        /// <param name="key">The scene's key.</param>
        /// <returns>The outcome moment, or null if no END was logged.</returns>
        private static string EndOutcome(string key) =>
            TourCues()
                .LastOrDefault(c => c.Cue == "END" && c.Key == key)
                .Detail?.Split(' ')[0];

        /// <summary>
        /// Why the raccoon scene, declining on its own, should not have:
        /// unless the AnyRim scene was forced to decline (the incident
        /// needs the town shown), the town stands, Ned is on duty with his
        /// grenade, and the game has raccoons. Judged without the scene's
        /// own helpers, whose answers are what is under test: Ned is found
        /// among the map's pawns by the plan's names, his weapon is
        /// compared with the plan's, and the raccoon's kind is looked up.
        /// </summary>
        /// <param name="stage">The tour's stage.</param>
        /// <param name="map">The tour's map.</param>
        /// <returns>Null when the stage explains it, else why not.</returns>
        private static string RaccoonDeclineUnjustified(TourStage stage, Map map)
        {
            if (TourScene.SkippedForTest.Contains(TourScript.AnyRim)
                || !stage.HasAnyRim
                || map.GetComponent<MapComponent_AnyRim>()?.Built != true)
            {
                return null;
            }

            ResidentPlan plan = stage.AnyRimPlan?.Residents?
                .FirstOrDefault(r => r.Key == RaccoonIncident.GuardKey);
            if (plan == null)
            {
                return "AnyRim's plan has no guard";
            }

            Pawn ned = map.mapPawns.AllPawns.FirstOrDefault(p =>
                p?.Name is NameTriple name
                && name.First == plan.First && name.Last == plan.Last);
            if (ned == null)
            {
                return $"AnyRim stands, but {plan.First} {plan.Last} was "
                    + "never put on the map";
            }

            string weapon = ned.equipment?.Primary?.def.defName;
            if (!ned.Spawned || ned.Dead || ned.Downed || ned.InMentalState)
            {
                return null; // he is down or gone: a fair decline
            }

            if (weapon != plan.Weapon)
            {
                // Nothing in the tour takes his grenade before his scene,
                // and the storyteller is silenced: a stage fault.
                return $"{plan.First} stands holding {weapon ?? "nothing"}, "
                    + $"not the planned {plan.Weapon}";
            }

            return DefDatabase<PawnKindDef>.GetNamedSilentFail("Raccoon") != null
                && AnyRim.ToMap(stage.AnyRimAnchor, RaccoonIncident.Entry).Standable(map)
                    ? $"AnyRim stands with {plan.First} on duty, holding {weapon}"
                    : null;
        }
    }
}
