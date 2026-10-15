using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;

namespace BatshitTechnology.Showcases;

/// <summary>
/// The arithmetic of a showcase's camera (slice 19000d): keeping a view
/// on the map, so a shot near the edge never shows the black beyond it,
/// and fitting several things into the part of the screen the overlays
/// leave clear. RimWorld's camera is orthographic: at a root size (the
/// shots' "zoom") of z it shows 2z tiles from top to bottom and 2z times
/// the screen's aspect ratio from side to side (CameraDriver,
/// CurrentViewRect, RimWorld 1.6). Positions here are in tiles on the
/// map's own axes, x east and z north, with a cell's centre at its
/// coordinates plus one half; the map spans 0 to its size on each axis.
/// </summary>
public static class ShotFraming
{
    /// <summary>
    /// The share of the screen's height the colonist bar and the caption
    /// bar cover at the top: about 120 of 1080 pixels with three
    /// colonists, with room to spare.
    /// </summary>
    public const double TopCover = 0.15;

    /// <summary>
    /// The share of the screen's height the narration covers at the
    /// bottom: its lines sit 230 pixels above the bottom of a 1080-pixel
    /// screen (NarrationOverlayLayout.BottomReserve), up to two lines of
    /// large type above that, with the inspect pane and gizmos below.
    /// </summary>
    public const double BottomCover = 0.32;

    /// <summary>
    /// The share of the screen's width kept clear at each side: the
    /// alerts and the date down the right, the menu icons down the left.
    /// </summary>
    public const double SideCover = 0.08;

    /// <summary>
    /// Where a view's centre goes on one axis to keep the view on the
    /// map: unchanged when the view already fits, moved in from an edge
    /// when it does not, and the map's middle when the view is wider
    /// than the map itself.
    /// </summary>
    /// <param name="centre">The wanted centre, in tiles.</param>
    /// <param name="half">Half the view's extent on this axis, in tiles.</param>
    /// <param name="size">The map's size on this axis, in tiles.</param>
    /// <returns>The centre to use.</returns>
    public static double KeepOnMap(double centre, double half, double size)
    {
        if (size <= 0 || double.IsNaN(centre) || double.IsNaN(half))
        {
            return centre;
        }

        half = Math.Max(0, half);
        if (half * 2 >= size)
        {
            return size / 2;
        }

        return Math.Max(half, Math.Min(size - half, centre));
    }

    /// <summary>
    /// Where a view's centre goes to keep the whole view on the map.
    /// </summary>
    /// <param name="centre">The wanted centre.</param>
    /// <param name="zoom">The view's root size: half its height, in tiles.</param>
    /// <param name="aspect">The screen's width over its height.</param>
    /// <param name="sizeX">The map's width, in tiles.</param>
    /// <param name="sizeZ">The map's height, in tiles.</param>
    /// <returns>The centre to use.</returns>
    public static Spot KeepOnMap(
        Spot centre, double zoom, double aspect, int sizeX, int sizeZ) =>
        new(KeepOnMap(centre.X, zoom * Math.Max(0, aspect), sizeX),
            KeepOnMap(centre.Z, zoom, sizeZ));

    /// <summary>
    /// How far a view reaches beyond the map's edges, in whole tiles: the
    /// most on any side along an axis on which the view is smaller than
    /// the map (a view wider than the map cannot help showing its sides).
    /// </summary>
    /// <param name="centre">The view's centre.</param>
    /// <param name="zoom">The view's root size.</param>
    /// <param name="aspect">The screen's width over its height.</param>
    /// <param name="sizeX">The map's width, in tiles.</param>
    /// <param name="sizeZ">The map's height, in tiles.</param>
    /// <returns>The overhang, 0 when the view is on the map.</returns>
    public static double Overhang(
        Spot centre, double zoom, double aspect, int sizeX, int sizeZ)
    {
        double halfX = zoom * Math.Max(0, aspect);
        double halfZ = zoom;
        double over = 0;
        if (halfX * 2 < sizeX)
        {
            over = Math.Max(over, Math.Max(halfX - centre.X, centre.X + halfX - sizeX));
        }

        if (halfZ * 2 < sizeZ)
        {
            over = Math.Max(over, Math.Max(halfZ - centre.Z, centre.Z + halfZ - sizeZ));
        }

        return Math.Max(0, over);
    }

    /// <summary>
    /// Where a point shows on the screen: its share of the screen's width
    /// from the left and of its height from the top.
    /// </summary>
    /// <param name="point">The point, in tiles.</param>
    /// <param name="camera">The view's centre, in tiles.</param>
    /// <param name="zoom">The view's root size.</param>
    /// <param name="aspect">The screen's width over its height.</param>
    /// <returns>
    /// The shares, 0 to 1 when on screen: X from the left, Y from the top.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="zoom"/> or <paramref name="aspect"/> is
    /// not positive.
    /// </exception>
    public static (double X, double Y) OnScreen(
        Spot point, Spot camera, double zoom, double aspect)
    {
        if (!(zoom > 0) || !(aspect > 0))
        {
            throw new ArgumentOutOfRangeException(
                zoom > 0 ? nameof(aspect) : nameof(zoom),
                "A view needs a positive zoom and aspect ratio.");
        }

        return (0.5 + ((point.X - camera.X) / (2 * zoom * aspect)),
            0.5 - ((point.Z - camera.Z) / (2 * zoom)));
    }

    /// <summary>
    /// Whether a point on the screen is clear of the overlays: below the
    /// caption bar, above the narration, and in from the sides
    /// (<see cref="TopCover"/>, <see cref="BottomCover"/>,
    /// <see cref="SideCover"/>).
    /// </summary>
    /// <param name="screen">The point's shares, from <see cref="OnScreen"/>.</param>
    /// <returns>True when the overlays leave it in plain view.</returns>
    public static bool InClearView((double X, double Y) screen) =>
        screen.X >= SideCover && screen.X <= 1 - SideCover
        && screen.Y >= TopCover && screen.Y <= 1 - BottomCover;

    /// <summary>
    /// The view that shows every point in the clear part of the screen
    /// (<see cref="InClearView"/>) with a margin round them: the closest
    /// zoom that fits, no closer than the least, and the centre that puts
    /// the points' middle in the middle of the clear part.
    /// </summary>
    /// <param name="points">The points; at least one.</param>
    /// <param name="aspect">The screen's width over its height.</param>
    /// <param name="margin">Tiles kept round the points.</param>
    /// <param name="minZoom">The closest the view may come.</param>
    /// <param name="maxZoom">The farthest it may pull back.</param>
    /// <returns>
    /// The view's centre and zoom. At <paramref name="maxZoom"/> the
    /// points may not all fit; the middle is still centred.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when there are no points, or the aspect ratio or zooms are
    /// not positive and in order.
    /// </exception>
    public static (Spot Centre, double Zoom) Fit(
        IEnumerable<Spot> points, double aspect, double margin,
        double minZoom, double maxZoom)
    {
        List<Spot> list = (points ?? Enumerable.Empty<Spot>()).ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("A fit needs a point.", nameof(points));
        }

        if (!(aspect > 0) || !(minZoom > 0) || !(maxZoom >= minZoom))
        {
            throw new ArgumentException(
                "A fit needs a positive aspect ratio and zooms in order.");
        }

        margin = Math.Max(0, margin);
        double minX = list.Min(p => p.X);
        double maxX = list.Max(p => p.X);
        double minZ = list.Min(p => p.Z);
        double maxZ = list.Max(p => p.Z);
        double clearHigh = 1 - TopCover - BottomCover;
        double clearWide = 1 - (2 * SideCover);
        double forHeight = (maxZ - minZ + (2 * margin)) / (2 * clearHigh);
        double forWidth = (maxX - minX + (2 * margin)) / (2 * aspect * clearWide);
        double zoom = Math.Min(maxZoom, Math.Max(minZoom, Math.Max(forHeight, forWidth)));

        // The clear part's middle lies above the screen's: the camera
        // sits that much south of the points' middle.
        double clearMiddle = (TopCover + (1 - BottomCover)) / 2;
        double south = (0.5 - clearMiddle) * 2 * zoom;
        var centre = new Spot((minX + maxX) / 2, ((minZ + maxZ) / 2) - south);
        return (centre, zoom);
    }
}
