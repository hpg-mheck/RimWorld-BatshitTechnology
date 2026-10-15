using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the arithmetic of running from a Tractor Bomb in
/// <see cref="TractorBombFlee"/> (docs/TRACTOR-BOMB.md, "Everyone Else
/// Runs"): the flee distance follows the pull radius set, pawns inside
/// the blast radius freeze, pawns inside a started pull are left to it,
/// destinations lie clear of the flee distance, and a frozen pawn's line
/// is stable and spread over all twenty.
/// </summary>
public sealed class TractorBombFleeTests
{
    /// <summary>
    /// The spec's numbers: pull radius plus 8, so 30.5 tiles at the
    /// default (Neighborly, 22.5 since the pulls were raised by half), 17
    /// for Intimate and 41.75 for Popular, whatever the blast; never less
    /// than a tile past the blast.
    /// </summary>
    /// <param name="pull">The pull radius, in tiles.</param>
    /// <param name="blast">The blast radius, in tiles.</param>
    /// <param name="expected">The flee distance, in tiles.</param>
    [Theory(DisplayName = "Flee distance is the pull plus 8")]
    [InlineData(22.5, 7.5, 30.5)]
    [InlineData(9, 3, 17)]
    [InlineData(9, 12, 17)]
    [InlineData(33.75, 12, 41.75)]
    [InlineData(33.75, 3, 41.75)]
    [InlineData(22.5, 12, 30.5)]
    public void FleeDistance_should_be_pull_plus_margin(
        double pull, double blast, double expected)
    {
        Assert.Equal(8, TractorBombFlee.FleeMargin);
        Assert.Equal(expected, TractorBombFlee.FleeDistance(pull, blast));
    }

    /// <summary>
    /// Radii outside the stops' range, or not numbers at all, are clamped,
    /// so the distance always makes sense and always clears the blast.
    /// </summary>
    /// <param name="pull">The pull radius asked for.</param>
    /// <param name="blast">The blast radius asked for.</param>
    /// <param name="expected">The flee distance, in tiles.</param>
    [Theory(DisplayName = "Flee distance clamps nonsense radii")]
    [InlineData(-4, 5, 17)]
    [InlineData(500, 5, 41.75)]
    [InlineData(double.NaN, double.NaN, 30.5)]
    [InlineData(10.4, 4.6, 18.4)]
    public void FleeDistance_should_clamp_radii(
        double pull, double blast, double expected)
    {
        double distance = TractorBombFlee.FleeDistance(pull, blast);
        Assert.Equal(expected, distance, 9);
        Assert.True(distance > TractorBombYield.ClampBlast(blast));
    }

    /// <summary>
    /// The too-close test at the default yield (pull 22.5, blast 7.5, flee
    /// 30.5), before the pull starts: frozen within 7.5 tiles, fleeing out
    /// to 30.5 (30 included, as RimWorld's InHorDistOf includes the edge),
    /// unconcerned beyond.
    /// </summary>
    /// <param name="dx">The offset along x.</param>
    /// <param name="dz">The offset along z.</param>
    /// <param name="expected">The reaction.</param>
    [Theory(DisplayName = "Too close freezes, in range flees, beyond ignores")]
    [InlineData(0, 0, TractorBombReaction.Freeze)]
    [InlineData(3, 4, TractorBombReaction.Freeze)]
    [InlineData(7, 0, TractorBombReaction.Freeze)]
    [InlineData(5, 5, TractorBombReaction.Freeze)]
    [InlineData(8, 0, TractorBombReaction.Flee)]
    [InlineData(6, 5, TractorBombReaction.Flee)]
    [InlineData(-12, 0, TractorBombReaction.Flee)]
    [InlineData(30, 0, TractorBombReaction.Flee)]
    [InlineData(-21, 21, TractorBombReaction.Flee)]
    [InlineData(31, 0, TractorBombReaction.None)]
    [InlineData(22, 22, TractorBombReaction.None)]
    [InlineData(-200, 300, TractorBombReaction.None)]
    public void React_should_sort_pawns_by_distance(
        int dx, int dz, TractorBombReaction expected)
    {
        Assert.Equal(
            expected,
            TractorBombFlee.React(dx, dz, 22.5, 7.5, pulling: false));
    }

    /// <summary>
    /// Once the pull starts, everyone inside it belongs to the implosion
    /// (neither fleeing nor frozen); those outside the pull still flee,
    /// and with a blast wider than the pull (Intimate and Block Party),
    /// those between the two still freeze.
    /// </summary>
    [Fact(DisplayName = "A started pull catches everyone inside it")]
    public void React_should_leave_the_pulled_to_the_implosion()
    {
        Assert.Equal(TractorBombReaction.Caught,
            TractorBombFlee.React(3, 0, 22.5, 7.5, pulling: true));
        Assert.Equal(TractorBombReaction.Caught,
            TractorBombFlee.React(12, 18, 22.5, 7.5, pulling: true));
        Assert.Equal(TractorBombReaction.Flee,
            TractorBombFlee.React(23, 0, 22.5, 7.5, pulling: true));
        Assert.Equal(TractorBombReaction.None,
            TractorBombFlee.React(31, 0, 22.5, 7.5, pulling: true));

        // Pull 9, blast 12: flee distance 17.
        Assert.Equal(TractorBombReaction.Caught,
            TractorBombFlee.React(0, 9, 9, 12, pulling: true));
        Assert.Equal(TractorBombReaction.Freeze,
            TractorBombFlee.React(0, 10, 9, 12, pulling: true));
        Assert.Equal(TractorBombReaction.Flee,
            TractorBombFlee.React(0, 13, 9, 12, pulling: true));
    }

    /// <summary>
    /// The reaction follows the settings: a Popular beam sends pawns at
    /// 41 tiles running, an Intimate one leaves them be at 18, and a
    /// fractional edge counts (41.75 includes 41, not 42).
    /// </summary>
    [Fact(DisplayName = "Reactions follow the settings")]
    public void React_should_follow_the_settings()
    {
        Assert.Equal(TractorBombReaction.Flee,
            TractorBombFlee.React(41, 0, 33.75, 3, pulling: false));
        Assert.Equal(TractorBombReaction.None,
            TractorBombFlee.React(42, 0, 33.75, 3, pulling: false));
        Assert.Equal(TractorBombReaction.Flee,
            TractorBombFlee.React(17, 0, 9, 3, pulling: false));
        Assert.Equal(TractorBombReaction.None,
            TractorBombFlee.React(18, 0, 9, 3, pulling: false));
        Assert.Equal(TractorBombReaction.Freeze,
            TractorBombFlee.React(0, 12, 9, 12, pulling: false));
    }

    /// <summary>
    /// A flee search may visit four and a half times the circle a pawn
    /// must leave at the longest flee distance (Popular's 41.75 tiles and
    /// the clearance), as the old 12,000 did for the sliders' 28 tiles, so
    /// only a hemmed-in pawn runs out of cells; and it stays a bounded
    /// search.
    /// </summary>
    [Fact(DisplayName = "The flee search covers the longest flee")]
    public void SearchCells_should_cover_the_longest_flee()
    {
        double longest = TractorBombFlee.FleeDistance(
            TractorBombYield.MaxPull, TractorBombYield.MaxBlast)
            + TractorBombFlee.DestinationClearance;
        double circle = System.Math.PI * longest * longest;
        Assert.Equal(42.75, longest);
        Assert.True(TractorBombFlee.SearchCells >= 4.5 * circle,
            $"{TractorBombFlee.SearchCells} cells");
        Assert.InRange(TractorBombFlee.SearchCells, 12000, 40000);
    }

    /// <summary>
    /// A destination must lie more than a tile beyond the flee distance,
    /// so an arrival is clearly out; nonsense distances make no cell clear
    /// (NaN) or count as none (negative).
    /// </summary>
    [Fact(DisplayName = "Destinations lie clear of the flee distance")]
    public void IsClear_should_need_the_clearance()
    {
        Assert.Equal(1, TractorBombFlee.DestinationClearance);
        Assert.False(TractorBombFlee.IsClear(18, 0, 18));
        Assert.False(TractorBombFlee.IsClear(19, 0, 18));
        Assert.True(TractorBombFlee.IsClear(20, 0, 18));
        Assert.True(TractorBombFlee.IsClear(-14, -14, 18));
        Assert.False(TractorBombFlee.IsClear(13, 13, 18));
        Assert.False(TractorBombFlee.IsClear(10, 0, 9));
        Assert.True(TractorBombFlee.IsClear(11, 0, 9));
        Assert.False(TractorBombFlee.IsClear(500, 500, double.NaN));
        Assert.True(TractorBombFlee.IsClear(2, 0, -5));
        Assert.False(TractorBombFlee.IsClear(1, 0, -5));
    }

    /// <summary>
    /// A frozen pawn's line is always in range and the same every time it
    /// is asked for, for the same pawn and bomb.
    /// </summary>
    [Fact(DisplayName = "A frozen pawn keeps the same line")]
    public void LineIndex_should_be_stable_and_in_range()
    {
        for (int pawn = -50; pawn < 500; pawn += 7)
        {
            for (int bomb = 0; bomb < 3000; bomb += 499)
            {
                int index = TractorBombFlee.LineIndex(pawn, bomb, 20);
                Assert.InRange(index, 0, 19);
                Assert.Equal(index, TractorBombFlee.LineIndex(pawn, bomb, 20));
            }
        }

        Assert.Equal(0, TractorBombFlee.LineIndex(12345, 678, 1));
        Assert.Equal(0, TractorBombFlee.LineIndex(int.MinValue, int.MaxValue, 1));
    }

    /// <summary>
    /// Across a thousand pawns facing one bomb (ids as RimWorld hands them
    /// out, one after another), every one of the twenty lines comes up,
    /// none more than twice as often as fair, and neighbours mostly differ.
    /// </summary>
    [Fact(DisplayName = "Lines are spread over all twenty")]
    public void LineIndex_should_spread_over_every_line()
    {
        int[] counts = new int[20];
        int sameAsNeighbour = 0;
        int previous = -1;
        for (int pawn = 1000; pawn < 2000; pawn++)
        {
            int index = TractorBombFlee.LineIndex(pawn, 4242, 20);
            counts[index]++;
            sameAsNeighbour += index == previous ? 1 : 0;
            previous = index;
        }

        Assert.All(counts, c => Assert.InRange(c, 20, 100));
        Assert.True(sameAsNeighbour < 150, $"{sameAsNeighbour} repeats");
        Assert.True(Enumerable.Range(0, 50)
            .Select(bomb => TractorBombFlee.LineIndex(77, bomb, 20))
            .Distinct().Count() > 10, "one pawn, many bombs");
    }

    /// <summary>
    /// Asking for a line out of none is a programming error.
    /// </summary>
    /// <param name="count">The line count.</param>
    [Theory(DisplayName = "No lines is an error")]
    [InlineData(0)]
    [InlineData(-3)]
    public void LineIndex_should_reject_no_lines(int count)
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => TractorBombFlee.LineIndex(1, 2, count));
    }

    /// <summary>
    /// Lines become reports as vanilla's read: trimmed, with a full stop
    /// unless they already close with a mark of their own.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="expected">The report.</param>
    [Theory(DisplayName = "A line becomes a job report")]
    [InlineData("making peace with their impending doom",
        "making peace with their impending doom.")]
    [InlineData("  hoping it's a dud ", "hoping it's a dud.")]
    [InlineData("standing very, very still.", "standing very, very still.")]
    [InlineData("¡esperando que falle!", "¡esperando que falle!")]
    [InlineData("waiting…", "waiting…")]
    [InlineData("why?", "why?")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void AsReport_should_close_with_a_full_stop(
        string? line, string expected)
    {
        Assert.Equal(expected, TractorBombFlee.AsReport(line));
    }
}
