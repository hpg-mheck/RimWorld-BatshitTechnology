using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// A town's quality tier, as the Colony Bomb names them
/// (docs/COLONY-BOMB.md). AnyRim is Medium.
/// </summary>
public enum TownQuality
{
    /// <summary>Slum (vanilla Awful).</summary>
    Slum,

    /// <summary>Low (vanilla Poor).</summary>
    Low,

    /// <summary>Medium (vanilla Normal): AnyRim.</summary>
    Medium,

    /// <summary>High (vanilla Good).</summary>
    High,

    /// <summary>Resort (vanilla Excellent).</summary>
    Resort,

    /// <summary>Epic (vanilla Masterwork).</summary>
    Epic,
}

/// <summary>
/// The surfaces and materials a town of a given quality is built from.
/// Every string is a defName (TerrainDef, ThingDef, ColorDef) or a
/// QualityCategory name.
/// </summary>
/// <param name="Quality">The tier.</param>
/// <param name="Carriageway">The road surface.</param>
/// <param name="CarriagewayPaint">Paint for the road surface, or null.</param>
/// <param name="CentreLinePaint">Paint for the dashed centre line, or null for none.</param>
/// <param name="Sidewalk">The sidewalk surface.</param>
/// <param name="TaperNear">The first stretch of road past the town.</param>
/// <param name="TaperMid">The middle stretch.</param>
/// <param name="TaperFar">The last stretch, before nothing.</param>
/// <param name="FenceStuff">Stuff for picket fences and gates.</param>
/// <param name="FurnitureQuality">QualityCategory for furniture.</param>
public sealed record TownPalette(
    TownQuality Quality, string Carriageway, string? CarriagewayPaint,
    string? CentreLinePaint, string Sidewalk, string TaperNear, string TaperMid,
    string TaperFar, string FenceStuff, string FurnitureQuality)
{
    /// <summary>
    /// The palette for a tier. Medium is AnyRim as docs/SHOWCASE.md lays
    /// it out; the others follow the quality table in docs/COLONY-BOMB.md
    /// (roads from packed dirt up to gold tile), using only vanilla Core
    /// defs. Roads always degrade as they leave town.
    /// </summary>
    /// <param name="quality">The tier.</param>
    /// <returns>Its palette.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The tier is not one of the six.
    /// </exception>
    public static TownPalette For(TownQuality quality) => quality switch
    {
        TownQuality.Slum => new TownPalette(
            quality, "PackedDirt", null, null, "Gravel",
            "PackedDirt", "PackedDirt", "PackedDirt", "WoodLog", "Awful"),
        TownQuality.Low => new TownPalette(
            quality, "Gravel", null, null, "PackedDirt",
            "Gravel", "PackedDirt", "PackedDirt", "WoodLog", "Poor"),
        TownQuality.Medium => new TownPalette(
            quality, "Concrete", "Structure_GreyDark",
            "Structure_YellowPastel", "PavedTile",
            "BrokenAsphalt", "Gravel", "PackedDirt", "WoodLog", "Normal"),
        TownQuality.High => new TownPalette(
            quality, "Concrete", "Structure_Black", "Structure_White",
            "PavedTile", "Concrete", "BrokenAsphalt", "Gravel", "WoodLog",
            "Good"),
        TownQuality.Resort => new TownPalette(
            quality, "PavedTile", null, "Structure_White",
            "FlagstoneMarble", "Concrete", "BrokenAsphalt", "Gravel",
            "BlocksMarble", "Excellent"),
        TownQuality.Epic => new TownPalette(
            quality, "GoldTile", null, "Structure_White", "SilverTile",
            "PavedTile", "Concrete", "BrokenAsphalt", "Gold", "Masterwork"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(quality), quality, "Not a town quality tier."),
    };
}

/// <summary>Where a def's things sit, for overlap rules.</summary>
public enum ThingLayer
{
    /// <summary>
    /// One per cell: walls, doors, fences, furniture and devices (RimWorld
    /// edifices and other buildings that block a cell).
    /// </summary>
    Building,

    /// <summary>Under everything: power conduits.</summary>
    Conduit,

    /// <summary>On the floor or on furniture that holds items.</summary>
    Item,

    /// <summary>In the ground or a plant pot.</summary>
    Plant,

    /// <summary>A creature.</summary>
    Pawn,
}

/// <summary>The facts a town plan needs about a def it uses.</summary>
/// <param name="SizeX">size.x.</param>
/// <param name="SizeZ">size.z.</param>
/// <param name="Layer">Its layer.</param>
/// <param name="ItemCapacity">
/// Items its cells may hold (0: none, 1: a table's surface, 3: a shelf).
/// </param>
/// <param name="StackLimit">Items: the stack limit; 1 for everything else.</param>
public sealed record DefInfo(int SizeX, int SizeZ, ThingLayer Layer, int ItemCapacity, int StackLimit)
{
    /// <summary>
    /// Whether pawns can walk through its cells: RimWorld's Standable and
    /// PassThroughOnly count as walkable, Impassable does not.
    /// </summary>
    public bool Passable { get; init; } = true;

    /// <summary>
    /// The def's interactionCellOffset as seen facing north, or null if it
    /// has no interaction cell. RimWorld rotates it with IntVec3.RotatedBy
    /// and adds it to the thing's Position (ThingUtility.InteractionCell);
    /// <see cref="Footprint.InteractionCell"/> does the same.
    /// </summary>
    public Cell? InteractionCell { get; init; }

    /// <summary>
    /// Whether it draws power through a connection (CompPowerTrader), and
    /// so needs a transmitter within 6 cells (box) of its Position.
    /// </summary>
    public bool Powered { get; init; }

    /// <summary>
    /// Whether it carries the power net itself: conduits and plants with
    /// transmitsPower, such as the Batshittium Reactor.
    /// </summary>
    public bool TransmitsPower { get; init; }

    /// <summary>A door or fence gate: walkable, but a room boundary.</summary>
    public bool IsDoor { get; init; }

    /// <summary>A wall: impassable, a room boundary and a roof holder.</summary>
    public bool IsWall { get; init; }

    /// <summary>
    /// A fence: people step over it, roaming animals do not. Town plans
    /// treat fences as barriers, so pens and yards are entered by gates.
    /// </summary>
    public bool IsFence { get; init; }

    /// <summary>Whether the def may face other than north.</summary>
    public bool Rotatable { get; init; } = true;

    /// <summary>
    /// The stuff categories it can be made from ("Woody", "Metallic",
    /// "Stony", "Fabric", "Leathery"); empty if it takes no stuff.
    /// </summary>
    public IReadOnlyList<string> AcceptsStuff { get; init; } =
        Array.Empty<string>();

    /// <summary>For stuff items: the stuff categories they belong to.</summary>
    public IReadOnlyList<string> StuffCategories { get; init; } =
        Array.Empty<string>();

    /// <summary>Whether it has a quality (CompQuality).</summary>
    public bool HasQuality { get; init; }

    /// <summary>Whether it can be switched off (CompFlickable).</summary>
    public bool Flickable { get; init; }

    /// <summary>Whether it takes fuel (CompRefuelable).</summary>
    public bool Refuelable { get; init; }

    /// <summary>Whether it is a work table with bills.</summary>
    public bool HasBills { get; init; }

    /// <summary>Whether it can be minified (packed into a crate).</summary>
    public bool Minifiable { get; init; }

    /// <summary>
    /// Ballista bolts it holds (the Ballista's own 16, a magazine's
    /// 2,560); 0 for everything else.
    /// </summary>
    public int LoadCapacity { get; init; }
}

/// <summary>
/// A catalogue of the vanilla and mod defs a town plan uses, with the sizes
/// and limits the plan's geometry depends on. The XML tests check it
/// against RimWorld's own data where the game is installed.
/// </summary>
public static class TownDefs
{
    /// <summary>Every def the catalogue knows, by defName.</summary>
    public static IReadOnlyDictionary<string, DefInfo> All => TownDefCatalogue.Entries;

    /// <summary>Looks a def up.</summary>
    /// <param name="defName">The defName.</param>
    /// <returns>Its facts.</returns>
    /// <exception cref="KeyNotFoundException">The def is not catalogued.</exception>
    public static DefInfo Get(string defName) =>
        All.TryGetValue(defName, out DefInfo? info)
            ? info
            : throw new KeyNotFoundException($"{defName} is not in the town catalogue.");
}
