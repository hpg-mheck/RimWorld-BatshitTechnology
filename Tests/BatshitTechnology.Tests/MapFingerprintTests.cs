using System;
using System.Collections.Generic;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the map fingerprint behind the MAPHASH and STAGEHASH cues
/// (docs/SHOWCASE-API.md, "Fingerprints"): stable, blind to the order
/// things and pawns are listed in, and changed by every field it covers.
/// </summary>
public sealed class MapFingerprintTests
{
    /// <summary>One thing: def, x, z, rotation, stuff, stack count.</summary>
    private sealed record ThingRow(
        string Def, int X, int Z, int Rotation, string? Stuff, int Stack);

    /// <summary>One pawn: name, kind, faction.</summary>
    private sealed record PawnRow(string? Name, string Kind, string? Faction);

    /// <summary>The small map most tests start from.</summary>
    private static readonly (string Terrain, string? Roof)[] Cells =
    {
        ("Soil", null), ("Sand", "RoofConstructed"),
        ("WaterShallow", null), ("Soil", "RoofRockThin"),
    };

    /// <summary>The small map's things.</summary>
    private static readonly ThingRow[] ThingsRows =
    {
        new ThingRow("Steel", 0, 1, 0, null, 75),
        new ThingRow("Wall", 1, 1, 2, "BlocksGranite", 1),
    };

    /// <summary>The small map's pawns.</summary>
    private static readonly PawnRow[] PawnRows =
    {
        new PawnRow("Bob Smith", "Colonist", "PlayerColony/New Arrivals"),
        new PawnRow(string.Empty, "Boomalope", null),
    };

    /// <summary>Fingerprints a map from its parts.</summary>
    /// <param name="cells">The cells, in order.</param>
    /// <param name="things">The things, in the order to add them.</param>
    /// <param name="pawns">The pawns, in the order to add them.</param>
    /// <param name="sizeX">The map's width.</param>
    /// <returns>The fingerprint.</returns>
    private static MapFingerprint Print(
        IEnumerable<(string Terrain, string? Roof)> cells,
        IEnumerable<ThingRow> things,
        IEnumerable<PawnRow> pawns,
        int sizeX = 2)
    {
        var print = new MapFingerprint();
        print.AddSize(sizeX, 2);
        foreach ((string terrain, string? roof) in cells)
        {
            print.AddCell(terrain, roof);
        }

        foreach (ThingRow t in things)
        {
            print.AddThing(t.Def, t.X, t.Z, t.Rotation, t.Stuff, t.Stack);
        }

        foreach (PawnRow p in pawns)
        {
            print.AddPawn(p.Name, p.Kind, p.Faction);
        }

        return print;
    }

    /// <summary>The small map's fingerprint.</summary>
    /// <returns>The fingerprint.</returns>
    private static ulong Base() => Print(Cells, ThingsRows, PawnRows).Value;

    /// <summary>
    /// The fingerprint is pinned against an independent reference written
    /// in Python (the same records, separators and sort), so it means the
    /// same in every process; it counts what was added and prints as 16
    /// lowercase hex digits.
    /// </summary>
    [Fact(DisplayName = "The fingerprint matches its independent reference")]
    public void Value_should_match_the_reference()
    {
        MapFingerprint print = Print(Cells, ThingsRows, PawnRows);
        Assert.Equal(0xb233d7a37a6affdfUL, print.Value);
        Assert.Equal("b233d7a37a6affdf", print.Hex);
        Assert.Equal(4, print.Cells);
        Assert.Equal(2, print.Things);
        Assert.Equal(2, print.Pawns);

        var empty = new MapFingerprint();
        empty.AddSize(0, 0);
        Assert.Equal("50d3b6b96453f4f7", empty.Hex);
    }

    /// <summary>
    /// Things and pawns may be listed in any order: the same multiset
    /// gives the same fingerprint, and reading it twice changes nothing.
    /// </summary>
    [Fact(DisplayName = "Things and pawns may come in any order")]
    public void Value_should_ignore_the_order_of_things_and_pawns()
    {
        var things = new List<ThingRow>(ThingsRows);
        var pawns = new List<PawnRow>(PawnRows);
        things.Reverse();
        pawns.Reverse();
        MapFingerprint print = Print(Cells, things, pawns);
        Assert.Equal(Base(), print.Value);
        Assert.Equal(print.Value, print.Value);
    }

    /// <summary>
    /// Cells are a grid, so their order counts: swapping two cells is a
    /// different map. So does the size, and a duplicated thing.
    /// </summary>
    [Fact(DisplayName = "Cell order, size and duplicates count")]
    public void Value_should_count_cell_order_size_and_duplicates()
    {
        var swapped = new List<(string, string?)>(Cells);
        (swapped[0], swapped[1]) = (swapped[1], swapped[0]);
        Assert.NotEqual(Base(), Print(swapped, ThingsRows, PawnRows).Value);
        Assert.NotEqual(Base(), Print(Cells, ThingsRows, PawnRows, sizeX: 3).Value);

        var doubled = new List<ThingRow>(ThingsRows) { ThingsRows[0] };
        Assert.NotEqual(Base(), Print(Cells, doubled, PawnRows).Value);
    }

    /// <summary>
    /// Every field the fingerprint covers changes it: a cell's terrain and
    /// roof, a thing's def, cell, rotation, stuff and stack, and a pawn's
    /// name, kind and faction.
    /// </summary>
    [Fact(DisplayName = "Every covered field changes the fingerprint")]
    public void Value_should_change_with_every_field()
    {
        ulong original = Base();
        var changes = new List<(string What, ulong Value)>();

        var terrain = new List<(string, string?)>(Cells) { [2] = ("Mud", null) };
        changes.Add(("terrain", Print(terrain, ThingsRows, PawnRows).Value));
        var roof = new List<(string, string?)>(Cells) { [2] = ("WaterShallow", "RoofRockThick") };
        changes.Add(("roof", Print(roof, ThingsRows, PawnRows).Value));

        ThingRow t = ThingsRows[1];
        foreach ((string what, ThingRow row) in new[]
        {
            ("def", t with { Def = "Door" }),
            ("x", t with { X = 0 }),
            ("z", t with { Z = 0 }),
            ("rotation", t with { Rotation = 1 }),
            ("stuff", t with { Stuff = "Steel" }),
            ("no stuff", t with { Stuff = null }),
            ("stack", t with { Stack = 2 }),
        })
        {
            changes.Add((what, Print(Cells, new[] { ThingsRows[0], row }, PawnRows).Value));
        }

        PawnRow p = PawnRows[0];
        foreach ((string what, PawnRow row) in new[]
        {
            ("name", p with { Name = "Bob Smyth" }),
            ("kind", p with { Kind = "Slave" }),
            ("faction", p with { Faction = "PlayerColony/Old Arrivals" }),
        })
        {
            changes.Add((what, Print(Cells, ThingsRows, new[] { row, PawnRows[1] }).Value));
        }

        foreach ((string what, ulong value) in changes)
        {
            Assert.True(value != original, $"changing the {what} left the fingerprint alone");
        }
    }

    /// <summary>
    /// Fields are kept apart: text moved from one field to the next is a
    /// different record, so "ab" then "c" is not "a" then "bc".
    /// </summary>
    [Fact(DisplayName = "Fields are kept apart")]
    public void Value_should_keep_fields_apart()
    {
        MapFingerprint first = Print(
            new (string, string?)[] { ("ab", "c") }, Array.Empty<ThingRow>(),
            Array.Empty<PawnRow>());
        MapFingerprint second = Print(
            new (string, string?)[] { ("a", "bc") }, Array.Empty<ThingRow>(),
            Array.Empty<PawnRow>());
        Assert.NotEqual(first.Value, second.Value);

        var thingThenPawn = new MapFingerprint();
        thingThenPawn.AddThing("Steel", 0, 0, 0, null, 1);
        var pawnOnly = new MapFingerprint();
        pawnOnly.AddPawn("Steel", "0", "0");
        Assert.NotEqual(thingThenPawn.Value, pawnOnly.Value);
    }
}
