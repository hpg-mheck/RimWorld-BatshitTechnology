// Batshit Technology - Batshit Ballista.
// A turret that fires a volley of insanely inaccurate bolts once every five
// seconds. The player picks the range (10 to 325 tiles), the projectile count
// (1 to 256) and the bolt type (Painful: kinetic; Really Painful: explosive).
// Bolts come from its 16-bolt internal magazine and from Ballista Magazines
// chained to it by shared edges; magazines are drained first. Range and
// count set the power draw (Core.BallistaPower): a standby draw, and a
// one-second firing burst per volley that the grid must be able to supply,
// or the Ballista holds fire.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace BatshitTechnology
{
    /// <summary>Ballista defs used from code.</summary>
    [DefOf]
    public static class BT_BallistaDefOf
    {
        /// <summary>Kinetic bolts.</summary>
        public static ThingDef BT_BoltPainful;

        /// <summary>Explosive bolts.</summary>
        public static ThingDef BT_BoltReallyPainful;

        /// <summary>The magazine building.</summary>
        public static ThingDef BT_BallistaMagazine;

        /// <summary>Lets RimWorld bind the fields above at load.</summary>
        static BT_BallistaDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_BallistaDefOf));
        }
    }

    /// <summary>XML on a bolt item: the projectile it becomes when fired.</summary>
    public class BoltExtension : DefModExtension
    {
        /// <summary>The projectile ThingDef.</summary>
        public ThingDef projectile;
    }

    /// <summary>XML on the Ballista: volley, scatter and burst settings.</summary>
    public class BallistaExtension : DefModExtension
    {
        /// <summary>Range presets offered by the range gizmo, in tiles.</summary>
        public List<int> rangePresets = new List<int>();

        /// <summary>Projectile-count presets offered by the count gizmo.</summary>
        public List<int> countPresets = new List<int>();

        /// <summary>Range a new Ballista starts at.</summary>
        public int defaultRange = 30;

        /// <summary>Projectiles a new Ballista starts with.</summary>
        public int defaultCount = 4;

        /// <summary>Scatter radius at point-blank range, in tiles.</summary>
        public float scatterBase = 2f;

        /// <summary>Extra scatter radius per tile of distance.</summary>
        public float scatterPerTile = 0.25f;

        /// <summary>How long a volley's firing burst draws power, in ticks.</summary>
        public int burstTicks = 60;
    }

    /// <summary>XML on the magazine: its standby and feeding draw.</summary>
    public class MagazinePowerExtension : DefModExtension
    {
        /// <summary>Watts while idle.</summary>
        public float standbyWatts = 5f;

        /// <summary>Watts while feeding a volley.</summary>
        public float feedingWatts = 500f;
    }

    /// <summary>
    /// The Batshit Ballista: a vanilla turret with a per-building range,
    /// a bolt supply drawn from its own and chained magazines, and a
    /// range-and-count power draw.
    /// </summary>
    public class Building_BatshittiumBallista : Building_TurretGun, IHaulDestination,
        IHaulEnroute, IThingHolder, ISearchableContents
    {
        private readonly BoltStorage storage;
        private int selectedRange = -1;
        private int selectedCount = -1;
        private ThingDef ammoDef;
        private int burstTicksLeft;
        private int lastPowerMessageTick = -99999; // powerComp is inherited
        private static readonly Color MagazineLinkColor = new Color(0.85f, 0.6f, 1f);

        /// <summary>Volleys fired this session (smoke test).</summary>
        internal static int VolleysFired;

        /// <summary>Bolts fired this session (smoke test).</summary>
        internal static int BoltsFired;

        /// <summary>Volleys held for lack of power this session (smoke test).</summary>
        internal static int VolleysHeldForPower;

        /// <summary>Creates the Ballista with an empty internal magazine.</summary>
        public Building_BatshittiumBallista()
        {
            storage = new BoltStorage(this);
        }

        /// <summary>The XML settings.</summary>
        private BallistaExtension Settings =>
            def.GetModExtension<BallistaExtension>() ?? new BallistaExtension();

        /// <summary>Range in tiles, within the selectable limits.</summary>
        public int Range => Mathf.Clamp(
            selectedRange > 0 ? selectedRange : Settings.defaultRange,
            BallistaPower.MinRange, BallistaPower.MaxRange);

        /// <summary>Projectiles per volley, within the selectable limits.</summary>
        public int Count => Mathf.Clamp(
            selectedCount > 0 ? selectedCount : Settings.defaultCount,
            BallistaPower.MinCount, BallistaPower.MaxCount);

        /// <summary>The bolt type fired.</summary>
        public ThingDef AmmoDef => ammoDef ?? BT_BallistaDefOf.BT_BoltPainful;

        /// <summary>Standby draw at the current settings.</summary>
        public float StandbyWatts => (float)BallistaPower.StandbyWatts(Count, Range);

        /// <summary>Firing-burst draw at the current settings.</summary>
        public float FiringWatts => (float)BallistaPower.FiringWatts(Count, Range);

        /// <summary>True during a volley's firing burst.</summary>
        public bool InBurst => burstTicksLeft > 0;

        /// <summary>The internal magazine's bolts.</summary>
        internal BoltStorage InternalMagazine => storage;

        // ===== Storage (internal magazine) =====

        /// <summary>The internal magazine's bolts.</summary>
        /// <returns>The container.</returns>
        public ThingOwner GetDirectlyHeldThings() => storage.Container;

        /// <summary>Adds holders nested inside the bolts (none, normally).</summary>
        /// <param name="outChildren">List to append to.</param>
        public void GetChildHolders(List<IThingHolder> outChildren) =>
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        /// <summary>Lets the map search find the bolts.</summary>
        public ThingOwner SearchableContents => storage.Container;

        /// <summary>Haulers bring bolts here.</summary>
        public bool HaulDestinationEnabled => true;

        /// <summary>Shows the storage tab.</summary>
        public bool StorageTabVisible => true;

        /// <summary>The internal magazine's settings.</summary>
        /// <returns>The settings.</returns>
        public StorageSettings GetStoreSettings() => storage.Settings;

        /// <summary>The fixed limits the storage tab offers (bolts only).</summary>
        /// <returns>The Def's fixed settings.</returns>
        public StorageSettings GetParentStoreSettings() => def.building.fixedStorageSettings;

        /// <summary>Nothing to update when settings change.</summary>
        public void Notify_SettingsChanged()
        {
        }

        /// <summary>Takes allowed bolts while there is room.</summary>
        /// <param name="t">The item.</param>
        /// <returns>True if it belongs here.</returns>
        public bool Accepts(Thing t) => storage.Accepts(t);

        /// <summary>Room left, for haulers' en-route bookkeeping.</summary>
        /// <param name="stuff">The item type (any bolt).</param>
        /// <returns>Bolts that still fit.</returns>
        public int SpaceRemainingFor(ThingDef stuff) => storage.Container.SpaceLeft;

        // ===== Lifecycle =====

        /// <summary>Sets up storage settings and the turret's gun.</summary>
        public override void PostMake()
        {
            base.PostMake();
            storage.PostMake();
        }

        /// <summary>Caches power and applies this Ballista's range to its verb.</summary>
        /// <param name="map">The map.</param>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad); // also sets powerComp
            storage.Restore();
            ApplyRange();
            UpdatePowerDraw();
        }

        /// <summary>Spills the internal magazine when removed, unless it cooks off.</summary>
        /// <param name="mode">How it is leaving.</param>
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // Destroyed by damage: the death blast cooks the bolts off
            // instead of spilling them.
            if (mode != DestroyMode.KillFinalize
                || !(GetComp<CompDeathBlast>()?.ConsumesContents ?? false))
            {
                storage.Spill(mode);
            }

            base.DeSpawn(mode);
        }

        /// <summary>Saves and loads settings, bolts and the burst.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            storage.ExposeData();
            Scribe_Values.Look(ref selectedRange, "selectedRange", -1);
            Scribe_Values.Look(ref selectedCount, "selectedCount", -1);
            Scribe_Defs.Look(ref ammoDef, "ammoDef");
            Scribe_Values.Look(ref burstTicksLeft, "burstTicksLeft", 0);
        }

        /// <summary>Counts down the burst and keeps the power draw current.</summary>
        protected override void Tick()
        {
            // Minified (uninstalled) buildings still tick inside their package in
            // 1.6, with no map; nothing here makes sense off the map.
            if (!Spawned)
            {
                return;
            }

            base.Tick();
            if (burstTicksLeft > 0)
            {
                burstTicksLeft--;
            }

            UpdatePowerDraw();
        }

        /// <summary>The player can always pick a target for their own Ballista.</summary>
        protected override bool CanSetForcedTarget => Faction == Faction.OfPlayer;

        /// <summary>
        /// Gives this Ballista's verb its own copy of the verb properties,
        /// with the selected range. Vanilla shares one per gun Def.
        /// </summary>
        private void ApplyRange()
        {
            Verb verb = AttackVerb;
            VerbProperties template = def.building.turretGunDef.Verbs.FirstOrDefault();
            if (verb == null || template == null)
            {
                return;
            }

            VerbProperties own = Gen.MemberwiseClone(template);
            own.range = Range;
            verb.verbProps = own;
        }

        /// <summary>
        /// Standby or firing draw, re-applied every tick: the power net resets
        /// PowerOutput whenever it rewires.
        /// </summary>
        private void UpdatePowerDraw()
        {
            if (powerComp == null)
            {
                return;
            }

            float draw = InBurst ? FiringWatts : StandbyWatts;
            if (powerComp.PowerOutput != -draw)
            {
                powerComp.PowerOutput = -draw;
            }
        }

        // ===== Volleys (called by Verb_BallistaVolley) =====

        /// <summary>
        /// Starts a firing burst if the grid can supply it: either its surplus
        /// covers the extra draw, or stored battery energy covers the shortfall
        /// for the burst's length. Otherwise holds fire (with a message at most
        /// once a minute).
        /// </summary>
        /// <returns>True if the burst started.</returns>
        internal bool TryStartBurst()
        {
            PowerNet net = powerComp?.PowerNet;
            if (net == null || !powerComp.PowerOn)
            {
                return false;
            }

            float perTick = CompPower.WattsToWattDaysPerTick;
            float surplusWatts = net.CurrentEnergyGainRate() / perTick;
            float extraWatts = FiringWatts - StandbyWatts;
            float shortfallWattDays =
                Mathf.Max(0f, extraWatts - surplusWatts) * Settings.burstTicks * perTick;
            if (shortfallWattDays > net.CurrentStoredEnergy())
            {
                VolleysHeldForPower++;
                if (Find.TickManager.TicksGame - lastPowerMessageTick > 3600)
                {
                    lastPowerMessageTick = Find.TickManager.TicksGame;
                    Messages.Message(
                        $"{LabelCap} is holding fire: a volley needs {FiringWatts:N0} W "
                            + "for a second, more than the grid and its batteries can supply.",
                        this, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            burstTicksLeft = Settings.burstTicks;
            UpdatePowerDraw();
            return true;
        }

        /// <summary>Ends a burst that fired nothing (no bolts).</summary>
        internal void CancelBurst()
        {
            burstTicksLeft = 0;
            UpdatePowerDraw();
        }

        /// <summary>
        /// Takes up to <paramref name="wanted"/> bolts of the selected type:
        /// powered connected magazines first (nearest links first), then the
        /// internal magazine. Magazines that contribute draw feeding power.
        /// </summary>
        /// <param name="wanted">Bolts wanted.</param>
        /// <returns>Bolts taken.</returns>
        internal int DrawBolts(int wanted)
        {
            int taken = 0;
            foreach (Building_BallistaMagazine magazine in ConnectedMagazines())
            {
                if (taken >= wanted)
                {
                    break;
                }

                int n = magazine.Storage.Take(AmmoDef, wanted - taken);
                if (n > 0)
                {
                    magazine.Notify_Feeding(Settings.burstTicks);
                    taken += n;
                }
            }

            taken += storage.Take(AmmoDef, wanted - taken);
            return taken;
        }

        /// <summary>Records a fired volley.</summary>
        /// <param name="bolts">Bolts in it.</param>
        internal void Notify_VolleyFired(int bolts)
        {
            VolleysFired++;
            BoltsFired += bolts;
        }

        /// <summary>Scatter radius at a distance: insanely inaccurate.</summary>
        /// <param name="distance">Tiles to the target.</param>
        /// <returns>Radius in tiles.</returns>
        internal float ScatterRadius(float distance) =>
            Settings.scatterBase + (Settings.scatterPerTile * distance);

        /// <summary>
        /// Powered magazines connected to this Ballista: sharing an edge with
        /// it or with another connected magazine (Core.MagazineNetwork).
        /// </summary>
        /// <returns>The magazines, nearest links first.</returns>
        internal List<Building_BallistaMagazine> ConnectedMagazines()
        {
            var byCell = new Dictionary<(int x, int z), Building_BallistaMagazine>();
            foreach (Thing t in Map.listerThings.ThingsOfDef(BT_BallistaDefOf.BT_BallistaMagazine))
            {
                if (t is Building_BallistaMagazine magazine && magazine.CanFeed
                    && magazine.Faction == Faction)
                {
                    byCell[(t.Position.x, t.Position.z)] = magazine;
                }
            }

            if (byCell.Count == 0)
            {
                return new List<Building_BallistaMagazine>();
            }

            IEnumerable<(int x, int z)> own = this.OccupiedRect().Cells.Select(c => (c.x, c.z));
            return MagazineNetwork.Connected(own, new HashSet<(int x, int z)>(byCell.Keys))
                .Select(cell => byCell[cell])
                .ToList();
        }

        // ===== UI =====

        /// <summary>
        /// Vanilla rings below 90 tiles; a plain circle beyond, where the cell
        /// ring cannot reach. Also outlines the connected magazines.
        /// </summary>
        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            if (Range >= 90)
            {
                GenDraw.DrawCircleOutline(this.TrueCenter(), Range, SimpleColor.White);
            }

            List<IntVec3> cells = ConnectedMagazines().Select(m => m.Position).ToList();
            if (cells.Count > 0)
            {
                GenDraw.DrawFieldEdges(cells, MagazineLinkColor);
            }
        }

        /// <summary>Volley, power and ammunition status.</summary>
        /// <returns>Inspect pane text.</returns>
        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            List<Building_BallistaMagazine> magazines = ConnectedMagazines();
            int linked = magazines.Sum(m => m.Storage.CountOf(AmmoDef));
            var lines = new List<string>
            {
                $"Volley: {Count} x {AmmoDef.label} at up to {Range} tiles",
                $"Power: {StandbyWatts:N0} W standby, {FiringWatts:N0} W per volley"
                    + (InBurst ? " (firing)" : ""),
                "Internal magazine: " + storage.InspectLine(),
                $"Connected magazines: {magazines.Count} ({linked} {AmmoDef.label})",
            };
            return string.IsNullOrEmpty(text)
                ? string.Join("\n", lines)
                : text + "\n" + string.Join("\n", lines);
        }

        /// <summary>Turret gizmos plus range, count, ammo and storage controls.</summary>
        /// <returns>The gizmos.</returns>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            if (Faction != Faction.OfPlayer)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = $"Range: {Range}",
                defaultDesc = "Pick how far the Ballista reaches. Range multiplies "
                    + "its standby and firing power.",
                icon = BT_Textures.BallistaRange,
                action = () => Find.WindowStack.Add(new FloatMenu(Settings.rangePresets
                    .Select(r => new FloatMenuOption(
                        $"{r} tiles: {BallistaPower.StandbyWatts(Count, r):N0} W standby, "
                            + $"{BallistaPower.FiringWatts(Count, r):N0} W per volley",
                        () => SetRange(r)))
                    .ToList()))
            };
            yield return new Command_Action
            {
                defaultLabel = $"Projectiles: {Count}",
                defaultDesc = "Pick how many bolts each volley fires. The count "
                    + "multiplies its standby and firing power.",
                icon = BT_Textures.BallistaCount,
                action = () => Find.WindowStack.Add(new FloatMenu(Settings.countPresets
                    .Select(c => new FloatMenuOption(
                        $"{c}: {BallistaPower.StandbyWatts(c, Range):N0} W standby, "
                            + $"{BallistaPower.FiringWatts(c, Range):N0} W per volley",
                        () => SetCount(c)))
                    .ToList()))
            };
            yield return new Command_Action
            {
                defaultLabel = "Ammo: " + AmmoDef.LabelCap,
                defaultDesc = "Switch between Painful (kinetic) and Really Painful "
                    + "(explosive) bolts.",
                icon = AmmoDef.uiIcon,
                action = () => SetAmmo(AmmoDef == BT_BallistaDefOf.BT_BoltPainful
                    ? BT_BallistaDefOf.BT_BoltReallyPainful
                    : BT_BallistaDefOf.BT_BoltPainful)
            };
            foreach (Gizmo g in StorageSettingsClipboard.CopyPasteGizmosFor(storage.Settings))
            {
                yield return g;
            }
        }

        /// <summary>Sets the range and re-applies it to the verb.</summary>
        /// <param name="range">Tiles.</param>
        internal void SetRange(int range)
        {
            selectedRange = range;
            ApplyRange();
            UpdatePowerDraw();
        }

        /// <summary>Sets the projectiles per volley.</summary>
        /// <param name="count">Projectiles.</param>
        internal void SetCount(int count)
        {
            selectedCount = count;
            UpdatePowerDraw();
        }

        /// <summary>Sets the bolt type fired.</summary>
        /// <param name="bolt">The bolt item def.</param>
        internal void SetAmmo(ThingDef bolt) => ammoDef = bolt;
    }

    /// <summary>
    /// The Ballista Magazine: bolt storage that feeds any Ballista its edge
    /// chain touches, drawing feeding power while it does.
    /// </summary>
    public class Building_BallistaMagazine : Building, IHaulDestination, IHaulEnroute,
        IThingHolder, ISearchableContents
    {
        private readonly BoltStorage storage;
        private int feedingTicksLeft;
        private CompPowerTrader powerComp;

        /// <summary>Creates an empty magazine.</summary>
        public Building_BallistaMagazine()
        {
            storage = new BoltStorage(this);
        }

        /// <summary>The bolts.</summary>
        internal BoltStorage Storage => storage;

        /// <summary>A magazine feeds only while powered.</summary>
        public bool CanFeed => powerComp == null || powerComp.PowerOn;

        /// <summary>True while feeding a volley.</summary>
        public bool IsFeeding => feedingTicksLeft > 0;

        /// <summary>The XML power settings.</summary>
        private MagazinePowerExtension PowerSettings =>
            def.GetModExtension<MagazinePowerExtension>() ?? new MagazinePowerExtension();

        /// <summary>The bolts.</summary>
        /// <returns>The container.</returns>
        public ThingOwner GetDirectlyHeldThings() => storage.Container;

        /// <summary>Adds holders nested inside the bolts (none, normally).</summary>
        /// <param name="outChildren">List to append to.</param>
        public void GetChildHolders(List<IThingHolder> outChildren) =>
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        /// <summary>Lets the map search find the bolts.</summary>
        public ThingOwner SearchableContents => storage.Container;

        /// <summary>Haulers bring bolts here.</summary>
        public bool HaulDestinationEnabled => true;

        /// <summary>Shows the storage tab.</summary>
        public bool StorageTabVisible => true;

        /// <summary>The magazine's settings.</summary>
        /// <returns>The settings.</returns>
        public StorageSettings GetStoreSettings() => storage.Settings;

        /// <summary>The fixed limits the storage tab offers (bolts only).</summary>
        /// <returns>The Def's fixed settings.</returns>
        public StorageSettings GetParentStoreSettings() => def.building.fixedStorageSettings;

        /// <summary>Nothing to update when settings change.</summary>
        public void Notify_SettingsChanged()
        {
        }

        /// <summary>Takes allowed bolts while there is room.</summary>
        /// <param name="t">The item.</param>
        /// <returns>True if it belongs here.</returns>
        public bool Accepts(Thing t) => storage.Accepts(t);

        /// <summary>Room left, for haulers' en-route bookkeeping.</summary>
        /// <param name="stuff">The item type (any bolt).</param>
        /// <returns>Bolts that still fit.</returns>
        public int SpaceRemainingFor(ThingDef stuff) => storage.Container.SpaceLeft;

        /// <summary>Draws feeding power for a volley's burst.</summary>
        /// <param name="ticks">How long.</param>
        internal void Notify_Feeding(int ticks)
        {
            feedingTicksLeft = Math.Max(feedingTicksLeft, ticks);
            UpdatePowerDraw();
        }

        /// <summary>Sets up storage settings.</summary>
        public override void PostMake()
        {
            base.PostMake();
            storage.PostMake();
        }

        /// <summary>Caches power and applies the draw.</summary>
        /// <param name="map">The map.</param>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            storage.Restore();
            UpdatePowerDraw();
        }

        /// <summary>Spills the bolts when removed, unless they cook off.</summary>
        /// <param name="mode">How it is leaving.</param>
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // Destroyed by damage: the death blast cooks the bolts off
            // instead of spilling them.
            if (mode != DestroyMode.KillFinalize
                || !(GetComp<CompDeathBlast>()?.ConsumesContents ?? false))
            {
                storage.Spill(mode);
            }

            base.DeSpawn(mode);
        }

        /// <summary>Saves and loads the bolts and the feeding state.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            storage.ExposeData();
            Scribe_Values.Look(ref feedingTicksLeft, "feedingTicksLeft", 0);
        }

        /// <summary>Counts down feeding and keeps the draw current.</summary>
        protected override void Tick()
        {
            // Minified (uninstalled) buildings still tick inside their package in
            // 1.6, with no map; nothing here makes sense off the map.
            if (!Spawned)
            {
                return;
            }

            base.Tick();
            if (feedingTicksLeft > 0)
            {
                feedingTicksLeft--;
            }

            UpdatePowerDraw();
        }

        /// <summary>
        /// Standby or feeding draw, re-applied every tick: the power net resets
        /// PowerOutput whenever it rewires.
        /// </summary>
        private void UpdatePowerDraw()
        {
            if (powerComp == null)
            {
                return;
            }

            float draw = IsFeeding ? PowerSettings.feedingWatts : PowerSettings.standbyWatts;
            if (powerComp.PowerOutput != -draw)
            {
                powerComp.PowerOutput = -draw;
            }
        }

        /// <summary>Stored bolts and feeding state.</summary>
        /// <returns>Inspect pane text.</returns>
        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            string line = storage.InspectLine() + (IsFeeding ? " (feeding)" : "");
            return string.IsNullOrEmpty(text) ? line : text + "\n" + line;
        }

        /// <summary>Adds copy/paste buttons for the storage settings.</summary>
        /// <returns>The gizmos.</returns>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            foreach (Gizmo g in StorageSettingsClipboard.CopyPasteGizmosFor(storage.Settings))
            {
                yield return g;
            }
        }
    }

    /// <summary>
    /// The Ballista's verb: one cast fires a whole volley. Each bolt flies
    /// to its own random cell around the target, inside a scatter radius
    /// that grows with distance.
    /// </summary>
    public class Verb_BallistaVolley : Verb_LaunchProjectile
    {
        /// <summary>Fires one volley, if there is power and ammunition.</summary>
        /// <returns>True if anything was fired.</returns>
        protected override bool TryCastShot()
        {
            if (!(caster is Building_BatshittiumBallista ballista)
                || (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map))
            {
                return false;
            }

            ThingDef projectileDef = ballista.AmmoDef.GetModExtension<BoltExtension>()?.projectile;
            if (projectileDef == null || !ballista.TryStartBurst())
            {
                return false;
            }

            int bolts = ballista.DrawBolts(ballista.Count);
            if (bolts == 0)
            {
                ballista.CancelBurst();
                return false;
            }

            Map map = caster.Map;
            IntVec3 target = currentTarget.Cell;
            float scatter = ballista.ScatterRadius((target - caster.Position).LengthHorizontal);
            for (int i = 0; i < bolts; i++)
            {
                Vector2 offset = Rand.InsideUnitCircle * scatter;
                var cell = new IntVec3(
                    Mathf.Clamp(target.x + Mathf.RoundToInt(offset.x), 0, map.Size.x - 1),
                    0,
                    Mathf.Clamp(target.z + Mathf.RoundToInt(offset.y), 0, map.Size.z - 1));
                var projectile = (Projectile)GenSpawn.Spawn(projectileDef, caster.Position, map);
                projectile.Launch(
                    caster, caster.DrawPos, cell, currentTarget, ProjectileHitFlags.All,
                    false, EquipmentSource);
            }

            lastShotTick = Find.TickManager.TicksGame;
            ballista.Notify_VolleyFired(bolts);
            return true;
        }
    }
}
