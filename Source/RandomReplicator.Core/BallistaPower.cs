using System;

namespace RandomReplicator.Core;

/// <summary>
/// Power draw of the Batshittium Ballista. Range and projectile count
/// multiply into "units" (count x range / 10); power rises in a straight
/// line from the baseline (1 projectile at 10 tiles) to the maximum
/// (256 projectiles at 325 tiles). At maximum, standby is a quarter of one
/// 250 kW reactor and the firing burst leaves room in four reactors for the
/// magazines feeding it.
/// </summary>
public static class BallistaPower
{
    /// <summary>Fewest projectiles per volley.</summary>
    public const int MinCount = 1;

    /// <summary>Most projectiles per volley.</summary>
    public const int MaxCount = 256;

    /// <summary>Shortest selectable range, in tiles.</summary>
    public const int MinRange = 10;

    /// <summary>Longest selectable range: the largest normal map size.</summary>
    public const int MaxRange = 325;

    /// <summary>Standby watts at the baseline.</summary>
    public const double BaseStandbyWatts = 50;

    /// <summary>Standby watts at maximum: 25% of one 250 kW reactor.</summary>
    public const double MaxStandbyWatts = 62_500;

    /// <summary>Firing-burst watts at the baseline.</summary>
    public const double BaseFiringWatts = 1_000;

    /// <summary>
    /// Firing-burst watts at maximum: four reactors' 1 MW, less 10 kW for
    /// up to twenty magazines feeding the volley at 500 W each.
    /// </summary>
    public const double MaxFiringWatts = 990_000;

    /// <summary>Units at the baseline: 1 projectile x 10 tiles / 10.</summary>
    public const double BaseUnits = 1;

    /// <summary>Units at maximum: 256 projectiles x 325 tiles / 10.</summary>
    public const double MaxUnits = MaxCount * (MaxRange / 10d);

    /// <summary>
    /// Range and count combined: count x range / 10, after clamping both to
    /// their selectable limits.
    /// </summary>
    /// <param name="count">Projectiles per volley.</param>
    /// <param name="range">Range in tiles.</param>
    /// <returns>Units, between <see cref="BaseUnits"/> and <see cref="MaxUnits"/>.</returns>
    public static double Units(int count, double range)
    {
        int c = Math.Max(MinCount, Math.Min(MaxCount, count));
        double r = Math.Max(MinRange, Math.Min(MaxRange, range));
        return c * r / 10d;
    }

    /// <summary>Standby draw while the Ballista waits.</summary>
    /// <param name="count">Projectiles per volley.</param>
    /// <param name="range">Range in tiles.</param>
    /// <returns>Watts.</returns>
    public static double StandbyWatts(int count, double range) =>
        Interpolate(BaseStandbyWatts, MaxStandbyWatts, Units(count, range));

    /// <summary>Draw during a volley's firing burst.</summary>
    /// <param name="count">Projectiles per volley.</param>
    /// <param name="range">Range in tiles.</param>
    /// <returns>Watts.</returns>
    public static double FiringWatts(int count, double range) =>
        Interpolate(BaseFiringWatts, MaxFiringWatts, Units(count, range));

    /// <summary>Straight line from the baseline to the maximum.</summary>
    /// <param name="atBase">Value at <see cref="BaseUnits"/>.</param>
    /// <param name="atMax">Value at <see cref="MaxUnits"/>.</param>
    /// <param name="units">Units to evaluate at.</param>
    /// <returns>The interpolated value.</returns>
    private static double Interpolate(double atBase, double atMax, double units) =>
        atBase + ((atMax - atBase) * (units - BaseUnits) / (MaxUnits - BaseUnits));
}
