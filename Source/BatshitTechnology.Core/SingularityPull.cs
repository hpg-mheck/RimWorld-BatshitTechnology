using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// The arithmetic of the Ergonomic Singularity Grenade's pull: how fast it
/// drags things in as it accelerates, and which way one step takes a thing.
/// Kept free of RimWorld types so it can be unit tested.
/// </summary>
public static class SingularityPull
{
    /// <summary>
    /// The pull rate, in cells per second, part-way through the pull. It
    /// accelerates quadratically from the start rate to the end rate.
    /// </summary>
    /// <param name="progress">Fraction of the pull elapsed, clamped to [0, 1].</param>
    /// <param name="startRate">Cells per second at the start.</param>
    /// <param name="endRate">Cells per second at the end.</param>
    /// <returns>The rate.</returns>
    public static double Rate(double progress, double startRate, double endRate)
    {
        double t = Math.Min(1, Math.Max(0, progress));
        return startRate + ((endRate - startRate) * t * t);
    }

    /// <summary>
    /// How many cells a thing is dragged over the whole pull, if nothing
    /// blocks it: the integral of <see cref="Rate"/>.
    /// </summary>
    /// <param name="seconds">The pull's length, in seconds.</param>
    /// <param name="startRate">Cells per second at the start.</param>
    /// <param name="endRate">Cells per second at the end.</param>
    /// <returns>The distance, in cells.</returns>
    public static double TotalCells(double seconds, double startRate, double endRate) =>
        seconds * (startRate + ((endRate - startRate) / 3));

    /// <summary>
    /// The vortex's size multiplier in its final moments: 1 until the last
    /// <paramref name="pulseSeconds"/>, then a throb of
    /// <paramref name="cycles"/> swells whose strength grows toward zero.
    /// </summary>
    /// <param name="secondsLeft">Seconds until it explodes.</param>
    /// <param name="pulseSeconds">How long before the end it starts.</param>
    /// <param name="cycles">How many swells.</param>
    /// <param name="amplitude">The largest swell, as a fraction of size.</param>
    /// <returns>The multiplier, in [1 - amplitude, 1 + amplitude].</returns>
    public static double FinalPulse(
        double secondsLeft, double pulseSeconds = 1, int cycles = 4, double amplitude = 0.2)
    {
        if (pulseSeconds <= 0 || secondsLeft >= pulseSeconds)
        {
            return 1;
        }

        double into = 1 - (Math.Max(0, secondsLeft) / pulseSeconds); // 0 to 1
        double strength = amplitude * (0.35 + (0.65 * into));
        return 1 + (strength * Math.Sin(2 * Math.PI * cycles * into));
    }

    /// <summary>
    /// The steps that take a cell one closer to a centre, best first: the
    /// diagonal (or straight) step along both axes, then each axis alone,
    /// larger gap first, for when the first is blocked.
    /// </summary>
    /// <param name="x">The thing's x.</param>
    /// <param name="z">The thing's z.</param>
    /// <param name="centerX">The centre's x.</param>
    /// <param name="centerZ">The centre's z.</param>
    /// <returns>Up to three (dx, dz) steps; none at the centre.</returns>
    public static IReadOnlyList<(int Dx, int Dz)> StepsToward(int x, int z, int centerX, int centerZ)
    {
        int sx = Math.Sign(centerX - x);
        int sz = Math.Sign(centerZ - z);
        var steps = new List<(int, int)>(3);
        if (sx == 0 && sz == 0)
        {
            return steps;
        }

        steps.Add((sx, sz));
        if (sx != 0 && sz != 0)
        {
            bool xFirst = Math.Abs(centerX - x) >= Math.Abs(centerZ - z);
            steps.Add(xFirst ? (sx, 0) : (0, sz));
            steps.Add(xFirst ? (0, sz) : (sx, 0));
        }

        return steps;
    }
}
