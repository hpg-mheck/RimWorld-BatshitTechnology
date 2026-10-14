using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Showcases;

/// <summary>
/// The beats inside one scene (camera moves, narration lines), each due
/// at a tick counted from the scene's start. Each beat is handed out
/// exactly once, in tick order, ties in declared order, however unevenly
/// the scene is ticked.
/// </summary>
public sealed class BeatSchedule
{
    /// <summary>
    /// Beat indices, sorted by due tick, then declared order.
    /// </summary>
    private readonly List<int> order;

    /// <summary>Each beat's due tick, by declared index.</summary>
    private readonly List<int> dueTicks;

    /// <summary>
    /// How many beats, in sorted order, have been handed out.
    /// </summary>
    private int taken;

    /// <summary>Creates a schedule.</summary>
    /// <param name="dueTicks">
    /// Each beat's due tick from the scene's start, in declared order; a
    /// negative tick is due at once. Null means no beats.
    /// </param>
    public BeatSchedule(IEnumerable<int>? dueTicks)
    {
        this.dueTicks = (dueTicks ?? Enumerable.Empty<int>())
            .Select(t => Math.Max(0, t))
            .ToList();
        order = Enumerable.Range(0, this.dueTicks.Count)
            .OrderBy(i => this.dueTicks[i])
            .ThenBy(i => i)
            .ToList();
    }

    /// <summary>Gets how many beats there are.</summary>
    public int Count => dueTicks.Count;

    /// <summary>Gets how many beats have not been handed out yet.</summary>
    public int Remaining => dueTicks.Count - taken;

    /// <summary>
    /// Hands out every beat due by now that has not been handed out yet.
    /// </summary>
    /// <param name="elapsed">Ticks since the scene began.</param>
    /// <returns>
    /// The beats' declared indices, in the order to fire them.
    /// </returns>
    public IReadOnlyList<int> TakeDue(int elapsed)
    {
        var due = new List<int>();
        while (taken < order.Count && dueTicks[order[taken]] <= elapsed)
        {
            due.Add(order[taken]);
            taken++;
        }

        return due;
    }
}
