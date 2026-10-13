using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>
/// Reduces everything a town touches to a short list of boxes
/// (<see cref="TownPlan.Parts"/>): each row's runs of touched cells, with
/// runs that line up in consecutive rows merged into one box.
/// </summary>
internal static class TownParts
{
    /// <summary>The boxes covering a town's cells.</summary>
    /// <param name="terrain">Its terrain.</param>
    /// <param name="placements">Its placements.</param>
    /// <param name="buildings">Its buildings (their outer footprints).</param>
    /// <param name="labels">Its labels.</param>
    /// <returns>
    /// Disjoint boxes whose union is exactly the touched cells, south-west
    /// first.
    /// </returns>
    /// <exception cref="ArgumentNullException">Any list is null.</exception>
    internal static IReadOnlyList<Box> From(
        IEnumerable<TerrainPlacement> terrain,
        IEnumerable<Placement> placements,
        IEnumerable<BuildingPlan> buildings,
        IEnumerable<LabelPlan> labels)
    {
        if (terrain == null || placements == null || buildings == null
            || labels == null)
        {
            throw new ArgumentNullException(
                terrain == null ? nameof(terrain)
                : placements == null ? nameof(placements)
                : buildings == null ? nameof(buildings)
                : nameof(labels));
        }

        var cells = new HashSet<Cell>();
        foreach (TerrainPlacement t in terrain)
        {
            cells.Add(t.At);
        }

        foreach (Placement p in placements)
        {
            cells.UnionWith(p.Occupied.Cells());
        }

        foreach (BuildingPlan b in buildings)
        {
            cells.UnionWith(b.Outer.Cells());
        }

        foreach (LabelPlan l in labels)
        {
            cells.Add(l.At);
        }

        return Merge(cells);
    }

    /// <summary>Covers a set of cells with row runs merged vertically.</summary>
    /// <param name="cells">The cells.</param>
    /// <returns>The boxes, ordered by south edge, then west edge.</returns>
    internal static IReadOnlyList<Box> Merge(ICollection<Cell> cells)
    {
        var done = new List<Box>();
        var open = new Dictionary<(int MinX, int MaxX), Box>();
        foreach (IGrouping<int, Cell> row in cells
            .GroupBy(c => c.Z)
            .OrderBy(g => g.Key))
        {
            int z = row.Key;
            var next = new Dictionary<(int MinX, int MaxX), Box>();
            foreach ((int minX, int maxX) in Runs(row.Select(c => c.X)))
            {
                (int, int) span = (minX, maxX);
                if (open.TryGetValue(span, out Box above) && above.MaxZ == z - 1)
                {
                    next[span] = above with { MaxZ = z };
                    open.Remove(span);
                }
                else
                {
                    next[span] = new Box(minX, z, maxX, z);
                }
            }

            done.AddRange(open.Values);
            open = next;
        }

        done.AddRange(open.Values);
        return done
            .OrderBy(b => b.MinZ)
            .ThenBy(b => b.MinX)
            .ToList();
    }

    /// <summary>The runs of consecutive values in a row.</summary>
    /// <param name="xs">The row's x values, in any order, repeats allowed.</param>
    /// <returns>Each run's first and last x, west first.</returns>
    private static IEnumerable<(int MinX, int MaxX)> Runs(IEnumerable<int> xs)
    {
        int[] sorted = xs.Distinct().OrderBy(x => x).ToArray();
        int i = 0;
        while (i < sorted.Length)
        {
            int j = i;
            while (j + 1 < sorted.Length && sorted[j + 1] == sorted[j] + 1)
            {
                j++;
            }

            yield return (sorted[i], sorted[j]);
            i = j + 1;
        }
    }
}
