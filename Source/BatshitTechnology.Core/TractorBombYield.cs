using System;

namespace BatshitTechnology.Core;

/// <summary>
/// The Tractor Bomb's yield: how far its tractor beam (the implosion's
/// pull) reaches and how far its blast reaches, in tiles. The defaults are
/// fixed; Tractor Bomb Dial-A-Yield lets the player dial each one, in whole
/// tiles, within its own range (docs/TRACTOR-BOMB.md). Kept free of
/// RimWorld types so it can be unit tested.
/// </summary>
/// <remarks>
/// Every value that reaches a bomb passes through here: a slider, a save
/// edited by hand and a later version's save alike come out as a whole
/// number of tiles within range, so nothing can stall or crash a pull.
/// </remarks>
public static class TractorBombYield
{
    /// <summary>The shortest tractor beam, in tiles.</summary>
    public const int MinPull = 3;

    /// <summary>The longest tractor beam, in tiles.</summary>
    public const int MaxPull = 20;

    /// <summary>The tractor beam before anyone dials it, in tiles.</summary>
    public const int DefaultPull = 10;

    /// <summary>The smallest blast, in tiles.</summary>
    public const int MinBlast = 2;

    /// <summary>The largest blast, in tiles.</summary>
    public const int MaxBlast = 10;

    /// <summary>The blast before anyone dials it, in tiles.</summary>
    public const int DefaultBlast = 5;

    /// <summary>
    /// A pull radius brought within range: rounded to the nearest whole
    /// tile and clamped to [<see cref="MinPull"/>, <see cref="MaxPull"/>].
    /// A value that is not a number becomes the default.
    /// </summary>
    /// <param name="pull">The radius asked for, in tiles.</param>
    /// <returns>The radius the bomb will use.</returns>
    public static int ClampPull(double pull) =>
        Snap(pull, MinPull, MaxPull, DefaultPull);

    /// <summary>
    /// A blast radius brought within range: rounded to the nearest whole
    /// tile and clamped to [<see cref="MinBlast"/>, <see cref="MaxBlast"/>].
    /// A value that is not a number becomes the default.
    /// </summary>
    /// <param name="blast">The radius asked for, in tiles.</param>
    /// <returns>The radius the bomb will use.</returns>
    public static int ClampBlast(double blast) =>
        Snap(blast, MinBlast, MaxBlast, DefaultBlast);

    /// <summary>
    /// The radii a bomb actually uses: the dialled ones, brought within
    /// range, once Dial-A-Yield is researched; the defaults before, whatever
    /// was dialled.
    /// </summary>
    /// <param name="pull">The dialled pull radius, in tiles.</param>
    /// <param name="blast">The dialled blast radius, in tiles.</param>
    /// <param name="researched">Whether Dial-A-Yield is researched.</param>
    /// <returns>The pull and blast radii, in tiles.</returns>
    public static (int Pull, int Blast) Effective(
        double pull, double blast, bool researched) =>
        researched
            ? (ClampPull(pull), ClampBlast(blast))
            : (DefaultPull, DefaultBlast);

    /// <summary>
    /// Where a pull radius sits along its slider, from 0 at the shortest to
    /// 1 at the longest.
    /// </summary>
    /// <param name="pull">The pull radius, in tiles.</param>
    /// <returns>The slider position, in [0, 1].</returns>
    public static double PullFraction(double pull) =>
        Fraction(ClampPull(pull), MinPull, MaxPull);

    /// <summary>
    /// Where a blast radius sits along its slider, from 0 at the smallest
    /// to 1 at the largest.
    /// </summary>
    /// <param name="blast">The blast radius, in tiles.</param>
    /// <returns>The slider position, in [0, 1].</returns>
    public static double BlastFraction(double blast) =>
        Fraction(ClampBlast(blast), MinBlast, MaxBlast);

    /// <summary>The pull radius at a slider position.</summary>
    /// <param name="fraction">
    /// The slider position; clamped to [0, 1], and 0 if not a number.
    /// </param>
    /// <returns>The pull radius, in whole tiles.</returns>
    public static int PullAt(double fraction) =>
        ClampPull(Along(fraction, MinPull, MaxPull));

    /// <summary>The blast radius at a slider position.</summary>
    /// <param name="fraction">
    /// The slider position; clamped to [0, 1], and 0 if not a number.
    /// </param>
    /// <returns>The blast radius, in whole tiles.</returns>
    public static int BlastAt(double fraction) =>
        ClampBlast(Along(fraction, MinBlast, MaxBlast));

    /// <summary>
    /// How many whole-tile steps a slider has: one per tile between its
    /// ends, so every stop is a whole tile.
    /// </summary>
    /// <param name="min">The slider's smallest radius.</param>
    /// <param name="max">The slider's largest radius.</param>
    /// <returns>The number of steps, at least 1.</returns>
    public static int Steps(int min, int max) => Math.Max(1, max - min);

    /// <summary>
    /// Rounds a radius to a whole tile and clamps it; NaN becomes the
    /// fallback.
    /// </summary>
    /// <param name="value">The radius.</param>
    /// <param name="min">The smallest allowed.</param>
    /// <param name="max">The largest allowed.</param>
    /// <param name="fallback">What NaN becomes.</param>
    /// <returns>The whole-tile radius.</returns>
    private static int Snap(double value, int min, int max, int fallback)
    {
        if (double.IsNaN(value))
        {
            return fallback;
        }

        if (value <= min)
        {
            return min;
        }

        if (value >= max)
        {
            return max;
        }

        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    /// <summary>A radius's position between two ends.</summary>
    /// <param name="value">The radius, already in range.</param>
    /// <param name="min">The smallest radius.</param>
    /// <param name="max">The largest radius.</param>
    /// <returns>The position, in [0, 1].</returns>
    private static double Fraction(int value, int min, int max) =>
        (value - min) / (double)Steps(min, max);

    /// <summary>The radius at a position between two ends.</summary>
    /// <param name="fraction">The position; clamped, NaN as 0.</param>
    /// <param name="min">The smallest radius.</param>
    /// <param name="max">The largest radius.</param>
    /// <returns>The radius, not yet rounded.</returns>
    private static double Along(double fraction, int min, int max)
    {
        double f = double.IsNaN(fraction) ? 0
            : fraction < 0 ? 0
            : fraction > 1 ? 1
            : fraction;
        return min + (f * (max - min));
    }
}
