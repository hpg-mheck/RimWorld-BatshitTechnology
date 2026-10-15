// Showcase API - a quiet screen while a showcase runs.
// A showcase is a film, and vanilla's clutter piles up over it: alerts
// ("Need colonist beds", "Pen needed") down the right, letters ("Ancient
// mech", "Roof collapsed") under them, messages top left, and the
// Learning helper's lessons. A def that asks for a quiet screen
// (quietScreen) gets, from its set-up until it finishes or stops, all
// without Harmony (docs/SHOWCASE-API.md, "The Quiet Screen"):
// - no alerts and no Learning helper: the current storyteller def's
//   disableAlerts and disableAdaptiveTraining are raised, as vanilla's
//   hidden Tutor storyteller has them (RimWorld 1.6.4871:
//   AlertsReadout.AlertsReadoutUpdate clears every alert while the first
//   is set; TutorSystem.AdaptiveTrainingEnabled is false while the second
//   is). A def is shared by every game until RimWorld restarts, so the
//   values from before are lent, not set (Core's OwnedFlagsLoan), and
//   given back on every way out, as god mode is. Prefs are never touched:
//   Prefs.Save runs from the options, the main menu and the key bindings,
//   and would keep a value changed for the showcase;
// - no letter that arrived after the showcase began: taken off the stack
//   each frame, after the ticks and before the frame is drawn. The letter
//   stays in the History tab's archive. Its sound has played by then, and
//   a big threat's letter pauses the game under the default preference
//   (pause on major threats), so the mod's own letters are not sent at
//   all while the screen is quiet (IsQuiet);
// - no top-left message: vanilla's live messages are cleared each frame.
//   Historical ones stay archived.

using RimWorld;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Keeps the screen quiet while a showcase that asks for it runs, and
    /// gives back what it borrowed.
    /// </summary>
    public static class ShowcaseScreen
    {
        /// <summary>
        /// The loan of the storyteller def's flags, in the order
        /// disableAlerts, disableAdaptiveTraining.
        /// </summary>
        private static readonly OwnedFlagsLoan<StorytellerDef> Loan =
            new OwnedFlagsLoan<StorytellerDef>();

        /// <summary>
        /// Gets a value indicating whether a showcase holds the storyteller
        /// def's flags that have not been given back yet.
        /// </summary>
        public static bool Lent => Loan.Owed;

        /// <summary>
        /// Gets the storyteller def whose flags are lent, or null.
        /// </summary>
        public static StorytellerDef LentDef => Loan.Owner;

        /// <summary>
        /// Gets how many letters the quiet screen has taken off the stack
        /// since it was last lent.
        /// </summary>
        public static int LettersTakenOff { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the screen is quiet now: a
        /// showcase that asked for it is running in the current game. The
        /// mod's own letter senders ask, and send nothing while it is.
        /// </summary>
        public static bool IsQuiet =>
            GameComponent_ShowcaseSession.Current?.ScreenQuiet == true;

        /// <summary>
        /// Raises the current storyteller def's flags, lending them: the
        /// values from before are put back by <see cref="Restore"/>. If
        /// the storyteller changed since the last call, the old def's
        /// flags are put back first. Resets the letter count when a new
        /// loan starts.
        /// </summary>
        /// <param name="def">The current storyteller def, or null.</param>
        internal static void Lend(StorytellerDef def)
        {
            bool fresh = !Loan.Owed;
            PutBack(Loan.Borrow(
                def, def == null
                    ? new bool[0]
                    : new[] { def.disableAlerts, def.disableAdaptiveTraining }));
            if (def == null)
            {
                return;
            }

            if (fresh)
            {
                LettersTakenOff = 0;
            }

            def.disableAlerts = true;
            def.disableAdaptiveTraining = true;
        }

        /// <summary>
        /// Keeps the screen quiet for one frame: keeps the flags lent on
        /// the current storyteller def (another, if the viewer changed
        /// it), takes off every letter that arrived at or after
        /// <paramref name="sinceTick"/>, and clears vanilla's live
        /// messages. Call it from the session's update, which runs after
        /// the frame's ticks and before the frame is drawn.
        /// </summary>
        /// <param name="sinceTick">The tick the showcase began.</param>
        internal static void Keep(int sinceTick)
        {
            Lend(Find.Storyteller?.def);
            LetterStack stack = Find.LetterStack;
            if (stack != null)
            {
                var letters = stack.LettersListForReading;
                for (int i = letters.Count - 1; i >= 0; i--)
                {
                    Letter letter = letters[i];
                    if (letter != null && letter.arrivalTick >= sinceTick)
                    {
                        stack.RemoveLetter(letter);
                        LettersTakenOff++;
                    }
                }
            }

            Messages.Clear();
        }

        /// <summary>
        /// Gives the storyteller def's flags back: the values they had
        /// before the showcase lent them. Does nothing when nothing is
        /// owed, so every way out of a showcase calls it.
        /// </summary>
        public static void Restore()
        {
            PutBack(Loan.Repay());
        }

        /// <summary>
        /// Puts a def's flags back as the loan hands them back.
        /// </summary>
        /// <param name="back">What to put back, or null for nothing.</param>
        private static void PutBack(FlagRepayment<StorytellerDef> back)
        {
            if (back?.Owner == null || back.Values == null || back.Values.Count < 2)
            {
                return;
            }

            back.Owner.disableAlerts = back.Values[0];
            back.Owner.disableAdaptiveTraining = back.Values[1];
        }
    }
}
