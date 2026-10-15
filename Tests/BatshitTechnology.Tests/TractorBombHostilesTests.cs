using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies how hostiles react to an armed Tractor Bomb in
/// <see cref="TractorBombHostiles"/> (docs/TRACTOR-BOMB.md, "Decisions"
/// 9): a third each flee at once, hesitate about two seconds or ignore it;
/// the roll is the same every time it is asked; and a hesitating hostile
/// starts to run two seconds after it first saw the bomb.
/// </summary>
public sealed class TractorBombHostilesTests
{
    /// <summary>
    /// The roll's thirds, edges included: below a third flees, below two
    /// thirds hesitates, the rest ignores; out-of-range values clamp and
    /// a value that is not a number flees.
    /// </summary>
    /// <param name="u">The uniform number.</param>
    /// <param name="expected">The reaction.</param>
    [Theory(DisplayName = "The roll's thirds: flee, hesitate, ignore")]
    [InlineData(0.0, TractorBombHostileReaction.Flee)]
    [InlineData(0.3333, TractorBombHostileReaction.Flee)]
    [InlineData(1.0 / 3.0, TractorBombHostileReaction.Hesitate)]
    [InlineData(0.5, TractorBombHostileReaction.Hesitate)]
    [InlineData(0.6666, TractorBombHostileReaction.Hesitate)]
    [InlineData(2.0 / 3.0, TractorBombHostileReaction.Ignore)]
    [InlineData(0.9999, TractorBombHostileReaction.Ignore)]
    [InlineData(1.0, TractorBombHostileReaction.Ignore)]
    [InlineData(7.5, TractorBombHostileReaction.Ignore)]
    [InlineData(-0.5, TractorBombHostileReaction.Flee)]
    [InlineData(double.NaN, TractorBombHostileReaction.Flee)]
    public void FromUnit_should_split_in_thirds(
        double u, TractorBombHostileReaction expected)
    {
        Assert.Equal(expected, TractorBombHostiles.FromUnit(u));
    }

    /// <summary>
    /// Asked again and again, a pawn's roll for a bomb never changes:
    /// nothing re-rolls every half second.
    /// </summary>
    [Fact(DisplayName = "A pawn's roll for a bomb never changes")]
    public void Roll_should_be_stable()
    {
        for (int pawn = 1; pawn < 500; pawn += 7)
        {
            TractorBombHostileReaction first =
                TractorBombHostiles.Roll(pawn, 4242, 99);
            for (int again = 0; again < 20; again++)
            {
                Assert.Equal(first, TractorBombHostiles.Roll(pawn, 4242, 99));
            }
        }
    }

    /// <summary>
    /// Over many hostiles, each reaction comes up about a third of the
    /// time (within two points), for several bombs and seeds.
    /// </summary>
    /// <param name="bombId">The bomb's id.</param>
    /// <param name="seed">The bomb's seed.</param>
    [Theory(DisplayName = "About a third of hostiles react each way")]
    [InlineData(1, 0)]
    [InlineData(5021, 123456)]
    [InlineData(77, -987654321)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void Roll_should_split_evenly(int bombId, int seed)
    {
        const int pawns = 30000;
        int[] counts = new int[3];
        for (int pawn = 0; pawn < pawns; pawn++)
        {
            counts[(int)TractorBombHostiles.Roll(pawn, bombId, seed)]++;
        }

        foreach (int count in counts)
        {
            Assert.InRange(count / (double)pawns, 0.31, 0.3567);
        }
    }

    /// <summary>
    /// The roll is per bomb: the same hostiles meeting a second bomb, or
    /// one planted with another seed, roll afresh, so about two thirds of
    /// them land on a different reaction.
    /// </summary>
    [Fact(DisplayName = "Each bomb gets its own rolls")]
    public void Roll_should_differ_between_bombs()
    {
        const int pawns = 3000;
        int otherBomb = Enumerable.Range(0, pawns).Count(p =>
            TractorBombHostiles.Roll(p, 100, 7)
                != TractorBombHostiles.Roll(p, 101, 7));
        int otherSeed = Enumerable.Range(0, pawns).Count(p =>
            TractorBombHostiles.Roll(p, 100, 7)
                != TractorBombHostiles.Roll(p, 100, 8));
        Assert.InRange(otherBomb / (double)pawns, 0.6, 0.73);
        Assert.InRange(otherSeed / (double)pawns, 0.6, 0.73);
    }

    /// <summary>
    /// Neighbouring pawn ids are not in lockstep: no long runs of one
    /// reaction among consecutive ids, as a weak hash would give.
    /// </summary>
    [Fact(DisplayName = "Neighbouring pawns roll independently")]
    public void Roll_should_not_run_in_streaks()
    {
        int longest = 0;
        int run = 0;
        TractorBombHostileReaction previous = (TractorBombHostileReaction)(-1);
        for (int pawn = 0; pawn < 5000; pawn++)
        {
            TractorBombHostileReaction r = TractorBombHostiles.Roll(pawn, 9, 3);
            run = r == previous ? run + 1 : 1;
            previous = r;
            longest = Math.Max(longest, run);
        }

        Assert.InRange(longest, 1, 14);
    }

    /// <summary>
    /// When a hostile reacts, from its first sight at tick 1000: one that
    /// flees, at once; one that hesitates, from tick 1120 (two seconds) on;
    /// one that ignores the bomb, never.
    /// </summary>
    /// <param name="reaction">Its reaction.</param>
    /// <param name="now">The tick now.</param>
    /// <param name="expected">Whether it reacts.</param>
    [Theory(DisplayName = "Flee at once, hesitate two seconds, ignore always")]
    [InlineData(TractorBombHostileReaction.Flee, 1000, true)]
    [InlineData(TractorBombHostileReaction.Flee, 5000, true)]
    [InlineData(TractorBombHostileReaction.Hesitate, 1000, false)]
    [InlineData(TractorBombHostileReaction.Hesitate, 1119, false)]
    [InlineData(TractorBombHostileReaction.Hesitate, 1120, true)]
    [InlineData(TractorBombHostileReaction.Hesitate, 1500, true)]
    [InlineData(TractorBombHostileReaction.Ignore, 1000, false)]
    [InlineData(TractorBombHostileReaction.Ignore, 1000000, false)]
    public void ReactsNow_should_time_each_reaction(
        TractorBombHostileReaction reaction, int now, bool expected)
    {
        Assert.Equal(120, TractorBombHostiles.HesitationTicks);
        Assert.Equal(expected,
            TractorBombHostiles.ReactsNow(reaction, 1000, now));
    }

    /// <summary>
    /// Ticks near the ends of the range do not overflow the hesitation
    /// sum: a hostile seen at the very last ticks still hesitates.
    /// </summary>
    [Fact(DisplayName = "Hesitation survives extreme ticks")]
    public void ReactsNow_should_not_overflow()
    {
        Assert.False(TractorBombHostiles.ReactsNow(
            TractorBombHostileReaction.Hesitate, int.MaxValue - 10,
            int.MaxValue));
        Assert.True(TractorBombHostiles.ReactsNow(
            TractorBombHostileReaction.Hesitate, int.MinValue, int.MaxValue));
    }

    /// <summary>
    /// A reaction from a save that none has becomes Flee, as everyone
    /// else does; the saved numbers of the three never change.
    /// </summary>
    [Fact(DisplayName = "Unknown reactions from a save flee")]
    public void Sanitize_should_flee_on_unknown_values()
    {
        Assert.Equal(TractorBombHostileReaction.Flee,
            TractorBombHostiles.Sanitize((TractorBombHostileReaction)9));
        Assert.Equal(TractorBombHostileReaction.Ignore,
            TractorBombHostiles.Sanitize(TractorBombHostileReaction.Ignore));
        Assert.True(TractorBombHostiles.ReactsNow(
            (TractorBombHostileReaction)9, 0, 0));
        Assert.Equal(0, (int)TractorBombHostileReaction.Flee);
        Assert.Equal(1, (int)TractorBombHostileReaction.Hesitate);
        Assert.Equal(2, (int)TractorBombHostileReaction.Ignore);
    }
}
