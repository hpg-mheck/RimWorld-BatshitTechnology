// Batshit Technology - smoke checks for the Showcase API's quiet screen
// (slice 19000b; docs/SHOWCASE-API.md, "The Quiet Screen"). Test
// showcases that ask for it are adopted in the smoke game: the checks see
// the storyteller def's alert and adaptive-training flags lent (alerts
// and the Learning helper off) with the player's preferences untouched,
// a letter that arrives and a top-left message taken off at the next
// frame's update while a letter from before stays, the flags lent on to
// another storyteller the viewer picks, and the flags given back on
// every way out: Stop, the showcase finishing, the main menu, a loaded
// game and a new game. A showcase that does not ask leaves the screen
// alone. The session's update is called directly: the smoke runner's
// ticks run without frames.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// A smoke-test worker that does nothing and never finishes on its own,
    /// for the quiet-screen checks.
    /// </summary>
    internal class SmokeQuietWorker : ShowcaseWorker
    {
        /// <summary>Gets a value indicating whether it is done: never.</summary>
        public override bool Finished => false;

        /// <summary>Sets nothing up.</summary>
        /// <param name="map">The map.</param>
        public override void Setup(Map map)
        {
        }
    }

    /// <summary>
    /// A <see cref="SmokeQuietWorker"/> that finishes two ticks after it
    /// starts.
    /// </summary>
    internal sealed class SmokeQuietFinishingWorker : SmokeQuietWorker
    {
        /// <summary>
        /// Gets a value indicating whether two ticks have passed since
        /// set-up.
        /// </summary>
        public override bool Finished => Find.TickManager.TicksGame - StartTick >= 2;
    }

    /// <summary>The Showcase API's quiet-screen checks.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// A test showcase that asks for a quiet screen (run directly,
        /// never listed).
        /// </summary>
        private const string QuietShowcase = "BT_SmokeShowcase_Quiet";

        /// <summary>
        /// The quiet screen: flags lent and given back on every way out,
        /// letters and messages taken off, the player's preferences left
        /// alone, and nothing done for a showcase that does not ask.
        /// </summary>
        /// <param name="map">The map.</param>
        private void TestQuietScreen(Map map)
        {
            Storyteller storyteller = Find.Storyteller;
            StorytellerDef teller = storyteller?.def;
            if (teller == null)
            {
                Check("a quiet-screen showcase lends the storyteller def's flags",
                    false, "the smoke game has no storyteller def");
                return;
            }

            bool alertsBefore = teller.disableAlerts;
            bool trainingBefore = teller.disableAdaptiveTraining;
            bool prefTraining = Prefs.AdaptiveTrainingEnabled;
            bool prefDev = Prefs.DevMode;
            ShowcaseDef quiet = MakeQuietShowcase(QuietShowcase, typeof(SmokeQuietWorker), true, 940);
            ShowcaseDef finishing = MakeQuietShowcase(
                QuietShowcase + "_Finishing", typeof(SmokeQuietFinishingWorker), true, 941);
            ShowcaseDef loud = MakeQuietShowcase(
                QuietShowcase + "_Loud", typeof(SmokeQuietWorker), false, 942);
            var letters = new List<Letter>();
            StorytellerDef other = DefDatabase<StorytellerDef>.AllDefs
                .FirstOrDefault(d => d != teller);
            bool otherAlerts = other?.disableAlerts ?? false;
            bool otherTraining = other?.disableAdaptiveTraining ?? false;
            try
            {
                Letter older = SmokeLetter("before", letters);
                Tick(1);
                showcaseSession.AdoptForTest(quiet, map);
                bool lent = teller.disableAlerts && teller.disableAdaptiveTraining
                    && !TutorSystem.AdaptiveTrainingEnabled && ShowcaseScreen.Lent
                    && ShowcaseScreen.LentDef == teller && ShowcaseScreen.IsQuiet
                    && showcaseSession.ScreenQuiet;
                bool prefsKept = Prefs.AdaptiveTrainingEnabled == prefTraining
                    && Prefs.DevMode == prefDev;
                Check("a quiet-screen showcase lends the storyteller def's alert and "
                        + "adaptive-training flags (no alerts, no Learning helper), and "
                        + "leaves the player's preferences alone",
                    lent && prefsKept,
                    $"alerts off {teller.disableAlerts}; training off "
                        + $"{teller.disableAdaptiveTraining}; helper on "
                        + $"{TutorSystem.AdaptiveTrainingEnabled}; lent "
                        + $"{ShowcaseScreen.Lent}; quiet {ShowcaseScreen.IsQuiet}; "
                        + $"prefs kept {prefsKept}");

                Letter during = SmokeLetter("during", letters);
                Messages.Message("Smoke message during a quiet screen.",
                    MessageTypeDefOf.SilentInput, false);
                int liveBefore = LiveMessages().Count;
                int takenBefore = ShowcaseScreen.LettersTakenOff;
                showcaseSession.GameComponentUpdate();
                List<Letter> stack = Find.LetterStack.LettersListForReading;
                bool letterOff = !stack.Contains(during) && stack.Contains(older)
                    && ShowcaseScreen.LettersTakenOff == takenBefore + 1;
                bool messagesOff = LiveMessagesField != null && liveBefore > 0
                    && LiveMessages().Count == 0;
                Check("while the screen is quiet, a letter that arrives and every "
                        + "top-left message are taken off before the frame is "
                        + "drawn; a letter from before stays",
                    letterOff && messagesOff,
                    $"arrived letter on the stack {stack.Contains(during)}; earlier "
                        + $"letter kept {stack.Contains(older)}; taken off "
                        + $"{takenBefore} to {ShowcaseScreen.LettersTakenOff}; live "
                        + $"messages {liveBefore} to {LiveMessages().Count}"
                        + (LiveMessagesField == null ? " (not readable)" : string.Empty));

                CheckQuietScreenFollowsStoryteller(storyteller, teller, other, alertsBefore,
                    trainingBefore, otherAlerts, otherTraining);

                showcaseSession.StopShowcase(byViewer: false);
                bool back = FlagsBack(teller, alertsBefore, trainingBefore)
                    && !ShowcaseScreen.IsQuiet && !showcaseSession.ScreenQuiet;
                Letter after = SmokeLetter("after", letters);
                showcaseSession.GameComponentUpdate();
                bool kept = Find.LetterStack.LettersListForReading.Contains(after);
                showcaseSession.ReleaseForTest();
                Check("Stop gives the storyteller def's flags back, and letters are "
                        + "left to arrive again",
                    back && kept, $"flags back {back}; a later letter kept {kept}");

                showcaseSession.AdoptForTest(finishing, map);
                bool onBefore = ShowcaseScreen.Lent && teller.disableAlerts;
                TickUntil(() => !showcaseSession.Running, 10);
                bool offAfter = FlagsBack(teller, alertsBefore, trainingBefore)
                    && !ShowcaseScreen.IsQuiet;
                showcaseSession.ReleaseForTest();
                Check("a quiet-screen showcase that finishes gives the flags back",
                    onBefore && offAfter, $"lent {onBefore}; given back at FINISHED {offAfter}");

                CheckQuietScreenWaysOut(map, quiet, teller, alertsBefore, trainingBefore);

                showcaseSession.AdoptForTest(loud, map);
                bool untouched = !ShowcaseScreen.Lent && !showcaseSession.ScreenQuiet
                    && FlagsBack(teller, alertsBefore, trainingBefore);
                Letter loudLetter = SmokeLetter("loud", letters);
                showcaseSession.GameComponentUpdate();
                bool loudKept = Find.LetterStack.LettersListForReading.Contains(loudLetter);
                showcaseSession.ReleaseForTest();
                Check("a showcase that does not ask for a quiet screen leaves the "
                        + "screen alone",
                    untouched && loudKept,
                    $"flags untouched {untouched}; its letter kept {loudKept}");
            }
            finally
            {
                showcaseSession.ReleaseForTest();
                if (storyteller != null)
                {
                    storyteller.def = teller;
                }

                teller.disableAlerts = alertsBefore;
                teller.disableAdaptiveTraining = trainingBefore;
                if (other != null)
                {
                    other.disableAlerts = otherAlerts;
                    other.disableAdaptiveTraining = otherTraining;
                }

                foreach (Letter letter in letters)
                {
                    if (Find.LetterStack.LettersListForReading.Contains(letter))
                    {
                        Find.LetterStack.RemoveLetter(letter);
                    }
                }
            }
        }

        /// <summary>
        /// The viewer picks another storyteller mid-showcase: at the next
        /// update the flags are lent on the new def, and the first def's
        /// are given back at once. The first def is then put back, and
        /// lent again.
        /// </summary>
        /// <param name="storyteller">The game's storyteller.</param>
        /// <param name="teller">The def it had.</param>
        /// <param name="other">Another storyteller def, or null.</param>
        /// <param name="alertsBefore">The first def's disableAlerts before.</param>
        /// <param name="trainingBefore">The first def's disableAdaptiveTraining before.</param>
        /// <param name="otherAlerts">The other def's disableAlerts before.</param>
        /// <param name="otherTraining">The other def's disableAdaptiveTraining before.</param>
        private void CheckQuietScreenFollowsStoryteller(Storyteller storyteller,
            StorytellerDef teller, StorytellerDef other, bool alertsBefore,
            bool trainingBefore, bool otherAlerts, bool otherTraining)
        {
            const string Name = "the quiet screen follows a change of storyteller: "
                + "the new def's flags are lent, the old def's given back";
            if (other == null)
            {
                Check(Name, false, "no other storyteller def to change to");
                return;
            }

            storyteller.def = other;
            showcaseSession.GameComponentUpdate();
            bool moved = ShowcaseScreen.LentDef == other && other.disableAlerts
                && other.disableAdaptiveTraining
                && teller.disableAlerts == alertsBefore
                && teller.disableAdaptiveTraining == trainingBefore;
            storyteller.def = teller;
            showcaseSession.GameComponentUpdate();
            bool movedBack = ShowcaseScreen.LentDef == teller && teller.disableAlerts
                && other.disableAlerts == otherAlerts
                && other.disableAdaptiveTraining == otherTraining;
            Check(Name, moved && movedBack,
                $"lent to {other.defName} {moved}; back to {teller.defName} {movedBack}");
        }

        /// <summary>
        /// Leaving a quiet-screen showcase's game (the main menu, a loaded
        /// game, a new game) gives the storyteller def's flags back.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="quiet">The quiet-screen test showcase.</param>
        /// <param name="teller">The storyteller def.</param>
        /// <param name="alertsBefore">Its disableAlerts before.</param>
        /// <param name="trainingBefore">Its disableAdaptiveTraining before.</param>
        private void CheckQuietScreenWaysOut(Map map, ShowcaseDef quiet,
            StorytellerDef teller, bool alertsBefore, bool trainingBefore)
        {
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
                showcaseSession.AdoptForTest(quiet, map);
                bool on = ShowcaseScreen.Lent && teller.disableAlerts;
                leave();
                bool off = FlagsBack(teller, alertsBefore, trainingBefore);
                allBack &= on && off;
                results.Add($"{name}: lent {on}, given back {off}");
                showcaseSession.ReleaseForTest();
            }

            Check("leaving a quiet-screen showcase's game (main menu, loaded game, "
                    + "new game) gives the storyteller def's flags back",
                allBack, string.Join("; ", results));
        }

        /// <summary>
        /// Whether the storyteller def's flags are as before and nothing is
        /// lent. (The test session itself may still be running: leaving a
        /// game is only simulated here.)
        /// </summary>
        /// <param name="teller">The storyteller def.</param>
        /// <param name="alertsBefore">Its disableAlerts before.</param>
        /// <param name="trainingBefore">Its disableAdaptiveTraining before.</param>
        /// <returns>True if given back.</returns>
        private static bool FlagsBack(StorytellerDef teller, bool alertsBefore, bool trainingBefore) =>
            teller.disableAlerts == alertsBefore
            && teller.disableAdaptiveTraining == trainingBefore
            && !ShowcaseScreen.Lent;

        /// <summary>
        /// Makes a test showcase for the quiet-screen checks.
        /// </summary>
        /// <param name="defName">Its def name.</param>
        /// <param name="worker">Its worker class.</param>
        /// <param name="quietScreen">Whether it asks for a quiet screen.</param>
        /// <param name="order">Its place in the list.</param>
        /// <returns>The def.</returns>
        private static ShowcaseDef MakeQuietShowcase(
            string defName, Type worker, bool quietScreen, int order)
        {
            ShowcaseDef def = MakeTestShowcase(defName, "smoke quiet screen", null, order);
            def.workerClass = worker;
            def.quietScreen = quietScreen;
            return def;
        }

        /// <summary>
        /// Sends a silent neutral test letter now, and notes it for
        /// clearing up.
        /// </summary>
        /// <param name="when">A word for its label.</param>
        /// <param name="sent">The letters sent so far; it is added.</param>
        /// <returns>The letter.</returns>
        private static Letter SmokeLetter(string when, List<Letter> sent)
        {
            Letter letter = LetterMaker.MakeLetter(
                $"Smoke letter {when}", "A smoke test's letter.", LetterDefOf.NeutralEvent);
            Find.LetterStack.ReceiveLetter(letter, null, 0, playSound: false);
            sent.Add(letter);
            return letter;
        }
    }
}
