using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>
/// Lays out AnyRim, the showcase's test town, exactly as docs/SHOWCASE.md
/// draws it, as a <see cref="TownPlan"/> in town-local coordinates: (0,0)
/// is the crossroads and Ground Zero, x grows east and z north. This part
/// holds the bookkeeping, the streets and road tapers, the street
/// furniture, the power grid and the final assembly; AnyRimLots.cs holds
/// the eight lots and the outbuildings.
/// </summary>
/// <remarks>
/// Only the taper wander and gaps come from the seed, through
/// <see cref="SeededStream"/>; everything else is fixed, so the same seed
/// always gives the same town and two seeds differ only in their tapers.
/// </remarks>
internal sealed partial class AnyRimBuilder
{
    /// <summary>The Batshittium Reactor's defName.</summary>
    internal const string ReactorDef = "BT_BatshittiumReactor";

    /// <summary>Main Street's southern end (z).</summary>
    internal const int MainSouth = -21;

    /// <summary>Main Street's northern end (z), at the cemetery road.</summary>
    internal const int MainNorth = 47;

    /// <summary>Doorstep Lane's ends (x = -20 and +20).</summary>
    internal const int LaneEnd = 20;

    /// <summary>Carriageways run from -2 to +2 across.</summary>
    internal const int Kerb = 2;

    /// <summary>Sidewalks lie on x = +/-3 and z = +/-3.</summary>
    internal const int SidewalkLine = 3;

    /// <summary>Centre-line dashes start this far from Ground Zero.</summary>
    internal const int CentreLineStart = 5;

    /// <summary>Width of the bare firebreak around the core.</summary>
    internal const int Firebreak = 4;

    private const string Wood = "WoodLog";
    private const string Cloth = "Cloth";
    private const string Steel = "Steel";
    private const string Soil = "Soil";

    private static readonly Cell Origin = new(0, 0);

    private readonly int seed;
    private readonly TownPalette palette;
    private readonly SeededStream random;
    private readonly List<TerrainPlacement> terrain = new();
    private readonly Dictionary<Cell, int> terrainAt = new();
    private readonly List<Placement>[] phases;
    private readonly HashSet<Cell> conduitCells = new();
    private readonly List<RoofPlacement> roofs = new();
    private readonly List<BuildingPlan> buildings = new();
    private readonly List<ResidentPlan> residents = new();
    private readonly List<LabelPlan> labels = new();
    private readonly List<StockpilePlan> stockpiles = new();
    private readonly List<Cell> lawn = new();
    private Box taperExtent;
    private bool anyTaper;

    /// <summary>Starts an empty plan.</summary>
    /// <param name="seed">The taper seed.</param>
    /// <param name="palette">Surfaces and furniture quality.</param>
    private AnyRimBuilder(int seed, TownPalette palette)
    {
        this.seed = seed;
        this.palette = palette
            ?? throw new ArgumentNullException(nameof(palette));
        random = new SeededStream(seed);
        phases = new List<Placement>[Enum.GetValues(typeof(Phase)).Length];
        for (int i = 0; i < phases.Length; i++)
        {
            phases[i] = new List<Placement>();
        }
    }

    /// <summary>
    /// Build order within <see cref="TownPlan.Placements"/>: walls and
    /// fences, then doors and gates, then everything else built, then
    /// conduits, items, plants and animals.
    /// </summary>
    private enum Phase
    {
        /// <summary>Walls and fences.</summary>
        Wall,

        /// <summary>Doors and gates, set into the walls and fences.</summary>
        Door,

        /// <summary>Other buildings, furniture and devices.</summary>
        Building,

        /// <summary>Power conduits.</summary>
        Conduit,

        /// <summary>Items and crates.</summary>
        Item,

        /// <summary>Plants.</summary>
        Plant,

        /// <summary>Animals.</summary>
        Animal,
    }

    /// <summary>Plans AnyRim.</summary>
    /// <param name="seed">The taper seed.</param>
    /// <param name="palette">Surfaces and furniture quality.</param>
    /// <returns>The plan.</returns>
    internal static TownPlan Build(int seed, TownPalette palette) =>
        new AnyRimBuilder(seed, palette).Run();

    /// <summary>Lays out the whole town, in dependency order.</summary>
    /// <returns>The finished plan.</returns>
    private TownPlan Run()
    {
        Streets();
        Tapers();
        Lots();
        Outbuildings();
        StreetFurniture();
        Power();
        Residents();
        Labels();
        stockpiles.Add(new StockpilePlan(
            ArtificerStockpile,
            new[]
            {
                "BT_BoltPainful", "BT_BoltReallyPainful",
                "BT_ErgonomicSingularityGrenade",
            }));
        return Finish();
    }

    // ===== Streets =====

    /// <summary>
    /// Main Street and Doorstep Lane: carriageways, dashed centre lines
    /// (two cells painted, two not, stopping short of the roundel),
    /// sidewalks, and the Ground Zero roundel, laid only over street and
    /// sidewalk cells.
    /// </summary>
    private void Streets()
    {
        string road = palette.Carriageway;
        string? paint = palette.CarriagewayPaint;
        LayBox(new Box(-Kerb, MainSouth, Kerb, MainNorth), road, paint);
        LayBox(new Box(-LaneEnd, -Kerb, LaneEnd, Kerb), road, paint);

        if (palette.CentreLinePaint is string line)
        {
            for (int z = MainSouth; z <= MainNorth; z++)
            {
                if (IsDash(z))
                {
                    Lay(new Cell(0, z), road, line);
                }
            }

            for (int x = -LaneEnd; x <= LaneEnd; x++)
            {
                if (IsDash(x))
                {
                    Lay(new Cell(x, 0), road, line);
                }
            }
        }

        for (int z = MainSouth; z <= MainNorth; z++)
        {
            if (Math.Abs(z) > Kerb)
            {
                Lay(new Cell(-SidewalkLine, z), palette.Sidewalk);
                Lay(new Cell(SidewalkLine, z), palette.Sidewalk);
            }
        }

        for (int x = -LaneEnd; x <= LaneEnd; x++)
        {
            if (Math.Abs(x) > Kerb)
            {
                Lay(new Cell(x, -SidewalkLine), palette.Sidewalk);
                Lay(new Cell(x, SidewalkLine), palette.Sidewalk);
            }
        }

        foreach (Cell c in new Box(-4, -4, 4, 4).Cells())
        {
            double d = c.DistanceTo(Origin);
            if (d <= 4.5 && terrainAt.ContainsKey(c))
            {
                Lay(c, d <= 1.5 ? "GoldTile"
                    : d <= 3 ? "FlagstoneMarble"
                    : "FlagstoneSlate");
            }
        }
    }

    /// <summary>
    /// Whether a centre-line cell, at a distance along the street from
    /// Ground Zero, is painted: two cells on, two off, from
    /// <see cref="CentreLineStart"/> outward.
    /// </summary>
    /// <param name="along">The signed distance along the street.</param>
    /// <returns>True for a painted dash.</returns>
    internal static bool IsDash(int along)
    {
        int d = Math.Abs(along);
        return d >= CentreLineStart && (d - CentreLineStart) % 4 < 2;
    }

    // ===== Tapers =====

    /// <summary>
    /// The four road ends. Each runs on past the town in three surfaces,
    /// narrowing as it goes: south and east end in a one-wide dirt track
    /// that wanders and has gaps; north ends at the cemetery gate and west
    /// at the substation gate.
    /// </summary>
    private void Tapers()
    {
        Arm(new Cell(0, MainSouth - 1), new Cell(0, -1), new Cell(1, 0),
            new[] { 5, 5, 4, 3, 3 }, new[] { 3, 3, 3, 2, 2 }, 5, true);
        Arm(new Cell(0, MainNorth + 1), new Cell(0, 1), new Cell(1, 0),
            new[] { 5, 4, 3 }, new[] { 3, 3, 2 }, 2, false);
        Arm(new Cell(-LaneEnd - 1, 0), new Cell(-1, 0), new Cell(0, 1),
            new[] { 5, 4, 4, 3 }, new[] { 3, 3 }, 0, false);
        Arm(new Cell(LaneEnd + 1, 0), new Cell(1, 0), new Cell(0, 1),
            new[] { 5, 5, 4, 4, 3, 3 }, new[] { 3, 3, 3, 2, 2, 2 }, 8, true);
    }

    /// <summary>
    /// One road taper, row by row outward: the near surface at the given
    /// widths, then the middle surface, then a one-wide far track.
    /// </summary>
    /// <param name="start">The first row's centre.</param>
    /// <param name="step">One row outward.</param>
    /// <param name="across">One cell across the road.</param>
    /// <param name="near">Widths of the near rows.</param>
    /// <param name="mid">Widths of the middle rows.</param>
    /// <param name="far">Length of the far track.</param>
    /// <param name="wander">
    /// Whether the far track wanders a cell either way and leaves every
    /// third cell natural (seeded); otherwise it runs straight on.
    /// </param>
    private void Arm(
        Cell start, Cell step, Cell across, int[] near, int[] mid,
        int far, bool wander)
    {
        // Which side an even width favours; seeded per arm.
        int lean = random.Next(2);
        int row = 0;
        foreach (int width in near)
        {
            TaperRow(start + Times(step, row++), across, width, lean,
                palette.TaperNear);
        }

        foreach (int width in mid)
        {
            TaperRow(start + Times(step, row++), across, width, lean,
                palette.TaperMid);
        }

        int phase = wander ? random.Next(3) : 0;
        int offset = 0;
        for (int i = 0; i < far; i++)
        {
            if (wander)
            {
                offset = Math.Max(-1, Math.Min(1, offset + random.Next(3) - 1));
                if ((i + phase) % 3 == 2)
                {
                    row++;
                    continue;
                }
            }

            Lay(start + Times(step, row++) + Times(across, offset),
                palette.TaperFar, taper: true);
        }

        Cell last = start + Times(step, row - 1);
        Box arm = Box.Spanning(
            start - Times(across, Kerb), last + Times(across, Kerb));
        taperExtent = anyTaper ? taperExtent.Union(arm) : arm;
        anyTaper = true;
    }

    /// <summary>One row of a taper, centred on the road's axis.</summary>
    /// <param name="centre">The row's centre cell.</param>
    /// <param name="across">One cell across the road.</param>
    /// <param name="width">Cells in the row.</param>
    /// <param name="lean">0 or 1: the side an even width favours.</param>
    /// <param name="def">The surface.</param>
    private void TaperRow(Cell centre, Cell across, int width, int lean, string def)
    {
        int first = width % 2 == 1 ? -(width - 1) / 2 : -(width / 2) + lean;
        for (int i = 0; i < width; i++)
        {
            Lay(centre + Times(across, first + i), def, taper: true);
        }
    }

    // ===== Street furniture =====

    /// <summary>
    /// Street lights at the lot lines on alternating sides, a dead ancient
    /// lamppost at each end of town, hydrants on two corners of the
    /// crossroads, the four cars at measured distances from Ground Zero
    /// (about 11.4, 11.4, 20.7 and 32.5 tiles to their centres), the
    /// driveways under them, and the welcome and exit signs.
    /// </summary>
    private void StreetFurniture()
    {
        foreach (Cell lamp in new[]
        {
            new Cell(4, -16), new Cell(-4, -5), new Cell(4, 5),
            new Cell(-4, 18), new Cell(4, 32), new Cell(-4, 44),
        })
        {
            Add(Thing("StandingLamp", lamp));
        }

        Add(Thing("AncientLamppost", new Cell(4, MainSouth)));
        Add(Thing("AncientLamppost", new Cell(-4, MainNorth)));
        Add(Thing("AncientHydrant", new Cell(-4, -4)));
        Add(Thing("AncientHydrant", new Cell(4, 4)));

        // The Now car and Engine No. 1 in the Doorstep Lane driveways, the
        // Damages' car frame in theirs, and a car at the bank's kerb.
        LayBox(new Box(-14, 4, -5, 5), "Gravel");
        LayBox(new Box(5, 4, 14, 5), "Gravel");
        LayBox(new Box(5, 17, 14, 18), "Gravel");
        Add(Over("AncientRustedCar", new Box(-12, 4, -9, 5), Facing.West));
        Add(Over("AncientRustedTruck", new Box(9, 4, 12, 5), Facing.East));
        Add(Over("AncientRustedCarFrame", new Box(10, 17, 12, 18), Facing.East));
        Add(Over("AncientRustedCar", new Box(1, 31, 2, 34), Facing.North));

        Sign(WelcomeSignCell);
        Sign(ExitSignCell);
    }

    /// <summary>
    /// A sign: a wall painted white between two columns, running east to
    /// west. Its text is drawn above the wall by the game.
    /// </summary>
    /// <param name="centre">The wall's cell.</param>
    private void Sign(Cell centre)
    {
        Add(Thing("Column", centre + new Cell(-1, 0)) with { Stuff = Wood });
        Add(Thing("Wall", centre) with
        {
            Stuff = Wood, Paint = "Structure_White",
        });
        Add(Thing("Column", centre + new Cell(1, 0)) with { Stuff = Wood });
    }

    // ===== Power =====

    /// <summary>
    /// Conduits from the reactor out of the substation, along Doorstep
    /// Lane's north sidewalk (z = +3) and up both Main Street sidewalks,
    /// with a spur along each lot's door row to its back wall. Public
    /// Works' spur runs on into the Ballista yard and the Knott house's to
    /// the shelter behind it. One connected net, with every powered thing
    /// within RimWorld's 6-cell connection box of a conduit.
    /// </summary>
    private void Power()
    {
        foreach (Cell c in new[]
        {
            new Cell(-30, 1), new Cell(-29, 1), new Cell(-28, 1),
            new Cell(-28, 2),
        })
        {
            Conduit(c);
        }

        for (int x = -28; x <= SidewalkLine; x++)
        {
            Conduit(new Cell(x, SidewalkLine));
        }

        for (int z = MainSouth; z <= MainNorth; z++)
        {
            Conduit(new Cell(-SidewalkLine, z));
            Conduit(new Cell(SidewalkLine, z));
        }

        foreach (BuildingPlan lot in buildings.Where(b => b.Side != null).ToList())
        {
            int sign = lot.Side == "E" ? 1 : -1;
            int row = lot.FrontDoor?.Z
                ?? throw new InvalidOperationException($"{lot.Key} has no front door.");
            int end = lot.Key switch
            {
                "E1" => 21, // into the Ballista yard
                "W3" => -18, // to the Knott shelter
                _ => sign > 0 ? lot.Outer.MaxX : lot.Outer.MinX,
            };
            for (int x = 4 * sign; x * sign <= end * sign; x += sign)
            {
                Conduit(new Cell(x, row));
            }
        }
    }

    /// <summary>
    /// A conduit, once per cell, keyed to the building or outbuilding it
    /// lies in (if any) so that <see cref="TownPlan.Avoiding"/> takes it
    /// away with that building.
    /// </summary>
    /// <param name="c">The cell.</param>
    private void Conduit(Cell c)
    {
        if (conduitCells.Add(c))
        {
            string? key = buildings.FirstOrDefault(b => b.Outer.Contains(c))?.Key;
            Add(Thing("PowerConduit", c) with { BuildingKey = key });
        }
    }

    // ===== Labels =====

    /// <summary>The map labels of docs/SHOWCASE.md, "Signs and Labels".</summary>
    private void Labels()
    {
        labels.Add(new LabelPlan(WelcomeSignCell, LabelKind.Welcome));
        labels.Add(new LabelPlan(ExitSignCell, LabelKind.Exit));
        labels.Add(new LabelPlan(CemeteryGate, LabelKind.Cemetery));
        labels.Add(new LabelPlan(SubstationGate, LabelKind.Substation));
        labels.Add(new LabelPlan(new Cell(0, 6), LabelKind.Evacuation));
        labels.Add(new LabelPlan(PublicWorksDoor, LabelKind.Incident));
        labels.Add(new LabelPlan(Origin, LabelKind.GroundZero));
    }

    // ===== Assembly =====

    /// <summary>
    /// Plants the lawns (grass on every planned soil cell that holds no
    /// building or other plant), then assembles the plan with its key
    /// points, core, bounds and parts.
    /// </summary>
    /// <returns>The plan.</returns>
    private TownPlan Finish()
    {
        var built = new HashSet<Cell>();
        foreach (Placement p in phases[(int)Phase.Wall]
            .Concat(phases[(int)Phase.Door])
            .Concat(phases[(int)Phase.Building]))
        {
            built.UnionWith(p.Occupied.Cells());
        }

        var planted = new HashSet<Cell>(phases[(int)Phase.Plant].Select(p => p.At));
        foreach (Cell c in lawn)
        {
            if (!built.Contains(c) && !planted.Contains(c) && TerrainDefAt(c) == Soil
                && planted.Add(c))
            {
                Add(Make(PlacementKind.Plant, "Plant_Grass", c));
            }
        }

        List<Placement> placements = phases.SelectMany(p => p).ToList();
        Box core = CoreOf(placements);
        return new TownPlan
        {
            Name = AnyRimText.TownName,
            Quality = palette.Quality,
            Seed = seed,
            Bounds = anyTaper ? core.Union(taperExtent) : core,
            Core = core,
            FirebreakWidth = Firebreak,
            GroundZero = Origin,
            Reactor = ReactorAnchor,
            WelcomeSign = WelcomeSignCell,
            Terrain = terrain.ToList(),
            Roofs = roofs.ToList(),
            Placements = placements,
            Buildings = buildings.ToList(),
            Residents = residents.ToList(),
            Labels = labels.ToList(),
            Stockpiles = stockpiles.ToList(),
            Parts = TownParts.From(terrain, placements, buildings, labels),
        };
    }

    /// <summary>
    /// The town proper: everything except the road tapers, as one box.
    /// </summary>
    /// <param name="placements">Every placement.</param>
    /// <returns>The core box.</returns>
    private Box CoreOf(List<Placement> placements)
    {
        Box? core = null;
        foreach (TerrainPlacement t in terrain.Where(t => !t.Taper))
        {
            core = Grow(core, new Box(t.At.X, t.At.Z, t.At.X, t.At.Z));
        }

        foreach (Placement p in placements)
        {
            core = Grow(core, p.Occupied);
        }

        foreach (BuildingPlan b in buildings)
        {
            core = Grow(core, b.Outer);
        }

        foreach (LabelPlan l in labels)
        {
            core = Grow(core, new Box(l.At.X, l.At.Z, l.At.X, l.At.Z));
        }

        return core ?? throw new InvalidOperationException("The town is empty.");
    }

    /// <summary>Grows a running union by one more box.</summary>
    /// <param name="sofar">The union so far, or null.</param>
    /// <param name="box">The box to add.</param>
    /// <returns>The new union.</returns>
    private static Box Grow(Box? sofar, Box box) =>
        sofar is Box s ? s.Union(box) : box;

    // ===== Bookkeeping =====

    /// <summary>Sets a cell's terrain; a later call for the same cell wins.</summary>
    /// <param name="at">The cell.</param>
    /// <param name="def">The TerrainDef.</param>
    /// <param name="paint">A ColorDef to paint it, or null.</param>
    /// <param name="key">The building whose floor it is, or null.</param>
    /// <param name="taper">Whether it is a road-taper cell.</param>
    private void Lay(
        Cell at, string def, string? paint = null, string? key = null,
        bool taper = false)
    {
        var t = new TerrainPlacement(at, def, paint)
        {
            BuildingKey = key, Taper = taper,
        };
        if (terrainAt.TryGetValue(at, out int index))
        {
            terrain[index] = t;
        }
        else
        {
            terrainAt[at] = terrain.Count;
            terrain.Add(t);
        }
    }

    /// <summary>Sets the terrain of every cell in a box.</summary>
    /// <param name="box">The box.</param>
    /// <param name="def">The TerrainDef.</param>
    /// <param name="paint">A ColorDef to paint it, or null.</param>
    /// <param name="key">The building whose floor it is, or null.</param>
    private void LayBox(Box box, string def, string? paint = null, string? key = null)
    {
        foreach (Cell c in box.Cells())
        {
            Lay(c, def, paint, key);
        }
    }

    /// <summary>The terrain planned for a cell so far.</summary>
    /// <param name="c">The cell.</param>
    /// <returns>Its TerrainDef, or null if none is planned.</returns>
    private string? TerrainDefAt(Cell c) =>
        terrainAt.TryGetValue(c, out int i) ? terrain[i].Def : null;

    /// <summary>
    /// A placement with its def's sizes from the catalogue (a crate's too:
    /// the installed building's) and the defaults for its kind: buildings
    /// owned by the town, at the palette's quality if they have one;
    /// items, plants and animals owned by nobody; items and crates
    /// forbidden (the field's default).
    /// </summary>
    /// <remarks>
    /// Buildings are spawned not forbidden. Doors, gates and plant pots
    /// can be forbidden in RimWorld, and a forbidden door, whoever owns
    /// it, is forbidden to colonists (ForbidUtility.IsForbidden), so a
    /// game half that applied the default to them would lock the player
    /// out of every house and yard in town.
    /// </remarks>
    /// <param name="kind">What sort of thing.</param>
    /// <param name="def">Its defName.</param>
    /// <param name="at">Its anchor.</param>
    /// <param name="rot">Its facing.</param>
    /// <returns>The placement.</returns>
    private Placement Make(
        PlacementKind kind, string def, Cell at, Facing rot = Facing.North)
    {
        DefInfo info = TownDefs.Get(def);
        bool building = kind == PlacementKind.Building;
        var p = new Placement(kind, def, at)
        {
            Rot = rot,
            SizeX = info.SizeX,
            SizeZ = info.SizeZ,
            Owner = building || kind == PlacementKind.Crate ? Owner.Town : Owner.None,
            Quality = building && info.HasQuality ? palette.FurnitureQuality : null,
        };
        return building ? p with { Forbidden = false } : p;
    }

    /// <summary>A building at an anchor.</summary>
    /// <param name="def">Its defName.</param>
    /// <param name="at">Its anchor.</param>
    /// <param name="rot">Its facing.</param>
    /// <returns>The placement.</returns>
    private Placement Thing(string def, Cell at, Facing rot = Facing.North) =>
        Make(PlacementKind.Building, def, at, rot);

    /// <summary>A building that covers exactly a box.</summary>
    /// <param name="def">Its defName.</param>
    /// <param name="covers">The cells it must cover.</param>
    /// <param name="rot">Its facing.</param>
    /// <returns>The placement, anchored by <see cref="Footprint.AnchorFor"/>.</returns>
    private Placement Over(string def, Box covers, Facing rot)
    {
        DefInfo info = TownDefs.Get(def);
        return Thing(def, Footprint.AnchorFor(covers, info.SizeX, info.SizeZ, rot), rot);
    }

    /// <summary>A forbidden stack of items.</summary>
    /// <param name="def">Its defName.</param>
    /// <param name="at">Its cell.</param>
    /// <param name="count">The stack size.</param>
    /// <returns>The placement.</returns>
    private Placement Stack(string def, Cell at, int count) =>
        Make(PlacementKind.Item, def, at) with { Count = count };

    /// <summary>A fully grown plant.</summary>
    /// <param name="def">Its defName.</param>
    /// <param name="at">Its cell.</param>
    /// <returns>The placement.</returns>
    private Placement Plant(string def, Cell at) =>
        Make(PlacementKind.Plant, def, at);

    /// <summary>Adds a placement to the list for its build phase.</summary>
    /// <param name="p">The placement.</param>
    private void Add(Placement p) => phases[(int)PhaseOf(p)].Add(p);

    /// <summary>Adds a placement, mirrored across x = 0 if asked.</summary>
    /// <param name="mirror">Whether to mirror it.</param>
    /// <param name="p">The placement, drawn for the west side.</param>
    private void Add(bool mirror, Placement p) => Add(mirror ? Mirrored(p) : p);

    /// <summary>
    /// A placement mirrored across x = 0: the mirrored facing at the anchor
    /// that covers the mirror image of its cells.
    /// </summary>
    /// <param name="p">The placement.</param>
    /// <returns>The mirror image.</returns>
    private static Placement Mirrored(Placement p)
    {
        (Cell at, Facing rot) = Footprint.MirrorX(p.At, p.SizeX, p.SizeZ, p.Rot);
        return p with { At = at, Rot = rot };
    }

    /// <summary>Which build phase a placement belongs to.</summary>
    /// <param name="p">The placement.</param>
    /// <returns>Its phase.</returns>
    private static Phase PhaseOf(Placement p)
    {
        switch (p.Kind)
        {
            case PlacementKind.Item:
            case PlacementKind.Crate:
                return Phase.Item;
            case PlacementKind.Plant:
                return Phase.Plant;
            case PlacementKind.Animal:
                return Phase.Animal;
        }

        DefInfo info = TownDefs.Get(p.Def);
        if (info.IsWall || info.IsFence)
        {
            return Phase.Wall;
        }

        if (info.IsDoor)
        {
            return Phase.Door;
        }

        return info.Layer == ThingLayer.Conduit ? Phase.Conduit : Phase.Building;
    }

    /// <summary>A cell scaled as a vector.</summary>
    /// <param name="c">The cell.</param>
    /// <param name="k">The factor.</param>
    /// <returns>The scaled cell.</returns>
    private static Cell Times(Cell c, int k) => new(c.X * k, c.Z * k);

    /// <summary>A cell, mirrored across x = 0 if asked.</summary>
    /// <param name="mirror">Whether to mirror it.</param>
    /// <param name="c">The cell, for the west side.</param>
    /// <returns>The cell.</returns>
    private static Cell As(bool mirror, Cell c) => mirror ? c.MirroredX : c;

    /// <summary>A box, mirrored across x = 0 if asked.</summary>
    /// <param name="mirror">Whether to mirror it.</param>
    /// <param name="b">The box, for the west side.</param>
    /// <returns>The box.</returns>
    private static Box As(bool mirror, Box b) => mirror ? b.MirroredX : b;
}
