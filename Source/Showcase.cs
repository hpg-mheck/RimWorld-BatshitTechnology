// Batshit Technology - in-game showcase.
// Runs only when RimWorld starts with -btshowcase (Tools/smoke-test.ps1
// -Showcase). On the quick-test map it finishes the mod's research and builds
// a working demo base: a fuelled reactor powering a stocked replicator, the
// Transmogrifier with a bill and rock chunks, a colonist in the Brain
// Squeezer, the Artificer making bolts, and a Ballista with chained,
// loaded magazines firing explosive volleys at open ground. It builds
// AnyRim, the test town, on the best site in the north-west (or logs that
// it skipped it). Then two lone devices in the most open far corners that
// are out of reach of AnyRim self-destruct, well clear of the base, with
// the camera on each as it arms: a Random Replicator five seconds in
// (blast, fires, secondaries), then a Batshittium Reactor twenty seconds
// later (core blast and a barrage of bolts). Next, a colonist throws an
// Ergonomic Singularity Grenade at a junk pile (and a volunteer) west of
// the base, and ten seconds after the throw the camera tours AnyRim from
// its welcome sign. Then it leaves the game running. Log cues for capture
// tooling: [BT-SHOWCASE] READY, ANYRIM (or AnyRim skipped), THROW, TOUR.

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
        /// <summary>The command-line flag that requests the showcase.</summary>
        internal const string Flag = "btshowcase";

        /// <summary>Builds the demo base around the map's center.</summary>
        /// <param name="map">The quick-test map.</param>
        internal static void Build(Map map)
        {
            FinishResearch();
            DebugSettings.godMode = true; // show the DEV buttons
            IntVec3 center = map.Center;
            Clear(map, CellRect.CenteredOn(center, 16));

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

            BuildBallista(map, center);

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

            // AnyRim goes up before the self-destruct demo, which clears
            // the fog and must pick corners out of the town's reach.
            bool town = BuildAnyRim(map, center);
            Thing doomed = BuildSelfDestructDemo(map, center, out int demos);
            Find.CameraDriver.JumpToCurrentMapLoc(doomed?.Position ?? center);
            Find.CameraDriver.SetRootSize(MaxCameraSize);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            Messages.Message(
                OpeningMessage(demos, throwAtTick >= 0, town),
                MessageTypeDefOf.PositiveEvent, false);
            // Tools/smoke-test.ps1 waits for this line before maximizing.
            Log.Message("[BT-SHOWCASE] READY");
        }

        /// <summary>
        /// The tiles a demo corner's self-destruct can reach, counting the
        /// fires and secondary blasts it starts (the replicator's 83.6, the
        /// larger of the two), plus a 4-tile margin. docs/SHOWCASE.md,
        /// "Safety Rules".
        /// </summary>
        internal const double DemoCornerReach = 87.6;

        /// <summary>The quick-test map's size, which the doc's numbers assume.</summary>
        private const float DocMapSize = 250f;

        /// <summary>Game ticks from the grenade throw to the AnyRim tour.</summary>
        private const int TourDelayTicks = 600;

        /// <summary>
        /// The camera's zoom on the tour: Middle (under 42), where the
        /// signs show, and wide enough to take in the town's south half.
        /// </summary>
        private const float TourCameraSize = 30f;

        /// <summary>
        /// How far north of the welcome sign the tour's camera looks, so the
        /// sign sits low in the frame with the town above it.
        /// </summary>
        private const int TourLookNorth = 20;

        /// <summary>The plan AnyRim was built from, or null if it was skipped.</summary>
        private static TownPlan anyRimPlan;

        /// <summary>AnyRim's crossroads, or Invalid if it was skipped.</summary>
        private static IntVec3 anyRimAnchor = IntVec3.Invalid;

        /// <summary>The tick to tour AnyRim at, or -1.</summary>
        private static int tourAtTick = -1;

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
            tourAtTick = -1;
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

        /// <summary>
        /// Points the camera at AnyRim's welcome sign with the town in
        /// view, at a zoom where the signs show, and says what the town is
        /// for.
        /// </summary>
        private static void TourAnyRim()
        {
            Map map = Find.CurrentMap;
            MapComponent_AnyRim record = map?.GetComponent<MapComponent_AnyRim>();
            if (record == null || !record.Built)
            {
                return;
            }

            IntVec3 sign = record.WelcomeSign.IsValid ? record.WelcomeSign : record.Anchor;
            var look = new IntVec3(
                record.Anchor.x, 0, Math.Min(sign.z + TourLookNorth, map.Size.z - 1));
            Find.CameraDriver.JumpToCurrentMapLoc(look);
            Find.CameraDriver.SetRootSize(TourCameraSize);
            try
            {
                Messages.Message(
                    AnyRimText.OpeningMessage, new LookTargets(sign, map),
                    MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception e)
            {
                Log.Error($"[BT] Showcase could not word AnyRim's opening message: {e}");
            }

            Log.Message("[BT-SHOWCASE] TOUR"); // a cue for screen-capture tooling
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
        /// A Ballista with three chained magazines of explosive bolts, firing
        /// 16-bolt volleys at open ground 60 tiles east.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The base's center.</param>
        private static void BuildBallista(Map map, IntVec3 center)
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
            ballista.SetRange(75);
            IntVec3 target = center + new IntVec3(60, 0, 0);
            target.x = System.Math.Min(target.x, map.Size.x - 10);
            ballista.OrderAttack(new LocalTargetInfo(target));
        }

        /// <summary>
        /// Distance from the base to the self-destruct demo, along both axes:
        /// about 106 tiles, clear of the 75-tile blast and the base's own
        /// half-width.
        /// </summary>
        private const int DemoOffset = 75;

        /// <summary>The camera's widest zoom (CameraMapConfig.sizeRange.max).</summary>
        private const float MaxCameraSize = 60f;

        /// <summary>
        /// The camera's zoom during a countdown: close enough to see the
        /// warning band around the device.
        /// </summary>
        private const float CountdownCameraSize = 22f;

        /// <summary>The demo device counting down under the camera, if any.</summary>
        private static Thing watching;

        /// <summary>
        /// Game ticks between the base being ready and the first demo arming:
        /// time for the window to maximize and the viewer to find the rings.
        /// </summary>
        private const int ArmDelayTicks = 300;

        /// <summary>
        /// Game ticks between the replicator demo arming and the reactor demo
        /// arming: its 10 s countdown, the ~6 s aftermath, and a breath.
        /// </summary>
        private const int SecondDemoDelayTicks = 1200;

        private static readonly List<(Thing Device, int ArmAt)> pendingDemos =
            new List<(Thing Device, int ArmAt)>();

        /// <summary>
        /// Game ticks from the reactor demo arming to the grenade throw: its
        /// countdown and barrage, and a breath.
        /// </summary>
        private const int GrenadeDemoDelayTicks = 1200;

        private static Pawn grenadier;
        private static Pawn volunteer;
        private static IntVec3 grenadeTarget;
        private static int throwAtTick = -1;

        /// <summary>
        /// Called every game tick by the runner; arms each self-destruct demo
        /// when its moment comes, with the camera on it.
        /// </summary>
        internal static void Tick()
        {
            if (throwAtTick >= 0 && Find.TickManager.TicksGame >= throwAtTick)
            {
                throwAtTick = -1;
                ThrowGrenade();
            }

            if (tourAtTick >= 0 && Find.TickManager.TicksGame >= tourAtTick)
            {
                tourAtTick = -1;
                TourAnyRim();
            }

            // Pull back to take in the whole blast when it goes off.
            if (watching != null && watching.Destroyed)
            {
                watching = null;
                Find.CameraDriver.SetRootSize(MaxCameraSize);
            }

            for (int i = pendingDemos.Count - 1; i >= 0; i--)
            {
                (Thing device, int armAt) = pendingDemos[i];
                if (Find.TickManager.TicksGame < armAt)
                {
                    continue;
                }

                pendingDemos.RemoveAt(i);
                if (!device.Spawned)
                {
                    continue;
                }

                CompSelfDestruct comp = device.TryGetComp<CompSelfDestruct>();
                MoveColonistsClear(device, comp.Props.blastRadius + 8f);
                Find.CameraDriver.JumpToCurrentMapLoc(device.Position);
                Find.CameraDriver.SetRootSize(CountdownCameraSize);
                Find.Selector.ClearSelection();
                Find.Selector.Select(device, playSound: false);
                watching = device;
                comp.Arm();
            }
        }

        /// <summary>
        /// Two lone devices on small pads in the far corners with the most
        /// open ground, among the corners that every part of AnyRim (as
        /// built) clears by <see cref="DemoCornerReach"/> tiles, the rule
        /// the site search applied (<see cref="TownSite.Clears"/>): a
        /// Random Replicator, armed after <see cref="ArmDelayTicks"/>, then
        /// a Batshittium Reactor, armed after the replicator's blast has
        /// played out. The replicator is selected first so its blast rings
        /// show. Fog is cleared so every blast can be watched. The grenade
        /// demo and the AnyRim tour are scheduled after them.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="baseCenter">The demo base's centre.</param>
        /// <param name="demos">How many self-destruct demos were set up: 0, 1 or 2.</param>
        /// <returns>
        /// The replicator, which the camera starts on, or null if no corner
        /// was safe to use.
        /// </returns>
        private static Thing BuildSelfDestructDemo(Map map, IntVec3 baseCenter, out int demos)
        {
            List<IntVec3> corners = new[]
                {
                    new IntVec3(-DemoOffset, 0, DemoOffset),
                    new IntVec3(DemoOffset, 0, DemoOffset),
                    new IntVec3(-DemoOffset, 0, -DemoOffset),
                    new IntVec3(DemoOffset, 0, -DemoOffset),
                }
                .Select(offset => baseCenter + offset)
                .Where(c => CellRect.CenteredOn(c, 3).InBounds(map))
                .Where(c => anyRimPlan == null || TownSite.Clears(
                    anyRimPlan, AnyRim.ToCell(anyRimAnchor), AnyRim.ToBox(CellRect.SingleCell(c)),
                    DemoCornerReach))
                .OrderByDescending(c => OpenGround(map, c))
                .ToList();
            demos = Math.Min(corners.Count, 2);
            map.fogGrid.ClearAllFog();
            int now = Find.TickManager.TicksGame;
            pendingDemos.Clear();
            watching = null;

            Thing replicator = null;
            if (corners.Count > 0)
            {
                Clear(map, CellRect.CenteredOn(corners[0], 2));
                replicator = Spawn(map, "BT_RandomReplicator", corners[0]);
                pendingDemos.Add((replicator, now + ArmDelayTicks));
                Find.Selector.ClearSelection();
                Find.Selector.Select(replicator, playSound: false);
            }
            else
            {
                Log.Warning("[BT] Showcase: no demo corner is clear of AnyRim; no self-destructs.");
            }

            if (corners.Count > 1)
            {
                Clear(map, CellRect.CenteredOn(corners[1], 2));
                Thing reactor = Spawn(map, "BT_BatshittiumReactor", corners[1]);
                pendingDemos.Add((reactor, now + ArmDelayTicks + SecondDemoDelayTicks));
            }
            else if (corners.Count == 1)
            {
                Log.Message("[BT-SHOWCASE] only one demo corner clears AnyRim; reactor demo skipped");
            }

            int throwAt = now + ArmDelayTicks + SecondDemoDelayTicks + GrenadeDemoDelayTicks;
            BuildGrenadeDemo(map, baseCenter, throwAt);
            tourAtTick = anyRimPlan != null ? throwAt + TourDelayTicks : -1;
            return replicator;
        }

        /// <summary>
        /// The showcase's opening message, saying only what will run: the
        /// replicator and reactor demos that found a corner, the grenade if
        /// a colonist holds one, and the AnyRim tour if the town stands.
        /// </summary>
        /// <param name="demos">Self-destruct demos set up: 0, 1 or 2.</param>
        /// <param name="grenade">Whether the grenade throw is scheduled.</param>
        /// <param name="town">Whether AnyRim was built.</param>
        /// <returns>The message.</returns>
        private static string OpeningMessage(int demos, bool grenade, bool town)
        {
            var steps = new List<string>();
            if (demos >= 1)
            {
                steps.Add("this lone replicator arms its self-destruct in five seconds");
            }

            if (demos >= 2)
            {
                steps.Add(grenade ? "a lone reactor follows it" : "and a lone reactor follows it");
            }

            if (grenade)
            {
                steps.Add(steps.Count > 0
                    ? "then a colonist throws an Ergonomic Singularity Grenade"
                    : "a colonist throws an Ergonomic Singularity Grenade shortly");
            }

            string intro = steps.Count > 0
                ? string.Join(", ", steps) + ". The demo base"
                : "the demo base";
            return "Batshit Technology showcase: " + intro + " is at the map's centre; "
                + "select things there to see their buttons and status."
                + (town
                    ? " AnyRim, a small test town, stands to the north-west; the camera visits it "
                        + (grenade ? "after the grenade." : "in about a minute.")
                    : string.Empty);
        }

        /// <summary>
        /// A junk pile 25 tiles west of the base (stools, a wooden wall and
        /// stacks of steel and wood), and a colonist armed with an Ergonomic
        /// Singularity Grenade, due to throw it at the pile with another
        /// colonist volunteering inside it.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="baseCenter">The demo base's centre.</param>
        /// <param name="throwAt">The tick to throw at.</param>
        private static void BuildGrenadeDemo(Map map, IntVec3 baseCenter, int throwAt)
        {
            throwAtTick = -1; // set below once a grenadier is ready
            grenadeTarget = baseCenter + new IntVec3(-25, 0, 0);
            Clear(map, CellRect.CenteredOn(grenadeTarget, 13));
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
                return;
            }

            grenadier = colonists[0];
            Thing grenade = ThingMaker.MakeThing(
                DefDatabase<ThingDef>.GetNamed("BT_ErgonomicSingularityGrenade"));
            grenadier.equipment.DestroyAllEquipment();
            grenadier.equipment.AddEquipment((ThingWithComps)grenade);
            volunteer = colonists.Count > 1 ? colonists[colonists.Count - 1] : null;
            throwAtTick = throwAt;
        }

        /// <summary>
        /// Brings the grenadier into range, points the camera at the pile,
        /// and orders the throw.
        /// </summary>
        private static void ThrowGrenade()
        {
            if (grenadier == null || grenadier.Dead || !grenadier.Spawned)
            {
                return;
            }

            Teleport(grenadier, grenadeTarget + new IntVec3(12, 0, 0));
            if (volunteer != null && !volunteer.Dead && volunteer.Spawned)
            {
                Teleport(volunteer, grenadeTarget + new IntVec3(-7, 0, -3));
            }

            Find.CameraDriver.JumpToCurrentMapLoc(grenadeTarget);
            Find.CameraDriver.SetRootSize(CountdownCameraSize);
            grenadier.drafter.Drafted = true;
            Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, grenadeTarget);
            job.maxNumStaticAttacks = 1;
            grenadier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            Log.Message("[BT-SHOWCASE] THROW"); // a cue for screen-capture tooling
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
        /// Showcase only: colonists who wandered into a demo's blast radius
        /// are moved back to the base, so the demo kills no one.
        /// </summary>
        /// <param name="device">The demo device about to arm.</param>
        /// <param name="radius">How far out counts as in danger.</param>
        private static void MoveColonistsClear(Thing device, float radius)
        {
            Map map = device.Map;
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (!pawn.Position.InHorDistOf(device.Position, radius)
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
        private static void SendColonistToSqueezer(Map map, Thing squeezer)
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

        /// <summary>Clears a square to concrete, keeping pawns.</summary>
        /// <param name="map">The map.</param>
        /// <param name="rect">The area.</param>
        private static void Clear(Map map, CellRect rect)
        {
            foreach (IntVec3 c in rect)
            {
                if (!c.InBounds(map))
                {
                    continue;
                }

                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    // Steam geysers and the like can't be destroyed; trying
                    // logs an error (and pops the debug log over the demo).
                    if (!(t is Pawn) && t.def.destroyable)
                    {
                        t.Destroy();
                    }
                }

                map.terrainGrid.SetTerrain(c, TerrainDefOf.Concrete);
                map.roofGrid.SetRoof(c, null);
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
