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
/// stray colonist inside the pull to make the robot protest. The robot
/// starts at the barn's door and is Nosy (slice 18500): with the tour's
/// Popular pull at 33.75 tiles, a robot that stops more than about 8
/// tiles short of a party it can see from the door (45 tiles at most)
/// plants within its own pull of the barn. Nosy, it stops 3 tiles short,
/// about 38 tiles from the barn, with the raiders inside its blast.
/// </summary>
public static class SingleTractorLayout
{
    /// <summary>
    /// How close the scene's robot drives, and what the barn's pane says
    /// its new robots get: Nosy (Prudent before the pulls were raised by
    /// half, when it stopped 15 tiles short).
    /// </summary>
    public const TractorBombProximity TourProximity =
        TractorBombProximity.Nosy;

    /// <summary>
    /// How far north the stray is sent once the robot protests, in tiles:
    /// from <see cref="Stray"/>, beyond the tour's flee distance (41.75
    /// tiles) from where the robot plants.
    /// </summary>
    public const int StrayRetreat = 30;

    /// <summary>The barn's centre.</summary>
    public static readonly Cell Barn = new(-30, 0);

    /// <summary>
    /// The barn's door, the robot's start: the barn's interaction cell,
    /// two tiles south of its centre.
    /// </summary>
    public static readonly Cell BarnDoor = Barn + new Cell(0, -2);

    /// <summary>The party's centre.</summary>
    public static readonly Cell PartyCentre = new(12, 0);

    /// <summary>Where the four raiders stand.</summary>
    public static readonly IReadOnlyList<Cell> Party =
        TwinJunkyardLayout.Cells((12, 0), (13, 1), (13, -1), (14, 0));

    /// <summary>
    /// Where the stray colonist stands: about 21 tiles north-west of where
    /// the robot is predicted to plant, (9,0), so inside its 33.75-tile
    /// pull (the robot protests) but well outside its 12-tile blast, and
    /// off the robot's road. Sent north by <see cref="StrayRetreat"/>, it
    /// is out of the pull after about 17 tiles, well within the protest,
    /// the tyres' flip and the shortest countdown. (It stood 7.6 tiles
    /// from the plant before, and was downed there on two trial seeds.)
    /// </summary>
    public static readonly Cell Stray = new(-8, 12);

    /// <summary>
    /// How far the scene's robot stops from its quarry: Nosy, 3 tiles,
    /// whatever the pull.
    /// </summary>
    public static double TourStop =>
        TractorBombApproach.StopDistance(
            TourProximity,
            TractorBombApproach.DefaultTimidStop,
            TractorBombYield.Radius(TourStagePlanner.TourPull));

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
        TractorPlanting.PlantFrom(BarnDoor, Party, TourStop);
}

/// <summary>
/// The junk the tour lays in its tractor yard (slice 19000d), as stacks
/// at offsets from the yard's centre: for the single tractor, whose yard
/// held only the barn and the party, so its pull looked sparse; and an
/// outer ring for the twins, beyond the junkyard, so their pulls drag
/// things in from afar. Each piece lies inside the yard, inside a pull
/// and outside every blast (so it is seen to slide before the blast takes
/// it), and clear of the robots' roads, the barn and the stray's way out.
/// Chunks and crates count one; the stacks are bright metals and stone,
/// which read against the yard's concrete where steel would not, and
/// nothing that burns or goes off when hit. The smoke runner's junkyard
/// (<see cref="TwinJunkyardLayout"/>) is unchanged.
/// </summary>
public static class TourYardJunk
{
    /// <summary>The slag chunk's def.</summary>
    public const string Slag = TwinJunkyardLayout.SlagDef;

    /// <summary>The rock chunk's def.</summary>
    public const string Rock = TwinJunkyardLayout.RockDef;

    /// <summary>The ancient crate's def (a building, torn down by a pull).</summary>
    public const string Crate = TwinJunkyardLayout.CrateDef;

    /// <summary>
    /// The single tractor's yard: east of the party, south of the road,
    /// and north-east of the stray, 13 to 31 tiles from where the robot
    /// is predicted to plant, (9,0).
    /// </summary>
    public static readonly IReadOnlyList<LaidStack> Single = new[]
    {
        new LaidStack(Slag, new Cell(24, 6), 1),
        new LaidStack(Slag, new Cell(29, -12), 1),
        new LaidStack(Slag, new Cell(-6, -9), 1),
        new LaidStack(Slag, new Cell(16, 11), 1),
        new LaidStack(Slag, new Cell(-18, -7), 1),
        new LaidStack(Slag, new Cell(31, 1), 1),
        new LaidStack(Rock, new Cell(27, -4), 1),
        new LaidStack(Rock, new Cell(6, -13), 1),
        new LaidStack(Rock, new Cell(0, 11), 1),
        new LaidStack(Rock, new Cell(-12, -12), 1),
        new LaidStack(Rock, new Cell(26, 13), 1),
        new LaidStack("Silver", new Cell(23, -10), 60),
        new LaidStack("Gold", new Cell(14, -13), 20),
        new LaidStack("Jade", new Cell(8, 14), 15),
        new LaidStack("Uranium", new Cell(30, 10), 20),
        new LaidStack("Plasteel", new Cell(-3, -14), 25),
        new LaidStack("ComponentIndustrial", new Cell(0, -10), 8),
        new LaidStack("Silver", new Cell(22, -1), 40),
        new LaidStack("Jade", new Cell(-18, 10), 15),
        new LaidStack(Crate, new Cell(20, -6), 1),
        new LaidStack(Crate, new Cell(-10, -6), 1),
        new LaidStack(Crate, new Cell(28, 5), 1),
    };

    /// <summary>
    /// The twins' outer ring: north and south of the junkyard and at its
    /// ends, 13 to 33 tiles from the nearer of the two predicted plants.
    /// </summary>
    public static readonly IReadOnlyList<LaidStack> TwinsOuter = new[]
    {
        new LaidStack(Slag, new Cell(-24, 12), 1),
        new LaidStack(Rock, new Cell(-14, 13), 1),
        new LaidStack("Silver", new Cell(-4, 13), 40),
        new LaidStack(Slag, new Cell(6, 13), 1),
        new LaidStack("Gold", new Cell(16, 12), 15),
        new LaidStack(Rock, new Cell(26, 11), 1),
        new LaidStack(Rock, new Cell(-26, -11), 1),
        new LaidStack("Jade", new Cell(-16, -13), 15),
        new LaidStack(Slag, new Cell(-2, -14), 1),
        new LaidStack("Uranium", new Cell(10, -13), 20),
        new LaidStack(Slag, new Cell(20, -12), 1),
        new LaidStack("Plasteel", new Cell(28, -10), 25),
        new LaidStack(Crate, new Cell(30, 6), 1),
        new LaidStack(Slag, new Cell(31, -5), 1),
        new LaidStack(Crate, new Cell(-30, 6), 1),
        new LaidStack("Silver", new Cell(-31, -5), 40),
    };
}
