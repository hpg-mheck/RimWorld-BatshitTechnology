using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Tractor Bomb's clock in <see cref="TractorBombTiming"/>:
/// the protest, the tyre flip, the rolled countdown, the implosion's
/// durations, and the lamp and beeps of a protest.
/// </summary>
public sealed class TractorBombTimingTests
{
    /// <summary>
    /// The spec's times: a three-second protest, about half a second for
    /// the tyres, and a countdown of 3 to 5 seconds.
    /// </summary>
    [Fact(DisplayName = "Protest, flip and countdown lengths match the spec")]
    public void Constants_should_match_spec()
    {
        Assert.Equal(180, TractorBombTiming.ProtestTicks);
        Assert.Equal(30, TractorBombTiming.DeployTicks);
        Assert.Equal(3d, TractorBombTiming.MinCountdownSeconds);
        Assert.Equal(5d, TractorBombTiming.MaxCountdownSeconds);
    }

    /// <summary>
    /// It protests for three seconds only if a colonist is inside its pull
    /// radius; otherwise it emplaces at once.
    /// </summary>
    [Fact(DisplayName = "Protest delays emplacing by 3 s only for colonists")]
    public void ProtestDelay_should_depend_on_colonists()
    {
        Assert.Equal(180, TractorBombTiming.ProtestDelay(true));
        Assert.Equal(0, TractorBombTiming.ProtestDelay(false));
    }

    /// <summary>
    /// The countdown lands between 3 and 5 seconds, both included, for any
    /// random number, and the ends map to the ends.
    /// </summary>
    [Fact(DisplayName = "Countdown rolls 180 to 300 ticks, both included")]
    public void RollCountdownTicks_should_stay_in_range()
    {
        Assert.Equal(180, TractorBombTiming.RollCountdownTicks(0));
        Assert.Equal(300, TractorBombTiming.RollCountdownTicks(0.999999));
        var rolls = Enumerable.Range(0, 10001)
            .Select(i => TractorBombTiming.RollCountdownTicks(i / 10001d))
            .ToList();
        Assert.All(rolls, t => Assert.InRange(t, 180, 300));
        Assert.Equal(121, rolls.Distinct().Count()); // every tick reachable
    }

    /// <summary>
    /// The roll is uniform: each of the 121 lengths takes the same share of
    /// the unit interval, to within one sample.
    /// </summary>
    [Fact(DisplayName = "Countdown roll is uniform")]
    public void RollCountdownTicks_should_be_uniform()
    {
        const int Samples = 121 * 100;
        var counts = Enumerable.Range(0, Samples)
            .Select(i => TractorBombTiming.RollCountdownTicks(
                (i + 0.5) / Samples))
            .GroupBy(t => t)
            .Select(g => g.Count())
            .ToList();
        Assert.Equal(121, counts.Count);
        Assert.All(counts, c => Assert.InRange(c, 99, 101));
    }

    /// <summary>
    /// Nonsense input still gives a countdown in range: random numbers out
    /// of [0, 1) or NaN, and lengths given backwards, negative or NaN.
    /// </summary>
    /// <param name="unit">The random number.</param>
    /// <param name="min">The shortest length, in seconds.</param>
    /// <param name="max">The longest length, in seconds.</param>
    /// <param name="low">The smallest acceptable result.</param>
    /// <param name="high">The largest acceptable result.</param>
    [Theory(DisplayName = "Countdown roll survives nonsense")]
    [InlineData(-3, 3, 5, 180, 180)]
    [InlineData(7, 3, 5, 300, 300)]
    [InlineData(double.NaN, 3, 5, 180, 180)]
    [InlineData(0.5, 5, 3, 300, 300)]
    [InlineData(0.5, -1, -1, 1, 1)]
    [InlineData(0.5, double.NaN, 2, 1, 120)]
    [InlineData(0.99, 0.001, 0.002, 1, 1)]
    public void RollCountdownTicks_should_survive_nonsense(
        double unit, double min, double max, int low, int high)
    {
        Assert.InRange(
            TractorBombTiming.RollCountdownTicks(unit, min, max), low, high);
    }

    /// <summary>
    /// Its implosion runs at least two seconds and at most eight, as the
    /// roadmap's multi-bomb physics asks (its build used a 7.5 s cap).
    /// </summary>
    [Fact(DisplayName = "Tractor Bomb implosion runs 2 to 8 s")]
    public void Implosion_durations_should_match_spec()
    {
        Assert.Equal(120, TractorBombTiming.MinImplosionTicks);
        Assert.Equal(480, TractorBombTiming.MaxImplosionTicks);
    }

    /// <summary>
    /// The protest lamp starts red and alternates with yellow every half
    /// period; a nonsense half period still alternates.
    /// </summary>
    [Fact(DisplayName = "Protest lamp blinks red then yellow")]
    public void LampIsRed_should_alternate()
    {
        Assert.True(TractorBombTiming.LampIsRed(0, 10));
        Assert.True(TractorBombTiming.LampIsRed(9, 10));
        Assert.False(TractorBombTiming.LampIsRed(10, 10));
        Assert.False(TractorBombTiming.LampIsRed(19, 10));
        Assert.True(TractorBombTiming.LampIsRed(20, 10));
        Assert.True(TractorBombTiming.LampIsRed(-5, 10));
        Assert.True(TractorBombTiming.LampIsRed(0, 0));
        Assert.False(TractorBombTiming.LampIsRed(1, 0));
    }

    /// <summary>
    /// A three-second protest beeping every half second beeps six times,
    /// the first at once.
    /// </summary>
    [Fact(DisplayName = "Protest beeps at its start and every interval")]
    public void BeepsAt_should_beep_on_the_interval()
    {
        var beeps = Enumerable.Range(0, TractorBombTiming.ProtestTicks)
            .Where(t => TractorBombTiming.BeepsAt(t, 30))
            .ToList();
        Assert.Equal(new[] { 0, 30, 60, 90, 120, 150 }, beeps);
        Assert.False(TractorBombTiming.BeepsAt(-30, 30));
        Assert.True(TractorBombTiming.BeepsAt(7, 0));
    }
}
