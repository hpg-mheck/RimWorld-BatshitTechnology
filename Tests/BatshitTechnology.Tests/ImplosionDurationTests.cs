using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies an implosion's absolute minimum and maximum durations
/// (<see cref="ImplosionMath.IsOver"/>; ROADMAP.md, "Absolute timeouts"):
/// before the minimum it keeps pulling whatever the state, at the maximum
/// it ends however things stand, and in between it ends once everything
/// is gathered or settled.
/// </summary>
public sealed class ImplosionDurationTests
{
    /// <summary>
    /// The grenade keeps exactly five seconds: its minimum and maximum are
    /// equal, so it is over at tick 300 and never before, whether or not
    /// everything is gathered or settled.
    /// </summary>
    [Fact(DisplayName = "Grenade implosion lasts exactly 5 s")]
    public void Grenade_should_last_exactly_five_seconds()
    {
        ImplosionSettings s = new ImplosionSettings().Normalized(79.8);
        foreach (bool gathered in new[] { false, true })
        {
            foreach (bool settled in new[] { false, true })
            {
                int firstOver = Enumerable.Range(0, 1000).First(
                    t => ImplosionMath.IsOver(
                        t, s.minDurationTicks, s.maxDurationTicks,
                        gathered, settled));
                Assert.Equal(300, firstOver);
            }
        }
    }

    /// <summary>
    /// Before the minimum an implosion is never over, even with everything
    /// gathered and settled: there is always a show.
    /// </summary>
    /// <param name="elapsed">Ticks run, all under the minimum.</param>
    [Theory(DisplayName = "Implosion keeps pulling before its minimum")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(119)]
    public void Should_keep_pulling_before_the_minimum(int elapsed)
    {
        Assert.False(ImplosionMath.IsOver(
            elapsed, TractorBombTiming.MinImplosionTicks,
            TractorBombTiming.MaxImplosionTicks, true, true));
    }

    /// <summary>
    /// Between the minimum and the maximum it is over exactly when
    /// everything is gathered or nothing more can move.
    /// </summary>
    /// <param name="elapsed">Ticks run.</param>
    /// <param name="gathered">Everything is gathered.</param>
    /// <param name="settled">Nothing more can move.</param>
    /// <param name="over">Whether it should be over.</param>
    [Theory(DisplayName = "Implosion ends between its limits when done")]
    [InlineData(120, false, false, false)]
    [InlineData(120, true, false, true)]
    [InlineData(120, false, true, true)]
    [InlineData(300, true, true, true)]
    [InlineData(479, false, false, false)]
    public void Should_end_between_limits_once_done(
        int elapsed, bool gathered, bool settled, bool over)
    {
        Assert.Equal(over, ImplosionMath.IsOver(
            elapsed, TractorBombTiming.MinImplosionTicks,
            TractorBombTiming.MaxImplosionTicks, gathered, settled));
    }

    /// <summary>
    /// At the maximum, and after it, it is over however things stand: a
    /// balance point or a tug-of-war cannot hold it open.
    /// </summary>
    /// <param name="elapsed">Ticks run, at or past the maximum.</param>
    [Theory(DisplayName = "Implosion ends at its maximum however things stand")]
    [InlineData(480)]
    [InlineData(481)]
    [InlineData(int.MaxValue)]
    public void Should_end_at_the_maximum(int elapsed)
    {
        Assert.True(ImplosionMath.IsOver(
            elapsed, TractorBombTiming.MinImplosionTicks,
            TractorBombTiming.MaxImplosionTicks, false, false));
    }

    /// <summary>
    /// Nonsense limits still end it: a maximum under a tick counts as one,
    /// a negative minimum as none, and a minimum past the maximum as the
    /// maximum.
    /// </summary>
    /// <param name="min">The minimum given.</param>
    /// <param name="max">The maximum given.</param>
    /// <param name="done">Whether everything is gathered and settled.</param>
    /// <param name="firstOver">The first tick it should be over.</param>
    [Theory(DisplayName = "Implosion durations survive nonsense")]
    [InlineData(0, 0, false, 1)]
    [InlineData(-5, -9, false, 1)]
    [InlineData(-5, 100, true, 0)]
    [InlineData(900, 480, true, 480)]
    [InlineData(900, 480, false, 480)]
    public void Should_survive_nonsense(
        int min, int max, bool done, int firstOver)
    {
        Assert.Equal(firstOver, Enumerable.Range(0, 2000).First(
            t => ImplosionMath.IsOver(t, min, max, done, done)));
    }
}
