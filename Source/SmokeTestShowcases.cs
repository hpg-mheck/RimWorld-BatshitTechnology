// Batshit Technology - in-game smoke checks for the Showcase API
// (docs/SHOWCASE-API.md; phase 4000, slices 6000 and 8000). Part of the
// -btsmoketest runner (Source/SmokeTest.cs); runs after the device tests,
// in two parts. First, checks that tick the game inside one frame, as the
// device tests do: the Harmony patch and the menu column's gates, the
// launcher's guards, launch verdicts against Core's, a directed test
// showcase run through Skip, a timeout and its closing message (narration
// on the overlay, never in vanilla's messages), scenes that throw, the god
// mode a showcase borrows, and the autosave and save-file rules. Then a
// few real frames, for what only drawing can show: the caption bar, the
// narration overlay replacing one line with the next in real time, a cut
// that follows a glide, the main menu's button, the three menu windows
// with Back and Escape, Stop (which clears the overlay), and the save
// folder afterwards. The test showcases are made here in code, never
// shipped in XML, and removed again before the runner's final log check.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// A scene for the smoke runner's test showcases: it can wait for an
    /// action that never ends, point at a given subject, and throw from
    /// any of its hooks, to test how the director handles a broken scene.
    /// </summary>
    internal sealed class SmokeShowcaseScene : ShowcaseScene
    {
        /// <summary>
        /// The text every exception a test scene throws on purpose
        /// carries, so the runner can tell its own faults from real ones.
        /// </summary>
        public const string FaultMarker = "smoke test fault";

        /// <summary>Whether the scene's action never ends.</summary>
        public bool neverDone;

        /// <summary>The thing the scene is about, or null.</summary>
        public Thing subject;

        /// <summary>Whether <see cref="Setup"/> throws.</summary>
        public bool throwInSetup;

        /// <summary>Whether <see cref="Tick"/> throws.</summary>
        public bool throwInTick;

        /// <summary>Whether <see cref="IsDone"/> throws.</summary>
        public bool throwInIsDone;

        /// <summary>Whether <see cref="End"/> throws.</summary>
        public bool throwInEnd;

        /// <summary>
        /// Why the scene declines to run, or null (it runs).
        /// </summary>
        public string unavailableReason;

        /// <summary>
        /// The scene's own tick on which it plays the beat
        /// <see cref="beatMoment"/>; negative for none.
        /// </summary>
        public int beatAtTick = -1;

        /// <summary>The moment of the beat it plays, if any.</summary>
        public string beatMoment;

        /// <summary>How many of its own ticks this run has had.</summary>
        private int ticksRun;

        /// <summary>Gets the thing the scene is about, or null.</summary>
        public override Thing Subject => subject;

        /// <summary>Gets why the scene declines to run, or null.</summary>
        public override string UnavailableReason => unavailableReason;

        /// <summary>Sets the scene up, or throws if told to.</summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="throwInSetup"/> is set.
        /// </exception>
        public override void Setup()
        {
            ticksRun = 0;
            ThrowIf(throwInSetup, "Setup");
        }

        /// <summary>
        /// Runs a tick, playing the beat when its tick comes, or throws if
        /// told to.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="throwInTick"/> is set.
        /// </exception>
        public override void Tick()
        {
            ThrowIf(throwInTick, "Tick");
            ticksRun++;
            if (ticksRun == beatAtTick && !beatMoment.NullOrEmpty())
            {
                Beat(beatMoment);
            }
        }

        /// <summary>Whether the scene's action has played out.</summary>
        /// <returns>False while <see cref="neverDone"/> is set.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="throwInIsDone"/> is set.
        /// </exception>
        public override bool IsDone()
        {
            ThrowIf(throwInIsDone, "IsDone");
            return !neverDone;
        }

        /// <summary>Ends the scene, or throws if told to.</summary>
        /// <param name="ending">How it ended.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="throwInEnd"/> is set.
        /// </exception>
        public override void End(SceneEnd ending)
        {
            ThrowIf(throwInEnd, "End");
        }

        /// <summary>Throws a marked fault when told to.</summary>
        /// <param name="when">Whether to throw.</param>
        /// <param name="where">The hook throwing, for the message.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="when"/> is true.
        /// </exception>
        private void ThrowIf(bool when, string where)
        {
            if (when)
            {
                throw new InvalidOperationException(
                    $"{FaultMarker} in {where} of scene '{key}'");
            }
        }
    }

    /// <summary>
    /// A smoke-test worker that asks for its devices' DEV buttons, as
    /// BatshitTourDirector does, and never finishes on its own.
    /// </summary>
    internal class SmokeDevButtonsWorker : ShowcaseWorker
    {
        /// <summary>Gets a value indicating whether it is done: never.</summary>
        public override bool Finished => false;

        /// <summary>Asks for the DEV buttons (god mode, lent by the API).</summary>
        /// <param name="map">The map.</param>
        public override void Setup(Map map)
        {
            EnableDevButtons();
        }
    }

    /// <summary>
    /// A <see cref="SmokeDevButtonsWorker"/> that finishes two ticks after
    /// it starts.
    /// </summary>
    internal sealed class SmokeDevButtonsFinishingWorker : SmokeDevButtonsWorker
    {
        /// <summary>
        /// Gets a value indicating whether two ticks have passed since
        /// set-up.
        /// </summary>
        public override bool Finished => Find.TickManager.TicksGame - StartTick >= 2;
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

        /// <summary>
        /// A directed test showcase whose scenes throw (made in code and run
        /// directly, never listed in the menus).
        /// </summary>
        private const string FaultShowcase = "BT_SmokeShowcase_Faults";

        /// <summary>
        /// A test showcase that asks for DEV buttons (run directly, never
        /// listed).
        /// </summary>
        private const string DevButtonsShowcase = "BT_SmokeShowcase_DevButtons";

        /// <summary>
        /// A test showcase with a glide and a cut on the same tick (run
        /// directly, never listed).
        /// </summary>
        private const string CameraShowcase = "BT_SmokeShowcase_Camera";

        /// <summary>The zoom of the camera test's glide.</summary>
        private const float GlideZoom = 50f;

        /// <summary>How long the camera test's glide would take, in seconds.</summary>
        private const float GlideSeconds = 3f;

        /// <summary>
        /// How long the camera test lets real frames run before judging the
        /// cut, in seconds: well inside the glide, which would otherwise
        /// still be pulling the camera.
        /// </summary>
        private const float CutHoldSeconds = 0.5f;

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

        /// <summary>
        /// The overlay test's first line: long, so its full reading time
        /// (7 s and more) is far longer than the base time after which the
        /// next line replaces it.
        /// </summary>
        private const string OverlayLongLine = "Smoke narration: a long line "
            + "for the overlay, replaced by the next one after its base time.";

        /// <summary>The overlay test's second line, which must replace it.</summary>
        private const string OverlayNextLine = "Smoke narration: the line after.";

        /// <summary>
        /// How long the overlay test's second line may take to appear, in
        /// real seconds: two base times (the stop run's opening line, then
        /// the long line) and some slack, well short of the long line's
        /// full reading time.
        /// </summary>
        private const float OverlayReplaceSeconds = 7f;

        /// <summary>
        /// Vanilla's live messages (Verse.Messages.liveMessages, private in
        /// RimWorld 1.6.4871), read to check narration never reaches them.
        /// </summary>
        private static readonly FieldInfo LiveMessagesField =
            typeof(Messages).GetField(
                "liveMessages", BindingFlags.Static | BindingFlags.NonPublic);

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

        /// <summary>The camera test's director, while it runs.</summary>
        private ShowcaseDirector cameraRun;

        /// <summary>Where the camera test's cut points.</summary>
        private IntVec3 cutCell;

        /// <summary>The camera test's cut zoom, as the camera allows it.</summary>
        private float cutZoom;

        /// <summary>When the camera test framed its shots, in real seconds.</summary>
        private float cutFramedAt;

        /// <summary>Frames drawn since the camera test framed its shots.</summary>
        private int cutFrames;

        /// <summary>When the overlay test said its lines, in real seconds.</summary>
        private float overlaySaidAt;

        /// <summary>The overlay's painted frames before the overlay test.</summary>
        private int overlayFramesBefore;

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
            TestQuickTestMenuGuard();
            TestShowcasePatch();
            TestMenuColumnGates();
            showcaseSubject = SpawnShowcaseSubject(map);
            if (!RegisterTestShowcases())
            {
                EndShowcaseChecks();
                return;
            }

            TestLaunchGuards();
            TestRequestBinding();
            TestShowcaseVerdicts();
            TestStoppedLineConfigError();
            TestForceOutsideDevMode();
            TestShowcaseGroups();
            TestDirectorRun(map);
            TestFaultyScenes(map);
            TestDevButtonLoan(map);
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
        /// Under -quicktest (which the smoke run uses), the main menu is
        /// drawn for a frame while vanilla's quick-test game is about to
        /// generate. It must leave -showcase= alone then, so the quick-test
        /// game adopts the showcase instead of a second generation racing
        /// it (ShowcaseStartRules.MenuActsOnCommandLine).
        /// </summary>
        private void TestQuickTestMenuGuard()
        {
            bool quickTest = GenCommandLine.CommandLineArgPassed(ShowcaseStartRules.QuickTestArg);
            Log.Message("[BT-SMOKE] NOTE the main menu reported itself shown "
                + $"{ShowcaseLauncher.MainMenuShownCount} times before this "
                + $"quick-test game; -quicktest passed {quickTest}");
            Check("under -quicktest the main menu leaves -showcase= to the "
                    + "quick-test game",
                quickTest && !ShowcaseLauncher.MenuActedOnCommandLine,
                $"-quicktest {quickTest}; the menu acted on the command line "
                    + $"{ShowcaseLauncher.MenuActedOnCommandLine} after being shown "
                    + $"{ShowcaseLauncher.MainMenuShownCount} times");
        }

        /// <summary>
        /// The patch's test for the main menu's own column, one gate at a
        /// time, on lists made here: a positive control first (Entry, ending
        /// in Quit to OS), then each gate alone must reject an otherwise
        /// valid list: the in-game state, a web link, a null entry, a last
        /// option that only calls Quit to OS from a closure, and an empty
        /// or missing list. Vanilla's real menus cannot tell these gates
        /// apart (its in-game Quit to OS is a closure already).
        /// </summary>
        private void TestMenuColumnGates()
        {
            Action quit = Root.Shutdown;
            Action wrapped = () => Root.Shutdown(); // never invoked
            var cases = new (string Name, ProgramState State, List<ListableOption> List,
                bool Expected)[]
            {
                ("main menu column", ProgramState.Entry, new List<ListableOption>
                {
                    new ListableOption("a", null), new ListableOption("q", quit),
                }, true),
                ("in-game state", ProgramState.Playing, new List<ListableOption>
                {
                    new ListableOption("q", quit),
                }, false),
                ("web link", ProgramState.Entry, new List<ListableOption>
                {
                    new ListableOption_WebLink("w", "https://example.invalid", null),
                    new ListableOption("q", quit),
                }, false),
                ("web link ending in Quit", ProgramState.Entry, new List<ListableOption>
                {
                    new ListableOption_WebLink("w", quit, null),
                }, false),
                ("null entry", ProgramState.Entry, new List<ListableOption>
                {
                    new ListableOption("a", null), null, new ListableOption("q", quit),
                }, false),
                ("Quit only through a closure", ProgramState.Entry, new List<ListableOption>
                {
                    new ListableOption("q", wrapped),
                }, false),
                ("empty list", ProgramState.Entry, new List<ListableOption>(), false),
                ("no list", ProgramState.Entry, null, false),
            };

            var results = new List<string>();
            bool ok = true;
            ProgramState state = Current.ProgramState;
            try
            {
                foreach (var c in cases)
                {
                    Current.ProgramState = c.State;
                    bool got = ShowcaseMenuButton.IsMainMenuColumn(c.List);
                    ok &= got == c.Expected;
                    results.Add($"{c.Name} {got} (want {c.Expected})");
                }
            }
            finally
            {
                Current.ProgramState = state;
            }

            Check("the main menu's column is told apart by each gate on its own",
                ok, string.Join("; ", results));
        }

        /// <summary>
        /// The launcher refuses what it must, saying why and queueing
        /// nothing: a missing showcase, any showcase from inside a game,
        /// and (from the main menu's state, for one call) a showcase flagged
        /// fatal. A launch that could start is never tried here: it would
        /// generate a real game.
        /// </summary>
        private void TestLaunchGuards()
        {
            LogMessage mark = Log.Messages.LastOrDefault();
            bool missing = ShowcaseLauncher.Launch(null, false, out string missingWhy);
            bool inGame = ShowcaseLauncher.Launch(
                ShowcaseLibrary.Named(ShippedShowcase), false, out string inGameWhy);
            bool queuedInGame = LongEventHandler.AnyEventNowOrWaiting;

            ShowcaseDef fatal = ShowcaseLibrary.Named(FatalShowcase);
            string expectedFatal = ShowcaseTexts.WhyNot(
                ShowcaseLibrary.Evaluate(fatal, force: false));
            bool fatalStarted;
            string fatalWhy;
            bool queuedFatal;
            ProgramState state = Current.ProgramState;
            try
            {
                Current.ProgramState = ProgramState.Entry;
                fatalStarted = ShowcaseLauncher.Launch(fatal, false, out fatalWhy);
                queuedFatal = LongEventHandler.AnyEventNowOrWaiting;
            }
            finally
            {
                Current.ProgramState = state;
            }

            List<string> cues = ShowcaseCuesSince(mark);
            Check("the launcher refuses a missing showcase, and any showcase "
                    + "from inside a game, saying why",
                !missing && missingWhy == "BT_Showcases_RefusedMissing".Translate().Resolve()
                    && !inGame && inGameWhy == "BT_Showcases_RefusedInGame".Translate().Resolve()
                    && !queuedInGame,
                $"missing: started {missing}, '{missingWhy}'; in game: started {inGame}, "
                    + $"'{inGameWhy}'; long event queued {queuedInGame}");
            Check("from the main menu, the launcher refuses a showcase flagged "
                    + "fatal unless forced, queueing nothing",
                !fatalStarted && fatalWhy == expectedFatal && !queuedFatal
                    && !cues.Any(c => c.StartsWith("LAUNCH", StringComparison.Ordinal)),
                $"started {fatalStarted}; reason '{fatalWhy}' (expected '{expectedFatal}'); "
                    + $"long event queued {queuedFatal}; cues {string.Join(" | ", cues)}");
        }

        /// <summary>
        /// A launch request reaches only the game generated for it: one
        /// planted for another game is dropped, with a warning, and never
        /// handed to this one, so a failed generation can never turn the
        /// player's next real colony into a showcase; one planted for this
        /// game is handed over exactly once.
        /// </summary>
        private void TestRequestBinding()
        {
            ShowcaseDef def = ShowcaseLibrary.Named(DirectorShowcase);
            LogMessage mark = Log.Messages.LastOrDefault();
            var other = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
            LaunchRequest stale = null, again = null, mine = null, twice = null;
            bool pendingAfterStale = true;
            try
            {
                ShowcaseLauncher.PlantForTest(new LaunchRequest(def, other, false));
                stale = ShowcaseLauncher.TakeRequestFor(Current.Game);
                pendingAfterStale = ShowcaseLauncher.HasPendingForTest;
                again = ShowcaseLauncher.TakeRequestFor(Current.Game);

                var planted = new LaunchRequest(def, Current.Game, false);
                ShowcaseLauncher.PlantForTest(planted);
                mine = ShowcaseLauncher.TakeRequestFor(Current.Game);
                twice = ShowcaseLauncher.TakeRequestFor(Current.Game);
                mine = ReferenceEquals(mine, planted) ? mine : null;
            }
            finally
            {
                ShowcaseLauncher.PlantForTest(null);
            }

            List<LogMessage> dropped = MessagesSince(mark)
                .Where(m => m.type == LogMessageType.Warning
                    && m.text.Contains("dropped a stale request"))
                .ToList();
            ExpectLogs(dropped);
            Check("a launch request reaches only the game generated for it, "
                    + "and only once",
                stale == null && !pendingAfterStale && again == null && dropped.Count == 1
                    && mine != null && twice == null,
                $"stale handed over {stale != null}; still pending {pendingAfterStale}; "
                    + $"warnings {dropped.Count}; own request handed over {mine != null}, "
                    + $"again {twice != null}");
        }

        /// <summary>
        /// A def's configuration check reports a "stopped" narration line
        /// with a delay, which Stop would drop unsaid. The directed test
        /// showcase declares one on purpose ("opening", 10 ticks); its
        /// def is made in code, so the game's own start-up check never
        /// sees it.
        /// </summary>
        private void TestStoppedLineConfigError()
        {
            List<string> errors = ShowcaseLibrary.Named(DirectorShowcase)?.ConfigErrors()
                .ToList() ?? new List<string>();
            List<string> stopped = errors
                .Where(e => e.Contains("at 'stopped' has delayTicks"))
                .ToList();
            Check("a def's configuration check reports a delayed 'stopped' "
                    + "narration line, which would never be said",
                stopped.Count == 1 && stopped[0].Contains("'opening'")
                    && stopped[0].Contains("delayTicks 10"),
                $"{errors.Count} configuration errors: {string.Join(" | ", errors)}");
        }

        /// <summary>
        /// Outside dev mode, as a player sees it, the details page of a
        /// showcase flagged fatal still refuses Start and offers Force
        /// start (docs/SHOWCASE-API.md, "Other Mods"). Dev mode is switched
        /// off quietly, in Prefs' data, for the one check.
        /// </summary>
        private void TestForceOutsideDevMode()
        {
            const string Name = "outside dev mode, the details page refuses a "
                + "fatal flag and still offers Force start";
            bool wasDev = Prefs.DevMode;
            try
            {
                if (!TrySetDevModeQuietly(false))
                {
                    Check(Name, false, "could not switch dev mode off (Prefs.data.devMode)");
                    return;
                }

                var details = new Dialog_ShowcaseDetails(
                    ShowcaseLibrary.Named(FatalShowcase), null);
                Check(Name, !Prefs.DevMode && !details.StartEnabled && details.ForceOffered,
                    $"dev mode {Prefs.DevMode}; start {details.StartEnabled}; force "
                        + details.ForceOffered);
            }
            finally
            {
                TrySetDevModeQuietly(wasDev);
            }
        }

        /// <summary>
        /// Sets Prefs' dev-mode flag directly in its data, without the
        /// setter's side effects: Prefs.Apply re-applies the resolution,
        /// and switching dev mode off also clears god mode, which the god
        /// mode checks must see the showcase leave alone.
        /// </summary>
        /// <param name="on">The dev mode wanted.</param>
        /// <returns>True if dev mode is now as wanted.</returns>
        private static bool TrySetDevModeQuietly(bool on)
        {
            FieldInfo dataField = typeof(Prefs).GetField(
                "data", BindingFlags.Static | BindingFlags.NonPublic);
            object data = dataField?.GetValue(null);
            FieldInfo devMode = data?.GetType().GetField(
                "devMode", BindingFlags.Instance | BindingFlags.Public);
            if (devMode == null || devMode.FieldType != typeof(bool))
            {
                return false;
            }

            devMode.SetValue(data, on);
            return Prefs.DevMode == on;
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
        /// skipped, and it selects the subject and plays the beat "poke" on
        /// its second tick; "timeout" never finishes and times out after
        /// 40 ticks; "unavailable" declines to run (gap G1), so it is
        /// dropped and the caption counts four scenes, not five; "closing"
        /// completes after 20 ticks and says its last line 30 ticks later.
        /// Narration covers each outcome, including lines that must never
        /// be said.
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
                beatAtTick = 2,
                beatMoment = "poke",
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
            var unavailable = new SmokeShowcaseScene
            {
                key = "unavailable",
                title = "Unavailable scene",
                minTicks = 0,
                timeoutTicks = 600,
                unavailableReason = "smoke",
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
            def.scenes.AddRange(
                new[] { opening, waiting, timeout, unavailable, closing });
            def.narration.AddRange(new[]
            {
                Line("opening", "start", 0, "Smoke narration: opening."),
                Line("opening", "completed", 0, "Smoke narration: opening completed."),
                Line("opening", "stopped", 0, "Smoke narration: opening stopped."),
                Line("opening", "stopped", 10, "Smoke narration: queued at the stop, never said."),
                Line("waiting", "start", 0, "Smoke narration: waiting."),
                Line("waiting", "poke", 0, "Smoke narration: waiting poked."),
                Line("waiting", "skipped", 0, "Smoke narration: waiting skipped."),
                Line("waiting", "completed", 0, "Smoke narration: waiting never completes."),
                Line("timeout", "timedOut", 0, "Smoke narration: timeout timed out."),
                Line("unavailable", "start", 0, "Smoke narration: never said."),
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
            List<Message> liveBefore = LiveMessages();
            int handedBefore = showcaseSession.Narration.LinesSaid;
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
            Check("the caption reads 2 of 4: Waiting scene (a declined "
                    + "scene is not counted)",
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
                "GAME " + DirectorShowcase, "START " + DirectorShowcase,
                "SKIPPED unavailable: smoke", "READY",
                "SCENE opening", "END opening completed",
                "SCENE waiting", "BEAT waiting poke",
                "SKIP " + DirectorShowcase, "END waiting skipped",
                "SCENE timeout", "END timeout timedOut",
                "SCENE closing", "END closing completed",
                "FINISHED " + DirectorShowcase,
            };
            Check("the director runs its scenes to FINISHED, logging each cue "
                    + "in order: a declined scene is SKIPPED before READY, "
                    + "and a beat logs BEAT",
                cues.SequenceEqual(expectedCues), string.Join(" | ", cues));

            List<NarrationRecord> said = director.NarrationLog.ToList();
            var expectedLines = new[]
            {
                "Smoke narration: opening.",
                "Smoke narration: opening completed.",
                "Smoke narration: waiting.",
                "Smoke narration: waiting poked.",
                "Smoke narration: waiting skipped.",
                "Smoke narration: timeout timed out.",
                "Smoke narration: closing, said late.",
            };
            int lastLine = said.Count > 0 ? said[said.Count - 1].Tick : -1;
            Check("narration says each scene's lines for how it ended and "
                    + "for its beat, then the closing message",
                said.Select(r => r.Text).SequenceEqual(expectedLines)
                    && showcaseSession.FarewellTick > lastLine,
                $"said: {string.Join(" | ", said.Select(r => $"{r.Tick}: {r.Text}"))}; "
                    + $"closing message at {showcaseSession.FarewellTick}");
            CheckNarrationNotInMessages(
                liveBefore, handedBefore, said.Select(r => r.Text).ToList());

            List<string> warnings = ShowcaseWarningsSince(mark);
            Check("the directed run logs no Showcase API warning (camera moves, "
                    + "selection and the narration overlay all worked)",
                warnings.Count == 0, string.Join(" | ", warnings));
            showcaseSession.ReleaseForTest();
        }

        /// <summary>
        /// Narration and the closing message go to the overlay, never to
        /// vanilla's messages: the run added no live message and archived
        /// none with a narration line's words, and the overlay was handed
        /// every line said and then the closing message.
        /// </summary>
        /// <param name="liveBefore">The live messages before the run.</param>
        /// <param name="handedBefore">Lines handed to the overlay before it.</param>
        /// <param name="said">The lines the run said.</param>
        private void CheckNarrationNotInMessages(
            List<Message> liveBefore, int handedBefore, List<string> said)
        {
            string farewell = "BT_Showcases_Finished".Translate();
            var words = new HashSet<string>(
                said.Append(farewell), StringComparer.OrdinalIgnoreCase);
            List<string> added = LiveMessages()
                .Where(m => !liveBefore.Contains(m))
                .Select(m => m.text)
                .ToList();
            List<string> archived = (Find.Archive?.ArchivablesListForReading
                    ?? new List<IArchivable>())
                .OfType<Message>()
                .Where(m => m.text != null && words.Contains(m.text))
                .Select(m => m.text)
                .ToList();
            NarrationOverlay overlay = showcaseSession.Narration;
            int handed = overlay.LinesSaid - handedBefore;
            Check("narration and the closing message go to the overlay, never "
                    + "to vanilla's messages or the archive",
                LiveMessagesField != null && added.Count == 0
                    && archived.Count == 0 && handed == said.Count + 1
                    && overlay.LastLine == farewell,
                (LiveMessagesField == null
                    ? "Messages.liveMessages not found; "
                    : $"live messages added: {string.Join(" | ", added)}; ")
                    + $"archived: {string.Join(" | ", archived)}; overlay "
                    + $"handed {handed} of {said.Count + 1} lines, the last "
                    + $"'{overlay.LastLine}'");
        }

        /// <summary>
        /// Vanilla's live messages now (a copy), or an empty list if they
        /// cannot be read.
        /// </summary>
        /// <returns>The messages.</returns>
        private static List<Message> LiveMessages() =>
            (LiveMessagesField?.GetValue(null) as List<Message>)?.ToList()
                ?? new List<Message>();

        /// <summary>
        /// A directed showcase whose scenes throw keeps going: a scene whose
        /// set-up throws never begins (SKIPPED, no SCENE or END, no
        /// narration); one whose Tick or IsDone throws ends Failed on that
        /// tick, saying its "failed" lines and never its "completed" ones;
        /// one whose End throws still ends as it ended; the scene after
        /// them runs, and the tour reaches FINISHED. Each fault logs exactly
        /// one error; those are expected, and left out of the final log
        /// check.
        /// </summary>
        /// <param name="map">The map.</param>
        private void TestFaultyScenes(Map map)
        {
            ShowcaseDef def = MakeFaultShowcase(
                ShowcaseLibrary.Named(ShippedShowcase)?.modContentPack);
            LogMessage mark = Log.Messages.LastOrDefault();
            showcaseSession.AdoptForTest(def, map);
            var director = showcaseSession.Worker as ShowcaseDirector;
            TickUntil(() => !showcaseSession.Running && showcaseSession.FarewellTick >= 0, 200);

            List<string> cues = ShowcaseCuesSince(mark);
            var expectedCues = new[]
            {
                "GAME " + FaultShowcase, "START " + FaultShowcase, "READY",
                "SKIPPED setupFault: set-up failed",
                "SCENE tickFault", "END tickFault failed",
                "SCENE doneFault", "END doneFault failed",
                "SCENE endFault", "END endFault completed",
                "SCENE after", "END after completed",
                "FINISHED " + FaultShowcase,
            };
            Check("a scene that throws is logged and the tour moves on: set-up "
                    + "faults never begin, Tick and IsDone faults end failed",
                director != null && cues.SequenceEqual(expectedCues)
                    && director.Endings.SequenceEqual(new[]
                    {
                        SceneEnd.Failed, SceneEnd.Failed, SceneEnd.Failed,
                        SceneEnd.Completed, SceneEnd.Completed,
                    }),
                $"cues {string.Join(" | ", cues)}; endings "
                    + (director == null ? "no director" : Endings(director)));

            List<string> said = director?.NarrationLog.Select(r => r.Text).ToList()
                ?? new List<string>();
            var expectedLines = new[]
            {
                "Smoke narration: tick fault begins.",
                "Smoke narration: tick fault failed.",
                "Smoke narration: done fault failed.",
                "Smoke narration: end fault completed.",
                "Smoke narration: after.",
            };
            Check("a failed scene says its failed lines, never its completed "
                    + "ones, and a scene that never began says nothing",
                said.SequenceEqual(expectedLines), string.Join(" | ", said));

            List<LogMessage> errors = MessagesSince(mark)
                .Where(m => m.type == LogMessageType.Error)
                .ToList();
            var faults = new[] { "setupFault", "tickFault", "doneFault", "endFault" };
            bool oneEach = errors.Count == faults.Length
                && errors.All(e => e.text.Contains(SmokeShowcaseScene.FaultMarker))
                && faults.All(k => errors.Count(e => e.text.Contains($"'{k}'")) == 1);
            Check("each scene fault logs exactly one error",
                oneEach,
                $"{errors.Count} errors: "
                    + string.Join(" | ", errors.Select(e => e.text.Split('\n')[0])));
            ExpectLogs(errors.Where(e => e.text.Contains(SmokeShowcaseScene.FaultMarker)));
            showcaseSession.ReleaseForTest();
        }

        /// <summary>
        /// Makes the fault test showcase: "setupFault" throws in Setup,
        /// "tickFault" in Tick, "doneFault" in IsDone, "endFault" in End
        /// (after completing), and "after" is a plain scene that must
        /// still run. Its narration has lines that must never be said.
        /// </summary>
        /// <param name="pack">The mod it is listed under; may be null.</param>
        /// <returns>The def, not registered.</returns>
        private static ShowcaseDef MakeFaultShowcase(ModContentPack pack)
        {
            ShowcaseDef def = MakeTestShowcase(FaultShowcase, "smoke faults", pack, 910);
            def.workerClass = typeof(ShowcaseDirector);
            def.scenes.AddRange(new ShowcaseScene[]
            {
                new SmokeShowcaseScene
                {
                    key = "setupFault", minTicks = 0, timeoutTicks = 600,
                    throwInSetup = true, selectSubject = false,
                },
                new SmokeShowcaseScene
                {
                    key = "tickFault", minTicks = 0, timeoutTicks = 600,
                    neverDone = true, throwInTick = true, selectSubject = false,
                },
                new SmokeShowcaseScene
                {
                    key = "doneFault", minTicks = 0, timeoutTicks = 600,
                    throwInIsDone = true, selectSubject = false,
                },
                new SmokeShowcaseScene
                {
                    key = "endFault", minTicks = 5, timeoutTicks = 600,
                    throwInEnd = true, selectSubject = false,
                },
                new ShowcaseScene
                {
                    key = "after", minTicks = 5, timeoutTicks = 600, selectSubject = false,
                },
            });
            def.narration.AddRange(new[]
            {
                Line("setupFault", "start", 0, "Smoke narration: never said (set-up start)."),
                Line("setupFault", "failed", 0, "Smoke narration: never said (set-up failed)."),
                Line("setupFault", "skipped", 0, "Smoke narration: never said (set-up skip)."),
                Line("tickFault", "start", 0, "Smoke narration: tick fault begins."),
                Line("tickFault", "failed", 0, "Smoke narration: tick fault failed."),
                Line("tickFault", "completed", 0, "Smoke narration: never said (tick done)."),
                Line("doneFault", "failed", 0, "Smoke narration: done fault failed."),
                Line("doneFault", "completed", 0, "Smoke narration: never said (done)."),
                Line("endFault", "completed", 0, "Smoke narration: end fault completed."),
                Line("after", "start", 0, "Smoke narration: after."),
            });
            return def;
        }

        /// <summary>
        /// God mode, which a showcase borrows for its DEV buttons, is lent
        /// only in dev mode and given back on every way out: Stop, the
        /// showcase finishing, the main menu, a loaded game and a new game.
        /// A developer's own god mode stays on; a player outside dev mode
        /// never gets it at all.
        /// </summary>
        /// <param name="map">The map.</param>
        private void TestDevButtonLoan(Map map)
        {
            bool godBefore = DebugSettings.godMode;
            bool devBefore = Prefs.DevMode;
            ShowcaseDef plain = MakeTestShowcase(DevButtonsShowcase, "smoke dev buttons", null, 920);
            plain.workerClass = typeof(SmokeDevButtonsWorker);
            ShowcaseDef finishing = MakeTestShowcase(
                DevButtonsShowcase + "_Finishing", "smoke dev buttons finishing", null, 921);
            finishing.workerClass = typeof(SmokeDevButtonsFinishingWorker);
            try
            {
                DebugSettings.godMode = false;
                showcaseSession.AdoptForTest(plain, map);
                bool lent = DebugSettings.godMode && ShowcaseGodMode.Lent;
                showcaseSession.StopShowcase(byViewer: false);
                bool back = !DebugSettings.godMode && !ShowcaseGodMode.Lent;
                showcaseSession.ReleaseForTest();
                Check("in dev mode a showcase lends god mode for its DEV buttons, "
                        + "and Stop gives it back",
                    devBefore && lent && back,
                    $"dev mode {devBefore}; lent {lent}; given back {back}");

                DebugSettings.godMode = true;
                showcaseSession.AdoptForTest(plain, map);
                showcaseSession.StopShowcase(byViewer: false);
                bool kept = DebugSettings.godMode;
                showcaseSession.ReleaseForTest();
                Check("a developer's own god mode is still on after a showcase",
                    kept, $"god mode after Stop {kept}");

                DebugSettings.godMode = false;
                bool quiet = TrySetDevModeQuietly(false);
                showcaseSession.AdoptForTest(plain, map);
                bool raised = DebugSettings.godMode || ShowcaseGodMode.Lent;
                showcaseSession.ReleaseForTest();
                TrySetDevModeQuietly(devBefore);
                Check("outside dev mode a showcase never turns god mode on",
                    quiet && !raised,
                    $"dev mode switched off {quiet}; god mode raised {raised}");

                var exits = new (string Name, Action Leave)[]
                {
                    ("main menu", ShowcaseLauncher.OnMainMenuShown),
                    ("loaded game", () => showcaseSession.LoadedGame()),
                    ("new game", () => showcaseSession.StartedNewGame()),
                };
                var results = new List<string>();
                bool allBack = true;
                foreach ((string name, Action leave) in exits)
                {
                    DebugSettings.godMode = false;
                    showcaseSession.AdoptForTest(plain, map);
                    bool on = DebugSettings.godMode;
                    leave();
                    bool off = !DebugSettings.godMode && !ShowcaseGodMode.Lent;
                    allBack &= on && off;
                    results.Add($"{name}: lent {on}, given back {off}");
                    showcaseSession.ReleaseForTest();
                }

                Check("leaving a showcase game (main menu, loaded game, new game) "
                        + "gives god mode back",
                    allBack, string.Join("; ", results));

                DebugSettings.godMode = false;
                showcaseSession.AdoptForTest(finishing, map);
                bool onBefore = DebugSettings.godMode;
                TickUntil(() => !showcaseSession.Running, 10);
                bool offAfter = !DebugSettings.godMode && !ShowcaseGodMode.Lent;
                showcaseSession.ReleaseForTest();
                Check("a showcase that finishes gives god mode back",
                    onBefore && offAfter,
                    $"lent {onBefore}; given back at FINISHED {offAfter}");
            }
            finally
            {
                showcaseSession.ReleaseForTest();
                TrySetDevModeQuietly(devBefore);
                DebugSettings.godMode = godBefore;
            }
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
                ScribedNothing(saved) && !saved.Contains(DirectorShowcase),
                $"wrote: {saved?.Replace('\n', ' ')}");

            CheckAutosaveSuppressed("a showcase game never reaches its autosave");
        }

        /// <summary>
        /// Gets whether Scribe's debug output for an object is an empty
        /// "saveable" element: attributes such as Class allowed, but no
        /// child elements or text, so the object's ExposeData wrote
        /// nothing.
        /// </summary>
        /// <param name="saved">The output of ScribeSaver.DebugOutputFor.</param>
        /// <returns>True when nothing was written.</returns>
        private static bool ScribedNothing(string saved)
        {
            if (saved.NullOrEmpty())
            {
                return false;
            }

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(saved);
                XmlElement root = doc.DocumentElement;
                return root != null && root.Name == "saveable" && !root.HasChildNodes;
            }
            catch (XmlException)
            {
                return false;
            }
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
            new FrameStep("narration lines", false, SayOverlayLines),
            new FrameStep("narration replaced", false, CheckOverlayReplaced),
            new FrameStep("narration fits at 4K", true, CheckNarrationFits),
            new FrameStep("glide then cut", false, StartGlideThenCut),
            new FrameStep("cut holds", false, CheckCutHolds),
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
        /// Starts the overlay test: the second run, still playing (paused),
        /// says a long line and then a short one through the worker.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool SayOverlayLines()
        {
            overlaySaidAt = Time.realtimeSinceStartup;
            overlayFramesBefore = showcaseSession.NarrationFrames;
            stopRun?.Say("smoke", OverlayLongLine, null);
            stopRun?.Say("smoke", OverlayNextLine, null);
            return true;
        }

        /// <summary>
        /// Waits, in real frames, for the overlay to show the last line
        /// said: each line replaces the one before after its base time
        /// (2.5 s), not its full reading time, and is painted. Gives up
        /// after <see cref="OverlayReplaceSeconds"/>.
        /// </summary>
        /// <returns>True once the line shows or the time is up.</returns>
        private bool CheckOverlayReplaced()
        {
            NarrationOverlay overlay = showcaseSession.Narration;
            string last = stopRun?.NarrationLog.LastOrDefault()?.Text;
            float waited = Time.realtimeSinceStartup - overlaySaidAt;
            bool shows = last != null && overlay.CurrentLine == last
                && overlay.Alpha(NarrationOverlayDrawer.Now) > 0.5;
            if (!shows && waited < OverlayReplaceSeconds)
            {
                return false;
            }

            int painted = showcaseSession.NarrationFrames - overlayFramesBefore;
            Log.Message($"[BT-SMOKE] NOTE narration overlay: font size "
                + $"{NarrationOverlayDrawer.LastFontSize} (Medium "
                + $"{NarrationOverlayDrawer.MediumSize}); the last line showed "
                + $"{waited:0.0} s after it was said; {painted} frames painted");
            Check("the narration overlay's current line is the last line said, "
                    + "replacing the one before after its base time, painted in "
                    + "real frames",
                shows && last == OverlayNextLine && painted > 0
                    && !showcaseSession.NarrationFailed,
                $"current '{overlay.CurrentLine}', last said '{last}', after "
                    + $"{waited:0.0} s; waiting "
                    + $"{string.Join(" | ", overlay.WaitingLines)}; painted "
                    + $"{painted} frames; failed {showcaseSession.NarrationFailed}");
            return true;
        }

        /// <summary>
        /// The screen the narration overlay is checked for, in UI units: a
        /// 3840 by 2160 screen at 2x UI scale (the operator's).
        /// </summary>
        private const float FourKAt2xWidth = 3840f / 2f;

        /// <summary>
        /// Every narration line of every showcase, measured inside a repaint
        /// as the overlay draws it on a 3840-pixel screen at 2x UI scale:
        /// each must wrap to at most two lines at the Medium size or more,
        /// and the font must have a glyph for each of its characters (the
        /// em dash among them). Logs the stampede's volley line's size.
        /// </summary>
        /// <returns>True: the check runs in one frame.</returns>
        private bool CheckNarrationFits()
        {
            var problems = new List<string>();
            int lines = 0;
            int smallest = int.MaxValue;
            foreach (ShowcaseDef def in DefDatabase<ShowcaseDef>.AllDefs)
            {
                foreach (ShowcaseNarrationLine line in def.narration
                    ?? new List<ShowcaseNarrationLine>())
                {
                    if (line?.text == null)
                    {
                        continue;
                    }

                    (int size, int wraps, string missing) =
                        NarrationOverlayDrawer.Probe(line.text, FourKAt2xWidth);
                    lines++;
                    smallest = Math.Min(smallest, size);
                    if (wraps > NarrationOverlayLayout.MaxLines
                        || size < NarrationOverlayDrawer.MediumSize
                        || missing.Length > 0)
                    {
                        problems.Add($"{def.defName} {line.scene}/{line.when}: "
                            + $"{wraps} lines at size {size}"
                            + (missing.Length > 0
                                ? $", no glyph for [{missing}]"
                                : string.Empty));
                    }

                    if (line.scene == TourScript.Stampede && line.when == "volley")
                    {
                        Log.Message("[BT-SMOKE] NOTE narration at 3840x2160, 2x: "
                            + $"the stampede's volley line ({line.text.Length} "
                            + $"characters) fits {wraps} lines at size {size} "
                            + $"(Medium {NarrationOverlayDrawer.MediumSize}), "
                            + $"glyphs missing [{missing}]");
                    }
                }
            }

            Check("every narration line fits the overlay in two lines at "
                    + "3840x2160 and 2x UI scale, with a glyph for every character",
                lines > 0 && problems.Count == 0,
                $"{lines} lines measured, smallest size "
                    + $"{(lines > 0 ? smallest : 0)}; "
                    + string.Join("; ", problems));
            return true;
        }

        /// <summary>
        /// Starts the camera test: a director scene whose first two shots
        /// fall on the same tick, a long glide to one place and then a cut
        /// to another. The cut must win once real frames run, which needs
        /// it to cancel the glide CameraDriver would otherwise keep
        /// re-applying every frame.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool StartGlideThenCut()
        {
            Map map = Find.CurrentMap;
            ShowcaseDef def = MakeTestShowcase(CameraShowcase, "smoke camera", null, 930);
            def.workerClass = typeof(ShowcaseDirector);
            var scene = new ShowcaseScene
            {
                key = "camera",
                minTicks = 0,
                timeoutTicks = 600,
                selectSubject = false,
            };
            scene.shots.Add(new ShowcaseShot
            {
                atTick = 0, fromMapCenter = true, offset = new IntVec2(40, 40),
                zoom = GlideZoom, pan = true, panSeconds = GlideSeconds,
            });
            scene.shots.Add(new ShowcaseShot
            {
                atTick = 0, fromMapCenter = true, offset = new IntVec2(-30, -30),
                zoom = CutShotZoom, pan = false,
            });
            def.scenes.Add(scene);

            CameraDriver camera = Find.CameraDriver;
            camera.JumpToCurrentMapLoc(map.Center);
            camera.SetRootSize(45f);
            cutCell = (map.Center + new IntVec3(-30, 0, -30)).ClampInsideMap(map);
            FloatRange range = camera.config.sizeRange;
            cutZoom = Mathf.Clamp(CutShotZoom, range.min, range.max);
            cameraRun = def.MakeWorker() as ShowcaseDirector;
            cameraRun?.Begin(map);
            cameraRun?.DoTick();
            cutFramedAt = Time.realtimeSinceStartup;
            cutFrames = 0;
            return true;
        }

        /// <summary>
        /// After a few real frames, the camera sits where the cut put it, at
        /// the cut's zoom, not drifting toward the glide's target. Then the
        /// camera test's director is stopped.
        /// </summary>
        /// <returns>True once judged.</returns>
        private bool CheckCutHolds()
        {
            if (++cutFrames < 5 || Time.realtimeSinceStartup - cutFramedAt < CutHoldSeconds)
            {
                return false;
            }

            CameraDriver camera = Find.CameraDriver;
            IntVec3 at = camera.MapPosition;
            float size = camera.RootSize;
            Check("a cut right after a glide holds once frames run: the glide "
                    + "is cancelled",
                cameraRun != null && Mathf.Abs(size - cutZoom) < 0.05f
                    && at.InHorDistOf(cutCell, 2f),
                $"camera at {at} size {size:F2} after {cutFrames} frames; cut to "
                    + $"{cutCell} size {cutZoom:F2}; the glide went to size {GlideZoom}");
            cameraRun?.Stop();
            cameraRun = null;
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
            bool keyed = "BT_Showcases_MenuButton".CanTranslate()
                && "BT_Showcases_ModsTitle".CanTranslate();
            Check("the main menu gets exactly one Showcases button, with its text",
                mainCalls == 2 && mainDrawn == 1 && keyed
                    && label == "BT_Showcases_MenuButton".Translate().Resolve(),
                $"postfix ran {mainCalls} times; buttons drawn {mainDrawn}; "
                    + $"label '{label}'; keyed text loaded {keyed}");

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
        /// Force start (TestForceOutsideDevMode checks the same outside dev
        /// mode). Then goes Back.
        /// </summary>
        /// <returns>True: the step is complete.</returns>
        private bool CheckFatalDetails()
        {
            var details = Find.WindowStack.WindowOfType<Dialog_ShowcaseDetails>();
            List<string> facts = details?.FactLines.ToList() ?? new List<string>();
            Check("the details page refuses a fatal flag with the reason, and "
                    + "offers Force start",
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
        /// said. The overlay is cleared at once, so that line shows alone,
        /// with nothing fading out under it. A stopped showcase game still
        /// never autosaves; released
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

            NarrationOverlay overlay = showcaseSession.Narration;
            Check("Stop clears the narration overlay at once: the stopped "
                    + "scene's own line shows alone",
                overlay.CurrentLine == "Smoke narration: opening stopped."
                    && overlay.OutgoingLine == null
                    && overlay.WaitingLines.Count == 0,
                $"current '{overlay.CurrentLine}', outgoing "
                    + $"'{overlay.OutgoingLine}', waiting "
                    + string.Join(" | ", overlay.WaitingLines));

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
                cameraRun?.Stop();
                cameraRun = null;
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
        /// Cues are plain messages; the API's errors and warnings share the
        /// prefix but are not cues.
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
                .Where(m => m.type == LogMessageType.Message && m.text != null
                    && m.text.StartsWith(prefix, StringComparison.Ordinal))
                .Select(m => m.text.Substring(prefix.Length))
                .ToList();
        }

        /// <summary>The log messages since a message.</summary>
        /// <param name="mark">The last message before; null for all.</param>
        /// <returns>The messages, in order.</returns>
        private static List<LogMessage> MessagesSince(LogMessage mark) =>
            (mark == null
                ? Log.Messages
                : Log.Messages.SkipWhile(m => m != mark).Skip(1))
            .ToList();

        /// <summary>
        /// The Showcase API's warnings since a message, first lines only.
        /// </summary>
        /// <param name="mark">The last message before; null for all.</param>
        /// <returns>The warnings, in order.</returns>
        private static List<string> ShowcaseWarningsSince(LogMessage mark) =>
            MessagesSince(mark)
                .Where(m => m.type == LogMessageType.Warning && m.text != null
                    && m.text.StartsWith(ShowcaseLog.Prefix, StringComparison.Ordinal))
                .Select(m => m.text.Split('\n')[0])
                .ToList();

        /// <summary>
        /// Records messages a check provoked on purpose: the final log
        /// check leaves them out, the launch script is told their first
        /// lines (an EXPECTED line each) so it leaves them out of Player.log
        /// too, and the debug log window that dev mode opens on an error is
        /// kept shut, so it covers no later check.
        /// </summary>
        /// <param name="messages">The messages.</param>
        private void ExpectLogs(IEnumerable<LogMessage> messages)
        {
            foreach (LogMessage m in messages)
            {
                if (expectedLogs.Add(m))
                {
                    Log.Message(
                        $"[BT-SMOKE] EXPECTED {m.text.Split('\n')[0].TrimEnd('\r')}");
                }
            }

            EditWindow_Log.wantsToOpen = false;
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
