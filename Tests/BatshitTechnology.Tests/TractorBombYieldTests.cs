using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Tractor Bomb's yield in <see cref="TractorBombYield"/>:
/// the defaults and ranges from docs/TRACTOR-BOMB.md, the Dial-A-Yield
/// clamps, and the sliders' whole-tile stops.
/// </summary>
public sealed class TractorBombYieldTests
{
    /// <summary>
    /// The numbers the operator specified: pull 3 to 20 tiles, blast 2 to
    /// 10, defaults 10 and 5.
    /// </summary>
    [Fact(DisplayName = "Yield ranges and defaults match the spec")]
    public void Constants_should_match_spec()
    {
        Assert.Equal(3, TractorBombYield.MinPull);
        Assert.Equal(20, TractorBombYield.MaxPull);
        Assert.Equal(10, TractorBombYield.DefaultPull);
        Assert.Equal(2, TractorBombYield.MinBlast);
        Assert.Equal(10, TractorBombYield.MaxBlast);
        Assert.Equal(5, TractorBombYield.DefaultBlast);
    }

    /// <summary>
    /// A pull radius is clamped to its range and rounded to a whole tile;
    /// NaN becomes the default.
    /// </summary>
    /// <param name="asked">The radius asked for.</param>
    /// <param name="expected">The radius the bomb uses.</param>
    [Theory(DisplayName = "Pull clamps to 3-20 whole tiles")]
    [InlineData(-5, 3)]
    [InlineData(0, 3)]
    [InlineData(2.9, 3)]
    [InlineData(3, 3)]
    [InlineData(7.4, 7)]
    [InlineData(7.5, 8)]
    [InlineData(10, 10)]
    [InlineData(20, 20)]
    [InlineData(20.4, 20)]
    [InlineData(1e9, 20)]
    [InlineData(double.PositiveInfinity, 20)]
    [InlineData(double.NegativeInfinity, 3)]
    [InlineData(double.NaN, 10)]
    public void ClampPull_should_keep_whole_tiles_in_range(
        double asked, int expected)
    {
        Assert.Equal(expected, TractorBombYield.ClampPull(asked));
    }

    /// <summary>
    /// A blast radius is clamped to its range and rounded to a whole tile;
    /// NaN becomes the default.
    /// </summary>
    /// <param name="asked">The radius asked for.</param>
    /// <param name="expected">The radius the bomb uses.</param>
    [Theory(DisplayName = "Blast clamps to 2-10 whole tiles")]
    [InlineData(-1, 2)]
    [InlineData(1.9, 2)]
    [InlineData(2, 2)]
    [InlineData(4.49, 4)]
    [InlineData(4.5, 5)]
    [InlineData(10, 10)]
    [InlineData(11, 10)]
    [InlineData(double.PositiveInfinity, 10)]
    [InlineData(double.NaN, 5)]
    public void ClampBlast_should_keep_whole_tiles_in_range(
        double asked, int expected)
    {
        Assert.Equal(expected, TractorBombYield.ClampBlast(asked));
    }

    /// <summary>
    /// Before Dial-A-Yield is researched a bomb uses the defaults, whatever
    /// was dialled; afterwards it uses the dialled radii, clamped.
    /// </summary>
    [Fact(DisplayName = "Dialled radii apply only once researched")]
    public void Effective_should_ignore_dial_until_researched()
    {
        Assert.Equal((10, 5), TractorBombYield.Effective(4, 3, false));
        Assert.Equal((10, 5), TractorBombYield.Effective(99, -1, false));
        Assert.Equal((4, 3), TractorBombYield.Effective(4, 3, true));
        Assert.Equal((20, 2), TractorBombYield.Effective(99, -1, true));
        Assert.Equal((10, 5), TractorBombYield.Effective(
            double.NaN, double.NaN, true));
    }

    /// <summary>
    /// Each slider stops on every whole tile between its ends, and nowhere
    /// else: a position maps to a radius and back again exactly.
    /// </summary>
    [Fact(DisplayName = "Sliders stop on every whole tile and round-trip")]
    public void Sliders_should_round_trip_every_whole_tile()
    {
        int pullSteps = TractorBombYield.Steps(
            TractorBombYield.MinPull, TractorBombYield.MaxPull);
        int blastSteps = TractorBombYield.Steps(
            TractorBombYield.MinBlast, TractorBombYield.MaxBlast);
        Assert.Equal(17, pullSteps);
        Assert.Equal(8, blastSteps);

        var pulls = Enumerable.Range(0, pullSteps + 1)
            .Select(i => TractorBombYield.PullAt(i / (double)pullSteps))
            .ToList();
        Assert.Equal(Enumerable.Range(3, 18), pulls);
        Assert.All(pulls, p => Assert.Equal(
            p, TractorBombYield.PullAt(TractorBombYield.PullFraction(p))));

        var blasts = Enumerable.Range(0, blastSteps + 1)
            .Select(i => TractorBombYield.BlastAt(i / (double)blastSteps))
            .ToList();
        Assert.Equal(Enumerable.Range(2, 9), blasts);
        Assert.All(blasts, b => Assert.Equal(
            b, TractorBombYield.BlastAt(TractorBombYield.BlastFraction(b))));
    }

    /// <summary>
    /// Slider positions outside [0, 1], or not a number, land on an end,
    /// and the defaults sit where the sliders say they do.
    /// </summary>
    [Fact(DisplayName = "Slider positions are clamped; defaults sit inside")]
    public void Sliders_should_clamp_positions()
    {
        Assert.Equal(3, TractorBombYield.PullAt(-1));
        Assert.Equal(3, TractorBombYield.PullAt(double.NaN));
        Assert.Equal(20, TractorBombYield.PullAt(2));
        Assert.Equal(2, TractorBombYield.BlastAt(-0.5));
        Assert.Equal(10, TractorBombYield.BlastAt(double.PositiveInfinity));
        Assert.Equal(7 / 17d, TractorBombYield.PullFraction(10), 12);
        Assert.Equal(3 / 8d, TractorBombYield.BlastFraction(5), 12);
        Assert.InRange(TractorBombYield.PullFraction(double.NaN), 0, 1);
    }

    /// <summary>A slider always has at least one step.</summary>
    [Fact(DisplayName = "A degenerate slider still has a step")]
    public void Steps_should_never_be_zero()
    {
        Assert.Equal(1, TractorBombYield.Steps(5, 5));
        Assert.Equal(1, TractorBombYield.Steps(9, 2));
        Assert.Equal(1, TractorBombYield.Steps(0, 0));
    }
}
