using System.Collections.Generic;
using System.Globalization;

namespace BatshitTechnology.Showcases;

/// <summary>
/// A stable 64-bit fingerprint of a map, to show that a seed gave the same
/// map and the same stage twice: the map's size, the terrain and roof of
/// every cell (in cell order), every thing (its def, cell, rotation, stuff
/// and stack count) and every pawn (its name, kind and faction). Things
/// and pawns are a multiset: the order they are listed in does not count.
/// Built from FNV-1a (<see cref="Fnv1a"/>), so it is the same in every
/// process. What goes in is the game code's choice (docs/SHOWCASE-API.md,
/// "Fingerprints"); this class only hashes it.
/// </summary>
public sealed class MapFingerprint
{
    /// <summary>Separates the fields of one record.</summary>
    private const byte FieldMark = 0x1F;

    /// <summary>Ends one record.</summary>
    private const byte RecordMark = 0x1E;

    /// <summary>The ordered hash over the size and the cells.</summary>
    private ulong grid = Fnv1a.Offset64;

    /// <summary>Each thing's own hash.</summary>
    private readonly List<ulong> things = new List<ulong>();

    /// <summary>Each pawn's own hash.</summary>
    private readonly List<ulong> pawns = new List<ulong>();

    /// <summary>Gets how many cells were added.</summary>
    public int Cells { get; private set; }

    /// <summary>Gets how many things were added.</summary>
    public int Things => things.Count;

    /// <summary>Gets how many pawns were added.</summary>
    public int Pawns => pawns.Count;

    /// <summary>Adds the map's size; call it once, before the cells.</summary>
    /// <param name="x">Cells east to west.</param>
    /// <param name="z">Cells north to south.</param>
    public void AddSize(int x, int z)
    {
        grid = Field(grid, "size");
        grid = Field(grid, Number(x));
        grid = Record(grid, Number(z));
    }

    /// <summary>Adds the next cell, in the map's cell-index order.</summary>
    /// <param name="terrain">Its terrain's def name, or null.</param>
    /// <param name="roof">Its roof's def name, or null for none.</param>
    public void AddCell(string? terrain, string? roof)
    {
        grid = Field(grid, terrain);
        grid = Record(grid, roof);
        Cells++;
    }

    /// <summary>Adds a thing; things may come in any order.</summary>
    /// <param name="def">Its def name.</param>
    /// <param name="x">Its cell's x.</param>
    /// <param name="z">Its cell's z.</param>
    /// <param name="rotation">Its rotation, 0 to 3.</param>
    /// <param name="stuff">Its stuff's def name, or null.</param>
    /// <param name="stackCount">Its stack count.</param>
    public void AddThing(
        string? def, int x, int z, int rotation, string? stuff, int stackCount)
    {
        ulong h = Fnv1a.Offset64;
        h = Field(h, def);
        h = Field(h, Number(x));
        h = Field(h, Number(z));
        h = Field(h, Number(rotation));
        h = Field(h, stuff);
        things.Add(Record(h, Number(stackCount)));
    }

    /// <summary>Adds a pawn; pawns may come in any order.</summary>
    /// <param name="name">Its full name, or null for none.</param>
    /// <param name="kind">Its kind's def name.</param>
    /// <param name="faction">Its faction, or null for none.</param>
    public void AddPawn(string? name, string? kind, string? faction)
    {
        ulong h = Fnv1a.Offset64;
        h = Field(h, name);
        h = Field(h, kind);
        pawns.Add(Record(h, faction));
    }

    /// <summary>
    /// Gets the fingerprint: the grid's hash, then the things' hashes in
    /// sorted order, then the pawns', each part headed by its count.
    /// </summary>
    public ulong Value
    {
        get
        {
            ulong h = Fnv1a.Append64(Fnv1a.Offset64, grid);
            h = Part(h, things);
            return Part(h, pawns);
        }
    }

    /// <summary>Gets the fingerprint as sixteen lowercase hex digits.</summary>
    public string Hex => Value.ToString("x16", CultureInfo.InvariantCulture);

    /// <summary>
    /// Folds one unordered part into the hash: its count, then its hashes
    /// in sorted order.
    /// </summary>
    /// <param name="hash">The hash so far.</param>
    /// <param name="part">The part's hashes.</param>
    /// <returns>The hash.</returns>
    private static ulong Part(ulong hash, List<ulong> part)
    {
        var sorted = new List<ulong>(part);
        sorted.Sort();
        hash = Fnv1a.Append64(hash, (ulong)sorted.Count);
        foreach (ulong item in sorted)
        {
            hash = Fnv1a.Append64(hash, item);
        }

        return hash;
    }

    /// <summary>Hashes one field and its separator.</summary>
    /// <param name="hash">The hash so far.</param>
    /// <param name="text">The field; null hashes as empty.</param>
    /// <returns>The hash.</returns>
    private static ulong Field(ulong hash, string? text) =>
        Fnv1a.AppendByte64(Fnv1a.Append64(hash, text), FieldMark);

    /// <summary>Hashes a record's last field and its end mark.</summary>
    /// <param name="hash">The hash so far.</param>
    /// <param name="text">The field; null hashes as empty.</param>
    /// <returns>The hash.</returns>
    private static ulong Record(ulong hash, string? text) =>
        Fnv1a.AppendByte64(Fnv1a.Append64(hash, text), RecordMark);

    /// <summary>A number as invariant text.</summary>
    /// <param name="n">The number.</param>
    /// <returns>The text.</returns>
    private static string Number(int n) => n.ToString(CultureInfo.InvariantCulture);
}
