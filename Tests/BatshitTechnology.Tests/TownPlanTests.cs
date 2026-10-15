using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="TownPlan.AnyRim"/> against docs/SHOWCASE.md and the
/// rules RimWorld will enforce when the game half builds it: nothing
/// overlaps that shouldn't, doors and interaction cells are usable, every
/// room is reachable, power reaches everything, the heat and cook-off
/// rules hold, and the seed only moves the road tapers.
/// </summary>
public sealed class TownPlanTests
{
    private static readonly TownPlan Plan =
        TownPlan.AnyRim(1, TownPalette.For(TownQuality.Medium));

    // ===== Determinism =====

    /// <summary>The same seed always gives the same town.</summary>
    [Fact(DisplayName = "The same seed gives the same plan")]
    public void AnyRim_should_be_deterministic()
    {
        TownPlan again = TownPlan.AnyRim(1, TownPalette.For(TownQuality.Medium));
        Assert.Equal(Describe(Plan, tapers: true), Describe(again, tapers: true));
        Assert.Equal(Plan.Parts, again.Parts);
    }

    /// <summary>
    /// Different seeds differ only in the road tapers: everything else,
    /// bounds and core included, is identical, and the tapers do vary.
    /// </summary>
    [Fact(DisplayName = "Different seeds differ only in the tapers")]
    public void AnyRim_seeds_should_only_move_tapers()
    {
        List<string> fixedPart = Describe(Plan, tapers: false);
        var taperSets = new HashSet<string>();
        foreach (int seed in new[] { 0, 2, 3, 7, 42, -5, int.MaxValue, int.MinValue })
        {
            TownPlan other = TownPlan.AnyRim(seed, TownPalette.For(TownQuality.Medium));
            Assert.Equal(fixedPart, Describe(other, tapers: false));
            Assert.Equal(Plan.Bounds, other.Bounds);
            Assert.Equal(Plan.Core, other.Core);
            Assert.Equal(seed, other.Seed);
            taperSets.Add(string.Join(";", other.Terrain.Where(t => t.Taper)
                .Select(t => $"{t.At}{t.Def}")));
        }

        Assert.True(taperSets.Count >= 3, $"only {taperSets.Count} distinct taper layouts");
    }

    /// <summary>A plan needs a palette.</summary>
    [Fact(DisplayName = "AnyRim rejects a missing palette")]
    public void AnyRim_should_reject_null_palette()
    {
        Assert.Throws<ArgumentNullException>(() => TownPlan.AnyRim(1, null!));
    }

    // ===== Overlaps, items and plants =====

    /// <summary>No two buildings, walls or pieces of furniture share a cell.</summary>
    [Fact(DisplayName = "Building-layer placements never overlap")]
    public void Buildings_should_not_overlap()
    {
        var seen = new Dictionary<Cell, Placement>();
        foreach (Placement p in Plan.Placements.Where(IsBuildingLayer))
        {
            foreach (Cell c in p.Occupied.Cells())
            {
                Assert.False(seen.TryGetValue(c, out Placement? other),
                    $"{p.Def} at {p.At} overlaps {other?.Def} at {c}");
                seen[c] = p;
            }
        }

        var conduits = Plan.Placements.Where(p => p.Def == "PowerConduit").Select(p => p.At).ToList();
        Assert.Equal(conduits.Count, conduits.Distinct().Count());
    }

    /// <summary>
    /// Items sit only where a cell can hold them: one on bare floor, one on
    /// a table, three on a shelf, none on a wall or anything else.
    /// </summary>
    [Fact(DisplayName = "Items respect each cell's capacity")]
    public void Items_should_respect_cell_capacity()
    {
        Dictionary<Cell, Placement> built = BuildingCells(Plan);
        foreach (IGrouping<Cell, Placement> cell in Plan.Placements
            .Where(p => p.Kind is PlacementKind.Item or PlacementKind.Crate)
            .GroupBy(p => p.At))
        {
            int capacity = built.TryGetValue(cell.Key, out Placement? holder)
                ? Info(holder).ItemCapacity
                : 1;
            Assert.True(cell.Count() <= capacity,
                $"{cell.Count()} items at {cell.Key} on {holder?.Def ?? "the floor"} (holds {capacity})");
        }
    }

    /// <summary>Every stack is between one and its def's stack limit.</summary>
    [Fact(DisplayName = "Item counts stay within stack limits")]
    public void Items_should_respect_stack_limits()
    {
        foreach (Placement p in Plan.Placements)
        {
            int limit = p.Kind == PlacementKind.Item ? Info(p).StackLimit : 1;
            Assert.InRange(p.Count, 1, limit);
        }

        foreach (Placement p in Plan.Placements.Where(p => p.Load.Count > 0))
        {
            Assert.True(p.Load.Sum(l => l.Count) <= Info(p).LoadCapacity, $"{p.Def} overloaded");
            Assert.All(p.Load, l => Assert.True(TownDefs.All.ContainsKey(l.Def), l.Def));
        }
    }

    /// <summary>
    /// Plants grow only on open ground or in a plant pot, one to a cell,
    /// in soil (or the pot).
    /// </summary>
    [Fact(DisplayName = "Plants never sit under buildings except plant pots")]
    public void Plants_should_not_sit_under_buildings()
    {
        Dictionary<Cell, Placement> built = BuildingCells(Plan);
        Dictionary<Cell, TerrainPlacement> ground = TerrainCells(Plan);
        var planted = new HashSet<Cell>();
        foreach (Placement p in Plan.Placements.Where(p => p.Kind == PlacementKind.Plant))
        {
            Assert.True(planted.Add(p.At), $"two plants at {p.At}");
            if (built.TryGetValue(p.At, out Placement? holder))
            {
                Assert.True(holder.Def == "PlantPot", $"{p.Def} under {holder.Def} at {p.At}");
            }
            else
            {
                Assert.True(ground.TryGetValue(p.At, out TerrainPlacement? t) && t.Def == "Soil",
                    $"{p.Def} at {p.At} is not in soil");
            }
        }
    }

    // ===== Buildings and rooms =====

    /// <summary>
    /// A building's placements lie inside its outer footprint, and inside
    /// an enclosed building every piece of furniture lies inside a room.
    /// </summary>
    [Fact(DisplayName = "Placements stay inside their building and rooms")]
    public void Placements_should_stay_inside_their_building()
    {
        Dictionary<string, BuildingPlan> byKey = Plan.Buildings.ToDictionary(b => b.Key);
        foreach (Placement p in Plan.Placements.Where(p => p.BuildingKey != null))
        {
            Assert.True(byKey.TryGetValue(p.BuildingKey!, out BuildingPlan? b), p.BuildingKey);
            Assert.True(b!.Outer.Contains(p.Occupied), $"{p.Def} at {p.At} outside {b.Key}");
            DefInfo info = Info(p);
            bool furniture = p.Kind == PlacementKind.Building && info.Layer == ThingLayer.Building
                && !info.IsWall && !info.IsDoor;
            if (b.Enclosed && (furniture || p.Kind != PlacementKind.Building))
            {
                Assert.True(p.Occupied.Cells().All(c => b.Rooms.Any(r => r.Contains(c))),
                    $"{p.Def} at {p.At} is not inside a room of {b.Key}");
            }
        }
    }

    /// <summary>
    /// An enclosed building is a closed shell of walls and doors, floored
    /// throughout and roofed over everything inside its outer walls; its
    /// rooms lie inside and do not overlap.
    /// </summary>
    [Fact(DisplayName = "Enclosed buildings are closed, floored and roofed")]
    public void Enclosed_buildings_should_be_closed_floored_and_roofed()
    {
        Dictionary<Cell, Placement> built = BuildingCells(Plan);
        Dictionary<Cell, TerrainPlacement> ground = TerrainCells(Plan);
        var roofed = Plan.Roofs.ToDictionary(r => r.At);
        foreach (BuildingPlan b in Plan.Buildings.Where(b => b.Enclosed))
        {
            foreach (Cell c in b.Outer.EdgeCells())
            {
                Assert.True(built.TryGetValue(c, out Placement? p)
                    && (Info(p).IsWall || Info(p).IsDoor), $"{b.Key} shell open at {c}");
            }

            foreach (Cell c in b.Outer.Cells())
            {
                Assert.True(ground.TryGetValue(c, out TerrainPlacement? t) && t.BuildingKey == b.Key,
                    $"{b.Key} has no floor at {c}");
            }

            foreach (Cell c in b.Outer.ExpandedBy(-1).Cells())
            {
                Assert.True(roofed.TryGetValue(c, out RoofPlacement? r) && r.BuildingKey == b.Key,
                    $"{b.Key} unroofed at {c}");
            }

            Assert.NotEmpty(b.Rooms);
            Assert.All(b.Rooms, r => Assert.True(b.Outer.ExpandedBy(-1).Contains(r), $"{b.Key} {r}"));
            var roomCells = b.Rooms.SelectMany(r => r.Cells()).ToList();
            Assert.Equal(roomCells.Count, roomCells.Distinct().Count());
        }

        Assert.All(Plan.Roofs, r => Assert.True(
            Plan.Buildings.Single(b => b.Key == r.BuildingKey).Enclosed, $"roof at {r.At}"));
        Box shelter = Plan.Buildings.Single(b => b.Key == "KnottShelter")
            .Outer.ExpandedBy(-1);
        var rock = Plan.Roofs.Where(r => r.Kind == RoofKind.RockThick)
            .Select(r => r.At).ToHashSet();
        Assert.True(rock.SetEquals(shelter.Cells()), "thick roof over the shelter only");
    }

    /// <summary>
    /// Every door and gate is set into a wall or fence line, with walkable
    /// cells on both sides.
    /// </summary>
    [Fact(DisplayName = "Doors sit in walls with walkable cells either side")]
    public void Doors_should_sit_in_walls()
    {
        Dictionary<Cell, Placement> built = BuildingCells(Plan);
        foreach (Placement door in Plan.Placements.Where(p => p.Kind == PlacementKind.Building && Info(p).IsDoor))
        {
            foreach (Cell c in door.Occupied.Cells())
            {
                Cell n = c + new Cell(0, 1), s = c + new Cell(0, -1);
                Cell e = c + new Cell(1, 0), w = c + new Cell(-1, 0);
                bool northSouthWall = InLine(built, n) && InLine(built, s)
                    && IsOpen(built, e) && IsOpen(built, w);
                bool eastWestWall = InLine(built, e) && InLine(built, w)
                    && IsOpen(built, n) && IsOpen(built, s);
                Assert.True(northSouthWall || eastWestWall, $"{door.Def} at {c}");
            }
        }
    }

    /// <summary>
    /// Benches and devices with an interaction cell have it clear of
    /// buildings and items, inside the same room as the thing.
    /// </summary>
    [Fact(DisplayName = "Interaction cells are clear and in the same room")]
    public void Interaction_cells_should_be_clear()
    {
        Dictionary<Cell, Placement> built = BuildingCells(Plan);
        var items = new HashSet<Cell>(Plan.Placements
            .Where(p => p.Kind is PlacementKind.Item or PlacementKind.Crate).Select(p => p.At));
        var users = Plan.Placements.Where(p => p.InteractionCell != null).ToList();
        Assert.Equal(3, users.Count); // stove, Transmogrifier, Artificer
        foreach (Placement p in users)
        {
            Cell ic = p.InteractionCell!.Value;
            Assert.False(built.ContainsKey(ic), $"{p.Def}'s interaction cell {ic} is built on");
            Assert.False(items.Contains(ic), $"{p.Def}'s interaction cell {ic} holds items");
            BuildingPlan b = Plan.Buildings.Single(x => x.Key == p.BuildingKey);
            Assert.Contains(p.At, RoomOf(b, ic, built));
        }
    }

    /// <summary>
    /// A placement reports an interaction cell only when it is a building
    /// whose catalogued def has one, turned with the placement.
    /// </summary>
    [Fact(DisplayName = "Placements know their interaction cells")]
    public void Placement_should_know_its_interaction_cell()
    {
        var stove = new Placement(PlacementKind.Building, "ElectricStove", new Cell(3, 3))
        {
            SizeX = 3, Rot = Facing.South,
        };
        Assert.Equal(new Cell(3, 4), stove.InteractionCell);
        Assert.Equal(new Cell(3, 2), (stove with { Rot = Facing.North }).InteractionCell);
        Assert.Null((stove with { Kind = PlacementKind.Crate }).InteractionCell);
        Assert.Null(new Placement(PlacementKind.Building, "Wall", new Cell(0, 0)).InteractionCell);
        Assert.Null(new Placement(PlacementKind.Building, "NoSuchDef", new Cell(0, 0)).InteractionCell);
    }

    /// <summary>
    /// From its street cell, every walkable cell of every room is reachable
    /// with doors and gates passable and walls, fences and impassable
    /// furniture blocking; so are the insides of the fenced yards, pens
    /// and cemetery, and the News Nob's stools.
    /// </summary>
    [Fact(DisplayName = "Every room is reachable from the street")]
    public void Rooms_should_be_reachable_from_the_street()
    {
        Dictionary<Cell, Placement> built = BuildingCells(Plan);
        foreach (BuildingPlan b in Plan.Buildings)
        {
            Cell street = b.StreetCell ?? throw new InvalidOperationException($"{b.Key} has no street cell");
            HashSet<Cell> reached = Reachable(Plan, street, built);
            IEnumerable<Cell> inside = b.Enclosed
                ? b.Rooms.SelectMany(r => r.Cells())
                : b.Outer.Width > 2 && b.Outer.Height > 2 ? b.Outer.ExpandedBy(-1).Cells() : b.Outer.Cells();
            int walkable = 0;
            foreach (Cell c in inside.Where(c => !built.TryGetValue(c, out Placement? p) || !Blocks(p)))
            {
                walkable++;
                Assert.True(reached.Contains(c), $"{b.Key}: {c} unreachable from {street}");
            }

            Assert.True(walkable > 0, $"{b.Key} has nowhere to stand");
        }
    }

    /// <summary>The street cells named by the buildings are street or sidewalk.</summary>
    [Fact(DisplayName = "Street cells are street")]
    public void StreetCells_should_be_street()
    {
        Dictionary<Cell, TerrainPlacement> ground = TerrainCells(Plan);
        var surfaces = new[] { "Concrete", "PavedTile" };
        foreach (BuildingPlan b in Plan.Buildings)
        {
            Assert.True(ground.TryGetValue(b.StreetCell!.Value, out TerrainPlacement? t)
                && surfaces.Contains(t.Def) && !t.Taper, $"{b.Key}: {b.StreetCell}");
        }
    }

    // ===== Layout =====

    /// <summary>The lots' outer footprints are those of docs/SHOWCASE.md.</summary>
    /// <param name="key">The lot.</param>
    /// <param name="minX">Westmost column.</param>
    /// <param name="minZ">Southmost row.</param>
    /// <param name="maxX">Eastmost column.</param>
    /// <param name="maxZ">Northmost row.</param>
    [Theory(DisplayName = "Lots have the documented footprints")]
    [InlineData("W1", -15, -15, -7, -7)]
    [InlineData("E1", 7, -15, 17, -7)]
    [InlineData("W2", -15, 7, -7, 15)]
    [InlineData("E2", 7, 7, 17, 15)]
    [InlineData("W3", -15, 21, -7, 29)]
    [InlineData("E3", 7, 21, 15, 29)]
    [InlineData("W4", -17, 35, -7, 43)]
    [InlineData("E4", 7, 35, 15, 43)]
    [InlineData("Substation", -37, -5, -27, 5)]
    [InlineData("BallistaYard", 19, -16, 27, -6)]
    [InlineData("KnottShelter", -23, 23, -19, 27)]
    [InlineData("Cemetery", -6, 56, 6, 66)]
    public void Lots_should_match_the_design(string key, int minX, int minZ, int maxX, int maxZ)
    {
        BuildingPlan b = Plan.Buildings.Single(x => x.Key == key);
        Assert.Equal(new Box(minX, minZ, maxX, maxZ), b.Outer);
        if (b.Side != null)
        {
            Assert.Equal(9, b.Outer.Height);
            Assert.Equal(b.Side == "W" ? -7 : 7, b.Side == "W" ? b.Outer.MaxX : b.Outer.MinX);
            Assert.Equal(b.Side == "W" ? -7 : 7, b.FrontDoor!.Value.X);
        }
    }

    /// <summary>
    /// Doorstep Lane runs north of both slot-1 buildings and south of both
    /// slot-2 buildings, and each side has 3 to 5 lots in slots 1, 2, ....
    /// </summary>
    [Fact(DisplayName = "The cross street splits the first pair from the rest")]
    public void CrossStreet_should_run_between_slots_one_and_two()
    {
        var lane = Plan.Terrain.Where(t => t.At.X == -10 && t.Def == "Concrete").Select(t => t.At.Z).ToList();
        Assert.Equal(new[] { -2, -1, 0, 1, 2 }, lane.OrderBy(z => z));
        foreach (string side in new[] { "W", "E" })
        {
            var row = Plan.Buildings.Where(b => b.Side == side).OrderBy(b => b.Slot).ToList();
            Assert.InRange(row.Count, 3, 5);
            Assert.Equal(Enumerable.Range(1, row.Count), row.Select(b => b.Slot));
            Assert.True(row[0].Outer.MaxZ < lane.Min(), $"{row[0].Key} reaches the lane");
            Assert.True(row[1].Outer.MinZ > lane.Max(), $"{row[1].Key} reaches the lane");
            for (int i = 1; i < row.Count; i++)
            {
                int gap = row[i].Outer.MinZ - row[i - 1].Outer.MaxZ - 1;
                Assert.True(i == 1 || gap is >= 3 and <= 5, $"{side} side yard of {gap}");
            }
        }
    }

    /// <summary>
    /// The streets: concrete painted dark grey with a pastel yellow centre
    /// line two cells on, two off, stopping short of the roundel; paved
    /// sidewalks on x and z = +/-3; and the roundel's three rings laid
    /// only over street and sidewalk cells.
    /// </summary>
    [Fact(DisplayName = "Streets, centre lines, sidewalks and the roundel")]
    public void Streets_should_follow_the_design()
    {
        Dictionary<Cell, TerrainPlacement> ground = TerrainCells(Plan);
        var street = new HashSet<Cell>(new Box(-2, -21, 2, 47).Cells().Concat(new Box(-20, -2, 20, 2).Cells()));
        var sidewalk = new HashSet<Cell>(Enumerable.Range(-21, 69).Where(z => Math.Abs(z) > 2)
            .SelectMany(z => new[] { new Cell(-3, z), new Cell(3, z) })
            .Concat(Enumerable.Range(-20, 41).Where(x => Math.Abs(x) > 2)
                .SelectMany(x => new[] { new Cell(x, -3), new Cell(x, 3) })));
        foreach (Cell c in street.Concat(sidewalk))
        {
            TerrainPlacement t = ground[c];
            double d = c.DistanceTo(new Cell(0, 0));
            if (d <= 4.5)
            {
                string ring = d <= 1.5 ? "GoldTile" : d <= 3 ? "FlagstoneMarble" : "FlagstoneSlate";
                Assert.Equal((ring, (string?)null), (t.Def, t.Paint));
            }
            else if (sidewalk.Contains(c))
            {
                Assert.Equal(("PavedTile", (string?)null), (t.Def, t.Paint));
            }
            else
            {
                bool line = (c.X == 0 && Dash(c.Z)) || (c.Z == 0 && Math.Abs(c.X) > 2 && Dash(c.X));
                Assert.Equal(("Concrete", line ? "Structure_YellowPastel" : "Structure_GreyDark"),
                    (t.Def, t.Paint));
            }
        }

        var roundel = new[] { "GoldTile", "FlagstoneMarble", "FlagstoneSlate" };
        Assert.All(Plan.Terrain.Where(t => roundel.Contains(t.Def)),
            t => Assert.True(street.Contains(t.At) || sidewalk.Contains(t.At), $"roundel at {t.At}"));
        Assert.Equal(Plan.Terrain.Count, Plan.Terrain.Select(t => t.At).Distinct().Count());
    }

    /// <summary>
    /// Each lot's front yard: a lawn of grass on soil, a picket fence on
    /// x = +/-4 with a gate at the walk, and a paved walk to the door.
    /// </summary>
    [Fact(DisplayName = "Front yards have lawns, picket fences, gates and walks")]
    public void FrontYards_should_follow_the_design()
    {
        Dictionary<Cell, Placement> built = BuildingCells(Plan);
        Dictionary<Cell, TerrainPlacement> ground = TerrainCells(Plan);
        var grass = new HashSet<Cell>(Plan.Placements.Where(p => p.Def == "Plant_Grass").Select(p => p.At));
        foreach (BuildingPlan lot in Plan.Buildings.Where(b => b.Side != null))
        {
            int s = lot.Side == "E" ? 1 : -1;
            Cell door = lot.FrontDoor!.Value;
            int gates = 0;
            for (int z = lot.Outer.MinZ; z <= lot.Outer.MaxZ; z++)
            {
                Placement fence = built[new Cell(4 * s, z)];
                Assert.Equal("WoodLog", fence.Stuff);
                Assert.Contains(fence.Def, new[] { "Fence", "FenceGate" });
                if (fence.Def == "FenceGate")
                {
                    gates++;
                    Assert.Equal(door.Z, z);
                }
            }

            Assert.Equal(1, gates);
            foreach (int x in new[] { 4, 5, 6 })
            {
                Assert.Equal("PavedTile", ground[new Cell(x * s, door.Z)].Def);
            }

            int lawn = new Box(5, lot.Outer.MinZ, 6, lot.Outer.MaxZ).Cells()
                .Select(c => new Cell(c.X * s, c.Z))
                .Count(c => grass.Contains(c) && ground[c].Def == "Soil");
            Assert.True(lawn >= 8, $"{lot.Key} lawn of {lawn}");
        }
    }

    /// <summary>
    /// Plan A in the three model homes: the same plan, mirrored for the
    /// east side, so they compare directly as test pieces.
    /// </summary>
    [Fact(DisplayName = "The model homes share Plan A, mirrored for the east")]
    public void ModelHomes_should_share_plan_A()
    {
        List<string> knott = PlanALayout("W3", 21, false);
        Assert.Equal(knott, PlanALayout("E3", 21, true));
        List<string> now = PlanALayout("W2", 7, false);
        Assert.Equal(knott.Count + 1, now.Count); // two single beds for one double
        Assert.Equal(
            knott.Where(s => !s.StartsWith("DoubleBed") && !s.StartsWith("Armchair")),
            now.Where(s => !s.StartsWith("Bed") && !s.StartsWith("Armchair")));
    }

    // ===== Bounds, tapers and key points =====

    /// <summary>
    /// Everything lies inside the bounds; everything but the tapers lies
    /// inside the core; the core lies inside the bounds; and the parts
    /// cover exactly the cells the town touches.
    /// </summary>
    [Fact(DisplayName = "Everything lies inside the bounds and core")]
    public void Everything_should_lie_inside_bounds()
    {
        Assert.True(Plan.Bounds.Contains(Plan.Core));
        Assert.Equal(4, Plan.FirebreakWidth);
        Assert.All(Plan.Terrain, t => Assert.True(
            (t.Taper ? Plan.Bounds : Plan.Core).Contains(t.At), $"{t.Def} at {t.At}"));
        Assert.All(Plan.Placements, p => Assert.True(Plan.Core.Contains(p.Occupied), $"{p.Def} at {p.At}"));
        Assert.All(Plan.Buildings, b => Assert.True(Plan.Core.Contains(b.Outer), b.Key));
        Assert.All(Plan.Labels, l => Assert.True(Plan.Core.Contains(l.At), l.Kind.ToString()));
        Assert.All(Plan.Roofs, r => Assert.True(Plan.Core.Contains(r.At)));

        var touched = new HashSet<Cell>(Plan.Terrain.Select(t => t.At)
            .Concat(Plan.Placements.SelectMany(p => p.Occupied.Cells()))
            .Concat(Plan.Buildings.SelectMany(b => b.Outer.Cells()))
            .Concat(Plan.Labels.Select(l => l.At)));
        var parts = Plan.Parts.SelectMany(b => b.Cells()).ToList();
        Assert.Equal(parts.Count, parts.Distinct().Count());
        Assert.True(touched.SetEquals(parts), "parts must cover exactly the touched cells");
        Assert.All(Plan.Parts, b => Assert.True(Plan.Bounds.Contains(b)));
    }

    /// <summary>
    /// Each taper narrows as it leaves town (near surface, then middle,
    /// then a one-wide far track) and ends: nothing lies past its design
    /// end, and the north and west ones finish at their gates.
    /// </summary>
    [Fact(DisplayName = "Tapers narrow and end")]
    public void Tapers_should_narrow_and_end()
    {
        TownPalette palette = TownPalette.For(TownQuality.Medium);
        var arms = new (string Name, Func<Cell, bool> In, Func<Cell, int> Out, Func<Cell, int> Across, int End)[]
        {
            ("south", c => c.Z < -21, c => -c.Z, c => c.X, 36),
            ("north", c => c.Z > 47, c => c.Z, c => c.X, 55),
            ("west", c => c.X < -20, c => -c.X, c => c.Z, 26),
            ("east", c => c.X > 20, c => c.X, c => c.Z, 40),
        };
        var tapers = Plan.Terrain.Where(t => t.Taper).ToList();
        Assert.All(tapers, t => Assert.Single(arms, a => a.In(t.At)));
        string[] order = { palette.TaperNear, palette.TaperMid, palette.TaperFar };
        foreach (var arm in arms)
        {
            var rows = tapers.Where(t => arm.In(t.At)).GroupBy(t => arm.Out(t.At)).OrderBy(g => g.Key).ToList();
            Assert.InRange(rows[^1].Key, arm.End - 1, arm.End);
            int lastWidth = int.MaxValue;
            int lastSurface = 0;
            foreach (var row in rows)
            {
                int width = row.Count();
                Assert.True(width <= lastWidth, $"{arm.Name} widens at {row.Key}");
                Assert.True(width <= 5);
                Assert.Equal(width - 1, row.Max(t => arm.Across(t.At)) - row.Min(t => arm.Across(t.At)));
                int surface = Array.IndexOf(order, row.First().Def);
                Assert.True(row.All(t => t.Def == row.First().Def) && surface >= lastSurface,
                    $"{arm.Name} surfaces out of order at {row.Key}");
                Assert.True(surface < 2 || width == 1, $"{arm.Name} far track is {width} wide");
                Assert.All(row, t => Assert.Null(t.Paint));
                lastWidth = width;
                lastSurface = surface;
            }
        }

        Assert.Contains(tapers, t => t.At == new Cell(0, 55));
        Assert.Contains(tapers, t => t.At.X == -26 && t.At.Z == 0);
        Assert.Equal(new Box(-37, -36, 52, 66), Plan.Bounds);
    }

    /// <summary>Ground Zero, the reactor and the welcome sign are where the design puts them.</summary>
    [Fact(DisplayName = "Key points match the design")]
    public void KeyPoints_should_match_the_design()
    {
        Assert.Equal("AnyRim", Plan.Name);
        Assert.Equal(TownQuality.Medium, Plan.Quality);
        Assert.Equal(new Cell(0, 0), Plan.GroundZero);
        Assert.Equal(new Cell(-32, 0), Plan.Reactor);
        Assert.Single(Plan.Placements, p => p.Def == "BT_BatshittiumReactor" && p.At == Plan.Reactor);
        Assert.Equal(new Cell(5, -24), Plan.WelcomeSign);
        Assert.Single(Plan.Labels, l => l.Kind == LabelKind.Welcome && l.At == Plan.WelcomeSign);
        Assert.Contains(Plan.Placements, p => p.Def == "Wall" && p.At == Plan.WelcomeSign
            && p.Paint == "Structure_White");
    }

    /// <summary>Each label appears once, where docs/SHOWCASE.md puts it.</summary>
    [Fact(DisplayName = "Every label kind appears once, in place")]
    public void Labels_should_be_placed()
    {
        Assert.Equal(Enum.GetValues(typeof(LabelKind)).Length, Plan.Labels.Count);
        Assert.Equal(Plan.Labels.Count, Plan.Labels.Select(l => l.Kind).Distinct().Count());
        Assert.Equal(new Cell(31, -4), LabelAt(LabelKind.Exit));
        Assert.Equal(new Cell(0, 56), LabelAt(LabelKind.Cemetery));
        Assert.Equal(new Cell(-27, 0), LabelAt(LabelKind.Substation));
        Assert.Equal(new Cell(0, 0), LabelAt(LabelKind.GroundZero));
        Assert.Equal(Plan.Buildings.Single(b => b.Key == "E1").FrontDoor, LabelAt(LabelKind.Incident));
        Cell evac = LabelAt(LabelKind.Evacuation);
        Assert.True(Math.Abs(evac.X) <= 2 && evac.Z is > 4 and < 10, $"evacuation sign at {evac}");
    }

    /// <summary>
    /// The cars stand at measured distances from Ground Zero, about 12, 12,
    /// 21 and 33 tiles to their centres, as Operation Doorstep's did.
    /// </summary>
    [Fact(DisplayName = "Cars stand at measured distances")]
    public void Cars_should_stand_at_measured_distances()
    {
        var cars = new[] { "AncientRustedCar", "AncientRustedTruck", "AncientRustedCarFrame" };
        double[] distances = Plan.Placements.Where(p => cars.Contains(p.Def))
            .Select(p => Math.Sqrt(Math.Pow((p.Occupied.MinX + p.Occupied.MaxX) / 2.0, 2)
                + Math.Pow((p.Occupied.MinZ + p.Occupied.MaxZ) / 2.0, 2)))
            .OrderBy(d => d).ToArray();
        Assert.Equal(4, distances.Length);
        double[] expected = { 12, 12, 21, 33 };
        for (int i = 0; i < 4; i++)
        {
            Assert.InRange(distances[i], expected[i] - 1, expected[i] + 1);
        }
    }

    // ===== Heat, cook-off and power =====

    /// <summary>
    /// No roofed cell lies within 13 tiles of the reactor, so its heat ring
    /// holds no room, and its own yard is unroofed.
    /// </summary>
    [Fact(DisplayName = "Nothing roofed within 13 tiles of the reactor")]
    public void Reactor_heat_ring_should_hold_no_roof()
    {
        Box reactor = Plan.Placements.Single(p => p.Def == "BT_BatshittiumReactor").Occupied;
        Assert.All(Plan.Roofs, r => Assert.True(reactor.DistanceTo(r.At) > 13, $"roof at {r.At}"));
        Box yard = Plan.Buildings.Single(b => b.Key == "Substation").Outer;
        Assert.DoesNotContain(Plan.Roofs, r => yard.Contains(r.At));
        Assert.False(Plan.Buildings.Single(b => b.Key == "Substation").Enclosed);
    }

    /// <summary>
    /// The Ballista and its magazines stand more than 30 tiles from the
    /// reactor, beyond a magazine's 25-30 tile cook-off throw, and the
    /// magazines chain to the Ballista by shared edges.
    /// </summary>
    [Fact(DisplayName = "The Ballista yard is out of the reactor's reach and chained")]
    public void Ballista_should_be_far_from_reactor_and_chained()
    {
        Box reactor = Plan.Placements.Single(p => p.Def == "BT_BatshittiumReactor").Occupied;
        Placement ballista = Plan.Placements.Single(p => p.Def == "BT_BatshittiumBallista");
        var magazines = Plan.Placements.Where(p => p.Def == "BT_BallistaMagazine").ToList();
        Assert.Equal(2, magazines.Count);
        foreach (Placement p in magazines.Append(ballista))
        {
            Assert.True(reactor.DistanceTo(p.Occupied) > 30, $"{p.Def} at {p.At}");
            Assert.Equal(Owner.Player, p.Owner);
        }

        var connected = MagazineNetwork.Connected(
            ballista.Occupied.Cells().Select(c => (c.X, c.Z)),
            new HashSet<(int x, int z)>(magazines.Select(m => (m.At.X, m.At.Z))));
        Assert.Equal(2, connected.Count);
        Assert.True(ballista.SwitchedOff);
        Assert.Equal(new[] { ("BT_BoltPainful", 16) }, ballista.Load);
        Assert.Equal(
            new[] { ("BT_BoltPainful", 200), ("BT_BoltReallyPainful", 100) },
            magazines.SelectMany(m => m.Load).OrderBy(l => l.Def));
    }

    /// <summary>
    /// Every powered thing has a transmitter within RimWorld's 6-cell
    /// connection box of its position, and every conduit is on the
    /// reactor's net (joined by shared edges).
    /// </summary>
    [Fact(DisplayName = "Power reaches everything from one net")]
    public void Power_should_reach_everything()
    {
        var transmitters = Plan.Placements.Where(p => Info(p).TransmitsPower).ToList();
        var cells = new HashSet<Cell>(transmitters.SelectMany(t => t.Occupied.Cells()));
        foreach (Placement p in Plan.Placements.Where(p => p.Kind == PlacementKind.Building && Info(p).Powered))
        {
            Assert.True(cells.Any(c => Math.Abs(c.X - p.At.X) <= 6 && Math.Abs(c.Z - p.At.Z) <= 6),
                $"{p.Def} at {p.At} has no conduit within 6");
        }

        Placement reactor = Plan.Placements.Single(p => p.Def == "BT_BatshittiumReactor");
        var net = new HashSet<Cell>(reactor.Occupied.Cells());
        var frontier = new Queue<Cell>(net);
        while (frontier.Count > 0)
        {
            Cell c = frontier.Dequeue();
            foreach (Cell n in Neighbours(c))
            {
                if (cells.Contains(n) && net.Add(n))
                {
                    frontier.Enqueue(n);
                }
            }
        }

        Assert.True(cells.SetEquals(net), $"{cells.Count - net.Count} transmitter cells off the net");
        Assert.DoesNotContain(Plan.Placements, p => p.Def == "Battery");
    }

    // ===== Residents =====

    /// <summary>The cast of docs/SHOWCASE.md, with homes and hats.</summary>
    /// <param name="key">The resident.</param>
    /// <param name="nick">Nickname.</param>
    /// <param name="last">Last name.</param>
    /// <param name="female">Sex.</param>
    /// <param name="age">Age.</param>
    /// <param name="home">Home key.</param>
    /// <param name="apparel">Apparel, comma-separated.</param>
    [Theory(DisplayName = "Residents match the cast list")]
    [InlineData("Hugh", "Lame Duck", "Moribund", false, 63, "E2", "Apparel_BowlerHat,Apparel_CollarShirt")]
    [InlineData("Wendy", "Any Minute", "Now", true, 34, "W2", "")]
    [InlineData("Ernest", "Next", "Inline", false, 41, "W2", "Apparel_CowboyHat")]
    [InlineData("Shirley", "Probably Fine", "Knott", true, 52, "W3", "Apparel_PsychicFoilHelmet")]
    [InlineData("Constance", "Collateral", "Damage", true, 46, "E3", "")]
    [InlineData("Barry", "Six Feet", "Graves", false, 58, "W4", "Apparel_Duster,Apparel_BowlerHat")]
    [InlineData("Bea", "Right Back", "Shortly", true, 29, "W1", "")]
    [InlineData("Morty", "Two Weeks", "Tallis", false, 64, "E1", "Apparel_SimpleHelmet")]
    [InlineData("Ned", "Overkill", "Lessly", false, 44, "E4", "Apparel_FlakVest")]
    public void Residents_should_match_the_cast(
        string key, string nick, string last, bool female, int age, string home, string apparel)
    {
        ResidentPlan r = Plan.Residents.Single(x => x.Key == key);
        Assert.Equal((key, nick, last, female, age, home), (r.First, r.Nick, r.Last, r.Female, r.Age, r.HomeKey));
        Assert.Equal(apparel.Split(',', StringSplitOptions.RemoveEmptyEntries), r.Apparel);
    }

    /// <summary>
    /// Only Ned, the Savings and Loan's guard, is armed, with the
    /// Ergonomic Singularity Grenade "for emergencies"; the def is the
    /// mod's own and is catalogued.
    /// </summary>
    [Fact(DisplayName = "Only the bank's guard is armed, with the grenade")]
    public void Only_the_guard_should_be_armed()
    {
        ResidentPlan armed = Assert.Single(Plan.Residents, r => r.Weapon != null);
        Assert.Equal(("Ned", "E4", "BT_ErgonomicSingularityGrenade"),
            (armed.Key, armed.HomeKey, armed.Weapon));
        Assert.True(TownDefs.All.ContainsKey(armed.Weapon!), armed.Weapon);
        Assert.Equal(RaccoonIncident.GuardKey, armed.Key);
        Assert.Equal(RaccoonIncident.GrenadeDef, armed.Weapon);
    }

    /// <summary>
    /// Nine residents with unique keys and short nicknames, each with a
    /// home that exists, a duty spot just inside its front door, a bed
    /// reserved in it and a reserved grave.
    /// </summary>
    [Fact(DisplayName = "Residents have homes, duty spots, beds and graves")]
    public void Residents_should_have_homes_beds_and_graves()
    {
        Assert.Equal(9, Plan.Residents.Count);
        Assert.Equal(9, Plan.Residents.Select(r => r.Key).Distinct().Count());
        Dictionary<Cell, Placement> built = BuildingCells(Plan);
        foreach (ResidentPlan r in Plan.Residents)
        {
            Assert.InRange(r.Nick.Length, 1, 16);
            Assert.False(string.IsNullOrWhiteSpace(r.First) || string.IsNullOrWhiteSpace(r.Last));
            BuildingPlan home = Plan.Buildings.Single(b => b.Key == r.HomeKey);
            Assert.True(home.Enclosed && home.Rooms.Any(room => room.Contains(r.DutySpot)), r.Key);
            Assert.False(built.ContainsKey(r.DutySpot), $"{r.Key}'s duty spot is built on");
            Assert.Equal(1, Math.Abs(r.DutySpot.X - home.FrontDoor!.Value.X)
                + Math.Abs(r.DutySpot.Z - home.FrontDoor.Value.Z));
            Placement bed = Plan.Placements.Single(p => p.ReservedFor == r.Key && p.Def is "Bed" or "DoubleBed");
            Assert.Equal(home.Key, bed.BuildingKey);
            Placement grave = Plan.Placements.Single(p => p.ReservedFor == r.Key && p.Def == "Grave");
            Assert.Equal("Cemetery", grave.BuildingKey);
        }

        var graves = Plan.Placements.Where(p => p.Def == "Grave").ToList();
        Assert.Equal(10, graves.Count);
        Assert.All(graves, g => Assert.Equal(2, g.Occupied.Area));
        Assert.Equal(1, graves.Count(g => g.ReservedFor == null));
        Assert.Empty(Plan.Placements.Where(p => p.ReservedFor != null
            && !Plan.Residents.Any(r => r.Key == p.ReservedFor)));
    }

    // ===== Defs, ownership and order =====

    /// <summary>
    /// Every def used is catalogued, made of stuff it accepts (or none),
    /// at a quality only if it has one, with switches, fuel and bills only
    /// where it has them, facing north if it cannot turn, and crated only
    /// if it can be minified.
    /// </summary>
    [Fact(DisplayName = "Every def is catalogued and used as it allows")]
    public void Defs_should_be_catalogued_and_used_correctly()
    {
        foreach (Placement p in Plan.Placements)
        {
            Assert.True(TownDefs.All.TryGetValue(p.Def, out DefInfo? info), p.Def);
            string where = $"{p.Def} at {p.At}";
            bool layerFits = p.Kind switch
            {
                PlacementKind.Item => info!.Layer == ThingLayer.Item,
                PlacementKind.Crate => info!.Layer == ThingLayer.Building,
                PlacementKind.Plant => info!.Layer == ThingLayer.Plant,
                PlacementKind.Animal => info!.Layer == ThingLayer.Pawn,
                _ => info!.Layer is ThingLayer.Building or ThingLayer.Conduit,
            };
            Assert.True(layerFits, where);
            Assert.Equal((info.SizeX, info.SizeZ), (p.SizeX, p.SizeZ));
            if (p.Stuff == null)
            {
                Assert.True(info.AcceptsStuff.Count == 0 || p.Kind != PlacementKind.Building, where);
            }
            else
            {
                Assert.True(TownDefs.All.TryGetValue(p.Stuff, out DefInfo? stuff), p.Stuff);
                Assert.True(info.AcceptsStuff.Intersect(stuff!.StuffCategories).Any(), $"{where} of {p.Stuff}");
            }

            Assert.Equal(info.HasQuality && p.Kind == PlacementKind.Building ? "Normal" : null, p.Quality);
            Assert.True(!p.SwitchedOff || info.Flickable, where);
            Assert.True(!(p.Refuel || p.AutoRefuelOff) || info.Refuelable, where);
            Assert.True(!p.SuspendBills || info.HasBills, where);
            Assert.True(info.Rotatable || p.Rot == Facing.North, where);
            Assert.True(p.Kind != PlacementKind.Crate || info.Minifiable, where);
            Assert.True(p.Load.Count == 0 || info.LoadCapacity > 0, where);
        }

        Assert.All(Plan.Residents.SelectMany(r => r.Apparel),
            a => Assert.True(TownDefs.All.ContainsKey(a), a));
        Assert.All(Plan.Stockpiles.SelectMany(s => s.Accepts),
            a => Assert.True(TownDefs.All.ContainsKey(a), a));
    }

    /// <summary>
    /// The player owns the Batshit devices, so their buttons work, and
    /// they are left in their safe states; the town owns the rest; items,
    /// plants and animals belong to nobody; loose items and the crate are
    /// forbidden, buildings are not (plants and animals keep the default).
    /// </summary>
    [Fact(DisplayName = "Ownership, safe states and forbidding")]
    public void Ownership_should_follow_the_design()
    {
        var devices = new[]
        {
            "BT_BatshittiumReactor", "BT_BatshittiumTransmogrifier", "BT_BatshittiumArtificer",
            "BT_BrainSqueezer", "BT_BatshittiumBallista", "BT_BallistaMagazine",
        };
        foreach (Placement p in Plan.Placements)
        {
            Owner expected = devices.Contains(p.Def) || p.Kind == PlacementKind.Crate ? Owner.Player
                : p.Kind == PlacementKind.Building ? Owner.Town
                : Owner.None;
            Assert.True(expected == p.Owner, $"{p.Def} at {p.At} owned by {p.Owner}");
            Assert.True(p.Forbidden == (p.Kind != PlacementKind.Building),
                $"{p.Def} at {p.At} forbidden {p.Forbidden}");
        }

        Assert.True(Only("BT_BatshittiumReactor") is { Refuel: true, AutoRefuelOff: true });
        Assert.True(Only("BT_BatshittiumArtificer")
            is { Refuel: true, AutoRefuelOff: true, SwitchedOff: true });
        Assert.True(Only("BT_BatshittiumTransmogrifier").SuspendBills);
        Assert.True(Only("BT_BrainSqueezer").SwitchedOff);
        Assert.Equal(PlacementKind.Crate, Only("BT_RandomReplicator").Kind);
        Assert.All(Plan.Placements.Where(p => p.Def == "TubeTelevision"), p => Assert.True(p.SwitchedOff));
        var heaters = Plan.Placements.Where(p => p.Def == "Heater").ToList();
        Assert.All(heaters, h => Assert.True(h.OnlyIfCold));
        Assert.Equal(
            Plan.Residents.Select(r => r.HomeKey).Distinct().OrderBy(k => k),
            heaters.Select(h => h.BuildingKey!).Distinct().OrderBy(k => k));
        Assert.Equal(
            new[] { ("Boomalope", "Daisy"), ("Pig", "Priscilla") },
            Plan.Placements.Where(p => p.Kind == PlacementKind.Animal)
                .Select(p => (p.Def, p.Name!)).OrderBy(a => a.Def));
    }

    /// <summary>
    /// Placements come in build order: walls and fences, then doors and
    /// gates, then other buildings, furniture and conduits, then items,
    /// plants and animals.
    /// </summary>
    [Fact(DisplayName = "Placements come in build order")]
    public void Placements_should_come_in_build_order()
    {
        int[] ranks = Plan.Placements.Select(Rank).ToArray();
        for (int i = 1; i < ranks.Length; i++)
        {
            Assert.True(ranks[i] >= ranks[i - 1], $"{Plan.Placements[i].Def} after {Plan.Placements[i - 1].Def}");
        }
    }

    /// <summary>
    /// AnyRim zones no stockpile. The town Artificer ships switched off, so
    /// a zone for its output would hold nothing of its own and only pull
    /// the colony's bolts and grenades into town.
    /// </summary>
    [Fact(DisplayName = "AnyRim zones no stockpile")]
    public void AnyRim_should_zone_no_stockpile()
    {
        Assert.Empty(Plan.Stockpiles);
        Assert.True(Only("BT_BatshittiumArtificer").SwitchedOff);
    }

    // ===== Cleared ground =====

    /// <summary>
    /// The cleared ground hugs the town: every cell lies in the levelled
    /// area within the firebreak of a part, every cell of the town inside
    /// the levelled area is cleared, and the empty land between the town's
    /// arms (where a colony may stand) is not.
    /// </summary>
    [Fact(DisplayName = "Cleared ground is the parts grown by the firebreak")]
    public void ClearedCells_should_follow_the_parts()
    {
        IReadOnlyList<Cell> cleared = Plan.ClearedCells();
        var set = new HashSet<Cell>(cleared);
        Box levelled = Plan.LevelledArea;
        int w = Plan.FirebreakWidth;
        Assert.Equal(Plan.Core.ExpandedBy(w), levelled);
        Assert.Equal(cleared.Count, set.Count);
        Assert.All(cleared, c => Assert.True(levelled.Contains(c), $"{c}"));
        Assert.All(cleared, c => Assert.Contains(Plan.Parts, p => p.ExpandedBy(w).Contains(c)));
        foreach (Cell c in Plan.Parts.SelectMany(p => p.Cells()).Where(levelled.Contains))
        {
            Assert.Contains(c, set);
        }

        foreach (Cell c in levelled.Cells())
        {
            bool near = Plan.Parts.Any(p => p.ExpandedBy(w).Contains(c));
            Assert.True(near == set.Contains(c), $"{c}");
        }

        // The north-east, between the cemetery road and the News Nob.
        Assert.DoesNotContain(new Cell(40, 55), set);
        Assert.True(cleared.Count < levelled.Area, $"{cleared.Count} of {levelled.Area}");
        Assert.Equal(
            cleared.OrderBy(c => c.Z).ThenBy(c => c.X), cleared);
    }

    /// <summary>
    /// Ground the reduced plan no longer needs is not cleared: a building
    /// dropped by <see cref="TownPlan.Avoiding"/> takes the cells only it
    /// needed with it, and nothing new is cleared.
    /// </summary>
    [Fact(DisplayName = "A reduced plan clears no more than the full one")]
    public void ClearedCells_should_shrink_with_the_plan()
    {
        var full = new HashSet<Cell>(Plan.ClearedCells());
        TownPlan reduced = Plan.Avoiding(new[] { new Box(-40, -1, -38, 1) });
        var less = new HashSet<Cell>(reduced.ClearedCells());
        Assert.True(less.IsSubsetOf(full));
        Assert.True(less.Count < full.Count, $"{less.Count} of {full.Count}");
        Assert.DoesNotContain(new Cell(-37, 0), less);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TownPlan { FirebreakWidth = -1 }.ClearedCells());
        Assert.Empty(new TownPlan().ClearedCells());
    }

    // ===== Palettes =====

    /// <summary>
    /// Every quality tier plans a sound town in its own surfaces: nothing
    /// overlaps, every def is catalogued and furniture has the tier's
    /// quality.
    /// </summary>
    /// <param name="quality">The tier.</param>
    [Theory(DisplayName = "Every tier plans a sound town")]
    [InlineData(TownQuality.Slum)]
    [InlineData(TownQuality.Low)]
    [InlineData(TownQuality.Medium)]
    [InlineData(TownQuality.High)]
    [InlineData(TownQuality.Resort)]
    [InlineData(TownQuality.Epic)]
    public void Every_tier_should_plan_a_sound_town(TownQuality quality)
    {
        TownPalette palette = TownPalette.For(quality);
        TownPlan plan = TownPlan.AnyRim(9, palette);
        Assert.Equal(quality, plan.Quality);
        Assert.Equal(palette.Carriageway, TerrainCells(plan)[new Cell(0, 20)].Def);
        Assert.Equal(palette.Sidewalk, TerrainCells(plan)[new Cell(3, 20)].Def);
        Assert.Equal(palette.CentreLinePaint ?? palette.CarriagewayPaint, TerrainCells(plan)[new Cell(0, 5)].Paint);
        Assert.All(plan.Placements, p => Assert.True(TownDefs.All.ContainsKey(p.Def), p.Def));
        Assert.All(plan.Placements.Where(p => p.Quality != null),
            p => Assert.Equal(palette.FurnitureQuality, p.Quality));
        Assert.All(plan.Placements.Where(p => p.Def == "Fence" && p.BuildingKey == null),
            p => Assert.Equal(palette.FenceStuff, p.Stuff));
        int cells = plan.Placements.Where(IsBuildingLayer).Sum(p => p.Occupied.Area);
        Assert.Equal(cells, BuildingCells(plan).Count);
    }

    // ===== Avoiding =====

    /// <summary>With no obstacles, nothing is dropped.</summary>
    [Fact(DisplayName = "Avoiding nothing keeps everything")]
    public void Avoiding_nothing_should_keep_everything()
    {
        TownPlan same = Plan.Avoiding(Array.Empty<Box>());
        Assert.Equal(Describe(Plan, tapers: true), Describe(same, tapers: true));
        Assert.Equal(Plan.Parts, same.Parts);
        Assert.Equal(Plan.Reactor, same.Reactor);
        Assert.Throws<ArgumentNullException>(() => Plan.Avoiding(null!));
    }

    /// <summary>
    /// An obstacle inside the Now house drops the house with its
    /// placements, roofs, floors and residents; their graves stay,
    /// unreserved; everything else stays.
    /// </summary>
    [Fact(DisplayName = "Avoiding drops a building with its contents and residents")]
    public void Avoiding_should_drop_a_building_whole()
    {
        TownPlan reduced = Plan.Avoiding(new[] { new Box(-10, 10, -10, 10) });
        Assert.DoesNotContain(reduced.Buildings, b => b.Key == "W2");
        Assert.Equal(Plan.Buildings.Count - 1, reduced.Buildings.Count);
        Assert.DoesNotContain(reduced.Placements, p => p.BuildingKey == "W2");
        Assert.DoesNotContain(reduced.Roofs, r => r.BuildingKey == "W2");
        Assert.DoesNotContain(reduced.Terrain, t => t.BuildingKey == "W2");
        Assert.Equal(new[] { "Barry", "Bea", "Constance", "Hugh", "Morty", "Ned", "Shirley" },
            reduced.Residents.Select(r => r.Key).OrderBy(k => k));
        Assert.DoesNotContain(reduced.Placements, p => p.ReservedFor is "Wendy" or "Ernest");
        Assert.Equal(10, reduced.Placements.Count(p => p.Def == "Grave"));
        Assert.Equal(Plan.Placements.Count(p => p.BuildingKey == "E2"),
            reduced.Placements.Count(p => p.BuildingKey == "E2"));
        Assert.Equal(Plan.Reactor, reduced.Reactor);
        Assert.DoesNotContain(new Cell(-10, 10), reduced.Parts.SelectMany(b => b.Cells()));
    }

    /// <summary>
    /// An obstacle one cell outside a building's walls still drops it,
    /// since the outer footprint is grown by one.
    /// </summary>
    [Fact(DisplayName = "Avoiding counts the cell around a building")]
    public void Avoiding_should_grow_footprints_by_one()
    {
        TownPlan reduced = Plan.Avoiding(new[] { new Box(-16, 11, -16, 11) });
        Assert.DoesNotContain(reduced.Buildings, b => b.Key == "W2");
        TownPlan kept = Plan.Avoiding(new[] { new Box(-17, 11, -17, 11) });
        Assert.Contains(kept.Buildings, b => b.Key == "W2");
    }

    /// <summary>
    /// Anything else on an obstacle (a street light, a stretch of road) is
    /// dropped on its own, and the rest of the town stays.
    /// </summary>
    [Fact(DisplayName = "Avoiding drops loose things on their own")]
    public void Avoiding_should_drop_loose_things_alone()
    {
        TownPlan reduced = Plan.Avoiding(new[] { new Box(4, 32, 4, 32), new Box(0, 20, 0, 21) });
        Assert.Equal(Plan.Buildings.Count, reduced.Buildings.Count);
        Assert.Equal(9, reduced.Residents.Count);
        Assert.Equal(Plan.Placements.Count - 1, reduced.Placements.Count);
        Assert.DoesNotContain(reduced.Placements, p => p.At == new Cell(4, 32));
        Assert.Equal(Plan.Terrain.Count - 2, reduced.Terrain.Count);
        Assert.Equal(Plan.Labels.Count, reduced.Labels.Count);
    }

    /// <summary>
    /// Dropping the substation takes the reactor, its label and its
    /// conduits with it, and clears the Reactor key point.
    /// </summary>
    [Fact(DisplayName = "Avoiding the substation drops the reactor")]
    public void Avoiding_the_substation_should_drop_the_reactor()
    {
        TownPlan reduced = Plan.Avoiding(new[] { new Box(-40, -1, -38, 1) });
        Assert.Null(reduced.Reactor);
        Assert.DoesNotContain(reduced.Placements, p => p.Def == "BT_BatshittiumReactor");
        Assert.DoesNotContain(reduced.Labels, l => l.Kind == LabelKind.Substation);
        Assert.DoesNotContain(reduced.Placements, p => p.BuildingKey == "Substation");
        Assert.Equal(Plan.Bounds, reduced.Bounds);
        Assert.Equal(Plan.Core, reduced.Core);
    }

    /// <summary>
    /// Dropping Public Works takes a stockpile inside it and the incident
    /// board with it. AnyRim zones no stockpile, so one is added for the
    /// test: the plan format still carries them.
    /// </summary>
    [Fact(DisplayName = "Avoiding Public Works drops its stockpile and label")]
    public void Avoiding_public_works_should_drop_its_stockpile()
    {
        var pile = new StockpilePlan(new Box(14, -9, 15, -8), new[] { "Steel" });
        var away = new StockpilePlan(new Box(-1, 30, -1, 30), new[] { "Steel" });
        TownPlan zoned = WithStockpiles(Plan, pile, away);
        TownPlan reduced = zoned.Avoiding(new[] { new Box(12, -12, 12, -12) });
        Assert.Equal(new[] { away }, reduced.Stockpiles);
        Assert.DoesNotContain(reduced.Labels, l => l.Kind == LabelKind.Incident);
        Assert.DoesNotContain(reduced.Residents, r => r.Key == "Morty");
        Assert.Contains(reduced.Buildings, b => b.Key == "BallistaYard");
        Assert.Empty(zoned.Avoiding(new[] { new Box(-1, 30, -1, 30) })
            .Stockpiles.Where(s => s == away));
    }

    // ===== Helpers =====

    /// <summary>A copy of a plan with the given stockpiles.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="piles">The stockpiles it should have.</param>
    /// <returns>The copy.</returns>
    private static TownPlan WithStockpiles(TownPlan plan, params StockpilePlan[] piles) =>
        new()
        {
            Name = plan.Name,
            Quality = plan.Quality,
            Seed = plan.Seed,
            Bounds = plan.Bounds,
            Core = plan.Core,
            FirebreakWidth = plan.FirebreakWidth,
            GroundZero = plan.GroundZero,
            Reactor = plan.Reactor,
            WelcomeSign = plan.WelcomeSign,
            Terrain = plan.Terrain,
            Roofs = plan.Roofs,
            Placements = plan.Placements,
            Buildings = plan.Buildings,
            Residents = plan.Residents,
            Labels = plan.Labels,
            Stockpiles = piles,
            Parts = plan.Parts,
        };

    /// <summary>A def's catalogue entry.</summary>
    /// <param name="p">A placement.</param>
    /// <returns>Its def's facts.</returns>
    private static DefInfo Info(Placement p) => TownDefs.Get(p.Def);

    /// <summary>Whether a placement occupies the one-per-cell building layer.</summary>
    /// <param name="p">The placement.</param>
    /// <returns>True for walls, doors, fences, furniture and devices.</returns>
    private static bool IsBuildingLayer(Placement p) =>
        p.Kind == PlacementKind.Building && Info(p).Layer == ThingLayer.Building;

    /// <summary>Whether a building stops a walker: walls, fences and impassable things.</summary>
    /// <param name="p">The building.</param>
    /// <returns>True if it blocks.</returns>
    private static bool Blocks(Placement p)
    {
        DefInfo info = Info(p);
        return info.IsWall || info.IsFence || !info.Passable;
    }

    /// <summary>The building-layer placement on each cell.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>A map from cell to placement.</returns>
    private static Dictionary<Cell, Placement> BuildingCells(TownPlan plan)
    {
        var map = new Dictionary<Cell, Placement>();
        foreach (Placement p in plan.Placements.Where(IsBuildingLayer))
        {
            foreach (Cell c in p.Occupied.Cells())
            {
                map[c] = p;
            }
        }

        return map;
    }

    /// <summary>The final terrain of each planned cell (later entries win).</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>A map from cell to terrain.</returns>
    private static Dictionary<Cell, TerrainPlacement> TerrainCells(TownPlan plan)
    {
        var map = new Dictionary<Cell, TerrainPlacement>();
        foreach (TerrainPlacement t in plan.Terrain)
        {
            map[t.At] = t;
        }

        return map;
    }

    /// <summary>The four cells sharing an edge with a cell.</summary>
    /// <param name="c">The cell.</param>
    /// <returns>Its neighbours.</returns>
    private static IEnumerable<Cell> Neighbours(Cell c)
    {
        yield return c + new Cell(1, 0);
        yield return c + new Cell(-1, 0);
        yield return c + new Cell(0, 1);
        yield return c + new Cell(0, -1);
    }

    /// <summary>
    /// Every cell a walker reaches from a start by shared edges, within the
    /// bounds grown by one, through doors and gates but not through walls,
    /// fences or impassable things. Unplanned cells are open ground.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="start">Where the walker starts.</param>
    /// <param name="built">The building-layer map.</param>
    /// <returns>The reached cells.</returns>
    private static HashSet<Cell> Reachable(TownPlan plan, Cell start, Dictionary<Cell, Placement> built)
    {
        Box domain = plan.Bounds.ExpandedBy(1);
        var reached = new HashSet<Cell> { start };
        var frontier = new Queue<Cell>(reached);
        while (frontier.Count > 0)
        {
            foreach (Cell n in Neighbours(frontier.Dequeue()))
            {
                bool open = domain.Contains(n) && (!built.TryGetValue(n, out Placement? p) || !Blocks(p));
                if (open && reached.Add(n))
                {
                    frontier.Enqueue(n);
                }
            }
        }

        return reached;
    }

    /// <summary>
    /// The room around a cell inside a building, as RimWorld bounds rooms:
    /// by walls and doors, with furniture inside the room.
    /// </summary>
    /// <param name="b">The building.</param>
    /// <param name="start">A cell inside it.</param>
    /// <param name="built">The building-layer map.</param>
    /// <returns>The room's cells.</returns>
    private static HashSet<Cell> RoomOf(BuildingPlan b, Cell start, Dictionary<Cell, Placement> built)
    {
        Box inside = b.Outer.ExpandedBy(-1);
        var room = new HashSet<Cell> { start };
        var frontier = new Queue<Cell>(room);
        while (frontier.Count > 0)
        {
            foreach (Cell n in Neighbours(frontier.Dequeue()))
            {
                bool bound = built.TryGetValue(n, out Placement? p) && (Info(p).IsWall || Info(p).IsDoor);
                if (inside.Contains(n) && !bound && room.Add(n))
                {
                    frontier.Enqueue(n);
                }
            }
        }

        return room;
    }

    /// <summary>Whether a cell continues a wall or fence line (walls, fences, doors).</summary>
    /// <param name="built">The building-layer map.</param>
    /// <param name="c">The cell.</param>
    /// <returns>True if the line continues there.</returns>
    private static bool InLine(Dictionary<Cell, Placement> built, Cell c) =>
        built.TryGetValue(c, out Placement? p)
        && (Info(p).IsWall || Info(p).IsFence || Info(p).IsDoor);

    /// <summary>Whether a walker can stand on a cell.</summary>
    /// <param name="built">The building-layer map.</param>
    /// <param name="c">The cell.</param>
    /// <returns>True if nothing there blocks.</returns>
    private static bool IsOpen(Dictionary<Cell, Placement> built, Cell c) =>
        !built.TryGetValue(c, out Placement? p) || !Blocks(p);

    /// <summary>Where the plan draws a label.</summary>
    /// <param name="kind">The label.</param>
    /// <returns>Its cell.</returns>
    private static Cell LabelAt(LabelKind kind) =>
        Plan.Labels.Single(l => l.Kind == kind).At;

    /// <summary>The plan's only placement of a def.</summary>
    /// <param name="def">The defName.</param>
    /// <returns>The placement.</returns>
    private static Placement Only(string def) =>
        Plan.Placements.Single(p => p.Def == def);

    /// <summary>
    /// A placement's build rank: walls and fences 0, doors and gates 1,
    /// other buildings 2, conduits 3, items 4, plants 5, animals 6.
    /// </summary>
    /// <param name="p">The placement.</param>
    /// <returns>Its rank.</returns>
    private static int Rank(Placement p)
    {
        DefInfo info = Info(p);
        return p.Kind switch
        {
            PlacementKind.Item or PlacementKind.Crate => 4,
            PlacementKind.Plant => 5,
            PlacementKind.Animal => 6,
            _ when info.IsWall || info.IsFence => 0,
            _ when info.IsDoor => 1,
            _ => info.Layer == ThingLayer.Conduit ? 3 : 2,
        };
    }

    /// <summary>
    /// A model home's buildings as text, moved to z = 0 at its south wall
    /// and, for the east side, mirrored back to the west.
    /// </summary>
    /// <param name="key">The home.</param>
    /// <param name="dz">Its south wall's z.</param>
    /// <param name="mirror">Whether to mirror it.</param>
    /// <returns>One sorted line per building.</returns>
    private static List<string> PlanALayout(string key, int dz, bool mirror) =>
        Plan.Placements
            .Where(p => p.BuildingKey == key && p.Kind == PlacementKind.Building)
            .Select(p =>
            {
                Box box = p.Occupied.Offset(new Cell(0, -dz));
                Facing rot = mirror ? Footprint.MirroredX(p.Rot) : p.Rot;
                return $"{p.Def} {(mirror ? box.MirroredX : box)} {rot}";
            })
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

    /// <summary>Whether a centre-line cell is painted: two on, two off from 5.</summary>
    /// <param name="along">Signed distance along the street.</param>
    /// <returns>True for a dash.</returns>
    private static bool Dash(int along) => Math.Abs(along) >= 5 && (Math.Abs(along) - 5) % 4 < 2;

    /// <summary>
    /// A plan as comparable text, one line per element, optionally leaving
    /// out the taper terrain.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="tapers">Whether to include taper cells.</param>
    /// <returns>The lines.</returns>
    private static List<string> Describe(TownPlan plan, bool tapers)
    {
        var lines = new List<string>
        {
            $"{plan.Name} {plan.Quality} {plan.Bounds} {plan.Core} {plan.FirebreakWidth}",
            $"{plan.GroundZero} {plan.Reactor} {plan.WelcomeSign}",
        };
        lines.AddRange(plan.Terrain.Where(t => tapers || !t.Taper)
            .Select(t => $"T {t.At} {t.Def} {t.Paint} {t.BuildingKey} {t.Taper}"));
        lines.AddRange(plan.Roofs.Select(r => $"R {r}"));
        lines.AddRange(plan.Placements.Select(p =>
            $"P {p.Kind} {p.Def} {p.At} {p.Rot} {p.Stuff} {p.Owner} {p.SizeX}x{p.SizeZ} {p.Count} "
            + $"{p.Forbidden} {p.Paint} {p.Quality} {p.Name} {p.Growth} {p.SwitchedOff} {p.Refuel} "
            + $"{p.AutoRefuelOff} {p.SuspendBills} {p.ReservedFor} {p.OnlyIfCold} {p.BuildingKey} "
            + string.Join(",", p.Load)));
        lines.AddRange(plan.Buildings.Select(b =>
            $"B {b.Key} {b.Name} {b.Role} {b.Outer} {b.FrontDoor} {b.StreetCell} {b.Enclosed} "
            + $"{b.Side} {b.Slot} {string.Join(";", b.Rooms)}"));
        lines.AddRange(plan.Residents.Select(r => $"H {r} {string.Join(",", r.Apparel)}"));
        lines.AddRange(plan.Labels.Select(l => $"L {l}"));
        lines.AddRange(plan.Stockpiles.Select(s => $"S {s.Area} {string.Join(",", s.Accepts)}"));
        return lines;
    }
}
