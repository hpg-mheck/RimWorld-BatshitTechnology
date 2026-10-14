using System;

namespace BatshitTechnology.Core;

/// <summary>
/// The arithmetic of a Tractor Bomb's death (docs/TRACTOR-BOMB.md): its
/// parts burst out of the wreck, hang for a moment, then spiral back into
/// a sputtering miniature vortex, faster and smaller as they go, until it
/// pops. Kept free of RimWorld types so it can be unit tested. Times are
/// game ticks; distances are tiles from the vortex.
/// </summary>
public static class DeathVortexMath
{
    /// <summary>
    /// Ticks the parts take to burst out to their full reach.
    /// </summary>
    public const int FlingTicks = 18;

    /// <summary>Ticks the parts hang at their full reach.</summary>
    public const int HangTicks = 12;

    /// <summary>Ticks the vortex takes to suck its parts back in.</summary>
    public const int SuckTicks = 96;

    /// <summary>Total ticks from death to the pop.</summary>
    public const int TotalTicks = FlingTicks + HangTicks + SuckTicks;

    /// <summary>Turns a part makes on its way in.</summary>
    public const double SuckTurns = 1.25;

    /// <summary>A part's size at the end, as a fraction of its own.</summary>
    public const double FinalScale = 0.15;

    /// <summary>
    /// Where a part is, and how big, some ticks after the death.
    /// </summary>
    /// <param name="reach">
    /// How far the part bursts out, in tiles; negative or NaN as 0.
    /// </param>
    /// <param name="angle">
    /// The direction it bursts out in, in radians; NaN as 0.
    /// </param>
    /// <param name="ticks">
    /// Ticks since the death; clamped to the timeline.
    /// </param>
    /// <returns>
    /// Its offset from the vortex (x and z, in tiles) and its scale: 1
    /// while it flies out and hangs, shrinking to
    /// <see cref="FinalScale"/> as it is sucked in.
    /// </returns>
    public static (double X, double Z, double Scale) PartAt(
        double reach, double angle, int ticks)
    {
        double r0 = reach > 0 ? reach : 0;
        double a0 = double.IsNaN(angle) ? 0 : angle;
        int t = Math.Max(0, Math.Min(TotalTicks, ticks));
        double radius;
        double turn = 0;
        double scale = 1;
        if (t <= FlingTicks)
        {
            // Out fast, easing to a stop.
            double p = t / (double)FlingTicks;
            radius = r0 * (1 - ((1 - p) * (1 - p)));
        }
        else if (t <= FlingTicks + HangTicks)
        {
            radius = r0;
        }
        else
        {
            // In slowly at first, then faster, spinning up as it goes.
            double p = SuckProgress(t);
            radius = r0 * (1 - (p * p));
            turn = SuckTurns * 2 * Math.PI * p * p;
            scale = 1 - ((1 - FinalScale) * p);
        }

        double a = a0 + turn;
        return (radius * Math.Cos(a), radius * Math.Sin(a), scale);
    }

    /// <summary>
    /// How far the suck has come: 0 until the parts start back, 1 at the
    /// pop.
    /// </summary>
    /// <param name="ticks">Ticks since the death.</param>
    /// <returns>The progress, in [0, 1].</returns>
    public static double SuckProgress(int ticks)
    {
        double into = ticks - (FlingTicks + HangTicks);
        return into <= 0 ? 0 : into >= SuckTicks ? 1 : into / SuckTicks;
    }

    /// <summary>
    /// The vortex's size, as a fraction of its full size: it opens as the
    /// parts hang, then sputters, flickering between about 55% and 115% of
    /// its growing size, never quite steady.
    /// </summary>
    /// <param name="ticks">Ticks since the death.</param>
    /// <param name="seed">
    /// Varies the sputter from one death to the next.
    /// </param>
    /// <returns>The size factor, in [0, 1.15].</returns>
    public static double VortexSize(int ticks, int seed)
    {
        if (ticks <= FlingTicks)
        {
            return 0;
        }

        double open = Math.Min(1, (ticks - FlingTicks) / (double)HangTicks);
        // A cheap, repeatable flicker: a hash of the tick and the seed.
        uint h = unchecked((uint)((ticks / 3 * 73856093) ^ (seed * 19349663)));
        h ^= h >> 13;
        h = unchecked(h * 0x5bd1e995);
        h ^= h >> 15;
        double flicker = 0.55 + (0.6 * (h % 1000) / 999.0);
        return open * flicker;
    }

    /// <summary>
    /// The direction the i-th of n parts bursts out in: spread evenly
    /// around the wreck, nudged by a jitter so no two deaths look alike.
    /// </summary>
    /// <param name="index">The part, from 0.</param>
    /// <param name="count">How many parts; taken as 1 if smaller.</param>
    /// <param name="jitter">
    /// A random number in [0, 1); NaN as 0. Shifts the part by up to half
    /// its share of the circle.
    /// </param>
    /// <returns>The angle, in radians.</returns>
    public static double PartAngle(int index, int count, double jitter)
    {
        int n = Math.Max(1, count);
        double share = 2 * Math.PI / n;
        double j = double.IsNaN(jitter) ? 0 : Math.Min(1, Math.Max(0, jitter));
        return (index * share) + ((j - 0.5) * share * 0.5);
    }
}
