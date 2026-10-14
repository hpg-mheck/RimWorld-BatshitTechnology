// Showcase API - god mode, lent to a showcase and always given back.
// A showcase may want its devices' developer buttons on screen, which
// vanilla shows only with dev mode and god mode both on
// (DebugSettings.ShowDevGizmos). God mode is a plain static flag
// (DebugSettings.godMode, RimWorld 1.6.4871): it outlives the game that
// set it, nothing in vanilla resets it when a game ends, and outside dev
// mode it is never drawn on the dev toolbar, so a player could not even
// see it, let alone switch it off. Left on, it builds everything at once
// and for free in the player's next real colony. So the API lends it:
// only in dev mode, remembering what it was, and the session gives it
// back when the showcase stops or finishes, and on every way out of a
// showcase game (the main menu, a new game, a loaded game). The rules
// live in Core (DebugFlagLoan), where they are unit tested.

using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Lends god mode to a showcase in dev mode, and gives it back.
    /// </summary>
    public static class ShowcaseGodMode
    {
        /// <summary>The loan: what god mode was before, if it is owed.</summary>
        private static readonly DebugFlagLoan Loan = new DebugFlagLoan();

        /// <summary>
        /// Gets a value indicating whether a showcase holds god mode that
        /// has not been given back yet.
        /// </summary>
        public static bool Lent => Loan.Owed;

        /// <summary>
        /// Turns god mode on for a showcase, so devices' developer buttons
        /// show, but only in dev mode; outside it, does nothing at all.
        /// The value god mode had before is remembered for
        /// <see cref="Restore"/>.
        /// </summary>
        internal static void Lend()
        {
            DebugSettings.godMode = Loan.Borrow(Prefs.DevMode, DebugSettings.godMode);
        }

        /// <summary>
        /// Gives god mode back: the value it had before the showcase
        /// borrowed it, never left on outside dev mode. Does nothing when
        /// nothing is owed, so every way out of a showcase calls it.
        /// </summary>
        public static void Restore()
        {
            if (!Loan.Owed)
            {
                return;
            }

            DebugSettings.godMode = Loan.Repay(Prefs.DevMode, DebugSettings.godMode);
        }
    }
}
