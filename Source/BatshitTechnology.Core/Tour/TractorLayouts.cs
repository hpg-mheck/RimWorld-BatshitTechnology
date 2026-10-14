using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>A stack of items laid at a spot: its def, where, how many.</summary>
/// <param name="DefName">The ThingDef's defName.</param>
/// <param name="At">Where, from the layout's centre.</param>
/// <param name="Count">How many.</param>
public readonly record struct LaidStack(string DefName, Cell At, int Count);

/// <summary>
/// Where a robot plants itself, predicted from the Tractor Bomb's rules
/// (docs/TRACTOR-BOMB.md): it sees hostiles within its sight, picks the
/// nearest, and stops its disposition's distance short of it, on the
/// straight line from where it starts. Pathing and line of sight can move
/// the real spot a tile or two; the layouts keep wide margins.
/// </summary>
public static class TractorPlanting
{
    /// <summary>
    /// How far a robot sees, in tiles: the robot def's sightRadius, which
    /// an XML test keeps equal to this.
    /// </summary>
    public const int RobotSight = 45;

    /// <summary>
    /// Where a robot starting at a cell plants itself, or null if it sees
    /// none of the party.
    /// </summary>
    /// <param name="robot">Where it starts.</param>
    /// <param name="party">The hostiles.</param>
    /// <param name="stop">Its stopping distance, in tiles.</param>
    /// <returns>The predicted cell, or null.</returns>
    public static Cell? PlantFrom(Cell robot, IEnumerable<Cell> party, double stop)
    {
        Cell? nearest = (party ?? Enumerable.Empty<Cell>())
            .Where(p => p.DistanceTo(robot) <= RobotSight)
            .OrderBy(p => p.DistanceTo(robot))
            .Cast<Cell?>()
            .FirstOrDefault();
        if (nearest is not Cell quarry)
        {
            return null;
        }

        double gap = quarry.DistanceTo(robot);
        if (gap <= stop)
        {
            return robot;
        }

        double t = stop / gap;
        return new Cell(
            (int)Math.Round(quarry.X + ((robot.X - quarry.X) * t)),
            (int)Math.Round(quarry.Z + ((robot.Z - quarry.Z) * t)));
    }
}

/// <summary>
/// The twin-bomb junkyard (the smoke test's, and the tour's finale), as
/// offsets from its centre: steel slag and rock chunks, loose stacks,
/// small shelves holding stacks, ancient crates, fourteen raiders round
/// the middle, and two robots driving in from either side. None of the
/// junk lies on the robots' road in or where they plant.
/// </summary>
public static class TwinJunkyardLayout
{
    /// <summary>The slag chunks' def.</summary>
    public const string SlagDef = "ChunkSlagSteel";

    /// <summary>The rock chunks' def (another stone chunk if missing).</summary>
    public const string RockDef = "ChunkGranite";

    /// <summary>The crates of items: small shelves.</summary>
    public const string ShelfDef = "ShelfSmall";

    /// <summary>The closed crates.</summary>
    public const string CrateDef = "AncientCrate";

    /// <summary>How far either side of the army the robots start, in tiles.</summary>
    public const int DriveInDistance = 28;

    /// <summary>Where the slag chunks lie.</summary>
    public static readonly IReadOnlyList<Cell> Slag = Cells(
        (-9, -5), (-8, 4), (-6, -3), (-5, 6), (-2, -6), (2, 6),
        (5, -6), (6, 3), (8, -4), (9, 5), (-1, 6), (1, -6));

    /// <summary>Where the rock chunks lie.</summary>
    public static readonly IReadOnlyList<Cell> Rock = Cells(
        (-7, -6), (-4, 5), (0, -5), (0, 5), (4, -5), (7, 6),
        (-2, 3), (2, -3));

    /// <summary>The loose stacks.</summary>
    public static readonly IReadOnlyList<LaidStack> Stacks = new[]
    {
        new LaidStack("Steel", new Cell(-9, 3), 30),
        new LaidStack("Plasteel", new Cell(-6, 3), 10),
        new LaidStack("Cloth", new Cell(-4, -5), 20),
        new LaidStack("Steel", new Cell(6, -3), 30),
        new LaidStack("Cloth", new Cell(2, 2), 20),
        new LaidStack("Jade", new Cell(5, 4), 10),
        new LaidStack("Uranium", new Cell(9, -3), 10),
        new LaidStack("ComponentIndustrial", new Cell(-2, -2), 5),
        new LaidStack("Steel", new Cell(0, 3), 25),
        new LaidStack("Plasteel", new Cell(0, -3), 10),
    };

    /// <summary>Where the small shelves stand.</summary>
    public static readonly IReadOnlyList<Cell> Shelves = Cells(
        (-5, -3), (5, 2), (-1, -5));

    /// <summary>What each shelf holds: def and count, one stack each.</summary>
    public static readonly IReadOnlyList<(string DefName, int Count)> ShelfStock =
        new[] { ("Plasteel", 10), ("Jade", 8), ("ComponentIndustrial", 4) };

    /// <summary>Where the ancient crates stand.</summary>
    public static readonly IReadOnlyList<Cell> Crates = Cells(
        (-7, 3), (3, 6), (7, -5));

    /// <summary>Where the fourteen raiders stand.</summary>
    public static readonly IReadOnlyList<Cell> Army = Cells(
        (-4, 0), (4, 0), (-3, -3), (-3, 3), (3, -3), (3, 3),
        (-1, -4), (-1, 4), (1, -4), (1, 4), (-2, 1), (2, -1),
        (0, 2), (0, -2));

    /// <summary>Where the two robots start: west, then east.</summary>
    public static readonly IReadOnlyList<Cell> DriveIn = Cells(
        (-DriveInDistance, 0), (DriveInDistance, 0));

    /// <summary>Every offset the layout uses, for bounds checks.</summary>
    /// <returns>The offsets.</returns>
    public static IEnumerable<Cell> AllOffsets() =>
        Slag.Concat(Rock).Concat(Stacks.Select(s => s.At)).Concat(Shelves)
            .Concat(Crates).Concat(Army).Concat(DriveIn);

    /// <summary>
    /// Where the two Nosy robots are predicted to plant, west then east;
    /// null for one that sees none of the army.
    /// </summary>
    /// <returns>The two predictions.</returns>
    public static IReadOnlyList<Cell?> PredictedPlants() =>
        DriveIn.Select(d => TractorPlanting.PlantFrom(
                d, Army, TractorBombApproach.NosyStop))
            .ToList();

    /// <summary>Makes a list of cells from (x,z) pairs.</summary>
    /// <param name="cells">The pairs.</param>
    /// <returns>The cells.</returns>
    internal static IReadOnlyList<Cell> Cells(params (int X, int Z)[] cells) =>
        cells.Select(c => new Cell(c.X, c.Z)).ToList();
}

/// <summary>
/// The single-tractor scene's yard, as offsets from its centre: the
/// Tractor Barn to the west, a party of four raiders to the east, and a
/// stray colonist close enough to where the robot plants to make it
/// protest. The robot starts at the barn's door.
/// </summary>
public static class SingleTractorLayout
{
    /// <summary>The barn's centre.</summary>
    public static readonly Cell Barn = new(-24, 0);

    /// <summary>
    /// The barn's door, the robot's start: the barn's interaction cell,
    /// two tiles south of its centre.
    /// </summary>
    public static readonly Cell BarnDoor = Barn + new Cell(0, -2);

    /// <summary>The party's centre.</summary>
    public static readonly Cell PartyCentre = new(8, 0);

    /// <summary>Where the four raiders stand.</summary>
    public static readonly IReadOnlyList<Cell> Party =
        TwinJunkyardLayout.Cells((8, 0), (9, 1), (9, -1), (10, 0));

    /// <summary>
    /// Where the stray colonist stands: six tiles north of the planting
    /// spot, inside the pull, so the robot protests.
    /// </summary>
    public static readonly Cell Stray = new(0, 6);

    /// <summary>
    /// How far a robot of the default disposition stops from its quarry:
    /// one tile inside the default pull, within its 15-tile emplacing
    /// range.
    /// </summary>
    public static double DefaultStop =>
        TractorBombApproach.StopDistance(
            TractorBombApproach.Default,
            TractorBombApproach.DefaultCautiousStop,
            TractorBombYield.DefaultPull);

    /// <summary>Every offset the layout uses, for bounds checks.</summary>
    /// <returns>The offsets.</returns>
    public static IEnumerable<Cell> AllOffsets() =>
        new[] { Barn, BarnDoor, PartyCentre, Stray }.Concat(Party);

    /// <summary>
    /// Where the robot is predicted to plant, or null if it sees none of
    /// the party from the barn's door.
    /// </summary>
    /// <returns>The prediction.</returns>
    public static Cell? PredictedPlant() =>
        TractorPlanting.PlantFrom(BarnDoor, Party, DefaultStop);
}
