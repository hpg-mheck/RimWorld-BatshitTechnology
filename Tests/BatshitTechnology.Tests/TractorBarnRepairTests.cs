using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Tractor Barn's repair arithmetic in
/// <see cref="TractorBarnRepair"/> (docs/TRACTOR-BOMB.md, "The Tractor
/// Barn"): who goes home, what each step costs, whether the hopper can pay,
/// and how much steel and time a whole repair takes.
/// </summary>
public sealed class TractorBarnRepairTests
{
    /// <summary>
    /// "Below about 50% health": under half goes home, half and over keeps
    /// patrolling, and a reading that is not a number keeps patrolling.
    /// </summary>
    /// <param name="health">The robot's summary health.</param>
    /// <param name="expected">Whether it goes home.</param>
    [Theory(DisplayName = "Robots below half health go home for repairs")]
    [InlineData(0.05, true)]
    [InlineData(0.49, true)]
    [InlineData(0.4999, true)]
    [InlineData(0.5, false)]
    [InlineData(0.75, false)]
    [InlineData(1.0, false)]
    [InlineData(double.NaN, false)]
    public void ShouldSeekRepair_should_split_at_half(
        double health, bool expected)
    {
        Assert.Equal(expected, TractorBarnRepair.ShouldSeekRepair(health));
    }

    /// <summary>
    /// A step mends one hit point for a fifth of a unit of steel, or refits
    /// a part for two units: "a little steel".
    /// </summary>
    [Fact(DisplayName = "A repair step costs a little steel")]
    public void StepCost_should_be_small()
    {
        Assert.Equal(1f, TractorBarnRepair.HitPointsPerStep);
        Assert.Equal(0.2f, TractorBarnRepair.StepCost(false), 5);
        Assert.Equal(2f, TractorBarnRepair.StepCost(true), 5);
        Assert.Equal(30, TractorBarnRepair.TicksPerStep);
    }

    /// <summary>
    /// The hopper pays when it holds the cost (allowing for the float
    /// arithmetic of a hopper drained a fifth at a time), and not when it
    /// is short, empty or unreadable.
    /// </summary>
    /// <param name="steel">What the hopper holds.</param>
    /// <param name="cost">The step's cost.</param>
    /// <param name="expected">Whether it can pay.</param>
    [Theory(DisplayName = "The hopper pays only what it holds")]
    [InlineData(20.0, 0.2, true)]
    [InlineData(0.2, 0.2, true)]
    [InlineData(0.19999, 0.2, true)]
    [InlineData(0.15, 0.2, false)]
    [InlineData(0.0, 0.2, false)]
    [InlineData(1.9, 2.0, false)]
    [InlineData(0.0, 0.0, true)]
    [InlineData(5.0, -1.0, true)]
    [InlineData(double.NaN, 0.2, false)]
    [InlineData(5.0, double.NaN, false)]
    public void CanAfford_should_check_the_hopper(
        double steel, double cost, bool expected)
    {
        Assert.Equal(expected, TractorBarnRepair.CanAfford(steel, cost));
    }

    /// <summary>
    /// A drained hopper pays exactly as many fifth-unit steps as it holds:
    /// float drift never costs a robot its last hit point.
    /// </summary>
    [Fact(DisplayName = "A unit of steel pays for five hit points exactly")]
    public void CanAfford_should_survive_float_drift()
    {
        float steel = 1f;
        int steps = 0;
        float cost = TractorBarnRepair.StepCost(false);
        while (TractorBarnRepair.CanAfford(steel, cost) && steps < 100)
        {
            steel -= cost;
            steps++;
        }

        Assert.Equal(5, steps);
    }

    /// <summary>
    /// One injury takes a step per hit point, a part of a hit point a
    /// whole step (each step mends one injury); nothing, or nonsense,
    /// takes none.
    /// </summary>
    /// <param name="severity">The injury's hit points.</param>
    /// <param name="expected">The steps.</param>
    [Theory(DisplayName = "An injury takes a step per hit point, rounded up")]
    [InlineData(1.0, 1L)]
    [InlineData(0.3, 1L)]
    [InlineData(4.32, 5L)]
    [InlineData(20.0, 20L)]
    [InlineData(20.0000001, 20L)]
    [InlineData(0.0, 0L)]
    [InlineData(-3.0, 0L)]
    [InlineData(double.NaN, 0L)]
    public void StepsToMend_should_round_up_per_injury(
        double severity, long expected)
    {
        Assert.Equal(expected, TractorBarnRepair.StepsToMend(severity));
    }

    /// <summary>
    /// A whole repair: a robot at about half health (30 hit points of
    /// injury) and a lost wheel takes eight units of steel and 31 steps
    /// (15.5 seconds); split into injuries with parts of hit points, each
    /// rounds up on its own (the smoke test's robot: 20.8, 5.76 and three
    /// of 4.32 take 42 steps, not 41); nothing to mend costs nothing; and
    /// nonsense counts as nothing.
    /// </summary>
    /// <param name="injuries">Each injury's hit points.</param>
    /// <param name="parts">Missing parts.</param>
    /// <param name="steel">The steel expected.</param>
    /// <param name="ticks">The ticks expected.</param>
    [Theory(DisplayName = "A whole repair's steel and time")]
    [InlineData(new[] { 30.0 }, 1, 8.0, 930L)]
    [InlineData(new[] { 20.0, 10.0 }, 0, 6.0, 900L)]
    [InlineData(new[] { 20.8, 5.76, 4.32, 4.32, 4.32 }, 1, 10.4, 1290L)]
    [InlineData(new[] { 0.5 }, 0, 0.2, 30L)]
    [InlineData(new double[0], 0, 0.0, 0L)]
    [InlineData(new[] { -4.0 }, -2, 0.0, 0L)]
    [InlineData(new[] { double.NaN }, 0, 0.0, 0L)]
    [InlineData(new double[0], 3, 6.0, 90L)]
    public void SteelFor_and_TicksFor_should_add_up(
        double[] injuries, int parts, double steel, long ticks)
    {
        Assert.Equal(steel, TractorBarnRepair.SteelFor(injuries, parts), 4);
        Assert.Equal(ticks, TractorBarnRepair.TicksFor(injuries, parts));
    }

    /// <summary>No list of injuries counts as no injuries.</summary>
    [Fact(DisplayName = "A repair with no injury list costs only its parts")]
    public void SteelFor_should_accept_null()
    {
        Assert.Equal(2.0, TractorBarnRepair.SteelFor(null, 1), 4);
        Assert.Equal(30L, TractorBarnRepair.TicksFor(null, 1));
    }
}
