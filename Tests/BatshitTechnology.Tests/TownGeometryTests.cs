using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the town geometry of TownGeometry.cs: boxes,
/// and RimWorld's footprint rule for multi-cell things as
/// <see cref="Footprint"/> mirrors it from Verse.GenAdj.OccupiedRect.
/// </summary>
public sealed class TownGeometryTests
{
    private static readonly Facing[] Facings =
        { Facing.North, Facing.East, Facing.South, Facing.West };

    /// <summary>
    /// Known RimWorld cases for things spawned at (0,0): even sizes shift
    /// toward the south-west as the thing turns (GenAdj.AdjustForRotation:
    /// east (0,-1), south (-1,-1), west (-1,0)); a 1x1 never moves.
    /// </summary>
    /// <param name="sizeX">The def's size.x.</param>
    /// <param name="sizeZ">The def's size.z.</param>
    /// <param name="rot">The facing.</param>
    /// <param name="minX">Expected westmost column.</param>
    /// <param name="minZ">Expected southmost row.</param>
    /// <param name="maxX">Expected eastmost column.</param>
    /// <param name="maxZ">Expected northmost row.</param>
    [Theory(DisplayName = "Footprints match RimWorld's OccupiedRect")]
    [InlineData(1, 1, Facing.North, 0, 0, 0, 0)]
    [InlineData(1, 1, Facing.South, 0, 0, 0, 0)]
    [InlineData(2, 2, Facing.North, 0, 0, 1, 1)]
    [InlineData(2, 2, Facing.East, 0, -1, 1, 0)]
    [InlineData(2, 2, Facing.South, -1, -1, 0, 0)]
    [InlineData(2, 2, Facing.West, -1, 0, 0, 1)]
    [InlineData(1, 2, Facing.North, 0, 0, 0, 1)]
    [InlineData(1, 2, Facing.East, 0, 0, 1, 0)]
    [InlineData(1, 2, Facing.South, 0, -1, 0, 0)]
    [InlineData(1, 2, Facing.West, -1, 0, 0, 0)]
    [InlineData(3, 1, Facing.North, -1, 0, 1, 0)]
    [InlineData(3, 1, Facing.East, 0, -1, 0, 1)]
    [InlineData(3, 1, Facing.South, -1, 0, 1, 0)]
    [InlineData(3, 1, Facing.West, 0, -1, 0, 1)]
    [InlineData(2, 4, Facing.North, 0, -1, 1, 2)]
    [InlineData(2, 4, Facing.East, -1, -1, 2, 0)]
    [InlineData(2, 4, Facing.South, -1, -2, 0, 1)]
    [InlineData(2, 4, Facing.West, -2, 0, 1, 1)]
    public void Occupied_should_match_known_cases(
        int sizeX, int sizeZ, Facing rot, int minX, int minZ, int maxX, int maxZ)
    {
        Assert.Equal(new Box(minX, minZ, maxX, maxZ),
            Footprint.Occupied(new Cell(0, 0), sizeX, sizeZ, rot));
        Assert.Equal(new Box(minX + 7, minZ - 3, maxX + 7, maxZ - 3),
            Footprint.Occupied(new Cell(7, -3), sizeX, sizeZ, rot));
    }

    /// <summary>A footprint always covers size.x by size.z cells, turned.</summary>
    [Fact(DisplayName = "Footprints keep their area and turn their sides")]
    public void Occupied_should_keep_area()
    {
        foreach ((int sx, int sz, Facing rot) in Cases())
        {
            Box box = Footprint.Occupied(new Cell(3, 4), sx, sz, rot);
            bool turned = rot is Facing.East or Facing.West;
            Assert.Equal(turned ? sz : sx, box.Width);
            Assert.Equal(turned ? sx : sz, box.Height);
            Assert.True(box.Contains(new Cell(3, 4)), $"{sx}x{sz} {rot} covers its anchor");
        }
    }

    /// <summary>Sizes below one cell are rejected.</summary>
    [Fact(DisplayName = "Footprints reject empty sizes")]
    public void Occupied_should_reject_empty_sizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Footprint.Occupied(new Cell(0, 0), 0, 1, Facing.North));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Footprint.Occupied(new Cell(0, 0), 2, -1, Facing.East));
    }

    /// <summary>
    /// AnchorFor inverts Occupied: the anchor it finds for a thing's
    /// footprint is the thing's own anchor.
    /// </summary>
    [Fact(DisplayName = "AnchorFor is the inverse of Occupied")]
    public void AnchorFor_should_invert_Occupied()
    {
        foreach ((int sx, int sz, Facing rot) in Cases())
        {
            for (int x = -3; x <= 3; x++)
            {
                for (int z = -3; z <= 3; z++)
                {
                    var anchor = new Cell(x, z);
                    Box box = Footprint.Occupied(anchor, sx, sz, rot);
                    Assert.Equal(anchor, Footprint.AnchorFor(box, sx, sz, rot));
                }
            }
        }
    }

    /// <summary>No anchor covers a box of the wrong shape.</summary>
    [Fact(DisplayName = "AnchorFor rejects boxes of the wrong shape")]
    public void AnchorFor_should_reject_wrong_shapes()
    {
        Assert.Throws<ArgumentException>(
            () => Footprint.AnchorFor(new Box(0, 0, 1, 1), 1, 2, Facing.North));
        Assert.Throws<ArgumentException>(
            () => Footprint.AnchorFor(new Box(0, 0, 0, 1), 1, 2, Facing.East));
    }

    /// <summary>
    /// Mirroring covers the mirror image of the original cells with east
    /// and west swapped, and mirroring twice gives the original back.
    /// </summary>
    [Fact(DisplayName = "MirrorX covers the mirror image and round-trips")]
    public void MirrorX_should_round_trip()
    {
        foreach ((int sx, int sz, Facing rot) in Cases())
        {
            for (int x = -9; x <= 9; x += 3)
            {
                var anchor = new Cell(x, 2);
                (Cell a, Facing r) = Footprint.MirrorX(anchor, sx, sz, rot);
                Assert.Equal(Footprint.Occupied(anchor, sx, sz, rot).MirroredX,
                    Footprint.Occupied(a, sx, sz, r));
                Assert.Equal(Footprint.MirroredX(rot), r);
                Assert.Equal((anchor, rot), Footprint.MirrorX(a, sx, sz, r));
            }
        }

        Assert.Equal(Facing.West, Footprint.MirroredX(Facing.East));
        Assert.Equal(Facing.North, Footprint.MirroredX(Facing.North));
        Assert.Equal(new Cell(-4, 9), new Cell(4, 9).MirroredX);
        Assert.Equal(new Box(-17, 7, -7, 15), new Box(7, 7, 17, 15).MirroredX);
    }

    /// <summary>
    /// Offsets turn as IntVec3.RotatedBy turns them, so an interaction cell
    /// of (0,0,-1) lies south of a north-facing bench and north of a
    /// south-facing one.
    /// </summary>
    [Fact(DisplayName = "Interaction cells turn with the thing")]
    public void InteractionCell_should_turn_with_the_thing()
    {
        var anchor = new Cell(5, 5);
        var offset = new Cell(0, -1);
        Assert.Equal(new Cell(5, 4), Footprint.InteractionCell(anchor, offset, Facing.North));
        Assert.Equal(new Cell(4, 5), Footprint.InteractionCell(anchor, offset, Facing.East));
        Assert.Equal(new Cell(5, 6), Footprint.InteractionCell(anchor, offset, Facing.South));
        Assert.Equal(new Cell(6, 5), Footprint.InteractionCell(anchor, offset, Facing.West));
        Assert.Equal(new Cell(2, -1), Footprint.Rotated(new Cell(1, 2), Facing.East));
        Assert.Equal(new Cell(-1, -2), Footprint.Rotated(new Cell(1, 2), Facing.South));
        Assert.Equal(new Cell(-2, 1), Footprint.Rotated(new Cell(1, 2), Facing.West));
    }

    /// <summary>Boxes measure, grow, combine and list their cells correctly.</summary>
    [Fact(DisplayName = "Boxes measure and combine")]
    public void Box_should_measure_and_combine()
    {
        Box box = Box.FromSize(2, 3, 4, 2);
        Assert.Equal(new Box(2, 3, 5, 4), box);
        Assert.Equal(8, box.Area);
        Assert.Equal(8, box.Cells().Count());
        Assert.Equal(8, box.EdgeCells().Count());
        Assert.Equal(16, new Box(0, 0, 4, 4).EdgeCells().Count());
        Assert.Equal(new Box(1, 2, 6, 5), box.ExpandedBy(1));
        Assert.Equal(0, new Box(1, 1, 0, 0).Area);
        Assert.Equal(new Box(0, 3, 5, 9), box.Union(new Box(0, 9, 0, 9)));
        Assert.Equal(5, new Box(0, 0, 1, 1).DistanceTo(new Cell(4, 5)), 10);
        Assert.Equal(0, box.DistanceTo(new Box(5, 4, 9, 9)), 10);
        Assert.Equal(4, box.DistanceTo(new Box(9, 0, 9, 9)), 10);
        Assert.True(box.Overlaps(new Box(5, 4, 5, 4)));
        Assert.False(box.Overlaps(new Box(6, 4, 6, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Box.FromSize(0, 0, 0, 1));
    }

    /// <summary>Every size and facing the tests sweep.</summary>
    /// <returns>Size and facing triples.</returns>
    private static IEnumerable<(int SizeX, int SizeZ, Facing Rot)> Cases()
    {
        (int, int)[] sizes = { (1, 1), (1, 2), (2, 1), (2, 2), (3, 1), (2, 3), (2, 4), (3, 3) };
        return sizes.SelectMany(s => Facings.Select(f => (s.Item1, s.Item2, f)));
    }
}
