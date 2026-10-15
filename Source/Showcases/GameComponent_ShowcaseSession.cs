// Showcase API - the running session.
// RimWorld creates one of these for every game. It does nothing unless
// the game was generated for a showcase (ShowcaseLauncher), or started
// with -quicktest and -showcase=. Then, once the new map has settled, it
// makes the showcase's worker, sets it up, ticks it, draws the caption bar
// with Skip and Stop and the narration overlay (one line at a time, in the
// lower third, silent; NarrationOverlayDrawer), writes the [BT-SHOWCASE]
// log cues capture tooling waits on, and keeps the game from autosaving,
// so a showcase never overwrites the player's autosave slots. Nothing
// about the session is saved: a showcase game the player chooses to save
// loads as an ordinary game. God mode a showcase borrowed for its
// developer buttons (ShowcaseGodMode) is given back when the showcase
// stops or finishes, and again whenever a game that is not a showcase
// starts or loads. Random incidents are off while a showcase runs: the
// storyteller's parts are held from the session's start until the
// showcase finishes or stops, then given back, since the closing line
// hands the colony to the viewer. For repeatability (docs/SHOWCASE-API.md,
// "Seeds"), the session closes the map seed's window the launcher opened
// (ShowcaseMapSeed: the second Harmony patch pins Map.NextGenSeed only
// while it is open) as soon as any new game starts, and says how many of
// the map's seeds it pinned (MAPSEED), then logs the new map's
// fingerprint (MAPHASH), holds the game paused while the map settles, so
// no frame-timed ticks run before the worker, reseeds the global random
// state from the launch's seed, and logs the stage's fingerprint
// (STAGEHASH) before READY. The window is closed again, harmlessly, on
// every later way out (set-up, Stop, FINISHED, a loaded game). Two
// internal seams, AdoptForTest and ReleaseForTest, let the smoke runner
// (Source/SmokeTestShowcases.cs) run test showcases in its own game.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Runs the showcase a game was generated for, if any.
    /// </summary>
    public class GameComponent_ShowcaseSession : GameComponent
    {
        /// <summary>
        /// Frames to wait after the map appears before setting up, so map
        /// generation's own follow-up work has settled (as the smoke
        /// runner does).
        /// </summary>
        private const int SettleFrames = 120;

        /// <summary>
        /// Room added to the caption's measured width. Widgets.LabelEllipses
        /// clips any text wider than its box less 13 units
        /// (Text.ClampTextWithEllipsis, RimWorld 1.6.4871), so a caption
        /// laid out at exactly its measured width was cut to "Demo ...".
        /// </summary>
        private const float CaptionSlack = 16f;

        /// <summary>
        /// The autosaver's private tick counter (RimWorld 1.6.4871). The
        /// session resets it every tick, before the autosaver's own tick
        /// in TickManager.DoSingleTick, so it never reaches the shortest
        /// interval (0.05 days, 3,000 ticks). No Harmony needed.
        /// </summary>
        private static readonly FieldInfo AutosaveCounter =
            typeof(Autosaver).GetField(
                "ticksSinceSave", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Whether the missing autosave counter has been reported.</summary>
        private static bool autosaveWarned;

        /// <summary>The game this component belongs to.</summary>
        private readonly Game game;

        /// <summary>The showcase this game runs, or null.</summary>
        private ShowcaseDef def;

        /// <summary>Whether it was force-started.</summary>
        private bool forced;

        /// <summary>The worker, once made.</summary>
        private ShowcaseWorker worker;

        /// <summary>Frames counted while settling.</summary>
        private int framesWithMap;

        /// <summary>Whether the worker has been set up (or failed to be).</summary>
        private bool begun;

        /// <summary>Whether the end has been reported.</summary>
        private bool ended;

        /// <summary>
        /// Whether the closing message waits for the last narration lines.
        /// </summary>
        private bool farewellPending;

        /// <summary>Whether the caption bar failed to draw; it then stops.</summary>
        private bool guiFailed;

        /// <summary>How many frames the caption bar has painted.</summary>
        private int captionFrames;

        /// <summary>The tick the closing message was shown on, or -1.</summary>
        private int farewellTick = -1;

        /// <summary>
        /// What the narration overlay shows. It belongs to this game, so
        /// leaving the game takes it along.
        /// </summary>
        private readonly NarrationOverlay narration = new NarrationOverlay();

        /// <summary>Whether the overlay failed; it then stops.</summary>
        private bool narrationFailed;

        /// <summary>How many frames the overlay has drawn text in.</summary>
        private int narrationFrames;

        /// <summary>The seed the game was generated from, or null.</summary>
        private ResolvedSeed seed;

        /// <summary>The new map's fingerprint, as hex, or null.</summary>
        private string mapHash;

        /// <summary>
        /// How many of the map's generation seeds the map seed's window
        /// pinned, or -1 when it was not open for this game.
        /// </summary>
        private int mapSeedsPinned = -1;

        /// <summary>The stage's fingerprint at READY, as hex, or null.</summary>
        private string stageHash;

        /// <summary>
        /// Whether the global random state was reseeded from the seed as
        /// the worker was set up.
        /// </summary>
        private bool reseeded;

        /// <summary>
        /// The storyteller's parts while the showcase holds them, or null
        /// when it does not: random incidents are off while it does.
        /// </summary>
        private List<StorytellerComp> heldStorytellerComps;

        /// <summary>
        /// Whether the session paused the game while the map settles, and
        /// so must unpause it when it sets the worker up.
        /// </summary>
        private bool settleHeld;

        /// <summary>The game's speed before the settle hold.</summary>
        private TimeSpeed speedBeforeSettle = TimeSpeed.Normal;

        /// <summary>Created by RimWorld for every game.</summary>
        /// <param name="game">The game.</param>
        public GameComponent_ShowcaseSession(Game game)
        {
            this.game = game;
        }

        /// <summary>
        /// Gets the session of the current game, or null outside a game.
        /// </summary>
        public static GameComponent_ShowcaseSession Current =>
            Verse.Current.Game?.GetComponent<GameComponent_ShowcaseSession>();

        /// <summary>
        /// Gets a value indicating whether this game is a showcase game.
        /// </summary>
        public bool IsShowcaseGame => def != null;

        /// <summary>Gets the showcase this game runs, or null.</summary>
        public ShowcaseDef Def => def;

        /// <summary>Gets the running worker, or null.</summary>
        public ShowcaseWorker Worker => worker;

        /// <summary>
        /// Gets a value indicating whether the showcase is still playing
        /// out: set up, and neither finished nor stopped.
        /// </summary>
        public bool Running => worker != null && begun && !ended;

        /// <summary>
        /// Gets how many frames the caption bar has painted, for the smoke
        /// runner.
        /// </summary>
        public int CaptionBarFrames => captionFrames;

        /// <summary>
        /// Gets a value indicating whether the caption bar failed and was
        /// hidden.
        /// </summary>
        public bool CaptionBarFailed => guiFailed;

        /// <summary>
        /// Gets the tick the closing message was handed to the narration
        /// overlay on (it appears once the last line has been read), or -1
        /// if it has not been.
        /// </summary>
        public int FarewellTick => farewellTick;

        /// <summary>
        /// Gets how many frames the narration overlay has drawn text in,
        /// for the smoke runner.
        /// </summary>
        public int NarrationFrames => narrationFrames;

        /// <summary>
        /// Gets a value indicating whether the narration overlay failed
        /// and was hidden.
        /// </summary>
        public bool NarrationFailed => narrationFailed;

        /// <summary>
        /// Gets the narration overlay's state, for the smoke runner and
        /// capture tooling.
        /// </summary>
        internal NarrationOverlay Narration => narration;

        /// <summary>
        /// Gets the seed this game was generated from, or null for a game
        /// the smoke runner adopted.
        /// </summary>
        public ResolvedSeed Seed => seed;

        /// <summary>
        /// Gets the new map's fingerprint (the MAPHASH cue's), as hex, or
        /// null before the game started.
        /// </summary>
        public string MapHash => mapHash;

        /// <summary>
        /// Gets the stage's fingerprint at READY (the STAGEHASH cue's), as
        /// hex, or null before READY.
        /// </summary>
        public string StageHash => stageHash;

        /// <summary>
        /// Gets how many of the map's generation seeds were pinned while
        /// the map was generated (the MAPSEED cue's), or -1 when they were
        /// not: a game the smoke runner adopted, a quick-test map, or no
        /// patch.
        /// </summary>
        public int MapSeedsPinned => mapSeedsPinned;

        /// <summary>
        /// Gets a value indicating whether the global random state was
        /// reseeded from the seed as the worker was set up.
        /// </summary>
        public bool Reseeded => reseeded;

        /// <summary>
        /// Gets a value indicating whether the showcase holds the
        /// storyteller's parts, so no random incident comes.
        /// </summary>
        public bool StorytellerQuiet => heldStorytellerComps != null;

        /// <summary>
        /// Gets how many storyteller parts the showcase holds, or -1 when
        /// it holds none.
        /// </summary>
        public int StorytellerCompsHeld => heldStorytellerComps?.Count ?? -1;

        /// <summary>
        /// Closes the map seed's window first, for every new game, since
        /// its map is generated by now. Then takes the launcher's request
        /// for this game, if there is one, says how many of the map's
        /// seeds were pinned (MAPSEED), fingerprints the new map
        /// (MAPHASH), quiets the storyteller, and holds the game paused
        /// while the map settles. A game that is not a showcase gives back
        /// any god mode an earlier showcase still held, and warns if it
        /// found the window open (a showcase's generation that failed
        /// left it so; it was bound to that game, so it pinned nothing
        /// here). Runs at the end of Game.InitNewGame, inside the long
        /// event, before the game's first tick.
        /// </summary>
        public override void StartedNewGame()
        {
            int pinned = ShowcaseMapSeed.Disarm();
            LaunchRequest request = ShowcaseLauncher.TakeRequestFor(game)
                ?? ShowcaseLauncher.TakeQuickTestRequest();
            if (request == null)
            {
                if (pinned >= 0)
                {
                    Log.Warning($"{ShowcaseLog.Prefix} closed the showcase map "
                        + "seed's window, left open by a showcase that never "
                        + $"started, as another game started ({pinned} seeds "
                        + "pinned for it)");
                }

                ShowcaseGodMode.Restore();
                return;
            }

            def = request.Def;
            forced = request.Forced;
            seed = request.Seed;
            mapSeedsPinned = ShowcaseMapSeed.Installed ? pinned : -1;
            ShowcaseLog.Cue("GAME", def.defName);
            ShowcaseLog.Cue("MAPSEED", MapSeedCueDetail(pinned));
            mapHash = Fingerprint(game.CurrentMap ?? game.Maps.FirstOrDefault(), "MAPHASH");
            QuietStoryteller();
            HoldWhileSettling();
        }

        /// <summary>
        /// A loaded game is never a showcase game (nothing about a session
        /// is saved), so it gives back any god mode an earlier showcase
        /// still held: loading from a showcase game's own menu skips the
        /// main menu. A launch request left behind is dropped too; it can
        /// only be stale, since a showcase launches only from the main
        /// menu and loading a save is itself a long event.
        /// </summary>
        public override void LoadedGame()
        {
            ShowcaseLauncher.ReleaseMapSeed("a loaded game");
            ShowcaseLauncher.TakeRequestFor(game);
            ShowcaseGodMode.Restore();
        }

        /// <summary>
        /// Moves the narration overlay on, every frame, and sets the
        /// showcase up once the map has settled for a couple of seconds.
        /// </summary>
        public override void GameComponentUpdate()
        {
            if (def == null)
            {
                return;
            }

            UpdateNarration();
            if (begun
                || Verse.Current.ProgramState != ProgramState.Playing
                || Find.CurrentMap == null)
            {
                return;
            }

            if (++framesWithMap < SettleFrames)
            {
                return;
            }

            Begin(Find.CurrentMap);
        }

        /// <summary>
        /// Keeps the game from autosaving, ticks the worker, and reports
        /// when it has finished.
        /// </summary>
        public override void GameComponentTick()
        {
            if (def == null)
            {
                return;
            }

            SuppressAutosave();
            if (farewellPending)
            {
                SayFarewellWhenQuiet();
                return;
            }

            if (!Running)
            {
                return;
            }

            try
            {
                worker.DoTick();
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} {def.defName} failed while "
                    + $"running and was stopped: {e}");
                StopShowcase(byViewer: false);
                return;
            }

            bool finished;
            try
            {
                finished = worker.Finished;
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} {def.defName} could not say "
                    + $"whether it had finished, and was stopped: {e}");
                StopShowcase(byViewer: false);
                return;
            }

            if (finished)
            {
                ended = true;
                farewellPending = true;
                ShowcaseLauncher.ReleaseMapSeed("the showcase's end");
                ShowcaseGodMode.Restore();
                ShowcaseLog.Cue("FINISHED", def.defName);
                RestoreStoryteller();
                SayFarewellWhenQuiet();
            }
        }

        /// <summary>
        /// Draws the narration overlay whenever it has a line (after the
        /// showcase ends too, for its last lines and the closing message),
        /// and the caption bar while the showcase plays out: its caption
        /// and the Skip and Stop buttons, centred below the colonist bar.
        /// IMGUI calls this on every event, not once a frame: the buttons
        /// take part in every event, as vanilla's do, so their control ids
        /// stay in step, while the background is painted on repaint only.
        /// Everything is guarded; one failure is logged and the bar is
        /// hidden, and the text and colour state is always restored. The
        /// bar hides while the world map is open, tested as vanilla's map
        /// interface does (WorldRendererUtility.DrawingMap, RimWorld
        /// 1.6.4871): WorldRendered is also true on a map drawn over the
        /// world (Odyssey's space maps), where the bar must stay. Screenshot
        /// mode, which the gravship cutscene turns on, hides it as well.
        /// </summary>
        public override void GameComponentOnGUI()
        {
            if (def == null || Event.current == null
                || Find.CurrentMap == null || !WorldRendererUtility.DrawingMap
                || Find.UIRoot?.screenshotMode?.FiltersCurrentEvent == true)
            {
                return;
            }

            DrawNarration();
            if (guiFailed || !Running)
            {
                return;
            }

            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            Color color = GUI.color;
            try
            {
                DrawCaptionBar();
            }
            catch (Exception e)
            {
                guiFailed = true;
                Log.Error($"{ShowcaseLog.Prefix} the caption bar failed and "
                    + $"is hidden for this game: {e}");
            }
            finally
            {
                Text.Font = font;
                Text.Anchor = anchor;
                GUI.color = color;
            }
        }

        /// <summary>
        /// Skips the current part of the showcase, if the worker offers it.
        /// </summary>
        public void Skip()
        {
            if (!Running || !worker.CanSkip)
            {
                return;
            }

            ShowcaseLog.Cue("SKIP", def.defName);
            try
            {
                worker.Skip();
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} {def.defName} failed to "
                    + $"skip and was stopped: {e}");
                StopShowcase(byViewer: false);
            }
        }

        /// <summary>
        /// Stops the showcase for good. The game keeps running; the camera
        /// and the colony are the viewer's, and any god mode the showcase
        /// borrowed is given back. The narration overlay is cleared at
        /// once; a scene's own "stopped" line, said as the worker stops,
        /// then shows on it. The viewer's Stop is confirmed with a silent
        /// vanilla message: it answers a click, as a refusal does, and is
        /// not the narrator speaking.
        /// </summary>
        /// <param name="byViewer">Whether the viewer asked for it.</param>
        public void StopShowcase(bool byViewer)
        {
            if (worker == null || ended)
            {
                return;
            }

            ended = true;
            farewellPending = false;
            narration.Clear();
            try
            {
                worker.Stop();
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} {def.defName} failed while "
                    + $"stopping: {e}");
            }

            ShowcaseLauncher.ReleaseMapSeed("Stop");
            ShowcaseGodMode.Restore();
            ShowcaseLog.Cue("STOPPED", def.defName);
            RestoreStoryteller();
            if (byViewer)
            {
                Message("BT_Showcases_Stopped".Translate());
            }
        }

        /// <summary>
        /// Smoke-test seam: makes the running game a showcase game for
        /// <paramref name="showcase"/> and begins it at once on
        /// <paramref name="map"/>, as if the launcher had generated this
        /// game for it and the map had settled. An earlier run in this game
        /// is dropped without a cue. Players never reach this; showcases
        /// start in a fresh game from the menu or the command line.
        /// </summary>
        /// <param name="showcase">The showcase.</param>
        /// <param name="map">The map to run it on.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either argument is null.
        /// </exception>
        internal void AdoptForTest(ShowcaseDef showcase, Map map)
        {
            if (showcase == null)
            {
                throw new ArgumentNullException(nameof(showcase));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            ResetRun();
            def = showcase;
            ShowcaseLog.Cue("GAME", def.defName);
            Begin(map);
        }

        /// <summary>
        /// Smoke-test seam: makes the game an ordinary game again, as a
        /// showcase game the player saved loads: no showcase, no caption
        /// bar, and autosaving as usual. The worker is dropped without
        /// being stopped.
        /// </summary>
        internal void ReleaseForTest()
        {
            ResetRun();
        }

        /// <summary>
        /// Forgets the showcase and everything about its run, giving back
        /// any god mode it borrowed, the storyteller's parts it held, and
        /// the game's speed if it was holding the game while the map
        /// settled.
        /// </summary>
        private void ResetRun()
        {
            ShowcaseLauncher.ReleaseMapSeed("a smoke test's reset");
            ShowcaseGodMode.Restore();
            RestoreStoryteller();
            ReleaseSettleHold();
            seed = null;
            mapHash = null;
            mapSeedsPinned = -1;
            stageHash = null;
            reseeded = false;
            def = null;
            forced = false;
            worker = null;
            framesWithMap = 0;
            begun = false;
            ended = false;
            farewellPending = false;
            guiFailed = false;
            captionFrames = 0;
            farewellTick = -1;
            narration.Clear();
            narrationFailed = false;
            narrationFrames = 0;
        }

        /// <summary>
        /// Makes the worker and sets it up on the map, then logs the
        /// stage's fingerprint (STAGEHASH) and READY, the cue capture
        /// tooling waits on before maximizing the window. Before the set
        /// up it lets the game run again (it was held paused while the map
        /// settled), quiets the storyteller if a test adopted the game,
        /// and reseeds the global random state from the launch's seed, so
        /// the same seed stages the same things in the same places. A set
        /// up that throws is logged and ends the showcase; the game stays
        /// open, with its storyteller back.
        /// </summary>
        /// <param name="map">The showcase's map.</param>
        private void Begin(Map map)
        {
            begun = true;
            ShowcaseLauncher.ReleaseMapSeed("the showcase's set-up");
            ShowcaseLog.Cue("START", forced ? $"{def.defName} forced" : def.defName);
            ReleaseSettleHold();
            QuietStoryteller();
            reseeded = seed != null
                && ShowcaseRandom.Reseed(seed.Derive(ShowcaseSeed.Stage));
            try
            {
                worker = def.MakeWorker();
                worker.Overlay = narration;
                worker.Begin(map);
            }
            catch (Exception e)
            {
                ended = true;
                ShowcaseGodMode.Restore();
                ShowcaseLog.Cue("FAILED", def.defName);
                Log.Error($"{ShowcaseLog.Prefix} {def.defName} could not set "
                    + $"up: {e}");
                RestoreStoryteller();
                return;
            }

            stageHash = Fingerprint(map, "STAGEHASH");
            ShowcaseLog.Cue("READY", null);
        }

        /// <summary>
        /// The MAPSEED cue's detail: <c>pinned (n seeds)</c> when the map
        /// seed's window was open while the map was generated; otherwise
        /// <c>not pinned</c>, with why: no patch (Harmony missing, or the
        /// getter changed), or a quick-test map, which vanilla generated
        /// before the showcase adopted it.
        /// </summary>
        /// <param name="pinned">
        /// What closing the window returned: the seeds pinned, or -1 if it
        /// was not open.
        /// </param>
        /// <returns>The detail.</returns>
        private static string MapSeedCueDetail(int pinned)
        {
            if (!ShowcaseMapSeed.Installed)
            {
                return "not pinned (no patch)";
            }

            return pinned < 0
                ? "not pinned (quicktest map)"
                : pinned == 1 ? "pinned (1 seed)" : $"pinned ({pinned} seeds)";
        }

        /// <summary>
        /// Fingerprints a map and logs it as a record cue. Never throws: a
        /// failure is logged as a warning (the run is then not provable)
        /// and gives null.
        /// </summary>
        /// <param name="map">The map, or null.</param>
        /// <param name="cue">The cue word, MAPHASH or STAGEHASH.</param>
        /// <returns>The fingerprint as hex, or null.</returns>
        private static string Fingerprint(Map map, string cue)
        {
            if (map == null)
            {
                Log.Warning($"{ShowcaseLog.Prefix} no map to fingerprint for {cue}");
                return null;
            }

            try
            {
                MapFingerprint print = ShowcaseFingerprint.Of(map);
                ShowcaseLog.Cue(cue, ShowcaseFingerprint.CueDetail(print));
                return print.Hex;
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not fingerprint the map "
                    + $"for {cue}: {e}");
                return null;
            }
        }

        /// <summary>
        /// Takes the storyteller's parts away, and drops any incident it
        /// has queued, so no random incident (a raid, a manhunter pack)
        /// lands in the showcase; logs a STORYTELLER cue. Does nothing if
        /// they are already held. The parts belong to this game, so a game
        /// left mid-showcase takes them along; a showcase game the viewer
        /// saves rebuilds them on loading.
        /// </summary>
        private void QuietStoryteller()
        {
            Storyteller teller = game?.storyteller;
            if (teller == null || heldStorytellerComps != null)
            {
                return;
            }

            heldStorytellerComps = teller.storytellerComps ?? new List<StorytellerComp>();
            teller.storytellerComps = new List<StorytellerComp>();
            int dropped = teller.incidentQueue?.Count ?? 0;
            teller.incidentQueue?.Clear();
            ShowcaseLog.Cue("STORYTELLER", $"quiet ({Parts(heldStorytellerComps.Count)} held"
                + (dropped > 0 ? $", {dropped} queued incidents dropped)" : ")"));
        }

        /// <summary>A count of storyteller parts, for a cue.</summary>
        /// <param name="n">The count.</param>
        /// <returns>"1 part" or "n parts".</returns>
        private static string Parts(int n) => n == 1 ? "1 part" : $"{n} parts";

        /// <summary>
        /// Gives the storyteller its parts back, if the showcase holds
        /// them, and logs a STORYTELLER cue. If something rebuilt the
        /// parts meanwhile (a change of storyteller or difficulty does),
        /// the new ones stay.
        /// </summary>
        private void RestoreStoryteller()
        {
            if (heldStorytellerComps == null)
            {
                return;
            }

            Storyteller teller = game?.storyteller;
            if (teller != null && (teller.storytellerComps == null
                || teller.storytellerComps.Count == 0))
            {
                teller.storytellerComps = heldStorytellerComps;
            }

            heldStorytellerComps = null;
            ShowcaseLog.Cue("STORYTELLER",
                $"restored ({Parts(teller?.storytellerComps?.Count ?? 0)})");
        }

        /// <summary>
        /// Pauses the game while the new map settles, so the worker is set
        /// up before the game's first tick. Unpaused, the game would run
        /// as many ticks in the settling frames as the frame rate allowed,
        /// and the stage would differ from run to run however it was
        /// seeded.
        /// </summary>
        private void HoldWhileSettling()
        {
            TickManager ticks = game?.tickManager;
            if (ticks == null || settleHeld || ticks.CurTimeSpeed == TimeSpeed.Paused)
            {
                return;
            }

            speedBeforeSettle = ticks.CurTimeSpeed;
            ticks.CurTimeSpeed = TimeSpeed.Paused;
            settleHeld = ticks.CurTimeSpeed == TimeSpeed.Paused;
            if (!settleHeld)
            {
                // The setter refuses while the player has no control.
                Log.Warning($"{ShowcaseLog.Prefix} could not pause the game while "
                    + "the map settles; the stage may not repeat for its seed");
            }
        }

        /// <summary>
        /// Lets the game run again after the settle hold, at its speed
        /// before, unless the viewer has changed the speed meanwhile.
        /// </summary>
        private void ReleaseSettleHold()
        {
            if (!settleHeld)
            {
                return;
            }

            settleHeld = false;
            TickManager ticks = game?.tickManager;
            if (ticks != null && ticks.CurTimeSpeed == TimeSpeed.Paused)
            {
                ticks.CurTimeSpeed = speedBeforeSettle;
            }
        }

        /// <summary>
        /// After the showcase finishes: says any narration still queued
        /// as it comes due, then hands the closing message to the
        /// narration overlay, once. It is patient: it appears only when the
        /// last line has had its whole reading time, and fades like any
        /// other line, leaving the overlay empty.
        /// </summary>
        private void SayFarewellWhenQuiet()
        {
            try
            {
                if (worker != null && worker.HasQueuedNarration)
                {
                    worker.SayDueNarration();
                    return;
                }
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not finish the "
                    + $"narration of {def.defName}: {e.Message}");
            }

            farewellPending = false;
            try
            {
                narration.Say(
                    "BT_Showcases_Finished".Translate(),
                    NarrationOverlayDrawer.Now,
                    patient: true);
                farewellTick = Find.TickManager.TicksGame;
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not show the closing "
                    + $"message: {e.Message}");
            }
        }

        /// <summary>
        /// Moves the narration overlay on to now, telling it when the next
        /// queued line is due (converted to real seconds at the game's
        /// speed). A failure is logged once and hides the overlay.
        /// </summary>
        private void UpdateNarration()
        {
            if (narrationFailed || (narration.IsEmpty && worker == null))
            {
                return;
            }

            try
            {
                double now = NarrationOverlayDrawer.Now;
                int? next = worker?.NextQueuedTick;
                TickManager ticks = Find.TickManager;
                double? seconds = next.HasValue && ticks != null
                    ? NarrationTiming.SecondsUntil(
                        next.Value - ticks.TicksGame, ticks.TickRateMultiplier)
                    : (double?)null;
                narration.SetNextLineIn(seconds, now);
                narration.Advance(now);
            }
            catch (Exception e)
            {
                narrationFailed = true;
                Log.Error($"{ShowcaseLog.Prefix} the narration overlay failed "
                    + $"and is hidden for this game: {e}");
            }
        }

        /// <summary>
        /// Draws the narration overlay, if it has a line: below the caption
        /// bar while that shows, and never taking a click. Guarded like
        /// the caption bar: one failure is logged and the overlay hidden,
        /// and the colour state is always restored.
        /// </summary>
        private void DrawNarration()
        {
            if (narrationFailed || narration.IsEmpty)
            {
                return;
            }

            Color color = GUI.color;
            try
            {
                float top = CaptionTop() + CaptionBarLayout.Margin
                    + (Running && !guiFailed ? CaptionBarLayout.Height : 0f);
                double now = NarrationOverlayDrawer.Now;
                narration.Advance(now);
                if (NarrationOverlayDrawer.Draw(narration, now, top))
                {
                    narrationFrames++;
                }
            }
            catch (Exception e)
            {
                narrationFailed = true;
                Log.Error($"{ShowcaseLog.Prefix} the narration overlay failed "
                    + $"and is hidden for this game: {e}");
            }
            finally
            {
                GUI.color = color;
            }
        }

        /// <summary>
        /// Resets the autosaver's counter. If RimWorld ever renames the
        /// field, the session says so once, so a showcase game is never
        /// silently left autosaving.
        /// </summary>
        private void SuppressAutosave()
        {
            Autosaver autosaver = game?.autosaver;
            if (autosaver == null)
            {
                return;
            }

            if (AutosaveCounter == null || AutosaveCounter.FieldType != typeof(int))
            {
                if (!autosaveWarned)
                {
                    autosaveWarned = true;
                    Log.Warning($"{ShowcaseLog.Prefix} cannot find "
                        + "Autosaver.ticksSinceSave; this showcase game may "
                        + "autosave over your autosave slots.");
                }

                return;
            }

            AutosaveCounter.SetValue(autosaver, 0);
        }

        /// <summary>
        /// Draws the bar: a dark title card with the caption, Skip (when
        /// offered) and Stop. A click on the bar's background is used up,
        /// so it does not select whatever lies beneath.
        /// </summary>
        private void DrawCaptionBar()
        {
            string caption = worker.Caption;
            if (caption.NullOrEmpty())
            {
                caption = def.LabelCap.Resolve();
            }

            bool canSkip = worker.CanSkip;
            Text.Font = GameFont.Small;
            float textWidth = Text.CalcSize(caption).x + CaptionSlack;
            CaptionBarRects rects = CaptionBarLayout.Compute(
                UI.screenWidth, CaptionTop(), textWidth, canSkip);
            Rect bar = ToRect(rects.Bar);
            if (Event.current.type == EventType.Repaint)
            {
                Widgets.DrawWindowBackground(bar);
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.LabelEllipses(ToRect(rects.Text), caption);
            Text.Anchor = TextAnchor.UpperLeft;
            if (rects.Skip.HasValue)
            {
                Rect skip = ToRect(rects.Skip.Value);
                TooltipHandler.TipRegion(skip, "BT_Showcases_SkipTip".Translate());
                if (Widgets.ButtonText(skip, "BT_Showcases_Skip".Translate()))
                {
                    Skip();
                }
            }

            Rect stop = ToRect(rects.Stop);
            TooltipHandler.TipRegion(stop, "BT_Showcases_StopTip".Translate());
            if (Widgets.ButtonText(stop, "BT_Showcases_Stop".Translate()))
            {
                StopShowcase(byViewer: true);
            }

            if (Event.current.type == EventType.MouseDown && Mouse.IsOver(bar))
            {
                Event.current.Use();
            }

            if (Event.current.type == EventType.Repaint)
            {
                captionFrames++;
            }
        }

        /// <summary>
        /// The bar's top edge: below the colonist bar's lowest row, if any
        /// colonist is shown.
        /// </summary>
        /// <returns>The top edge, in UI units.</returns>
        private static float CaptionTop()
        {
            ColonistBar colonists = Find.ColonistBar;
            if (colonists?.DrawLocs == null)
            {
                return CaptionBarLayout.Margin;
            }

            return CaptionBarLayout.TopBelowColonists(
                colonists.DrawLocs.Select(p => p.y),
                colonists.Size.y,
                colonists.Scale);
        }

        /// <summary>Converts a Core box to a Unity rectangle.</summary>
        /// <param name="box">The box.</param>
        /// <returns>The rectangle.</returns>
        private static Rect ToRect(UiBox box) =>
            new Rect(box.X, box.Y, box.Width, box.Height);

        /// <summary>
        /// Shows a session message, silently (the viewer's click has
        /// already sounded), never throwing.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>
        /// True if the message was shown; false if showing it threw (the
        /// failure is logged as a warning).
        /// </returns>
        private static bool Message(string text)
        {
            try
            {
                Messages.Message(text, MessageTypeDefOf.SilentInput, false);
                return true;
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not show a message: "
                    + e.Message);
                return false;
            }
        }
    }
}
