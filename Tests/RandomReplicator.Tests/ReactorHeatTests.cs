using System;
using RandomReplicator.Core;
using Xunit;

namespace RandomReplicator.Tests;

/// <summary>
/// Verifies the Batshittium Reactor's waste-heat arithmetic in
/// <see cref="ReactorHeat"/>.
/// </summary>
public sealed class ReactorHeatTests
{
    /// <summary>A cell at the reactor gets full weight.</summary>
    [Fact(DisplayName = "Cell weight is 1 at the reactor")]
    public void CellWeight_should_be_one_at_distance_zero()
    {
        Assert.Equal(1d, ReactorHeat.CellWeight(0, 12));
    }

    /// <summary>
    /// The edge of the radius still gets a share, so rooms at the rim warm
    /// up too.
    /// </summary>
    [Fact(DisplayName = "Cell weight stays positive at the edge of the radius")]
    public void CellWeight_should_stay_positive_at_radius()
    {
        Assert.Equal(1d / 13d, ReactorHeat.CellWeight(12, 12), 10);
    }

    /// <summary>Weight falls as distance grows.</summary>
    [Fact(DisplayName = "Cell weight falls with distance")]
    public void CellWeight_should_decrease_with_distance()
    {
        Assert.True(ReactorHeat.CellWeight(3, 12) > ReactorHeat.CellWeight(9, 12));
    }

    /// <summary>Beyond the falloff, weight clamps at zero rather than going negative.</summary>
    [Fact(DisplayName = "Cell weight never goes negative")]
    public void CellWeight_should_clamp_at_zero()
    {
        Assert.Equal(0d, ReactorHeat.CellWeight(50, 12));
    }

    /// <summary>Invalid geometry fails fast.</summary>
    [Fact(DisplayName = "Cell weight rejects a non-positive radius or negative distance")]
    public void CellWeight_should_reject_invalid_arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReactorHeat.CellWeight(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReactorHeat.CellWeight(-1, 12));
    }

    /// <summary>A room gets heat in proportion to its share of the weight.</summary>
    [Fact(DisplayName = "Room energy is the room's weighted share")]
    public void RoomEnergy_should_be_proportional_to_weight()
    {
        Assert.Equal(250d, ReactorHeat.RoomEnergy(1000, 25, 100, double.MaxValue), 10);
    }

    /// <summary>A room near the cap only gets what brings it to the cap.</summary>
    [Fact(DisplayName = "Room energy is capped by headroom")]
    public void RoomEnergy_should_cap_at_headroom()
    {
        Assert.Equal(40d, ReactorHeat.RoomEnergy(1000, 25, 100, 40));
    }

    /// <summary>A room at or past the cap, or with no weight, gets nothing.</summary>
    [Theory(DisplayName = "Room energy is zero without headroom or weight")]
    [InlineData(1000, 25, 100, 0)]
    [InlineData(1000, 25, 100, -5)]
    [InlineData(1000, 0, 100, 40)]
    [InlineData(1000, 25, 0, 40)]
    [InlineData(0, 25, 100, 40)]
    public void RoomEnergy_should_be_zero_in_degenerate_cases(
        double total, double roomWeight, double totalWeight, double headroom)
    {
        Assert.Equal(0d, ReactorHeat.RoomEnergy(total, roomWeight, totalWeight, headroom));
    }
}
