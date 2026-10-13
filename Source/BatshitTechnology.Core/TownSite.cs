using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// A map-sized grid of levelling costs and no-go cells, filled in by the
/// game, that answers box queries in constant time once frozen.
/// </summary>
public sealed class SiteGrid
{
    private readonly long[] cost;
    private readonly bool[] blocked;
    private long[]? costSums;
    private int[]? blockedSums;

    /// <summary>Creates an empty grid.</summary>
    /// <param name="width">Map width (x).</param>
    /// <param name="height">Map height (z).</param>
    /// <exception cref="ArgumentOutOfRangeException">A size below 1.</exception>
    public SiteGrid(int width, int height)
    {
        if (width < 1 || height < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), $"{width}x{height}", "A map needs at least one cell.");
        }

        Width = width;
        Height = height;
        cost = new long[width * height];
        blocked = new bool[width * height];
    }

    /// <summary>Map width.</summary>
    public int Width { get; }

    /// <summary>Map height.</summary>
    public int Height { get; }

    /// <summary>Whether <see cref="Freeze"/> has been called.</summary>
    public bool IsFrozen => costSums != null;

    /// <summary>Adds to a cell's levelling cost (before <see cref="Freeze"/>).</summary>
    /// <param name="x">Map x.</param>
    /// <param name="z">Map z.</param>
    /// <param name="cost">The cost to add; non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The cell is off the map, or the cost is negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">The grid is frozen.</exception>
    public void AddCost(int x, int z, int cost)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cost), cost, "Levelling costs are never negative.");
        }

        this.cost[IndexOf(x, z)] += cost;
    }

    /// <summary>Marks a cell no-go (before <see cref="Freeze"/>).</summary>
    /// <param name="x">Map x.</param>
    /// <param name="z">Map z.</param>
    /// <exception cref="ArgumentOutOfRangeException">The cell is off the map.</exception>
    /// <exception cref="InvalidOperationException">The grid is frozen.</exception>
    public void Block(int x, int z) => blocked[IndexOf(x, z)] = true;

    /// <summary>Builds the summed-area tables; the grid is read-only afterwards.</summary>
    /// <exception cref="InvalidOperationException">The grid is already frozen.</exception>
    public void Freeze()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("The site grid is already frozen.");
        }

        // Tables are one larger each way: entry (x+1, z+1) sums cells
        // (0..x, 0..z), and row and column 0 are zero.
        int stride = Width + 1;
        var costs = new long[stride * (Height + 1)];
        var blocks = new int[stride * (Height + 1)];
        for (int z = 0; z < Height; z++)
        {
            long rowCost = 0;
            int rowBlocks = 0;
            for (int x = 0; x < Width; x++)
            {
                rowCost += cost[(z * Width) + x];
                rowBlocks += blocked[(z * Width) + x] ? 1 : 0;
                int at = ((z + 1) * stride) + x + 1;
                costs[at] = costs[at - stride] + rowCost;
                blocks[at] = blocks[at - stride] + rowBlocks;
            }
        }

        costSums = costs;
        blockedSums = blocks;
    }

    /// <summary>The total cost inside a box, clipped to the map.</summary>
    /// <param name="mapBox">The box, in map coordinates.</param>
    /// <returns>The total cost; 0 if the box misses the map.</returns>
    /// <exception cref="InvalidOperationException">The grid is not frozen.</exception>
    public long CostIn(Box mapBox)
    {
        long[] sums = costSums
            ?? throw new InvalidOperationException("Freeze the site grid first.");
        return Clip(mapBox) is Box b ? Sum(sums, b) : 0;
    }

    /// <summary>Whether any no-go cell lies inside a box, clipped to the map.</summary>
    /// <param name="mapBox">The box, in map coordinates.</param>
    /// <returns>True if any cell is blocked.</returns>
    /// <exception cref="InvalidOperationException">The grid is not frozen.</exception>
    public bool AnyBlockedIn(Box mapBox)
    {
        int[] sums = blockedSums
            ?? throw new InvalidOperationException("Freeze the site grid first.");
        if (Clip(mapBox) is not Box b)
        {
            return false;
        }

        int stride = Width + 1;
        int count = sums[((b.MaxZ + 1) * stride) + b.MaxX + 1]
            - sums[(b.MinZ * stride) + b.MaxX + 1]
            - sums[((b.MaxZ + 1) * stride) + b.MinX]
            + sums[(b.MinZ * stride) + b.MinX];
        return count > 0;
    }

    /// <summary>A cell's index into the raw arrays, while still editable.</summary>
    /// <param name="x">Map x.</param>
    /// <param name="z">Map z.</param>
    /// <returns>The index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cell is off the map.</exception>
    /// <exception cref="InvalidOperationException">The grid is frozen.</exception>
    private int IndexOf(int x, int z)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("The site grid is frozen.");
        }

        if (x < 0 || x >= Width || z < 0 || z >= Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x), $"({x},{z})", $"Outside the {Width}x{Height} map.");
        }

        return (z * Width) + x;
    }

    /// <summary>A box clipped to the map.</summary>
    /// <param name="box">The box.</param>
    /// <returns>The clipped box, or null if nothing of it is on the map.</returns>
    private Box? Clip(Box box)
    {
        var clipped = new Box(
            Math.Max(0, box.MinX), Math.Max(0, box.MinZ),
            Math.Min(Width - 1, box.MaxX), Math.Min(Height - 1, box.MaxZ));
        return box.IsValid && clipped.IsValid ? clipped : null;
    }

    /// <summary>The sum over a box already clipped to the map.</summary>
    /// <param name="sums">A summed-area table.</param>
    /// <param name="b">The box.</param>
    /// <returns>The sum.</returns>
    private long Sum(long[] sums, Box b)
    {
        int stride = Width + 1;
        return sums[((b.MaxZ + 1) * stride) + b.MaxX + 1]
            - sums[(b.MinZ * stride) + b.MaxX + 1]
            - sums[((b.MaxZ + 1) * stride) + b.MinX]
            + sums[(b.MinZ * stride) + b.MinX];
    }
}

/// <summary>An area a town must keep a distance from.</summary>
/// <param name="Area">The area, in map coordinates.</param>
/// <param name="MinDistance">The least allowed gap, in cells.</param>
public readonly record struct KeepAway(Box Area, double MinDistance);

/// <summary>Picks where on a map to build a town.</summary>
public static class TownSite
{
    /// <summary>Scores closer than this are treated as equal.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Candidate anchors on a regular grid inside a window.
    /// </summary>
    /// <param name="window">The window, in map coordinates.</param>
    /// <param name="step">Spacing, in cells; at least 1.</param>
    /// <returns>The candidates, south-west first.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A step below 1.</exception>
    public static IEnumerable<Cell> Grid(Box window, int step)
    {
        if (step < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(step), step, "The grid step must be at least 1.");
        }

        return GridCells(window, step);
    }

    /// <summary>
    /// The cheapest acceptable anchor (map position of the town's local
    /// (0,0)). A candidate is rejected if the town's bounds, moved there,
    /// do not lie inside the map shrunk by <paramref name="edgeMargin"/>; if
    /// any no-go cell lies in its core grown by <paramref name="firebreak"/>;
    /// or if its bounds come closer to a keep-away area than allowed. The
    /// rest score the levelling cost of the core grown by the firebreak,
    /// plus <paramref name="penaltyPerTile"/> per tile of distance from
    /// <paramref name="preferred"/>; ties go to the candidate nearer the
    /// preferred spot, then to the first.
    /// </summary>
    /// <remarks>
    /// Bounds are one rectangle around a town and its road tapers, mostly
    /// empty land; AnyRim's bounds overlap the showcase's grenade range from
    /// every candidate in its window. Callers with a plan should use the
    /// overload taking the <see cref="TownPlan"/>, which tests keep-aways
    /// against <see cref="TownPlan.Parts"/> instead.
    /// </remarks>
    /// <param name="grid">The frozen site grid.</param>
    /// <param name="bounds">The town's bounds, town-local.</param>
    /// <param name="core">The town's core, town-local.</param>
    /// <param name="firebreak">Width of the firebreak around the core.</param>
    /// <param name="candidates">Candidate anchors, map coordinates.</param>
    /// <param name="preferred">The preferred anchor.</param>
    /// <param name="edgeMargin">Cells to keep clear of the map edge.</param>
    /// <param name="keepAways">Areas to keep away from.</param>
    /// <param name="penaltyPerTile">Score added per tile from the preferred anchor.</param>
    /// <returns>The best anchor, or null if none is acceptable.</returns>
    /// <exception cref="ArgumentNullException">A null argument.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A negative firebreak, edge margin or penalty.
    /// </exception>
    /// <exception cref="InvalidOperationException">The grid is not frozen.</exception>
    public static Cell? Best(
        SiteGrid grid, Box bounds, Box core, int firebreak, IEnumerable<Cell> candidates,
        Cell preferred, int edgeMargin, IReadOnlyList<KeepAway> keepAways, int penaltyPerTile = 10) =>
        Best(grid, bounds, core, firebreak, new[] { bounds }, candidates,
            preferred, edgeMargin, keepAways, penaltyPerTile);

    /// <summary>
    /// The cheapest acceptable anchor for a plan, as the other overload
    /// picks it, except that keep-aways are tested against the cells the
    /// town really touches (<see cref="TownPlan.Parts"/>) rather than its
    /// bounding rectangle. The map-edge test still uses
    /// <see cref="TownPlan.Bounds"/>; the levelling cost and no-go test use
    /// <see cref="TownPlan.Core"/> grown by
    /// <see cref="TownPlan.FirebreakWidth"/>.
    /// </summary>
    /// <param name="grid">The frozen site grid.</param>
    /// <param name="plan">The town.</param>
    /// <param name="candidates">Candidate anchors, map coordinates.</param>
    /// <param name="preferred">The preferred anchor.</param>
    /// <param name="edgeMargin">Cells to keep clear of the map edge.</param>
    /// <param name="keepAways">Areas to keep away from.</param>
    /// <param name="penaltyPerTile">Score added per tile from the preferred anchor.</param>
    /// <returns>The best anchor, or null if none is acceptable.</returns>
    /// <exception cref="ArgumentNullException">A null argument.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A negative edge margin or penalty, or a plan with a negative
    /// firebreak.
    /// </exception>
    /// <exception cref="InvalidOperationException">The grid is not frozen.</exception>
    public static Cell? Best(
        SiteGrid grid, TownPlan plan, IEnumerable<Cell> candidates, Cell preferred,
        int edgeMargin, IReadOnlyList<KeepAway> keepAways, int penaltyPerTile = 10)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        IReadOnlyList<Box> parts = plan.Parts.Count > 0
            ? plan.Parts
            : new[] { plan.Bounds };
        return Best(grid, plan.Bounds, plan.Core, plan.FirebreakWidth, parts,
            candidates, preferred, edgeMargin, keepAways, penaltyPerTile);
    }

    /// <summary>
    /// The shared search: bounds for the map edge, the core and firebreak
    /// for cost and no-go cells, the parts for keep-aways.
    /// </summary>
    /// <param name="grid">The frozen site grid.</param>
    /// <param name="bounds">The town's bounds, town-local.</param>
    /// <param name="core">The town's core, town-local.</param>
    /// <param name="firebreak">Width of the firebreak around the core.</param>
    /// <param name="parts">Boxes the keep-away gaps are measured from.</param>
    /// <param name="candidates">Candidate anchors, map coordinates.</param>
    /// <param name="preferred">The preferred anchor.</param>
    /// <param name="edgeMargin">Cells to keep clear of the map edge.</param>
    /// <param name="keepAways">Areas to keep away from.</param>
    /// <param name="penaltyPerTile">Score added per tile from the preferred anchor.</param>
    /// <returns>The best anchor, or null if none is acceptable.</returns>
    private static Cell? Best(
        SiteGrid grid, Box bounds, Box core, int firebreak, IReadOnlyList<Box> parts,
        IEnumerable<Cell> candidates, Cell preferred, int edgeMargin,
        IReadOnlyList<KeepAway> keepAways, int penaltyPerTile)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        if (keepAways == null)
        {
            throw new ArgumentNullException(nameof(keepAways));
        }

        if (firebreak < 0 || edgeMargin < 0 || penaltyPerTile < 0)
        {
            throw new ArgumentOutOfRangeException(
                firebreak < 0 ? nameof(firebreak)
                : edgeMargin < 0 ? nameof(edgeMargin)
                : nameof(penaltyPerTile),
                "Firebreak, edge margin and penalty are never negative.");
        }

        if (!grid.IsFrozen)
        {
            throw new InvalidOperationException("Freeze the site grid first.");
        }

        Box usable = new Box(0, 0, grid.Width - 1, grid.Height - 1)
            .ExpandedBy(-edgeMargin);
        Box levelled = core.ExpandedBy(firebreak);
        Cell? best = null;
        double bestScore = double.PositiveInfinity;
        double bestDistance = double.PositiveInfinity;
        foreach (Cell anchor in candidates)
        {
            if (!usable.IsValid || !usable.Contains(bounds.Offset(anchor))
                || grid.AnyBlockedIn(levelled.Offset(anchor))
                || TooClose(parts, anchor, keepAways))
            {
                continue;
            }

            double distance = anchor.DistanceTo(preferred);
            double score = grid.CostIn(levelled.Offset(anchor))
                + (penaltyPerTile * distance);
            bool cheaper = score < bestScore - Tolerance;
            bool tiedNearer = Math.Abs(score - bestScore) <= Tolerance
                && distance < bestDistance - Tolerance;
            if (cheaper || tiedNearer)
            {
                best = anchor;
                bestScore = score;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Whether any part of a town, moved to an anchor, comes closer to a
    /// keep-away area than that area allows.
    /// </summary>
    /// <param name="parts">The town's boxes, town-local.</param>
    /// <param name="anchor">The candidate anchor.</param>
    /// <param name="keepAways">The areas to keep away from.</param>
    /// <returns>True if the candidate breaks a keep-away rule.</returns>
    private static bool TooClose(
        IReadOnlyList<Box> parts, Cell anchor, IReadOnlyList<KeepAway> keepAways)
    {
        foreach (KeepAway rule in keepAways)
        {
            foreach (Box part in parts)
            {
                if (part.Offset(anchor).DistanceTo(rule.Area) < rule.MinDistance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The grid cells themselves, lazily.</summary>
    /// <param name="window">The window.</param>
    /// <param name="step">The spacing, already checked.</param>
    /// <returns>The candidates, row by row from the south-west.</returns>
    private static IEnumerable<Cell> GridCells(Box window, int step)
    {
        if (!window.IsValid)
        {
            yield break;
        }

        for (int z = window.MinZ; z <= window.MaxZ; z += step)
        {
            for (int x = window.MinX; x <= window.MaxX; x += step)
            {
                yield return new Cell(x, z);
            }
        }
    }
}
