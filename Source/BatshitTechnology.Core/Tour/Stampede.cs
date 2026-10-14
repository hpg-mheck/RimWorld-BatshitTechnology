using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>
/// A position in tiles that need not be whole: a herd's centre, where it
/// is heading, or where a volley should land.
/// </summary>
/// <param name="X">Tiles east.</param>
/// <param name="Z">Tiles north.</param>
public readonly record struct Spot(double X, double Z)
{
    /// <summary>Gets the distance from the origin, in tiles.</summary>
    public double Length => Math.Sqrt((X * X) + (Z * Z));

    /// <summary>Adds two spots as vectors.</summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The second.</param>
    /// <returns>The sum.</returns>
    public static Spot operator +(Spot a, Spot b) => new(a.X + b.X, a.Z + b.Z);

    /// <summary>Subtracts one spot from another, as vectors.</summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The one taken away.</param>
    /// <returns>The difference.</returns>
    public static Spot operator -(Spot a, Spot b) => new(a.X - b.X, a.Z - b.Z);

    /// <summary>Scales a spot, as a vector.</summary>
    /// <param name="a">The spot.</param>
    /// <param name="k">The factor.</param>
    /// <returns>The scaled spot.</returns>
    public static Spot operator *(Spot a, double k) => new(a.X * k, a.Z * k);

    /// <summary>A cell's spot.</summary>
    /// <param name="c">The cell.</param>
    /// <returns>The spot at the cell.</returns>
    public static Spot Of(Cell c) => new(c.X, c.Z);

    /// <summary>The straight-line distance to another spot.</summary>
    /// <param name="other">The other spot.</param>
    /// <returns>The distance, in tiles.</returns>
    public double DistanceTo(Spot other) => (this - other).Length;

    /// <summary>The nearest cell, rounding each axis.</summary>
    /// <returns>The cell.</returns>
    public Cell ToCell() =>
        new((int)Math.Round(X, MidpointRounding.AwayFromZero),
            (int)Math.Round(Z, MidpointRounding.AwayFromZero));
}

/// <summary>
/// The rules of the tour's boomalope stampede (docs/SHOWCASE.md, scene
/// <c>stampede</c>): where the herd enters, when it counts as too close,
/// where the Ballista aims, and what the tour verifier asks of it. The
/// game's scene and the verifier both read these, so the scene cannot
/// grade itself on easier terms than the test.
/// </summary>
public static class Stampede
{
    /// <summary>The herd the scene sends, unless the def says otherwise.</summary>
    public const int TargetHerd = 200;

    /// <summary>
    /// The smallest herd the scene may send: the operator asked for
    /// hundreds, and fewer only if the frame rate collapses.
    /// </summary>
    public const int MinHerd = 150;

    /// <summary>The largest herd the def may ask for.</summary>
    public const int MaxHerd = 400;

    /// <summary>The first volley: the Ballista's largest.</summary>
    public const int MoneyShotBolts = BallistaPower.MaxCount;

    /// <summary>How many volleys may follow it to clean up stragglers.</summary>
    public const int MaxCleanupVolleys = 2;

    /// <summary>The most volleys the scene fires.</summary>
    public const int MaxVolleys = 1 + MaxCleanupVolleys;

    /// <summary>
    /// How near the base's square a boomalope may come before it counts
    /// as having reached the base, in tiles: a boomalope's own blast
    /// (4.9) five times over, so one stopped there harms no one inside.
    /// </summary>
    public const double KeepAwayTiles = 25;

    /// <summary>
    /// The share of the herd that must be dead before the first one alive
    /// comes inside the keep-away (and by the scene's end, if none does).
    /// </summary>
    public const double DeadShare = 0.9;

    /// <summary>Tiles kept clear between the herd and the map's edge.</summary>
    public const int EdgeMargin = 2;

    /// <summary>The most boomalopes put in one cell when the herd enters.</summary>
    public const int AnimalsPerCell = 2;

    /// <summary>
    /// Watts the grid keeps spare during a volley beyond the firing draw
    /// and the magazines' feeding, so a brownout never cuts a magazine or
    /// the Ballista off mid-burst.
    /// </summary>
    public const double PowerMarginWatts = 10_000;

    /// <summary>
    /// The bands the herd may enter in, tried in order: depth east-west
    /// and half-height north-south, in tiles. The first is a dense front
    /// of about 0.6 boomalopes a cell for the target herd; the others
    /// widen it where water or rock leaves too little open ground.
    /// </summary>
    public static IReadOnlyList<(int Depth, int HalfHeight)> BandSizes { get; } =
        new[] { (8, 20), (12, 30), (16, 45) };

    /// <summary>
    /// How far north of the base's row a band may be slid along the east
    /// edge, tried in order, when rock or water closes the edge level with
    /// the base. North only: the tractor yard's candidates all lie south
    /// of the base (<see cref="TourStagePlanner.DefaultYardOffsets"/>), and
    /// a herd from the south-east would bring the volley down on it.
    /// </summary>
    public static IReadOnlyList<int> BandSlides { get; } =
        new[] { 0, 35, 70 };

    /// <summary>
    /// The band on the map's east edge, level with the base, where the
    /// herd enters, clipped to the map and kept
    /// <see cref="EdgeMargin"/> from its edges.
    /// </summary>
    /// <param name="map">The map's cells.</param>
    /// <param name="baseCentre">The demo base's centre.</param>
    /// <param name="step">Which of <see cref="BandSizes"/> to use.</param>
    /// <returns>The band; empty (not valid) on a map too small for it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An unknown step.</exception>
    public static Box Band(Box map, Cell baseCentre, int step) =>
        Band(map, baseCentre, step, 0);

    /// <summary>
    /// A band on the map's east edge, slid north or south of the base's
    /// row, clipped to the map and kept <see cref="EdgeMargin"/> from its
    /// edges.
    /// </summary>
    /// <param name="map">The map's cells.</param>
    /// <param name="baseCentre">The demo base's centre.</param>
    /// <param name="step">Which of <see cref="BandSizes"/> to use.</param>
    /// <param name="slide">Tiles north (negative: south) of the base's
    /// row.</param>
    /// <returns>The band; empty (not valid) when it falls off the map.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An unknown step.</exception>
    public static Box Band(Box map, Cell baseCentre, int step, int slide)
    {
        if (step < 0 || step >= BandSizes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }

        (int depth, int half) = BandSizes[step];
        int row = baseCentre.Z + slide;
        int maxX = map.MaxX - EdgeMargin;
        int minX = Math.Max(map.MinX + EdgeMargin, maxX - depth + 1);
        int minZ = Math.Max(map.MinZ + EdgeMargin, row - half);
        int maxZ = Math.Min(map.MaxZ - EdgeMargin, row + half);
        return new Box(minX, minZ, maxX, maxZ);
    }

    /// <summary>
    /// Every band the herd may enter in, best first: each size level with
    /// the base, widest last, then each size slid along the edge by
    /// <see cref="BandSlides"/>. Bands that fall off the map, or repeat
    /// one already offered, are left out.
    /// </summary>
    /// <param name="map">The map's cells.</param>
    /// <param name="baseCentre">The demo base's centre.</param>
    /// <returns>The bands.</returns>
    public static IReadOnlyList<Box> Bands(Box map, Cell baseCentre)
    {
        var bands = new List<Box>();
        foreach (int slide in BandSlides)
        {
            for (int step = 0; step < BandSizes.Count; step++)
            {
                Box band = Band(map, baseCentre, step, slide);
                if (band.IsValid && !bands.Contains(band))
                {
                    bands.Add(band);
                }
            }
        }

        return bands;
    }

    /// <summary>
    /// The open cells a herd needs to enter: enough to hold it at
    /// <see cref="AnimalsPerCell"/> a cell.
    /// </summary>
    /// <param name="herd">The herd's size.</param>
    /// <returns>The cells needed; at least one.</returns>
    public static int CellsNeeded(int herd) =>
        Math.Max(1, (int)Math.Ceiling(Math.Max(0, herd) / (double)AnimalsPerCell));

    /// <summary>
    /// A herd size the scene can use: the def's, held between
    /// <see cref="MinHerd"/> and <see cref="MaxHerd"/>.
    /// </summary>
    /// <param name="asked">The def's herd size.</param>
    /// <returns>The size to send.</returns>
    public static int HerdSize(int asked) =>
        Math.Max(MinHerd, Math.Min(MaxHerd, asked));

    /// <summary>The demo base's cleared square.</summary>
    /// <param name="baseCentre">The base's centre.</param>
    /// <returns>The square.</returns>
    public static Box BaseSquare(Cell baseCentre)
    {
        int half = TourStagePlanner.BaseHalfSize;
        return new Box(
            baseCentre.X - half, baseCentre.Z - half,
            baseCentre.X + half, baseCentre.Z + half);
    }

    /// <summary>
    /// Whether a cell is inside the base's keep-away: nearer its square
    /// than <see cref="KeepAwayTiles"/>.
    /// </summary>
    /// <param name="baseCentre">The base's centre.</param>
    /// <param name="cell">The cell.</param>
    /// <returns>True if a boomalope there has reached the base.</returns>
    public static bool InsideKeepAway(Cell baseCentre, Cell cell) =>
        BaseSquare(baseCentre).DistanceTo(cell) < KeepAwayTiles;

    /// <summary>
    /// How many of a herd must be dead to meet <see cref="DeadShare"/>.
    /// </summary>
    /// <param name="herd">The herd's size.</param>
    /// <returns>The dead needed; zero for no herd.</returns>
    public static int DeadNeeded(int herd) =>
        herd <= 0 ? 0 : (int)Math.Ceiling((herd * DeadShare) - 1e-9);

    /// <summary>
    /// The verifier's rule: the herd was stopped short of the base if, when
    /// the first live boomalope came inside the keep-away, enough of the
    /// herd was already dead; and if none came inside, enough were dead by
    /// the scene's end.
    /// </summary>
    /// <param name="herd">The herd's size.</param>
    /// <param name="deadAtFirstEntry">
    /// The dead when the first one came inside, or null if none did.
    /// </param>
    /// <param name="deadAtEnd">The dead at the scene's end.</param>
    /// <returns>True if the base was defended in time.</returns>
    public static bool HeldTheLine(int herd, int? deadAtFirstEntry, int deadAtEnd) =>
        herd > 0
        && (deadAtFirstEntry ?? int.MaxValue) >= DeadNeeded(herd)
        && deadAtEnd >= DeadNeeded(herd);

    /// <summary>The mean of some spots.</summary>
    /// <param name="spots">The spots.</param>
    /// <returns>Their centre, or null when there are none.</returns>
    public static Spot? Centroid(IEnumerable<Spot> spots)
    {
        double x = 0;
        double z = 0;
        int n = 0;
        foreach (Spot s in spots ?? Enumerable.Empty<Spot>())
        {
            x += s.X;
            z += s.Z;
            n++;
        }

        return n == 0 ? null : new Spot(x / n, z / n);
    }

    /// <summary>
    /// Where to aim so a bolt meets a herd moving at a steady velocity:
    /// the herd's position after the bolt's flight to that position,
    /// refined a few times (the flight depends on the aim point).
    /// </summary>
    /// <param name="from">Where the bolt is launched.</param>
    /// <param name="herd">The herd's centre now.</param>
    /// <param name="velocityPerTick">The herd's velocity, tiles a tick.</param>
    /// <param name="boltTilesPerTick">The bolt's speed, tiles a tick.</param>
    /// <returns>The aim point; the herd itself if the bolt cannot fly.</returns>
    public static Spot Lead(
        Spot from, Spot herd, Spot velocityPerTick, double boltTilesPerTick)
    {
        if (boltTilesPerTick <= 0 || double.IsNaN(boltTilesPerTick))
        {
            return herd;
        }

        Spot aim = herd;
        for (int i = 0; i < 4; i++)
        {
            double flight = from.DistanceTo(aim) / boltTilesPerTick;
            aim = herd + (velocityPerTick * flight);
        }

        return aim;
    }

    /// <summary>
    /// A point no farther than a distance from the launcher, on the line
    /// to the target: the target itself when it is near enough.
    /// </summary>
    /// <param name="from">The launcher.</param>
    /// <param name="target">The target.</param>
    /// <param name="maxDistance">The farthest allowed, in tiles.</param>
    /// <returns>The point.</returns>
    public static Spot Within(Spot from, Spot target, double maxDistance)
    {
        double d = from.DistanceTo(target);
        if (d <= maxDistance || d <= 0)
        {
            return target;
        }

        return from + ((target - from) * (Math.Max(0, maxDistance) / d));
    }

    /// <summary>
    /// The shortest range vanilla's turret ring cannot draw: a selected
    /// turret rings its range below 90 tiles, but the radial pattern the
    /// ring is drawn from ends at 79.8 (GenRadial.MaxRadialPatternRadius),
    /// so a range from 80 to 89 logs an error every frame it is selected.
    /// </summary>
    public const int UndrawableRingFrom = 80;

    /// <summary>
    /// The range from which vanilla's turret stops drawing its ring (the
    /// Ballista draws a plain circle instead).
    /// </summary>
    public const int RinglessFrom = 90;

    /// <summary>
    /// The Ballista range to set for a target at a distance: whole tiles,
    /// one over the distance, within the selectable limits, and never
    /// from <see cref="UndrawableRingFrom"/> to just under
    /// <see cref="RinglessFrom"/>, which becomes <see cref="RinglessFrom"/>.
    /// </summary>
    /// <param name="distance">Tiles to the target.</param>
    /// <returns>The range.</returns>
    public static int RangeFor(double distance)
    {
        int range = Math.Max(BallistaPower.MinRange, Math.Min(
            BallistaPower.MaxRange, (int)Math.Ceiling(distance) + 1));
        return range >= UndrawableRingFrom && range < RinglessFrom
            ? RinglessFrom
            : range;
    }

    /// <summary>
    /// The spot that covers the most others within a radius: where a
    /// cleanup volley, which scatters over that radius, finds the most
    /// stragglers. Ties go to the earlier spot.
    /// </summary>
    /// <param name="spots">The stragglers.</param>
    /// <param name="radius">The volley's scatter radius.</param>
    /// <returns>The index of the best spot; -1 when there are none.</returns>
    public static int BestCover(IReadOnlyList<Spot> spots, double radius)
    {
        if (spots == null || spots.Count == 0)
        {
            return -1;
        }

        int best = 0;
        int bestCount = -1;
        for (int i = 0; i < spots.Count; i++)
        {
            int count = 0;
            for (int j = 0; j < spots.Count; j++)
            {
                if (spots[i].DistanceTo(spots[j]) <= radius)
                {
                    count++;
                }
            }

            if (count > bestCount)
            {
                best = i;
                bestCount = count;
            }
        }

        return best;
    }

    /// <summary>
    /// The radius of a boomalope's own burst when it dies, in tiles
    /// (vanilla's boomalope explodes over 4.9): a volley landing among
    /// the herd can set these off at the edge of its scatter.
    /// </summary>
    public const double BurstTiles = 4.9;

    /// <summary>
    /// The bolt explosion's radius assumed when the bolt's def cannot be
    /// read: the explosive bolt's own 3.9 tiles.
    /// </summary>
    public const double DefaultBoltBlastTiles = 3.9;

    /// <summary>
    /// How far from its aim a volley can do harm: its scatter, plus a
    /// bolt's explosion at the edge of the scatter, plus a boomalope's
    /// burst set off there.
    /// </summary>
    /// <param name="scatter">The volley's scatter radius, in tiles.</param>
    /// <param name="blast">A bolt's explosion radius, in tiles.</param>
    /// <returns>
    /// The footprint's radius; a negative or undefined part counts as 0.
    /// </returns>
    public static double Footprint(double scatter, double blast) =>
        NonNegative(scatter) + NonNegative(blast) + BurstTiles;

    /// <summary>
    /// Whether a volley may be aimed at a cell: its footprint stays clear
    /// of the base's square, where the colonists wait.
    /// </summary>
    /// <param name="baseCentre">The base's centre.</param>
    /// <param name="aim">Where the volley would be aimed.</param>
    /// <param name="footprint">
    /// The volley's footprint (<see cref="Footprint"/>), in tiles.
    /// </param>
    /// <returns>
    /// True when the square lies beyond the footprint; false on it, at
    /// exactly the footprint, or for an undefined footprint.
    /// </returns>
    public static bool SafeAim(Cell baseCentre, Cell aim, double footprint) =>
        !double.IsNaN(footprint)
        && BaseSquare(baseCentre).DistanceTo(aim) > footprint;

    /// <summary>A value with negatives and NaN taken as zero.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value, or 0.</returns>
    private static double NonNegative(double value) =>
        double.IsNaN(value) || value < 0 ? 0 : value;
}
