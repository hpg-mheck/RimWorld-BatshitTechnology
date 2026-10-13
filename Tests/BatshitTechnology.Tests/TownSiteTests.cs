using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the site scorer: <see cref="SiteGrid"/>'s summed-area queries
/// and <see cref="TownSite"/>'s candidate grid and choice rules
/// (docs/SHOWCASE.md, "Site Selection and Ground Work").
/// </summary>
public sealed class TownSiteTests
{
    private static readonly KeepAway[] None = Array.Empty<KeepAway>();

    // ===== SiteGrid =====

    /// <summary>Box sums match a brute-force count, clipped to the map.</summary>
    [Fact(DisplayName = "Cost sums match brute force")]
    public void CostIn_should_match_brute_force()
    {
        var random = new Random(1234);
        var grid = new SiteGrid(23, 17);
        var costs = new long[23, 17];
        for (int i = 0; i < 300; i++)
        {
            int x = random.Next(23), z = random.Next(17), cost = random.Next(10);
            grid.AddCost(x, z, cost);
            costs[x, z] += cost;
        }

        grid.Freeze();
        for (int i = 0; i < 500; i++)
        {
            int x0 = random.Next(-5, 28), z0 = random.Next(-5, 22);
            var box = new Box(x0, z0, x0 + random.Next(0, 12), z0 + random.Next(0, 12));
            long expected = 0;
            foreach (Cell c in box.Cells().Where(c => c.X is >= 0 and < 23 && c.Z is >= 0 and < 17))
            {
                expected += costs[c.X, c.Z];
            }

            Assert.Equal(expected, grid.CostIn(box));
        }

        Assert.Equal(costs.Cast<long>().Sum(), grid.CostIn(new Box(-100, -100, 100, 100)));
        Assert.Equal(0, grid.CostIn(new Box(30, 30, 40, 40)));
        Assert.Equal(0, grid.CostIn(new Box(5, 5, 4, 4)));
    }

    /// <summary>No-go cells are found by any box touching them, and only those.</summary>
    [Fact(DisplayName = "Blocked cells are found by any box over them")]
    public void AnyBlockedIn_should_find_blocked_cells()
    {
        var grid = new SiteGrid(10, 10);
        grid.Block(4, 6);
        grid.Block(0, 0);
        grid.Freeze();
        Assert.True(grid.AnyBlockedIn(new Box(4, 6, 4, 6)));
        Assert.True(grid.AnyBlockedIn(new Box(2, 2, 7, 7)));
        Assert.True(grid.AnyBlockedIn(new Box(-3, -3, 0, 0)));
        Assert.False(grid.AnyBlockedIn(new Box(5, 0, 9, 9)));
        Assert.False(grid.AnyBlockedIn(new Box(1, 1, 3, 9)));
        Assert.False(grid.AnyBlockedIn(new Box(12, 12, 14, 14)));
        Assert.Equal(0, grid.CostIn(new Box(0, 0, 9, 9)));
    }

    /// <summary>
    /// The grid is filled, then frozen, then read; anything out of order,
    /// off the map or negative is refused.
    /// </summary>
    [Fact(DisplayName = "The site grid refuses misuse")]
    public void SiteGrid_should_refuse_misuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SiteGrid(0, 5));
        var grid = new SiteGrid(4, 3);
        Assert.Equal((4, 3), (grid.Width, grid.Height));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.AddCost(4, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.AddCost(0, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.AddCost(0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Block(0, 3));
        Assert.Throws<InvalidOperationException>(() => grid.CostIn(new Box(0, 0, 1, 1)));
        Assert.Throws<InvalidOperationException>(() => grid.AnyBlockedIn(new Box(0, 0, 1, 1)));
        Assert.False(grid.IsFrozen);
        grid.Freeze();
        Assert.True(grid.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => grid.AddCost(0, 0, 1));
        Assert.Throws<InvalidOperationException>(() => grid.Block(0, 0));
        Assert.Throws<InvalidOperationException>(() => grid.Freeze());
    }

    // ===== Grid =====

    /// <summary>Candidates lie on the step grid from the window's south-west corner.</summary>
    [Fact(DisplayName = "Candidate grids run south-west first")]
    public void Grid_should_step_from_the_south_west()
    {
        Assert.Equal(
            new[] { new Cell(45, 150), new Cell(47, 150), new Cell(45, 152), new Cell(47, 152) },
            TownSite.Grid(new Box(45, 150, 48, 153), 2));
        Assert.Equal(16 * 28, TownSite.Grid(new Box(45, 150, 60, 177), 1).Count());
        Assert.Equal(8 * 14, TownSite.Grid(new Box(45, 150, 60, 177), 2).Count());
        Assert.Empty(TownSite.Grid(new Box(5, 5, 4, 4), 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => TownSite.Grid(new Box(0, 0, 9, 9), 0));
    }

    // ===== Best =====

    /// <summary>The cheapest candidate wins; the cost is the core grown by the firebreak.</summary>
    [Fact(DisplayName = "The cheapest site wins")]
    public void Best_should_pick_the_cheapest()
    {
        var grid = new SiteGrid(40, 40);
        for (int x = 0; x < 20; x++)
        {
            for (int z = 0; z < 40; z++)
            {
                grid.AddCost(x, z, 5);
            }
        }

        grid.Freeze();
        var core = new Box(-2, -2, 2, 2);
        Cell? best = TownSite.Best(grid, core, core, 1, new[] { new Cell(10, 20), new Cell(30, 20) },
            new Cell(10, 20), 0, None, penaltyPerTile: 10);
        Assert.Equal(new Cell(30, 20), best);

        // A penalty of 200 per tile outweighs 49 cells at 5.
        best = TownSite.Best(grid, core, core, 1, new[] { new Cell(10, 20), new Cell(30, 20) },
            new Cell(10, 20), 0, None, penaltyPerTile: 200);
        Assert.Equal(new Cell(10, 20), best);
    }

    /// <summary>No-go cells reject a candidate whose levelled area they lie in.</summary>
    [Fact(DisplayName = "Blocked ground rejects a site")]
    public void Best_should_reject_blocked_sites()
    {
        var grid = new SiteGrid(40, 40);
        grid.Block(13, 20); // inside (10,20)'s core grown by 1
        grid.Block(35, 20); // just outside (30,20)'s
        grid.Freeze();
        var core = new Box(-2, -2, 2, 2);
        var candidates = new[] { new Cell(10, 20), new Cell(30, 20) };
        Assert.Equal(new Cell(30, 20),
            TownSite.Best(grid, core, core, 1, candidates, new Cell(10, 20), 0, None));
        Assert.Null(TownSite.Best(grid, core, core, 3, candidates, new Cell(10, 20), 0, None));
    }

    /// <summary>The bounds, moved to a candidate, must fit the map less the margin.</summary>
    [Fact(DisplayName = "Sites keep the edge margin")]
    public void Best_should_keep_the_edge_margin()
    {
        var grid = new SiteGrid(30, 30);
        grid.Freeze();
        var bounds = new Box(-4, -4, 4, 4);
        var core = new Box(-1, -1, 1, 1);
        Assert.Equal(new Cell(10, 10), TownSite.Best(grid, bounds, core, 0,
            new[] { new Cell(10, 10) }, new Cell(0, 0), 6, None));
        Assert.Null(TownSite.Best(grid, bounds, core, 0,
            new[] { new Cell(9, 10), new Cell(10, 20), new Cell(25, 25) }, new Cell(0, 0), 6, None));
        Assert.Equal(new Cell(9, 10), TownSite.Best(grid, bounds, core, 0,
            new[] { new Cell(9, 10) }, new Cell(0, 0), 5, None));
    }

    /// <summary>A candidate whose bounds come too close to a keep-away area is rejected.</summary>
    [Fact(DisplayName = "Sites keep away from keep-away areas")]
    public void Best_should_respect_keep_aways()
    {
        var grid = new SiteGrid(60, 60);
        grid.Freeze();
        var bounds = new Box(-2, -2, 2, 2);
        var rules = new[] { new KeepAway(new Box(30, 0, 59, 59), 10) };
        var candidates = new[] { new Cell(20, 30), new Cell(18, 30), new Cell(10, 30) };

        // (20,30): gap 30-22 = 8; (18,30): gap 10, allowed; nearer the
        // preferred spot than (10,30), so it wins.
        Assert.Equal(new Cell(18, 30), TownSite.Best(grid, bounds, bounds, 0, candidates,
            new Cell(20, 30), 0, rules));
        Assert.Equal(new Cell(10, 30), TownSite.Best(grid, bounds, bounds, 0, candidates,
            new Cell(20, 30), 0, new[] { new KeepAway(new Box(30, 0, 59, 59), 10.5) }));
    }

    /// <summary>
    /// Equal scores go to the candidate nearer the preferred spot, then to
    /// the first offered.
    /// </summary>
    [Fact(DisplayName = "Ties go nearer the preferred spot, then first")]
    public void Best_should_break_ties()
    {
        var grid = new SiteGrid(40, 40);
        grid.AddCost(21, 20, 10);
        grid.Freeze();
        var core = new Box(0, 0, 0, 0);
        var preferred = new Cell(20, 20);

        // Equal scores and distances (9 tiles, 90 points): the first wins.
        Cell[] even = { new Cell(11, 20), new Cell(29, 20) };
        Assert.Equal(even[0], TownSite.Best(grid, core, core, 0, even, preferred, 0, None));
        Assert.Equal(even[1], TownSite.Best(grid, core, core, 0, even.AsEnumerable().Reverse(), preferred, 0, None));

        // Free and unpenalised, both score 0: the nearer wins, in any order.
        Cell[] free = { new Cell(11, 20), new Cell(25, 20) };
        Assert.Equal(free[1], TownSite.Best(grid, core, core, 0, free, preferred, 0, None, 0));
        Assert.Equal(free[1], TownSite.Best(
            grid, core, core, 0, free.AsEnumerable().Reverse(), preferred, 0, None, 0));

        // (21,20) costs 10 at one tile, (22,20) nothing at two: both 20,
        // and the nearer wins.
        Cell[] priced = { new Cell(22, 20), new Cell(21, 20) };
        Assert.Equal(new Cell(21, 20), TownSite.Best(grid, core, core, 0, priced, preferred, 0, None));
    }

    /// <summary>Arguments the search cannot use are refused.</summary>
    [Fact(DisplayName = "Best refuses bad arguments")]
    public void Best_should_refuse_bad_arguments()
    {
        var grid = new SiteGrid(10, 10);
        var box = new Box(0, 0, 0, 0);
        var one = new[] { new Cell(5, 5) };
        Assert.Throws<InvalidOperationException>(
            () => TownSite.Best(grid, box, box, 0, one, new Cell(5, 5), 0, None));
        grid.Freeze();
        Assert.Throws<ArgumentNullException>(() => TownSite.Best(null!, box, box, 0, one, new Cell(), 0, None));
        Assert.Throws<ArgumentNullException>(() => TownSite.Best(grid, box, box, 0, null!, new Cell(), 0, None));
        Assert.Throws<ArgumentNullException>(() => TownSite.Best(grid, box, box, 0, one, new Cell(), 0, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => TownSite.Best(grid, box, box, -1, one, new Cell(), 0, None));
        Assert.Throws<ArgumentOutOfRangeException>(() => TownSite.Best(grid, box, box, 0, one, new Cell(), -1, None));
        Assert.Throws<ArgumentNullException>(
            () => TownSite.Best(grid, (TownPlan)null!, one, new Cell(), 0, None));
        Assert.Null(TownSite.Best(grid, box, box, 0, Array.Empty<Cell>(), new Cell(), 0, None));
    }

    /// <summary>
    /// On the showcase's 250 by 250 quick-test map, with its base square
    /// (10-tile keep-away) and grenade clearing (15), AnyRim finds a site
    /// in its window when keep-aways are measured from the town's real
    /// parts. Measured from its bounding rectangle, every candidate fails,
    /// which is why the plan overload exists.
    /// </summary>
    [Fact(DisplayName = "AnyRim finds a site on the showcase map")]
    public void Best_should_place_anyrim_on_the_showcase_map()
    {
        TownPlan plan = TownPlan.AnyRim(1, TownPalette.For(TownQuality.Medium));
        var grid = new SiteGrid(250, 250);
        grid.Freeze();
        var keepAways = new[]
        {
            new KeepAway(new Box(109, 109, 141, 141), 10),
            new KeepAway(new Box(87, 112, 113, 138), 15),
        };
        var candidates = TownSite.Grid(new Box(45, 150, 60, 177), 2).ToList();
        var preferred = new Cell(55, 163);

        Cell? site = TownSite.Best(grid, plan, candidates, preferred, 6, keepAways);
        Assert.NotNull(site);
        Cell anchor = site!.Value;
        Assert.True(new Box(6, 6, 243, 243).Contains(plan.Bounds.Offset(anchor)));
        foreach (Box part in plan.Parts)
        {
            Assert.True(part.Offset(anchor).DistanceTo(keepAways[0].Area) >= 10);
            Assert.True(part.Offset(anchor).DistanceTo(keepAways[1].Area) >= 15);
        }

        Assert.True(anchor.DistanceTo(preferred) < 12, $"site {anchor}");
        Assert.Null(TownSite.Best(grid, plan.Bounds, plan.Core, plan.FirebreakWidth, candidates,
            preferred, 6, keepAways));
    }

    /// <summary>
    /// The plan overload scores the core grown by the plan's firebreak,
    /// like the box overload given the same core.
    /// </summary>
    [Fact(DisplayName = "The plan overload levels the core and firebreak")]
    public void Best_with_a_plan_should_level_core_and_firebreak()
    {
        TownPlan plan = TownPlan.AnyRim(1, TownPalette.For(TownQuality.Medium));
        var grid = new SiteGrid(250, 250);
        Box levelled = plan.Core.ExpandedBy(plan.FirebreakWidth).Offset(new Cell(55, 163));
        grid.Block(levelled.MaxX, levelled.MaxZ); // only (55,163) covers this corner
        grid.Freeze();
        var candidates = new[] { new Cell(55, 163), new Cell(54, 163) };
        Assert.Equal(new Cell(54, 163),
            TownSite.Best(grid, plan, candidates, new Cell(55, 163), 6, None));
    }
}
