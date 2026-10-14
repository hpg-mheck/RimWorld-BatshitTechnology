using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// The settings of one implosion, the singularity pull shared by the
/// Ergonomic Singularity Grenade, the Tractor Bomb and the Colony Bomb.
/// The defaults are the grenade's. The fields are public and lower-case so
/// that a Def can carry these settings in XML, as RimWorld expects of the
/// classes it loads, and so that a host can adjust a copy (Dial-A-Yield).
/// </summary>
/// <remarks>
/// Hosts never trust these values as given: the implosion works from
/// <see cref="Normalized"/>, and Defs report <see cref="Problems"/> as
/// configuration errors.
/// </remarks>
public sealed class ImplosionSettings
{
    /// <summary>The smallest pull radius, in tiles.</summary>
    public const double MinPullRadius = 1;

    /// <summary>
    /// The fastest drag, in cells per second: a step every tick. More would
    /// repeat work within a single tick and teleport things across the map.
    /// </summary>
    public const double MaxRate = 60;

    /// <summary>
    /// How far it reaches, in tiles: pawns, items and fixtures within this
    /// radius of the centre are caught.
    /// </summary>
    public float pullRadius = 10f;

    /// <summary>
    /// How near the centre a thing must come to count as gathered, in tiles.
    /// 0 (the grenade) gathers only the centre, so it pulls as close as it
    /// can; 1.5 (the Tractor Bomb) is the 3 by 3 block around it.
    /// </summary>
    public float gatherRadius;

    /// <summary>
    /// Ticks the drag takes to accelerate from its start rate to its end
    /// rate. How long the implosion runs is
    /// <see cref="minDurationTicks"/> and <see cref="maxDurationTicks"/>.
    /// </summary>
    public int pullTicks = 300;

    /// <summary>Drag speed at the start, in cells per second.</summary>
    public float pullRateStart = 0.6f;

    /// <summary>
    /// Drag speed at the end of <see cref="pullTicks"/>, and after it, in
    /// cells per second.
    /// </summary>
    public float pullRateEnd = 12f;

    /// <summary>
    /// Whether it rips fixtures loose: buildings uninstalled or torn down,
    /// plants uprooted.
    /// </summary>
    public bool ripFixtures = true;

    /// <summary>
    /// Ticks for its reach to rip fixtures loose to grow from the centre to
    /// the full radius; 0 rips the whole radius at once.
    /// </summary>
    public int ripTicks = 120;

    /// <summary>
    /// The shortest it runs, in ticks: before this it keeps pulling even if
    /// everything is already gathered or settled, so there is always a
    /// show. The grenade's (the default) equals its maximum.
    /// </summary>
    public int minDurationTicks = 300;

    /// <summary>
    /// The longest it runs, in ticks: at this it ends however things
    /// stand, so a balance point or a tug-of-war between two pulls can
    /// never hold it open for ever. The default is the grenade's five
    /// seconds.
    /// </summary>
    public int maxDurationTicks = 300;

    /// <summary>An independent copy, for a host to adjust.</summary>
    /// <returns>The copy.</returns>
    public ImplosionSettings Copy() => (ImplosionSettings)MemberwiseClone();

    /// <summary>
    /// What is wrong with these settings, for a Def's configuration errors.
    /// Nothing is wrong with the result of <see cref="Normalized"/>.
    /// </summary>
    /// <param name="maxRadius">
    /// The engine's largest radius (RimWorld's
    /// <c>GenRadial.MaxRadialPatternRadius</c>).
    /// </param>
    /// <returns>
    /// One message per problem; none when the settings are sound.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxRadius"/> is below
    /// <see cref="MinPullRadius"/> or not a number.
    /// </exception>
    public IEnumerable<string> Problems(double maxRadius)
    {
        CheckMaxRadius(maxRadius);
        return ProblemsIn(maxRadius);
    }

    /// <summary>
    /// A copy with every value brought within bounds, so that no setting
    /// can stall or crash an implosion: the radii within the engine's
    /// limit (the gather radius within the pull radius), the timings not
    /// negative, the rates within [0, <see cref="MaxRate"/>] and never
    /// slowing down, and the durations at least a tick with the minimum
    /// no longer than the maximum. A value that is not a number falls back
    /// to the smallest it may be (the end rate to the start rate).
    /// </summary>
    /// <param name="maxRadius">
    /// The engine's largest radius (RimWorld's
    /// <c>GenRadial.MaxRadialPatternRadius</c>).
    /// </param>
    /// <returns>The normalized copy; this instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxRadius"/> is below
    /// <see cref="MinPullRadius"/> or not a number.
    /// </exception>
    public ImplosionSettings Normalized(double maxRadius)
    {
        CheckMaxRadius(maxRadius);
        ImplosionSettings n = Copy();
        n.pullRadius =
            (float)Clamp(pullRadius, MinPullRadius, Limit(maxRadius));
        n.gatherRadius = (float)Clamp(gatherRadius, 0, n.pullRadius);
        n.pullTicks = Math.Max(1, pullTicks);
        n.ripTicks = Math.Max(0, ripTicks);
        n.pullRateStart = (float)Clamp(pullRateStart, 0, MaxRate);
        n.pullRateEnd = (float)Clamp(
            double.IsNaN(pullRateEnd) ? n.pullRateStart : pullRateEnd,
            n.pullRateStart, MaxRate);
        n.maxDurationTicks = Math.Max(1, maxDurationTicks);
        n.minDurationTicks =
            Math.Min(n.maxDurationTicks, Math.Max(0, minDurationTicks));
        return n;
    }

    /// <summary>
    /// The checks behind <see cref="Problems"/>, run lazily.
    /// </summary>
    /// <param name="maxRadius">
    /// The engine's largest radius, already checked.
    /// </param>
    /// <returns>One message per problem.</returns>
    private IEnumerable<string> ProblemsIn(double maxRadius)
    {
        if (!(pullRadius >= MinPullRadius && pullRadius <= Limit(maxRadius)))
        {
            yield return "pull radius must be at least 1 tile and within "
                + "the engine maximum";
        }

        if (!(gatherRadius >= 0 && gatherRadius <= pullRadius))
        {
            yield return
                "gather radius must be between 0 and the pull radius";
        }

        if (pullTicks < 1 || ripTicks < 0)
        {
            yield return "pull must last at least a tick and rip time "
                + "must not be negative";
        }

        if (!(pullRateStart >= 0 && pullRateEnd >= pullRateStart
            && pullRateEnd <= MaxRate))
        {
            yield return "pull rate must not be negative, slow down or "
                + "exceed a cell per tick";
        }

        if (maxDurationTicks < 1 || minDurationTicks < 0
            || minDurationTicks > maxDurationTicks)
        {
            yield return "duration must last at least a tick, with its "
                + "minimum between 0 and its maximum";
        }
    }

    /// <summary>Rejects a nonsensical engine radius limit.</summary>
    /// <param name="maxRadius">The limit.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when it is below <see cref="MinPullRadius"/> or not a number.
    /// </exception>
    private static void CheckMaxRadius(double maxRadius)
    {
        if (!(maxRadius >= MinPullRadius))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRadius), maxRadius,
                "The engine's radius limit must be at least the "
                    + "smallest pull.");
        }
    }

    /// <summary>
    /// The engine's radius limit as the radius fields hold it: a float, as
    /// RimWorld's own limit is, so a radius clamped to it passes the check.
    /// </summary>
    /// <param name="maxRadius">The limit, already checked.</param>
    /// <returns>
    /// The limit, rounded to a float but never below the minimum.
    /// </returns>
    private static double Limit(double maxRadius) =>
        Math.Max(MinPullRadius, (float)maxRadius);

    /// <summary>
    /// Clamps a value to a range; a value that is not a number becomes the
    /// minimum.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="min">The minimum.</param>
    /// <param name="max">The maximum, at least the minimum.</param>
    /// <returns>The clamped value.</returns>
    private static double Clamp(double value, double min, double max) =>
        double.IsNaN(value) ? min
            : value < min ? min
            : value > max ? max
            : value;
}
