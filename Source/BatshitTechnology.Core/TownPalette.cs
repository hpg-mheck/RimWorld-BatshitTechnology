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
    /// <summary>The palette for a tier.</summary>
    /// <param name="quality">The tier.</param>
    /// <returns>Its palette.</returns>
    public static TownPalette For(TownQuality quality) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");
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
public sealed record DefInfo(int SizeX, int SizeZ, ThingLayer Layer, int ItemCapacity, int StackLimit);

/// <summary>
/// A catalogue of the vanilla and mod defs a town plan uses, with the sizes
/// and limits the plan's geometry depends on. The XML tests check it
/// against RimWorld's own data where the game is installed.
/// </summary>
public static class TownDefs
{
    /// <summary>Every def the catalogue knows, by defName.</summary>
    public static IReadOnlyDictionary<string, DefInfo> All =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

    /// <summary>Looks a def up.</summary>
    /// <param name="defName">The defName.</param>
    /// <returns>Its facts.</returns>
    /// <exception cref="KeyNotFoundException">The def is not catalogued.</exception>
    public static DefInfo Get(string defName) =>
        All.TryGetValue(defName, out DefInfo? info)
            ? info
            : throw new KeyNotFoundException($"{defName} is not in the town catalogue.");
}
