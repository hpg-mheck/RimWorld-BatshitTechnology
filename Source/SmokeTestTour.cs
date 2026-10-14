// Batshit Technology - the directed tour's verifier.
// Runs only when RimWorld starts with -bttourverify as well as
// -showcase=BT_Showcase (Tools/smoke-test.ps1 -Showcase -Verify). Once the
// tour is READY it switches to Superfast, watches every tick (the scene
// that runs, its caption, the world facts at each scene's end, colonists'
// deaths), and after FINISHED and the farewell checks the cues, endings,
// captions and narration, then logs "[BT-SMOKE] DONE" through the smoke
// runner's own Finish, which also checks the log for errors. The
// -bttourskip=<key,key> argument makes those scenes decline, so the
// API's declining (G1) runs on any map. Without -bttourverify it does
// nothing.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The tour verifier's part of the smoke runner.</summary>
    public partial class GameComponent_SmokeTest
    {
        private const string TourVerifyFlag = "bttourverify";
        private const string TourSkipArg = "bttourskip";
        private const string ShowcaseCuePrefix = "[BT-SHOWCASE] ";

        /// <summary>Ticks allowed beyond the scenes' timeouts.</summary>
        private const int TourMarginTicks = 3000;

        /// <summary>Ticks to wait after FINISHED for the farewell.</summary>
        private const int FarewellWaitTicks = 1500;

        /// <summary>Ticks to wait for the tour to become READY.</summary>
        private const int ReadyWaitTicks = 6000;

        /// <summary>Bolts the Ballista's volley must spend at least.</summary>
        private const int VolleyBolts = 16;

        /// <summary>What the verifier has seen; null until READY.</summary>
        private TourWatch tourWatch;

        /// <summary>Ticks seen while waiting for READY.</summary>
        private int tourWaitTicks;

        /// <summary>
        /// Gets a value indicating whether this run verifies the tour.
        /// </summary>
        private static bool TourVerifying =>
            GenCommandLine.CommandLineArgPassed(TourVerifyFlag);

        /// <summary>
        /// Arms the scenes the verifier forces to decline, from
        /// -bttourskip, before the tour's director is set up. Called from
        /// the constructor, which runs before any showcase begins.
        /// </summary>
        private static void ArmTourSkips()
        {
            TourScene.SkippedForTest.Clear();
            if (!TourVerifying
                || !GenCommandLine.TryGetCommandLineArg(TourSkipArg, out string keys)
                || keys.NullOrEmpty())
            {
                return;
            }

            foreach (string key in keys.Split(',').Select(k => k.Trim()))
            {
                if (key.Length > 0)
                {
                    TourScene.SkippedForTest.Add(key);
                }
            }

            Log.Message("[BT-SMOKE] NOTE tour scenes forced to decline: "
                + string.Join(", ", TourScene.SkippedForTest));
        }

        /// <summary>
        /// Watches the tour once per game tick in a verify run; idle
        /// otherwise. An exception fails the run and ends it.
        /// </summary>
        public override void GameComponentTick()
        {
            if (finished || !TourVerifying)
            {
                return;
            }

            try
            {
                TickTourVerifier();
            }
            catch (Exception e)
            {
                Check("tour verifier ran without an exception", false, e.ToString());
                Finish();
            }
        }

        /// <summary>
        /// Waits for READY, then samples the tour every tick, and runs the
        /// final checks after the farewell or at the deadline.
        /// </summary>
        private void TickTourVerifier()
        {
            GameComponent_ShowcaseSession session =
                GameComponent_ShowcaseSession.Current;
            int now = Find.TickManager.TicksGame;
            if (tourWatch == null)
            {
                if (session?.Worker is BatshitTourDirector director
                    && session.Running)
                {
                    tourWatch = new TourWatch(director, now);
                    Log.Message($"[BT-SMOKE] NOTE tour READY at tick {now}; "
                        + $"{director.Scenes.Count} scenes run, deadline tick "
                        + $"{tourWatch.DeadlineTick}");
                }
                else if (session?.Worker != null
                    && !(session.Worker is BatshitTourDirector))
                {
                    Check("the tour's worker is BatshitTourDirector", false,
                        session.Worker.GetType().FullName);
                    Finish();
                }
                else if (++tourWaitTicks > ReadyWaitTicks)
                {
                    Check("the tour became READY", false,
                        $"no running tour after {ReadyWaitTicks} ticks");
                    Finish();
                }

                return;
            }

            if (Find.TickManager.CurTimeSpeed != TimeSpeed.Superfast)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
            }

            tourWatch.Sample(now);
            if (tourWatch.FinishedTick < 0 && tourWatch.Director.Finished)
            {
                tourWatch.FinishedTick = now;
            }

            bool farewellDone = tourWatch.FinishedTick >= 0
                && (session == null || session.FarewellTick >= 0
                    || now > tourWatch.FinishedTick + FarewellWaitTicks);
            if (farewellDone || now > tourWatch.DeadlineTick)
            {
                CheckTour(tourWatch);
                Finish();
            }
        }

        /// <summary>The verifier's six checks (the sixth is Finish's).</summary>
        /// <param name="watch">What the run saw.</param>
        private void CheckTour(TourWatch watch)
        {
            BatshitTourDirector director = watch.Director;
            List<(string Cue, string Key, string Detail)> cues = TourCues();
            List<string> skipped = cues.Where(c => c.Cue == "SKIPPED")
                .Select(c => c.Key).ToList();
            List<string> expected = TourScript.Keys
                .Where(k => !skipped.Contains(k)).ToList();
            List<string> run = director.Scenes.Select(s => s.key).ToList();

            // 1. FINISHED, with SCENE and END cues in the script's order.
            List<string> sceneEnd = cues
                .Where(c => c.Cue == "SCENE" || c.Cue == "END")
                .Select(c => $"{c.Cue} {c.Key}").ToList();
            List<string> wanted = expected
                .SelectMany(k => new[] { $"SCENE {k}", $"END {k}" }).ToList();
            Check("tour: FINISHED reached", watch.FinishedTick >= 0
                    && cues.Any(c => c.Cue == "FINISHED"),
                $"state {director.State}; {director.Endings.Count} of "
                    + $"{run.Count} scenes ended by tick "
                    + $"{Find.TickManager.TicksGame}");
            Check("tour: SCENE and END cues follow the script, less SKIPPED",
                sceneEnd.SequenceEqual(wanted) && run.SequenceEqual(expected),
                $"cues [{string.Join(", ", sceneEnd)}]; skipped "
                    + $"[{string.Join(", ", skipped)}]");
            Check("tour: every forced skip declined",
                TourScene.SkippedForTest.All(skipped.Contains),
                $"forced [{string.Join(", ", TourScene.SkippedForTest)}], "
                    + $"skipped [{string.Join(", ", skipped)}]");

            // 2. Every scene Completed, with its world facts.
            List<string> notCompleted = director.Endings
                .Select((e, i) => (e, i))
                .Where(x => x.e != SceneEnd.Completed)
                .Select(x => $"{run[x.i]} {x.e}").ToList();
            Check("tour: every scene ends Completed",
                director.Endings.Count == run.Count && notCompleted.Count == 0,
                $"{director.Endings.Count} of {run.Count} ended; "
                    + $"[{string.Join(", ", notCompleted)}]");
            Check("tour: the world facts hold at each END",
                watch.FactFailures.Count == 0 && watch.FactsChecked > 0,
                $"{watch.FactsChecked} checked; "
                    + string.Join("; ", watch.FactFailures));

            // 3. Captions "i of N: title", N counting only scenes that run.
            List<string> wantedCaptions = director.Scenes
                .Select((s, i) => $"{i + 1} of {run.Count}: {s.title}").ToList();
            Check("tour: captions read 'i of N: title'",
                watch.Captions.SequenceEqual(wantedCaptions)
                    && run.Count == TourScript.Keys.Count - skipped.Count,
                $"saw [{string.Join(" | ", watch.Captions)}]");

            // 4. Narration in cue order, with targets.
            string narrationProblem = NarrationProblem(director, cues);
            Check("tour: narration follows the cues", narrationProblem == null,
                narrationProblem);

            // 5. No colonist dies but the volunteer.
            List<string> lost = watch.Deaths
                .Where(d => !d.Volunteer)
                .Select(d => $"{d.Name} during {d.Scene}").ToList();
            Check("tour: no colonist dies but the volunteer", lost.Count == 0,
                $"{watch.Colonists} colonists; lost [{string.Join(", ", lost)}]"
                    + $"; volunteer deaths {watch.Deaths.Count(d => d.Volunteer)}");
        }

        /// <summary>
        /// The Showcase API's cues logged since the tour's START, from
        /// RimWorld's log queue (1,000 messages, ample for one tour).
        /// </summary>
        /// <returns>Each cue, its scene key, and the rest of the line.</returns>
        private static List<(string Cue, string Key, string Detail)> TourCues()
        {
            var cues = new List<(string Cue, string Key, string Detail)>();
            foreach (LogMessage m in Log.Messages)
            {
                string text = m?.text;
                if (text == null
                    || !text.StartsWith(ShowcaseCuePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = text.Substring(ShowcaseCuePrefix.Length)
                    .Split(new[] { ' ' }, 3);
                string cue = parts[0];
                if (cue == "START")
                {
                    cues.Clear();
                }

                string key = parts.Length > 1 ? parts[1].TrimEnd(':') : null;
                string detail = parts.Length > 2 ? parts[2] : null;
                cues.Add((cue, key, detail));
            }

            return cues;
        }

        /// <summary>
        /// Checks that the narration log holds, in cue order, every line
        /// the def gives for each SCENE (start), BEAT and END (outcome)
        /// cue, each under its scene and with a valid target.
        /// </summary>
        /// <param name="director">The tour's director.</param>
        /// <param name="cues">The tour's cues.</param>
        /// <returns>Null when it does, else what is wrong.</returns>
        private static string NarrationProblem(
            BatshitTourDirector director,
            List<(string Cue, string Key, string Detail)> cues)
        {
            List<ShowcaseNarrationLine> lines =
                director.def?.narration ?? new List<ShowcaseNarrationLine>();
            var wanted = new List<(string Scene, string Text)>();
            foreach ((string cue, string key, string detail) in cues)
            {
                string moment = cue == "SCENE" ? NarrationScript.Start
                    : cue == "BEAT" || cue == "END" ? detail?.Split(' ')[0]
                    : null;
                if (moment == null)
                {
                    continue;
                }

                wanted.AddRange(lines
                    .Where(l => l != null && l.scene == key && l.when == moment)
                    .Select(l => (key, l.text)));
            }

            if (wanted.Count == 0)
            {
                return "no narration was expected; the cues are missing";
            }

            List<NarrationRecord> said = director.NarrationLog.ToList();
            int at = 0;
            foreach ((string scene, string text) in wanted)
            {
                while (at < said.Count && said[at].Text != text)
                {
                    at++;
                }

                if (at == said.Count)
                {
                    return $"missing or out of order: {scene}: \"{text}\" "
                        + $"({said.Count} lines said)";
                }

                NarrationRecord record = said[at];
                if (record.Scene != scene)
                {
                    return $"\"{text}\" said under '{record.Scene}', not '{scene}'";
                }

                if (record.Targets == null || !record.Targets.IsValid)
                {
                    return $"\"{text}\" has no valid target";
                }

                at++;
            }

            return null;
        }

        /// <summary>
        /// What the verifier saw of one tour: the scene showing, its
        /// caption, the counters at its start, the facts at its end, the
        /// implosions it set off, and colonists' deaths.
        /// </summary>
        private sealed class TourWatch
        {
            /// <summary>Every implosion seen so far, to count new ones.</summary>
            private readonly HashSet<Implosion> seenImplosions =
                new HashSet<Implosion>();

            /// <summary>The colonists at READY, and whether each is lost.</summary>
            private readonly Dictionary<Pawn, bool> colonists =
                new Dictionary<Pawn, bool>();

            /// <summary>The scene last seen running.</summary>
            private ShowcaseScene showing;

            /// <summary>How many of the director's endings were checked.</summary>
            private int endsSeen;

            /// <summary>The Ballista's bolts fired as the scene began.</summary>
            private int boltsAtStart;

            /// <summary>Tractor bomb explosions as the scene began.</summary>
            private int explosionsAtStart;

            /// <summary>Superposed pulls as the scene began.</summary>
            private int superposedAtStart;

            /// <summary>Implosions first seen during this scene.</summary>
            private int implosionsInScene;

            /// <summary>Starts watching a tour that is READY.</summary>
            /// <param name="director">The tour's director.</param>
            /// <param name="now">The current tick.</param>
            public TourWatch(BatshitTourDirector director, int now)
            {
                Director = director;
                DeadlineTick = now + TourMarginTicks
                    + director.Scenes.Sum(s => Math.Max(s.minTicks, s.timeoutTicks));
                foreach (Implosion i in Running())
                {
                    seenImplosions.Add(i);
                }

                foreach (Pawn p in director.Map.mapPawns.FreeColonistsSpawned)
                {
                    colonists[p] = false;
                }
            }

            /// <summary>Gets the tour's director.</summary>
            public BatshitTourDirector Director { get; }

            /// <summary>Gets the tick past which the tour has failed.</summary>
            public int DeadlineTick { get; }

            /// <summary>Gets or sets the tick FINISHED was seen, or -1.</summary>
            public int FinishedTick { get; set; } = -1;

            /// <summary>Gets each scene's caption as it began.</summary>
            public List<string> Captions { get; } = new List<string>();

            /// <summary>Gets the world facts that did not hold.</summary>
            public List<string> FactFailures { get; } = new List<string>();

            /// <summary>Gets how many world facts were checked.</summary>
            public int FactsChecked { get; private set; }

            /// <summary>Gets the colonists present at READY.</summary>
            public int Colonists => colonists.Count;

            /// <summary>
            /// Gets each colonist lost, the scene it was lost in, and
            /// whether it was the grenade's volunteer.
            /// </summary>
            public List<(string Name, string Scene, bool Volunteer)> Deaths { get; } =
                new List<(string Name, string Scene, bool Volunteer)>();

            /// <summary>Samples the tour once.</summary>
            /// <param name="now">The current tick.</param>
            public void Sample(int now)
            {
                while (endsSeen < Director.Endings.Count
                    && endsSeen < Director.Scenes.Count)
                {
                    CheckFacts(
                        Director.Scenes[endsSeen], Director.Endings[endsSeen]);
                    endsSeen++;
                }

                ShowcaseScene current = Director.CurrentScene;
                if (current != null && !ReferenceEquals(current, showing))
                {
                    showing = current;
                    Captions.Add(Director.Caption);
                    boltsAtStart = Building_BatshittiumBallista.BoltsFired;
                    explosionsAtStart = Building_TractorBomb.Explosions;
                    superposedAtStart = MapComponent_Implosions.Superposed;
                    implosionsInScene = 0;
                }

                foreach (Implosion i in Running())
                {
                    if (seenImplosions.Add(i))
                    {
                        implosionsInScene++;
                    }
                }

                foreach (Pawn p in colonists.Keys.ToList())
                {
                    if (!colonists[p] && (p.Dead || p.Destroyed))
                    {
                        colonists[p] = true;
                        bool volunteer = current is GrenadeScene g
                            && ReferenceEquals(g.Volunteer, p);
                        Deaths.Add((p.LabelShort,
                            current?.key ?? "between scenes", volunteer));
                    }
                }
            }

            /// <summary>
            /// The world facts at a scene's end: pads gone and the
            /// aftermath idle; 16 bolts spent; one more implosion; and the
            /// tractors' explosions (and superposed pulls for the twins).
            /// </summary>
            /// <param name="scene">The scene that ended.</param>
            /// <param name="ending">How it ended.</param>
            private void CheckFacts(ShowcaseScene scene, SceneEnd ending)
            {
                if (ending != SceneEnd.Completed)
                {
                    return; // check 2 reports it; its facts would only repeat it
                }

                Map map = Director.Map;
                switch (scene)
                {
                    case SelfDestructScene pad:
                        IReadOnlyList<Thing> pads = Director.Stage.Pads;
                        Thing device =
                            pad.pad >= 0 && pads != null && pad.pad < pads.Count
                                ? pads[pad.pad]
                                : null;
                        Fact(scene, "pad device destroyed",
                            device == null || device.Destroyed);
                        Fact(scene, "aftermath idle", map?
                            .GetComponent<MapComponent_SelfDestructAftermath>()
                            ?.Active != true);
                        break;
                    case BallistaVolleyScene _:
                        int bolts =
                            Building_BatshittiumBallista.BoltsFired - boltsAtStart;
                        Fact(scene, $"at least {VolleyBolts} bolts spent ({bolts})",
                            bolts >= VolleyBolts);
                        break;
                    case GrenadeScene _:
                        Fact(scene, $"one more implosion ({implosionsInScene})",
                            implosionsInScene >= 1);
                        break;
                    case TwinTractorsScene _:
                        int twin =
                            Building_TractorBomb.Explosions - explosionsAtStart;
                        int superposed =
                            MapComponent_Implosions.Superposed - superposedAtStart;
                        Fact(scene, $"explosions up by 2 ({twin})", twin >= 2);
                        Fact(scene, $"superposed pulls up ({superposed})",
                            superposed > 0);
                        break;
                    case TractorBombScene _:
                        int one =
                            Building_TractorBomb.Explosions - explosionsAtStart;
                        Fact(scene, $"explosions up by 1 ({one})", one >= 1);
                        break;
                }
            }

            /// <summary>Records one world fact.</summary>
            /// <param name="scene">The scene it belongs to.</param>
            /// <param name="what">What should hold.</param>
            /// <param name="ok">Whether it does.</param>
            private void Fact(ShowcaseScene scene, string what, bool ok)
            {
                FactsChecked++;
                if (!ok)
                {
                    FactFailures.Add($"{scene.key}: {what}");
                }
            }

            /// <summary>The implosions running on the tour's map.</summary>
            /// <returns>Each open implosion the field holds.</returns>
            private IEnumerable<Implosion> Running() =>
                (MapComponent_Implosions.For(Director.Map)?.Active
                    ?? (IReadOnlyList<Implosion>)new List<Implosion>())
                .Where(i => i != null && !i.Closed);
        }
    }
}
