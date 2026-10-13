using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the self-destruct aftermath arithmetic in
/// <see cref="BlastSchedule"/>.
/// </summary>
public sealed class BlastScheduleTests
{
    /// <summary>
    /// The wave front matches RimWorld's explosion timing: 1.5 ticks per
    /// tile at speed 1, so a 75-tile blast takes 112.5 ticks.
    /// </summary>
    [Fact(DisplayName = "Wave front follows RimWorld's explosion timing")]
    public void WaveFront_should_match_vanilla_timing()
    {
        Assert.Equal(0d, BlastSchedule.WaveFront(0, 1));
        Assert.Equal(10d, BlastSchedule.WaveFront(15, 1), 10);
        Assert.Equal(20d, BlastSchedule.WaveFront(15, 2), 10);
        Assert.Equal(113, BlastSchedule.WaveTicks(75, 1));
    }

    /// <summary>Before the explosion starts, the wave has gone nowhere.</summary>
    [Fact(DisplayName = "Wave front is zero before the blast")]
    public void WaveFront_should_be_zero_for_negative_ticks()
    {
        Assert.Equal(0d, BlastSchedule.WaveFront(-5, 1));
    }

    /// <summary>A non-positive speed is a configuration error.</summary>
    [Fact(DisplayName = "Wave timing rejects non-positive speeds")]
    public void Wave_should_reject_bad_speed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BlastSchedule.WaveFront(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BlastSchedule.WaveTicks(10, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BlastSchedule.WaveTicks(-1, 1));
    }

    /// <summary>
    /// A rate that does not divide the tick rate still delivers exactly
    /// the right number of events per second, with no drift.
    /// </summary>
    [Theory(DisplayName = "Per-second rates deliver exact counts")]
    [InlineData(14, 1, 14)]
    [InlineData(7.5, 2, 15)]
    [InlineData(60, 3, 180)]
    [InlineData(0, 5, 0)]
    public void DueThisTick_should_not_drift(double perSecond, int seconds, int expected)
    {
        double accumulator = 0;
        int total = Enumerable.Range(0, seconds * 60)
            .Sum(_ => BlastSchedule.DueThisTick(ref accumulator, perSecond));
        Assert.Equal(expected, total);
    }

    /// <summary>A negative rate produces nothing rather than going backwards.</summary>
    [Fact(DisplayName = "Negative rates produce no events")]
    public void DueThisTick_should_ignore_negative_rates()
    {
        double accumulator = 0;
        Assert.Equal(0, BlastSchedule.DueThisTick(ref accumulator, -10));
        Assert.Equal(0d, accumulator);
    }

    /// <summary>Every sampled point lies inside the annulus.</summary>
    [Fact(DisplayName = "Annulus points stay between the radii")]
    public void PointInAnnulus_should_stay_in_bounds()
    {
        var random = new Random(1234);
        for (int i = 0; i < 5000; i++)
        {
            var (x, z) = BlastSchedule.PointInAnnulus(
                40, 0.25, random.NextDouble(), random.NextDouble());
            double distance = Math.Sqrt((x * x) + (z * z));
            Assert.InRange(distance, 10 - 1e-9, 40 + 1e-9);
        }
    }

    /// <summary>
    /// Points spread evenly over area, not bunched at the centre: half the
    /// disc's area lies beyond 1/sqrt(2) of the radius, so about half the
    /// points should too.
    /// </summary>
    [Fact(DisplayName = "Annulus points spread evenly over area")]
    public void PointInAnnulus_should_be_uniform_over_area()
    {
        var random = new Random(99);
        const int n = 20000;
        int outer = 0;
        for (int i = 0; i < n; i++)
        {
            var (x, z) = BlastSchedule.PointInAnnulus(
                10, 0, random.NextDouble(), random.NextDouble());
            if (Math.Sqrt((x * x) + (z * z)) > 10 / Math.Sqrt(2))
            {
                outer++;
            }
        }

        Assert.InRange(outer / (double)n, 0.48, 0.52);
    }

    /// <summary>Out-of-range samples are clamped, not extrapolated.</summary>
    [Fact(DisplayName = "Annulus sampling clamps its inputs")]
    public void PointInAnnulus_should_clamp_inputs()
    {
        var (x, z) = BlastSchedule.PointInAnnulus(10, 2, 5, -1);
        Assert.Equal(10d, x, 10);
        Assert.Equal(0d, z, 10);
    }
}
