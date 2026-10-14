// Batshit Technology - in-game showcase: the tour's stage.
// The demo base behind BT_Showcase, built for the directed tour
// (BatshitTourDirector and its scenes in Source/Tour/): from the main
// menu's Showcases button, or with -showcase=BT_Showcase
// (Tools/smoke-test.ps1 -Showcase). On the fresh 250-cell map the API
// generates, it finishes the mod's research and builds a working demo
// base: a fuelled reactor powering a stocked replicator, the
// Transmogrifier with a bill and rock chunks, a colonist in the Brain
// Squeezer, the Artificer making bolts, and a Ballista with chained,
// loaded magazines that holds its fire until its scene. It builds AnyRim,
// the test town, on the best site in the north-west (or logs that it
// skipped it), then plans the tractor yard and the two self-destruct
// pads together (Core's TourStagePlanner), puts a lone Random Replicator
// and a lone Batshittium Reactor on the pads, and stages the grenade's
// junk pile and cast. Nothing here runs on a timer: the scenes arm,
// throw and tour. Log cue for capture tooling: [BT-SHOWCASE] ANYRIM (or
// AnyRim skipped); the API logs READY once the stage is built.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using Verse;
using Verse.AI;
using RimWorld;

namespace BatshitTechnology
{
    /// <summary>Builds the showcase base.</summary>
    internal static class Showcase
    {
        /// <summary>
        /// Builds the tour's stage around the map's centre: the demo base,
        /// AnyRim, the self-destruct pads and the grenade's junk pile. The
        /// Ballista is loaded but holds its fire, and nothing is armed or
        /// thrown; the scenes do that.
        /// </summary>
        /// <param name="map">The showcase's fresh map.</param>
        /// <returns>What the tour's scenes have to work with.</returns>
        internal static TourStage Build(Map map)
        {
            // God mode, for the devices' DEV buttons, is lent by the
            // Showcase API in BatshitTourDirector.Setup, in dev mode only,
            // and given back when the showcase ends: it is a static flag
            // that would otherwise follow the player into their own games.
            FinishResearch();
            IntVec3 center = map.Center;
            TractorStaging.LevelGround(map, CellRect.CenteredOn(
                center, TourStagePlanner.BaseHalfSize));

            Thing reactor = Spawn(map, "BT_BatshittiumReactor", center);
            reactor.TryGetComp<CompRefuelable>().Refuel(2f);

            var replicator = (Building_RandomReplicator)Spawn(
                map, "BT_RandomReplicator", center + new IntVec3(-5, 0, 0));
            var replicatorComp = replicator.GetComp<CompRandomReplicator>();
            for (int i = 0; i < 6; i++)
            {
                replicatorComp.ReplicateNow();
            }

            var bench = (Building_WorkTable)Spawn(
                map, "BT_BatshittiumTransmogrifier", center + new IntVec3(5, 0, 0));
            bench.billStack.AddBill(
                DefDatabase<RecipeDef>.GetNamed("BT_TransmogrifyRockChunks").MakeNewBill());
            ThingDef chunk = DefDatabase<ThingDef>.GetNamed("ChunkGranite");
            for (int i = 0; i < 20; i++)
            {
                GenPlace.TryPlaceThing(
                    ThingMaker.MakeThing(chunk), center + new IntVec3(9, 0, -2),
                    map, ThingPlaceMode.Near);
            }

            Thing squeezer = Spawn(map, "BT_BrainSqueezer", center + new IntVec3(0, 0, -5));

            Thing artificer = Spawn(map, "BT_BatshittiumArtificer", center + new IntVec3(3, 0, 3));
            artificer.TryGetComp<CompRefuelable>().Refuel(10f);

            Building_BatshittiumBallista ballista = BuildBallista(
                map, center, out IntVec3 volleyTarget);

            // Stock for haulers: batshittium to refuel the reactor and
            // Artificer, and a stockpile for replicated food.
            Thing batshittium = ThingMaker.MakeThing(
                DefDatabase<ThingDef>.GetNamed("BT_Batshittium"));
            batshittium.stackCount = 30;
            GenPlace.TryPlaceThing(batshittium, center + new IntVec3(-3, 0, 4), map,
                ThingPlaceMode.Near);
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(zone);
            foreach (IntVec3 c in CellRect.CenteredOn(center + new IntVec3(-9, 0, -9), 2))
            {
                zone.AddCell(c);
            }

            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            SendColonistToSqueezer(map, squeezer);

            // AnyRim goes up before the pads, which clear the fog and
            // must stay out of the town's reach.
            BuildAnyRim(map, center);
            List<Thing> pads = BuildSelfDestructDemo(map, center);
            (Pawn grenadier, Pawn volunteer, IntVec3 grenadeTarget) =
                BuildGrenadeDemo(map, center);
            Find.CameraDriver.JumpToCurrentMapLoc(center);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;

            // The Showcase API logs "[BT-SHOWCASE] READY" once the
            // director's Setup returns; Tools/smoke-test.ps1 waits for it.
            Box? yard = StagePlan?.Yard;
            return new TourStage
            {
                BaseCentre = center,
                Reactor = reactor,
                Replicator = replicator,
                Transmogrifier = bench,
                Squeezer = squeezer,
                Artificer = artificer,
                Ballista = ballista,
                BallistaTarget = volleyTarget,
                Pads = pads,
                Grenadier = grenadier,
                Volunteer = volunteer,
                GrenadeTarget = grenadeTarget,
                AnyRimPlan = anyRimPlan,
                AnyRimAnchor = anyRimAnchor,
                Yard = yard is Box y
                    ? CellRect.FromLimits(y.MinX, y.MinZ, y.MaxX, y.MaxZ)
                        .ClipInsideMap(map)
                    : (CellRect?)null,
                PlanNote = StagePlan?.Note,
            };
        }

        /// <summary>
        /// The tiles a demo corner's self-destruct can reach, counting the
        /// fires and secondary blasts it starts (the replicator's 83.6, the
        /// larger of the two), plus a 4-tile margin. docs/SHOWCASE.md,
        /// "Safety Rules". Core's planner holds the number.
        /// </summary>
        internal const double DemoCornerReach =
            TourStagePlanner.DemoCornerReach;

        /// <summary>
        /// Where the tour stages its dangerous scenes on this map: the
        /// tractor yard and the self-destruct pads, chosen together by
        /// Core's <see cref="TourStagePlanner"/> once AnyRim stands. Null
        /// before the first build.
        /// </summary>
        internal static TourStagePlan StagePlan { get; private set; }

        /// <summary>The quick-test map's size, which the doc's numbers assume.</summary>
        private const float DocMapSize = 250f;

        /// <summary>
        /// How far north of the welcome sign the AnyRim scene looks, so the
        /// sign sits low in the frame with the town above it.
        /// </summary>
        internal const int TourLookNorth = 20;

        /// <summary>
        /// The Ballista's range during its volley: far enough to reach the
        /// open ground 60 tiles east.
        /// </summary>
        internal const int VolleyRange = 75;

        /// <summary>
        /// A Ballista with three chained magazines of explosive bolts, set
        /// for 16-bolt volleys, holding its fire: its range is the least
        /// it allows, so it never reaches the tractor yard's raiders, and
        /// it has no target until the defence scene orders one.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The base's center.</param>
        /// <param name="target">The open ground 60 tiles east to fire at.</param>
        /// <returns>The Ballista.</returns>
        private static Building_BatshittiumBallista BuildBallista(
            Map map, IntVec3 center, out IntVec3 target)
        {
            var ballista = (Building_BatshittiumBallista)Spawn(
                map, "BT_BatshittiumBallista", center + new IntVec3(4, 0, -5));
            ThingDef explosive = BT_BallistaDefOf.BT_BoltReallyPainful;
            Fill(ballista.InternalMagazine, explosive, 16);
            foreach (IntVec3 offset in new[] { new IntVec3(6, 0, -5), new IntVec3(7, 0, -5),
                new IntVec3(7, 0, -6) })
            {
                var magazine = (Building_BallistaMagazine)Spawn(
                    map, "BT_BallistaMagazine", center + offset);
                Fill(magazine.Storage, explosive, 400);
                Fill(magazine.Storage, BT_BallistaDefOf.BT_BoltPainful, 200);
            }

            ballista.SetAmmo(explosive);
            ballista.SetCount(16);
            ballista.SetRange(BallistaPower.MinRange);
            target = center + new IntVec3(60, 0, 0);
            target.x = System.Math.Min(target.x, map.Size.x - 10);
            return ballista;
        }

        /// <summary>The plan AnyRim was built from, or null if it was skipped.</summary>
        private static TownPlan anyRimPlan;

        /// <summary>AnyRim's crossroads, or Invalid if it was skipped.</summary>
        private static IntVec3 anyRimAnchor = IntVec3.Invalid;

        /// <summary>
        /// AnyRim's preferred crossroads, (55,163) on the 250-tile
        /// quick-test map, scaled to the map's size.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The cell.</returns>
        internal static IntVec3 AnyRimPreferredSite(Map map) =>
            new IntVec3(ScaleX(map, 55), 0, ScaleZ(map, 163));

        /// <summary>
        /// Where AnyRim's crossroads may go: x 45..60 and z 150..177 on the
        /// 250-tile quick-test map, scaled to the map's size.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The window.</returns>
        internal static CellRect AnyRimWindow(Map map) =>
            CellRect.FromLimits(ScaleX(map, 45), ScaleZ(map, 150), ScaleX(map, 60), ScaleZ(map, 177));

        /// <summary>
        /// Where to look for AnyRim, in order: its own window, then the
        /// north-west half of the map (x 45..100, z 120..177 on the 250-tile
        /// quick-test map, scaled). The keep-aways still hold everywhere.
        /// One map had an insect hive close enough to rule out the whole
        /// first window.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The windows, best first.</returns>
        internal static List<CellRect> AnyRimWindows(Map map) => new List<CellRect>
        {
            AnyRimWindow(map),
            CellRect.FromLimits(ScaleX(map, 45), ScaleZ(map, 120), ScaleX(map, 100), ScaleZ(map, 177)),
        };

        /// <summary>
        /// What AnyRim keeps away from: 10 tiles from the base's cleared
        /// square, 15 from the grenade clearing, and the demo-corner reach
        /// from the north-east and south-east pads, so that those two stay
        /// usable.
        /// </summary>
        /// <param name="baseCenter">The demo base's centre.</param>
        /// <returns>The keep-aways, in map cells.</returns>
        internal static List<KeepAway> AnyRimKeepAways(IntVec3 baseCenter) => new List<KeepAway>
        {
            new KeepAway(AnyRim.ToBox(CellRect.CenteredOn(baseCenter, 16)), 10),
            new KeepAway(AnyRim.ToBox(CellRect.CenteredOn(baseCenter + new IntVec3(-25, 0, 0), 13)), 15),
            new KeepAway(
                AnyRim.ToBox(CellRect.SingleCell(baseCenter + new IntVec3(DemoOffset, 0, DemoOffset))),
                DemoCornerReach),
            new KeepAway(
                AnyRim.ToBox(CellRect.SingleCell(baseCenter + new IntVec3(DemoOffset, 0, -DemoOffset))),
                DemoCornerReach),
        };

        /// <summary>
        /// Builds AnyRim on the best site in its window and logs a cue
        /// either way: "[BT-SHOWCASE] ANYRIM at (x,z)" or "[BT-SHOWCASE]
        /// AnyRim skipped". A failure never stops the showcase.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="baseCenter">The demo base's centre.</param>
        /// <returns>True if the town was built.</returns>
        private static bool BuildAnyRim(Map map, IntVec3 baseCenter)
        {
            anyRimPlan = null;
            anyRimAnchor = IntVec3.Invalid;
            try
            {
                AnyRimBuildReport report = new AnyRimBuildReport();
                foreach (CellRect window in AnyRimWindows(map))
                {
                    report = AnyRim.BuildBest(
                        map, AnyRim.DefaultSeed, window, AnyRimPreferredSite(map),
                        AnyRimKeepAways(baseCenter));
                    if (report.Anchor.IsValid)
                    {
                        break;
                    }
                }

                MapComponent_AnyRim record = map.GetComponent<MapComponent_AnyRim>();
                if (report.Anchor.IsValid && record != null && record.Built
                    && report.Plan != null)
                {
                    anyRimPlan = report.Plan;
                    anyRimAnchor = report.Anchor;
                    Log.Message($"[BT-SHOWCASE] ANYRIM at ({report.Anchor.x},{report.Anchor.z})");
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[BT] Showcase could not build AnyRim: {e}");
            }

            Log.Message("[BT-SHOWCASE] AnyRim skipped");
            return false;
        }

        /// <summary>Scales an x from the 250-tile quick-test map to this map.</summary>
        /// <param name="map">The map.</param>
        /// <param name="x">The x on a 250-tile map.</param>
        /// <returns>The x on this map.</returns>
        private static int ScaleX(Map map, int x) => (int)Math.Round(x * map.Size.x / DocMapSize);

        /// <summary>Scales a z from the 250-tile quick-test map to this map.</summary>
        /// <param name="map">The map.</param>
        /// <param name="z">The z on a 250-tile map.</param>
        /// <returns>The z on this map.</returns>
        private static int ScaleZ(Map map, int z) => (int)Math.Round(z * map.Size.z / DocMapSize);

        /// <summary>
        /// Distance from the base to the self-destruct demo, along both axes:
        /// about 106 tiles, clear of the 75-tile blast and the base's own
        /// half-width.
        /// </summary>
        private const int DemoOffset = 75;

        /// <summary>
        /// Two lone devices on small pads in the far corners with the most
        /// open ground, among the corners that every part of AnyRim (as
        /// built) and the tractor yard clear by
        /// <see cref="DemoCornerReach"/> tiles; Core's planner chooses the
        /// pads and the yard together (<see cref="StagePlan"/>): a Random
        /// Replicator, then a Batshittium Reactor. Neither is armed here;
        /// the retirement scenes arm them. Fog is cleared so every blast
        /// can be watched.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="baseCenter">The demo base's centre.</param>
        /// <returns>
        /// The pad devices, replicator first: none, one or two.
        /// </returns>
        private static List<Thing> BuildSelfDestructDemo(Map map, IntVec3 baseCenter)
        {
            StagePlan = PlanStage(map, baseCenter);
            List<IntVec3> corners = StagePlan.Pads
                .Select(c => new IntVec3(c.X, 0, c.Z))
                .ToList();
            map.fogGrid.ClearAllFog();
            var pads = new List<Thing>();
            string[] devices = { "BT_RandomReplicator", "BT_BatshittiumReactor" };
            for (int i = 0; i < Math.Min(corners.Count, devices.Length); i++)
            {
                TractorStaging.LevelGround(
                    map, CellRect.CenteredOn(corners[i], 2));
                pads.Add(Spawn(map, devices[i], corners[i]));
            }

            if (corners.Count == 0)
            {
                Log.Message("[BT-SHOWCASE] no demo corner is clear of "
                    + $"AnyRim and the yard ({StagePlan.Note})");
            }
            else if (corners.Count == 1)
            {
                Log.Message("[BT-SHOWCASE] only one demo corner clears "
                    + "AnyRim and the yard; reactor demo skipped");
            }

            return pads;
        }

        /// <summary>
        /// Plans the tractor yard and the self-destruct pads: the pad
        /// corners are offered in order of open ground, and the planner
        /// keeps every pad <see cref="DemoCornerReach"/> from AnyRim (as
        /// built) and from the yard. A missing yard is logged as a
        /// message; the tractor scenes then decline.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="baseCenter">The demo base's centre.</param>
        /// <returns>The plan.</returns>
        private static TourStagePlan PlanStage(Map map, IntVec3 baseCenter)
        {
            List<Cell> padOrder = TourStagePlanner.DefaultPadOffsets
                .OrderByDescending(o => OpenGround(
                    map, baseCenter + new IntVec3(o.X, 0, o.Z)))
                .ToList();
            var input = new TourStageInput(
                new Box(0, 0, map.Size.x - 1, map.Size.z - 1),
                AnyRim.ToCell(baseCenter))
            {
                Town = anyRimPlan,
                TownAnchor = anyRimPlan != null
                    ? AnyRim.ToCell(anyRimAnchor)
                    : default,
                PadOffsets = padOrder,
            };
            TourStagePlan plan = TourStagePlanner.Plan(input);
            if (plan.Yard == null)
            {
                Log.Message($"[BT-SHOWCASE] no tractor yard: {plan.Note}");
            }

            return plan;
        }

        /// <summary>
        /// A junk pile 25 tiles west of the base (stools, a wooden wall and
        /// stacks of steel and wood), and a colonist armed with an Ergonomic
        /// Singularity Grenade, ready to throw it at the pile with another
        /// colonist volunteering inside it. The grenade scene throws it.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="baseCenter">The demo base's centre.</param>
        /// <returns>
        /// The grenadier and the volunteer (either may be null when the
        /// colony is short of colonists) and the pile's centre.
        /// </returns>
        private static (Pawn Grenadier, Pawn Volunteer, IntVec3 Target)
            BuildGrenadeDemo(Map map, IntVec3 baseCenter)
        {
            IntVec3 grenadeTarget = baseCenter + new IntVec3(
                TourStagePlanner.GrenadeOffset.X, 0,
                TourStagePlanner.GrenadeOffset.Z);
            TractorStaging.LevelGround(map, CellRect.CenteredOn(
                grenadeTarget, TourStagePlanner.GrenadeHalfSize));
            ThingDef stool = DefDatabase<ThingDef>.GetNamedSilentFail("Stool");
            foreach (IntVec3 offset in new[]
            {
                new IntVec3(-6, 0, 4), new IntVec3(5, 0, -5), new IntVec3(-3, 0, -7), new IntVec3(7, 0, 6),
            })
            {
                if (stool != null)
                {
                    GenSpawn.Spawn(ThingMaker.MakeThing(stool, ThingDefOf.WoodLog), grenadeTarget + offset, map);
                }
            }

            for (int x = -3; x <= 3; x++)
            {
                GenSpawn.Spawn(
                    ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog),
                    grenadeTarget + new IntVec3(x, 0, 8), map);
            }

            foreach (var (def, offset) in new[]
            {
                (ThingDefOf.Steel, new IntVec3(8, 0, -2)), (ThingDefOf.WoodLog, new IntVec3(-8, 0, 0)),
                (ThingDefOf.Steel, new IntVec3(2, 0, 9)), (ThingDefOf.WoodLog, new IntVec3(-4, 0, -9)),
            })
            {
                Thing stack = ThingMaker.MakeThing(def);
                stack.stackCount = 50;
                GenSpawn.Spawn(stack, grenadeTarget + offset, map);
            }

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Dead && !p.Downed).ToList();
            if (colonists.Count == 0)
            {
                return (null, null, grenadeTarget);
            }

            Pawn grenadier = ArmGrenadier(colonists[0]);
            Pawn volunteer = colonists.Count > 1 ? colonists[colonists.Count - 1] : null;
            return (grenadier, volunteer, grenadeTarget);
        }

        /// <summary>The Ergonomic Singularity Grenade's def name.</summary>
        internal const string GrenadeDef = "BT_ErgonomicSingularityGrenade";

        /// <summary>The grenade's projectile's def name.</summary>
        internal const string GrenadeProjectileDef = "BT_Projectile_SingularityGrenade";

        /// <summary>
        /// Puts an Ergonomic Singularity Grenade in a colonist's hands, in
        /// place of whatever it held, unless it holds one already.
        /// </summary>
        /// <param name="pawn">The colonist.</param>
        /// <returns>The colonist, or null if it has no equipment.</returns>
        internal static Pawn ArmGrenadier(Pawn pawn)
        {
            if (pawn?.equipment == null)
            {
                return null;
            }

            if (pawn.equipment.Primary?.def.defName != GrenadeDef)
            {
                Thing grenade = ThingMaker.MakeThing(
                    DefDatabase<ThingDef>.GetNamed(GrenadeDef));
                pawn.equipment.DestroyAllEquipment();
                pawn.equipment.AddEquipment((ThingWithComps)grenade);
            }

            return pawn;
        }

        /// <summary>
        /// Brings the grenadier into range of the pile and the volunteer
        /// into it, and orders the throw.
        /// </summary>
        /// <param name="grenadier">The colonist holding the grenade.</param>
        /// <param name="volunteer">The volunteer, or null.</param>
        /// <param name="target">The pile's centre.</param>
        /// <returns>Why the throw could not be ordered, or null if it was.</returns>
        internal static string ThrowGrenade(Pawn grenadier, Pawn volunteer, IntVec3 target)
        {
            if (grenadier == null || grenadier.Dead || !grenadier.Spawned
                || grenadier.Downed)
            {
                return $"the grenadier {grenadier?.LabelShort ?? "(none)"} "
                    + $"is not standing on the map (dead {grenadier?.Dead}, "
                    + $"spawned {grenadier?.Spawned}, downed {grenadier?.Downed})";
            }

            if (!target.IsValid)
            {
                return "the junk pile has no cell";
            }

            Teleport(grenadier, target + new IntVec3(12, 0, 0));
            if (volunteer != null && !volunteer.Dead && volunteer.Spawned)
            {
                Teleport(volunteer, target + new IntVec3(-7, 0, -3));
            }

            grenadier.drafter.Drafted = true;
            Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
            job.maxNumStaticAttacks = 1;
            return grenadier.jobs.TryTakeOrderedJob(job, JobTag.Misc)
                ? null
                : "the grenadier would not take the order";
        }

        /// <summary>Moves a pawn, interrupting whatever it was doing.</summary>
        /// <param name="pawn">The pawn.</param>
        /// <param name="cell">Where to put it.</param>
        private static void Teleport(Pawn pawn, IntVec3 cell)
        {
            pawn.jobs?.StopAll();
            pawn.Position = cell;
            pawn.Notify_Teleported();
        }

        /// <summary>
        /// Showcase only: colonists within a radius of a danger are moved
        /// back to the base, so the scene kills no one.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="danger">Where the danger is.</param>
        /// <param name="radius">How far out counts as in danger.</param>
        /// <param name="spare">A colonist to leave where it is, or null.</param>
        internal static void MoveColonistsClear(
            Map map, IntVec3 danger, float radius, Pawn spare = null)
        {
            if (danger.IsValid)
            {
                MoveColonistsOut(
                    map, c => c.InHorDistOf(danger, radius), spare);
            }
        }

        /// <summary>
        /// Showcase only: colonists standing where a test says is in
        /// danger are moved back to the base, near the map's centre.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="inDanger">Whether a cell is in danger.</param>
        /// <param name="spare">A colonist to leave where it is, or null.</param>
        internal static void MoveColonistsOut(
            Map map, Func<IntVec3, bool> inDanger, Pawn spare = null)
        {
            if (map == null || inDanger == null)
            {
                return;
            }

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (pawn == spare
                    || !inDanger(pawn.Position)
                    || !CellFinder.TryFindRandomCellNear(
                        map.Center, map, 8, c => c.Standable(map), out IntVec3 cell))
                {
                    continue;
                }

                pawn.jobs?.StopAll();
                pawn.Position = cell;
                pawn.Notify_Teleported();
            }
        }

        /// <summary>
        /// Open, unroofed, standable cells near a spot: where a blast and its
        /// fires have the most to show.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="spot">The candidate centre.</param>
        /// <returns>How many open cells lie within 20 tiles.</returns>
        private static int OpenGround(Map map, IntVec3 spot) =>
            GenRadial.RadialCellsAround(spot, 20f, true)
                .Count(c => c.InBounds(map) && c.Standable(map) && !c.Roofed(map));

        /// <summary>Starts a free colonist on a session in the squeezer.</summary>
        /// <param name="map">The map.</param>
        /// <param name="squeezer">The Brain Squeezer.</param>
        internal static void SendColonistToSqueezer(Map map, Thing squeezer)
        {
            Pawn pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                p => !p.Downed && !p.InMentalState && p.needs?.joy != null);
            if (pawn == null)
            {
                return;
            }

            pawn.needs.joy.CurLevel = 0.2f;
            pawn.jobs.StartJob(
                JobMaker.MakeJob(BT_BrainSqueezerDefOf.BT_UseBrainSqueezer, squeezer,
                    squeezer.Position),
                JobCondition.InterruptForced);
        }

        /// <summary>Finishes every project in the Batshit Technology tab.</summary>
        private static void FinishResearch()
        {
            foreach (ResearchProjectDef project in DefDatabase<ResearchProjectDef>.AllDefs
                .Where(p => p.tab?.defName == "BT_BatshitTechnology" && !p.IsFinished)
                .OrderBy(p => p.researchViewX))
            {
                Find.ResearchManager.FinishProject(
                    project, doCompletionDialog: false, doCompletionLetter: false);
            }
        }

        /// <summary>Spawns a player-owned building.</summary>
        /// <param name="map">The map.</param>
        /// <param name="defName">The ThingDef.</param>
        /// <param name="at">Where.</param>
        /// <returns>The building.</returns>
        private static Thing Spawn(Map map, string defName, IntVec3 at)
        {
            Thing thing = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName));
            thing.SetFaction(Faction.OfPlayer);
            return GenSpawn.Spawn(thing, at, map, WipeMode.Vanish);
        }

        /// <summary>
        /// Puts bolts straight into a bolt store (the showcase's Ballista
        /// and AnyRim's). Whatever does not fit is discarded.
        /// </summary>
        /// <param name="storage">The store.</param>
        /// <param name="bolt">The bolt type.</param>
        /// <param name="count">How many; split into full stacks.</param>
        internal static void Fill(BoltStorage storage, ThingDef bolt, int count)
        {
            while (count > 0)
            {
                Thing stack = ThingMaker.MakeThing(bolt);
                stack.stackCount = System.Math.Min(count, bolt.stackLimit);
                count -= stack.stackCount;
                storage.Container.TryAdd(stack);
            }
        }
    }
}
