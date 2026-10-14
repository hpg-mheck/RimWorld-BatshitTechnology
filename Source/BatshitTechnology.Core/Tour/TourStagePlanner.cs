using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>
/// What the tour's stage planner is given: the map, the demo base, and
/// AnyRim if it was built. Every position is in map coordinates. The
/// candidate lists default to the tour's own and are open for tests.
/// </summary>
/// <param name="Map">The map's cells (0..size-1 on each axis).</param>
/// <param name="Base">The demo base's centre.</param>
public sealed record TourStageInput(Box Map, Cell Base)
{
    /// <summary>AnyRim as built, or null if it was skipped.</summary>
    public TownPlan? Town { get; init; }

    /// <summary>AnyRim's anchor (its crossroads), if built.</summary>
    public Cell TownAnchor { get; init; }

    /// <summary>
    /// Where the tractor yard may go, as offsets of its centre from the
    /// base, best first.
    /// </summary>
    public IReadOnlyList<Cell> YardOffsets { get; init; } =
        TourStagePlanner.DefaultYardOffsets;

    /// <summary>
    /// Where the self-destruct pads may go, as offsets from the base, in
    /// order of preference (the game ranks them by open ground).
    /// </summary>
    public IReadOnlyList<Cell> PadOffsets { get; init; } =
        TourStagePlanner.DefaultPadOffsets;

    /// <summary>
    /// More areas the yard must keep away from, beyond the base, the
    /// grenade clearing and AnyRim.
    /// </summary>
    public IReadOnlyList<KeepAway> ExtraYardKeepAways { get; init; } =
        Array.Empty<KeepAway>();
}

/// <summary>
/// Where the tour stages its dangerous scenes: the tractor yard and the
/// self-destruct pads, chosen together.
/// </summary>
/// <param name="Yard">The yard, or null if none was clear.</param>
/// <param name="Pads">The pads, best first; at most two.</param>
/// <param name="Note">
/// Why the plan is short (no yard, or fewer than two pads), or null.
/// </param>
public sealed record TourStagePlan(
    Box? Yard, IReadOnlyList<Cell> Pads, string? Note)
{
    /// <summary>The yard's centre, or null if there is no yard.</summary>
    public Cell? YardCentre => Yard is Box y
        ? new Cell((y.MinX + y.MaxX) / 2, (y.MinZ + y.MaxZ) / 2)
        : null;
}

/// <summary>
/// Chooses the tour's tractor yard and self-destruct pads. One rule
/// applies every keep-away (<see cref="Clears(Box, KeepAway)"/>), and
/// AnyRim is measured part by part with
/// <see cref="TownSite.Clears(TownPlan, Cell, Box, double)"/>, as its own
/// site search measured it. The yard keeps
/// <see cref="TractorReach"/> from the base square, the grenade clearing
/// and AnyRim; the pads keep <see cref="DemoCornerReach"/> from AnyRim and
/// from the yard, since a pad's blast and fires reach that far and the
/// barn stands in the yard from the start. The first yard that leaves two
/// pads wins; failing that, the yard that leaves the most.
/// </summary>
public static class TourStagePlanner
{
    /// <summary>
    /// The tiles a pad's self-destruct can reach, counting its fires and
    /// secondary blasts (the replicator's 83.6, the larger of the two),
    /// plus a 4-tile margin. docs/SHOWCASE.md, "Safety Rules".
    /// </summary>
    public const double DemoCornerReach = 87.6;

    /// <summary>
    /// The tour's tractors' pull: Popular, one up from the default
    /// (the operator, 2026-10-14: "bump up to the next setting").
    /// </summary>
    public const TractorBombPull TourPull = TractorBombPull.Popular;

    /// <summary>The tour's tractors' blast: Block Party, one up from the default.</summary>
    public const TractorBombBlast TourBlast = TractorBombBlast.BlockParty;

    /// <summary>
    /// How far the yard keeps from the base, the grenade clearing and
    /// AnyRim, in whole tiles: as far as anyone runs from one of the
    /// tour's tractors, its pull plus 8 (Core's
    /// <see cref="TractorBombFlee.FleeDistance"/>), rounded up: 31.
    /// </summary>
    public static readonly int TractorReach = (int)Math.Ceiling(
        TractorBombFlee.FleeDistance(
            TractorBombYield.Radius(TourPull),
            TractorBombYield.Radius(TourBlast)));

    /// <summary>The yard's half-width, east-west, in tiles.</summary>
    public const int YardHalfWidth = 32;

    /// <summary>The yard's half-height, north-south, in tiles.</summary>
    public const int YardHalfHeight = 14;

    /// <summary>The demo base's cleared square's half-size, in tiles.</summary>
    public const int BaseHalfSize = 16;

    /// <summary>The grenade clearing's half-size, in tiles.</summary>
    public const int GrenadeHalfSize = 13;

    /// <summary>A pad's cleared square's half-size, in tiles.</summary>
    public const int PadHalfSize = 3;

    /// <summary>The most pads the tour uses.</summary>
    public const int MaxPads = 2;

    /// <summary>The grenade clearing's centre, from the base.</summary>
    public static readonly Cell GrenadeOffset = new(-25, 0);

    /// <summary>
    /// The yard's candidate centres, from the base, best first: south-west,
    /// south and south-east, away from AnyRim in the north-west, and far
    /// enough south that the yard keeps <see cref="TractorReach"/> from the
    /// base square (its top edge 32 tiles below the square's).
    /// </summary>
    public static readonly IReadOnlyList<Cell> DefaultYardOffsets =
        new[] { new Cell(-45, -62), new Cell(0, -62), new Cell(45, -62) };

    /// <summary>
    /// The pads' candidate cells, from the base: the four far corners,
    /// 75 tiles out on both axes.
    /// </summary>
    public static readonly IReadOnlyList<Cell> DefaultPadOffsets =
        new[]
        {
            new Cell(-75, 75), new Cell(75, 75),
            new Cell(-75, -75), new Cell(75, -75),
        };

    /// <summary>
    /// The one keep-away rule: an area clears a keep-away when the gap
    /// between them is at least its distance.
    /// </summary>
    /// <param name="area">The area being placed.</param>
    /// <param name="keepAway">What it must stay clear of.</param>
    /// <returns>True if it is far enough away.</returns>
    public static bool Clears(Box area, KeepAway keepAway) =>
        area.DistanceTo(keepAway.Area) >= keepAway.MinDistance;

    /// <summary>The yard centred on a cell.</summary>
    /// <param name="centre">Its centre.</param>
    /// <returns>The yard's box.</returns>
    public static Box YardAt(Cell centre) =>
        new(centre.X - YardHalfWidth, centre.Z - YardHalfHeight,
            centre.X + YardHalfWidth, centre.Z + YardHalfHeight);

    /// <summary>A pad's cleared square, centred on a cell.</summary>
    /// <param name="centre">The pad's device.</param>
    /// <returns>The square.</returns>
    public static Box PadAt(Cell centre) =>
        new(centre.X - PadHalfSize, centre.Z - PadHalfSize,
            centre.X + PadHalfSize, centre.Z + PadHalfSize);

    /// <summary>
    /// What the yard keeps away from, beyond AnyRim: the base square and
    /// the grenade clearing at <see cref="TractorReach"/>, and any extra.
    /// </summary>
    /// <param name="input">The map and base.</param>
    /// <returns>The keep-aways.</returns>
    public static IReadOnlyList<KeepAway> YardKeepAways(TourStageInput input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        Cell grenade = input.Base + GrenadeOffset;
        return new[]
            {
                new KeepAway(Square(input.Base, BaseHalfSize), TractorReach),
                new KeepAway(Square(grenade, GrenadeHalfSize), TractorReach),
            }
            .Concat(input.ExtraYardKeepAways ?? Array.Empty<KeepAway>())
            .ToList();
    }

    /// <summary>Plans the yard and the pads.</summary>
    /// <param name="input">The map, base and AnyRim.</param>
    /// <returns>The plan; never null.</returns>
    /// <exception cref="ArgumentNullException">No input.</exception>
    public static TourStagePlan Plan(TourStageInput input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        IReadOnlyList<KeepAway> keepAways = YardKeepAways(input);
        List<Box> yards = (input.YardOffsets ?? Array.Empty<Cell>())
            .Select(o => YardAt(input.Base + o))
            .Where(y => input.Map.Contains(y)
                && keepAways.All(k => Clears(y, k))
                && ClearsTown(input, y, TractorReach))
            .ToList();
        if (yards.Count == 0)
        {
            List<Cell> lone = Pads(input, null);
            return new TourStagePlan(null, lone,
                "no yard clears the base, the grenade clearing and AnyRim"
                + PadNote(lone.Count, false));
        }

        Box best = yards[0];
        List<Cell> bestPads = Pads(input, best);
        foreach (Box yard in yards.Skip(1))
        {
            if (bestPads.Count >= MaxPads)
            {
                break;
            }

            List<Cell> pads = Pads(input, yard);
            if (pads.Count > bestPads.Count)
            {
                best = yard;
                bestPads = pads;
            }
        }

        string note = PadNote(bestPads.Count, true);
        return new TourStagePlan(
            best, bestPads, note.Length == 0 ? null : note.TrimStart(';', ' '));
    }

    /// <summary>
    /// The pads that fit the map and clear AnyRim and the yard, in order of
    /// preference, at most <see cref="MaxPads"/>. The reach is measured
    /// from the device's own cell, where its blast and barrage start; the
    /// pad's cleared square must only fit the map.
    /// </summary>
    /// <param name="input">The map, base and AnyRim.</param>
    /// <param name="yard">The yard, or null.</param>
    /// <returns>The pads.</returns>
    private static List<Cell> Pads(TourStageInput input, Box? yard) =>
        (input.PadOffsets ?? Array.Empty<Cell>())
            .Select(o => input.Base + o)
            .Where(c => input.Map.Contains(PadAt(c))
                && ClearsTown(input, Point(c), DemoCornerReach)
                && (yard is not Box y
                    || Clears(Point(c), new KeepAway(y, DemoCornerReach))))
            .Take(MaxPads)
            .ToList();

    /// <summary>Why a plan has fewer than two pads, or empty.</summary>
    /// <param name="pads">How many pads it has.</param>
    /// <param name="withYard">Whether the yard was a keep-away.</param>
    /// <returns>The note, starting "; ", or empty.</returns>
    private static string PadNote(int pads, bool withYard)
    {
        string clear = withYard ? "AnyRim and the yard" : "AnyRim";
        return pads switch
        {
            0 => $"; no pad clears {clear}",
            1 => $"; only one pad clears {clear}",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Whether an area keeps a distance from every part of AnyRim; true
    /// when the town was not built.
    /// </summary>
    /// <param name="input">The input, with the town if any.</param>
    /// <param name="area">The area.</param>
    /// <param name="distance">The least allowed gap.</param>
    /// <returns>True if clear.</returns>
    private static bool ClearsTown(TourStageInput input, Box area, double distance) =>
        input.Town == null
        || TownSite.Clears(input.Town, input.TownAnchor, area, distance);

    /// <summary>A box of one cell.</summary>
    /// <param name="c">The cell.</param>
    /// <returns>The box.</returns>
    private static Box Point(Cell c) => new(c.X, c.Z, c.X, c.Z);

    /// <summary>A square centred on a cell.</summary>
    /// <param name="centre">The centre.</param>
    /// <param name="half">Half its size.</param>
    /// <returns>The square.</returns>
    private static Box Square(Cell centre, int half) =>
        new(centre.X - half, centre.Z - half, centre.X + half, centre.Z + half);
}
