// Batshit Technology - in-game smoke checks for the Showcase API
// (docs/SHOWCASE-API.md; phase 4000, slice 6000). Part of the -btsmoketest
// runner (Source/SmokeTest.cs); runs after the device tests, in two parts.
// First, checks that tick the game inside one frame, as the device tests
// do: the Harmony patch, launch verdicts against Core's, a directed test
// showcase run through Skip, a timeout and its closing message, and the
// autosave and save-file rules. Then a few real frames, for what only
// drawing can show: the main menu's button, the three menu windows with
// Back and Escape, the caption bar, Stop, and the save folder afterwards.
// The test showcases are made here in code, never shipped in XML, and
// removed again before the runner's final log check.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BatshitTechnology.Showcases;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// A scene for the smoke runner's test showcases: it can wait for an
    /// action that never ends, and it points at a given subject.
    /// </summary>
    internal sealed class SmokeShowcaseScene : ShowcaseScene
    {
        /// <summary>Whether the scene's action never ends.</summary>
        public bool neverDone;

        /// <summary>The thing the scene is about, or null.</summary>
        public Thing subject;

        /// <summary>Gets the thing the scene is about, or null.</summary>
        public override Thing Subject => subject;

        /// <summary>Whether the scene's action has played out.</summary>
        /// <returns>False while <see cref="neverDone"/> is set.</returns>
        public override bool IsDone() => !neverDone;
    }

    /// <summary>The Showcase API's checks in the smoke runner.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>The directed test showcase, with four scenes.</summary>
        private const string DirectorShowcase = "BT_SmokeShowcase_Director";

        /// <summary>A test showcase that flags a loaded mod as a concern.</summary>
        private const string ConcernShowcase = "BT_SmokeShowcase_Concern";

        /// <summary>A test showcase that flags a loaded mod as fatal.</summary>
        private const string FatalShowcase = "BT_SmokeShowcase_Fatal";

        /// <summary>A test showcase that requires a mod nobody has.</summary>
        private const string MissingShowcase = "BT_SmokeShowcase_Missing";

        /// <summary>The shipped showcase, which must be listed too.</summary>
        private const string ShippedShowcase = "BT_Showcase";

        /// <summary>This mod's package id, as About.xml writes it.</summary>
        private const string OwnModId = "breakpoint8088.BatshitTechnology";

        /// <summary>This mod's name, as About.xml writes it.</summary>
        private const string OwnModName = "Batshit Technology";

        /// <summary>A package id no installed mod has.</summary>
        private const string AbsentModId = "breakpoint8088.SmokeTestAbsentMod";

        /// <summary>
        /// The mod the test showcases flag: Core, always loaded. Written in
        /// About.xml's mixed case, which the API must match regardless.
        /// </summary>
        private const string FlaggedModId = "Ludeon.RimWorld";

        /// <summary>The note on every test flag, looked for in the text.</summary>
        private const string FlagNote = "smoke test flag";

        /// <summary>The one patch the mod may hold, as Harmony reports it.</summary>
        private const string ExpectedPatch =
            "Verse.OptionListingUtility.DrawOptionListing: postfix "
            + "BatshitTechnology.Showcases.ShowcaseMenuButton.Postfix";

        /// <summary>How long the frame-driven checks may take, in seconds.</summary>
        private const float ShowcaseFramesTimeout = 120f;

        /// <summary>The zoom of the director test's cut shot.</summary>
        private const float CutShotZoom = 30f;

        /// <summary>The test showcases, while registered.</summary>
        private List<ShowcaseDef> testShowcases;

        /// <summary>The directed test showcase's "waiting" scene's subject.</summary>
        private Thing showcaseSubject;

        /// <summary>The game's showcase session.</summary>
        private GameComponent_ShowcaseSession showcaseSession;

        /// <summary>The second test run, stopped during the frame checks.</summary>
        private ShowcaseDirector stopRun;

        /// <summary>The save folder's files before the showcase checks.</summary>
        private Dictionary<string, string> savesBefore;

        /// <summary>The player's autosave interval before the checks.</summary>
        private float autosaveDaysBefore;

        /// <summary>The newest error before the menu windows opened.</summary>
        private (LogMessage Last, int Repeats) windowsMark;

        /// <summary>The frame-driven steps, in order; null when none remain.</summary>
        private List<FrameStep> frameSteps;

        /// <summary>The step running now.</summary>
        private int frameStep;

        /// <summary>When the frame-driven steps began, in real seconds.</summary>
        private float framesStartedAt;

        /// <summary>
        /// Gets a value indicating whether frame-driven showcase checks are
        /// still to run.
        /// </summary>
        private bool ShowcaseFramesPending =>
            frameSteps != null && frameStep < frameSteps.Count;

        /// <summary>
        /// Runs the synchronous showcase checks, then queues the frame
        /// steps. The game is paused for the frame steps, so the second
        /// test run holds its scene until it is stopped.
        /// </summary>
        /// <param name="map">The quick-test map.</param>
        private void StartShowcaseChecks(Map map)
        {
            showcaseSession = GameComponent_ShowcaseSession.Current;
            if (showcaseSession == null)
            {
                Check("the game has a showcase session", false,
                    "no GameComponent_ShowcaseSession in Current.Game");
                return;
            }

            savesBefore = SnapshotSaves();
            autosaveDaysBefore = Prefs.AutosaveIntervalDays;
            TestShowcasePatch();
            showcaseSubject = SpawnShowcaseSubject(map);
            if (!RegisterTestShowcases())
            {
                EndShowcaseChecks();
                return;
            }

            TestShowcaseVerdicts();
            TestShowcaseGroups();
            TestDirectorRun(map);
            BeginStopRun(map);

            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            frameSteps = BuildFrameSteps();
            frameStep = 0;
            framesStartedAt = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// The Showcases button is the mod's only Harmony patch: one
        /// postfix, on the method vanilla's menus draw their options with.
        /// </summary>
        private void TestShowcasePatch()
        {
            List<string> patches = ShowcaseMenuButton.OwnPatches();
            Check("the Showcases button is the mod's only Harmony patch, a "
                    + "postfix on the menu method",
                ShowcaseMenuButton.Installed
                    && patches.Count == 1 && patches[0] == ExpectedPatch,
                $"installed {ShowcaseMenuButton.Installed}; patches: "
                    + (patches.Count == 0 ? "none" : string.Join("; ", patches)));
        }

        /// <summary>
        /// Spawns the directed test showcase's subject: a slag chunk near
        /// the map's centre, which nothing absorbs or hauls in a tick.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The chunk, or null if it could not be placed.</returns>
        private static Thing SpawnShowcaseSubject(Map map)
        {
            Thing chunk = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
            return GenPlace.TryPlaceThing(chunk, map.Center, map, ThingPlaceMode.Near)
                && chunk.Spawned
                ? chunk
                : null;
        }

        /// <summary>
        /// Makes the test showcases and adds them to the def database under
        /// this mod, beside BT_Showcase, so the menus list them as they
        /// would a mod's own.
        /// </summary>
        /// <returns>True if all four were registered.</returns>
        private bool RegisterTestShowcases()
        {
            ShowcaseDef shipped = DefDatabase<ShowcaseDef>.GetNamedSilentFail(ShippedShowcase);
            ModContentPack pack = shipped?.modContentPack;
            bool ours = pack != null && PackageIds.Normalize(pack.PackageIdPlayerFacing)
                == PackageIds.Normalize(OwnModId);
            Check("BT_Showcase is loaded, from Batshit Technology", ours,
                shipped == null ? "not loaded" : $"from {pack?.PackageIdPlayerFacing}");
            if (!ours)
            {
                return false;
            }

            ShowcaseDef concern = MakeTestShowcase(ConcernShowcase, "smoke concern", pack, 901);
            concern.compatibility.Add(new ShowcaseCompatibilityEntry
            {
                packageId = FlaggedModId,
                severity = ShowcaseSeverity.Concern,
                note = FlagNote,
            });
            ShowcaseDef fatal = MakeTestShowcase(FatalShowcase, "smoke fatal", pack, 902);
            fatal.compatibility.Add(new ShowcaseCompatibilityEntry
            {
                packageId = FlaggedModId,
                severity = ShowcaseSeverity.Fatal,
                note = FlagNote,
            });
            ShowcaseDef missing = MakeTestShowcase(MissingShowcase, "smoke missing", pack, 903);
            missing.requiredMods.Add(AbsentModId);

            testShowcases = new List<ShowcaseDef>
            {
                MakeDirectorShowcase(pack), concern, fatal, missing,
            };
            var clashes = testShowcases
                .Where(d => DefDatabase<ShowcaseDef>.GetNamedSilentFail(d.defName) != null)
                .Select(d => d.defName)
                .ToList();
            if (clashes.Count > 0)
            {
                Check("the test showcases' names are free", false,
                    "already loaded: " + string.Join(", ", clashes));
                testShowcases = null;
                return false;
            }

            foreach (ShowcaseDef def in testShowcases)
            {
                DefDatabase<ShowcaseDef>.Add(def);
            }

            return true;
        }

        /// <summary>Makes a bare test showcase: no scenes, no flags.</summary>
        /// <param name="defName">Its def name.</param>
        /// <param name="label">Its label.</param>
        /// <param name="pack">The mod it is listed under.</param>
        /// <param name="order">Its place in the mod's list.</param>
        /// <returns>The def.</returns>
        private static ShowcaseDef MakeTestShowcase(
            string defName, string label, ModContentPack pack, int order)
        {
            var def = new ShowcaseDef
            {
                defName = defName,
                label = label,
                description = "A test showcase made by the smoke runner.",
                estimatedMinutes = 1f,
                order = order,
                modContentPack = pack,
                generated = true,
            };
            def.ResolveDefNameHash();
            return def;
        }

        /// <summary>
        /// Makes the directed test showcase: "opening" completes after 30
        /// ticks, with a cut shot; "waiting" never finishes, so it is
        /// skipped, and it selects the subject; "timeout" never finishes
        /// and times out after 40 ticks; "closing" completes after 20 ticks
        /// and says its last line 30 ticks later. Narration covers each
        /// outcome, including lines that must never be said.
        /// </summary>
        /// <param name="pack">The mod it is listed under.</param>
        /// <returns>The def.</returns>
        private ShowcaseDef MakeDirectorShowcase(ModContentPack pack)
        {
            ShowcaseDef def = MakeTestShowcase(DirectorShowcase, "smoke director", pack, 900);
            def.workerClass = typeof(ShowcaseDirector);
            var opening = new ShowcaseScene
            {
                key = "opening",
                title = "Opening scene",
                minTicks = 30,
                timeoutTicks = 600,
            };
            opening.shots.Add(new ShowcaseShot { atTick = 0, zoom = CutShotZoom, pan = false });
            var waiting = new SmokeShowcaseScene
            {
                key = "waiting",
                title = "Waiting scene",
                minTicks = 0,
                timeoutTicks = 600,
                neverDone = true,
                subject = showcaseSubject,
            };
            waiting.shots.Add(new ShowcaseShot { atTick = 0, zoom = CutShotZoom, pan = false });
            var timeout = new SmokeShowcaseScene
            {
                key = "timeout",
                title = "Timeout scene",
                minTicks = 0,
                timeoutTicks = 40,
                neverDone = true,
                selectSubject = false,
            };
            var closing = new ShowcaseScene
            {
                key = "closing",
                title = "Closing scene",
                minTicks = 20,
                timeoutTicks = 600,
                selectSubject = false,
            };
            def.scenes.AddRange(new[] { opening, waiting, timeout, closing });
            def.narration.AddRange(new[]
            {
                Line("opening", "start", 0, "Smoke narration: opening."),
                Line("opening", "completed", 0, "Smoke narration: opening completed."),
                Line("opening", "stopped", 0, "Smoke narration: opening stopped."),
                Line("opening", "stopped", 10, "Smoke narration: queued at the stop, never said."),
                Line("waiting", "start", 0, "Smoke narration: waiting."),
                Line("waiting", "skipped", 0, "Smoke narration: waiting skipped."),
                Line("waiting", "completed", 0, "Smoke narration: waiting never completes."),
                Line("timeout", "timedOut", 0, "Smoke narration: timeout timed out."),
                Line("closing", "completed", 30, "Smoke narration: closing, said late."),
            });
            return def;
        }

        /// <summary>Makes one narration line.</summary>
        /// <param name="scene">The scene's key.</param>
        /// <param name="when">The moment.</param>
        /// <param name="delay">Ticks after the moment.</param>
        /// <param name="text">The words.</param>
        /// <returns>The line.</returns>
        private static ShowcaseNarrationLine Line(
            string scene, string when, int delay, string text) =>
            new ShowcaseNarrationLine
            {
                scene = scene,
                when = when,
                delayTicks = delay,
                text = text,
            };

        /// <summary>
        /// The menu's verdicts in game match Core's: a fatal flag refuses,
        /// Force start overrides it, a missing required mod makes a
        /// showcase unavailable even when forced, and a concern warns. The
        /// Core side is given hand-written entries and the active mod list
        /// in About.xml's case, not the defs or the game's lower-case ids.
        /// </summary>
        private void TestShowcaseVerdicts()
        {
            List<string> active = ModsConfig.ActiveModsInLoadOrder
                .Select(m => m.PackageIdPlayerFacing)
                .ToList();
            var flagged = new Func<ShowcaseSeverity, CompatibilityFlag[]>(
                s => new[] { new CompatibilityFlag(FlaggedModId, s, FlagNote) });
            var cases = new (string Name, bool Force, LaunchVerdict Expected,
                ShowcaseEntry Core)[]
            {
                (DirectorShowcase, false, LaunchVerdict.Clear, Entry(DirectorShowcase, null, null)),
                (DirectorShowcase, true, LaunchVerdict.Clear, Entry(DirectorShowcase, null, null)),
                (ConcernShowcase, false, LaunchVerdict.Warned,
                    Entry(ConcernShowcase, null, flagged(ShowcaseSeverity.Concern))),
                (ConcernShowcase, true, LaunchVerdict.Warned,
                    Entry(ConcernShowcase, null, flagged(ShowcaseSeverity.Concern))),
                (FatalShowcase, false, LaunchVerdict.Refused,
                    Entry(FatalShowcase, null, flagged(ShowcaseSeverity.Fatal))),
                (FatalShowcase, true, LaunchVerdict.Forced,
                    Entry(FatalShowcase, null, flagged(ShowcaseSeverity.Fatal))),
                (MissingShowcase, false, LaunchVerdict.Unavailable,
                    Entry(MissingShowcase, new[] { AbsentModId }, null)),
                (MissingShowcase, true, LaunchVerdict.Unavailable,
                    Entry(MissingShowcase, new[] { AbsentModId }, null)),
            };

            var wrong = new List<string>();
            foreach (var c in cases)
            {
                LaunchDecision game = ShowcaseLibrary.Evaluate(
                    ShowcaseLibrary.Named(c.Name), c.Force);
                LaunchDecision core = LaunchDecision.Evaluate(c.Core, active, c.Force);
                if (game.Verdict != c.Expected || core.Verdict != c.Expected)
                {
                    wrong.Add($"{c.Name} force={c.Force}: game {game.Verdict}, "
                        + $"Core {core.Verdict}, expected {c.Expected}");
                }
            }

            Check("every test showcase's in-game verdict matches Core's",
                wrong.Count == 0, string.Join("; ", wrong));

            LaunchDecision fatal = ShowcaseLibrary.Evaluate(
                ShowcaseLibrary.Named(FatalShowcase), force: false);
            string fatalWhy = ShowcaseTexts.WhyNot(fatal);
            Check("a showcase flagged fatal is refused in game, giving the "
                    + "flag's note",
                fatal.Verdict == LaunchVerdict.Refused && !fatal.CanStart
                    && Mentions(fatalWhy, FlagNote),
                $"{fatal.Verdict}; reason '{fatalWhy}'");

            LaunchDecision forced = ShowcaseLibrary.Evaluate(
                ShowcaseLibrary.Named(FatalShowcase), force: true);
            Check("Force start overrides a fatal flag in game",
                forced.Verdict == LaunchVerdict.Forced && forced.CanStart,
                $"{forced.Verdict}, can start {forced.CanStart}");

            ShowcaseDef missingDef = ShowcaseLibrary.Named(MissingShowcase);
            LaunchDecision missing = ShowcaseLibrary.Evaluate(missingDef, force: false);
            LaunchDecision missingForced = ShowcaseLibrary.Evaluate(missingDef, force: true);
            string missingWhy = ShowcaseTexts.WhyNot(missing);
            Check("a showcase missing a required mod is unavailable in game, "
                    + "even forced, naming the mod",
                missing.Verdict == LaunchVerdict.Unavailable && !missing.CanStart
                    && missingForced.Verdict == LaunchVerdict.Unavailable
                    && !missingForced.CanStart
                    && Mentions(missingWhy, AbsentModId),
                $"{missing.Verdict} / forced {missingForced.Verdict}; reason '{missingWhy}'");

            LaunchDecision concern = ShowcaseLibrary.Evaluate(
                ShowcaseLibrary.Named(ConcernShowcase), force: false);
            string concernLine = Dialog_ModShowcases.StatusLine(concern);
            Check("a showcase flagging a concern warns in game and still starts",
                concern.Verdict == LaunchVerdict.Warned && concern.CanStart
                    && Mentions(concernLine, FlagNote),
                $"{concern.Verdict}; status '{concernLine}'");
        }

        /// <summary>
        /// A hand-written Core entry for a test showcase, listed under this
        /// mod.
        /// </summary>
        /// <param name="defName">The showcase.</param>
        /// <param name="required">Its required mods, or null.</param>
        /// <param name="flags">Its flags, or null.</param>
        /// <returns>The entry.</returns>
        private static ShowcaseEntry Entry(
            string defName, string[] required, CompatibilityFlag[] flags) =>
            new ShowcaseEntry(defName, defName, OwnModId, OwnModName, 0, required, flags);

        /// <summary>
        /// Whether text mentions a fragment, in any case: package ids are
        /// normalized to lower case, and the game capitalizes a note that
        /// opens a sentence.
        /// </summary>
        /// <param name="text">The text; may be null.</param>
        /// <param name="fragment">The package id or note.</param>
        /// <returns>True if it does.</returns>
        private static bool Mentions(string text, string fragment) =>
            text != null
            && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// The library groups the test showcases under Batshit Technology
        /// with BT_Showcase, which comes first (its order is 0).
        /// </summary>
        private void TestShowcaseGroups()
        {
            ShowcaseModGroup group = OwnGroup(ShowcaseLibrary.Groups());
            List<string> names = group?.Showcases.Select(e => e.DefName).ToList()
                ?? new List<string>();
            var expected = new[]
            {
                ShippedShowcase, DirectorShowcase, ConcernShowcase,
                FatalShowcase, MissingShowcase,
            };
            Check("the test showcases are listed under Batshit Technology, "
                    + "after BT_Showcase",
                group != null && ShowcaseLibrary.GroupName(group) == OwnModName
                    && names.SequenceEqual(expected),
                group == null
                    ? "no group for " + OwnModId
                    : $"'{ShowcaseLibrary.GroupName(group)}': {string.Join(", ", names)}");
        }

        /// <summary>This mod's group in a list of groups, or null.</summary>
        /// <param name="groups">The groups.</param>
        /// <returns>The group, or null.</returns>
        private static ShowcaseModGroup OwnGroup(IEnumerable<ShowcaseModGroup> groups) =>
            groups?.FirstOrDefault(
                g => g.PackageId == PackageIds.Normalize(OwnModId));

        /// <summary>
        /// Runs the directed test showcase through the game's session to
        /// its end: a cut shot at its zoom, the subject selected, the
        /// caption, Skip, a timeout, the cues in order, and the narration
        /// for each outcome before the closing message.
        /// </summary>
        /// <param name="map">The map.</param>
        private void TestDirectorRun(Map map)
        {
            ShowcaseDef def = ShowcaseLibrary.Named(DirectorShowcase);
            LogMessage mark = Log.Messages.LastOrDefault();
            CameraDriver camera = Find.CameraDriver;
            camera.SetRootSize(45f);
            Find.Selector.ClearSelection();
            showcaseSession.AdoptForTest(def, map);
            var director = showcaseSession.Worker as ShowcaseDirector;
            if (director == null || !showcaseSession.Running)
            {
                Check("the directed test showcase begins", false,
                    $"worker {showcaseSession.Worker?.GetType().Name ?? "none"}, "
                    + $"running {showcaseSession.Running}");
                showcaseSession.ReleaseForTest();
                return;
            }

            Tick(1);
            Check("the director frames a scene's cut shot at its zoom",
                director.CurrentScene?.key == "opening"
                    && Mathf.Abs(camera.RootSize - CutShotZoom) < 0.01f,
                $"scene {director.CurrentScene?.key}; root size {camera.RootSize}");

            TickUntil(() => director.CurrentScene?.key == "waiting", 200);
            Check("the director selects a scene's subject",
                showcaseSubject != null && Find.Selector.IsSelected(showcaseSubject),
                showcaseSubject == null
                    ? "no subject could be placed"
                    : $"selected: {string.Join(", ", Find.Selector.SelectedObjects)}");
            Check("the caption reads 2 of 4: Waiting scene",
                director.Caption == "2 of 4: Waiting scene",
                $"'{director.Caption}'");

            Tick(5);
            showcaseSession.Skip();
            Check("Skip ends the running scene as skipped and starts the next",
                director.CurrentScene?.key == "timeout"
                    && director.Endings.SequenceEqual(
                        new[] { SceneEnd.Completed, SceneEnd.Skipped }),
                $"scene {director.CurrentScene?.key}; endings {Endings(director)}");

            int timeoutStart = Find.TickManager.TicksGame;
            TickUntil(() => director.CurrentScene?.key != "timeout", 200);
            Check("a scene whose action never ends times out",
                director.Endings.Count >= 3 && director.Endings[2] == SceneEnd.TimedOut,
                $"endings {Endings(director)} after "
                    + $"{Find.TickManager.TicksGame - timeoutStart} ticks");

            TickUntil(() => !showcaseSession.Running && showcaseSession.FarewellTick >= 0, 600);
            List<string> cues = ShowcaseCuesSince(mark);
            var expectedCues = new[]
            {
                "GAME " + DirectorShowcase, "START " + DirectorShowcase, "READY",
                "SCENE opening", "END opening completed",
                "SCENE waiting", "SKIP " + DirectorShowcase, "END waiting skipped",
                "SCENE timeout", "END timeout timedOut",
                "SCENE closing", "END closing completed",
                "FINISHED " + DirectorShowcase,
            };
            Check("the director runs its scenes to FINISHED, logging each cue "
                    + "in order",
                cues.SequenceEqual(expectedCues), string.Join(" | ", cues));

            List<NarrationRecord> said = director.NarrationLog.ToList();
            var expectedLines = new[]
            {
                "Smoke narration: opening.",
                "Smoke narration: opening completed.",
                "Smoke narration: waiting.",
                "Smoke narration: waiting skipped.",
                "Smoke narration: timeout timed out.",
                "Smoke narration: closing, said late.",
            };
            int lastLine = said.Count > 0 ? said[said.Count - 1].Tick : -1;
            Check("narration says each scene's lines for how it ended, then "
                    + "the closing message",
                said.Select(r => r.Text).SequenceEqual(expectedLines)
                    && showcaseSession.FarewellTick > lastLine,
                $"said: {string.Join(" | ", said.Select(r => $"{r.Tick}: {r.Text}"))}; "
                    + $"closing message at {showcaseSession.FarewellTick}");
            showcaseSession.ReleaseForTest();
        }

        /// <summary>
        /// Begins the second test run, which is stopped later, during the
        /// frame checks: checks that the session writes nothing into a
        /// save, and that the game cannot reach its autosave.
        /// </summary>
        /// <param name="map">The map.</param>
        private void BeginStopRun(Map map)
        {
            showcaseSession.AdoptForTest(ShowcaseLibrary.Named(DirectorShowcase), map);
            stopRun = showcaseSession.Worker as ShowcaseDirector;
            Tick(2);

            string saved = Scribe.saver.DebugOutputFor(showcaseSession);
            Check("a showcase game's session writes nothing into a save, so a "
                    + "saved showcase loads as an ordinary game",
                !saved.NullOrEmpty() && saved.Contains("saveable")
                    && !saved.Contains(DirectorShowcase),
                $"wrote: {saved?.Replace('\n', ' ')}");

            CheckAutosaveSuppressed("a showcase game never reaches its autosave");
        }

        /// <summary>
        /// Sets the autosaver one tick short of saving and ticks three
        /// times; in a showcase game the counter stays near zero and no
        /// autosave is queued.
        /// </summary>
        /// <param name="name">The check's name.</param>
        private void CheckAutosaveSuppressed(string name)
        {
            if (!TryAutosaveCounter(out FieldInfo counter, out int interval, out string why))
            {
                Check(name, false, why);
                return;
            }

            bool busyBefore = LongEventHandler.AnyEventNowOrWaiting;
            counter.SetValue(Find.Autosaver, interval - 1);
            Tick(3);
            int now = (int)counter.GetValue(Find.Autosaver);
            bool queued = LongEventHandler.AnyEventNowOrWaiting;
            Check(name, !busyBefore && !queued && now <= 3,
                $"counter {now} of {interval}; autosave queued {queued} "
                    + $"(a long event was already waiting: {busyBefore})");
        }

        /// <summary>
        /// Finds the autosaver's private tick counter and its interval
        /// (RimWorld 1.6.4871: Autosaver.ticksSinceSave and
        /// AutosaveIntervalTicks).
        /// </summary>
        /// <param name="counter">The counter field.</param>
        /// <param name="interval">The interval, in ticks.</param>
        /// <param name="why">What is missing, on failure.</param>
        /// <returns>True if both were found.</returns>
        private static bool TryAutosaveCounter(
            out FieldInfo counter, out int interval, out string why)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            counter = typeof(Autosaver).GetField("ticksSinceSave", Private);
            PropertyInfo ticks = typeof(Autosaver).GetProperty("AutosaveIntervalTicks", Private);
            interval = 0;
            why = null;
            if (counter == null || counter.FieldType != typeof(int)
                || ticks == null || ticks.PropertyType != typeof(int))
            {
                why = "Autosaver.ticksSinceSave or AutosaveIntervalTicks is missing";
                return false;
            }

            interval = (int)ticks.GetValue(Find.Autosaver);
            if (interval < 10)
            {
                why = $"autosave interval is only {interval} ticks";
                return false;
            }

            return true;
        }

        /// <summary>The frame-driven steps, in order.</summary>
        /// <returns>The steps.</returns>
        private List<FrameStep> BuildFrameSteps() => new List<FrameStep>
        {
            WaitFrames("caption bar paints", 5),
            new FrameStep("caption bar check", false, CheckCaptionBar),
            new FrameStep("main menu draw", true, CheckMenuButtonDraws),
            new FrameStep("open Showcases", false, OpenShowcasesMenu),
            WaitFrames("mods window paints", 3),
            new FrameStep("mods window", false, CheckModsWindow),
            WaitFrames("mod's window paints", 3),
            new FrameStep("mod's window", false, CheckModWindow),
            WaitFrames("fatal details paint", 3),
            new FrameStep("fatal details", false, CheckFatalDetails),
            WaitFrames("back to the mod's window", 3),
            new FrameStep("back to the mod", false, CheckBackToMod),
            WaitFrames("back to the mods window", 3),
            new FrameStep("back to the mods", false, CheckBackToMods),
            WaitFrames("mod's window again", 3),
            new FrameStep("open BT_Showcase", false, OpenShippedShowcase),
            WaitFrames("BT_Showcase details paint", 3),
            new FrameStep("BT_Showcase details", false, CheckShippedDetails),
            new FrameStep("press Escape", true, PressEscape),
            WaitFrames("Escape takes effect", 3),
            new FrameStep("menu closed", false, CheckMenuClosed),
            new FrameStep("stop the second run", false, StopSecondRun),
            WaitFrames("queued events run", 30),
            new FrameStep("save folder", false, CheckSaveFolder),
        };

        /// <summary>A step that waits a number of frames.</summary>
        /// <param name="name">The step's name.</param>
        /// <param name="frames">How many frames.</param>
        /// <returns>The step.</returns>
        private static FrameStep WaitFrames(string name, int frames)
        {
            int left = frames;
            return new FrameStep(name, false, () => --left <= 0);
        }

        /// <summary>
        /// Runs the current frame step if it belongs to this call (a frame
        /// update or a repaint event), moving on when it completes. A step
        /// that throws, or steps that take too long, fail a check and end
        /// the run.
        /// </summary>
        /// <param name="inGui">Whether this call is a repaint event.</param>
        private void AdvanceShowcaseFrames(bool inGui)
        {
            if (!ShowcaseFramesPending)
            {
                return;
            }

            FrameStep step = frameSteps[frameStep];
            if (Time.realtimeSinceStartup - framesStartedAt > ShowcaseFramesTimeout)
            {
                Check("the showcase frame checks finish in time", false,
                    $"still at '{step.Name}' after {ShowcaseFramesTimeout} s");
                EndShowcaseChecks();
                Finish();
                return;
            }

            if (step.InGui != inGui)
            {
                return;
            }

            bool complete;
            try
            {
                complete = step.Run();
            }
            catch (Exception e)
            {
                Check($"the showcase frame step '{step.Name}' ran without an "
                    + "exception", false, e.ToString());
                EndShowcaseChecks();
                Finish();
                return;
            }

            if (complete && ++frameStep >= frameSteps.Count)
            {
                EndShowcaseChecks();
                Finish();
            }
        }

        /// <summary>
        /// The caption bar paints in real frames while the second run
        /// plays, without failing.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckCaptionBar()
        {
            Check("the caption bar paints in real frames without failing",
                showcaseSession.Running && showcaseSession.CaptionBarFrames >= 3
                    && !showcaseSession.CaptionBarFailed,
                $"running {showcaseSession.Running}; painted "
                    + $"{showcaseSession.CaptionBarFrames} frames; failed "
                    + showcaseSession.CaptionBarFailed);
            return true;
        }

        /// <summary>
        /// Draws vanilla's menu options twice, off screen, through the
        /// patched method: once as the main menu (the Entry scene's state,
        /// set for this one call), where exactly one Showcases button must
        /// be added, and once as the in-game menu, where none may be. Both
        /// draws also pass the column of web links through the patch.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckMenuButtonDraws()
        {
            var offScreen = new Rect(-4000f, -4000f, 450f, 700f);
            int calls = ShowcaseMenuButton.PostfixCalls;
            int drawn = ShowcaseMenuButton.ButtonsDrawn;
            ProgramState state = Current.ProgramState;
            try
            {
                Current.ProgramState = ProgramState.Entry;
                MainMenuDrawer.DoMainMenuControls(offScreen, anyMapFiles: false);
            }
            finally
            {
                Current.ProgramState = state;
            }

            int mainCalls = ShowcaseMenuButton.PostfixCalls - calls;
            int mainDrawn = ShowcaseMenuButton.ButtonsDrawn - drawn;
            string label = ShowcaseMenuButton.Button?.label;
            Check("the main menu gets exactly one Showcases button",
                mainCalls == 2 && mainDrawn == 1
                    && label == "BT_Showcases_MenuButton".Translate().Resolve(),
                $"postfix ran {mainCalls} times; buttons drawn {mainDrawn}; "
                    + $"label '{label}'");

            calls = ShowcaseMenuButton.PostfixCalls;
            drawn = ShowcaseMenuButton.ButtonsDrawn;
            MainMenuDrawer.DoMainMenuControls(offScreen, anyMapFiles: false);
            int gameCalls = ShowcaseMenuButton.PostfixCalls - calls;
            int gameDrawn = ShowcaseMenuButton.ButtonsDrawn - drawn;
            Check("the in-game menu gets no Showcases button",
                gameCalls == 2 && gameDrawn == 0,
                $"postfix ran {gameCalls} times; buttons drawn {gameDrawn}");
            return true;
        }

        /// <summary>
        /// Runs the Showcases button's own action, as a click does.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool OpenShowcasesMenu()
        {
            windowsMark = ErrorMark();
            ListableOption button = ShowcaseMenuButton.Button;
            if (button?.action == null)
            {
                Check("the Showcases button has an action", false, "no button was drawn");
                return true;
            }

            button.action();
            return true;
        }

        /// <summary>
        /// The button opened the mods window, which painted and lists
        /// Batshit Technology; then opens its showcases, as a click does.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckModsWindow()
        {
            var mods = Find.WindowStack.WindowOfType<Dialog_ShowcaseMods>();
            int index = mods == null ? -1 : IndexOfOwnGroup(mods.Groups);
            Check("the Showcases button opens the mods window, listing Batshit "
                    + "Technology",
                mods != null && mods.FramesDrawn >= 2 && index >= 0,
                mods == null
                    ? "no mods window is open"
                    : $"painted {mods.FramesDrawn} frames; groups: "
                        + string.Join(", ", mods.Groups.Select(ShowcaseLibrary.GroupName)));
            mods?.OpenGroup(index);
            return true;
        }

        /// <summary>The index of this mod's group, or -1.</summary>
        /// <param name="groups">The groups, in menu order.</param>
        /// <returns>The index.</returns>
        private static int IndexOfOwnGroup(IReadOnlyList<ShowcaseModGroup> groups)
        {
            ShowcaseModGroup own = OwnGroup(groups);
            return own == null ? -1 : groups.ToList().IndexOf(own);
        }

        /// <summary>
        /// The mod's window replaced the mods window and lists BT_Showcase,
        /// greys out the showcase missing a mod with the reason, and shows
        /// the flags; a greyed-out row does not open. Then opens the fatal
        /// showcase's details.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckModWindow()
        {
            var window = Find.WindowStack.WindowOfType<Dialog_ModShowcases>();
            if (window == null)
            {
                Check("choosing a mod opens its showcases in place of the mods "
                    + "window", false, "no mod's window is open");
                return true;
            }

            int shipped = RowOf(window, ShippedShowcase);
            int missing = RowOf(window, MissingShowcase);
            int fatal = RowOf(window, FatalShowcase);
            int concern = RowOf(window, ConcernShowcase);
            string missingLine = Dialog_ModShowcases.StatusLine(window.DecisionAt(missing));
            string fatalLine = Dialog_ModShowcases.StatusLine(window.DecisionAt(fatal));
            string concernLine = Dialog_ModShowcases.StatusLine(window.DecisionAt(concern));
            bool replaced = !Find.WindowStack.IsOpen<Dialog_ShowcaseMods>();
            Check("a mod's window lists BT_Showcase, greys out a showcase "
                    + "missing a mod with the reason, and shows flags",
                replaced && window.FramesDrawn >= 2
                    && OwnGroup(new[] { window.Group }) != null
                    && shipped >= 0 && Dialog_ModShowcases.IsAvailable(window.DecisionAt(shipped))
                    && missing >= 0 && !Dialog_ModShowcases.IsAvailable(window.DecisionAt(missing))
                    && Mentions(missingLine, AbsentModId)
                    && fatal >= 0 && Dialog_ModShowcases.IsAvailable(window.DecisionAt(fatal))
                    && Mentions(fatalLine, FlagNote)
                    && concern >= 0 && Mentions(concernLine, FlagNote),
                $"mods window closed {replaced}; painted {window.FramesDrawn}; rows "
                    + $"{window.RowCount} (shipped {shipped}, missing {missing}, fatal "
                    + $"{fatal}, concern {concern}); missing '{missingLine}'; fatal "
                    + $"'{fatalLine}'; concern '{concernLine}'");

            bool opened = window.OpenShowcase(missing);
            Check("a greyed-out showcase cannot be opened",
                missing >= 0 && !opened && Find.WindowStack.IsOpen(window)
                    && !Find.WindowStack.IsOpen<Dialog_ShowcaseDetails>(),
                $"opened {opened}; details open "
                    + Find.WindowStack.IsOpen<Dialog_ShowcaseDetails>());
            if (!opened)
            {
                window.OpenShowcase(fatal);
            }

            return true;
        }

        /// <summary>The row of a showcase in a mod's window, or -1.</summary>
        /// <param name="window">The window.</param>
        /// <param name="defName">The showcase.</param>
        /// <returns>The row.</returns>
        private static int RowOf(Dialog_ModShowcases window, string defName)
        {
            for (int i = 0; i < window.RowCount; i++)
            {
                if (window.DefAt(i)?.defName == defName)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// The fatal showcase's details refuse Start, say why, and offer
        /// Force start (dev mode is on in smoke runs). Then goes Back.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckFatalDetails()
        {
            var details = Find.WindowStack.WindowOfType<Dialog_ShowcaseDetails>();
            List<string> facts = details?.FactLines.ToList() ?? new List<string>();
            Check("the details page refuses a fatal flag with the reason, and "
                    + "offers Force start in dev mode",
                details != null && details.Def?.defName == FatalShowcase
                    && details.FramesDrawn >= 2
                    && !details.StartEnabled && details.ForceOffered
                    && facts.Any(f => Mentions(f, FlagNote))
                    && !Find.WindowStack.IsOpen<Dialog_ModShowcases>(),
                details == null
                    ? "no details window is open"
                    : $"{details.Def?.defName}; painted {details.FramesDrawn}; start "
                        + $"{details.StartEnabled}; force {details.ForceOffered}; "
                        + $"facts: {string.Join(" | ", facts)}");
            details?.Back();
            return true;
        }

        /// <summary>
        /// Back from the details returned to the mod's window; then goes
        /// Back again.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckBackToMod()
        {
            var window = Find.WindowStack.WindowOfType<Dialog_ModShowcases>();
            Check("Back from a showcase's details returns to its mod's showcases",
                window != null && OwnGroup(new[] { window.Group }) != null
                    && window.FramesDrawn >= 2
                    && !Find.WindowStack.IsOpen<Dialog_ShowcaseDetails>(),
                $"mod's window {(window == null ? "closed" : "open")}; details "
                    + $"open {Find.WindowStack.IsOpen<Dialog_ShowcaseDetails>()}");
            window?.Back();
            return true;
        }

        /// <summary>
        /// Back from the mod's window returned to the mods window; then
        /// opens the mod again.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckBackToMods()
        {
            var mods = Find.WindowStack.WindowOfType<Dialog_ShowcaseMods>();
            Check("Back from a mod's showcases returns to the mods window",
                mods != null && mods.FramesDrawn >= 2
                    && !Find.WindowStack.IsOpen<Dialog_ModShowcases>(),
                $"mods window {(mods == null ? "closed" : "open")}; mod's window "
                    + $"open {Find.WindowStack.IsOpen<Dialog_ModShowcases>()}");
            mods?.OpenGroup(IndexOfOwnGroup(mods.Groups));
            return true;
        }

        /// <summary>Opens BT_Showcase's details from the mod's window.</summary>
        /// <returns>True: the step is complete.</returns>
        private bool OpenShippedShowcase()
        {
            var window = Find.WindowStack.WindowOfType<Dialog_ModShowcases>();
            window?.OpenShowcase(RowOf(window, ShippedShowcase));
            return true;
        }

        /// <summary>
        /// BT_Showcase's details offer Start and no Force start; a showcase
        /// missing a mod would offer neither.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckShippedDetails()
        {
            var details = Find.WindowStack.WindowOfType<Dialog_ShowcaseDetails>();
            Check("BT_Showcase's details offer Start, and no Force start",
                details != null && details.Def?.defName == ShippedShowcase
                    && details.FramesDrawn >= 2
                    && details.StartEnabled && !details.ForceOffered,
                details == null
                    ? "no details window is open"
                    : $"{details.Def?.defName}; painted {details.FramesDrawn}; start "
                        + $"{details.StartEnabled}; force {details.ForceOffered}");

            var missing = new Dialog_ShowcaseDetails(
                ShowcaseLibrary.Named(MissingShowcase), null);
            Check("a showcase missing a mod offers neither Start nor Force start",
                !missing.StartEnabled && !missing.ForceOffered,
                $"start {missing.StartEnabled}; force {missing.ForceOffered}");
            return true;
        }

        /// <summary>
        /// Presses Escape: a real key-down event is made current while the
        /// window stack handles the cancel key, as it does for a player.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool PressEscape()
        {
            Event current = Event.current;
            try
            {
                Event.current = new Event
                {
                    type = EventType.KeyDown,
                    keyCode = KeyCode.Escape,
                };
                Find.WindowStack.Notify_PressedCancel();
            }
            finally
            {
                Event.current = current;
            }

            return true;
        }

        /// <summary>
        /// Escape, pressed on a showcase's details, closed the whole menu,
        /// and the windows logged no error.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckMenuClosed()
        {
            List<string> open = Find.WindowStack.Windows
                .OfType<ShowcaseWindow>()
                .Select(w => w.GetType().Name)
                .ToList();
            Check("Escape closes the whole Showcases menu",
                open.Count == 0, "still open: " + string.Join(", ", open));
            Check("the Showcases windows log no error",
                !NewErrorSince(windowsMark), "an error was logged (see LOGGED below)");
            return true;
        }

        /// <summary>
        /// Stops the second run, as the Stop button does: its scene ends as
        /// stopped, no later scene runs, and only the stop's own line is
        /// said. A stopped showcase game still never autosaves; released
        /// to an ordinary game, it autosaves as usual, and the player's
        /// autosave setting is unchanged. Then removes the test showcases.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool StopSecondRun()
        {
            LogMessage mark = Log.Messages.LastOrDefault();
            int said = stopRun?.NarrationLog.Count ?? 0;
            showcaseSession.StopShowcase(byViewer: true);
            Tick(300);
            List<string> cues = ShowcaseCuesSince(mark);
            List<string> lines = stopRun?.NarrationLog.Skip(said).Select(r => r.Text).ToList()
                ?? new List<string>();
            Check("Stop ends the showcase: its scene ends as stopped, no later "
                    + "scene runs, and no more narration",
                stopRun != null && !showcaseSession.Running
                    && stopRun.State == SequencerState.Stopped
                    && stopRun.Endings.SequenceEqual(new[] { SceneEnd.Stopped })
                    && cues.SequenceEqual(new[]
                    {
                        "END opening stopped", "STOPPED " + DirectorShowcase,
                    })
                    && lines.SequenceEqual(new[] { "Smoke narration: opening stopped." }),
                $"running {showcaseSession.Running}; state {stopRun?.State}; endings "
                    + $"{(stopRun == null ? "none" : Endings(stopRun))}; cues "
                    + $"{string.Join(" | ", cues)}; lines {string.Join(" | ", lines)}");

            CheckAutosaveSuppressed("a stopped showcase game still never reaches "
                + "its autosave");
            showcaseSession.ReleaseForTest();
            CheckAutosaveRestored();
            RemoveTestShowcases();
            return true;
        }

        /// <summary>
        /// Once the game is no longer a showcase game, the autosaver counts
        /// every tick again, and the player's autosave interval is as it
        /// was: the suppression changed no setting.
        /// </summary>
        private void CheckAutosaveRestored()
        {
            const string Name = "autosave counts again once the game is no "
                + "longer a showcase game, and the autosave setting is unchanged";
            if (!TryAutosaveCounter(out FieldInfo counter, out int interval, out string why))
            {
                Check(Name, false, why);
                return;
            }

            counter.SetValue(Find.Autosaver, 100);
            Tick(10);
            int now = (int)counter.GetValue(Find.Autosaver);
            float days = Prefs.AutosaveIntervalDays;
            counter.SetValue(Find.Autosaver, 0);
            Check(Name, now == 110 && days == autosaveDaysBefore,
                $"counter {now} after 10 ticks from 100 (interval {interval}); "
                    + $"autosave every {days} days, was {autosaveDaysBefore}");
        }

        /// <summary>
        /// The save folder gained and changed no files during the showcase
        /// checks, and nothing (such as an autosave) is left waiting to run.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckSaveFolder()
        {
            Dictionary<string, string> after = SnapshotSaves();
            List<string> changed = after
                .Where(f => !savesBefore.TryGetValue(f.Key, out string was) || was != f.Value)
                .Select(f => Path.GetFileName(f.Key))
                .Concat(savesBefore.Keys.Where(k => !after.ContainsKey(k))
                    .Select(k => Path.GetFileName(k) + " (removed)"))
                .ToList();
            bool waiting = LongEventHandler.AnyEventNowOrWaiting;
            Check("the save folder gains and changes no files during the "
                    + "showcase checks",
                changed.Count == 0 && !waiting,
                $"{GenFilePaths.SavedGamesFolderPath}: "
                    + (changed.Count == 0 ? "unchanged" : string.Join(", ", changed))
                    + $"; long event waiting {waiting}");
            return true;
        }

        /// <summary>
        /// Every file in the save folder, with its size and last write
        /// time.
        /// </summary>
        /// <returns>File path to "size:time".</returns>
        private static Dictionary<string, string> SnapshotSaves()
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string folder = GenFilePaths.SavedGamesFolderPath;
            if (!Directory.Exists(folder))
            {
                return files;
            }

            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                files[file] = $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
            }

            return files;
        }

        /// <summary>
        /// Puts everything back: closes the menu windows, makes the game an
        /// ordinary game again, removes the test showcases and the subject,
        /// and drops any remaining frame steps. Safe to call twice.
        /// </summary>
        private void EndShowcaseChecks()
        {
            frameSteps = null;
            try
            {
                foreach (ShowcaseWindow window in Find.WindowStack.Windows
                    .OfType<ShowcaseWindow>().ToList())
                {
                    Find.WindowStack.TryRemove(window, doCloseSound: false);
                }

                showcaseSession?.ReleaseForTest();
                RemoveTestShowcases();
                if (showcaseSubject != null && showcaseSubject.Spawned)
                {
                    showcaseSubject.Destroy(DestroyMode.Vanish);
                }

                showcaseSubject = null;
            }
            catch (Exception e)
            {
                Check("the showcase checks clean up after themselves", false, e.ToString());
            }
        }

        /// <summary>
        /// Removes the test showcases from the def database (its Remove is
        /// private in RimWorld 1.6.4871), and checks they are gone.
        /// </summary>
        private void RemoveTestShowcases()
        {
            if (testShowcases == null)
            {
                return;
            }

            MethodInfo remove = typeof(DefDatabase<ShowcaseDef>).GetMethod(
                "Remove", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(ShowcaseDef) }, null);
            foreach (ShowcaseDef def in testShowcases)
            {
                remove?.Invoke(null, new object[] { def });
            }

            List<string> left = testShowcases
                .Where(d => DefDatabase<ShowcaseDef>.GetNamedSilentFail(d.defName) != null)
                .Select(d => d.defName)
                .ToList();
            testShowcases = null;
            Check("the test showcases are removed again", left.Count == 0,
                (remove == null ? "DefDatabase.Remove not found; " : string.Empty)
                    + "still loaded: " + string.Join(", ", left));
        }

        /// <summary>
        /// The Showcase API's log cues since a message, without the prefix.
        /// </summary>
        /// <param name="mark">The last message before; null for all.</param>
        /// <returns>The cues, in order.</returns>
        private static List<string> ShowcaseCuesSince(LogMessage mark)
        {
            IEnumerable<LogMessage> since = mark == null
                ? Log.Messages
                : Log.Messages.SkipWhile(m => m != mark).Skip(1);
            string prefix = ShowcaseLog.Prefix + " ";
            return since
                .Where(m => m.text != null && m.text.StartsWith(prefix, StringComparison.Ordinal))
                .Select(m => m.text.Substring(prefix.Length))
                .ToList();
        }

        /// <summary>A director's endings, for failure details.</summary>
        /// <param name="director">The director.</param>
        /// <returns>The endings, comma-separated.</returns>
        private static string Endings(ShowcaseDirector director) =>
            director.Endings.Count == 0 ? "none" : string.Join(", ", director.Endings);

        /// <summary>
        /// Ticks until a condition holds, or a number of ticks has passed.
        /// </summary>
        /// <param name="condition">The condition.</param>
        /// <param name="maxTicks">The most ticks to run.</param>
        /// <returns>Whether the condition holds.</returns>
        private static bool TickUntil(Func<bool> condition, int maxTicks)
        {
            for (int i = 0; i < maxTicks && !condition(); i++)
            {
                Tick(1);
            }

            return condition();
        }

        /// <summary>One step of the frame-driven checks.</summary>
        private sealed class FrameStep
        {
            /// <summary>Creates a step.</summary>
            /// <param name="name">Its name, for failure details.</param>
            /// <param name="inGui">
            /// Whether it runs in a repaint event rather than a frame update.
            /// </param>
            /// <param name="run">Runs it; returns true when it is complete.</param>
            public FrameStep(string name, bool inGui, Func<bool> run)
            {
                Name = name;
                InGui = inGui;
                Run = run ?? throw new ArgumentNullException(nameof(run));
            }

            /// <summary>Gets the step's name.</summary>
            public string Name { get; }

            /// <summary>Gets a value indicating whether it runs in a repaint event.</summary>
            public bool InGui { get; }

            /// <summary>Gets the step's action; true when complete.</summary>
            public Func<bool> Run { get; }
        }
    }
}
