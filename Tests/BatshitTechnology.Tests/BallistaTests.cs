using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Ballista's power formula (<see cref="BallistaPower"/>) and
/// magazine connection rule (<see cref="MagazineNetwork"/>).
/// </summary>
public sealed class BallistaTests
{
    /// <summary>One projectile at 10 tiles draws the agreed baseline.</summary>
    [Fact(DisplayName = "Baseline: 50 W standby, 1000 W firing")]
    public void Power_should_match_baseline()
    {
        Assert.Equal(50d, BallistaPower.StandbyWatts(1, 10), 6);
        Assert.Equal(1000d, BallistaPower.FiringWatts(1, 10), 6);
    }

    /// <summary>
    /// At maximum, standby is a quarter of one 250 kW reactor and firing
    /// fits in four with room for magazines.
    /// </summary>
    [Fact(DisplayName = "Maximum: 62.5 kW standby, 990 kW firing")]
    public void Power_should_match_maximum()
    {
        Assert.Equal(62_500d, BallistaPower.StandbyWatts(256, 325), 6);
        Assert.Equal(990_000d, BallistaPower.FiringWatts(256, 325), 6);
        Assert.True(BallistaPower.StandbyWatts(256, 325) <= 0.25 * 250_000);
        Assert.True(BallistaPower.FiringWatts(256, 325) + (20 * 500) <= 4 * 250_000);
    }

    /// <summary>Range and count count equally: they multiply into units.</summary>
    [Fact(DisplayName = "Range and count multiply")]
    public void Power_should_depend_on_count_times_range()
    {
        Assert.Equal(
            BallistaPower.FiringWatts(4, 50), BallistaPower.FiringWatts(20, 10), 6);
        Assert.Equal(
            BallistaPower.StandbyWatts(8, 100), BallistaPower.StandbyWatts(80, 10), 6);
    }

    /// <summary>Power rises linearly with units.</summary>
    [Fact(DisplayName = "Power is linear in units")]
    public void Power_should_rise_linearly()
    {
        double first = BallistaPower.FiringWatts(2, 10) - BallistaPower.FiringWatts(1, 10);
        double later = BallistaPower.FiringWatts(201, 10) - BallistaPower.FiringWatts(200, 10);
        Assert.Equal(first, later, 6);
        Assert.True(first > 0);
    }

    /// <summary>Out-of-range settings clamp to the selectable limits.</summary>
    [Fact(DisplayName = "Settings clamp to their limits")]
    public void Units_should_clamp_settings()
    {
        Assert.Equal(BallistaPower.Units(1, 10), BallistaPower.Units(0, 2));
        Assert.Equal(BallistaPower.MaxUnits, BallistaPower.Units(9999, 9999));
    }

    /// <summary>
    /// A chain connects through shared edges; diagonal-only and detached
    /// magazines do not.
    /// </summary>
    [Fact(DisplayName = "Magazines connect by shared edges, through chains")]
    public void Network_should_follow_edge_adjacent_chains()
    {
        var ballista = new[] { (0, 0), (1, 0), (0, 1), (1, 1) }; // 2x2
        var magazines = new HashSet<(int, int)>
        {
            (2, 0),   // touches the ballista
            (3, 0),   // touches that magazine: chained
            (3, 1),   // chained again
            (-1, -1), // diagonal to the ballista only
            (10, 10), // far away
        };

        var connected = MagazineNetwork.Connected(ballista, magazines);

        Assert.Equal(new[] { (2, 0), (3, 0), (3, 1) }, connected.OrderBy(c => c).ToArray());
    }

    /// <summary>Nearest links come first, so volleys drain the closest first.</summary>
    [Fact(DisplayName = "Connected magazines come nearest first")]
    public void Network_should_list_nearest_links_first()
    {
        var connected = MagazineNetwork.Connected(
            new[] { (0, 0) },
            new HashSet<(int, int)> { (3, 0), (2, 0), (1, 0) });

        Assert.Equal(new[] { (1, 0), (2, 0), (3, 0) }, connected.ToArray());
    }

    /// <summary>One magazine between two Ballistas serves both.</summary>
    [Fact(DisplayName = "One magazine can serve two Ballistas")]
    public void Network_should_let_a_magazine_serve_two_ballistas()
    {
        var shared = new HashSet<(int, int)> { (1, 0) };

        Assert.Single(MagazineNetwork.Connected(new[] { (0, 0) }, shared));
        Assert.Single(MagazineNetwork.Connected(new[] { (2, 0) }, shared));
    }
}
