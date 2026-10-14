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
// stops or finishes, and again whenever a game that is not a showcase starts or loads. Two
// internal seams, AdoptForTest and ReleaseForTest, let the smoke runner
// (Source/SmokeTestShowcases.cs) run test showcases in its own game.

using System;
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
        /// Takes the launcher's request for this game, if there is one. A
        /// game that is not a showcase gives back any god mode an earlier
        /// showcase still held.
        /// </summary>
        public override void StartedNewGame()
        {
            LaunchRequest request = ShowcaseLauncher.TakeRequestFor(game)
                ?? ShowcaseLauncher.TakeQuickTestRequest();
            if (request == null)
            {
                ShowcaseGodMode.Restore();
                return;
            }

            def = request.Def;
            forced = request.Forced;
            ShowcaseLog.Cue("GAME", def.defName);
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
                ShowcaseGodMode.Restore();
                ShowcaseLog.Cue("FINISHED", def.defName);
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

            ShowcaseGodMode.Restore();
            ShowcaseLog.Cue("STOPPED", def.defName);
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
        /// any god mode it borrowed.
        /// </summary>
        private void ResetRun()
        {
            ShowcaseGodMode.Restore();
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
        /// Makes the worker and sets it up on the map, then logs READY,
        /// the cue capture tooling waits on before maximizing the window.
        /// A set-up that throws is logged and ends the showcase; the game
        /// stays open.
        /// </summary>
        /// <param name="map">The showcase's map.</param>
        private void Begin(Map map)
        {
            begun = true;
            ShowcaseLog.Cue("START", forced ? $"{def.defName} forced" : def.defName);
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
                return;
            }

            ShowcaseLog.Cue("READY", null);
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
