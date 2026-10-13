using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>Who owns a placed thing once the town is built.</summary>
public enum Owner
{
    /// <summary>
    /// The town's own faction (the hidden, neutral Ancients in RimWorld):
    /// structures, furniture and the residents' beds.
    /// </summary>
    Town,

    /// <summary>The player: Batshit devices, so that their buttons work.</summary>
    Player,

    /// <summary>No faction: items, plants and wild animals.</summary>
    None,
}

/// <summary>What sort of thing a placement makes.</summary>
public enum PlacementKind
{
    /// <summary>A building (wall, door, fence, furniture, device, conduit).</summary>
    Building,

    /// <summary>A stack of items.</summary>
    Item,

    /// <summary>A plant, at <see cref="Placement.Growth"/>.</summary>
    Plant,

    /// <summary>An animal (a PawnKindDef), wild, optionally named.</summary>
    Animal,

    /// <summary>A building still in its crate (an uninstalled, minified thing).</summary>
    Crate,
}

/// <summary>The kind of roof over a cell.</summary>
public enum RoofKind
{
    /// <summary>An ordinary constructed roof (RoofConstructed).</summary>
    Constructed,

    /// <summary>Overhead mountain (RoofRockThick); swallows overhead projectiles.</summary>
    RockThick,
}

/// <summary>One cell of planned terrain. Later entries override earlier ones.</summary>
/// <param name="At">The cell, town-local.</param>
/// <param name="Def">The TerrainDef's defName.</param>
/// <param name="Paint">A ColorDef's defName to paint it, or null.</param>
public sealed record TerrainPlacement(Cell At, string Def, string? Paint = null)
{
    /// <summary>The building whose floor this is, if any (see <see cref="TownPlan.Avoiding"/>).</summary>
    public string? BuildingKey { get; init; }

    /// <summary>
    /// A road-taper cell outside <see cref="TownPlan.Core"/>, which the game
    /// does not level: where the ground is unsuitable (water, rock,
    /// something indestructible) the road just stops there.
    /// </summary>
    public bool Taper { get; init; }
}

/// <summary>One cell of planned roof.</summary>
/// <param name="At">The cell, town-local.</param>
/// <param name="Kind">The roof.</param>
/// <param name="BuildingKey">The building it covers.</param>
public sealed record RoofPlacement(Cell At, RoofKind Kind, string BuildingKey);

/// <summary>
/// One thing to make and spawn. Fields that do not apply to its kind are
/// left at their defaults.
/// </summary>
/// <param name="Kind">What sort of thing.</param>
/// <param name="Def">The ThingDef's defName (PawnKindDef's for animals).</param>
/// <param name="At">The anchor cell (RimWorld's Position), town-local.</param>
public sealed record Placement(PlacementKind Kind, string Def, Cell At)
{
    /// <summary>Facing.</summary>
    public Facing Rot { get; init; } = Facing.North;

    /// <summary>The stuff defName for stuff-made defs; null otherwise.</summary>
    public string? Stuff { get; init; }

    /// <summary>Who owns it.</summary>
    public Owner Owner { get; init; } = Owner.Town;

    /// <summary>The def's size.x (from <see cref="TownDefs"/>).</summary>
    public int SizeX { get; init; } = 1;

    /// <summary>The def's size.z (from <see cref="TownDefs"/>).</summary>
    public int SizeZ { get; init; } = 1;

    /// <summary>Items: the stack count, never above the def's stack limit.</summary>
    public int Count { get; init; } = 1;

    /// <summary>Items and crates: spawned forbidden.</summary>
    public bool Forbidden { get; init; } = true;

    /// <summary>Buildings: a ColorDef defName to paint it, or null.</summary>
    public string? Paint { get; init; }

    /// <summary>Furniture: a QualityCategory name, or null for none.</summary>
    public string? Quality { get; init; }

    /// <summary>Animals: a name, or null.</summary>
    public string? Name { get; init; }

    /// <summary>Plants: growth from 0 to 1.</summary>
    public float Growth { get; init; } = 1f;

    /// <summary>Flickable buildings: spawned switched off.</summary>
    public bool SwitchedOff { get; init; }

    /// <summary>Refuelable buildings: filled to capacity.</summary>
    public bool Refuel { get; init; }

    /// <summary>Refuelable buildings: auto-refuel turned off.</summary>
    public bool AutoRefuelOff { get; init; }

    /// <summary>Work tables: every bill suspended.</summary>
    public bool SuspendBills { get; init; }

    /// <summary>Beds and graves: the <see cref="ResidentPlan.Key"/> of the resident it belongs to.</summary>
    public string? ReservedFor { get; init; }

    /// <summary>Built only when the outdoor temperature is below 5 °C (heaters).</summary>
    public bool OnlyIfCold { get; init; }

    /// <summary>
    /// Ballista and magazines: bolts to load, as (ThingDef defName, count)
    /// pairs. Empty for everything else.
    /// </summary>
    public IReadOnlyList<(string Def, int Count)> Load { get; init; } =
        Array.Empty<(string, int)>();

    /// <summary>The building or outbuilding this belongs to (see <see cref="TownPlan.Avoiding"/>).</summary>
    public string? BuildingKey { get; init; }

    /// <summary>The cells it covers.</summary>
    public Box Occupied => Footprint.Occupied(At, SizeX, SizeZ, Rot);

    /// <summary>
    /// Its interaction cell, where a pawn stands to use it, if its def has
    /// one (<see cref="DefInfo.InteractionCell"/>); null for crates and
    /// for defs without one or outside the catalogue.
    /// </summary>
    public Cell? InteractionCell =>
        Kind == PlacementKind.Building
        && TownDefs.All.TryGetValue(Def, out DefInfo? info)
        && info.InteractionCell is Cell offset
            ? Footprint.InteractionCell(At, offset, Rot)
            : null;
}

/// <summary>What a building is for.</summary>
public enum BuildingRole
{
    /// <summary>A home.</summary>
    Residence,

    /// <summary>The utility building with the Batshit equipment.</summary>
    Utility,

    /// <summary>A surprise.</summary>
    Surprise,

    /// <summary>A civic building.</summary>
    Civic,

    /// <summary>Something outside the lots: a yard, pen, shelter or cemetery.</summary>
    Outbuilding,
}

/// <summary>A building or outbuilding.</summary>
/// <param name="Key">A unique key ("W1", "E1", ..., "Substation", "KnottShelter").</param>
/// <param name="Name">Its name as players read it.</param>
/// <param name="Role">What it is for.</param>
/// <param name="Outer">Its outer footprint, walls or fence included.</param>
public sealed record BuildingPlan(string Key, string Name, BuildingRole Role, Box Outer)
{
    /// <summary>Its front door (or gate), or null.</summary>
    public Cell? FrontDoor { get; init; }

    /// <summary>
    /// A street or sidewalk cell from which every room must be reachable,
    /// or null for buildings with no connection to the street.
    /// </summary>
    public Cell? StreetCell { get; init; }

    /// <summary>Its rooms' interiors (inside the walls), for roofed buildings.</summary>
    public IReadOnlyList<Box> Rooms { get; init; } = Array.Empty<Box>();

    /// <summary>Whether it has walls and a roof (false for yards, pens and the cemetery).</summary>
    public bool Enclosed { get; init; }

    /// <summary>For the west or east row: "W" or "E"; null for outbuildings.</summary>
    public string? Side { get; init; }

    /// <summary>For the rows: 1 for the southernmost lot; 0 for outbuildings.</summary>
    public int Slot { get; init; }
}

/// <summary>A resident.</summary>
/// <param name="Key">A unique key ("Hugh").</param>
/// <param name="First">First name.</param>
/// <param name="Nick">Nickname; shown above the pawn; at most 16 characters.</param>
/// <param name="Last">Last name.</param>
/// <param name="Female">True for female, false for male.</param>
/// <param name="Age">Biological age in years.</param>
/// <param name="HomeKey">The <see cref="BuildingPlan.Key"/> of their home.</param>
/// <param name="DutySpot">The cell they wander around (their front door, inside).</param>
public sealed record ResidentPlan(
    string Key, string First, string Nick, string Last, bool Female, int Age,
    string HomeKey, Cell DutySpot)
{
    /// <summary>Apparel ThingDef defNames to wear, over generated clothes.</summary>
    public IReadOnlyList<string> Apparel { get; init; } = Array.Empty<string>();
}

/// <summary>Which text a map label shows (see <see cref="AnyRimText.Label"/>).</summary>
public enum LabelKind
{
    /// <summary>The welcome sign, with the live population.</summary>
    Welcome,

    /// <summary>The exit sign.</summary>
    Exit,

    /// <summary>The cemetery gate.</summary>
    Cemetery,

    /// <summary>The substation gate.</summary>
    Substation,

    /// <summary>The evacuation route sign.</summary>
    Evacuation,

    /// <summary>Days without incident, over the Public Works door.</summary>
    Incident,

    /// <summary>The Ground Zero roundel.</summary>
    GroundZero,
}

/// <summary>A label drawn over a cell.</summary>
/// <param name="At">The cell, town-local.</param>
/// <param name="Kind">Which text.</param>
public sealed record LabelPlan(Cell At, LabelKind Kind);

/// <summary>A player stockpile zone.</summary>
/// <param name="Area">Its cells.</param>
/// <param name="Accepts">The ThingDef defNames it accepts, and nothing else.</param>
public sealed record StockpilePlan(Box Area, IReadOnlyList<string> Accepts);

/// <summary>
/// A whole town as data, in town-local coordinates: (0,0) is the
/// crossroads (Ground Zero), x east, z north. The game translates it to the
/// map and builds it in order: terrain, roofs, buildings, items, plants,
/// animals, stockpiles, residents. Kept free of RimWorld types so that its
/// geometry can be unit tested. See docs/SHOWCASE.md.
/// </summary>
public sealed class TownPlan
{
    /// <summary>The town's name.</summary>
    public string Name { get; init; } = AnyRimText.TownName;

    /// <summary>The quality tier it was planned at.</summary>
    public TownQuality Quality { get; init; } = TownQuality.Medium;

    /// <summary>The seed its random details (taper wander and gaps) came from.</summary>
    public int Seed { get; init; }

    /// <summary>Everything the town touches, tapering roads included.</summary>
    public Box Bounds { get; init; }

    /// <summary>
    /// The town proper without its road tapers: the area ringed by the
    /// firebreak.
    /// </summary>
    public Box Core { get; init; }

    /// <summary>Width of the bare firebreak around <see cref="Core"/>, in cells.</summary>
    public int FirebreakWidth { get; init; } = 4;

    /// <summary>The crossroads, which is Ground Zero: (0,0).</summary>
    public Cell GroundZero { get; init; }

    /// <summary>The reactor's anchor cell, or null if the plan has none.</summary>
    public Cell? Reactor { get; init; }

    /// <summary>The welcome sign's label cell.</summary>
    public Cell WelcomeSign { get; init; }

    /// <summary>Terrain, in order; later entries win.</summary>
    public IReadOnlyList<TerrainPlacement> Terrain { get; init; } = Array.Empty<TerrainPlacement>();

    /// <summary>Roofs.</summary>
    public IReadOnlyList<RoofPlacement> Roofs { get; init; } = Array.Empty<RoofPlacement>();

    /// <summary>Things, in build order (walls before doors before furniture before items).</summary>
    public IReadOnlyList<Placement> Placements { get; init; } = Array.Empty<Placement>();

    /// <summary>Buildings and outbuildings.</summary>
    public IReadOnlyList<BuildingPlan> Buildings { get; init; } = Array.Empty<BuildingPlan>();

    /// <summary>Residents.</summary>
    public IReadOnlyList<ResidentPlan> Residents { get; init; } = Array.Empty<ResidentPlan>();

    /// <summary>Map labels.</summary>
    public IReadOnlyList<LabelPlan> Labels { get; init; } = Array.Empty<LabelPlan>();

    /// <summary>Player stockpile zones.</summary>
    public IReadOnlyList<StockpilePlan> Stockpiles { get; init; } = Array.Empty<StockpilePlan>();

    /// <summary>
    /// Every cell the town touches (terrain, placements, buildings and
    /// labels) as a few boxes, row runs merged where they line up. Unlike
    /// <see cref="Bounds"/>, which is one rectangle and mostly empty land
    /// around an L-shaped town, these hug the town, so keep-away rules can
    /// be checked against what is really there (see the plan overload of
    /// <see cref="TownSite"/>.Best).
    /// </summary>
    public IReadOnlyList<Box> Parts { get; init; } = Array.Empty<Box>();

    /// <summary>
    /// Plans AnyRim, the showcase's test town, as docs/SHOWCASE.md
    /// describes it.
    /// </summary>
    /// <param name="seed">Seeds the taper wander and gaps; the same seed gives the same town.</param>
    /// <param name="palette">Street surfaces and furniture quality; AnyRim is Medium.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentNullException">No palette.</exception>
    public static TownPlan AnyRim(int seed, TownPalette palette)
    {
        if (palette == null)
        {
            throw new ArgumentNullException(nameof(palette));
        }

        return AnyRimBuilder.Build(seed, palette);
    }

    /// <summary>
    /// A copy without anything an obstacle would overlap: every building or
    /// outbuilding whose outer footprint (grown by one cell) touches an
    /// obstacle is dropped with its placements, roofs, floors, stockpiles
    /// and residents; any other placement or terrain cell on an obstacle is
    /// dropped on its own.
    /// </summary>
    /// <remarks>
    /// Also dropped: labels on an obstacle or inside a dropped building, and
    /// stockpiles touching either. Beds and graves elsewhere that were
    /// reserved for a dropped resident are kept, unreserved. The key point
    /// <see cref="Reactor"/> becomes null if the reactor goes;
    /// <see cref="Bounds"/> and <see cref="Core"/> are kept, since they
    /// still contain everything left.
    /// </remarks>
    /// <param name="obstacles">Boxes to keep clear, town-local.</param>
    /// <returns>The reduced plan.</returns>
    /// <exception cref="ArgumentNullException">No obstacle list.</exception>
    public TownPlan Avoiding(IEnumerable<Box> obstacles)
    {
        if (obstacles == null)
        {
            throw new ArgumentNullException(nameof(obstacles));
        }

        var blocks = new List<Box>();
        foreach (Box box in obstacles)
        {
            if (box.IsValid)
            {
                blocks.Add(box);
            }
        }

        var dropped = new HashSet<string>(StringComparer.Ordinal);
        var droppedOuters = new List<Box>();
        var buildings = new List<BuildingPlan>();
        foreach (BuildingPlan building in Buildings)
        {
            if (AnyOverlaps(blocks, building.Outer.ExpandedBy(1)))
            {
                dropped.Add(building.Key);
                droppedOuters.Add(building.Outer);
            }
            else
            {
                buildings.Add(building);
            }
        }

        var residents = new List<ResidentPlan>();
        var homeless = new HashSet<string>(StringComparer.Ordinal);
        foreach (ResidentPlan resident in Residents)
        {
            if (dropped.Contains(resident.HomeKey))
            {
                homeless.Add(resident.Key);
            }
            else
            {
                residents.Add(resident);
            }
        }

        var placements = new List<Placement>();
        foreach (Placement p in Placements)
        {
            if (IsDropped(dropped, p.BuildingKey)
                || AnyOverlaps(blocks, p.Occupied))
            {
                continue;
            }

            bool unreserve = p.ReservedFor != null
                && homeless.Contains(p.ReservedFor);
            placements.Add(unreserve ? p with { ReservedFor = null } : p);
        }

        var terrain = new List<TerrainPlacement>();
        foreach (TerrainPlacement t in Terrain)
        {
            if (!IsDropped(dropped, t.BuildingKey)
                && !AnyOverlaps(blocks, CellBox(t.At)))
            {
                terrain.Add(t);
            }
        }

        var roofs = new List<RoofPlacement>();
        foreach (RoofPlacement r in Roofs)
        {
            if (!IsDropped(dropped, r.BuildingKey)
                && !AnyOverlaps(blocks, CellBox(r.At)))
            {
                roofs.Add(r);
            }
        }

        var labels = new List<LabelPlan>();
        foreach (LabelPlan label in Labels)
        {
            Box at = CellBox(label.At);
            if (!AnyOverlaps(blocks, at) && !AnyOverlaps(droppedOuters, at))
            {
                labels.Add(label);
            }
        }

        var stockpiles = new List<StockpilePlan>();
        foreach (StockpilePlan pile in Stockpiles)
        {
            if (!AnyOverlaps(blocks, pile.Area)
                && !AnyOverlaps(droppedOuters, pile.Area))
            {
                stockpiles.Add(pile);
            }
        }

        Cell? reactor = null;
        if (Reactor is Cell anchor && placements.Exists(
            p => p.At == anchor && p.Def == AnyRimBuilder.ReactorDef))
        {
            reactor = anchor;
        }

        return new TownPlan
        {
            Name = Name,
            Quality = Quality,
            Seed = Seed,
            Bounds = Bounds,
            Core = Core,
            FirebreakWidth = FirebreakWidth,
            GroundZero = GroundZero,
            Reactor = reactor,
            WelcomeSign = WelcomeSign,
            Terrain = terrain,
            Roofs = roofs,
            Placements = placements,
            Buildings = buildings,
            Residents = residents,
            Labels = labels,
            Stockpiles = stockpiles,
            Parts = TownParts.From(terrain, placements, buildings, labels),
        };
    }

    /// <summary>Whether any of some boxes overlaps an area.</summary>
    /// <param name="boxes">The boxes.</param>
    /// <param name="area">The area.</param>
    /// <returns>True if at least one overlaps it.</returns>
    private static bool AnyOverlaps(List<Box> boxes, Box area) =>
        boxes.Exists(b => b.Overlaps(area));

    /// <summary>Whether a building key names a dropped building.</summary>
    /// <param name="dropped">The dropped buildings' keys.</param>
    /// <param name="key">The key, or null for none.</param>
    /// <returns>True if the key is set and dropped.</returns>
    private static bool IsDropped(HashSet<string> dropped, string? key) =>
        key != null && dropped.Contains(key);

    /// <summary>A one-cell box.</summary>
    /// <param name="c">The cell.</param>
    /// <returns>The box holding just that cell.</returns>
    private static Box CellBox(Cell c) => new(c.X, c.Z, c.X, c.Z);
}
