using System;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="ShotFraming"/>: a view kept on the map, how far a
/// view reaches beyond it, where a point shows on screen, and a fit that
/// puts several points in the part of the screen the caption bar and the
/// narration leave clear (slice 19000d).
/// </summary>
public sealed class ShotFramingTests
{
    /// <summary>A 16:9 screen, the presentation's 1920 by 1080.</summary>
    private const double Wide = 16.0 / 9.0;

    /// <summary>The tour's map size.</summary>
    private const int MapSize = 250;

    /// <summary>
    /// A view that fits is left where it is; one past an edge is moved in
    /// until its edge meets the map's; one wider than the map is centred.
    /// </summary>
    [Fact(DisplayName = "KeepOnMap moves a view in from an edge, or centres it")]
    public void KeepOnMap_should_move_in_from_the_edges()
    {
        Assert.Equal(125, ShotFraming.KeepOnMap(125, 40, MapSize));
        Assert.Equal(40, ShotFraming.KeepOnMap(10, 40, MapSize));
        Assert.Equal(210, ShotFraming.KeepOnMap(243.5, 40, MapSize));
        Assert.Equal(40, ShotFraming.KeepOnMap(40, 40, MapSize));
        Assert.Equal(125, ShotFraming.KeepOnMap(10, 125, MapSize));
        Assert.Equal(125, ShotFraming.KeepOnMap(240, 200, MapSize));
    }

    /// <summary>
    /// The stampede's herd shot (zoom 40, its look 12 tiles west of a herd
    /// entering at x 240 to 247) is moved in until the view's east edge is
    /// the map's, and the herd is still in the view.
    /// </summary>
    [Fact(DisplayName = "The herd's shot keeps on the map with the herd in view")]
    public void KeepOnMap_should_frame_the_herd_with_the_map()
    {
        var look = new Spot(231.5, 120.5);
        Spot kept = ShotFraming.KeepOnMap(look, 40, Wide, MapSize, MapSize);
        double half = 40 * Wide;
        Assert.Equal(MapSize - half, kept.X, 6);
        Assert.Equal(120.5, kept.Z, 6);
        Assert.Equal(0, ShotFraming.Overhang(kept, 40, Wide, MapSize, MapSize), 6);
        (double x, _) = ShotFraming.OnScreen(new Spot(243.5, 120.5), kept, 40, Wide);
        Assert.InRange(x, 0.5, 1.0);
        Assert.True(ShotFraming.Overhang(look, 40, Wide, MapSize, MapSize) > 50);
    }

    /// <summary>
    /// The overhang is the farthest a view reaches past any edge, and none
    /// is counted along an axis the view is bigger than the map on.
    /// </summary>
    [Fact(DisplayName = "Overhang measures the reach past the map's edges")]
    public void Overhang_should_measure_past_the_edges()
    {
        Assert.Equal(0, ShotFraming.Overhang(new Spot(125, 125), 40, Wide, MapSize, MapSize));
        Assert.Equal(10, ShotFraming.Overhang(new Spot(125, 30), 40, Wide, MapSize, MapSize), 6);
        Assert.Equal(15, ShotFraming.Overhang(new Spot(125, 225), 40, Wide, MapSize, MapSize), 6);
        double half = 40 * Wide;
        Assert.Equal(half - 20, ShotFraming.Overhang(new Spot(20, 125), 40, Wide, MapSize, MapSize), 6);

        // A view wider than a narrow map: its sides cannot be helped.
        Assert.Equal(0, ShotFraming.Overhang(new Spot(50, 125), 40, Wide, 100, MapSize));
    }

    /// <summary>
    /// A view kept on the map never reaches past it, whatever the wanted
    /// centre, for zooms that fit and screens wide and narrow.
    /// </summary>
    /// <param name="aspect">The screen's width over its height.</param>
    [Theory(DisplayName = "A kept view never overhangs the map")]
    [InlineData(4.0 / 3.0)]
    [InlineData(16.0 / 9.0)]
    [InlineData(21.0 / 9.0)]
    public void KeepOnMap_should_leave_no_overhang(double aspect)
    {
        foreach (double zoom in new[] { 14.0, 22.0, 40.0, 56.0 })
        {
            for (int x = -20; x <= 270; x += 29)
            {
                for (int z = -20; z <= 270; z += 31)
                {
                    Spot kept = ShotFraming.KeepOnMap(new Spot(x, z), zoom, aspect, MapSize, MapSize);
                    Assert.Equal(0, ShotFraming.Overhang(kept, zoom, aspect, MapSize, MapSize), 6);
                }
            }
        }
    }

    /// <summary>
    /// The camera's centre is the screen's middle; north is up, so a point
    /// north of it is above the middle; the view's edges are 0 and 1.
    /// </summary>
    [Fact(DisplayName = "OnScreen places points as the camera shows them")]
    public void OnScreen_should_place_points()
    {
        var camera = new Spot(100, 100);
        Assert.Equal((0.5, 0.5), ShotFraming.OnScreen(camera, camera, 20, Wide));
        (double x, double y) = ShotFraming.OnScreen(new Spot(100 + (20 * Wide), 120), camera, 20, Wide);
        Assert.Equal(1.0, x, 6);
        Assert.Equal(0.0, y, 6);
        (x, y) = ShotFraming.OnScreen(new Spot(100, 90), camera, 20, Wide);
        Assert.Equal(0.75, y, 6);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ShotFraming.OnScreen(camera, camera, 0, Wide));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ShotFraming.OnScreen(camera, camera, 20, 0));
    }

    /// <summary>
    /// The clear view is below the caption bar, above the narration, and
    /// in from the sides.
    /// </summary>
    [Fact(DisplayName = "InClearView leaves out the overlays")]
    public void InClearView_should_leave_out_the_overlays()
    {
        Assert.True(ShotFraming.InClearView((0.5, 0.4)));
        Assert.False(ShotFraming.InClearView((0.5, 0.05)));
        Assert.False(ShotFraming.InClearView((0.5, 0.8)));
        Assert.False(ShotFraming.InClearView((0.02, 0.4)));
        Assert.False(ShotFraming.InClearView((0.98, 0.4)));
    }

    /// <summary>
    /// Ned, the raccoon 20 tiles north of him and the grenade between them
    /// (the throw that sat under the caption bar) are fitted into the
    /// clear view, at a zoom within the bounds.
    /// </summary>
    [Fact(DisplayName = "Fit puts Ned, the raccoon and the grenade in the clear view")]
    public void Fit_should_put_every_point_in_the_clear_view()
    {
        Spot[] points =
        {
            new Spot(120.5, 100.5), new Spot(124.5, 120.5), new Spot(122.5, 110.5),
        };
        (Spot centre, double zoom) = ShotFraming.Fit(points, Wide, 4, 20, 34);
        Assert.InRange(zoom, 20, 34);
        Assert.All(points, p => Assert.True(
            ShotFraming.InClearView(ShotFraming.OnScreen(p, centre, zoom, Wide)), $"{p}"));

        // The margin is kept too: a point 4 tiles out from each is in view.
        Assert.True(ShotFraming.InClearView(
            ShotFraming.OnScreen(new Spot(124.5, 124), centre, zoom, Wide)));
        Assert.True(ShotFraming.InClearView(
            ShotFraming.OnScreen(new Spot(120.5, 97), centre, zoom, Wide)));
    }

    /// <summary>
    /// Points close together get the closest zoom allowed, and the camera
    /// sits south of them so they show in the clear view's middle, above
    /// the screen's.
    /// </summary>
    [Fact(DisplayName = "Fit keeps close points at the closest zoom, above the middle")]
    public void Fit_should_use_the_least_zoom_for_close_points()
    {
        (Spot centre, double zoom) = ShotFraming.Fit(
            new[] { new Spot(50, 50), new Spot(52, 51) }, Wide, 4, 20, 34);
        Assert.Equal(20, zoom);
        Assert.Equal(51, centre.X, 6);
        Assert.True(centre.Z < 50.5, $"camera at {centre.Z}");
        (_, double y) = ShotFraming.OnScreen(new Spot(51, 50.5), centre, zoom, Wide);
        Assert.Equal((ShotFraming.TopCover + 1 - ShotFraming.BottomCover) / 2, y, 6);
    }

    /// <summary>
    /// Points too far apart get the farthest zoom allowed, still centred;
    /// no points, or a bad screen or zoom range, are refused.
    /// </summary>
    [Fact(DisplayName = "Fit stops at the farthest zoom and refuses bad input")]
    public void Fit_should_cap_the_zoom_and_refuse_bad_input()
    {
        (Spot centre, double zoom) = ShotFraming.Fit(
            new[] { new Spot(0, 0), new Spot(0, 200) }, Wide, 4, 20, 34);
        Assert.Equal(34, zoom);
        Assert.Equal(0, centre.X, 6);
        Assert.Throws<ArgumentException>(
            () => ShotFraming.Fit(Enumerable.Empty<Spot>(), Wide, 4, 20, 34));
        Assert.Throws<ArgumentException>(
            () => ShotFraming.Fit(new[] { new Spot(0, 0) }, 0, 4, 20, 34));
        Assert.Throws<ArgumentException>(
            () => ShotFraming.Fit(new[] { new Spot(0, 0) }, Wide, 4, 34, 20));
    }
}
