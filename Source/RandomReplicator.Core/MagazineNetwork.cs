using System;
using System.Collections.Generic;

namespace RandomReplicator.Core;

/// <summary>
/// Which Ballista Magazines feed a Ballista. A magazine is connected if it
/// shares an edge with the Ballista, or with another connected magazine.
/// One magazine can therefore serve every Ballista its chain touches.
/// Cells are (x, z) map coordinates; kept free of RimWorld types so the
/// rule can be unit tested.
/// </summary>
public static class MagazineNetwork
{
    private static readonly (int dx, int dz)[] Edges = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>
    /// Finds every magazine cell reachable from the Ballista through shared
    /// edges.
    /// </summary>
    /// <param name="ballistaCells">Cells the Ballista occupies.</param>
    /// <param name="magazineCells">Cells holding a working magazine.</param>
    /// <returns>The connected magazine cells, nearest chain links first.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either collection is null.
    /// </exception>
    public static List<(int x, int z)> Connected(
        IEnumerable<(int x, int z)> ballistaCells,
        ISet<(int x, int z)> magazineCells)
    {
        if (ballistaCells == null)
        {
            throw new ArgumentNullException(nameof(ballistaCells));
        }

        if (magazineCells == null)
        {
            throw new ArgumentNullException(nameof(magazineCells));
        }

        var connected = new List<(int x, int z)>();
        var seen = new HashSet<(int x, int z)>();
        var frontier = new Queue<(int x, int z)>();
        foreach (var cell in ballistaCells)
        {
            seen.Add(cell);
            frontier.Enqueue(cell);
        }

        while (frontier.Count > 0)
        {
            var (x, z) = frontier.Dequeue();
            foreach (var (dx, dz) in Edges)
            {
                var next = (x + dx, z + dz);
                if (magazineCells.Contains(next) && seen.Add(next))
                {
                    connected.Add(next);
                    frontier.Enqueue(next);
                }
            }
        }

        return connected;
    }
}
