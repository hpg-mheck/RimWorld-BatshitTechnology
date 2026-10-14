using System;
using System.Collections.Generic;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="Stampede"/>, the boomalope stampede's rules, and
/// <see cref="BallistaPower.LongestRangeWithin"/>, which sets how far the
/// demo grid lets its 256-bolt volley reach.
/// </summary>
public sealed class StampedeTests
{
    /// <summary>The quick-test map: 250 cells a side.</summary>
    private static readonly Box Map250 = new(0, 0, 249, 249);

    /// <summary>The demo base's centre on that map.</summary>
    private static readonly Cell Base = new(125, 125);

    /// <summary>
    /// The first band is a dense front on the east edge, level with the
    /// base, kept off the edge, and holds the target herd at two a cell.
    /// </summary>
    [Fact(DisplayName = "The herd enters on the east edge, level with the base")]
    public void Band_should_sit_on_the_east_edge_level_with_the_base()
    {
        Box band = Stampede.Band(Map250, Base, 0);

        Assert.Equal(249 - Stampede.EdgeMargin, band.MaxX);
        Assert.Equal(8, band.Width);
        Assert.Equal(Base.Z - 20, band.MinZ);
        Assert.Equal(Base.Z + 20, band.MaxZ);
        Assert.True(band.Area >= Stampede.CellsNeeded(Stampede.TargetHerd));
        double density = Stampede.TargetHerd / (double)band.Area;
        Assert.InRange(density, 0.4, 1.0);
    }

    /// <summary>
    /// Each later band is wider than the one before, and every band stays
    /// inside the map's margin, even with the base near a corner.
    /// </summary>
    [Fact(DisplayName = "Later bands widen and every band stays on the map")]
    public void Bands_should_widen_and_stay_on_the_map()
    {
        var corner = new Cell(30, 220);
        for (int step = 0; step < Stampede.BandSizes.Count; step++)
        {
            Box band = Stampede.Band(Map250, corner, step);
            Assert.True(band.IsValid);
            Assert.True(band.MinX >= Stampede.EdgeMargin);
            Assert.True(band.MaxZ <= 249 - Stampede.EdgeMargin);
            Assert.True(band.MinZ >= Stampede.EdgeMargin);
            if (step > 0)
            {
                Assert.True(band.Area > Stampede.Band(Map250, corner, step - 1).Area);
            }
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Stampede.Band(Map250, Base, Stampede.BandSizes.Count));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Stampede.Band(Map250, Base, -1));
    }

    /// <summary>
    /// The bands come level with the base first, widest last, then slid
    /// north only (away from the tractor yard's southern candidates);
    /// none repeats, and all stay on the east edge of the map.
    /// </summary>
    [Fact(DisplayName = "Bands come level first, then slide north, all on the edge")]
    public void Bands_should_come_level_first_then_slide_north()
    {
        IReadOnlyList<Box> bands = Stampede.Bands(Map250, Base);

        Assert.Equal(Stampede.Band(Map250, Base, 0), bands[0]);
        Assert.Equal(Stampede.Band(Map250, Base, 1), bands[1]);
        Assert.Equal(Stampede.Band(Map250, Base, 2), bands[2]);
        Assert.Equal(bands.Count, new HashSet<Box>(bands).Count);
        Assert.All(bands, b =>
        {
            Assert.Equal(249 - Stampede.EdgeMargin, b.MaxX);
            Assert.True(b.MinZ >= Base.Z - 45, $"{b} reaches south");
            Assert.True(b.MaxZ <= 249 - Stampede.EdgeMargin);
        });
        Assert.All(Stampede.BandSlides, slide => Assert.True(slide >= 0));
        Box north = Stampede.Band(Map250, Base, 0, 35);
        Assert.Equal(Base.Z + 35 - 20, north.MinZ);
        Assert.Contains(north, bands);

        // Near the north edge a slid band falls off the map and is left out.
        var high = new Cell(125, 240);
        Assert.All(Stampede.Bands(Map250, high), b => Assert.True(b.IsValid));
    }

    /// <summary>
    /// The herd's size is held to the operator's bounds, and the cells it
    /// needs round up.
    /// </summary>
    [Fact(DisplayName = "Herd sizes hold to their bounds; cells round up")]
    public void Herd_size_should_hold_to_its_bounds()
    {
        Assert.Equal(Stampede.MinHerd, Stampede.HerdSize(10));
        Assert.Equal(200, Stampede.HerdSize(200));
        Assert.Equal(Stampede.MaxHerd, Stampede.HerdSize(10_000));
        Assert.Equal(100, Stampede.CellsNeeded(200));
        Assert.Equal(101, Stampede.CellsNeeded(201));
        Assert.Equal(1, Stampede.CellsNeeded(0));
    }

    /// <summary>
    /// The keep-away is measured from the base's square: a cell just
    /// inside the distance is inside, one at the distance is not.
    /// </summary>
    [Fact(DisplayName = "The keep-away is measured from the base's square")]
    public void Keep_away_should_be_measured_from_the_base_square()
    {
        int edge = Base.X + TourStagePlanner.BaseHalfSize;
        int at = edge + (int)Stampede.KeepAwayTiles;

        Assert.True(Stampede.InsideKeepAway(Base, Base));
        Assert.True(Stampede.InsideKeepAway(Base, new Cell(at - 1, Base.Z)));
        Assert.False(Stampede.InsideKeepAway(Base, new Cell(at, Base.Z)));
        Assert.False(Stampede.InsideKeepAway(Base, new Cell(240, Base.Z)));
    }

    /// <summary>
    /// Ninety per cent, rounded up, must be dead at the first entry and
    /// at the end; with no entry only the end counts.
    /// </summary>
    [Fact(DisplayName = "HeldTheLine wants 90% dead at first entry and at the end")]
    public void Held_the_line_should_want_ninety_per_cent()
    {
        Assert.Equal(180, Stampede.DeadNeeded(200));
        Assert.Equal(136, Stampede.DeadNeeded(151));
        Assert.Equal(0, Stampede.DeadNeeded(0));

        Assert.True(Stampede.HeldTheLine(200, null, 180));
        Assert.True(Stampede.HeldTheLine(200, 190, 200));
        Assert.False(Stampede.HeldTheLine(200, null, 179));
        Assert.False(Stampede.HeldTheLine(200, 179, 200));
        Assert.False(Stampede.HeldTheLine(0, null, 0));
    }

    /// <summary>
    /// The aim leads a moving herd by the bolt's flight: the bolt's flight
    /// to the aim point matches the herd's travel to it.
    /// </summary>
    [Fact(DisplayName = "Lead aims where the herd will be when the bolt lands")]
    public void Lead_should_meet_the_herd()
    {
        var from = new Spot(130, 120);
        var herd = new Spot(220, 125);
        var velocity = new Spot(-0.05, 0);
        const double boltSpeed = 0.45;

        Spot aim = Stampede.Lead(from, herd, velocity, boltSpeed);

        double flight = from.DistanceTo(aim) / boltSpeed;
        Spot herdThen = herd + (velocity * flight);
        Assert.True(aim.DistanceTo(herdThen) < 0.05, $"aim {aim}, herd {herdThen}");
        Assert.True(aim.X < herd.X);
        Assert.Equal(herd, Stampede.Lead(from, herd, velocity, 0));
        Assert.Equal(herd, Stampede.Lead(from, herd, new Spot(0, 0), boltSpeed));
    }

    /// <summary>
    /// Within leaves a near target alone and pulls a far one in along the
    /// line from the launcher.
    /// </summary>
    [Fact(DisplayName = "Within pulls a far target in along the line")]
    public void Within_should_pull_a_far_target_in()
    {
        var from = new Spot(0, 0);
        Assert.Equal(new Spot(3, 4), Stampede.Within(from, new Spot(3, 4), 10));

        Spot pulled = Stampede.Within(from, new Spot(30, 40), 10);
        Assert.Equal(10, from.DistanceTo(pulled), 6);
        Assert.Equal(6, pulled.X, 6);
        Assert.Equal(8, pulled.Z, 6);
    }

    /// <summary>
    /// The range set is one tile over the target's distance, rounded up,
    /// within the Ballista's limits, and skips the ranges whose ring
    /// vanilla cannot draw (80 to 89 become 90).
    /// </summary>
    [Fact(DisplayName = "RangeFor sets one tile over, skipping undrawable rings")]
    public void Range_should_be_one_tile_over_within_limits()
    {
        Assert.Equal(76, Stampede.RangeFor(74.2));
        Assert.Equal(79, Stampede.RangeFor(78));
        Assert.Equal(90, Stampede.RangeFor(78.5));
        Assert.Equal(90, Stampede.RangeFor(86));
        Assert.Equal(90, Stampede.RangeFor(88.9));
        Assert.Equal(91, Stampede.RangeFor(89.5));
        Assert.Equal(BallistaPower.MinRange, Stampede.RangeFor(2));
        Assert.Equal(BallistaPower.MaxRange, Stampede.RangeFor(900));
    }

    /// <summary>
    /// BestCover picks the straggler with the most others in reach, and
    /// reports none for no stragglers.
    /// </summary>
    [Fact(DisplayName = "BestCover finds the thickest group of stragglers")]
    public void Best_cover_should_find_the_thickest_group()
    {
        var spots = new List<Spot>
        {
            new(0, 0), new(50, 50), new(51, 50), new(50, 52), new(100, 0),
        };

        Assert.Equal(1, Stampede.BestCover(spots, 3));
        Assert.Equal(-1, Stampede.BestCover(new List<Spot>(), 3));
        Assert.Equal(0, Stampede.BestCover(new List<Spot> { new(7, 7) }, 3));
    }

    /// <summary>The centroid is the mean, and there is none of nothing.</summary>
    [Fact(DisplayName = "Centroid is the mean; none of nothing")]
    public void Centroid_should_be_the_mean()
    {
        Assert.Equal(
            new Spot(2, 3),
            Stampede.Centroid(new[] { new Spot(0, 0), new Spot(4, 6) }));
        Assert.Null(Stampede.Centroid(Array.Empty<Spot>()));
        Assert.Equal(new Cell(3, -2), new Spot(2.5, -2.4).ToCell());
    }

    /// <summary>
    /// LongestRangeWithin inverts the firing draw: the range it returns
    /// fits the watts, and one tile more does not.
    /// </summary>
    [Fact(DisplayName = "LongestRangeWithin inverts the firing draw")]
    public void Longest_range_should_invert_firing_watts()
    {
        // One 250 kW reactor, less the base's other draws, the three
        // magazines' feeding and the margin: the demo grid's budget.
        double watts = 250_000 - 1_500 - 1_500 - Stampede.PowerMarginWatts;

        int range = BallistaPower.LongestRangeWithin(256, watts);

        Assert.InRange(range, 70, 80);
        Assert.True(BallistaPower.FiringWatts(256, range) <= watts);
        Assert.True(BallistaPower.FiringWatts(256, range + 1) > watts);
        Assert.Equal(BallistaPower.MaxRange,
            BallistaPower.LongestRangeWithin(256, 5_000_000));
        Assert.Equal(0, BallistaPower.LongestRangeWithin(256, 500));
        Assert.Equal(325, BallistaPower.LongestRangeWithin(256, 990_000));
    }
}
