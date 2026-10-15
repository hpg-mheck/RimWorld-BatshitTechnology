using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Showcases;

/// <summary>
/// What a <see cref="OwnedFlagsLoan{TOwner}"/> hands back: the owner whose
/// flags must be put back, and the values to put back, in the order they
/// were borrowed.
/// </summary>
/// <typeparam name="TOwner">The type that holds the flags.</typeparam>
/// <param name="Owner">The owner to put the flags back on.</param>
/// <param name="Values">The flags' values before the loan.</param>
public sealed record FlagRepayment<TOwner>(TOwner Owner, IReadOnlyList<bool> Values)
    where TOwner : class;

/// <summary>
/// Flags on one owner that a showcase raises while it runs and must put
/// back, such as a RimWorld storyteller def's <c>disableAlerts</c> and
/// <c>disableAdaptiveTraining</c>, which a quiet screen raises
/// (docs/SHOWCASE-API.md, "The Quiet Screen"). A def is shared by every
/// game until RimWorld restarts, so the values from before must be put
/// back on every way out of the showcase. The owner can change while the
/// loan stands (the viewer picks another storyteller): the old owner's
/// flags are then handed back at once, and the new owner's borrowed.
/// This class holds those rules without RimWorld, so they are unit
/// tested.
/// </summary>
/// <remarks>
/// The rules: the values before the first borrow from an owner are the
/// ones put back, however often it is borrowed again; borrowing from
/// another owner hands back the first; repaying when nothing is owed
/// hands back nothing, so every way out may call it.
/// </remarks>
/// <typeparam name="TOwner">The type that holds the flags.</typeparam>
public sealed class OwnedFlagsLoan<TOwner>
    where TOwner : class
{
    /// <summary>The owner's flags before the loan, or null.</summary>
    private bool[]? before;

    /// <summary>Gets the owner the flags were borrowed from, or null.</summary>
    public TOwner? Owner { get; private set; }

    /// <summary>Gets a value indicating whether flags are still owed.</summary>
    public bool Owed => Owner != null;

    /// <summary>
    /// Borrows an owner's flags: the first time from this owner, records
    /// their values. The caller then raises them.
    /// </summary>
    /// <param name="owner">The owner; null borrows nothing.</param>
    /// <param name="current">The flags' values now, in a fixed order.</param>
    /// <returns>
    /// What to put back on an earlier owner, if the flags were borrowed
    /// from another owner (or <paramref name="owner"/> is null); otherwise
    /// null.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="owner"/> is given but
    /// <paramref name="current"/> is null.
    /// </exception>
    public FlagRepayment<TOwner>? Borrow(TOwner? owner, IReadOnlyList<bool> current)
    {
        if (owner == null)
        {
            return Repay();
        }

        if (current == null)
        {
            throw new ArgumentNullException(nameof(current));
        }

        if (ReferenceEquals(owner, Owner))
        {
            return null;
        }

        FlagRepayment<TOwner>? earlier = Repay();
        Owner = owner;
        before = current.ToArray();
        return earlier;
    }

    /// <summary>
    /// Ends the loan: hands back the owner and its values from before.
    /// </summary>
    /// <returns>What to put back, or null if nothing is owed.</returns>
    public FlagRepayment<TOwner>? Repay()
    {
        if (Owner == null || before == null)
        {
            Owner = null;
            before = null;
            return null;
        }

        var repayment = new FlagRepayment<TOwner>(Owner, before);
        Owner = null;
        before = null;
        return repayment;
    }
}
