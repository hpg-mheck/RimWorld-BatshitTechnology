using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the shared implosion's arithmetic in <see cref="ImplosionMath"/>,
/// including that the Ergonomic Singularity Grenade, moved onto it, pulls
/// exactly as it did before.
/// </summary>
public sealed class ImplosionMathTests
{
    /// <summary>Progress runs from 0 to 1 over the pull, clamped.</summary>
    [Fact(DisplayName = "Implosion progress runs from 0 to 1, clamped")]
    public void Progress_should_run_from_0_to_1()
    {
        Assert.Equal(0d, ImplosionMath.Progress(0, 300));
        Assert.Equal(0.5, ImplosionMath.Progress(150, 300), 10);
        Assert.Equal(1d, ImplosionMath.Progress(300, 300));
        Assert.Equal(1d, ImplosionMath.Progress(900, 300));
        Assert.Equal(0d, ImplosionMath.Progress(-5, 300));
    }

    /// <summary>A pull with no length is already complete.</summary>
    /// <param name="pullTicks">The pull's length, in ticks.</param>
    [Theory(DisplayName = "Implosion progress is complete with no length")]
    [InlineData(0)]
    [InlineData(-10)]
    public void Progress_should_be_complete_without_a_length(int pullTicks)
    {
        Assert.Equal(1d, ImplosionMath.Progress(0, pullTicks));
    }

    /// <summary>
    /// The grenade's drag rate is unchanged: its old fraction (ticks over
    /// pull ticks, clamped by the rate) gives the same rate as
    /// <see cref="ImplosionMath.Progress"/> at every tick of its pull.
    /// </summary>
    [Fact(DisplayName = "Grenade's drag rate is unchanged by the implosion")]
    public void Progress_should_keep_the_grenade_rate()
    {
        for (int tick = 0; tick <= 400; tick++)
        {
            double progress = ImplosionMath.Progress(tick, 300);
            Assert.Equal(
                SingularityPull.Rate(tick / 300d, 0.6, 12),
                SingularityPull.Rate(progress, 0.6, 12));
        }
    }

    /// <summary>
    /// The reach to rip fixtures loose grows steadily from nothing to the
    /// full radius over the rip time, then holds.
    /// </summary>
    [Fact(DisplayName = "Rip reach grows to the radius, then holds")]
    public void RipReach_should_grow_then_hold()
    {
        Assert.Equal(0d, ImplosionMath.RipReach(0, 120, 10));
        Assert.Equal(5d, ImplosionMath.RipReach(60, 120, 10), 10);
        Assert.Equal(10d, ImplosionMath.RipReach(120, 120, 10), 10);
        Assert.Equal(10d, ImplosionMath.RipReach(1000, 120, 10), 10);
        double[] reach = Enumerable.Range(0, 200)
            .Select(t => ImplosionMath.RipReach(t, 120, 10))
            .ToArray();
        Assert.True(reach.Zip(reach.Skip(1), (a, b) => b >= a).All(x => x));
    }

    /// <summary>
    /// The grenade rips at the same pace as before: its old float formula
    /// (radius times the clamped fraction of the rip time) agrees with
    /// <see cref="ImplosionMath.RipReach"/> to float precision.
    /// </summary>
    [Fact(DisplayName = "Grenade's rip reach is unchanged by the implosion")]
    public void RipReach_should_keep_the_grenade_pace()
    {
        for (int tick = 0; tick <= 200; tick++)
        {
            float old = 10f * Math.Min(1f, tick / 120f);
            float now = (float)ImplosionMath.RipReach(tick, 120, 10);
            Assert.Equal(old, now, 5);
        }
    }

    /// <summary>
    /// No rip time rips the whole radius at once; a radius that is not
    /// positive reaches nowhere.
    /// </summary>
    [Fact(DisplayName = "Rip reach handles no rip time and bad radii")]
    public void RipReach_should_handle_edge_cases()
    {
        Assert.Equal(10d, ImplosionMath.RipReach(0, 0, 10));
        Assert.Equal(10d, ImplosionMath.RipReach(1, -3, 10));
        Assert.Equal(0d, ImplosionMath.RipReach(60, 120, -4));
        Assert.Equal(0d, ImplosionMath.RipReach(60, 120, double.NaN));
    }

    /// <summary>
    /// A gather radius of 0 gathers only the centre (the grenade's old
    /// "at the centre" test); 1 the centre and its four neighbours; 1.5
    /// exactly the 3 by 3 block, the Tractor Bomb's pile; 10 the 317 cells
    /// RimWorld counts within 10 tiles.
    /// </summary>
    /// <param name="radius">The gather radius, in tiles.</param>
    /// <param name="cells">How many cells it should gather.</param>
    [Theory(DisplayName = "Gather radius covers the expected cells")]
    [InlineData(0, 1)]
    [InlineData(1, 5)]
    [InlineData(1.5, 9)]
    [InlineData(10, 317)]
    public void IsGathered_should_cover_the_expected_cells(
        double radius, int cells)
    {
        var gathered = Gathered(12, radius);
        Assert.Equal(cells, gathered.Length);
        if (radius == 1.5)
        {
            Assert.All(gathered, o =>
                Assert.True(Math.Max(Math.Abs(o.X), Math.Abs(o.Z)) <= 1));
        }
    }

    /// <summary>
    /// A negative or NaN gather radius gathers only the centre.
    /// </summary>
    /// <param name="radius">The bad gather radius.</param>
    [Theory(DisplayName = "Bad gather radii gather only the centre")]
    [InlineData(-2)]
    [InlineData(double.NaN)]
    public void IsGathered_should_treat_bad_radii_as_zero(double radius)
    {
        Assert.Equal(new[] { (0, 0) }, Gathered(3, radius));
    }

    /// <summary>
    /// With a gather radius of 0 the steps are exactly
    /// <see cref="SingularityPull.StepsToward"/>'s, from every cell: the
    /// grenade moves things exactly as it did before.
    /// </summary>
    [Fact(DisplayName = "With no gather radius, steps are the grenade's")]
    public void StepsInward_should_match_the_grenade_at_radius_zero()
    {
        foreach (var (x, z) in Offsets(12))
        {
            Assert.Equal(
                SingularityPull.StepsToward(x + 40, z - 7, 40, -7),
                ImplosionMath.StepsInward(x + 40, z - 7, 40, -7, 0));
        }
    }

    /// <summary>
    /// Every step, from every cell and at every gather radius, is one cell
    /// in some direction, appears once, and brings the thing strictly
    /// closer to the centre: nothing is ever shoved away.
    /// </summary>
    [Fact(DisplayName = "Inward steps are single, distinct and closer")]
    public void StepsInward_should_always_close_in()
    {
        foreach (double radius in new[] { 0, 1, 1.5, 2.5, 5 })
        {
            foreach (var (x, z) in Offsets(10))
            {
                var steps = ImplosionMath.StepsInward(x, z, 0, 0, radius);
                Assert.Equal(steps.Count, steps.Distinct().Count());
                foreach (var (dx, dz) in steps)
                {
                    Assert.InRange(dx, -1, 1);
                    Assert.InRange(dz, -1, 1);
                    int before = (x * x) + (z * z);
                    int after = ((x + dx) * (x + dx)) + ((z + dz) * (z + dz));
                    Assert.True(after < before,
                        $"({x},{z}) r {radius} step ({dx},{dz})");
                }
            }
        }
    }

    /// <summary>
    /// A thing waiting just outside a 1.5-tile pile, straight behind a full
    /// cell, may slip diagonally into either corner beside it, clockwise
    /// first; the centre may be anywhere.
    /// </summary>
    [Fact(DisplayName = "A blocked thing may slip sideways into the pile")]
    public void StepsInward_should_offer_slips_into_the_pile()
    {
        Assert.Equal(new[] { (0, -1), (1, -1), (-1, -1) },
            ImplosionMath.StepsInward(50, 72, 50, 70, 1.5));
        Assert.Equal(new[] { (-1, 0), (-1, -1), (-1, 1) },
            ImplosionMath.StepsInward(2, 0, 0, 0, 1.5));
    }

    /// <summary>
    /// Slips are offered only where they land in the pile: not from far
    /// away, not from a corner (whose own steps already reach it), and not
    /// to a thing already gathered, which just keeps settling inward.
    /// </summary>
    [Fact(DisplayName = "Slips are offered only next to the pile")]
    public void StepsInward_should_offer_slips_only_next_to_the_pile()
    {
        Assert.Equal(new[] { (0, -1) },
            ImplosionMath.StepsInward(0, 5, 0, 0, 1.5));
        Assert.Equal(SingularityPull.StepsToward(2, 2, 0, 0),
            ImplosionMath.StepsInward(2, 2, 0, 0, 1.5));
        Assert.Equal(new[] { (-1, 0) },
            ImplosionMath.StepsInward(1, 0, 0, 0, 1.5));
        Assert.Empty(ImplosionMath.StepsInward(3, 3, 3, 3, 1.5));
    }

    /// <summary>Every offset in a square around the centre.</summary>
    /// <param name="half">Half the square's side, in cells.</param>
    /// <returns>The (x, z) offsets.</returns>
    private static (int X, int Z)[] Offsets(int half)
    {
        int side = (2 * half) + 1;
        return Enumerable.Range(-half, side)
            .SelectMany(x =>
                Enumerable.Range(-half, side).Select(z => (x, z)))
            .ToArray();
    }

    /// <summary>
    /// The offsets in a square that a gather radius gathers.
    /// </summary>
    /// <param name="half">Half the square's side, in cells.</param>
    /// <param name="radius">The gather radius, in tiles.</param>
    /// <returns>The gathered (x, z) offsets.</returns>
    private static (int X, int Z)[] Gathered(int half, double radius) =>
        Offsets(half)
            .Where(o => ImplosionMath.IsGathered(o.X, o.Z, radius))
            .ToArray();
}
