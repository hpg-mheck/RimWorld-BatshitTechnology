using System;

namespace BatshitTechnology.Core;

/// <summary>
/// How far a Tractor Bomb's tractor beam (the implosion's pull) reaches:
/// one of three named stops (docs/TRACTOR-BOMB.md, "Named Settings").
/// Saved by name, so the names must not change.
/// </summary>
public enum TractorBombPull
{
    /// <summary>9 tiles: only the closest of friends.</summary>
    Intimate = 0,

    /// <summary>22.5 tiles, the default: the whole street.</summary>
    Neighborly = 1,

    /// <summary>33.75 tiles: everyone, whether invited or not.</summary>
    Popular = 2,
}

/// <summary>
/// How far a Tractor Bomb's blast reaches once everything is gathered:
/// one of three named stops. Saved by name, so the names must not change.
/// </summary>
public enum TractorBombBlast
{
    /// <summary>3 tiles.</summary>
    BirthdayCake = 0,

    /// <summary>7.5 tiles, the default.</summary>
    Firework = 1,

    /// <summary>12 tiles.</summary>
    BlockParty = 2,
}

/// <summary>
/// The Tractor Bomb's yield: the radius, in tiles, of each named pull and
/// blast; which ones a bomb uses before and after Dial-A-Yield; and how an
/// old save's dialled number finds its stop. Kept free of RimWorld types
/// so it can be unit tested.
/// </summary>
/// <remarks>
/// The radii are exact, not rounded: RimWorld's radial patterns and
/// explosions take fractional radii (vanilla's own use 2.9 and 4.9). The
/// pulls were raised by half on 2026-10-15 (the operator: "expand the pull
/// radii for all tractor levels by 50%"), from 6, 15 and 22.5 tiles; the
/// blasts were not. The longest pull plus the flee margin, 41.75 tiles,
/// stays well inside RimWorld's radial pattern (about 79.8 tiles).
/// Every radius that reaches a bomb comes from a stop here, and every
/// stop read from a save passes through <see cref="Sanitize(TractorBombPull)"/>
/// or <see cref="Sanitize(TractorBombBlast)"/>, so nothing can stall or
/// crash a pull.
/// </remarks>
public static class TractorBombYield
{
    /// <summary>Intimate's tractor beam, in tiles.</summary>
    public const double IntimatePull = 9;

    /// <summary>Neighborly's tractor beam, in tiles: the default.</summary>
    public const double NeighborlyPull = 22.5;

    /// <summary>Popular's tractor beam, in tiles.</summary>
    public const double PopularPull = 33.75;

    /// <summary>
    /// Intimate's tractor beam before the pulls were raised by half
    /// (2026-10-15), in tiles: what an older save's dialled number was
    /// snapped against, and still is.
    /// </summary>
    public const double LegacyIntimatePull = 6;

    /// <summary>
    /// Neighborly's tractor beam before the pulls were raised by half, in
    /// tiles.
    /// </summary>
    public const double LegacyNeighborlyPull = 15;

    /// <summary>
    /// Popular's tractor beam before the pulls were raised by half, in
    /// tiles.
    /// </summary>
    public const double LegacyPopularPull = 22.5;

    /// <summary>Birthday Cake's blast, in tiles.</summary>
    public const double BirthdayCakeBlast = 3;

    /// <summary>Firework's blast, in tiles: the default.</summary>
    public const double FireworkBlast = 7.5;

    /// <summary>Block Party's blast, in tiles.</summary>
    public const double BlockPartyBlast = 12;

    /// <summary>The shortest tractor beam, in tiles.</summary>
    public const double MinPull = IntimatePull;

    /// <summary>The longest tractor beam, in tiles.</summary>
    public const double MaxPull = PopularPull;

    /// <summary>The smallest blast, in tiles.</summary>
    public const double MinBlast = BirthdayCakeBlast;

    /// <summary>The largest blast, in tiles.</summary>
    public const double MaxBlast = BlockPartyBlast;

    /// <summary>
    /// The pull a new bomb has, and every bomb before Dial-A-Yield.
    /// </summary>
    public const TractorBombPull DefaultPull = TractorBombPull.Neighborly;

    /// <summary>
    /// The blast a new bomb has, and every bomb before Dial-A-Yield.
    /// </summary>
    public const TractorBombBlast DefaultBlast = TractorBombBlast.Firework;

    /// <summary>The default pull's radius, in tiles.</summary>
    public const double DefaultPullRadius = NeighborlyPull;

    /// <summary>The default blast's radius, in tiles.</summary>
    public const double DefaultBlastRadius = FireworkBlast;

    /// <summary>A pull's radius.</summary>
    /// <param name="pull">
    /// The stop; an undefined one counts as the default.
    /// </param>
    /// <returns>The radius, in tiles.</returns>
    public static double Radius(TractorBombPull pull) => Sanitize(pull) switch
    {
        TractorBombPull.Intimate => IntimatePull,
        TractorBombPull.Popular => PopularPull,
        _ => NeighborlyPull,
    };

    /// <summary>A blast's radius.</summary>
    /// <param name="blast">
    /// The stop; an undefined one counts as the default.
    /// </param>
    /// <returns>The radius, in tiles.</returns>
    public static double Radius(TractorBombBlast blast) => Sanitize(blast) switch
    {
        TractorBombBlast.BirthdayCake => BirthdayCakeBlast,
        TractorBombBlast.BlockParty => BlockPartyBlast,
        _ => FireworkBlast,
    };

    /// <summary>
    /// A pull brought into range: a value no stop has (from a save edited
    /// by hand, or a later version's) becomes the default.
    /// </summary>
    /// <param name="pull">The value read.</param>
    /// <returns>A defined stop.</returns>
    public static TractorBombPull Sanitize(TractorBombPull pull) =>
        pull is TractorBombPull.Intimate or TractorBombPull.Neighborly
            or TractorBombPull.Popular
            ? pull
            : DefaultPull;

    /// <summary>
    /// A blast brought into range: a value no stop has becomes the
    /// default.
    /// </summary>
    /// <param name="blast">The value read.</param>
    /// <returns>A defined stop.</returns>
    public static TractorBombBlast Sanitize(TractorBombBlast blast) =>
        blast is TractorBombBlast.BirthdayCake or TractorBombBlast.Firework
            or TractorBombBlast.BlockParty
            ? blast
            : DefaultBlast;

    /// <summary>
    /// The stops a bomb actually uses: the ones set, once Dial-A-Yield is
    /// researched; the defaults before, whatever was set.
    /// </summary>
    /// <param name="pull">The pull set.</param>
    /// <param name="blast">The blast set.</param>
    /// <param name="researched">Whether Dial-A-Yield is researched.</param>
    /// <returns>The pull and blast in effect.</returns>
    public static (TractorBombPull Pull, TractorBombBlast Blast) Effective(
        TractorBombPull pull, TractorBombBlast blast, bool researched) =>
        researched
            ? (Sanitize(pull), Sanitize(blast))
            : (DefaultPull, DefaultBlast);

    /// <summary>
    /// The stop a dialled tractor beam from an older save (3 to 20 whole
    /// tiles, from before the named settings) meant: the nearest of the
    /// stops as they were when such saves were first snapped (6, 15 and
    /// 22.5 tiles), not of today's radii, so 3 to 10 still load as
    /// Intimate, 11 to 18 as Neighborly, and 19 and 20 as Popular. The
    /// stop then pulls at its current radius. A tie goes to the larger
    /// stop; a value that is not a number becomes the default.
    /// </summary>
    /// <param name="tiles">The radius dialled, in tiles.</param>
    /// <returns>The stop.</returns>
    public static TractorBombPull NearestPull(double tiles) =>
        double.IsNaN(tiles)
            ? DefaultPull
            : (TractorBombPull)Nearest(
                tiles, LegacyIntimatePull, LegacyNeighborlyPull,
                LegacyPopularPull);

    /// <summary>
    /// The stop nearest a dialled blast from an older save (2 to 10 whole
    /// tiles). A tie goes to the larger stop; a value that is not a number
    /// becomes the default.
    /// </summary>
    /// <param name="tiles">The radius dialled, in tiles.</param>
    /// <returns>The stop.</returns>
    public static TractorBombBlast NearestBlast(double tiles) =>
        double.IsNaN(tiles)
            ? DefaultBlast
            : (TractorBombBlast)Nearest(
                tiles, BirthdayCakeBlast, FireworkBlast, BlockPartyBlast);

    /// <summary>
    /// A pull radius from anywhere (a planted bomb's, a test's) clamped to
    /// the stops' range; a value that is not a number becomes the
    /// default's radius. Used by arithmetic that takes radii, so a nonsense
    /// value cannot give a nonsense distance.
    /// </summary>
    /// <param name="radius">The radius, in tiles.</param>
    /// <returns>
    /// The radius, within [<see cref="MinPull"/>, <see cref="MaxPull"/>].
    /// </returns>
    public static double ClampPull(double radius) =>
        Clamp(radius, MinPull, MaxPull, DefaultPullRadius);

    /// <summary>
    /// A blast radius from anywhere clamped to the stops' range; a value
    /// that is not a number becomes the default's radius.
    /// </summary>
    /// <param name="radius">The radius, in tiles.</param>
    /// <returns>
    /// The radius, within [<see cref="MinBlast"/>,
    /// <see cref="MaxBlast"/>].
    /// </returns>
    public static double ClampBlast(double radius) =>
        Clamp(radius, MinBlast, MaxBlast, DefaultBlastRadius);

    /// <summary>
    /// Which of three ascending stops lies nearest a value; a tie goes to
    /// the larger.
    /// </summary>
    /// <param name="value">The value, not NaN.</param>
    /// <param name="low">The first stop.</param>
    /// <param name="middle">The second stop.</param>
    /// <param name="high">The third stop.</param>
    /// <returns>0, 1 or 2.</returns>
    private static int Nearest(
        double value, double low, double middle, double high)
    {
        if (value >= (middle + high) / 2)
        {
            return 2;
        }

        return value >= (low + middle) / 2 ? 1 : 0;
    }

    /// <summary>Clamps a radius; NaN becomes the fallback.</summary>
    /// <param name="value">The radius.</param>
    /// <param name="min">The smallest allowed.</param>
    /// <param name="max">The largest allowed.</param>
    /// <param name="fallback">What NaN becomes.</param>
    /// <returns>The radius, in range.</returns>
    private static double Clamp(
        double value, double min, double max, double fallback) =>
        double.IsNaN(value) ? fallback : Math.Max(min, Math.Min(max, value));
}
