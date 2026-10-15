// Batshit Technology - the tour verifier's quiet-screen checks (slice
// 19000b; docs/SHOWCASE-API.md, "The Quiet Screen"). At READY: the
// screen is quiet (the storyteller def's alerts and adaptive training
// off, so no alert and no Learning helper) and no DEV button shows (the
// tour lends no god mode). Every tick until FINISHED: still so. Every
// frame until FINISHED: no letter that arrived after READY, and no
// top-left message, is still on screen a frame later (the session takes
// them off each frame, before the frame is drawn). At each scene's end:
// a fact on both. After FINISHED: the storyteller def's flags were given
// back. The SUMMARY's "screen" holds the counts.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The tour verifier's quiet-screen checks.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// The quiet screen at READY: the def asks for it, the session
        /// keeps it, the storyteller def's flags are lent (no alerts, no
        /// Learning helper), and no DEV button shows.
        /// </summary>
        /// <param name="session">The showcase session.</param>
        /// <param name="director">The tour's director.</param>
        private void CheckQuietScreenAtReady(
            GameComponent_ShowcaseSession session, BatshitTourDirector director)
        {
            StorytellerDef teller = Find.Storyteller?.def;
            Check("tour: the screen is quiet from READY: no alerts, no Learning "
                    + "helper, no DEV buttons",
                director.def?.quietScreen == true && session.ScreenQuiet
                    && ScreenIsQuiet(),
                $"def asks {director.def?.quietScreen}; session quiet "
                    + $"{session.ScreenQuiet}; {ScreenNote(teller)}");
        }

        /// <summary>
        /// The quiet screen given back at FINISHED: nothing lent, and the
        /// storyteller def's alerts and adaptive training on again (the
        /// showcase's storyteller has neither off of its own). Checked only
        /// when the tour finished.
        /// </summary>
        /// <param name="session">The showcase session, or null.</param>
        /// <param name="watch">What the run saw.</param>
        private void CheckQuietScreenGivenBack(
            GameComponent_ShowcaseSession session, TourWatch watch)
        {
            if (watch.FinishedTick < 0)
            {
                return; // "FINISHED reached" already fails
            }

            StorytellerDef teller = Find.Storyteller?.def;
            Check("tour: the quiet screen gave the storyteller's alerts and "
                    + "adaptive training back at FINISHED",
                teller != null && !ShowcaseScreen.Lent && session?.ScreenQuiet != true
                    && !teller.disableAlerts && !teller.disableAdaptiveTraining,
                $"lent {ShowcaseScreen.Lent}; session quiet {session?.ScreenQuiet}; "
                    + ScreenNote(teller));
        }

        /// <summary>
        /// Whether the screen is quiet now: alerts and adaptive training off
        /// on the current storyteller def, the Learning helper off, and no
        /// DEV button showing.
        /// </summary>
        /// <returns>True if quiet.</returns>
        private static bool ScreenIsQuiet()
        {
            StorytellerDef teller = Find.Storyteller?.def;
            return teller != null && teller.disableAlerts && teller.disableAdaptiveTraining
                && !TutorSystem.AdaptiveTrainingEnabled && !DebugSettings.ShowDevGizmos;
        }

        /// <summary>The screen's switches, for a check's detail.</summary>
        /// <param name="teller">The storyteller def, or null.</param>
        /// <returns>The text.</returns>
        private static string ScreenNote(StorytellerDef teller) =>
            teller == null
                ? "no storyteller"
                : $"{teller.defName}: alerts off {teller.disableAlerts}, adaptive "
                    + $"training off {teller.disableAdaptiveTraining}; helper on "
                    + $"{TutorSystem.AdaptiveTrainingEnabled}; DEV buttons "
                    + $"{DebugSettings.ShowDevGizmos} (dev mode {Prefs.DevMode}, god "
                    + $"mode {DebugSettings.godMode})";

        /// <summary>The tour watch's quiet-screen measures.</summary>
        private sealed partial class TourWatch
        {
            /// <summary>Vanilla's live messages (private in RimWorld 1.6).</summary>
            private static readonly FieldInfo LiveMessagesInWatch = typeof(Messages).GetField(
                "liveMessages", BindingFlags.Static | BindingFlags.NonPublic);

            /// <summary>Letters after READY seen at the last frame's sample.</summary>
            private HashSet<Letter> lettersLastFrame = new HashSet<Letter>();

            /// <summary>Messages seen at the last frame's sample.</summary>
            private HashSet<Message> messagesLastFrame = new HashSet<Message>();

            /// <summary>Letters and messages already listed as left on screen.</summary>
            private readonly HashSet<object> reported = new HashSet<object>();

            /// <summary>Every message seen on screen before FINISHED.</summary>
            private readonly HashSet<Message> messagesSeen = new HashSet<Message>();

            /// <summary>
            /// Letters and messages still on screen a frame later, each
            /// with the scene showing.
            /// </summary>
            private readonly List<(string Scene, string What)> stale =
                new List<(string Scene, string What)>();

            /// <summary>Ticks each scene's screen was not quiet, by key.</summary>
            private readonly Dictionary<string, int> loudTicks =
                new Dictionary<string, int>();

            /// <summary>Gets how many frames were sampled before FINISHED.</summary>
            public int ScreenFrames { get; private set; }

            /// <summary>
            /// Samples the screen once a frame, until FINISHED: a letter
            /// that arrived after READY, or a live message, seen on two
            /// frames in a row was not taken off. Comparing two frames'
            /// samples makes the check independent of whether this runs
            /// before or after the session's own update in a frame.
            /// </summary>
            public void SampleScreenFrame()
            {
                if (FinishedTick >= 0)
                {
                    return;
                }

                ScreenFrames++;
                string key = showing?.key ?? "before the first scene";
                var letters = new HashSet<Letter>(
                    (Find.LetterStack?.LettersListForReading ?? new List<Letter>())
                    .Where(l => l != null && l.arrivalTick >= ReadyTick));
                foreach (Letter letter in letters.Where(lettersLastFrame.Contains))
                {
                    if (reported.Add(letter))
                    {
                        stale.Add((key, $"letter '{letter.Label}' (arrived at "
                            + $"{letter.arrivalTick})"));
                    }
                }

                var messages = new HashSet<Message>(
                    (LiveMessagesInWatch?.GetValue(null) as List<Message>)?
                    .Where(m => m != null) ?? Enumerable.Empty<Message>());
                foreach (Message message in messages)
                {
                    messagesSeen.Add(message);
                    if (messagesLastFrame.Contains(message) && reported.Add(message))
                    {
                        stale.Add((key, $"message '{message.text}'"));
                    }
                }

                lettersLastFrame = letters;
                messagesLastFrame = messages;
            }

            /// <summary>
            /// Samples the screen's switches once a tick, while the
            /// showcase runs: a tick with alerts, the Learning helper or
            /// DEV buttons on is counted against the scene showing. (On
            /// the tick the showcase finishes the session may give the
            /// flags back before this runs; that tick is not counted.)
            /// </summary>
            private void SampleQuietScreen()
            {
                if (FinishedTick >= 0 || Director.Finished
                    || GameComponent_ShowcaseSession.Current?.Running != true
                    || ScreenIsQuiet())
                {
                    return;
                }

                string key = showing?.key ?? "before the first scene";
                loudTicks[key] = (loudTicks.TryGetValue(key, out int n) ? n : 0) + 1;
            }

            /// <summary>
            /// The quiet-screen fact at a scene's end: alerts, the Learning
            /// helper and DEV buttons off every tick, and no letter or
            /// message left on screen a frame later.
            /// </summary>
            /// <param name="scene">The scene that ended.</param>
            private void QuietScreenFacts(ShowcaseScene scene)
            {
                int loud = loudTicks.TryGetValue(scene.key, out int n) ? n : 0;
                List<string> left = stale.Where(s => s.Scene == scene.key)
                    .Select(s => s.What).ToList();
                Fact(scene, $"the screen stayed quiet ({loud} ticks with alerts, the "
                        + "Learning helper or DEV buttons on; left on screen a frame "
                        + $"later: [{string.Join("; ", left)}])",
                    loud == 0 && left.Count == 0);
            }

            /// <summary>Adds the quiet screen's counts to the SUMMARY.</summary>
            /// <param name="summary">The SUMMARY.</param>
            private void AddQuietScreenTo(JsonLine summary)
            {
                summary.Add("screen", new JsonLine
                {
                    { "frames", ScreenFrames },
                    { "lettersTakenOff", ShowcaseScreen.LettersTakenOff },
                    { "messagesSeen", messagesSeen.Count },
                    { "stale", stale.Select(s => $"{s.Scene}: {s.What}").ToList() },
                    { "loudTicks", loudTicks.Values.Sum() },
                });
            }
        }
    }
}
