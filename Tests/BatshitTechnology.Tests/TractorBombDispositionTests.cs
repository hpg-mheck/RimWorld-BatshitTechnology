using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Tractor Bomb's dispositions in
/// <see cref="TractorBombApproach"/> (roadmap, "Decided after the core
/// build"): Cautious stops 15 tiles out, Moderate (the default) at 15 or
/// one tile inside the dialled pull, whichever is nearer, and Nosy a few
/// tiles out; the button cycles through all three; and nonsense from a
/// save becomes something sensible.
/// </summary>
public sealed class TractorBombDispositionTests
{
    /// <summary>
    /// The spec's stops at the default yield (pull 10): 15, 9 and 3.
    /// </summary>
    /// <param name="disposition">The disposition.</param>
    /// <param name="expected">The stop, in tiles.</param>
    [Theory(DisplayName = "Each disposition stops at its distance")]
    [InlineData(TractorBombDisposition.Cautious, 15.0)]
    [InlineData(TractorBombDisposition.Moderate, 9.0)]
    [InlineData(TractorBombDisposition.Nosy, 3.0)]
    public void StopDistance_should_match_the_spec(
        TractorBombDisposition disposition, double expected)
    {
        Assert.Equal(expected, TractorBombApproach.StopDistance(
            disposition, 15, TractorBombYield.DefaultPull));
    }

    /// <summary>
    /// Dial-A-Yield moves the Moderate stop with the pull, never past
    /// the cautious 15; Cautious never moves; Nosy stays at 3 unless a
    /// tiny pull brings Moderate nearer still.
    /// </summary>
    /// <param name="pull">The dialled pull radius.</param>
    /// <param name="cautious">The cautious stop expected.</param>
    /// <param name="moderate">The moderate stop expected.</param>
    /// <param name="nosy">The nosy stop expected.</param>
    [Theory(DisplayName = "Dial-A-Yield moves the Moderate stop")]
    [InlineData(3.0, 15.0, 2.0, 2.0)]
    [InlineData(4.0, 15.0, 3.0, 3.0)]
    [InlineData(5.0, 15.0, 4.0, 3.0)]
    [InlineData(10.0, 15.0, 9.0, 3.0)]
    [InlineData(16.0, 15.0, 15.0, 3.0)]
    [InlineData(20.0, 15.0, 15.0, 3.0)]
    public void StopDistance_should_follow_the_pull(
        double pull, double cautious, double moderate, double nosy)
    {
        Assert.Equal(cautious, TractorBombApproach.StopDistance(
            TractorBombDisposition.Cautious, 15, pull));
        Assert.Equal(moderate, TractorBombApproach.StopDistance(
            TractorBombDisposition.Moderate, 15, pull));
        Assert.Equal(nosy, TractorBombApproach.StopDistance(
            TractorBombDisposition.Nosy, 15, pull));
    }

    /// <summary>
    /// The stops are ordered at every pull: Nosy never farther than
    /// Moderate, Moderate never farther than Cautious, and a target
    /// standing still at a Moderate or Nosy stop is inside the pull.
    /// </summary>
    [Fact(DisplayName = "Nosy <= Moderate <= Cautious, inside the pull")]
    public void StopDistance_should_be_ordered()
    {
        for (int pull = TractorBombYield.MinPull;
            pull <= TractorBombYield.MaxPull; pull++)
        {
            double c = TractorBombApproach.StopDistance(
                TractorBombDisposition.Cautious, 15, pull);
            double m = TractorBombApproach.StopDistance(
                TractorBombDisposition.Moderate, 15, pull);
            double n = TractorBombApproach.StopDistance(
                TractorBombDisposition.Nosy, 15, pull);
            Assert.True(n <= m && m <= c, $"pull {pull}: {n} {m} {c}");
            Assert.True(m < pull && n < pull, $"pull {pull}");
            Assert.True(n > 0, $"pull {pull}");
        }
    }

    /// <summary>
    /// The robot's XML cautious stop is honoured; a missing, zero or
    /// nonsense one counts as 15, and a nonsense pull as the default 10.
    /// </summary>
    /// <param name="cautious">The cautious stop given.</param>
    /// <param name="pull">The pull given.</param>
    /// <param name="expectedCautious">The cautious stop expected.</param>
    /// <param name="expectedModerate">The moderate stop expected.</param>
    [Theory(DisplayName = "Nonsense stops and pulls fall back to the spec")]
    [InlineData(12.0, 10.0, 12.0, 9.0)]
    [InlineData(6.0, 10.0, 6.0, 6.0)]
    [InlineData(0.0, 10.0, 15.0, 9.0)]
    [InlineData(-3.0, 10.0, 15.0, 9.0)]
    [InlineData(double.NaN, 10.0, 15.0, 9.0)]
    [InlineData(15.0, double.NaN, 15.0, 9.0)]
    [InlineData(15.0, 500.0, 15.0, 15.0)]
    public void StopDistance_should_sanitize_inputs(
        double cautious, double pull, double expectedCautious,
        double expectedModerate)
    {
        Assert.Equal(expectedCautious, TractorBombApproach.StopDistance(
            TractorBombDisposition.Cautious, cautious, pull));
        Assert.Equal(expectedModerate, TractorBombApproach.StopDistance(
            TractorBombDisposition.Moderate, cautious, pull));
    }

    /// <summary>
    /// A new robot is Moderate, as proposed; the button steps Cautious,
    /// Moderate, Nosy and round again, visiting every setting.
    /// </summary>
    [Fact(DisplayName = "Moderate by default; the button cycles all three")]
    public void Next_should_cycle_all_three()
    {
        Assert.Equal(TractorBombDisposition.Moderate,
            TractorBombApproach.Default);
        Assert.Equal(TractorBombDisposition.Nosy,
            TractorBombApproach.Next(TractorBombDisposition.Moderate));
        Assert.Equal(TractorBombDisposition.Cautious,
            TractorBombApproach.Next(TractorBombDisposition.Nosy));
        Assert.Equal(TractorBombDisposition.Moderate,
            TractorBombApproach.Next(TractorBombDisposition.Cautious));
        var all = Enum.GetValues(typeof(TractorBombDisposition))
            .Cast<TractorBombDisposition>().ToList();
        var seen = all.Select(TractorBombApproach.Next).Distinct().ToList();
        Assert.Equal(all.Count, seen.Count);
    }

    /// <summary>
    /// A number from a save that no setting has becomes Moderate, and
    /// counts as Moderate everywhere it is used; the saved numbers of the
    /// three settings never change.
    /// </summary>
    [Fact(DisplayName = "Unknown dispositions from a save become Moderate")]
    public void Sanitize_should_default_unknown_values()
    {
        var unknown = (TractorBombDisposition)7;
        Assert.Equal(TractorBombDisposition.Moderate,
            TractorBombApproach.Sanitize(unknown));
        Assert.Equal(TractorBombDisposition.Moderate,
            TractorBombApproach.Sanitize((TractorBombDisposition)(-1)));
        Assert.Equal(9.0, TractorBombApproach.StopDistance(unknown, 15, 10));
        Assert.Equal(TractorBombDisposition.Nosy,
            TractorBombApproach.Next(unknown));
        Assert.Equal(0, (int)TractorBombDisposition.Cautious);
        Assert.Equal(1, (int)TractorBombDisposition.Moderate);
        Assert.Equal(2, (int)TractorBombDisposition.Nosy);
    }
}
