using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="OwnedFlagsLoan{TOwner}"/>, the loan of a
/// storyteller def's flags that a quiet screen raises
/// (docs/SHOWCASE-API.md, "The Quiet Screen").
/// </summary>
public sealed class OwnedFlagsLoanTests
{
    /// <summary>A stand-in for a def that holds flags.</summary>
    private sealed class Owner
    {
    }

    /// <summary>
    /// Nothing is owed before a borrow, and repaying then hands back
    /// nothing.
    /// </summary>
    [Fact(DisplayName = "Nothing borrowed, nothing to repay")]
    public void Repay_should_hand_back_nothing_when_nothing_is_owed()
    {
        var loan = new OwnedFlagsLoan<Owner>();
        Assert.False(loan.Owed);
        Assert.Null(loan.Repay());
    }

    /// <summary>
    /// The values before the first borrow are the ones handed back, even
    /// if the same owner is borrowed again with the flags already raised.
    /// </summary>
    [Fact(DisplayName = "The first values are the ones repaid")]
    public void Repay_should_hand_back_the_values_before_the_first_borrow()
    {
        var loan = new OwnedFlagsLoan<Owner>();
        var def = new Owner();
        Assert.Null(loan.Borrow(def, new[] { false, true }));
        Assert.Null(loan.Borrow(def, new[] { true, true }));
        Assert.True(loan.Owed);
        Assert.Same(def, loan.Owner);

        FlagRepayment<Owner>? back = loan.Repay();
        Assert.NotNull(back);
        Assert.Same(def, back!.Owner);
        Assert.Equal(new[] { false, true }, back.Values);
        Assert.False(loan.Owed);
        Assert.Null(loan.Repay());
    }

    /// <summary>
    /// Borrowing from another owner (the viewer picked another
    /// storyteller) hands the first owner's values back at once, and the
    /// new owner's are the ones owed.
    /// </summary>
    [Fact(DisplayName = "A new owner gets the old one's flags handed back")]
    public void Borrow_should_hand_back_the_old_owner_when_the_owner_changes()
    {
        var loan = new OwnedFlagsLoan<Owner>();
        var first = new Owner();
        var second = new Owner();
        loan.Borrow(first, new[] { false, false });

        FlagRepayment<Owner>? back = loan.Borrow(second, new[] { true, false });
        Assert.NotNull(back);
        Assert.Same(first, back!.Owner);
        Assert.Equal(new[] { false, false }, back.Values);
        Assert.Same(second, loan.Owner);

        FlagRepayment<Owner>? last = loan.Repay();
        Assert.Same(second, last!.Owner);
        Assert.Equal(new[] { true, false }, last.Values);
    }

    /// <summary>
    /// Borrowing from no owner (no storyteller) ends the loan, handing
    /// back what was owed.
    /// </summary>
    [Fact(DisplayName = "Borrowing from no owner repays")]
    public void Borrow_should_repay_when_there_is_no_owner()
    {
        var loan = new OwnedFlagsLoan<Owner>();
        var def = new Owner();
        loan.Borrow(def, new[] { false });
        FlagRepayment<Owner>? back = loan.Borrow(null, new[] { true });
        Assert.Same(def, back!.Owner);
        Assert.False(loan.Owed);
    }

    /// <summary>
    /// The values recorded are a copy: changing the array passed in later
    /// does not change what is repaid.
    /// </summary>
    [Fact(DisplayName = "The recorded values are a copy")]
    public void Borrow_should_copy_the_values()
    {
        var loan = new OwnedFlagsLoan<Owner>();
        bool[] values = { false, false };
        loan.Borrow(new Owner(), values);
        values[0] = true;
        Assert.Equal(new[] { false, false }, loan.Repay()!.Values);
    }

    /// <summary>An owner without values is refused.</summary>
    [Fact(DisplayName = "An owner without values is refused")]
    public void Borrow_should_refuse_an_owner_without_values()
    {
        var loan = new OwnedFlagsLoan<Owner>();
        Assert.Throws<System.ArgumentNullException>(() => loan.Borrow(new Owner(), null!));
        Assert.False(loan.Owed);
    }
}
