using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// A cell, in town-local or map coordinates. As in RimWorld, x grows east
/// and z grows north.
/// </summary>
/// <param name="X">East-west position.</param>
/// <param name="Z">North-south position.</param>
public readonly record struct Cell(int X, int Z)
{
    /// <summary>Adds two cells as vectors.</summary>
    /// <param name="a">The first cell.</param>
    /// <param name="b">The second cell.</param>
    /// <returns>The sum.</returns>
    public static Cell operator +(Cell a, Cell b) => new(a.X + b.X, a.Z + b.Z);

    /// <summary>Subtracts one cell from another as vectors.</summary>
    /// <param name="a">The cell to subtract from.</param>
    /// <param name="b">The cell to subtract.</param>
    /// <returns>The difference.</returns>
    public static Cell operator -(Cell a, Cell b) => new(a.X - b.X, a.Z - b.Z);

    /// <summary>This cell mirrored across the line x = 0.</summary>
    public Cell MirroredX => new(-X, Z);

    /// <summary>The straight-line distance to another cell.</summary>
    /// <param name="other">The other cell.</param>
    /// <returns>The Euclidean distance, in cells.</returns>
    public double DistanceTo(Cell other)
    {
        double dx = X - other.X;
        double dz = Z - other.Z;
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>Formats the cell as "(x,z)".</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"({X},{Z})";
}

/// <summary>
/// An axis-aligned rectangle of cells, inclusive at both ends, like
/// RimWorld's CellRect.
/// </summary>
/// <param name="MinX">The westmost column.</param>
/// <param name="MinZ">The southmost row.</param>
/// <param name="MaxX">The eastmost column.</param>
/// <param name="MaxZ">The northmost row.</param>
public readonly record struct Box(int MinX, int MinZ, int MaxX, int MaxZ)
{
    /// <summary>Whether the box holds at least one cell.</summary>
    public bool IsValid => MaxX >= MinX && MaxZ >= MinZ;

    /// <summary>Width in cells (east-west).</summary>
    public int Width => MaxX - MinX + 1;

    /// <summary>Height in cells (north-south).</summary>
    public int Height => MaxZ - MinZ + 1;

    /// <summary>Number of cells.</summary>
    public int Area => IsValid ? Width * Height : 0;

    /// <summary>A box from a corner cell and a size.</summary>
    /// <param name="minX">The westmost column.</param>
    /// <param name="minZ">The southmost row.</param>
    /// <param name="width">Width in cells; at least 1.</param>
    /// <param name="height">Height in cells; at least 1.</param>
    /// <returns>The box.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A size below 1.</exception>
    public static Box FromSize(int minX, int minZ, int width, int height)
    {
        if (width < 1 || height < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), $"{width}x{height}", "A box needs at least one cell.");
        }

        return new Box(minX, minZ, minX + width - 1, minZ + height - 1);
    }

    /// <summary>The smallest box holding two cells.</summary>
    /// <param name="a">One corner.</param>
    /// <param name="b">The opposite corner.</param>
    /// <returns>The box.</returns>
    public static Box Spanning(Cell a, Cell b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Z, b.Z), Math.Max(a.X, b.X), Math.Max(a.Z, b.Z));

    /// <summary>Whether a cell lies inside.</summary>
    /// <param name="c">The cell.</param>
    /// <returns>True if inside.</returns>
    public bool Contains(Cell c) => c.X >= MinX && c.X <= MaxX && c.Z >= MinZ && c.Z <= MaxZ;

    /// <summary>Whether another box lies wholly inside this one.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>True if wholly inside.</returns>
    public bool Contains(Box other) =>
        other.MinX >= MinX && other.MaxX <= MaxX && other.MinZ >= MinZ && other.MaxZ <= MaxZ;

    /// <summary>Whether two boxes share at least one cell.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>True if they overlap.</returns>
    public bool Overlaps(Box other) =>
        IsValid && other.IsValid
        && other.MinX <= MaxX && other.MaxX >= MinX && other.MinZ <= MaxZ && other.MaxZ >= MinZ;

    /// <summary>The box grown (or, if negative, shrunk) on every side.</summary>
    /// <param name="cells">Cells to add on each side.</param>
    /// <returns>The new box.</returns>
    public Box ExpandedBy(int cells) => new(MinX - cells, MinZ - cells, MaxX + cells, MaxZ + cells);

    /// <summary>The box moved by an offset.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The moved box.</returns>
    public Box Offset(Cell offset) =>
        new(MinX + offset.X, MinZ + offset.Z, MaxX + offset.X, MaxZ + offset.Z);

    /// <summary>This box mirrored across the line x = 0.</summary>
    public Box MirroredX => new(-MaxX, MinZ, -MinX, MaxZ);

    /// <summary>The smallest box holding both boxes.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>The union's bounding box.</returns>
    public Box Union(Box other) =>
        new(Math.Min(MinX, other.MinX), Math.Min(MinZ, other.MinZ),
            Math.Max(MaxX, other.MaxX), Math.Max(MaxZ, other.MaxZ));

    /// <summary>Every cell, row by row from the south-west corner.</summary>
    /// <returns>The cells.</returns>
    public IEnumerable<Cell> Cells()
    {
        for (int z = MinZ; z <= MaxZ; z++)
        {
            for (int x = MinX; x <= MaxX; x++)
            {
                yield return new Cell(x, z);
            }
        }
    }

    /// <summary>The cells on the box's outer edge (its perimeter ring).</summary>
    /// <returns>The edge cells, each once.</returns>
    public IEnumerable<Cell> EdgeCells()
    {
        foreach (Cell c in Cells())
        {
            if (c.X == MinX || c.X == MaxX || c.Z == MinZ || c.Z == MaxZ)
            {
                yield return c;
            }
        }
    }

    /// <summary>
    /// The straight-line distance from a cell to the nearest cell of the
    /// box; zero inside.
    /// </summary>
    /// <param name="c">The cell.</param>
    /// <returns>The distance, in cells.</returns>
    public double DistanceTo(Cell c)
    {
        int dx = Math.Max(0, Math.Max(MinX - c.X, c.X - MaxX));
        int dz = Math.Max(0, Math.Max(MinZ - c.Z, c.Z - MaxZ));
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>The straight-line gap between two boxes; zero if they touch or overlap.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>The distance between their nearest cells.</returns>
    public double DistanceTo(Box other)
    {
        int dx = Math.Max(0, Math.Max(other.MinX - MaxX, MinX - other.MaxX));
        int dz = Math.Max(0, Math.Max(other.MinZ - MaxZ, MinZ - other.MaxZ));
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>Formats the box as "x a..b, z c..d".</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"x {MinX}..{MaxX}, z {MinZ}..{MaxZ}";
}

/// <summary>
/// Which way a thing faces, numbered as RimWorld's Rot4.AsInt.
/// </summary>
public enum Facing
{
    /// <summary>Facing north (Rot4.North).</summary>
    North = 0,

    /// <summary>Facing east (Rot4.East).</summary>
    East = 1,

    /// <summary>Facing south (Rot4.South).</summary>
    South = 2,

    /// <summary>Facing west (Rot4.West).</summary>
    West = 3,
}

/// <summary>
/// RimWorld's rule for which cells a multi-cell thing covers, mirrored from
/// Verse.GenAdj.OccupiedRect and AdjustForRotation (reference North) so
/// that a town plan can be checked without the game.
/// </summary>
public static class Footprint
{
    /// <summary>
    /// The cells a thing of a given size covers when spawned at an anchor
    /// cell with a facing.
    /// </summary>
    /// <param name="anchor">The thing's Position.</param>
    /// <param name="sizeX">The def's size.x (as seen facing north).</param>
    /// <param name="sizeZ">The def's size.z (as seen facing north).</param>
    /// <param name="rot">The facing.</param>
    /// <returns>The occupied box.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A size below 1.</exception>
    public static Box Occupied(Cell anchor, int sizeX, int sizeZ, Facing rot)
    {
        if (sizeX < 1 || sizeZ < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeX), $"{sizeX}x{sizeZ}", "Sizes must be at least 1.");
        }

        int cx = anchor.X;
        int cz = anchor.Z;
        int sx = sizeX;
        int sz = sizeZ;
        if (sx != 1 || sz != 1)
        {
            if (rot == Facing.East || rot == Facing.West)
            {
                (sx, sz) = (sz, sx);
            }

            (int dx, int dz) = rot switch
            {
                Facing.East => (0, -1),
                Facing.South => (-1, -1),
                Facing.West => (-1, 0),
                _ => (0, 0),
            };
            if (sx % 2 == 0)
            {
                cx += dx;
            }

            if (sz % 2 == 0)
            {
                cz += dz;
            }
        }

        return Box.FromSize(cx - ((sx - 1) / 2), cz - ((sz - 1) / 2), sx, sz);
    }

    /// <summary>The facing mirrored across a north-south line: east and west swap.</summary>
    /// <param name="rot">The facing.</param>
    /// <returns>The mirrored facing.</returns>
    public static Facing MirroredX(Facing rot) => rot switch
    {
        Facing.East => Facing.West,
        Facing.West => Facing.East,
        _ => rot,
    };

    /// <summary>
    /// The anchor that makes a thing of a given size and facing cover
    /// exactly a box.
    /// </summary>
    /// <param name="occupied">The cells to cover.</param>
    /// <param name="sizeX">The def's size.x.</param>
    /// <param name="sizeZ">The def's size.z.</param>
    /// <param name="rot">The facing.</param>
    /// <returns>The anchor.</returns>
    /// <exception cref="ArgumentException">No anchor covers the box exactly.</exception>
    public static Cell AnchorFor(Box occupied, int sizeX, int sizeZ, Facing rot)
    {
        for (int x = occupied.MinX - 1; x <= occupied.MaxX + 1; x++)
        {
            for (int z = occupied.MinZ - 1; z <= occupied.MaxZ + 1; z++)
            {
                var anchor = new Cell(x, z);
                if (Occupied(anchor, sizeX, sizeZ, rot) == occupied)
                {
                    return anchor;
                }
            }
        }

        throw new ArgumentException(
            $"No {sizeX}x{sizeZ} thing facing {rot} covers exactly {occupied}.", nameof(occupied));
    }

    /// <summary>
    /// Mirrors a placed thing across x = 0: the mirrored facing, and the
    /// anchor that covers the mirror image of the original cells.
    /// </summary>
    /// <param name="anchor">The original anchor.</param>
    /// <param name="sizeX">The def's size.x.</param>
    /// <param name="sizeZ">The def's size.z.</param>
    /// <param name="rot">The original facing.</param>
    /// <returns>The mirrored anchor and facing.</returns>
    public static (Cell Anchor, Facing Rot) MirrorX(Cell anchor, int sizeX, int sizeZ, Facing rot)
    {
        Facing mirrored = MirroredX(rot);
        Box target = Occupied(anchor, sizeX, sizeZ, rot).MirroredX;
        return (AnchorFor(target, sizeX, sizeZ, mirrored), mirrored);
    }
}
