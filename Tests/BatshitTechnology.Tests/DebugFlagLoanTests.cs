using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the loan of a debug flag such as god mode to a showcase: it
/// is raised only where allowed, the value before the first borrow comes
/// back, and nothing is ever left raised outside dev mode.
/// </summary>
public sealed class DebugFlagLoanTests
{
    /// <summary>
    /// Outside dev mode the flag is never raised and nothing is owed, so a
    /// player's later games are untouched.
    /// </summary>
    [Fact(DisplayName = "A flag that is not allowed is never raised")]
    public void Borrow_should_not_raise_a_flag_that_is_not_allowed()
    {
        var loan = new DebugFlagLoan();

        Assert.False(loan.Borrow(allowed: false, current: false));
        Assert.False(loan.Owed);
        Assert.False(loan.Repay(allowed: false, current: false));
    }

    /// <summary>
    /// In dev mode the flag is raised for the showcase and lowered again
    /// on the way out.
    /// </summary>
    [Fact(DisplayName = "A borrowed flag is raised, then put back")]
    public void Borrow_then_repay_should_restore_the_earlier_value()
    {
        var loan = new DebugFlagLoan();

        Assert.True(loan.Borrow(allowed: true, current: false));
        Assert.True(loan.Owed);
        Assert.False(loan.Repay(allowed: true, current: true));
        Assert.False(loan.Owed);
    }

    /// <summary>
    /// A developer who already had god mode on keeps it: the loan restores
    /// what it found.
    /// </summary>
    [Fact(DisplayName = "A flag already raised stays raised after repayment")]
    public void Repay_should_keep_a_flag_that_was_already_raised()
    {
        var loan = new DebugFlagLoan();

        Assert.True(loan.Borrow(allowed: true, current: true));
        Assert.True(loan.Repay(allowed: true, current: true));
    }

    /// <summary>
    /// Borrowing twice keeps the value from before the first borrow, not
    /// the raised value the second borrow saw.
    /// </summary>
    [Fact(DisplayName = "Borrowing twice keeps the first earlier value")]
    public void Borrow_twice_should_keep_the_first_earlier_value()
    {
        var loan = new DebugFlagLoan();

        loan.Borrow(allowed: true, current: false);
        Assert.True(loan.Borrow(allowed: true, current: true));
        Assert.False(loan.Repay(allowed: true, current: true));
    }

    /// <summary>
    /// Repaying when nothing is owed leaves the flag as it is, so every
    /// way out of a showcase may call it safely; and it pays back once.
    /// </summary>
    /// <param name="current">The flag's value now.</param>
    [Theory(DisplayName = "Repaying with nothing owed changes nothing")]
    [InlineData(false)]
    [InlineData(true)]
    public void Repay_with_nothing_owed_should_change_nothing(bool current)
    {
        var loan = new DebugFlagLoan();

        Assert.Equal(current, loan.Repay(allowed: true, current: current));
        loan.Borrow(allowed: true, current: false);
        Assert.False(loan.Repay(allowed: true, current: true));
        Assert.Equal(current, loan.Repay(allowed: true, current: current));
    }

    /// <summary>
    /// A viewer who left dev mode during the showcase is never handed god
    /// mode on the way out, even if it was on before.
    /// </summary>
    [Fact(DisplayName = "Repayment never raises a flag outside dev mode")]
    public void Repay_should_not_raise_a_flag_that_is_no_longer_allowed()
    {
        var loan = new DebugFlagLoan();

        loan.Borrow(allowed: true, current: true);
        Assert.False(loan.Repay(allowed: false, current: false));
        Assert.False(loan.Owed);
    }
}
