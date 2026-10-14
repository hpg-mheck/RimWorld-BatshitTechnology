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
        /// Whether this run verifies the tour; read once, because the
        /// command line cannot change and GameComponentTick checks it
        /// every tick in every game.
        /// </summary>
        private static readonly bool TourVerifying =
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
                tourWatch.StopClock(now);
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

            // The unforced declines must be explained by the stage itself.
            List<string> unjustified = skipped
                .Where(k => !TourScene.SkippedForTest.Contains(k))
                .Select(k => (Key: k, Why: DeclineUnjustified(director, k)))
                .Where(x => x.Why != null)
                .Select(x => $"{x.Key}: {x.Why} (said: "
                    + cues.FirstOrDefault(c => c.Cue == "SKIPPED"
                        && c.Key == x.Key).Detail + ")")
                .ToList();
            Check("tour: every unforced decline is justified by the stage",
                unjustified.Count == 0, string.Join("; ", unjustified));

            // 2b. Every scene that completed played its beats.
            List<string> beatGaps = BeatGaps(director, cues);
            Check("tour: every completed scene played its beats",
                beatGaps.Count == 0, string.Join("; ", beatGaps));

            // 3. Captions "i of N: title", worked out without the director:
            // the script's order, less the forced and the declined, with
            // the def's titles.
            List<string> runKeys = TourScript.Keys
                .Where(k => !TourScene.SkippedForTest.Contains(k)
                    && !skipped.Contains(k))
                .ToList();
            Dictionary<string, string> titles = (director.def?.scenes
                    ?? new List<ShowcaseScene>())
                .Where(s => s?.key != null)
                .GroupBy(s => s.key)
                .ToDictionary(g => g.Key, g => g.First().title);
            List<string> wantedCaptions = runKeys
                .Select((k, i) => $"{i + 1} of {runKeys.Count}: "
                    + (titles.TryGetValue(k, out string t) ? t : k))
                .ToList();
            Check("tour: captions read 'i of N: title'",
                watch.Captions.SequenceEqual(wantedCaptions)
                    && run.SequenceEqual(runKeys),
                $"saw [{string.Join(" | ", watch.Captions)}]; wanted "
                    + $"[{string.Join(" | ", wantedCaptions)}]");

            // 4. Narration in cue order, with targets.
            string narrationProblem = NarrationProblem(director, cues);
            Check("tour: narration follows the cues", narrationProblem == null,
                narrationProblem);
            Log.Message("[BT-SMOKE] NOTE " + OverlayPacing(director));

            // 5. No colonist dies but the volunteer, during the grenade.
            List<string> lost = watch.Deaths
                .Where(d => !d.Volunteer)
                .Select(d => $"{d.Name} during {d.Scene}").ToList();
            Check("tour: no colonist dies but the volunteer", lost.Count == 0,
                $"{watch.Colonists} colonists; lost [{string.Join(", ", lost)}]"
                    + $"; volunteer deaths {watch.Deaths.Count(d => d.Volunteer)}");
        }

        /// <summary>
        /// How the tour's narration would pace on the overlay watched at
        /// normal speed (60 ticks a real second; the verifier itself runs
        /// at Superfast, where the overlay's real-time rules do not show):
        /// the lines said, at their ticks, replayed through Core's rules in
        /// 1/60 s steps, then the closing message. Reports the longest any
        /// line would wait and how many would be replaced before their full
        /// reading time.
        /// </summary>
        /// <param name="director">The tour's director.</param>
        /// <returns>The note's text.</returns>
        private static string OverlayPacing(BatshitTourDirector director)
        {
            List<NarrationRecord> said = director.NarrationLog.ToList();
            if (said.Count == 0)
            {
                return "overlay pacing: no lines said";
            }

            const double Step = 1.0 / NarrationTiming.TicksPerSecond;
            int first = said[0].Tick;
            var overlay = new NarrationOverlay();
            int next = 0;
            int shortened = 0;
            string showing = null;
            double shownAt = 0.0;
            double end = (said[said.Count - 1].Tick - first) * Step + 60.0;
            for (double t = 0.0; t <= end; t += Step)
            {
                while (next < said.Count
                    && (said[next].Tick - first) * Step <= t)
                {
                    overlay.Say(said[next++].Text, t);
                    if (next == said.Count)
                    {
                        overlay.Say(
                            "BT_Showcases_Finished".Translate(), t, patient: true);
                    }
                }

                overlay.Advance(t);
                if (overlay.CurrentLine != showing)
                {
                    double reading = NarrationTiming.ReadingSeconds(showing);
                    if (showing != null && overlay.CurrentLine != null
                        && t - shownAt < reading - 0.001)
                    {
                        shortened++;
                    }

                    showing = overlay.CurrentLine;
                    shownAt = t;
                }
            }

            return $"overlay pacing at normal speed: {overlay.LinesShown} of "
                + $"{overlay.LinesSaid} lines shown, the longest wait "
                + $"{overlay.LongestWaitSeconds:0.0} s, {shortened} replaced "
                + "before their full reading time (each still shows "
                + $"{NarrationTiming.BaseReadingSeconds:0.0} s or more)";
        }

        /// <summary>
        /// Why a scene that declined on its own should not have, judged
        /// from the stage and the planner's plan rather than by asking the
        /// scene (whose reason is what is under test).
        /// </summary>
        /// <param name="director">The tour's director.</param>
        /// <param name="key">The scene that declined.</param>
        /// <returns>Null when the stage explains it, else why not.</returns>
        private static string DeclineUnjustified(
            BatshitTourDirector director, string key)
        {
            TourStage stage = director.Stage;
            Map map = director.Map;
            if (stage == null || map == null)
            {
                return null; // no stage at all: every scene may decline
            }

            switch (key)
            {
                case TourScript.Home:
                    return new[] { stage.Reactor, stage.Replicator,
                            stage.Transmogrifier, stage.Squeezer }.Any(Standing)
                        ? "the base's devices stand" : null;
                case TourScript.Defence:
                    return Standing(stage.Ballista) && stage.BallistaTarget.IsValid
                        ? "the Ballista stands with a target" : null;
                case TourScript.Stampede:
                    // The open ground is the map's to judge: the same cell
                    // query the scene uses, since the rule is the map's.
                    return Standing(stage.Ballista)
                        && stage.Ballista.BoltsAvailable(
                            BT_BallistaDefOf.BT_BoltReallyPainful,
                            unpoweredToo: true) >= Stampede.MoneyShotBolts
                        && DefDatabase<PawnKindDef>.GetNamedSilentFail("Boomalope") != null
                        && StampedeScene.EntryCells(
                            map, stage.BaseCentre, Stampede.TargetHerd).Count > 0
                        ? "the Ballista stands, loaded, with open ground east"
                        : null;
                case TourScript.RetireReplicator:
                case TourScript.RetireReactor:
                    int pad = director.def?.scenes?.OfType<SelfDestructScene>()
                        .FirstOrDefault(s => s.key == key)?.pad ?? -1;
                    Thing device = pad >= 0 && pad < (stage.Pads?.Count ?? 0)
                        ? stage.Pads[pad] : null;
                    return Standing(device)
                        && device.TryGetComp<CompSelfDestruct>() != null
                        ? $"pad {pad}'s device stands" : null;
                case TourScript.AnyRim:
                    return stage.HasAnyRim
                        && map.GetComponent<MapComponent_AnyRim>()?.Built == true
                        ? "AnyRim stands" : null;
                case TourScript.Raccoon:
                    return RaccoonDeclineUnjustified(stage, map);
                case TourScript.Grenade:
                    return stage.GrenadeTarget.IsValid
                        && map.mapPawns.FreeColonistsSpawned.Any(p =>
                            !p.Dead && !p.Downed && !p.InMentalState)
                        ? "a pile and an able colonist" : null;
                case TourScript.Tractor:
                case TourScript.Twins:
                    if (Showcase.StagePlan?.Yard != null && !stage.Yard.HasValue)
                    {
                        return "the planner placed a yard the stage lost";
                    }

                    return Showcase.StagePlan?.Yard != null
                        && Find.FactionManager.RandomEnemyFaction(
                            allowNonHumanlike: false) != null
                        ? "a yard and a hostile faction" : null;
                default:
                    return "no rule explains this scene declining";
            }
        }

        /// <summary>Whether a thing stands on the map.</summary>
        /// <param name="t">The thing, or null.</param>
        /// <returns>True if spawned and not destroyed.</returns>
        private static bool Standing(Thing t) =>
            t != null && t.Spawned && !t.Destroyed;

        /// <summary>
        /// The beats each completed scene should have played and did not:
        /// the device parade's planned devices, else the script's required
        /// moments (an empty-pile throw stands for the throw).
        /// </summary>
        /// <param name="director">The tour's director.</param>
        /// <param name="cues">The tour's cues.</param>
        /// <returns>One entry per scene with missing beats.</returns>
        private static List<string> BeatGaps(
            BatshitTourDirector director,
            List<(string Cue, string Key, string Detail)> cues)
        {
            var gaps = new List<string>();
            for (int i = 0; i < director.Scenes.Count
                && i < director.Endings.Count; i++)
            {
                if (director.Endings[i] != SceneEnd.Completed)
                {
                    continue; // check 2 reports it
                }

                ShowcaseScene scene = director.Scenes[i];
                IReadOnlyList<string> required = scene is DeviceParadeScene d
                    ? d.PlannedMoments
                    : TourScript.RequiredMoments(scene.key);
                var played = new HashSet<string>(cues
                    .Where(c => c.Cue == "BEAT" && c.Key == scene.key)
                    .Select(c => TourScript.StandsFor(c.Detail?.Split(' ')[0])));
                List<string> missing =
                    required.Where(m => !played.Contains(m)).ToList();
                if (missing.Count > 0)
                {
                    gaps.Add($"{scene.key}: beats missing "
                        + $"[{string.Join(", ", missing)}]");
                }
            }

            return gaps;
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

            /// <summary>
            /// Each living colonist's job and cell on the last tick, so a
            /// death can say what the colonist was doing and where.
            /// </summary>
            private readonly Dictionary<Pawn, string> lastSeen =
                new Dictionary<Pawn, string>();

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

            /// <summary>The grenade's volunteer once cast, or null.</summary>
            private Pawn volunteerSeen;

            /// <summary>The stampede's watch, once it has begun.</summary>
            private StampedeWatch stampedeWatch;

            /// <summary>The raccoon scene's watch, once it has begun.</summary>
            private RaccoonWatch raccoonWatch;

            /// <summary>The clock on the scene showing, or null.</summary>
            private SceneClock clock;

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
                clock?.Tick();
                if (current != null && !ReferenceEquals(current, showing))
                {
                    StopClock(now);
                    Log.Message($"[BT-SMOKE] NOTE {current.key} begins: "
                        + Bystanders(Director.Map));
                    clock = new SceneClock(current.key, now);
                    if (current is StampedeScene stampede)
                    {
                        stampedeWatch = new StampedeWatch(stampede, Director);
                    }

                    if (current is RaccoonScene incident)
                    {
                        raccoonWatch = new RaccoonWatch(incident, Director);
                    }

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

                if (showing is GrenadeScene cast && cast.Volunteer != null)
                {
                    volunteerSeen = cast.Volunteer;
                }

                if (showing is StampedeScene && current == showing)
                {
                    stampedeWatch?.Sample();
                }

                if (showing is RaccoonScene && current == showing)
                {
                    raccoonWatch?.Sample();
                }

                foreach (Pawn p in colonists.Keys.ToList())
                {
                    if (colonists[p])
                    {
                        continue;
                    }

                    if (!p.Dead && !p.Destroyed)
                    {
                        lastSeen[p] = $"{p.CurJobDef?.defName ?? "no job"}"
                            + $"{(p.Drafted ? ", drafted" : string.Empty)}, "
                            + $"at {p.Position}";
                        continue;
                    }

                    colonists[p] = true;
                    bool volunteer = current is GrenadeScene g
                        && ReferenceEquals(g.Volunteer, p);

                    // The volunteer's death is excused only during the
                    // grenade (GrenadeScene.End heals a survivor), but a
                    // later one is labelled so its cause is plain.
                    string role = !volunteer && ReferenceEquals(p, volunteerSeen)
                        ? "the volunteer, " : string.Empty;
                    lastSeen.TryGetValue(p, out string last);
                    string hurt = string.Join(", ", p.health?.hediffSet?.hediffs?
                        .Where(h => h?.def != null && h.def.isBad)
                        .Select(h => h.def.defName).Distinct().Take(5)
                        ?? Enumerable.Empty<string>());
                    last = $"{last ?? "unseen"}; "
                        + (p.Destroyed && !p.Dead ? "vanished" : "dead")
                        + $"; hediffs [{hurt}]";
                    Deaths.Add(($"{p.LabelShort} ({role}last: {last})",
                        current?.key ?? "between scenes", volunteer));
                }
            }

            /// <summary>
            /// The world facts at a scene's end: no bolt fired outside the
            /// volley; pads gone and the aftermath idle; 16 bolts spent; one
            /// more implosion; and the tractors' explosions (and superposed
            /// pulls for the twins).
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
                if (!(scene is BallistaVolleyScene) && !(scene is StampedeScene))
                {
                    int stray = Building_BatshittiumBallista.BoltsFired - boltsAtStart;
                    Fact(scene, $"the Ballista held its fire ({stray} bolts)",
                        stray == 0);
                }

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
                    case RaccoonScene _:
                        Fact(scene, $"one more implosion ({implosionsInScene})",
                            implosionsInScene >= 1);
                        if (raccoonWatch == null)
                        {
                            Fact(scene, "the raccoon scene was watched", false);
                            break;
                        }

                        raccoonWatch.Check(
                            (what, ok) => Fact(scene, what, ok), EndOutcome(scene.key));
                        break;
                    case StampedeScene _:
                        if (stampedeWatch == null)
                        {
                            Fact(scene, "the stampede was watched", false);
                            break;
                        }

                        stampedeWatch.Check((what, ok) => Fact(scene, what, ok), map);
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

            /// <summary>
            /// Logs the clock on the scene that was showing, if any, as a
            /// NOTE, and stops it.
            /// </summary>
            /// <param name="now">The current tick.</param>
            public void StopClock(int now)
            {
                if (clock != null)
                {
                    Log.Message("[BT-SMOKE] NOTE " + clock.Note(now));
                    clock = null;
                }
            }

            /// <summary>
            /// Who else is about as a scene begins: AnyRim's population, if
            /// the town stands, and the map's hostile pawns by faction (a
            /// raid the storyteller sends is not the tour's doing, but it
            /// explains deaths the tour did not cause).
            /// </summary>
            /// <param name="map">The tour's map.</param>
            /// <returns>The note's text.</returns>
            private static string Bystanders(Map map)
            {
                if (map == null)
                {
                    return "no map";
                }

                MapComponent_AnyRim town = map.GetComponent<MapComponent_AnyRim>();
                string anyRim = town != null && town.Built
                    ? $"AnyRim population {town.Population}"
                    : "no AnyRim";
                List<string> hostiles = map.mapPawns.AllPawnsSpawned
                    .Where(p => !p.Dead && !p.Downed
                        && p.HostileTo(Faction.OfPlayer))
                    .GroupBy(p => p.Faction?.Name ?? p.kindDef.defName)
                    .Select(g => $"{g.Key} {g.Count()}")
                    .ToList();
                return $"{anyRim}; hostiles on the map: "
                    + (hostiles.Count == 0 ? "none" : string.Join(", ", hostiles));
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
