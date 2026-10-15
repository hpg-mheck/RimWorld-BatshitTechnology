using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Tractor Bomb's death in <see cref="DeathVortexMath"/>: its
/// parts burst out, hang, and are sucked back into the vortex, which
/// sputters, and nothing ever flies past a part's own reach.
/// </summary>
public sealed class DeathVortexMathTests
{
    /// <summary>The distance of an offset from the vortex.</summary>
    /// <param name="p">The part's offset and scale.</param>
    /// <returns>The distance, in tiles.</returns>
    private static double Distance((double X, double Z, double Scale) p) =>
        Math.Sqrt((p.X * p.X) + (p.Z * p.Z));

    /// <summary>
    /// A part starts at the wreck, reaches its full distance as the burst
    /// ends, hangs there, and ends at the vortex, small.
    /// </summary>
    [Fact(DisplayName = "Parts burst out, hang, and end in the vortex")]
    public void PartAt_should_follow_the_timeline()
    {
        const double Reach = 2;
        Assert.Equal(0, Distance(DeathVortexMath.PartAt(Reach, 1, 0)), 9);
        Assert.Equal(Reach, Distance(DeathVortexMath.PartAt(
            Reach, 1, DeathVortexMath.FlingTicks)), 9);
        Assert.Equal(Reach, Distance(DeathVortexMath.PartAt(
            Reach, 1, DeathVortexMath.FlingTicks
                + DeathVortexMath.HangTicks)), 9);
        var end = DeathVortexMath.PartAt(
            Reach, 1, DeathVortexMath.TotalTicks);
        Assert.Equal(0, Distance(end), 9);
        Assert.Equal(DeathVortexMath.FinalScale, end.Scale, 9);
    }

    /// <summary>
    /// A part never flies past its own reach (nothing else is touched), it
    /// only moves outward while bursting, and only inward while sucked.
    /// </summary>
    [Fact(DisplayName = "Parts stay within reach and move one way per phase")]
    public void PartAt_should_be_monotonic_per_phase()
    {
        const double Reach = 1.7;
        double previous = 0;
        for (int t = 0; t <= DeathVortexMath.TotalTicks; t++)
        {
            double d = Distance(DeathVortexMath.PartAt(Reach, 0.3, t));
            Assert.InRange(d, 0, Reach + 1e-9);
            if (t <= DeathVortexMath.FlingTicks)
            {
                Assert.True(d >= previous - 1e-9, $"tick {t} moved in");
            }
            else if (t > DeathVortexMath.FlingTicks
                + DeathVortexMath.HangTicks)
            {
                Assert.True(d <= previous + 1e-9, $"tick {t} moved out");
            }

            previous = d;
        }
    }

    /// <summary>
    /// Out-of-range input is clamped: ticks before the death or after the
    /// pop, and a negative or NaN reach or angle.
    /// </summary>
    [Fact(DisplayName = "Part positions survive nonsense input")]
    public void PartAt_should_clamp_input()
    {
        Assert.Equal(0, Distance(DeathVortexMath.PartAt(2, 0, -100)), 9);
        Assert.Equal(0, Distance(DeathVortexMath.PartAt(
            2, 0, DeathVortexMath.TotalTicks + 1000)), 9);
        Assert.Equal(0, Distance(DeathVortexMath.PartAt(-2, 0, 20)), 9);
        Assert.Equal(0, Distance(DeathVortexMath.PartAt(double.NaN, 0, 20)), 9);
        var p = DeathVortexMath.PartAt(2, double.NaN, DeathVortexMath.FlingTicks);
        Assert.Equal(2, p.X, 9);
        Assert.Equal(0, p.Z, 9);
    }

    /// <summary>
    /// The suck spins a part round as it comes in, so it spirals rather
    /// than sliding straight back.
    /// </summary>
    [Fact(DisplayName = "Parts spiral in")]
    public void PartAt_should_spiral()
    {
        int midSuck = DeathVortexMath.FlingTicks + DeathVortexMath.HangTicks
            + (DeathVortexMath.SuckTicks / 2);
        var hang = DeathVortexMath.PartAt(2, 0, DeathVortexMath.FlingTicks);
        var mid = DeathVortexMath.PartAt(2, 0, midSuck);
        double hangAngle = Math.Atan2(hang.Z, hang.X);
        double midAngle = Math.Atan2(mid.Z, mid.X);
        Assert.True(Math.Abs(midAngle - hangAngle) > 0.5,
            $"turned only {midAngle - hangAngle:F2} rad");
    }

    /// <summary>
    /// The vortex is closed while the parts burst out, then sputters within
    /// its bounds, flickering rather than holding steady.
    /// </summary>
    [Fact(DisplayName = "Vortex opens after the burst and sputters")]
    public void VortexSize_should_sputter()
    {
        Assert.Equal(0, DeathVortexMath.VortexSize(0, 7));
        Assert.Equal(0, DeathVortexMath.VortexSize(
            DeathVortexMath.FlingTicks, 7));
        var sizes = Enumerable.Range(
                DeathVortexMath.FlingTicks + DeathVortexMath.HangTicks,
                DeathVortexMath.SuckTicks)
            .Select(t => DeathVortexMath.VortexSize(t, 7))
            .ToList();
        Assert.All(sizes, s => Assert.InRange(s, 0.55, 1.15));
        Assert.True(sizes.Distinct().Count() > 10, "it should flicker");
        Assert.NotEqual(
            sizes, Enumerable.Range(
                    DeathVortexMath.FlingTicks + DeathVortexMath.HangTicks,
                    DeathVortexMath.SuckTicks)
                .Select(t => DeathVortexMath.VortexSize(t, 8)));
    }

    /// <summary>
    /// Parts spread evenly round the wreck, each nudged by at most a
    /// quarter of its share either way, and a nonsense count is survived.
    /// </summary>
    [Fact(DisplayName = "Parts burst out evenly spread")]
    public void PartAngle_should_spread_parts()
    {
        double share = 2 * Math.PI / 6;
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(i * share, DeathVortexMath.PartAngle(i, 6, 0.5), 9);
            Assert.InRange(
                DeathVortexMath.PartAngle(i, 6, 0), (i - 0.25) * share - 1e-9,
                (i + 0.25) * share + 1e-9);
            Assert.InRange(
                DeathVortexMath.PartAngle(i, 6, 0.999), (i - 0.25) * share,
                (i + 0.25) * share);
        }

        // No parts counts as one, and NaN jitter as 0: a quarter turn back.
        Assert.Equal(-Math.PI / 2,
            DeathVortexMath.PartAngle(0, 0, double.NaN), 9);
    }
}
