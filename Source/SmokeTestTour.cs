// Batshit Technology - the directed tour's verifier.
// Runs only when RimWorld starts with -bttourverify as well as
// -showcase=BT_Showcase (Tools/smoke-test.ps1 -Showcase -Verify). Once the
// tour is READY it switches to Superfast, watches every tick (the scene
// that runs, its caption, the world facts at each scene's end, colonists'
// deaths), and after FINISHED and the farewell checks the cues, endings,
// captions and narration, then logs "[BT-SMOKE] DONE" through the smoke
// runner's own Finish, which also checks the log for errors. The
// -bttourskip=<key,key> argument makes those scenes decline, so the
// API's declining (G1) runs on any map. The Showcase API quiets the
// storyteller for every showcase (no raid lands mid-tour); the verifier
// checks that it is quiet from READY to FINISHED and given back after,
// that the run's seed and fingerprints were logged (SEED, MAPHASH,
// STAGEHASH), and that the stage was set up before the game's first tick.
// At the end it logs one "[BT-TOUR] SUMMARY {json}" line, what trials
// compare seeds by (Source/SmokeTestTour.Summary.cs, Tools/tour-trials.ps1),
// and with -btshots it saves the game's frame at READY and at each SCENE,
// BEAT and END cue. Without -bttourverify it does nothing.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;
using Verse.AI;

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

                    // A raid or other incident from the storyteller is not
                    // the tour's doing, and killed colonists in four of six
                    // runs in slice 15000; the Showcase API now quiets it
                    // for every showcase, and the verifier checks that it
                    // did. Hostiles already on the map stay, and each
                    // scene's start notes them.
                    CheckShowcaseRecordsAtReady(session, director, now);
                    Log.Message($"[BT-SMOKE] NOTE tour READY at tick {now}; "
                        + $"{director.Scenes.Count} scenes run, deadline tick "
                        + $"{tourWatch.DeadlineTick}; "
                        + $"{tourWatch.HostilesAtReady.Count} hostile pawns on the map");
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

            if (tourWatch.FinishedTick < 0 && session != null
                && (!session.StorytellerQuiet
                    || (Find.Storyteller?.storytellerComps?.Count ?? 0) > 0))
            {
                tourWatch.StorytellerAwakeTicks++;
            }

            bool farewellDone = tourWatch.FinishedTick >= 0
                && (session == null || session.FarewellTick >= 0
                    || now > tourWatch.FinishedTick + FarewellWaitTicks);
            if (farewellDone || now > tourWatch.DeadlineTick)
            {
                tourWatch.StopClock(now);
                tourWatch.EndTick = now;
                CheckTour(tourWatch);
                CheckStorytellerGivenBack(session, tourWatch);
                Finish();
            }
        }

        /// <summary>
        /// The Showcase API's records at READY: the storyteller quiet (no
        /// part, nothing queued); the seed the launch logged in its SEED
        /// cue, from the source its precedence picks; MAPHASH logged before
        /// START and STAGEHASH just before READY, matching the session's;
        /// the global random state reseeded; and the stage set up before
        /// the game's first tick, so no frame-timed tick ran before it.
        /// </summary>
        /// <param name="session">The showcase session.</param>
        /// <param name="director">The tour's director.</param>
        /// <param name="now">The current tick.</param>
        private void CheckShowcaseRecordsAtReady(
            GameComponent_ShowcaseSession session, BatshitTourDirector director, int now)
        {
            Storyteller teller = Find.Storyteller;
            int parts = teller?.storytellerComps?.Count ?? -1;
            int queued = teller?.incidentQueue?.Count ?? -1;
            Check("tour: the storyteller is quiet at READY",
                session.StorytellerQuiet && parts == 0 && queued == 0
                    && session.StorytellerCompsHeld > 0,
                $"quiet {session.StorytellerQuiet}; parts {parts}; queued {queued}; "
                    + $"held {session.StorytellerCompsHeld}");

            List<string> cues = ShowcaseCuesSince(null, withRecords: true);
            int game = cues.FindLastIndex(c => c.StartsWith("GAME ", StringComparison.Ordinal));
            List<string> run = game < 0 ? cues : cues.Skip(game).ToList();
            string seedCue = cues.LastOrDefault(c => c.StartsWith("SEED ", StringComparison.Ordinal));
            int map = run.FindIndex(c => c.StartsWith("MAPHASH ", StringComparison.Ordinal));
            int start = run.FindIndex(c => c.StartsWith("START ", StringComparison.Ordinal));
            int stage = run.FindIndex(c => c.StartsWith("STAGEHASH ", StringComparison.Ordinal));
            int ready = run.IndexOf("READY");
            ResolvedSeed seed = session.Seed;
            string passed = ShowcaseSeed.Normalize(ShowcaseLauncher.CommandLineSeed);
            string pinned = ShowcaseSeed.Normalize(director.def?.seed);
            SeedSource wanted = passed != null ? SeedSource.CommandLine
                : pinned != null ? SeedSource.Def : SeedSource.Random;
            string wantedText = passed ?? pinned;
            Check("tour: the launch logged its seed, from the source its "
                    + "precedence picks",
                seed != null && seedCue == $"SEED {seed.CueDetail}"
                    && seed.Source == wanted
                    && (wantedText == null || seed.Text == wantedText),
                $"cue '{seedCue}'; session {seed?.CueDetail ?? "none"}; wanted "
                    + $"{ShowcaseSeed.SourceName(wanted)} {wantedText}");
            Check("tour: MAPHASH before START and STAGEHASH just before READY, "
                    + "as the session holds them",
                map >= 0 && map < start && stage >= 0 && stage == ready - 1
                    && session.MapHash != null && session.StageHash != null
                    && run[map].StartsWith($"MAPHASH {session.MapHash} (", StringComparison.Ordinal)
                    && run[stage].StartsWith($"STAGEHASH {session.StageHash} (",
                        StringComparison.Ordinal),
                $"records [{string.Join(" | ", run.Where(IsRecord))}]; MAPHASH at {map}, "
                    + $"START at {start}, STAGEHASH at {stage}, READY at {ready}");
            Check("tour: the stage was set up from the seed before the game's "
                    + "first tick",
                session.Reseeded && director.StartTick == 0,
                $"reseeded {session.Reseeded}; set up at tick {director.StartTick}, "
                    + $"READY seen at {now}");
        }

        /// <summary>
        /// The storyteller stayed quiet until FINISHED and has its parts
        /// back after it, as many as the showcase held; checked only when
        /// the tour finished (one that ran out of time still holds them).
        /// </summary>
        /// <param name="session">The showcase session, or null.</param>
        /// <param name="watch">What the run saw.</param>
        private void CheckStorytellerGivenBack(
            GameComponent_ShowcaseSession session, TourWatch watch)
        {
            if (watch.FinishedTick < 0)
            {
                return; // "FINISHED reached" already fails
            }

            int parts = Find.Storyteller?.storytellerComps?.Count ?? -1;
            Check("tour: the storyteller stays quiet until FINISHED, then has "
                    + "its parts back",
                watch.StorytellerAwakeTicks == 0 && session != null
                    && !session.StorytellerQuiet && parts == watch.StorytellerPartsHeld,
                $"awake {watch.StorytellerAwakeTicks} ticks before FINISHED; quiet "
                    + $"{session?.StorytellerQuiet}; parts {parts} of "
                    + $"{watch.StorytellerPartsHeld} held");
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
            NarrationPacing pacing = PaceNarration(director);
            if (pacing.Said == 0)
            {
                return "overlay pacing: no lines said";
            }

            return $"overlay pacing at normal speed: {pacing.Shown} of "
                + $"{pacing.Said} lines shown, the longest wait "
                + $"{pacing.LongestWaitSeconds:0.0} s, {pacing.CutShort} replaced "
                + "before their full reading time (each still shows "
                + $"{NarrationTiming.BaseReadingSeconds:0.0} s or more)";
        }

        /// <summary>
        /// How the tour's narration would pace on the overlay at normal
        /// speed: lines said, at their ticks, replayed through Core's rules
        /// in 1/60 s steps, then the closing message.
        /// </summary>
        /// <param name="director">The tour's director.</param>
        /// <returns>The pacing.</returns>
        private static NarrationPacing PaceNarration(BatshitTourDirector director)
        {
            List<NarrationRecord> said = director.NarrationLog.ToList();
            if (said.Count == 0)
            {
                return new NarrationPacing(0, 0, 0, 0.0);
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

            return new NarrationPacing(
                overlay.LinesSaid, overlay.LinesShown, shortened, overlay.LongestWaitSeconds);
        }

        /// <summary>
        /// The narration's pacing at normal speed: lines handed to the
        /// overlay (the closing message included), lines shown, lines
        /// replaced before their full reading time, and the longest wait.
        /// </summary>
        private readonly struct NarrationPacing
        {
            /// <summary>Creates the pacing.</summary>
            /// <param name="said">Lines handed to the overlay.</param>
            /// <param name="shown">Lines shown.</param>
            /// <param name="cutShort">Lines replaced before their reading time.</param>
            /// <param name="longestWait">The longest wait, in seconds.</param>
            public NarrationPacing(int said, int shown, int cutShort, double longestWait)
            {
                Said = said;
                Shown = shown;
                CutShort = cutShort;
                LongestWaitSeconds = longestWait;
            }

            /// <summary>Gets the lines handed to the overlay.</summary>
            public int Said { get; }

            /// <summary>Gets the lines shown.</summary>
            public int Shown { get; }

            /// <summary>Gets the lines replaced before their reading time.</summary>
            public int CutShort { get; }

            /// <summary>Gets the longest any line waited, in seconds.</summary>
            public double LongestWaitSeconds { get; }
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
                    // The open ground is judged by the verifier's own query
                    // over Core's bands, not the scene's EntryCells, whose
                    // answer is what is under test.
                    int herd = Stampede.HerdSize(director.def?.scenes?
                        .OfType<StampedeScene>().FirstOrDefault()?.herdSize
                        ?? Stampede.TargetHerd);
                    return Standing(stage.Ballista)
                        && stage.Ballista.BoltsAvailable(
                            BT_BallistaDefOf.BT_BoltReallyPainful,
                            unpoweredToo: true) >= Stampede.MoneyShotBolts
                        && DefDatabase<PawnKindDef>.GetNamedSilentFail("Boomalope") != null
                        && StampedeGround(map, stage.BaseCentre, herd) is string ground
                        ? $"the Ballista stands, loaded, with open ground east "
                            + $"({ground})"
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
        /// The verifier's own look for the stampede's entry: the first of
        /// Core's bands on the east edge with enough standable cells from
        /// which the base can be reached. Reachability is asked from the
        /// base out to each cell, the other way round from the scene, so a
        /// fault in the scene's query does not hide here.
        /// </summary>
        /// <param name="map">The tour's map.</param>
        /// <param name="baseCentre">The demo base's centre.</param>
        /// <param name="herd">The herd's size.</param>
        /// <returns>
        /// The band and its count when one has enough, else null.
        /// </returns>
        private static string StampedeGround(Map map, IntVec3 baseCentre, int herd)
        {
            if (map == null || !baseCentre.IsValid)
            {
                return null;
            }

            // The base's centre holds the reactor: start from the nearest
            // standable cell to it instead.
            IntVec3 start = GenRadial.RadialCellsAround(baseCentre, 8f, true)
                .Where(c => c.InBounds(map) && c.Standable(map))
                .DefaultIfEmpty(IntVec3.Invalid)
                .First();
            if (!start.IsValid)
            {
                return null;
            }

            int needed = Stampede.CellsNeeded(herd);
            var bounds = new Box(0, 0, map.Size.x - 1, map.Size.z - 1);
            TraverseParms parms = TraverseParms.For(
                TraverseMode.PassDoors, Danger.Deadly);
            foreach (Box band in Stampede.Bands(bounds, AnyRim.ToCell(baseCentre)))
            {
                int reaching = 0;
                for (int x = band.MinX; x <= band.MaxX && reaching < needed; x++)
                {
                    for (int z = band.MinZ; z <= band.MaxZ && reaching < needed; z++)
                    {
                        var c = new IntVec3(x, 0, z);
                        if (c.InBounds(map) && c.Standable(map)
                            && map.reachability.CanReach(
                                start, c, PathEndMode.OnCell, parms))
                        {
                            reaching++;
                        }
                    }
                }

                if (reaching >= needed)
                {
                    return $"band {band}: {reaching} reachable cells, {needed} needed";
                }
            }

            return null;
        }

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
        /// implosions it set off, and colonists' deaths. The SUMMARY's
        /// measures are in Source/SmokeTestTour.Summary.cs.
        /// </summary>
        private sealed partial class TourWatch
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

            /// <summary>
            /// Each colonist's injuries as the scene showing began, for the
            /// per-scene "no colonist harmed" fact.
            /// </summary>
            private readonly Dictionary<Pawn, HashSet<Hediff>> sceneInjuries =
                new Dictionary<Pawn, HashSet<Hediff>>();

            /// <summary>
            /// Each colonist first seen hurt, downed or dead during the scene
            /// showing: what and where, and why it is excused, if it is.
            /// </summary>
            private readonly Dictionary<Pawn, (string What, string Excuse)> sceneHarm =
                new Dictionary<Pawn, (string What, string Excuse)>();

            /// <summary>
            /// The hostile pawns already on the map at READY (a mechhive's,
            /// dark entities): not the tour's doing, so harm done beside one
            /// is noted rather than failed.
            /// </summary>
            private readonly HashSet<Pawn> startingHostiles = new HashSet<Pawn>();

            /// <summary>
            /// How near a starting hostile a colonist's harm counts as that
            /// hostile's doing, in tiles.
            /// </summary>
            private const float HostileExcuseTiles = 12f;

            /// <summary>Starts watching a tour that is READY.</summary>
            /// <param name="director">The tour's director.</param>
            /// <param name="now">The current tick.</param>
            public TourWatch(BatshitTourDirector director, int now)
            {
                Director = director;
                ReadyTick = now;
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

                foreach (Pawn p in director.Map.mapPawns.AllPawnsSpawned)
                {
                    if (!p.Dead && p.HostileTo(Faction.OfPlayer))
                    {
                        startingHostiles.Add(p);
                    }
                }

                StorytellerPartsHeld =
                    GameComponent_ShowcaseSession.Current?.StorytellerCompsHeld ?? -1;
                NoteReady();
            }

            /// <summary>Gets the tick the tour was READY.</summary>
            public int ReadyTick { get; }

            /// <summary>Gets or sets the tick the verifier stopped watching.</summary>
            public int EndTick { get; set; } = -1;

            /// <summary>
            /// Gets the hostile pawns on the map at READY (a mechhive's, dark
            /// entities): not the tour's doing.
            /// </summary>
            public IReadOnlyCollection<Pawn> HostilesAtReady => startingHostiles;

            /// <summary>
            /// Gets how many storyteller parts the showcase held at READY,
            /// or -1 if it held none.
            /// </summary>
            public int StorytellerPartsHeld { get; }

            /// <summary>
            /// Gets or sets how many ticks before FINISHED the storyteller
            /// was seen with parts (or the showcase not holding them).
            /// </summary>
            public int StorytellerAwakeTicks { get; set; }

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

                    NoteSceneBegins(current, now);

                    showing = current;
                    Captions.Add(Director.Caption);
                    boltsAtStart = Building_BatshittiumBallista.BoltsFired;
                    explosionsAtStart = Building_TractorBomb.Explosions;
                    superposedAtStart = MapComponent_Implosions.Superposed;
                    implosionsInScene = 0;
                    sceneInjuries.Clear();
                    sceneHarm.Clear();
                    foreach (Pawn p in colonists.Keys)
                    {
                        if (!p.Dead && !p.Destroyed)
                        {
                            sceneInjuries[p] = Injuries(p);
                        }
                    }
                }

                if (current != null && current == showing)
                {
                    SampleHarm(current);
                }

                foreach (Implosion i in Running())
                {
                    if (seenImplosions.Add(i))
                    {
                        implosionsInScene++;
                        NoteImplosion(i);
                    }
                }

                if (showing is GrenadeScene cast && cast.Volunteer != null)
                {
                    volunteerSeen = cast.Volunteer;
                }

                if (current != null && current == showing)
                {
                    SampleForSummary(current, now);
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
                    string death = $"{p.LabelShort} ({role}last: {last})";
                    if (FinishedTick >= 0)
                    {
                        // The storyteller is back after FINISHED: a death
                        // in the farewell is the colony's, not the tour's.
                        DeathsAfterTour.Add(death);
                        Log.Message($"[BT-SMOKE] NOTE a colonist died after the "
                            + $"tour finished: {death}");
                        continue;
                    }

                    Deaths.Add((death, current?.key ?? "between scenes", volunteer));
                }
            }

            /// <summary>
            /// Gets the colonists lost after FINISHED, when the colony is the
            /// viewer's again: noted, never failed.
            /// </summary>
            public List<string> DeathsAfterTour { get; } = new List<string>();

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

                // Colonists are never the casualties: in every scene, only
                // the grenade's volunteer (during the grenade) may be hurt.
                List<string> harmed = sceneHarm.Values
                    .Where(h => h.Excuse == null).Select(h => h.What).ToList();
                Fact(scene, "no colonist harmed during the scene "
                        + $"([{string.Join("; ", harmed)}])",
                    harmed.Count == 0);
                List<string> excused = sceneHarm.Values
                    .Where(h => h.Excuse != null)
                    .Select(h => $"{h.What} ({h.Excuse})").ToList();
                if (excused.Count > 0)
                {
                    Log.Message($"[BT-SMOKE] NOTE {scene.key}: colonist harm "
                        + $"excused: {string.Join("; ", excused)}");
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
                        SingularityOver(scene, map);
                        break;
                    case RaccoonScene _:
                        Fact(scene, $"one more implosion ({implosionsInScene})",
                            implosionsInScene >= 1);
                        SingularityOver(scene, map);
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
                    clock.Freeze(now);
                    clocks[clock.Key] = clock;
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

            /// <summary>
            /// The fact that a grenade scene ended after its singularity
            /// did: no singularity field on the map and no implosion still
            /// ticking (what TourScene.SingularityOpen and ImplosionActive
            /// promise), so its explosion cannot land in the next scene.
            /// </summary>
            /// <param name="scene">The scene that ended.</param>
            /// <param name="map">The tour's map.</param>
            private void SingularityOver(ShowcaseScene scene, Map map)
            {
                int fields = map?.listerThings
                    .ThingsOfDef(BT_SingularityDefOf.BT_SingularityField).Count ?? 0;
                int now = Find.TickManager.TicksGame;
                int open = Running().Count(i => i.LastTickedAt >= now - 1);
                Fact(scene, $"the singularity is over at the scene's end "
                        + $"(fields {fields}, implosions still ticking {open})",
                    fields == 0 && open == 0);
            }

            /// <summary>
            /// Notes, once per colonist per scene, a colonist hurt, downed or
            /// killed during the scene, and whether it is excused: the
            /// grenade's volunteer during the grenade, or harm done with one
            /// of the map's starting hostiles beside the colonist.
            /// </summary>
            /// <param name="scene">The scene showing.</param>
            private void SampleHarm(ShowcaseScene scene)
            {
                foreach (KeyValuePair<Pawn, HashSet<Hediff>> was in sceneInjuries)
                {
                    Pawn p = was.Key;
                    if (sceneHarm.ContainsKey(p))
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
                    string what = $"{p.LabelShort} {state} at {at}, doing "
                        + $"{p.CurJobDef?.defName ?? "nothing"}: "
                        + string.Join(", ", fresh.Take(4));
                    string excuse = null;
                    if (scene is GrenadeScene g && ReferenceEquals(g.Volunteer, p))
                    {
                        excuse = "the volunteer";
                    }
                    else
                    {
                        Pawn hostile = startingHostiles.FirstOrDefault(h =>
                            h.Spawned && !h.Dead && at.IsValid
                            && h.Position.InHorDistOf(at, HostileExcuseTiles));
                        if (hostile != null)
                        {
                            excuse = $"a starting hostile, {hostile.LabelShort}, "
                                + $"at {hostile.Position}";
                        }
                    }

                    sceneHarm[p] = (what, excuse);
                }
            }

            /// <summary>A pawn's injuries and lost parts now.</summary>
            /// <param name="p">The pawn.</param>
            /// <returns>The hediffs.</returns>
            private static HashSet<Hediff> Injuries(Pawn p) =>
                new HashSet<Hediff>(p?.health?.hediffSet?.hediffs?
                    .Where(h => h is Hediff_Injury || h is Hediff_MissingPart)
                    ?? Enumerable.Empty<Hediff>());

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
