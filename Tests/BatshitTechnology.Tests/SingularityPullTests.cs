using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Ergonomic Singularity Grenade's pull arithmetic in
/// <see cref="SingularityPull"/>.
/// </summary>
public sealed class SingularityPullTests
{
    /// <summary>The pull starts slow, ends fast, and only ever speeds up.</summary>
    [Fact(DisplayName = "Pull rate accelerates from start to end")]
    public void Rate_should_accelerate()
    {
        Assert.Equal(0.6, SingularityPull.Rate(0, 0.6, 12), 10);
        Assert.Equal(12d, SingularityPull.Rate(1, 0.6, 12), 10);
        double[] rates = Enumerable.Range(0, 101)
            .Select(i => SingularityPull.Rate(i / 100d, 0.6, 12))
            .ToArray();
        Assert.True(rates.Zip(rates.Skip(1), (a, b) => b >= a).All(x => x));
        Assert.True(SingularityPull.Rate(0.5, 0.6, 12) < 6.3, "accelerating, not linear");
    }

    /// <summary>Progress outside [0, 1] is clamped.</summary>
    [Fact(DisplayName = "Pull rate clamps progress")]
    public void Rate_should_clamp_progress()
    {
        Assert.Equal(SingularityPull.Rate(0, 1, 9), SingularityPull.Rate(-3, 1, 9));
        Assert.Equal(SingularityPull.Rate(1, 1, 9), SingularityPull.Rate(7, 1, 9));
    }

    /// <summary>
    /// With the shipped settings (0.6 to 12 cells per second over five
    /// seconds), an unblocked thing at the edge of the 10-tile radius
    /// reaches the centre.
    /// </summary>
    [Fact(DisplayName = "Default pull reaches the centre from the edge")]
    public void TotalCells_should_cover_the_radius()
    {
        double total = SingularityPull.TotalCells(5, 0.6, 12);
        Assert.Equal(5 * (0.6 + (11.4 / 3)), total, 10);
        Assert.True(total >= 10, $"{total} cells");
    }

    /// <summary>
    /// Every suggested step brings a thing at least one cell closer to the
    /// centre (Chebyshev distance), and the first takes both axes.
    /// </summary>
    [Fact(DisplayName = "Steps always move closer to the centre")]
    public void StepsToward_should_close_distance()
    {
        for (int x = -10; x <= 10; x++)
        {
            for (int z = -10; z <= 10; z++)
            {
                int before = Math.Max(Math.Abs(x), Math.Abs(z));
                var steps = SingularityPull.StepsToward(x, z, 0, 0);
                if (before == 0)
                {
                    Assert.Empty(steps);
                    continue;
                }

                Assert.Equal((Math.Sign(-x), Math.Sign(-z)), steps[0]);
                Assert.Equal(before - 1,
                    Math.Max(Math.Abs(x + steps[0].Dx), Math.Abs(z + steps[0].Dz)));
                foreach (var (dx, dz) in steps)
                {
                    int after = Math.Max(Math.Abs(x + dx), Math.Abs(z + dz));
                    Assert.True(after <= before, $"({x},{z}) step ({dx},{dz})");
                    Assert.True(Math.Abs(x + dx) + Math.Abs(z + dz) < Math.Abs(x) + Math.Abs(z));
                }
            }
        }
    }

    /// <summary>
    /// A diagonal approach offers the two straight steps as fallbacks,
    /// larger gap first; a straight approach offers just the one.
    /// </summary>
    [Fact(DisplayName = "Fallback steps go along each axis, larger gap first")]
    public void StepsToward_should_offer_axis_fallbacks()
    {
        Assert.Equal(new[] { (-1, -1), (-1, 0), (0, -1) }, SingularityPull.StepsToward(5, 2, 0, 0));
        Assert.Equal(new[] { (1, 1), (0, 1), (1, 0) }, SingularityPull.StepsToward(-2, -7, 0, 0));
        Assert.Equal(new[] { (0, -1) }, SingularityPull.StepsToward(0, 4, 0, 0));
    }
}
