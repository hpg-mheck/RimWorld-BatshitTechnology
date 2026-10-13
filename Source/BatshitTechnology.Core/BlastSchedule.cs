using System;

namespace BatshitTechnology.Core;

/// <summary>
/// Timing and placement arithmetic for the Random Replicator's
/// self-destruct aftermath: when the main blast wave reaches a distance,
/// how many secondary explosions are due each tick, and where they land.
/// Kept free of RimWorld types so it can be unit tested.
/// </summary>
public static class BlastSchedule
{
    /// <summary>
    /// RimWorld's explosion timing: a cell at distance d is hit
    /// <c>d * 1.5 / propagationSpeed</c> ticks after the blast starts
    /// (Verse.Explosion.GetCellAffectTick in 1.6).
    /// </summary>
    public const double TicksPerTileAtUnitSpeed = 1.5;

    /// <summary>
    /// How far the blast wave has travelled after a number of ticks.
    /// </summary>
    /// <param name="ticksSinceStart">Ticks since the explosion began.</param>
    /// <param name="propagationSpeed">
    /// The explosion's propagation speed (1 for a default explosion).
    /// </param>
    /// <returns>
    /// The wave front's distance from the centre in tiles; zero before
    /// the explosion starts.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="propagationSpeed"/> is not positive.
    /// </exception>
    public static double WaveFront(int ticksSinceStart, double propagationSpeed)
    {
        if (propagationSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(propagationSpeed), propagationSpeed,
                "Propagation speed must be positive.");
        }

        return Math.Max(0, ticksSinceStart)
            * propagationSpeed / TicksPerTileAtUnitSpeed;
    }

    /// <summary>Ticks the blast wave needs to reach its full radius.</summary>
    /// <param name="radius">Blast radius, in tiles.</param>
    /// <param name="propagationSpeed">The explosion's propagation speed.</param>
    /// <returns>The wave's travel time, rounded up to whole ticks.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="radius"/> is negative or
    /// <paramref name="propagationSpeed"/> is not positive.
    /// </exception>
    public static int WaveTicks(double radius, double propagationSpeed)
    {
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radius), radius, "Radius cannot be negative.");
        }

        if (propagationSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(propagationSpeed), propagationSpeed,
                "Propagation speed must be positive.");
        }

        return (int)Math.Ceiling(
            radius * TicksPerTileAtUnitSpeed / propagationSpeed);
    }

    /// <summary>
    /// Spreads a per-second rate over ticks without drift: adds one tick's
    /// worth to the accumulator and takes out the whole events now due.
    /// </summary>
    /// <param name="accumulator">
    /// Fractional events carried between ticks; updated in place.
    /// </param>
    /// <param name="perSecond">Events per second; negative counts as zero.</param>
    /// <param name="ticksPerSecond">Game ticks per second (60).</param>
    /// <returns>How many events happen this tick.</returns>
    public static int DueThisTick(
        ref double accumulator, double perSecond, int ticksPerSecond = 60)
    {
        if (ticksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticksPerSecond), ticksPerSecond,
                "There must be at least one tick per second.");
        }

        accumulator += Math.Max(0, perSecond) / ticksPerSecond;
        int due = (int)Math.Floor(accumulator + 1e-9);
        accumulator -= due;
        return due;
    }

    /// <summary>
    /// A point spread evenly over the area of an annulus (a disc with the
    /// middle removed), from two uniform samples in [0, 1).
    /// </summary>
    /// <param name="outerRadius">The annulus's outer radius.</param>
    /// <param name="innerFraction">
    /// Inner radius as a fraction of the outer, clamped to [0, 1]; 0 gives
    /// the whole disc.
    /// </param>
    /// <param name="u1">Uniform sample choosing the distance.</param>
    /// <param name="u2">Uniform sample choosing the angle.</param>
    /// <returns>The point's offset from the centre, in tiles.</returns>
    public static (double X, double Z) PointInAnnulus(
        double outerRadius, double innerFraction, double u1, double u2)
    {
        double outer = Math.Max(0, outerRadius);
        double inner = Clamp01(innerFraction);
        // Even over area: the squared distance is uniform between the
        // squared radii.
        double minSq = inner * inner;
        double distance = outer * Math.Sqrt(minSq + (1 - minSq) * Clamp01(u1));
        double angle = 2 * Math.PI * Clamp01(u2);
        return (distance * Math.Cos(angle), distance * Math.Sin(angle));
    }

    /// <summary>Clamps a value to [0, 1].</summary>
    /// <param name="value">The value.</param>
    /// <returns>The clamped value.</returns>
    private static double Clamp01(double value) =>
        value < 0 ? 0 : value > 1 ? 1 : value;
}
