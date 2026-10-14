// Showcase API - the running session.
// RimWorld creates one of these for every game. It does nothing unless
// the game was generated for a showcase (ShowcaseLauncher), or started
// with -quicktest and -showcase=. Then, once the new map has settled, it
// makes the showcase's worker, sets it up, ticks it, draws the caption bar
// with Skip and Stop, writes the [BT-SHOWCASE] log cues capture tooling
// waits on, and keeps the game from autosaving, so a showcase never
// overwrites the player's autosave slots. Nothing about the session is
// saved: a showcase game the player chooses to save loads as an ordinary
// game.

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
        /// Takes the launcher's request for this game, if there is one.
        /// </summary>
        public override void StartedNewGame()
        {
            LaunchRequest request = ShowcaseLauncher.TakeRequestFor(game)
                ?? ShowcaseLauncher.TakeQuickTestRequest();
            if (request == null)
            {
                return;
            }

            def = request.Def;
            forced = request.Forced;
            ShowcaseLog.Cue("GAME", def.defName);
        }

        /// <summary>
        /// Sets the showcase up once the map has settled for a couple of
        /// seconds.
        /// </summary>
        public override void GameComponentUpdate()
        {
            if (def == null || begun
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
                ShowcaseLog.Cue("FINISHED", def.defName);
                SayFarewellWhenQuiet();
            }
        }

        /// <summary>
        /// Draws the caption bar while the showcase plays out: its caption
        /// and the Skip and Stop buttons, centred below the colonist bar.
        /// IMGUI calls this on every event, not once a frame: the buttons
        /// take part in every event, as vanilla's do, so their control ids
        /// stay in step, while the background is painted on repaint only.
        /// Everything is guarded; one failure is logged and the bar is
        /// hidden, and the text and colour state is always restored.
        /// </summary>
        public override void GameComponentOnGUI()
        {
            if (guiFailed || !Running || Event.current == null
                || Find.CurrentMap == null || WorldRendererUtility.WorldRendered
                || Find.UIRoot?.screenshotMode?.FiltersCurrentEvent == true)
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
        /// and the colony are the viewer's.
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
            try
            {
                worker.Stop();
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} {def.defName} failed while "
                    + $"stopping: {e}");
            }

            ShowcaseLog.Cue("STOPPED", def.defName);
            if (byViewer)
            {
                Message("BT_Showcases_Stopped".Translate());
            }
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
                worker.Begin(map);
            }
            catch (Exception e)
            {
                ended = true;
                ShowcaseLog.Cue("FAILED", def.defName);
                Log.Error($"{ShowcaseLog.Prefix} {def.defName} could not set "
                    + $"up: {e}");
                return;
            }

            ShowcaseLog.Cue("READY", null);
        }

        /// <summary>
        /// After the showcase finishes: says any narration still queued
        /// as it comes due, then the closing message, once.
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
            Message("BT_Showcases_Finished".Translate());
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
        /// Shows a session message, never throwing.
        /// </summary>
        /// <param name="text">The text.</param>
        private static void Message(string text)
        {
            try
            {
                Messages.Message(text, MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not show a message: "
                    + e.Message);
            }
        }
    }
}
