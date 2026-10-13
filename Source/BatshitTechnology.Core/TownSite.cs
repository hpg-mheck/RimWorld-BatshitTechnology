using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// A map-sized grid of levelling costs and no-go cells, filled in by the
/// game, that answers box queries in constant time once frozen.
/// </summary>
public sealed class SiteGrid
{
    /// <summary>Creates an empty grid.</summary>
    /// <param name="width">Map width (x).</param>
    /// <param name="height">Map height (z).</param>
    /// <exception cref="ArgumentOutOfRangeException">A size below 1.</exception>
    public SiteGrid(int width, int height) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

    /// <summary>Map width.</summary>
    public int Width { get; }

    /// <summary>Map height.</summary>
    public int Height { get; }

    /// <summary>Adds to a cell's levelling cost (before <see cref="Freeze"/>).</summary>
    /// <param name="x">Map x.</param>
    /// <param name="z">Map z.</param>
    /// <param name="cost">The cost to add; non-negative.</param>
    public void AddCost(int x, int z, int cost) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

    /// <summary>Marks a cell no-go (before <see cref="Freeze"/>).</summary>
    /// <param name="x">Map x.</param>
    /// <param name="z">Map z.</param>
    public void Block(int x, int z) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

    /// <summary>Builds the summed-area tables; the grid is read-only afterwards.</summary>
    public void Freeze() =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

    /// <summary>The total cost inside a box, clipped to the map.</summary>
    /// <param name="mapBox">The box, in map coordinates.</param>
    /// <returns>The total cost.</returns>
    public long CostIn(Box mapBox) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

    /// <summary>Whether any no-go cell lies inside a box, clipped to the map.</summary>
    /// <param name="mapBox">The box, in map coordinates.</param>
    /// <returns>True if any cell is blocked.</returns>
    public bool AnyBlockedIn(Box mapBox) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");
}

/// <summary>An area a town must keep a distance from.</summary>
/// <param name="Area">The area, in map coordinates.</param>
/// <param name="MinDistance">The least allowed gap, in cells.</param>
public readonly record struct KeepAway(Box Area, double MinDistance);

/// <summary>Picks where on a map to build a town.</summary>
public static class TownSite
{
    /// <summary>
    /// Candidate anchors on a regular grid inside a window.
    /// </summary>
    /// <param name="window">The window, in map coordinates.</param>
    /// <param name="step">Spacing, in cells; at least 1.</param>
    /// <returns>The candidates, south-west first.</returns>
    public static IEnumerable<Cell> Grid(Box window, int step) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

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
    public static Cell? Best(
        SiteGrid grid, Box bounds, Box core, int firebreak, IEnumerable<Cell> candidates,
        Cell preferred, int edgeMargin, IReadOnlyList<KeepAway> keepAways, int penaltyPerTile = 10) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");
}
