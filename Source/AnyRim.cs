// Batshit Technology - AnyRim, the showcase's test town.
// The town is data (Core.TownPlan, local coordinates with Ground Zero at
// (0,0)); this file puts it on a map. It finds a site (a cost grid the game
// fills in, scored by Core.TownSite), levels the ground the town occupies
// (its parts grown by the firebreak, not its bounding box: fog lifted,
// pawns out, roofs off before rock, everything destroyable vanished with
// no leavings or blasts, pending roof collapses cleared, zones taken off,
// soft ground and water turned to soil), then executes the plan in order:
// terrain, roofs, buildings, crates, items, plants, animals, stockpiles,
// power, residents.
// Every failure goes into a build report, logged once as an error so the
// smoke test catches it. AnyRim.Demolish takes the town down again. Three
// dev-mode actions under "Mods" build and remove it in any game. See
// docs/SHOWCASE.md, "AnyRim".

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BatshitTechnology.Core;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// What one AnyRim build did: how much of each kind of thing it made,
    /// and every failure (a missing def, a failed spawn, a full cell, ...).
    /// Identical failures are counted rather than repeated.
    /// </summary>
    internal sealed class AnyRimBuildReport
    {
        /// <summary>Each distinct failure and how often it happened.</summary>
        private readonly Dictionary<string, int> failures =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>The distinct failures, in the order they first happened.</summary>
        private readonly List<string> order = new List<string>();

        /// <summary>Where the town's local (0,0) went, or Invalid if nowhere.</summary>
        internal IntVec3 Anchor { get; set; } = IntVec3.Invalid;

        /// <summary>Terrain cells laid.</summary>
        internal int TerrainCells { get; set; }

        /// <summary>
        /// Taper cells outside the levelled area left alone because they
        /// were water, rock or soft ground: the road just stops there.
        /// </summary>
        internal int TaperCellsSkipped { get; set; }

        /// <summary>Roof cells set.</summary>
        internal int RoofCells { get; set; }

        /// <summary>Buildings spawned.</summary>
        internal int Buildings { get; set; }

        /// <summary>Crated (minified) buildings spawned.</summary>
        internal int Crates { get; set; }

        /// <summary>Item stacks spawned.</summary>
        internal int Items { get; set; }

        /// <summary>Plants spawned.</summary>
        internal int Plants { get; set; }

        /// <summary>Animals spawned.</summary>
        internal int Animals { get; set; }

        /// <summary>Stockpile cells zoned.</summary>
        internal int StockpileCells { get; set; }

        /// <summary>Residents spawned.</summary>
        internal int Residents { get; set; }

        /// <summary>Household lords made.</summary>
        internal int Lords { get; set; }

        /// <summary>Cold-weather placements (heaters) left out in warm weather.</summary>
        internal int SkippedWarm { get; set; }

        /// <summary>Beds and graves given to their residents.</summary>
        internal int Reserved { get; set; }

        /// <summary>Beds and graves the game would not give (not failures).</summary>
        internal int ReservationsRefused { get; set; }

        /// <summary>Indestructible things the plan was reduced around.</summary>
        internal int ObstaclesAvoided { get; set; }

        /// <summary>
        /// The plan the build executed: the plan asked for, reduced around
        /// indestructible things; null if nothing was built.
        /// </summary>
        internal TownPlan Plan { get; set; }

        /// <summary>Distinct failures, with a count when repeated.</summary>
        internal IReadOnlyList<string> Failures =>
            order.Select(f => failures[f] > 1 ? $"{f} (x{failures[f]})" : f).ToList();

        /// <summary>Whether nothing failed.</summary>
        internal bool Clean => order.Count == 0;

        /// <summary>Records a failure.</summary>
        /// <param name="what">What went wrong, in a few words.</param>
        internal void Fail(string what)
        {
            if (failures.TryGetValue(what, out int count))
            {
                failures[what] = count + 1;
                return;
            }

            failures[what] = 1;
            order.Add(what);
        }

        /// <summary>The counts, on one line.</summary>
        /// <returns>The summary.</returns>
        internal string Summary() =>
            $"terrain {TerrainCells} (taper cells skipped {TaperCellsSkipped}), roofs {RoofCells}, "
            + $"buildings {Buildings}, crates {Crates}, items {Items}, plants {Plants}, "
            + $"animals {Animals}, stockpile cells {StockpileCells}, residents {Residents} "
            + $"in {Lords} households, reserved {Reserved} (refused {ReservationsRefused}), "
            + $"warm-weather skips {SkippedWarm}, obstacles avoided {ObstaclesAvoided}, "
            + $"failures {order.Count}";

        /// <summary>
        /// Logs every failure as one error (so the debug log and the smoke
        /// test see it once), or nothing if the build was clean.
        /// </summary>
        internal void LogFailures()
        {
            if (Clean)
            {
                return;
            }

            var text = new StringBuilder();
            text.Append($"[BT] AnyRim build had {order.Count} problem(s):");
            foreach (string failure in Failures.Take(60))
            {
                text.Append("\n  ").Append(failure);
            }

            if (order.Count > 60)
            {
                text.Append($"\n  ...and {order.Count - 60} more");
            }

            Log.Error(text.ToString());
        }
    }

    /// <summary>Finds a site for AnyRim, builds it, and takes it down.</summary>
    internal static class AnyRim
    {
        /// <summary>
        /// The seed the showcase, smoke test and dev actions use, so every
        /// run builds the same town (Operation Doorstep's year).
        /// </summary>
        internal const int DefaultSeed = 1953;

        /// <summary>Spacing of candidate crossroads, in cells.</summary>
        internal const int SiteGridStep = 2;

        /// <summary>Cells the whole town keeps clear of the map edge.</summary>
        internal const int EdgeMargin = 6;

        /// <summary>Site score added per tile from the preferred spot.</summary>
        internal const int PenaltyPerTile = 10;

        /// <summary>Cold-weather placements (heaters) are built below this, in °C.</summary>
        internal const float ColdBelowCelsius = 5f;

        /// <summary>No lava within this many tiles of the town's core.</summary>
        private const int LavaReach = 6;

        /// <summary>No hostile pawn within this many tiles of the core.</summary>
        private const int HostilePawnReach = 15;

        /// <summary>No hostile building within this many tiles of the core.</summary>
        private const int HostileBuildingReach = 35;

        /// <summary>No ancient vent within this many tiles of the core.</summary>
        private const int VentReach = 20;

        /// <summary>Levelling cost of deep or chest-deep water.</summary>
        private const int CostDeep = 6;

        /// <summary>Levelling cost of shallow water, marsh and mud.</summary>
        private const int CostShallow = 3;

        /// <summary>Levelling cost of other ground too soft for stone walls.</summary>
        private const int CostSoft = 2;

        /// <summary>Levelling cost of natural rock.</summary>
        private const int CostRock = 4;

        /// <summary>Levelling cost of a thick rock roof.</summary>
        private const int CostThickRoof = 2;

        /// <summary>Levelling cost of any other roof.</summary>
        private const int CostThinRoof = 1;

        /// <summary>Levelling cost of any other building (ruins).</summary>
        private const int CostRuin = 1;

        /// <summary>
        /// CompFlickable's private "wanted" switch state. Setting
        /// SwitchIsOn alone leaves the player's toggle showing "on" for a
        /// device that is off; RimWorld has no public way to spawn a device
        /// switched off.
        /// </summary>
        private static readonly FieldInfo WantSwitchOnField =
            typeof(CompFlickable).GetField("wantSwitchOn", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>The rock-chunk recipe the Transmogrifier gets, suspended.</summary>
        private const string RockChunkRecipe = "BT_TransmogrifyRockChunks";

        /// <summary>
        /// How far the town reactor's self-destruct reaches: its 74.6-tile
        /// barrage (docs/SHOWCASE.md, "Safety Rules") plus a 4-tile margin.
        /// "Build AnyRim (best site)" keeps the reactor this far from the
        /// colony's buildings.
        /// </summary>
        internal const double ReactorReach = 78.6;

        /// <summary>
        /// How far from a cleared cell a pawn being moved out may be set
        /// down; the cleared ground is never thicker than this.
        /// </summary>
        private const int MoveOutReach = 120;

        /// <summary>AnyRim's plan at the Medium tier.</summary>
        /// <param name="seed">The seed for the taper wander and gaps.</param>
        /// <returns>The plan.</returns>
        internal static TownPlan Plan(int seed) =>
            TownPlan.AnyRim(seed, TownPalette.For(TownQuality.Medium));

        // ===== Coordinates =====

        /// <summary>A town-local cell on the map.</summary>
        /// <param name="anchor">The map cell of the town's local (0,0).</param>
        /// <param name="local">The local cell.</param>
        /// <returns>The map cell.</returns>
        internal static IntVec3 ToMap(IntVec3 anchor, Cell local) =>
            new IntVec3(anchor.x + local.X, 0, anchor.z + local.Z);

        /// <summary>A town-local box on the map.</summary>
        /// <param name="anchor">The map cell of the town's local (0,0).</param>
        /// <param name="local">The local box.</param>
        /// <returns>The map rectangle.</returns>
        internal static CellRect ToMap(IntVec3 anchor, Box local) =>
            CellRect.FromLimits(
                anchor.x + local.MinX, anchor.z + local.MinZ,
                anchor.x + local.MaxX, anchor.z + local.MaxZ);

        /// <summary>A map rectangle as a Core box.</summary>
        /// <param name="rect">The rectangle.</param>
        /// <returns>The box, in map coordinates.</returns>
        internal static Box ToBox(CellRect rect) => new Box(rect.minX, rect.minZ, rect.maxX, rect.maxZ);

        /// <summary>A map cell as a Core cell.</summary>
        /// <param name="c">The map cell.</param>
        /// <returns>The cell, in map coordinates.</returns>
        internal static Cell ToCell(IntVec3 c) => new Cell(c.x, c.z);

        /// <summary>The levelled area: the core and its firebreak, town-local.</summary>
        /// <param name="plan">The plan.</param>
        /// <returns>The box.</returns>
        internal static Box LevelledArea(TownPlan plan) => plan.LevelledArea;

        /// <summary>
        /// The ground a build of the plan at an anchor clears, levels and
        /// later demolishes, on the map: <see cref="TownPlan.ClearedCells"/>
        /// (the parts grown by the firebreak, within the levelled area),
        /// row by row, off-map cells left out.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="plan">The plan built (or to be built).</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <returns>The map cells.</returns>
        internal static List<IntVec3> ClearedCells(Map map, TownPlan plan, IntVec3 anchor) =>
            plan.ClearedCells()
                .Select(c => ToMap(anchor, c))
                .Where(c => c.InBounds(map))
                .ToList();

        /// <summary>Everything the town touches, firebreak included, town-local.</summary>
        /// <param name="plan">The plan.</param>
        /// <returns>The box.</returns>
        internal static Box Footprint(TownPlan plan) => plan.Bounds.Union(LevelledArea(plan));

        // ===== Site =====

        /// <summary>
        /// Fills a site grid for the whole map: levelling costs per cell
        /// (deep water 6, shallow water, marsh and mud 3, other soft ground
        /// 2, natural rock 4, thick roof 2, other roofs 1, other buildings
        /// 1) and no-go cells, marked so that a town with any part on one is
        /// rejected (TownSite.Best tests the plan's parts): lava within 6
        /// tiles of a part, hostile pawns within 15, hostile buildings
        /// within 35, ancient vents within 20 and, if asked, anything
        /// indestructible under a part.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="blockIndestructible">
        /// Whether indestructible things are no-go (site search) or left for
        /// the plan to be reduced around (building at a chosen cell).
        /// </param>
        /// <returns>The frozen grid.</returns>
        internal static SiteGrid ScanSite(Map map, bool blockIndestructible)
        {
            var grid = new SiteGrid(map.Size.x, map.Size.z);
            foreach (IntVec3 c in map.AllCells)
            {
                TerrainDef terrain = map.terrainGrid.BaseTerrainAt(c);
                int cost = TerrainCost(terrain);
                RoofDef roof = map.roofGrid.RoofAt(c);
                if (roof != null)
                {
                    cost += roof.isThickRoof ? CostThickRoof : CostThinRoof;
                }

                Building edifice = c.GetEdifice(map);
                if (edifice != null)
                {
                    cost += IsNaturalRock(edifice.def) ? CostRock : CostRuin;
                }

                if (cost > 0)
                {
                    grid.AddCost(c.x, c.z, cost);
                }

                if ((terrain != null && terrain.dangerous)
                    || map.terrainGrid.TerrainAt(c)?.dangerous == true)
                {
                    Block(grid, map, CellRect.SingleCell(c), LavaReach);
                }
            }

            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (!thing.Spawned)
                {
                    continue;
                }

                if (blockIndestructible && !thing.def.destroyable)
                {
                    Block(grid, map, thing.OccupiedRect(), 0);
                }

                if (thing is Building_AncientVent)
                {
                    Block(grid, map, thing.OccupiedRect(), VentReach);
                }
            }

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.HostileTo(Faction.OfPlayer))
                {
                    Block(grid, map, CellRect.SingleCell(pawn.Position), HostilePawnReach);
                }
            }

            foreach (Building building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.Faction != null && building.HostileTo(Faction.OfPlayer))
                {
                    Block(grid, map, building.OccupiedRect(), HostileBuildingReach);
                }
            }

            grid.Freeze();
            return grid;
        }

        /// <summary>
        /// The best crossroads for a town inside a window, or null if no
        /// candidate qualifies. Keep-aways are measured from the cells the
        /// town really touches (<see cref="TownPlan.Parts"/>), not its
        /// bounding box: AnyRim's box is mostly empty land, and with
        /// docs/SHOWCASE.md's window it always overlaps the grenade range.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="plan">The plan.</param>
        /// <param name="window">Where the crossroads may go, in map cells.</param>
        /// <param name="preferred">The preferred crossroads.</param>
        /// <param name="keepAways">Areas to keep away from, in map cells.</param>
        /// <param name="anchorAllowed">
        /// An extra rule a candidate crossroads (map cell) must pass before
        /// it is scored, or null for none.
        /// </param>
        /// <returns>The crossroads' map cell, or null.</returns>
        internal static IntVec3? FindSite(
            Map map, TownPlan plan, CellRect window, IntVec3 preferred,
            IReadOnlyList<KeepAway> keepAways, Predicate<IntVec3> anchorAllowed = null)
        {
            CellRect clipped = window.ClipInsideMap(map);
            if (clipped.Area <= 0)
            {
                return null;
            }

            // Indestructible things are not no-go ground: the town builds
            // around them. They only rule out a site that would lose a
            // building to them (TownSite.KeepsEveryBuilding).
            SiteGrid grid = ScanSite(map, blockIndestructible: false);
            List<Box> fixtures = map.listerThings.AllThings
                .Where(t => t.Spawned && !t.def.destroyable)
                .Select(t => ToBox(t.OccupiedRect()))
                .ToList();
            IReadOnlyList<KeepAway> rules = keepAways ?? Array.Empty<KeepAway>();
            List<Cell> candidates = TownSite.Grid(ToBox(clipped), SiteGridStep).ToList();
            int refused = 0;
            if (anchorAllowed != null)
            {
                int before = candidates.Count;
                candidates = candidates
                    .Where(c => anchorAllowed(new IntVec3(c.X, 0, c.Z)))
                    .ToList();
                refused = before - candidates.Count;
            }

            int wouldLoseBuilding = candidates.Count;
            candidates = candidates.Where(c => TownSite.KeepsEveryBuilding(plan, c, fixtures)).ToList();
            wouldLoseBuilding -= candidates.Count;

            Cell? best = TownSite.Best(
                grid, plan, candidates, ToCell(preferred), EdgeMargin, rules, PenaltyPerTile);
            if (!best.HasValue)
            {
                Log.Message($"[BT] AnyRim found no site in {clipped}: "
                    + (refused > 0 ? $"{refused} refused by the caller's rule first; " : string.Empty)
                    + (wouldLoseBuilding > 0
                        ? $"{wouldLoseBuilding} would lose a building to an indestructible thing; "
                        : string.Empty)
                    + TownSite.Explain(grid, plan, candidates, EdgeMargin, rules)
                    + "; " + DescribeNoGo(map, clipped.ExpandedBy(80)));
            }

            return best.HasValue ? new IntVec3(best.Value.X, 0, best.Value.Z) : (IntVec3?)null;
        }

        /// <summary>
        /// What makes ground no-go near a window, for the log line when no
        /// site qualifies: the defs of hostile pawns, hostile buildings,
        /// indestructible things and ancient vents within it, with counts.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="area">Where to look.</param>
        /// <returns>A short description.</returns>
        private static string DescribeNoGo(Map map, CellRect area)
        {
            area = area.ClipInsideMap(map);
            var found = new List<string>();
            found.AddRange(map.mapPawns.AllPawnsSpawned
                .Where(p => area.Contains(p.Position) && p.HostileTo(Faction.OfPlayer))
                .Select(p => $"hostile pawn {p.def.defName} ({p.Faction?.Name ?? "no faction"})"));
            found.AddRange(map.listerBuildings.allBuildingsNonColonist
                .Where(b => area.Contains(b.Position) && b.Faction != null
                    && b.HostileTo(Faction.OfPlayer))
                .Select(b => $"hostile building {b.def.defName} ({b.Faction.Name})"));
            found.AddRange(map.listerThings.AllThings
                .Where(t => t.Spawned && area.Contains(t.Position)
                    && (!t.def.destroyable || t is Building_AncientVent))
                .Select(t => $"no-go thing {t.def.defName}"));
            return found.Count == 0
                ? "nothing no-go found nearby"
                : string.Join(", ", found.GroupBy(f => f).Select(g => $"{g.Key} x{g.Count()}"));
        }

        /// <summary>
        /// Finds the best site in a window and builds AnyRim there.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="seed">The plan's seed.</param>
        /// <param name="window">Where the crossroads may go, in map cells.</param>
        /// <param name="preferred">The preferred crossroads.</param>
        /// <param name="keepAways">Areas to keep away from, in map cells.</param>
        /// <param name="anchorAllowed">
        /// An extra rule a candidate crossroads (map cell) must pass, or
        /// null for none.
        /// </param>
        /// <returns>
        /// The build report; its <see cref="AnyRimBuildReport.Anchor"/> is
        /// Invalid if no site qualified (and nothing was built).
        /// </returns>
        internal static AnyRimBuildReport BuildBest(
            Map map, int seed, CellRect window, IntVec3 preferred, IReadOnlyList<KeepAway> keepAways,
            Predicate<IntVec3> anchorAllowed = null)
        {
            TownPlan plan = Plan(seed);
            IntVec3? site = FindSite(map, plan, window, preferred, keepAways, anchorAllowed);
            return site.HasValue ? Build(map, plan, site.Value) : new AnyRimBuildReport();
        }

        /// <summary>
        /// Builds a town with its crossroads at a map cell: reduces the plan
        /// around indestructible things, levels the ground it occupies,
        /// executes the plan, and registers the town (with the obstacles,
        /// so the executed plan can be rebuilt after a load) with the map.
        /// One town per map.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="plan">The plan.</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <returns>The build report, already logged.</returns>
        internal static AnyRimBuildReport Build(Map map, TownPlan plan, IntVec3 anchor)
        {
            var report = new AnyRimBuildReport { Anchor = anchor };
            MapComponent_AnyRim record = map.GetComponent<MapComponent_AnyRim>();
            if (record == null)
            {
                report.Fail("the map has no MapComponent_AnyRim");
            }
            else if (record.Built)
            {
                report.Fail("this map already has a town; remove it first");
            }
            else
            {
                List<Box> obstacles = FindObstacles(map, plan, anchor);
                if (obstacles.Count > 0)
                {
                    plan = plan.Avoiding(obstacles);
                    report.ObstaclesAvoided = obstacles.Count;
                }

                report.Plan = plan;
                List<IntVec3> cleared = ClearedCells(map, plan, anchor);
                PrepareGround(map, cleared);
                Execute(map, plan, anchor, cleared, obstacles, record, report);
                Log.Message($"[BT] AnyRim built at ({anchor.x},{anchor.z}): {report.Summary()}");
            }

            report.LogFailures();
            return report;
        }

        /// <summary>
        /// Indestructible things (steam geysers, craters, ancient vents)
        /// anywhere the town would touch, as town-local boxes.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="plan">The plan.</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <returns>The obstacles.</returns>
        private static List<Box> FindObstacles(Map map, TownPlan plan, IntVec3 anchor)
        {
            CellRect area = ToMap(anchor, Footprint(plan)).ClipInsideMap(map);
            var found = new HashSet<Thing>();
            foreach (IntVec3 c in area)
            {
                foreach (Thing thing in c.GetThingList(map))
                {
                    if (!thing.def.destroyable)
                    {
                        found.Add(thing);
                    }
                }
            }

            return found
                .Select(t => t.OccupiedRect())
                .Select(r => new Box(r.minX - anchor.x, r.minZ - anchor.z, r.maxX - anchor.x, r.maxZ - anchor.z))
                .Distinct()
                .ToList();
        }

        // ===== Ground work =====

        /// <summary>
        /// Levels the ground the town occupies (its cleared cells: the parts
        /// grown by the firebreak, within the levelled area; never the empty
        /// land inside its bounding box), in docs/SHOWCASE.md's order: fog
        /// off first (so breaking into a fogged cave sends no "area
        /// revealed" letter), pawns out, roofs off before rock, everything
        /// destroyable vanished (no leavings, explosions or death blasts),
        /// pending roof collapses cleared, zones taken off, then the ground
        /// made fit for stone walls. The firebreak keeps its natural ground
        /// but loses its plants with everything else.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="cleared">The cleared cells, on the map.</param>
        private static void PrepareGround(Map map, List<IntVec3> cleared)
        {
            var area = new HashSet<IntVec3>(cleared);
            foreach (IntVec3 c in cleared)
            {
                foreach (IntVec3 offset in GenAdj.AdjacentCellsAndInside)
                {
                    IntVec3 near = c + offset;
                    if (near.InBounds(map) && map.fogGrid.IsFogged(near))
                    {
                        map.fogGrid.Unfog(near);
                    }
                }
            }

            MovePawnsOut(map, area);
            foreach (IntVec3 c in cleared)
            {
                map.roofGrid.SetRoof(c, null);
            }

            var alreadyMarked = new HashSet<IntVec3>(map.roofCollapseBuffer.CellsMarkedToCollapse);
            var holders = new List<IntVec3>();
            foreach (IntVec3 c in cleared)
            {
                foreach (Thing thing in c.GetThingList(map).ToList())
                {
                    if (thing.Destroyed || thing is Pawn || !thing.def.destroyable)
                    {
                        continue;
                    }

                    if (thing.def.holdsRoof)
                    {
                        holders.Add(thing.Position);
                    }

                    thing.Destroy(DestroyMode.Vanish);
                }
            }

            ClearRoofCollapses(map, alreadyMarked, holders, area);
            Unzone(map, area);
            foreach (IntVec3 c in cleared)
            {
                Level(map, c);
            }
        }

        /// <summary>
        /// Takes every zone (the player's stockpiles and growing zones
        /// included) off a set of cells, so nothing sows the town's lawns
        /// or hauls into its houses. Cells outside the set keep their
        /// zones, even where a zone is cut in two.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="area">The cells.</param>
        private static void Unzone(Map map, HashSet<IntVec3> area)
        {
            foreach (Zone zone in map.zoneManager.AllZones.ToList())
            {
                foreach (IntVec3 c in zone.Cells.Where(area.Contains).ToList())
                {
                    zone.RemoveCell(c);
                }
            }
        }

        /// <summary>
        /// Moves every pawn out of an area: hostile ones vanish, everyone
        /// else is set down on the nearest standable cell outside it. A
        /// spawned wall never wipes a pawn, so anyone left inside would be
        /// walled in.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="area">The area, map cells.</param>
        private static void MovePawnsOut(Map map, HashSet<IntVec3> area)
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (!area.Contains(pawn.Position))
                {
                    continue;
                }

                if (pawn.HostileTo(Faction.OfPlayer))
                {
                    pawn.Destroy(DestroyMode.Vanish);
                    continue;
                }

                if (!TryFindNearestOutside(pawn.Position, area, map, out IntVec3 cell))
                {
                    continue; // nowhere to go; it will step out of any wall itself
                }

                pawn.jobs?.StopAll();
                pawn.Position = cell;
                pawn.Notify_Teleported();
            }
        }

        /// <summary>
        /// The nearest standable cell outside an area, searched in growing
        /// square rings around a cell, up to <see cref="MoveOutReach"/>.
        /// </summary>
        /// <param name="from">A cell inside the area.</param>
        /// <param name="area">The area.</param>
        /// <param name="map">The map.</param>
        /// <param name="cell">The cell found, or Invalid.</param>
        /// <returns>True if one was found.</returns>
        private static bool TryFindNearestOutside(
            IntVec3 from, HashSet<IntVec3> area, Map map, out IntVec3 cell)
        {
            for (int r = 1; r <= MoveOutReach; r++)
            {
                IntVec3 best = IntVec3.Invalid;
                float bestDistance = float.MaxValue;
                foreach (IntVec3 c in CellRect.CenteredOn(from, r).EdgeCells)
                {
                    if (!c.InBounds(map) || area.Contains(c) || !c.Standable(map))
                    {
                        continue;
                    }

                    float distance = c.DistanceToSquared(from);
                    if (distance < bestDistance)
                    {
                        best = c;
                        bestDistance = distance;
                    }
                }

                if (best.IsValid)
                {
                    cell = best;
                    return true;
                }
            }

            cell = IntVec3.Invalid;
            return false;
        }

        /// <summary>
        /// Clears what removing rock and walls marked to collapse. Removing
        /// a roof holder during play marks unsupported roof nearby, and the
        /// marks drop (crushing whatever is under them) on the next tick.
        /// New marks are unroofed instead, then the removal-mode sweep
        /// vanilla's river generation uses takes off any roof left
        /// unsupported. Marks that were there before this build are left
        /// for the game to resolve, except inside the cleared cells: those
        /// cells were just unroofed, and a mark left there would drop the
        /// roof the town is about to put on them.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="alreadyMarked">Cells marked before anything was removed.</param>
        /// <param name="holders">Where removed roof holders stood.</param>
        /// <param name="area">The cleared cells.</param>
        private static void ClearRoofCollapses(
            Map map, HashSet<IntVec3> alreadyMarked, List<IntVec3> holders, HashSet<IntVec3> area)
        {
            List<IntVec3> marked = map.roofCollapseBuffer.CellsMarkedToCollapse;
            for (int i = marked.Count - 1; i >= 0; i--)
            {
                IntVec3 c = marked[i];
                if (alreadyMarked.Contains(c) && !area.Contains(c))
                {
                    continue;
                }

                map.roofGrid.SetRoof(c, null);
                marked.RemoveAt(i);
            }

            if (holders.Count > 0)
            {
                RoofCollapseCellsFinder.RemoveBulkCollapsingRoofs(holders, map);
            }
        }

        /// <summary>
        /// Makes one cell fit for building: temporary terrain (floods, thin
        /// ice), foundations and laid floors come up, and ground that is
        /// water, dangerous, impassable or too soft for stone walls becomes
        /// soil. Other natural ground stays, so each run gives a desert,
        /// tundra or forest AnyRim. Pollution is cleaned.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="c">The cell.</param>
        private static void Level(Map map, IntVec3 c)
        {
            TerrainGrid terrain = map.terrainGrid;
            if (terrain.TempTerrainAt(c) != null)
            {
                terrain.RemoveTempTerrain(c, doLeavings: false, preventDestroyEffects: true);
            }

            if (terrain.FoundationAt(c) != null)
            {
                terrain.RemoveFoundation(c, doLeavings: false);
            }

            if (terrain.UnderTerrainAt(c) != null)
            {
                terrain.RemoveTopLayer(c, doLeavings: false);
            }

            if (NeedsLevelling(terrain.TerrainAt(c)))
            {
                terrain.SetTerrain(c, TerrainDefOf.Soil);
            }

            if (ModsConfig.BiotechActive && map.pollutionGrid != null && map.pollutionGrid.IsPolluted(c))
            {
                map.pollutionGrid.SetPolluted(c, false, silent: true);
            }
        }

        /// <summary>Whether ground must become soil before a town stands on it.</summary>
        /// <param name="def">The terrain.</param>
        /// <returns>True for water, lava, impassable or soft ground.</returns>
        private static bool NeedsLevelling(TerrainDef def) =>
            def == null || def.IsWater || def.dangerous || def.passability != Traversability.Standable
            || !def.affordances.Contains(TerrainAffordanceDefOf.Heavy);

        /// <summary>A cell's levelling cost from its terrain.</summary>
        /// <param name="def">The base terrain.</param>
        /// <returns>6, 3, 2 or 0.</returns>
        private static int TerrainCost(TerrainDef def)
        {
            if (def == null)
            {
                return 0;
            }

            if (def.passability == Traversability.Impassable)
            {
                return CostDeep;
            }

            if (def.IsWater)
            {
                TerrainAffordanceDef shallow =
                    DefDatabase<TerrainAffordanceDef>.GetNamedSilentFail("ShallowWater");
                return shallow != null && def.affordances.Contains(shallow) ? CostShallow : CostDeep;
            }

            if (def.HasTag("Mud"))
            {
                return CostShallow;
            }

            return def.affordances.Contains(TerrainAffordanceDefOf.Heavy) ? 0 : CostSoft;
        }

        /// <summary>Whether a def is natural rock (a mineable wall).</summary>
        /// <param name="def">The def.</param>
        /// <returns>True for natural rock.</returns>
        private static bool IsNaturalRock(ThingDef def) =>
            def.mineable || (def.building != null && def.building.isNaturalRock);

        /// <summary>Marks a rectangle, grown by a radius, no-go.</summary>
        /// <param name="grid">The grid, not yet frozen.</param>
        /// <param name="map">The map, to clip to.</param>
        /// <param name="rect">The rectangle.</param>
        /// <param name="radius">Cells to grow it by; negative counts as 0.</param>
        private static void Block(SiteGrid grid, Map map, CellRect rect, int radius)
        {
            foreach (IntVec3 c in rect.ExpandedBy(Mathf.Max(0, radius)).ClipInsideMap(map))
            {
                grid.Block(c.x, c.z);
            }
        }

        // ===== Building =====

        /// <summary>
        /// Executes a plan on levelled ground: terrain, roofs, buildings,
        /// crates, items, plants, animals and stockpiles; then power, rooms
        /// and fog; then the residents and their beds and graves; then the
        /// home area is cleared over what the town occupies (its cleared
        /// cells and the road-taper cells laid) and the town is registered.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="plan">The plan, already reduced around obstacles.</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <param name="cleared">The cleared cells, on the map.</param>
        /// <param name="obstacles">What the plan was reduced around, town-local.</param>
        /// <param name="record">The map's town record.</param>
        /// <param name="report">Collects counts and failures.</param>
        private static void Execute(
            Map map, TownPlan plan, IntVec3 anchor, List<IntVec3> cleared, List<Box> obstacles,
            MapComponent_AnyRim record, AnyRimBuildReport report)
        {
            Faction town = AnyRimResidents.TownFaction();
            if (town == null)
            {
                report.Fail("no Ancients faction: town things are unowned");
            }

            List<IntVec3> tapers = LayTerrain(map, plan, anchor, report);
            foreach (RoofPlacement roof in plan.Roofs)
            {
                IntVec3 c = ToMap(anchor, roof.At);
                if (!c.InBounds(map))
                {
                    report.Fail($"roof off the map at {roof.At}");
                    continue;
                }

                map.roofGrid.SetRoof(
                    c, roof.Kind == RoofKind.RockThick ? RoofDefOf.RoofRockThick : RoofDefOf.RoofConstructed);
                report.RoofCells++;
            }

            var reservations = new List<(Thing Thing, string Key)>();
            var animals = new List<Pawn>();
            bool cold = map.mapTemperature.OutdoorTemp < ColdBelowCelsius;
            foreach (PlacementKind kind in new[]
            {
                PlacementKind.Building, PlacementKind.Crate, PlacementKind.Item, PlacementKind.Plant,
                PlacementKind.Animal,
            })
            {
                foreach (Placement placement in plan.Placements.Where(p => p.Kind == kind))
                {
                    if (placement.OnlyIfCold && !cold)
                    {
                        report.SkippedWarm++;
                        continue;
                    }

                    try
                    {
                        Thing made = Place(map, anchor, placement, town, report);
                        if (made is Pawn animal)
                        {
                            animals.Add(animal);
                        }
                        else if (made != null && placement.ReservedFor != null)
                        {
                            reservations.Add((made, placement.ReservedFor));
                        }
                    }
                    catch (Exception e)
                    {
                        report.Fail($"{Describe(placement)}: threw {e.GetType().Name}: {e.Message}");
                    }
                }
            }

            foreach (StockpilePlan stockpile in plan.Stockpiles)
            {
                Zone(map, anchor, stockpile, report);
            }

            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            List<IntVec3> occupied = cleared.Concat(tapers).ToList();
            foreach (IntVec3 c in occupied)
            {
                if (map.fogGrid.IsFogged(c))
                {
                    map.fogGrid.Unfog(c);
                }
            }

            Dictionary<string, Pawn> residents = AnyRimResidents.SpawnAll(map, anchor, plan, report);
            foreach ((Thing thing, string key) in reservations)
            {
                if (residents.TryGetValue(key, out Pawn owner))
                {
                    AnyRimResidents.Reserve(thing, owner, report);
                }
                else
                {
                    report.ReservationsRefused++;
                }
            }

            Area home = map.areaManager.Home;
            if (home != null)
            {
                foreach (IntVec3 c in occupied)
                {
                    home[c] = false;
                }
            }

            record.Register(
                anchor, ToMap(anchor, Footprint(plan)).ClipInsideMap(map),
                ToMap(anchor, LevelledArea(plan)).ClipInsideMap(map), plan, obstacles,
                plan.Residents.Where(r => residents.ContainsKey(r.Key)).Select(r => residents[r.Key]),
                animals);
        }

        /// <summary>
        /// Lays the planned terrain (the last entry for a cell wins) and its
        /// paint. Cells outside the levelled area (the road tapers) are laid
        /// only on firm, open ground, after their plants and filth go;
        /// anywhere else the road just stops.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="plan">The plan.</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <param name="report">Collects counts and failures.</param>
        /// <returns>The cells laid outside the levelled area, on the map.</returns>
        private static List<IntVec3> LayTerrain(
            Map map, TownPlan plan, IntVec3 anchor, AnyRimBuildReport report)
        {
            var tapers = new List<IntVec3>();
            var final = new Dictionary<Cell, TerrainPlacement>();
            foreach (TerrainPlacement placement in plan.Terrain)
            {
                final[placement.At] = placement;
            }

            Box levelled = LevelledArea(plan);
            foreach (TerrainPlacement placement in final.Values)
            {
                IntVec3 c = ToMap(anchor, placement.At);
                if (!c.InBounds(map))
                {
                    report.Fail($"terrain off the map at {placement.At}");
                    continue;
                }

                TerrainDef def = DefDatabase<TerrainDef>.GetNamedSilentFail(placement.Def);
                if (def == null)
                {
                    report.Fail($"missing TerrainDef {placement.Def}");
                    continue;
                }

                if (!levelled.Contains(placement.At))
                {
                    if (!ClearTaperCell(map, c))
                    {
                        report.TaperCellsSkipped++;
                        continue;
                    }

                    tapers.Add(c);
                }

                map.terrainGrid.SetTerrain(c, def);
                if (placement.Paint != null)
                {
                    ColorDef paint = DefDatabase<ColorDef>.GetNamedSilentFail(placement.Paint);
                    if (paint == null)
                    {
                        report.Fail($"missing ColorDef {placement.Paint}");
                    }
                    else
                    {
                        map.terrainGrid.SetTerrainColor(c, paint);
                    }
                }

                report.TerrainCells++;
            }

            return tapers;
        }

        /// <summary>
        /// Readies a road-taper cell outside the levelled area: false (leave
        /// it) for water, soft ground, rock, ruins, temporary terrain or
        /// anything indestructible; otherwise its plants and filth go.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="c">The cell.</param>
        /// <returns>Whether the road may be laid there.</returns>
        private static bool ClearTaperCell(Map map, IntVec3 c)
        {
            if (map.terrainGrid.TempTerrainAt(c) != null || NeedsLevelling(map.terrainGrid.TerrainAt(c))
                || c.GetEdifice(map) != null)
            {
                return false;
            }

            List<Thing> things = c.GetThingList(map);
            if (things.Any(t => !t.def.destroyable))
            {
                return false;
            }

            foreach (Thing thing in things.ToList())
            {
                if (thing.def.category == ThingCategory.Plant || thing.def.category == ThingCategory.Filth)
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
            }

            return true;
        }

        /// <summary>Makes and spawns one placement.</summary>
        /// <param name="map">The map.</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <param name="placement">The placement.</param>
        /// <param name="town">The town's faction, or null.</param>
        /// <param name="report">Collects counts and failures.</param>
        /// <returns>The spawned thing (the crate, for crates), or null on failure.</returns>
        private static Thing Place(
            Map map, IntVec3 anchor, Placement placement, Faction town, AnyRimBuildReport report)
        {
            IntVec3 c = ToMap(anchor, placement.At);
            if (!c.InBounds(map))
            {
                report.Fail($"{Describe(placement)}: off the map");
                return null;
            }

            switch (placement.Kind)
            {
                case PlacementKind.Building:
                    return PlaceBuilding(map, c, placement, town, report);
                case PlacementKind.Crate:
                    return PlaceCrate(map, c, placement, town, report);
                case PlacementKind.Item:
                    return PlaceItem(map, c, placement, report);
                case PlacementKind.Plant:
                    return PlacePlant(map, c, placement, report);
                case PlacementKind.Animal:
                    return PlaceAnimal(map, c, placement, report);
                default:
                    report.Fail($"{Describe(placement)}: unknown kind");
                    return null;
            }
        }

        /// <summary>
        /// Spawns a building with its owner, stuff, facing, paint and
        /// quality, then applies its switches, fuel, bills and bolts.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="c">The anchor cell on the map.</param>
        /// <param name="placement">The placement.</param>
        /// <param name="town">The town's faction, or null.</param>
        /// <param name="report">Collects counts and failures.</param>
        /// <returns>The building, or null.</returns>
        private static Thing PlaceBuilding(
            Map map, IntVec3 c, Placement placement, Faction town, AnyRimBuildReport report)
        {
            ThingDef def = ThingDefFor(placement, report);
            if (def == null)
            {
                return null;
            }

            if (def.category != ThingCategory.Building)
            {
                report.Fail($"{Describe(placement)}: not a building");
                return null;
            }

            var rot = new Rot4((int)placement.Rot);
            CellRect occupied = GenAdj.OccupiedRect(c, rot, def.size);
            if (!occupied.InBounds(map))
            {
                report.Fail($"{Describe(placement)}: footprint off the map");
                return null;
            }

            if (occupied.Cells.Any(cell => cell.GetThingList(map).Any(t => !t.def.destroyable)))
            {
                report.Fail($"{Describe(placement)}: something indestructible in the way");
                return null;
            }

            Thing thing = MakeThing(def, placement, town, report);
            if (thing == null)
            {
                return null;
            }

            Thing spawned = GenSpawn.Spawn(thing, c, map, rot, WipeMode.Vanish);
            if (spawned == null || !spawned.Spawned)
            {
                report.Fail($"{Describe(placement)}: failed to spawn");
                return null;
            }

            Configure(spawned, placement, report);
            report.Buildings++;
            return spawned;
        }

        /// <summary>
        /// Spawns a building still in its crate (minified), forbidden if
        /// planned so. The crate never goes into a full cell.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="c">The cell.</param>
        /// <param name="placement">The placement.</param>
        /// <param name="town">The town's faction, or null.</param>
        /// <param name="report">Collects counts and failures.</param>
        /// <returns>The crate, or null.</returns>
        private static Thing PlaceCrate(
            Map map, IntVec3 c, Placement placement, Faction town, AnyRimBuildReport report)
        {
            ThingDef def = ThingDefFor(placement, report, checkSize: false);
            if (def == null)
            {
                return null;
            }

            if (!def.Minifiable)
            {
                report.Fail($"{Describe(placement)}: cannot be crated");
                return null;
            }

            if (!HasRoomForItem(map, c, placement, report))
            {
                return null;
            }

            Thing inner = MakeThing(def, placement, town, report);
            MinifiedThing crate = inner?.MakeMinified();
            if (crate == null)
            {
                report.Fail($"{Describe(placement)}: could not be crated");
                return null;
            }

            GenSpawn.Spawn(crate, c, map, WipeMode.Vanish);
            if (!crate.Spawned)
            {
                report.Fail($"{Describe(placement)}: crate failed to spawn");
                return null;
            }

            if (placement.Forbidden)
            {
                crate.SetForbidden(true, warnOnFail: false);
            }

            report.Crates++;
            return crate;
        }

        /// <summary>
        /// Spawns a stack of items (never into a full cell), forbidden if
        /// planned so.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="c">The cell.</param>
        /// <param name="placement">The placement.</param>
        /// <param name="report">Collects counts and failures.</param>
        /// <returns>The stack, or null.</returns>
        private static Thing PlaceItem(Map map, IntVec3 c, Placement placement, AnyRimBuildReport report)
        {
            ThingDef def = ThingDefFor(placement, report);
            if (def == null)
            {
                return null;
            }

            if (def.category != ThingCategory.Item)
            {
                report.Fail($"{Describe(placement)}: not an item");
                return null;
            }

            if (!HasRoomForItem(map, c, placement, report))
            {
                return null;
            }

            Thing item = MakeThing(def, placement, null, report);
            if (item == null)
            {
                return null;
            }

            if (placement.Count > def.stackLimit || placement.Count < 1)
            {
                report.Fail($"{Describe(placement)}: count {placement.Count} outside 1..{def.stackLimit}");
            }

            item.stackCount = Mathf.Clamp(placement.Count, 1, def.stackLimit);
            GenSpawn.Spawn(item, c, map, WipeMode.Vanish);
            if (!item.Spawned)
            {
                report.Fail($"{Describe(placement)}: failed to spawn");
                return null;
            }

            if (placement.Forbidden)
            {
                item.SetForbidden(true, warnOnFail: false);
            }

            report.Items++;
            return item;
        }

        /// <summary>Spawns a plant, its growth set before it spawns.</summary>
        /// <param name="map">The map.</param>
        /// <param name="c">The cell.</param>
        /// <param name="placement">The placement.</param>
        /// <param name="report">Collects counts and failures.</param>
        /// <returns>The plant, or null.</returns>
        private static Thing PlacePlant(Map map, IntVec3 c, Placement placement, AnyRimBuildReport report)
        {
            ThingDef def = ThingDefFor(placement, report);
            if (def == null)
            {
                return null;
            }

            if (!(ThingMaker.MakeThing(def) is Plant plant))
            {
                report.Fail($"{Describe(placement)}: not a plant");
                return null;
            }

            plant.Growth = Mathf.Clamp01(placement.Growth);
            GenSpawn.Spawn(plant, c, map, WipeMode.Vanish);
            if (!plant.Spawned)
            {
                report.Fail($"{Describe(placement)}: failed to spawn");
                return null;
            }

            report.Plants++;
            return plant;
        }

        /// <summary>
        /// Spawns an animal, named if planned. A town-owned animal would
        /// have no lord and walk off the map, so only player-owned ones get
        /// a faction; the rest are wild.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="c">The cell.</param>
        /// <param name="placement">The placement (Def is a PawnKindDef).</param>
        /// <param name="report">Collects counts and failures.</param>
        /// <returns>The animal, or null.</returns>
        private static Thing PlaceAnimal(Map map, IntVec3 c, Placement placement, AnyRimBuildReport report)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(placement.Def);
            if (kind?.RaceProps == null || !kind.RaceProps.Animal)
            {
                report.Fail($"{Describe(placement)}: missing animal PawnKindDef");
                return null;
            }

            Faction faction = placement.Owner == Owner.Player ? Faction.OfPlayer : null;
            Pawn animal = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind, faction, tile: map.Tile, forceGenerateNewPawn: true));
            if (!string.IsNullOrEmpty(placement.Name))
            {
                animal.Name = new NameSingle(placement.Name);
            }

            if (!c.Standable(map)
                && !CellFinder.TryFindRandomCellNear(c, map, 2, x => x.Standable(map), out c))
            {
                report.Fail($"{Describe(placement)}: nowhere to stand");
                animal.Destroy(DestroyMode.Vanish);
                return null;
            }

            GenSpawn.Spawn(animal, c, map, WipeMode.Vanish);
            if (!animal.Spawned)
            {
                report.Fail($"{Describe(placement)}: failed to spawn");
                return null;
            }

            report.Animals++;
            return animal;
        }

        /// <summary>
        /// Whether a cell can take one more item stack; a full cell is a
        /// failure (RimWorld would push the old stack aside).
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="c">The cell.</param>
        /// <param name="placement">The placement, for the report.</param>
        /// <param name="report">Collects failures.</param>
        /// <returns>True if there is room.</returns>
        private static bool HasRoomForItem(Map map, IntVec3 c, Placement placement, AnyRimBuildReport report)
        {
            if (c.GetItemCount(map) < c.GetMaxItemsAllowedInCell(map))
            {
                return true;
            }

            report.Fail($"{Describe(placement)}: cell already full");
            return false;
        }

        /// <summary>
        /// Looks a placement's ThingDef up and, unless told not to, checks
        /// its size against the plan's (which the plan's geometry relies
        /// on).
        /// </summary>
        /// <param name="placement">The placement.</param>
        /// <param name="report">Collects failures.</param>
        /// <param name="checkSize">
        /// False for crates, which stand in one cell whatever their
        /// contents' size.
        /// </param>
        /// <returns>The def, or null if missing.</returns>
        private static ThingDef ThingDefFor(
            Placement placement, AnyRimBuildReport report, bool checkSize = true)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(placement.Def);
            if (def == null)
            {
                report.Fail($"missing ThingDef {placement.Def}");
                return null;
            }

            if (checkSize && (def.size.x != placement.SizeX || def.size.z != placement.SizeZ))
            {
                report.Fail(
                    $"{placement.Def}: plan size {placement.SizeX}x{placement.SizeZ}, "
                    + $"def size {def.size.x}x{def.size.z}");
            }

            return def;
        }

        /// <summary>
        /// Makes an unspawned thing from a placement: stuff (checked with
        /// CanMake, falling back to the default), owner, quality and paint.
        /// </summary>
        /// <param name="def">The def.</param>
        /// <param name="placement">The placement.</param>
        /// <param name="town">The town's faction, or null.</param>
        /// <param name="report">Collects failures.</param>
        /// <returns>The thing, or null.</returns>
        private static Thing MakeThing(ThingDef def, Placement placement, Faction town, AnyRimBuildReport report)
        {
            ThingDef stuff = null;
            if (def.MadeFromStuff)
            {
                stuff = placement.Stuff == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(placement.Stuff);
                if (placement.Stuff != null && (stuff?.stuffProps == null || !stuff.stuffProps.CanMake(def)))
                {
                    report.Fail($"{placement.Def}: stuff {placement.Stuff} missing or unusable");
                    stuff = null;
                }

                stuff = stuff ?? GenStuff.DefaultStuffFor(def);
            }
            else if (placement.Stuff != null)
            {
                report.Fail($"{placement.Def}: takes no stuff, but {placement.Stuff} was planned");
            }

            Thing thing = ThingMaker.MakeThing(def, stuff);
            Faction owner = placement.Owner == Owner.Player ? Faction.OfPlayer
                : placement.Owner == Owner.Town ? town : null;
            if (owner != null && def.CanHaveFaction)
            {
                thing.SetFaction(owner);
            }

            if (placement.Quality != null)
            {
                if (!Enum.TryParse(placement.Quality, ignoreCase: true, out QualityCategory quality))
                {
                    report.Fail($"{placement.Def}: unknown quality {placement.Quality}");
                }
                else
                {
                    thing.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Outsider);
                }
            }

            if (placement.Paint != null)
            {
                ColorDef paint = DefDatabase<ColorDef>.GetNamedSilentFail(placement.Paint);
                if (paint == null || !(thing is Building building))
                {
                    report.Fail($"{placement.Def}: cannot be painted {placement.Paint}");
                }
                else
                {
                    building.ChangePaint(paint);
                }
            }

            return thing;
        }

        /// <summary>
        /// Applies a spawned building's settings: switched off, fuelled,
        /// auto-refuel off, bills suspended (the Transmogrifier first gets
        /// its rock-chunk bill), and bolts loaded into a Ballista or
        /// magazine, which then refuses any more.
        /// </summary>
        /// <param name="building">The spawned building.</param>
        /// <param name="placement">The placement.</param>
        /// <param name="report">Collects failures.</param>
        private static void Configure(Thing building, Placement placement, AnyRimBuildReport report)
        {
            if (placement.SwitchedOff)
            {
                SwitchOff(building, report);
            }

            if (placement.Refuel || placement.AutoRefuelOff)
            {
                CompRefuelable fuel = building.TryGetComp<CompRefuelable>();
                if (fuel == null)
                {
                    report.Fail($"{placement.Def}: takes no fuel");
                }
                else
                {
                    if (placement.Refuel)
                    {
                        Fill(fuel);
                    }

                    if (placement.AutoRefuelOff)
                    {
                        fuel.allowAutoRefuel = false;
                    }
                }
            }

            if (placement.SuspendBills)
            {
                SuspendBills(building, report);
            }

            if (placement.Load.Count > 0)
            {
                Load(building, placement, report);
            }

            RefuseDeliveries(building);
        }

        /// <summary>
        /// Sets a town Ballista's or magazine's storage filter to allow
        /// nothing, after it is loaded, so the colony's haulers never carry
        /// bolts to town (both are player-owned haul destinations of high
        /// priority). It still fires: BoltStorage.Take ignores the filter,
        /// and the bolts inside are not spawned, so nothing hauls them out.
        /// Anything else is left alone.
        /// </summary>
        /// <param name="building">The spawned building.</param>
        private static void RefuseDeliveries(Thing building)
        {
            BoltStorage storage = (building as Building_BatshittiumBallista)?.InternalMagazine
                ?? (building as Building_BallistaMagazine)?.Storage;
            storage?.Settings?.filter?.SetDisallowAll();
        }

        /// <summary>Switches a flickable building off, wanted state included.</summary>
        /// <param name="building">The building.</param>
        /// <param name="report">Collects failures.</param>
        private static void SwitchOff(Thing building, AnyRimBuildReport report)
        {
            CompFlickable flick = building.TryGetComp<CompFlickable>();
            if (flick == null)
            {
                report.Fail($"{building.def.defName}: has no switch");
                return;
            }

            flick.SwitchIsOn = false;
            WantSwitchOnField?.SetValue(flick, false);
        }

        /// <summary>Fills a fuel tank to capacity.</summary>
        /// <param name="fuel">The refuelable comp.</param>
        private static void Fill(CompRefuelable fuel)
        {
            float perUnit = Mathf.Max(fuel.Props.FuelMultiplierCurrentDifficulty, 0.01f);
            float missing = fuel.Props.fuelCapacity - fuel.Fuel;
            if (missing > 0f)
            {
                fuel.Refuel(missing / perUnit + 0.01f);
            }
        }

        /// <summary>
        /// Suspends every bill on a work table, adding the Transmogrifier's
        /// rock-chunk bill first where the table can do it.
        /// </summary>
        /// <param name="building">The work table.</param>
        /// <param name="report">Collects failures.</param>
        private static void SuspendBills(Thing building, AnyRimBuildReport report)
        {
            if (!(building is IBillGiver giver) || giver.BillStack == null)
            {
                report.Fail($"{building.def.defName}: takes no bills");
                return;
            }

            RecipeDef chunks = DefDatabase<RecipeDef>.GetNamedSilentFail(RockChunkRecipe);
            if (chunks != null && building.def.AllRecipes.Contains(chunks)
                && !giver.BillStack.Bills.Any(b => b.recipe == chunks))
            {
                giver.BillStack.AddBill(chunks.MakeNewBill());
            }

            foreach (Bill bill in giver.BillStack.Bills)
            {
                bill.suspended = true;
            }
        }

        /// <summary>
        /// Loads planned bolts into a Ballista's internal magazine (its ammo
        /// set to the first kind) or a Ballista Magazine, as the showcase's
        /// demo base does.
        /// </summary>
        /// <param name="building">The Ballista or magazine.</param>
        /// <param name="placement">The placement.</param>
        /// <param name="report">Collects failures.</param>
        private static void Load(Thing building, Placement placement, AnyRimBuildReport report)
        {
            var ballista = building as Building_BatshittiumBallista;
            BoltStorage storage = ballista?.InternalMagazine
                ?? (building as Building_BallistaMagazine)?.Storage;
            if (storage == null)
            {
                report.Fail($"{placement.Def}: holds no bolts");
                return;
            }

            ThingDef first = null;
            foreach ((string defName, int count) in placement.Load)
            {
                ThingDef bolt = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (bolt?.GetModExtension<BoltExtension>() == null)
                {
                    report.Fail($"{placement.Def}: {defName} is not a bolt");
                    continue;
                }

                first = first ?? bolt;
                int before = storage.CountOf(bolt);
                Showcase.Fill(storage, bolt, count);
                int loaded = storage.CountOf(bolt) - before;
                if (loaded < count)
                {
                    report.Fail($"{placement.Def}: only {loaded} of {count} {defName} fit");
                }
            }

            if (ballista != null && first != null)
            {
                ballista.SetAmmo(first);
            }
        }

        /// <summary>
        /// Zones a player stockpile that accepts only the planned defs. Cells
        /// already zoned, or holding something a zone cannot overlap, are
        /// failures.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="anchor">The crossroads' map cell.</param>
        /// <param name="stockpile">The stockpile's plan.</param>
        /// <param name="report">Collects counts and failures.</param>
        private static void Zone(Map map, IntVec3 anchor, StockpilePlan stockpile, AnyRimBuildReport report)
        {
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(zone);
            zone.settings.filter.SetDisallowAll();
            foreach (string defName in stockpile.Accepts)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    report.Fail($"stockpile: missing ThingDef {defName}");
                    continue;
                }

                zone.settings.filter.SetAllow(def, true);
            }

            foreach (IntVec3 c in ToMap(anchor, stockpile.Area))
            {
                if (!c.InBounds(map) || map.zoneManager.ZoneAt(c) != null
                    || c.GetThingList(map).Any(t => !t.def.CanOverlapZones))
                {
                    report.Fail($"stockpile: cell {c} cannot be zoned");
                    continue;
                }

                zone.AddCell(c);
                report.StockpileCells++;
            }

            if (zone.Cells.Count == 0)
            {
                zone.Deregister();
            }
        }

        /// <summary>A placement, for failure messages.</summary>
        /// <param name="placement">The placement.</param>
        /// <returns>"Def at (x,z)".</returns>
        private static string Describe(Placement placement) => $"{placement.Def} at {placement.At}";

        // ===== Demolition =====

        /// <summary>
        /// Takes the map's town down (docs/SHOWCASE.md, "Tests") over the
        /// ground it cleared, recomputed from the plan it executed: roofs
        /// come off first, so nothing collapses as walls vanish; residents
        /// (or their corpses) and the town's animals vanish, their lords
        /// with them, unless they have left the town (recruited, captured,
        /// enslaved or tamed: those are only forgotten); every destroyable
        /// thing that is not a pawn vanishes (no self-destructs, death
        /// blasts or leavings), apart from player corpses; the town's own
        /// stockpile cells are unzoned (no other zone is touched); and the
        /// record is reset. The levelled terrain and the road tapers stay.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>False if the map had no town, or its plan is lost.</returns>
        internal static bool Demolish(Map map)
        {
            MapComponent_AnyRim record = map?.GetComponent<MapComponent_AnyRim>();
            if (record == null || !record.Built)
            {
                return false;
            }

            TownPlan plan = record.Plan;
            if (plan == null)
            {
                Log.Error("[BT] AnyRim cannot be removed: its plan could not be rebuilt.");
                return false;
            }

            List<IntVec3> cleared = ClearedCells(map, plan, record.Anchor);
            foreach (IntVec3 c in cleared)
            {
                map.roofGrid.SetRoof(c, null);
            }

            Faction town = Faction.OfAncients;
            foreach (Pawn pawn in record.Residents.ToList())
            {
                if (StillTownResident(pawn, town))
                {
                    Vanish(pawn, map);
                }
            }

            foreach (Pawn pawn in record.Animals.ToList())
            {
                if (StillTownAnimal(pawn))
                {
                    Vanish(pawn, map);
                }
            }

            foreach (IntVec3 c in cleared)
            {
                foreach (Thing thing in c.GetThingList(map).ToList())
                {
                    if (thing.Destroyed || thing is Pawn || !thing.def.destroyable
                        || (thing is Corpse corpse && corpse.InnerPawn?.Faction == Faction.OfPlayer))
                    {
                        continue;
                    }

                    // Empty buildings first: the Ballista and its magazines
                    // spill their bolts when they leave the map, even on
                    // Vanish (uninstalling uses it too). Corpses and crates
                    // look after their own contents.
                    if (thing is Building && thing is IThingHolder holder)
                    {
                        holder.GetDirectlyHeldThings()?.ClearAndDestroyContents(DestroyMode.Vanish);
                    }

                    thing.Destroy(DestroyMode.Vanish);
                }
            }

            foreach (StockpilePlan pile in plan.Stockpiles)
            {
                foreach (IntVec3 c in ToMap(record.Anchor, pile.Area).ClipInsideMap(map))
                {
                    if (map.zoneManager.ZoneAt(c) is Zone_Stockpile zone)
                    {
                        zone.RemoveCell(c);
                    }
                }
            }

            record.Reset();
            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            return true;
        }

        /// <summary>
        /// Whether a resident, alive or dead, still belongs to the town: of
        /// the town's faction, and neither a prisoner nor a slave of the
        /// colony. A recruited, captured or enslaved resident does not.
        /// </summary>
        /// <param name="pawn">The resident.</param>
        /// <param name="town">The town's faction, or null if it is gone.</param>
        /// <returns>True if demolition may take it.</returns>
        private static bool StillTownResident(Pawn pawn, Faction town) =>
            pawn != null && town != null && pawn.Faction == town
            && !pawn.IsPrisonerOfColony && !pawn.IsSlaveOfColony;

        /// <summary>
        /// Whether a town animal, alive or dead, is still the town's: still
        /// without a faction (wild), so not tamed by anyone.
        /// </summary>
        /// <param name="pawn">The animal.</param>
        /// <returns>True if demolition may take it.</returns>
        private static bool StillTownAnimal(Pawn pawn) => pawn != null && pawn.Faction == null;

        /// <summary>
        /// Vanishes a town pawn on this map, or its corpse if it is dead.
        /// Pawns that left the map are left alone.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <param name="map">The town's map.</param>
        private static void Vanish(Pawn pawn, Map map)
        {
            if (pawn == null || pawn.Destroyed)
            {
                return;
            }

            if (pawn.Dead)
            {
                Corpse corpse = pawn.Corpse;
                if (corpse != null && !corpse.Destroyed && corpse.MapHeld == map)
                {
                    corpse.Destroy(DestroyMode.Vanish);
                }

                return;
            }

            if (pawn.MapHeld == map)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
        }

        // ===== Dev-mode actions =====

        /// <summary>
        /// Dev mode, Actions, Mods: builds AnyRim with its crossroads at the
        /// clicked cell (moved in from the map edge if needed), reduced
        /// around anything indestructible. Refuses lava, hostiles and a
        /// second town.
        /// </summary>
        [DebugAction("Mods", "Build AnyRim here", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBuildHere()
        {
            Map map = Find.CurrentMap;
            IntVec3 clicked = UI.MouseCell();
            if (map == null || !clicked.InBounds(map) || !CanStartBuild(map))
            {
                return;
            }

            TownPlan plan = Plan(DefaultSeed);
            Box footprint = Footprint(plan);
            int minX = EdgeMargin - footprint.MinX;
            int maxX = map.Size.x - 1 - EdgeMargin - footprint.MaxX;
            int minZ = EdgeMargin - footprint.MinZ;
            int maxZ = map.Size.z - 1 - EdgeMargin - footprint.MaxZ;
            if (minX > maxX || minZ > maxZ)
            {
                Messages.Message("AnyRim does not fit on this map.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            var anchor = new IntVec3(
                Mathf.Clamp(clicked.x, minX, maxX), 0, Mathf.Clamp(clicked.z, minZ, maxZ));
            SiteGrid grid = ScanSite(map, blockIndestructible: false);
            Cell? ok = TownSite.Best(
                grid, plan.Bounds, plan.Core, plan.FirebreakWidth, new[] { ToCell(anchor) }, ToCell(anchor),
                EdgeMargin, Array.Empty<KeepAway>(), PenaltyPerTile);
            if (!ok.HasValue)
            {
                Messages.Message(
                    "AnyRim cannot be built here: lava, hostiles or an ancient vent are too close.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            ReportBuilt(map, Build(map, plan, anchor));
        }

        /// <summary>
        /// Dev mode, Actions, Mods: builds AnyRim at the cheapest site on
        /// the map, near where the showcase would put it, with its parts at
        /// least 10 tiles from the player's buildings and its reactor more
        /// than <see cref="ReactorReach"/> tiles from them, out of reach of
        /// its own self-destruct.
        /// </summary>
        [DebugAction("Mods", "Build AnyRim (best site)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBuildBest()
        {
            Map map = Find.CurrentMap;
            if (map == null || !CanStartBuild(map))
            {
                return;
            }

            var keepAways = new List<KeepAway>();
            Predicate<IntVec3> reactorClear = null;
            List<Building> colony = map.listerBuildings.allBuildingsColonist;
            if (colony.Count > 0)
            {
                CellRect bounds = colony[0].OccupiedRect();
                foreach (Building building in colony)
                {
                    bounds = bounds.Encapsulate(building.OccupiedRect());
                }

                keepAways.Add(new KeepAway(ToBox(bounds), 10));
                Cell? reactor = Plan(DefaultSeed).Reactor;
                if (reactor.HasValue)
                {
                    reactorClear = anchor =>
                        bounds.ClosestDistSquaredTo(ToMap(anchor, reactor.Value))
                            > ReactorReach * ReactorReach;
                }
            }

            AnyRimBuildReport report = BuildBest(
                map, DefaultSeed, CellRect.WholeMap(map), Showcase.AnyRimPreferredSite(map), keepAways,
                reactorClear);
            if (!report.Anchor.IsValid)
            {
                Messages.Message("No site on this map suits AnyRim.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            ReportBuilt(map, report);
        }

        /// <summary>Dev mode, Actions, Mods: takes the map's AnyRim down.</summary>
        [DebugAction("Mods", "Remove AnyRim", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRemove()
        {
            Map map = Find.CurrentMap;
            bool removed = map != null && Demolish(map);
            Messages.Message(
                removed ? "AnyRim has been removed. The ground stays level." : "There is no AnyRim on this map.",
                removed ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, false);
        }

        /// <summary>Refuses a second town on a map, with a message.</summary>
        /// <param name="map">The map.</param>
        /// <returns>True if a town may be built.</returns>
        private static bool CanStartBuild(Map map)
        {
            MapComponent_AnyRim record = map.GetComponent<MapComponent_AnyRim>();
            if (record != null && !record.Built)
            {
                return true;
            }

            Messages.Message(
                "AnyRim already stands on this map. Remove it first.", MessageTypeDefOf.RejectInput, false);
            return false;
        }

        /// <summary>Tells the dev-mode user how a build went.</summary>
        /// <param name="map">The map.</param>
        /// <param name="report">The build report.</param>
        private static void ReportBuilt(Map map, AnyRimBuildReport report)
        {
            string text = report.Clean
                ? $"AnyRim built at ({report.Anchor.x},{report.Anchor.z})."
                : $"AnyRim built at ({report.Anchor.x},{report.Anchor.z}) with "
                    + $"{report.Failures.Count} problem(s); see the log.";
            Messages.Message(
                text, new LookTargets(report.Anchor, map),
                report.Clean ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.CautionInput, false);
        }
    }
}
