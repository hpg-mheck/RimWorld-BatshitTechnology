// Batshit Technology - the tour verifier's clean-stage checks (slice
// 19000a). At READY: the stage holds none of the map's own hostiles, the
// dormant ones (sleeping hives, inactive temple pawns, casket occupants)
// counted too, no anima tree, and no tree in the volleys' zone; and the
// def's weather is held. Every tick until FINISHED: each hostile pawn that
// appears is one a scene staged (a pawn already on the map that turns
// hostile, such as a provoked wild animal, is noted, not failed), and the
// held weather is what shows unless a scene has borrowed it. At each
// scene's end: no unstaged hostile appeared, no anima tree screamed, and
// the weather held (or, for a scene that borrowed it, was given back).
// After FINISHED: the weather was given back. docs/SHOWCASE.md, "A Clean
// Stage"; docs/AI-TESTING-STRATEGY.txt.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The tour verifier's clean-stage checks.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// The clean stage at READY: no map hostile left, dormant ones
        /// included, no anima tree, no tree in the volleys' zone; and the
        /// def's weather held and showing. Notes what the stage cleared.
        /// </summary>
        /// <param name="session">The showcase session.</param>
        /// <param name="director">The tour's director.</param>
        private void CheckCleanStageAtReady(
            GameComponent_ShowcaseSession session, BatshitTourDirector director)
        {
            Map map = director.Map;
            TourStage stage = director.Stage ?? TourStage.Empty;
            (int hives, int dormantHives, int nests, int pawns, int dormantPawns,
                int occupants) = StageClearing.CountMapHostiles(map);
            (int anima, int zoneTrees) = StageClearing.CountTrees(map, stage.VolleyZone);
            string zone = stage.VolleyZone?.ToString() ?? "none";
            Log.Message($"[BT-SMOKE] NOTE tour stage cleared: {stage.Clearance}; "
                + $"volley zone {zone}");
            Check("tour: the stage is clean at READY: no map hostile, dormant "
                    + "ones included, no anima tree, no tree in the volley zone",
                hives == 0 && nests == 0 && pawns == 0 && occupants == 0 && anima == 0
                    && zoneTrees == 0 && stage.VolleyZone.HasValue,
                $"hives {hives} ({dormantHives} dormant); egg sacs and cocoons "
                    + $"{nests}; hostile pawns {pawns} "
                    + $"({dormantPawns} dormant); casket occupants {occupants}; "
                    + $"anima trees {anima}; trees in the volley zone {zoneTrees} "
                    + $"(zone {zone})");

            WeatherDef held = director.def?.holdWeather;
            WeatherManager weather = map?.weatherManager;
            Check("tour: the weather is held from READY, as the def asks",
                held != null && session.HeldWeather == held
                    && ShowsHeld(held, weather),
                $"def {held?.defName ?? "none"}; session "
                    + $"{session.HeldWeather?.defName ?? "none"}; weather "
                    + WeatherNote(weather));
        }

        /// <summary>
        /// The weather held from READY to FINISHED (it showed every tick
        /// but while a scene had borrowed it), then given back: the
        /// session holds none, and a WEATHER cue gave it back after
        /// FINISHED. Checked only when the tour finished.
        /// </summary>
        /// <param name="session">The showcase session, or null.</param>
        /// <param name="watch">What the run saw.</param>
        private void CheckWeatherGivenBack(
            GameComponent_ShowcaseSession session, TourWatch watch)
        {
            if (watch.FinishedTick < 0)
            {
                return; // "FINISHED reached" already fails
            }

            List<string> cues = ShowcaseCuesSince(null, withRecords: true);
            int finished = cues.FindLastIndex(
                c => c.StartsWith("FINISHED ", StringComparison.Ordinal));
            int givenBack = cues.FindLastIndex(
                c => c.StartsWith("WEATHER given back", StringComparison.Ordinal));
            Check("tour: the weather held until FINISHED (lent only to a scene "
                    + "with weather of its own), then was given back",
                watch.WeatherBreakTicks == 0 && session != null
                    && session.HeldWeather == null && finished >= 0
                    && givenBack > finished,
                $"{watch.WeatherBreakTicks} ticks showed another weather "
                    + $"({string.Join(", ", watch.WeatherSeen)}); lent "
                    + $"{watch.WeatherLentTicks} ticks; restarts "
                    + $"{session?.WeatherRestarts}; held now "
                    + $"{session?.HeldWeather?.defName ?? "none"}; FINISHED at "
                    + $"{finished}, given back at {givenBack}");
        }

        /// <summary>
        /// Whether a map's weather shows only the held weather (Core's
        /// <see cref="WeatherHold.Shows"/>).
        /// </summary>
        /// <param name="held">The held weather, or null.</param>
        /// <param name="weather">The map's weather, or null.</param>
        /// <returns>True if nothing else shows.</returns>
        private static bool ShowsHeld(WeatherDef held, WeatherManager weather) =>
            held != null && weather != null
            && WeatherHold.Shows(held.defName, weather.curWeather?.defName,
                weather.lastWeather?.defName, weather.TransitionLerpFactor);

        /// <summary>A map's weather, for a check's detail.</summary>
        /// <param name="weather">The map's weather, or null.</param>
        /// <returns>"current from last, age n".</returns>
        private static string WeatherNote(WeatherManager weather) =>
            weather == null
                ? "none"
                : $"{weather.curWeather?.defName ?? "none"} from "
                    + $"{weather.lastWeather?.defName ?? "none"}, age "
                    + weather.curWeatherAge;

        /// <summary>The tour watch's clean-stage measures.</summary>
        private sealed partial class TourWatch
        {
            /// <summary>Every pawn on the map at READY.</summary>
            private readonly HashSet<Pawn> pawnsAtReady = new HashSet<Pawn>();

            /// <summary>Every hostile pawn already classified.</summary>
            private readonly HashSet<Pawn> hostilesSeen = new HashSet<Pawn>();

            /// <summary>
            /// Hostile pawns that appeared after READY and that no scene
            /// staged, each with the scene showing.
            /// </summary>
            private readonly List<(string Scene, string Who)> unstaged =
                new List<(string Scene, string Who)>();

            /// <summary>
            /// Pawns on the map at READY that turned hostile later (a
            /// provoked wild animal), each with the scene showing.
            /// </summary>
            private readonly List<string> turnedHostile = new List<string>();

            /// <summary>Ticks each scene showed another weather, by key.</summary>
            private readonly Dictionary<string, int> weatherBreaks =
                new Dictionary<string, int>();

            /// <summary>Ticks each scene had the weather lent, by key.</summary>
            private readonly Dictionary<string, int> weatherLent =
                new Dictionary<string, int>();

            /// <summary>Every weather seen showing while it was held.</summary>
            private readonly HashSet<string> weatherSeen = new HashSet<string>();

            /// <summary>The anima scream's thought, or null without Royalty.</summary>
            private readonly ThoughtDef scream =
                DefDatabase<ThoughtDef>.GetNamedSilentFail("AnimaScream");

            /// <summary>Scream memories among the colonists as the scene began.</summary>
            private int screamsAtSceneStart;

            /// <summary>
            /// Gets how many ticks before FINISHED showed a weather other
            /// than the held one while no scene had borrowed it.
            /// </summary>
            public int WeatherBreakTicks { get; private set; }

            /// <summary>Gets how many ticks a scene had the weather lent.</summary>
            public int WeatherLentTicks { get; private set; }

            /// <summary>Gets every weather seen showing while it was held.</summary>
            public IEnumerable<string> WeatherSeen => weatherSeen.OrderBy(w => w);

            /// <summary>
            /// Notes every pawn on the map at READY, and its hostiles as
            /// already classified (the READY check counts them).
            /// </summary>
            private void NoteCleanStageAtReady()
            {
                foreach (Pawn p in Director.Map.mapPawns.AllPawnsSpawned)
                {
                    pawnsAtReady.Add(p);
                    if (p.HostileTo(Faction.OfPlayer))
                    {
                        hostilesSeen.Add(p);
                    }
                }
            }

            /// <summary>Notes the scream memories as a scene begins.</summary>
            private void NoteCleanStageSceneBegins()
            {
                screamsAtSceneStart = Screams();
            }

            /// <summary>
            /// Samples, until FINISHED: each hostile pawn first seen
            /// (staged by a scene, turned hostile, or unstaged), and
            /// whether the held weather shows (unless a scene has it).
            /// </summary>
            private void SampleCleanStage()
            {
                if (FinishedTick >= 0)
                {
                    return; // the colony is the viewer's again
                }

                string key = showing?.key ?? "before the first scene";
                foreach (Pawn p in Director.Map.mapPawns.AllPawnsSpawned)
                {
                    if (p.Dead || !p.HostileTo(Faction.OfPlayer) || !hostilesSeen.Add(p))
                    {
                        continue;
                    }

                    if (pawnsAtReady.Contains(p))
                    {
                        turnedHostile.Add($"{key}: {Who(p)}");
                        Log.Message($"[BT-SMOKE] NOTE {key}: a pawn on the map at "
                            + $"READY turned hostile: {Who(p)}");
                    }
                    else if (!Staged(p))
                    {
                        unstaged.Add((key, Who(p)));
                    }
                }

                GameComponent_ShowcaseSession session = GameComponent_ShowcaseSession.Current;
                if (session?.WeatherLent == true)
                {
                    WeatherLentTicks++;
                    weatherLent[key] = (weatherLent.TryGetValue(key, out int n) ? n : 0) + 1;
                    return;
                }

                WeatherManager weather = Director.Map.weatherManager;
                weatherSeen.Add(weather?.CurWeatherPerceived?.defName ?? "none");
                if (!ShowsHeld(Director.def?.holdWeather, weather))
                {
                    WeatherBreakTicks++;
                    weatherBreaks[key] = (weatherBreaks.TryGetValue(key, out int n) ? n : 0) + 1;
                }
            }

            /// <summary>
            /// The clean-stage facts at a scene's end: no unstaged hostile
            /// appeared during it, no anima tree screamed, and the held
            /// weather showed throughout, or, for a scene that borrowed it,
            /// was given back by its end.
            /// </summary>
            /// <param name="scene">The scene that ended.</param>
            private void CleanStageFacts(ShowcaseScene scene)
            {
                List<string> appeared = unstaged.Where(u => u.Scene == scene.key)
                    .Select(u => u.Who).ToList();
                Fact(scene, "no hostile appeared that the tour did not stage "
                        + $"([{string.Join("; ", appeared)}])",
                    appeared.Count == 0);

                int screams = Screams();
                Fact(scene, $"no anima tree screamed (colonists' scream memories "
                        + $"{screamsAtSceneStart} to {screams}"
                        + (scream == null ? ", no Royalty" : string.Empty) + ")",
                    screams <= screamsAtSceneStart);

                string held = Director.def?.holdWeather?.defName ?? "none";
                if (weatherLent.TryGetValue(scene.key, out int lent) && lent > 0)
                {
                    bool back = GameComponent_ShowcaseSession.Current?.WeatherLent != true;
                    Fact(scene, $"the weather, lent to the scene for {lent} ticks, "
                            + $"was given back by its end ({(back ? "yes" : "no")})",
                        back);
                }
                else
                {
                    int breaks = weatherBreaks.TryGetValue(scene.key, out int b) ? b : 0;
                    Fact(scene, $"the weather held {held} ({breaks} ticks showed "
                            + "another)",
                        breaks == 0);
                }
            }

            /// <summary>
            /// Adds the clean stage's measures to the SUMMARY: what the
            /// stage cleared, the hostiles that appeared unstaged or turned
            /// hostile, the scream memories, and the weather.
            /// </summary>
            /// <param name="summary">The SUMMARY.</param>
            private void AddCleanStageTo(JsonLine summary)
            {
                StageClearance cleared = Director.Stage?.Clearance ?? default;
                summary.Add("cleared", new JsonLine
                {
                    { "hives", cleared.Hives },
                    { "nests", cleared.Nests },
                    { "hostilePawns", cleared.HostilePawns },
                    { "casketOccupants", cleared.CasketOccupants },
                    { "animaTrees", cleared.AnimaTrees },
                    { "zoneTrees", cleared.ZoneTrees },
                });
                summary.Add("unstagedHostiles",
                    unstaged.Select(u => $"{u.Scene}: {u.Who}").ToList());
                summary.Add("turnedHostile", turnedHostile);
                summary.Add("screams", Screams());
                GameComponent_ShowcaseSession session = GameComponent_ShowcaseSession.Current;
                summary.Add("weather", new JsonLine
                {
                    { "held", Director.def?.holdWeather?.defName },
                    { "breakTicks", WeatherBreakTicks },
                    { "lentTicks", WeatherLentTicks },
                    { "restarts", session?.WeatherRestarts },
                    { "seen", WeatherSeen.ToList() },
                });
            }

            /// <summary>
            /// Whether a scene of this run staged a pawn (its herd, its
            /// raccoon, its raiders).
            /// </summary>
            /// <param name="p">The pawn.</param>
            /// <returns>True if staged.</returns>
            private bool Staged(Pawn p) =>
                Director.Scenes.OfType<TourScene>()
                    .Any(s => s.StagedHostiles.Contains(p));

            /// <summary>
            /// The anima scream's memories among the colonists present at
            /// READY: 0 without Royalty.
            /// </summary>
            /// <returns>The count.</returns>
            private int Screams() =>
                scream == null
                    ? 0
                    : colonists.Keys
                        .Where(p => !p.Dead && !p.Destroyed)
                        .Sum(p => p.needs?.mood?.thoughts?.memories?
                            .NumMemoriesOfDef(scream) ?? 0);

            /// <summary>A pawn, for a note: who, of what, and where.</summary>
            /// <param name="p">The pawn.</param>
            /// <returns>The text.</returns>
            private static string Who(Pawn p) =>
                $"{p.LabelShort} ({p.Faction?.Name ?? p.kindDef?.defName ?? "?"}) "
                    + $"at {p.Position}";
        }
    }
}
