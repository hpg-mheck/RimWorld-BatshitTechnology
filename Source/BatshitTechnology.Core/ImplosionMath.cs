using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>
/// The arithmetic of an implosion: the singularity pull that the Ergonomic
/// Singularity Grenade, the Tractor Bomb and the Colony Bomb share. How far
/// the pull has come, how far its reach to rip fixtures loose has grown,
/// which cells count as gathered, and which steps take a thing closer.
/// The pull rate and the first-choice steps are
/// <see cref="SingularityPull"/>'s. Kept free of RimWorld types so it can
/// be unit tested.
/// </summary>
public static class ImplosionMath
{
    /// <summary>
    /// The eight neighbouring steps, in the order that breaks ties between
    /// equally good sideways steps: clockwise from north.
    /// </summary>
    private static readonly (int Dx, int Dz)[] Neighbours =
    {
        (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1),
    };

    /// <summary>
    /// The fraction of the pull's ramp that has elapsed: what
    /// <see cref="SingularityPull.Rate"/> takes.
    /// </summary>
    /// <param name="elapsedTicks">Ticks the pull has run.</param>
    /// <param name="pullTicks">
    /// Ticks the rate takes to accelerate from start to end.
    /// </param>
    /// <returns>
    /// The fraction, in [0, 1]; 1 when <paramref name="pullTicks"/> is not
    /// positive.
    /// </returns>
    public static double Progress(int elapsedTicks, int pullTicks)
    {
        if (pullTicks <= 0)
        {
            return 1;
        }

        double fraction = elapsedTicks / (double)pullTicks;
        return fraction < 0 ? 0 : fraction > 1 ? 1 : fraction;
    }

    /// <summary>
    /// Whether an implosion is over: at its maximum duration however things
    /// stand, or, once its minimum has passed, as soon as everything in
    /// reach is gathered or nothing more can move. Before the minimum it
    /// keeps pulling whatever the state, so there is always a show.
    /// </summary>
    /// <param name="elapsedTicks">Ticks it has run.</param>
    /// <param name="minDurationTicks">
    /// Its minimum, in ticks; negative counts as 0, and more than the
    /// maximum as the maximum.
    /// </param>
    /// <param name="maxDurationTicks">
    /// Its maximum, in ticks; taken as 1 if smaller.
    /// </param>
    /// <param name="allGathered">Everything in reach is gathered.</param>
    /// <param name="settled">Nothing more can move.</param>
    /// <returns>True if it should end now.</returns>
    public static bool IsOver(
        int elapsedTicks, int minDurationTicks, int maxDurationTicks,
        bool allGathered, bool settled)
    {
        int max = Math.Max(1, maxDurationTicks);
        int min = Math.Min(max, Math.Max(0, minDurationTicks));
        return elapsedTicks >= max
            || (elapsedTicks >= min && (allGathered || settled));
    }

    /// <summary>
    /// How far from the centre the pull has reached to rip fixtures loose:
    /// it grows steadily from nothing to the full radius.
    /// </summary>
    /// <param name="elapsedTicks">Ticks the pull has run.</param>
    /// <param name="ripTicks">
    /// Ticks the reach takes to grow to the full radius; 0 or less rips
    /// the whole radius at once.
    /// </param>
    /// <param name="radius">The pull radius, in tiles.</param>
    /// <returns>
    /// The reach, in tiles, in [0, <paramref name="radius"/>]; 0 for a
    /// radius that is not positive.
    /// </returns>
    public static double RipReach(
        int elapsedTicks, int ripTicks, double radius)
    {
        double full = radius > 0 ? radius : 0; // NaN counts as nothing
        if (ripTicks <= 0)
        {
            return full;
        }

        if (elapsedTicks <= 0)
        {
            return 0;
        }

        return full * Math.Min(1, elapsedTicks / (double)ripTicks);
    }

    /// <summary>
    /// Whether a cell counts as gathered: within the gather radius of the
    /// centre, by the same measure RimWorld's <c>InHorDistOf</c> uses. A
    /// radius of 0 gathers only the centre; 1.5 gathers the 3 by 3 block
    /// around it.
    /// </summary>
    /// <param name="dx">The cell's x offset from the centre.</param>
    /// <param name="dz">The cell's z offset from the centre.</param>
    /// <param name="gatherRadius">
    /// The gather radius, in tiles; negative or NaN counts as 0.
    /// </param>
    /// <returns>True if the cell is gathered.</returns>
    public static bool IsGathered(int dx, int dz, double gatherRadius)
    {
        double r = gatherRadius > 0 ? gatherRadius : 0;
        double squared = ((double)dx * dx) + ((double)dz * dz);
        return squared <= r * r;
    }

    /// <summary>
    /// The steps that take a thing closer to the centre, best first. These
    /// are <see cref="SingularityPull.StepsToward"/>'s, then, for a thing
    /// still outside the gather radius, any other step that lands inside
    /// it, nearest the centre first. The extra steps let a thing waiting
    /// behind a full cell slip sideways into the pile instead of queueing
    /// outside it. With a gather radius of 0 they add nothing, so the
    /// grenade moves exactly as it always has. Every step brings the thing
    /// strictly closer to the centre.
    /// </summary>
    /// <param name="x">The thing's x.</param>
    /// <param name="z">The thing's z.</param>
    /// <param name="centerX">The centre's x.</param>
    /// <param name="centerZ">The centre's z.</param>
    /// <param name="gatherRadius">
    /// The gather radius, in tiles; negative or NaN counts as 0.
    /// </param>
    /// <returns>(dx, dz) steps, best first; none at the centre.</returns>
    public static IReadOnlyList<(int Dx, int Dz)> StepsInward(
        int x, int z, int centerX, int centerZ, double gatherRadius)
    {
        IReadOnlyList<(int Dx, int Dz)> toward =
            SingularityPull.StepsToward(x, z, centerX, centerZ);
        int ox = x - centerX;
        int oz = z - centerZ;
        if (toward.Count == 0 || IsGathered(ox, oz, gatherRadius))
        {
            return toward;
        }

        var steps = new List<(int Dx, int Dz)>(toward);
        var slips = new List<(int Dx, int Dz)>();
        foreach (var (dx, dz) in Neighbours)
        {
            if (IsGathered(ox + dx, oz + dz, gatherRadius)
                && !steps.Contains((dx, dz)))
            {
                slips.Add((dx, dz));
            }
        }

        // OrderBy is stable: equally near slips keep the clockwise order.
        steps.AddRange(
            slips.OrderBy(s => DistanceSquared(ox + s.Dx, oz + s.Dz)));
        return steps;
    }

    /// <summary>The squared distance of an offset from the centre.</summary>
    /// <param name="dx">The x offset.</param>
    /// <param name="dz">The z offset.</param>
    /// <returns>dx squared plus dz squared.</returns>
    private static long DistanceSquared(int dx, int dz) =>
        ((long)dx * dx) + ((long)dz * dz);
}
