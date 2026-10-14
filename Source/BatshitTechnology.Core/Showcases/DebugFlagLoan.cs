namespace BatshitTechnology.Showcases;

/// <summary>
/// A debug flag a showcase borrows and must give back, such as RimWorld's
/// god mode (<c>DebugSettings.godMode</c>), which shows a showcase's
/// developer buttons. The flag is a plain static in the game: it outlives
/// the showcase game, and nothing in vanilla resets it when a game ends.
/// A showcase that sets it must therefore remember what it was and put it
/// back on every way out of the showcase game. This class holds that
/// rule without RimWorld, so it is unit tested.
/// </summary>
/// <remarks>
/// The rules: the flag is raised only where it is allowed (in RimWorld,
/// only in dev mode, since the buttons it shows need dev mode as well);
/// the value before the first borrow is the one restored, however often
/// the flag is borrowed; repaying when nothing is owed changes nothing;
/// and a repayment never raises the flag where it is no longer allowed,
/// so a viewer who left dev mode during a showcase is never handed god
/// mode on the way out.
/// </remarks>
public sealed class DebugFlagLoan
{
    /// <summary>The flag's value before the first borrow.</summary>
    private bool before;

    /// <summary>
    /// Gets a value indicating whether a borrowed value is still owed.
    /// </summary>
    public bool Owed { get; private set; }

    /// <summary>
    /// Borrows the flag: records its value the first time, and raises it
    /// where it is allowed.
    /// </summary>
    /// <param name="allowed">
    /// Whether the flag may be raised (in RimWorld, whether dev mode is
    /// on).
    /// </param>
    /// <param name="current">The flag's value now.</param>
    /// <returns>
    /// The value to set the flag to: true where allowed, otherwise
    /// <paramref name="current"/> unchanged (and nothing is owed).
    /// </returns>
    public bool Borrow(bool allowed, bool current)
    {
        if (!allowed)
        {
            return current;
        }

        if (!Owed)
        {
            Owed = true;
            before = current;
        }

        return true;
    }

    /// <summary>
    /// Gives the flag back: the value it had before the first borrow, but
    /// never raised where it is no longer allowed. Does nothing when
    /// nothing is owed, so it is safe to call on every way out.
    /// </summary>
    /// <param name="allowed">
    /// Whether the flag may be raised now (in RimWorld, whether dev mode
    /// is still on).
    /// </param>
    /// <param name="current">The flag's value now.</param>
    /// <returns>The value to set the flag to.</returns>
    public bool Repay(bool allowed, bool current)
    {
        if (!Owed)
        {
            return current;
        }

        Owed = false;
        return before && allowed;
    }
}
