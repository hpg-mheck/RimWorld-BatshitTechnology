// Batshit Technology - the tour verifier's SUMMARY line and its shots.
// Every verify run (Tools/smoke-test.ps1 -Showcase -Verify) ends with one
// "[BT-TOUR] SUMMARY {json}" line just before DONE: what is needed to
// compare one seed's tour with another's (Tools/tour-trials.ps1 reads it).
// The fields are listed in docs/AI-TESTING-STRATEGY.txt, "Tour trials".
// Most are measured from the world as the tour runs, the way the
// verifier's facts are; the rest come from the API's cues and the
// session's records. With -btshots the verifier also saves the game's own
// rendered frame (Unity's ScreenCapture, so windows over the game do not
// matter) at READY and at each SCENE, BEAT and END cue, one a frame, to
// <data folder>\shots\NN-<scene>-<cue>.png.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The tour verifier's SUMMARY and shots.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>The flag that turns the verifier's shots on.</summary>
        private const string ShotsFlag = "btshots";

        /// <summary>The SUMMARY line's prefix; the JSON follows it.</summary>
        internal const string SummaryPrefix = "[BT-TOUR] SUMMARY ";

        /// <summary>The SUMMARY's layout version, raised when fields change.</summary>
        private const int SummaryVersion = 1;

        /// <summary>The cues the verifier takes a shot at.</summary>
        private static readonly HashSet<string> ShotCues =
            new HashSet<string> { "READY", "SCENE", "BEAT", "END" };

        /// <summary>Shots waiting for a frame, by file name; guarded by itself.</summary>
        private static readonly Queue<string> pendingShots = new Queue<string>();

        /// <summary>Whether the verifier listens for shot cues.</summary>
        private static bool shotsArmed;

        /// <summary>How many shots were named so far (their NN).</summary>
        private static int shotsNamed;

        /// <summary>How many shots were handed to Unity.</summary>
        private static int shotsTaken;

        /// <summary>Where the shots go, once armed.</summary>
        private static string shotsFolder;

        /// <summary>
        /// Arms the shots in a verify run started with -btshots: listens for
        /// the API's cues. Called from the constructor; arms once per run.
        /// </summary>
        private static void ArmTourShots()
        {
            if (shotsArmed || !TourVerifying
                || !GenCommandLine.CommandLineArgPassed(ShotsFlag))
            {
                return;
            }

            shotsArmed = true;
            shotsFolder = Path.Combine(GenFilePaths.SaveDataFolderPath, "shots");
            Directory.CreateDirectory(shotsFolder);
            ShowcaseLog.Cued += QueueShot;
            Log.Message($"[BT-SMOKE] NOTE tour shots go to {shotsFolder}");
        }

        /// <summary>
        /// Names a shot for a cue the verifier shoots at: NN, the scene (or
        /// "tour" for READY), and the cue with its moment or outcome.
        /// </summary>
        /// <param name="cue">The cue word.</param>
        /// <param name="detail">The cue's detail.</param>
        private static void QueueShot(string cue, string detail)
        {
            if (!ShotCues.Contains(cue))
            {
                return;
            }

            string[] parts = (detail ?? string.Empty).Split(' ');
            string scene = cue == "READY" ? "tour" : parts[0];
            string what = cue == "BEAT" || cue == "END"
                ? $"{cue}-{(parts.Length > 1 ? parts[1] : string.Empty)}"
                : cue;
            lock (pendingShots)
            {
                shotsNamed++;
                pendingShots.Enqueue(
                    $"{shotsNamed:00}-{FileSafe(scene)}-{FileSafe(what)}.png");
            }
        }

        /// <summary>
        /// Hands one waiting shot to Unity, which saves the frame as it
        /// finishes rendering; one a frame, so none overwrites another.
        /// </summary>
        private static void PumpTourShots()
        {
            if (!shotsArmed)
            {
                return;
            }

            string name;
            lock (pendingShots)
            {
                if (pendingShots.Count == 0)
                {
                    return;
                }

                name = pendingShots.Dequeue();
            }

            try
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(shotsFolder, name));
                shotsTaken++;
            }
            catch (Exception e)
            {
                Log.Message($"[BT-SMOKE] NOTE shot {name} failed: {e.Message}");
            }
        }

        /// <summary>A file-name-safe, lowercase form of text.</summary>
        /// <param name="text">The text.</param>
        /// <returns>Letters, digits and '-' only.</returns>
        private static string FileSafe(string text)
        {
            var safe = new StringBuilder();
            foreach (char c in (text ?? string.Empty).ToLowerInvariant())
            {
                safe.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '-');
            }

            return safe.Length == 0 ? "x" : safe.ToString();
        }

        /// <summary>
        /// Logs the SUMMARY line. Never throws: if building it fails, a
        /// SUMMARY still comes, holding the counts and the error.
        /// </summary>
        private void LogTourSummary()
        {
            JsonLine summary;
            try
            {
                summary = BuildTourSummary();
            }
            catch (Exception e)
            {
                summary = new JsonLine
                {
                    { "version", SummaryVersion },
                    { "passed", passed },
                    { "failed", failed },
                    { "summaryError", e.ToString().Split('\n')[0] },
                };
            }

            Log.Message(SummaryPrefix + summary);
        }

        /// <summary>Builds the SUMMARY from what the run saw.</summary>
        /// <returns>The SUMMARY.</returns>
        private JsonLine BuildTourSummary()
        {
            GameComponent_ShowcaseSession session = GameComponent_ShowcaseSession.Current;
            TourWatch watch = tourWatch;
            Map map = watch?.Director.Map ?? Find.CurrentMap;
            List<(string Cue, string Key, string Detail)> cues = TourCues();
            var summary = new JsonLine
            {
                { "version", SummaryVersion },
                { "seed", session?.Seed?.Text },
                { "seedSource", session?.Seed == null
                    ? null : ShowcaseSeed.SourceName(session.Seed.Source) },
                { "worldSeed", Find.World?.info?.seedString },
                { "mapHash", session?.MapHash },
                { "stageHash", session?.StageHash },
                { "biome", map?.Biome?.defName },
                { "tile", map == null ? (int?)null : map.Tile.tileId },
                { "mapSize", map?.Size.x },
                { "passed", passed },
                { "failed", failed },
                { "finished", watch != null && watch.FinishedTick >= 0
                    && cues.Any(c => c.Cue == "FINISHED") },
                { "skipForced", TourScene.SkippedForTest.OrderBy(k => k).ToList() },
                { "shots", shotsTaken },
            };
            if (watch == null)
            {
                summary.Add("error", "the tour never became READY");
                return summary;
            }

            watch.AddTo(summary, cues);
            return summary;
        }

        /// <summary>The SUMMARY's measures of one tour.</summary>
        private sealed partial class TourWatch
        {
            /// <summary>Each scene's clock, once stopped, by key.</summary>
            private readonly Dictionary<string, SceneClock> clocks =
                new Dictionary<string, SceneClock>();

            /// <summary>Each scene's start tick, by key.</summary>
            private readonly Dictionary<string, int> sceneStarts =
                new Dictionary<string, int>();

            /// <summary>The implosions first seen in each scene, by key.</summary>
            private readonly Dictionary<string, List<Implosion>> sceneImplosions =
                new Dictionary<string, List<Implosion>>();

            /// <summary>Hostiles seen stretched in each scene, by key.</summary>
            private readonly Dictionary<string, HashSet<Pawn>> stretched =
                new Dictionary<string, HashSet<Pawn>>();

            /// <summary>Hostiles already stretched as each scene began, by key.</summary>
            private readonly Dictionary<string, HashSet<Pawn>> stretchedBefore =
                new Dictionary<string, HashSet<Pawn>>();

            /// <summary>Colonist harm not excused, each with its scene.</summary>
            private readonly List<string> harmed = new List<string>();

            /// <summary>Colonist harm excused, each with its scene and why.</summary>
            private readonly List<string> excused = new List<string>();

            /// <summary>The scene whose harm was last added to the lists.</summary>
            private ShowcaseScene harmFlushed;

            /// <summary>The tick the grenade was first seen thrown, or -1.</summary>
            private int grenadeThrownAt = -1;

            /// <summary>Whether AnyRim stood at READY.</summary>
            private bool anyRimPlaced;

            /// <summary>AnyRim's crossroads at READY, or null.</summary>
            private string anyRimSite;

            /// <summary>AnyRim's population at READY, or -1.</summary>
            private int anyRimResidents = -1;

            /// <summary>Notes what the stage held at READY.</summary>
            private void NoteReady()
            {
                TourStage stage = Director.Stage ?? TourStage.Empty;
                MapComponent_AnyRim town = Director.Map?.GetComponent<MapComponent_AnyRim>();
                anyRimPlaced = stage.HasAnyRim && town?.Built == true;
                anyRimSite = stage.AnyRimAnchor.IsValid
                    ? $"{stage.AnyRimAnchor.x},{stage.AnyRimAnchor.z}"
                    : null;
                anyRimResidents = town != null && town.Built ? town.Population : -1;
            }

            /// <summary>
            /// Notes a scene beginning: its start tick, the stretched hostiles
            /// so far, and the harm of the scene before.
            /// </summary>
            /// <param name="scene">The scene beginning.</param>
            /// <param name="now">The current tick.</param>
            private void NoteSceneBegins(ShowcaseScene scene, int now)
            {
                FlushHarm();
                sceneStarts[scene.key] = now;
                sceneImplosions[scene.key] = new List<Implosion>();
                stretched[scene.key] = new HashSet<Pawn>();
                stretchedBefore[scene.key] = new HashSet<Pawn>(StretchedHostiles());
            }

            /// <summary>Notes an implosion first seen in the scene showing.</summary>
            /// <param name="implosion">The implosion.</param>
            private void NoteImplosion(Implosion implosion)
            {
                if (showing?.key != null
                    && sceneImplosions.TryGetValue(showing.key, out List<Implosion> list))
                {
                    list.Add(implosion);
                }
            }

            /// <summary>
            /// Samples the scene showing for the SUMMARY: the grenade's
            /// throw, and hostiles stretched in the tractor scenes (every 15
            /// ticks, since stretching marks last).
            /// </summary>
            /// <param name="scene">The scene showing.</param>
            /// <param name="now">The current tick.</param>
            private void SampleForSummary(ShowcaseScene scene, int now)
            {
                if (scene is GrenadeScene && grenadeThrownAt < 0
                    && Director.Map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile)
                        .Any(t => t.def.defName == Showcase.GrenadeProjectileDef))
                {
                    grenadeThrownAt = now;
                }

                if (scene is TractorYardScene && now % 15 == 0
                    && stretched.TryGetValue(scene.key, out HashSet<Pawn> seen))
                {
                    foreach (Pawn p in StretchedHostiles())
                    {
                        if (!stretchedBefore[scene.key].Contains(p))
                        {
                            seen.Add(p);
                        }
                    }
                }
            }

            /// <summary>
            /// The hostile pawns on the map with a stretch mark from opposing
            /// pulls.
            /// </summary>
            /// <returns>The pawns.</returns>
            private IEnumerable<Pawn> StretchedHostiles() =>
                Director.Map.mapPawns.AllPawnsSpawned.Where(p =>
                    p.HostileTo(Faction.OfPlayer)
                    && p.health?.hediffSet?.HasHediff(BT_ImplosionDefOf.BT_StretchMark) == true)
                .ToList();

            /// <summary>
            /// Adds the scene showing's colonist harm to the run's lists,
            /// once per scene.
            /// </summary>
            private void FlushHarm()
            {
                if (showing == null || ReferenceEquals(harmFlushed, showing))
                {
                    return;
                }

                harmFlushed = showing;
                foreach ((string what, string excuse) in sceneHarm.Values)
                {
                    if (excuse == null)
                    {
                        harmed.Add($"{showing.key}: {what}");
                    }
                    else
                    {
                        excused.Add($"{showing.key}: {what} ({excuse})");
                    }
                }
            }

            /// <summary>Adds the tour's measures to the SUMMARY.</summary>
            /// <param name="summary">The SUMMARY.</param>
            /// <param name="cues">The tour's cues.</param>
            public void AddTo(JsonLine summary, List<(string Cue, string Key, string Detail)> cues)
            {
                FlushHarm();
                int end = EndTick >= 0 ? EndTick : Find.TickManager.TicksGame;
                summary.Add("hostilesAtReady", startingHostiles.Count);
                summary.Add("hostileKinds", startingHostiles
                    .GroupBy(p => p.kindDef?.defName ?? "unknown")
                    .OrderBy(g => g.Key)
                    .Select(g => $"{g.Key} x{g.Count()}")
                    .ToList());
                summary.Add("readyTick", ReadyTick);
                summary.Add("finishedTick", FinishedTick);
                summary.Add("totalTicks", end - ReadyTick);
                summary.Add("scenes", Scenes(cues));
                summary.Add("colonists", Colonists);
                summary.Add("harmed", harmed);
                summary.Add("excused", excused);
                summary.Add("deaths", Deaths.Select(d => $"{d.Scene}: {d.Name}").ToList());
                summary.Add("deathsAfterTour", DeathsAfterTour);
                summary.Add("volunteer", Volunteer(cues));
                summary.Add("stampede", Stampede());
                summary.Add("grenade", Grenade());
                summary.Add("anyRim", new JsonLine
                {
                    { "placed", anyRimPlaced },
                    { "site", anyRimSite },
                    { "residents", anyRimResidents },
                });
                summary.Add("raccoon", Raccoon(cues));
                summary.Add("tractor", Tractor(TourScript.Tractor, cues));
                summary.Add("twins", Tractor(TourScript.Twins, cues));
                summary.Add("narration", Narration());
                summary.Add("storyteller", new JsonLine
                {
                    { "held", StorytellerPartsHeld },
                    { "awakeTicks", StorytellerAwakeTicks },
                    { "restored", GameComponent_ShowcaseSession.Current?.StorytellerQuiet == false
                        && (Find.Storyteller?.storytellerComps?.Count ?? 0) > 0 },
                });
            }

            /// <summary>
            /// Each scene of the script, by key: how it ended (its END cue's
            /// outcome and the director's ending), or that it declined and
            /// why; and for one that ran, its ticks, its average ticks a
            /// real second and its longest gap between ticks.
            /// </summary>
            /// <param name="cues">The tour's cues.</param>
            /// <returns>The scenes.</returns>
            private JsonLine Scenes(List<(string Cue, string Key, string Detail)> cues)
            {
                var scenes = new JsonLine();
                List<string> run = Director.Scenes.Select(s => s.key).ToList();
                foreach (string key in TourScript.Keys)
                {
                    var scene = new JsonLine();
                    (string Cue, string Key, string Detail) skipped =
                        cues.FirstOrDefault(c => c.Cue == "SKIPPED" && c.Key == key);
                    int index = run.IndexOf(key);
                    string outcome = cues.LastOrDefault(c => c.Cue == "END" && c.Key == key)
                        .Detail?.Split(' ')[0];
                    if (index < 0)
                    {
                        scene.Add("outcome", skipped.Cue != null ? "declined" : "notRun");
                        scene.Add("why", skipped.Detail);
                    }
                    else
                    {
                        scene.Add("outcome", outcome ?? "notEnded");
                        scene.Add("ending", index < Director.Endings.Count
                            ? Director.Endings[index].ToString()
                            : null);
                    }

                    if (clocks.TryGetValue(key, out SceneClock clock))
                    {
                        scene.Add("ticks", clock.Ticks);
                        scene.Add("seconds", Math.Round(clock.Seconds, 1));
                        scene.Add("tps", Math.Round(clock.TicksPerSecond, 1));
                        scene.Add("longestTickMs", Math.Round(clock.WorstMs, 1));
                    }

                    scenes.Add(key, scene);
                }

                return scenes;
            }

            /// <summary>
            /// The grenade's volunteer: who, how the grenade scene ended for
            /// them, and whether they were alive at the end of the tour.
            /// </summary>
            /// <param name="cues">The tour's cues.</param>
            /// <returns>The volunteer's record.</returns>
            private JsonLine Volunteer(List<(string Cue, string Key, string Detail)> cues)
            {
                Pawn p = volunteerSeen;
                return new JsonLine
                {
                    { "name", p?.LabelShort },
                    { "grenadeOutcome", cues.LastOrDefault(c => c.Cue == "END"
                        && c.Key == TourScript.Grenade).Detail?.Split(' ')[0] },
                    { "atEnd", p == null ? null : Fate(p) },
                };
            }

            /// <summary>
            /// The stampede: the herd, its dead before the second volley left
            /// (or at the end, after a single volley), the volleys and their
            /// bolts, the herd's dead at the end, the most fires on the map
            /// and near the base, and the scene tick of the first volley.
            /// </summary>
            /// <returns>The stampede's record, or null if it never ran.</returns>
            private JsonLine Stampede()
            {
                StampedeWatch s = stampedeWatch;
                if (s == null)
                {
                    return null;
                }

                int dead = s.Scene.Herd.Count(p => p != null && p.Dead);
                return new JsonLine
                {
                    { "herd", s.Scene.Herd.Count },
                    { "killedFirstVolley", s.DeadAtSecondVolley ?? dead },
                    { "volleys", s.Volleys.Count },
                    { "volleyBolts", s.Volleys },
                    { "killed", dead },
                    { "deadAtFirstEntry", s.DeadAtFirstEntry },
                    { "peakFires", s.MostFires },
                    { "firesNearBase", s.MostFiresNearBase },
                    { "firstVolleyTick", s.FirstVolleyTick },
                };
            }

            /// <summary>
            /// The grenade: whether it was thrown, and the ticks from the
            /// scene's start to the throw.
            /// </summary>
            /// <returns>The grenade's record, or null if it never ran.</returns>
            private JsonLine Grenade()
            {
                if (!sceneStarts.TryGetValue(TourScript.Grenade, out int start))
                {
                    return null;
                }

                return new JsonLine
                {
                    { "thrown", grenadeThrownAt >= 0 },
                    { "ticksToThrow", grenadeThrownAt >= 0 ? grenadeThrownAt - start : (int?)null },
                };
            }

            /// <summary>
            /// The Raccoon Incident: its outcome, Ned's fate, who threw, and
            /// AnyRim's census before and after.
            /// </summary>
            /// <param name="cues">The tour's cues.</param>
            /// <returns>The incident's record, or null if it never ran.</returns>
            private JsonLine Raccoon(List<(string Cue, string Key, string Detail)> cues)
            {
                RaccoonWatch r = raccoonWatch;
                if (r == null)
                {
                    return null;
                }

                Pawn ned = RaccoonScene.FindResident(
                    Director.Stage, Director.Map, RaccoonIncident.GuardKey);
                return new JsonLine
                {
                    { "outcome", cues.LastOrDefault(c => c.Cue == "END"
                        && c.Key == TourScript.Raccoon).Detail?.Split(' ')[0] },
                    { "ned", ned == null ? "missing" : Fate(ned) },
                    { "thrower", r.Thrower },
                    { "censusBefore", r.Before },
                    { "censusAfter", r.CensusCount },
                };
            }

            /// <summary>
            /// A tractor scene: the pawns each pull caught and the item moves
            /// it made, the fixtures it tore loose, the hostiles stretched,
            /// whether the robot protested, and who the stray was.
            /// </summary>
            /// <param name="key">The scene's key.</param>
            /// <param name="cues">The tour's cues.</param>
            /// <returns>The scene's record, or null if it never ran.</returns>
            private JsonLine Tractor(string key, List<(string Cue, string Key, string Detail)> cues)
            {
                if (!sceneImplosions.TryGetValue(key, out List<Implosion> pulls))
                {
                    return null;
                }

                var record = new JsonLine
                {
                    { "pulls", pulls.Count },
                    { "pullsPawns", pulls.Select(i => i.CaughtCount).ToList() },
                    { "pullsItemMoves", pulls.Select(i => i.ItemsMoved).ToList() },
                    { "pullsFixtures", pulls.Select(i => i.Uninstalled + i.TornDown).ToList() },
                    { "hostilesStretched", stretched.TryGetValue(key, out HashSet<Pawn> s)
                        ? s.Count : 0 },
                };
                if (key == TourScript.Tractor)
                {
                    record.Add("protest", cues.Any(c => c.Cue == "BEAT" && c.Key == key
                        && c.Detail?.Split(' ')[0] == "protest"));
                    record.Add("stray", StrayNote());
                }

                return record;
            }

            /// <summary>
            /// The tractor scene's stray as it logged it: the colonist and
            /// why it was chosen, or why there was none.
            /// </summary>
            /// <returns>The note, or null if none was logged.</returns>
            private static string StrayNote()
            {
                const string Stray = ShowcaseCuePrefix + "tractor: stray ";
                const string None = ShowcaseCuePrefix + "tractor: no stray ";
                string line = Log.Messages.Select(m => m?.text)
                    .LastOrDefault(t => t != null && (t.StartsWith(Stray, StringComparison.Ordinal)
                        || t.StartsWith(None, StringComparison.Ordinal)));
                if (line == null)
                {
                    return null;
                }

                if (line.StartsWith(None, StringComparison.Ordinal))
                {
                    return "none " + line.Substring(None.Length);
                }

                string rest = line.Substring(Stray.Length);
                int at = rest.LastIndexOf(" at ", StringComparison.Ordinal);
                return at > 0 ? rest.Substring(0, at) : rest;
            }

            /// <summary>
            /// The narration as it would pace on the overlay at normal speed
            /// (see <see cref="OverlayPacing"/>): lines said and shown, lines
            /// replaced before their full reading time, and the longest any
            /// line waited.
            /// </summary>
            /// <returns>The narration's record.</returns>
            private JsonLine Narration()
            {
                NarrationPacing pacing = PaceNarration(Director);
                return new JsonLine
                {
                    { "said", pacing.Said },
                    { "shown", pacing.Shown },
                    { "cutShort", pacing.CutShort },
                    { "longestWaitSeconds", Math.Round(pacing.LongestWaitSeconds, 1) },
                };
            }

            /// <summary>A pawn's state, for the SUMMARY.</summary>
            /// <param name="p">The pawn.</param>
            /// <returns>"dead", "gone", "downed" or "alive".</returns>
            private static string Fate(Pawn p) =>
                p.Dead ? "dead" : p.Destroyed ? "gone" : p.Downed ? "downed" : "alive";
        }
    }
}
