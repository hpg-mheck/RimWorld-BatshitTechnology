// Batshit Technology - self-destruct, shared by every device that has one.
// CompSelfDestruct runs the order, the pawn arming job, the countdown (alarm,
// camera shake, fleeing pawns, blast ring and the pulsing warning around the
// device) and hands off to the def's BatshitDetonation (Detonations.cs). Also
// here: the DefOfs, the vanilla textures and the overlay map component.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace BatshitTechnology
{
    /// <summary>The self-destruct order's designation.</summary>
    [DefOf]
    public static class BT_DesignationDefOf
    {
        /// <summary>Marks a device for a colonist to arm.</summary>
        public static DesignationDef BT_SelfDestruct;

        /// <summary>Lets RimWorld bind the field above at load.</summary>
        static BT_DesignationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_DesignationDefOf));
        }
    }

    /// <summary>The self-destruct arming job.</summary>
    [DefOf]
    public static class BT_JobDefOf
    {
        /// <summary>A colonist arming a device's self-destruct.</summary>
        public static JobDef BT_ArmSelfDestruct;

        /// <summary>Lets RimWorld bind the field above at load.</summary>
        static BT_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_JobDefOf));
        }
    }

    /// <summary>Vanilla textures, loaded on the main thread at startup.</summary>
    [StaticConstructorOnStartup]
    public static class BT_Textures
    {
        /// <summary>Arm self-destruct.</summary>
        public static readonly Texture2D ArmSelfDestruct =
            ContentFinder<Texture2D>.Get("UI/Commands/Detonate");

        /// <summary>Cancel the order or abort the countdown.</summary>
        public static readonly Texture2D Cancel =
            ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        /// <summary>Eject food, and the auto-eject toggle.</summary>
        public static readonly Texture2D Eject =
            ContentFinder<Texture2D>.Get("UI/Commands/PodEject");

        /// <summary>The refrigeration toggle.</summary>
        public static readonly Texture2D Refrigeration =
            ContentFinder<Texture2D>.Get("UI/Commands/TempLower");

        /// <summary>The Ballista's range picker.</summary>
        public static readonly Texture2D BallistaRange =
            ContentFinder<Texture2D>.Get("UI/Commands/Attack");

        /// <summary>The Ballista's projectile-count picker.</summary>
        public static readonly Texture2D BallistaCount =
            ContentFinder<Texture2D>.Get("UI/Commands/SquadAttack");
    }

    /// <summary>
    /// XML-facing properties for <see cref="CompSelfDestruct"/>: the order,
    /// countdown and warning, plus what happens at zero.
    /// </summary>
    public class CompProperties_SelfDestruct : CompProperties
    {
        /// <summary>Ticks of pawn work to arm it.</summary>
        public int armingTicks = 120;

        /// <summary>Countdown length, in seconds.</summary>
        public float countdownSeconds = 10f;

        /// <summary>
        /// In the last this-many seconds the warning turns red and pulses
        /// faster.
        /// </summary>
        public float finalWarningSeconds = 3f;

        /// <summary>Warning pulse period, in seconds, before the final phase.</summary>
        public float pulsePeriodSeconds = 1f;

        /// <summary>Warning pulse period, in seconds, in the final phase.</summary>
        public float finalPulsePeriodSeconds = 0.25f;

        /// <summary>Pawns this close run during the countdown.</summary>
        public float fleeRadius = 25f;

        /// <summary>
        /// The blast radius in tiles: drawn as the blast ring, and used by
        /// the detonation. Capped at GenRadial.MaxRadialPatternRadius
        /// (~79.8 in 1.6), the largest ring the engine draws.
        /// </summary>
        public float blastRadius = 75f;

        /// <summary>
        /// Chance, per damaging hit, that the self-destruct triggers on its
        /// own: a countdown, or (see below) an immediate detonation. Also
        /// applies to a device already counting down.
        /// </summary>
        public float damageTriggerChance = 0.05f;

        /// <summary>
        /// Chance that a damage trigger skips the countdown and detonates at
        /// once.
        /// </summary>
        public float damageSkipCountdownChance = 0.5f;

        /// <summary>What happens at zero.</summary>
        public BatshitDetonation detonation;

        /// <summary>Binds these properties to their comp class.</summary>
        public CompProperties_SelfDestruct()
        {
            compClass = typeof(CompSelfDestruct);
        }

        /// <summary>Reports Def mistakes at load.</summary>
        /// <param name="parentDef">The ThingDef carrying this comp.</param>
        /// <returns>One message per configuration error.</returns>
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (detonation == null)
            {
                yield return "self-destruct needs a detonation";
            }
            else
            {
                foreach (string error in detonation.ConfigErrors())
                {
                    yield return error;
                }
            }

            if (Mathf.Max(blastRadius, fleeRadius) > GenRadial.MaxRadialPatternRadius)
            {
                yield return "blastRadius and fleeRadius must not exceed the "
                    + $"engine maximum of {GenRadial.MaxRadialPatternRadius:F1}";
            }

            if (countdownSeconds <= 0f || finalWarningSeconds < 0f)
            {
                yield return "countdownSeconds must be positive and "
                    + "finalWarningSeconds not negative";
            }

            if (pulsePeriodSeconds <= 0f || finalPulsePeriodSeconds <= 0f)
            {
                yield return "pulse periods must be positive";
            }

            if (damageTriggerChance < 0f || damageTriggerChance > 1f
                || damageSkipCountdownChance < 0f || damageSkipCountdownChance > 1f)
            {
                yield return "damage trigger chances must be between 0 and 1";
            }

            if (parentDef.tickerType != TickerType.Normal)
            {
                yield return "a self-destructing building needs tickerType "
                    + "Normal for its countdown";
            }

            if (parentDef.comps.LastOrDefault() != this)
            {
                // Comps tick in order, and the device is destroyed mid-loop
                // at zero; ticking last keeps the others off a dead thing.
                yield return "CompProperties_SelfDestruct must be the last comp";
            }
        }
    }

    /// <summary>
    /// A device's self-destruct: the player orders it, a colonist arms it,
    /// it counts down with an alarm, a blast ring and a pulsing warning
    /// around the device, sends nearby pawns running, and at zero destroys
    /// the device and anything stored in it and sets off its detonation.
    /// Damage can trigger it, and destroying an armed device sets it off.
    /// </summary>
    public class CompSelfDestruct : ThingComp
    {
        private const int TicksPerSecond = 60;

        // How many opacity levels the warning uses; each colour and level
        // is one pooled material.
        private const int PulseLevels = 12;

        private static readonly Color BlastRingColor = new Color(1f, 0.25f, 0.15f);
        private static readonly Color BlastRingDimColor = new Color(1f, 0.25f, 0.15f, 0.3f);
        private static readonly Color FleeRingColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color WarningColor = new Color(1f, 0.62f, 0.1f);   // yellow-orange
        private static readonly Color FinalWarningColor = new Color(1f, 0.1f, 0.06f); // red

        private int ticksLeft; // > 0 while counting down
        private bool detonateOnDestroy; // destroyed by damage while armed
        private Sustainer alarmSustainer;
        private List<IntVec3> warningOutline;
        private List<IntVec3> warningBand;

        /// <summary>Typed access to this comp's XML properties.</summary>
        public CompProperties_SelfDestruct Props => (CompProperties_SelfDestruct)props;

        /// <summary>True while the countdown is running.</summary>
        public bool IsCountingDown => ticksLeft > 0;

        /// <summary>Damage triggers this session, across all devices (diagnostics).</summary>
        public static int DamageTriggers { get; private set; }

        /// <summary>Countdown seconds remaining; zero when not counting down.</summary>
        public float SecondsLeft => ticksLeft / (float)TicksPerSecond;

        /// <summary>The pending order, if any.</summary>
        private Designation Order =>
            parent.Map?.designationManager.DesignationOn(
                parent, BT_DesignationDefOf.BT_SelfDestruct);

        // ===== Lifecycle =====

        /// <summary>Resumes a countdown's overlay after a load.</summary>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            warningOutline = null; // rebuilt for the new position on first draw
            if (IsCountingDown)
            {
                Overlay(parent.Map).Register(this);
            }
        }

        /// <summary>
        /// Leaving the map ends the countdown; if it is leaving because it
        /// was destroyed while armed, it goes off (see PostDestroy).
        /// Uninstalling or deconstructing an armed device disarms it.
        /// </summary>
        /// <param name="map">The map being left.</param>
        /// <param name="mode">How the building is leaving.</param>
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            detonateOnDestroy = IsCountingDown && mode == DestroyMode.KillFinalize;
            StopCountdown(map);
        }

        /// <summary>
        /// Destroyed by damage while armed: detonates where it stood, as
        /// vanilla explosives do when their wick is burning.
        /// </summary>
        /// <param name="mode">How it was destroyed.</param>
        /// <param name="previousMap">The map it was on.</param>
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (detonateOnDestroy && previousMap != null)
            {
                detonateOnDestroy = false;
                Props.detonation.Detonate(parent, previousMap, parent.Position, Props.blastRadius);
            }
        }

        /// <summary>
        /// Every damaging hit may trigger the self-destruct: a countdown, or
        /// half the time an immediate detonation (on the next tick, so the
        /// hit finishes applying first).
        /// </summary>
        /// <param name="dinfo">The hit.</param>
        /// <param name="totalDamageDealt">Damage actually dealt.</param>
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            if (!parent.Spawned || totalDamageDealt <= 0f
                || !Rand.Chance(Props.damageTriggerChance))
            {
                return;
            }

            DamageTriggers++;
            if (Rand.Chance(Props.damageSkipCountdownChance))
            {
                if (!IsCountingDown)
                {
                    Overlay(parent.Map).Register(this);
                }

                ticksLeft = 1; // detonates on the next tick
                return;
            }

            if (!IsCountingDown)
            {
                Arm($"{parent.LabelCap} was damaged and its self-destruct has triggered");
            }
        }

        /// <summary>Saves and loads the countdown.</summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            // Same key as v0.7's replicator comp, so its saves carry over.
            Scribe_Values.Look(ref ticksLeft, "selfDestructTicksLeft", 0);
        }

        /// <summary>Runs the countdown, if there is one.</summary>
        public override void CompTick()
        {
            base.CompTick();
            if (IsCountingDown && parent.Spawned)
            {
                TickCountdown();
            }
        }

        // ===== Countdown =====

        /// <summary>
        /// Starts the countdown. Called by JobDriver_ArmSelfDestruct once a
        /// pawn finishes arming, and by the smoke test and showcase.
        /// </summary>
        public void Arm() => Arm(null);

        /// <summary>Starts the countdown, with an optional reason for the letter.</summary>
        /// <param name="reason">Why it started, or null for a player order.</param>
        private void Arm(string reason)
        {
            if (IsCountingDown || !parent.Spawned)
            {
                return;
            }

            ticksLeft = Mathf.Max(1, Mathf.RoundToInt(Props.countdownSeconds * TicksPerSecond));
            Overlay(parent.Map).Register(this);
            SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
            string why = reason == null ? "" : reason + ". ";
            Find.LetterStack.ReceiveLetter(
                reason == null ? "Self-destruct armed" : "Self-destruct triggered",
                $"{why}{parent.LabelCap} will detonate in {Props.countdownSeconds:0.#} "
                    + $"seconds: {Props.detonation.Describe(Props.blastRadius)}. "
                    + $"Everything within {Props.blastRadius:0} tiles is in danger."
                    + "\n\nAbort it from the device, or get clear.",
                LetterDefOf.ThreatBig, parent);
        }

        /// <summary>Stops a running countdown at the player's request.</summary>
        public void Abort()
        {
            StopCountdown(parent.Map);
            Messages.Message("Self-destruct aborted.", parent, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Ends the countdown, its alarm and its overlay.</summary>
        /// <param name="map">The map whose overlay to update; may be null.</param>
        private void StopCountdown(Map map)
        {
            ticksLeft = 0;
            alarmSustainer?.End();
            alarmSustainer = null;
            if (map != null)
            {
                Overlay(map).Deregister(this);
            }
        }

        /// <summary>
        /// One countdown tick: keeps the alarm going, shakes the camera and
        /// sends pawns running once a second, and detonates at zero.
        /// </summary>
        private void TickCountdown()
        {
            if (alarmSustainer == null)
            {
                // Also restarts the alarm after a load.
                alarmSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(
                    SoundInfo.InMap(parent, MaintenanceType.PerTick));
            }
            else
            {
                alarmSustainer.Maintain();
            }

            if (ticksLeft % TicksPerSecond == 0)
            {
                if (parent.Map == Find.CurrentMap)
                {
                    Find.CameraDriver.shaker.DoShake(0.25f);
                }

                SendNearbyPawnsFleeing();
            }

            ticksLeft--;
            if (ticksLeft <= 0)
            {
                Detonate();
            }
        }

        /// <summary>
        /// Gives nearby pawns a plain flee job. Not the PanicFlee mental
        /// state: PanicFlee sends pawns off the map for good.
        /// </summary>
        private void SendNearbyPawnsFleeing()
        {
            float radiusSq = Props.fleeRadius * Props.fleeRadius;
            int fleeDistance = Mathf.CeilToInt(Props.fleeRadius);
            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p.Dead || p.Downed || p.Drafted || p.InMentalState
                    || p.jobs == null || p.CurJobDef == JobDefOf.Flee
                    || (p.Position - parent.Position).LengthHorizontalSquared > radiusSq)
                {
                    continue;
                }

                Job job = FleeUtility.FleeJob(p, parent, fleeDistance);
                if (job != null)
                {
                    p.jobs.StartJob(job, JobCondition.InterruptForced);
                }
            }
        }

        /// <summary>
        /// Destroys the device and anything stored in it, then hands off to
        /// the detonation. Killing first keeps the device out of its own
        /// blast, as vanilla CompExplosive does.
        /// </summary>
        private void Detonate()
        {
            Map map = parent.Map;
            IntVec3 center = parent.Position;
            StopCountdown(map);
            (parent as IThingHolder)?.GetDirectlyHeldThings()?.ClearAndDestroyContents();
            parent.Kill();
            Props.detonation.Detonate(parent, map, center, Props.blastRadius);
        }

        /// <summary>This map's countdown overlay component.</summary>
        /// <param name="map">The map.</param>
        /// <returns>The overlay component.</returns>
        private static MapComponent_SelfDestructOverlay Overlay(Map map) =>
            map.GetComponent<MapComponent_SelfDestructOverlay>();

        // ===== Drawing =====

        /// <summary>
        /// Shows the blast and flee radii while selected. During a countdown
        /// the overlay map component draws the flashing blast ring instead.
        /// </summary>
        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (!IsCountingDown)
            {
                GenDraw.DrawRadiusRing(parent.Position, Props.blastRadius, BlastRingColor);
            }

            GenDraw.DrawRadiusRing(parent.Position, Props.fleeRadius, FleeRingColor);
        }

        /// <summary>
        /// Draws the countdown's warnings: the flashing blast ring and the
        /// pulsing surround, yellow-orange until the final seconds, then red
        /// and faster. Called every frame by MapComponent_SelfDestructOverlay,
        /// selected or not.
        /// </summary>
        public void DrawCountdownOverlay()
        {
            // Flash at 2 Hz. Only two fixed colors, because DrawRadiusRing
            // pools one material per color.
            bool bright = (int)(Time.realtimeSinceStartup * 4f) % 2 == 0;
            GenDraw.DrawRadiusRing(
                parent.Position, Props.blastRadius, bright ? BlastRingColor : BlastRingDimColor);

            var (final, alpha) = CountdownPulse.At(
                SecondsLeft, Props.finalWarningSeconds, Props.pulsePeriodSeconds,
                Props.finalPulsePeriodSeconds, Time.realtimeSinceStartup);
            float a = (float)CountdownPulse.Quantize(alpha, PulseLevels);
            Color color = final ? FinalWarningColor : WarningColor;
            EnsureWarningCells();

            // A translucent band one cell wide around the device, edged with
            // a brighter line at its outside.
            Material band = SolidColorMaterials.SimpleSolidColorMaterial(
                new Color(color.r, color.g, color.b, a * 0.45f));
            for (int i = 0; i < warningBand.Count; i++)
            {
                CellRenderer.RenderCell(warningBand[i], band);
            }

            GenDraw.DrawFieldEdges(warningOutline, new Color(color.r, color.g, color.b, a));
        }

        /// <summary>
        /// Caches the cells the warning covers: the device's footprint
        /// grown by one, and that ring without the footprint.
        /// </summary>
        private void EnsureWarningCells()
        {
            if (warningOutline != null)
            {
                return;
            }

            CellRect footprint = parent.OccupiedRect();
            warningOutline = footprint.ExpandedBy(1).Cells.ToList();
            warningBand = warningOutline.Where(c => !footprint.Contains(c)).ToList();
        }

        // ===== UI =====

        /// <summary>The countdown, while one runs.</summary>
        /// <returns>An inspect pane line, or null.</returns>
        public override string CompInspectStringExtra() =>
            IsCountingDown ? $"SELF-DESTRUCT: {SecondsLeft:F1}s" : null;

        /// <summary>
        /// The one self-destruct control that fits the current state: arm,
        /// cancel the order, or abort the countdown. Player devices only.
        /// </summary>
        /// <returns>The comp's gizmos.</returns>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (IsCountingDown)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Abort self-destruct",
                    defaultDesc = "Stop the countdown.",
                    icon = BT_Textures.Cancel,
                    action = Abort
                };
            }
            else if (Order is Designation order)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Cancel self-destruct order",
                    defaultDesc = "Withdraw the order to arm this device's self-destruct.",
                    icon = BT_Textures.Cancel,
                    action = () => parent.Map.designationManager.RemoveDesignation(order)
                };
            }
            else
            {
                string stored = parent is IThingHolder
                    ? " Anything stored inside is destroyed."
                    : "";
                yield return new Command_Action
                {
                    defaultLabel = "Arm self-destruct",
                    defaultDesc = "Order a colonist to arm this device's self-destruct. "
                        + $"Once armed, it counts down {Props.countdownSeconds:0.#} "
                        + $"seconds, then detonates: {Props.detonation.Describe(Props.blastRadius)}. "
                        + $"Pawns within {Props.fleeRadius:0} tiles flee during the "
                        + "countdown." + stored,
                    icon = BT_Textures.ArmSelfDestruct,
                    action = OrderSelfDestruct
                };
            }

            if (DebugSettings.godMode && !IsCountingDown)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Arm now",
                    action = Arm
                };
            }
        }

        /// <summary>Designates the device for a colonist to arm, and says who will.</summary>
        private void OrderSelfDestruct()
        {
            parent.Map.designationManager.AddDesignation(
                new Designation(parent, BT_DesignationDefOf.BT_SelfDestruct));
            Messages.Message(
                "Self-destruct ordered. A colonist with Basic work enabled will arm it.",
                parent, MessageTypeDefOf.NeutralEvent, false);
        }
    }

    /// <summary>
    /// Draws the countdown warnings of every device counting down on the
    /// visible map, selected or not. Buildings draw from the static map
    /// mesh, so their comps get no per-frame draw call of their own.
    /// </summary>
    public class MapComponent_SelfDestructOverlay : MapComponent
    {
        private readonly List<CompSelfDestruct> countingDown = new List<CompSelfDestruct>();

        /// <summary>Created by RimWorld for every map.</summary>
        /// <param name="map">The map this component belongs to.</param>
        public MapComponent_SelfDestructOverlay(Map map)
            : base(map)
        {
        }

        /// <summary>Starts drawing a device's countdown warnings.</summary>
        /// <param name="comp">The device's self-destruct.</param>
        public void Register(CompSelfDestruct comp)
        {
            if (!countingDown.Contains(comp))
            {
                countingDown.Add(comp);
            }
        }

        /// <summary>Stops drawing a device's countdown warnings.</summary>
        /// <param name="comp">The device's self-destruct.</param>
        public void Deregister(CompSelfDestruct comp) => countingDown.Remove(comp);

        /// <summary>Draws every registered warning on the visible map.</summary>
        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap)
            {
                return;
            }

            for (int i = 0; i < countingDown.Count; i++)
            {
                countingDown[i].DrawCountdownOverlay();
            }
        }
    }

    /// <summary>
    /// Finds devices designated for self-destruct and has a colonist arm
    /// them.
    /// </summary>
    public class WorkGiver_ArmSelfDestruct : WorkGiver_Scanner
    {
        /// <summary>Scans artificial buildings.</summary>
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        /// <summary>
        /// Reachability is checked to touch; the job itself walks to the
        /// interaction spot when the device has one.
        /// </summary>
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        /// <summary>Skips the scan when nothing is designated.</summary>
        /// <param name="pawn">The colonist looking for work.</param>
        /// <param name="forced">True for a player-forced job.</param>
        /// <returns>True when there is no work.</returns>
        public override bool ShouldSkip(Pawn pawn, bool forced = false) =>
            !pawn.Map.designationManager.AnySpawnedDesignationOfDef(
                BT_DesignationDefOf.BT_SelfDestruct);

        /// <summary>Only the designated devices.</summary>
        /// <param name="pawn">The colonist looking for work.</param>
        /// <returns>Designated, intact things.</returns>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(
                BT_DesignationDefOf.BT_SelfDestruct))
            {
                Thing t = des.target.Thing;
                if (t != null && !t.Destroyed)
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Whether this colonist can arm this device now: designated,
        /// reservable, and not already counting down.
        /// </summary>
        /// <param name="pawn">The colonist.</param>
        /// <param name="t">The device.</param>
        /// <param name="forced">True for a player-forced job.</param>
        /// <returns>True if there is a job to do.</returns>
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Map == null || t.IsForbidden(pawn)
                || t.Map.designationManager.DesignationOn(t, BT_DesignationDefOf.BT_SelfDestruct) == null
                || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            CompSelfDestruct comp = t.TryGetComp<CompSelfDestruct>();
            return comp != null && !comp.IsCountingDown;
        }

        /// <summary>Creates the arming job.</summary>
        /// <param name="pawn">The colonist.</param>
        /// <param name="t">The device.</param>
        /// <param name="forced">True for a player-forced job.</param>
        /// <returns>The job.</returns>
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            JobMaker.MakeJob(BT_JobDefOf.BT_ArmSelfDestruct, t);
    }

    /// <summary>
    /// Walks to the device (its interaction spot, if it has one), works for
    /// the arming time, then starts the countdown and clears the order.
    /// </summary>
    public class JobDriver_ArmSelfDestruct : JobDriver
    {
        /// <summary>The device being armed.</summary>
        public Thing TargetThing => job.targetA.Thing;

        /// <summary>Reserves the device so only one colonist arms it.</summary>
        /// <param name="errorOnFailed">Log an error if it fails.</param>
        /// <returns>True if reserved.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(TargetThing, job, 1, -1, null, errorOnFailed);

        /// <summary>Go there, work, arm.</summary>
        /// <returns>The job's toils.</returns>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnThingMissingDesignation(TargetIndex.A, BT_DesignationDefOf.BT_SelfDestruct);

            int armingTicks = TargetThing.TryGetComp<CompSelfDestruct>()?.Props.armingTicks ?? 120;
            PathEndMode endMode = TargetThing.def.hasInteractionCell
                ? PathEndMode.InteractionCell
                : PathEndMode.Touch;
            yield return Toils_Goto.GotoThing(TargetIndex.A, endMode);
            yield return Toils_General.WaitWith(
                TargetIndex.A, armingTicks, useProgressBar: true,
                face: TargetIndex.A, pathEndMode: endMode);

            Toil arm = ToilMaker.MakeToil("ArmSelfDestruct");
            arm.initAction = () =>
            {
                TargetThing.Map.designationManager.TryRemoveDesignationOn(
                    TargetThing, BT_DesignationDefOf.BT_SelfDestruct);
                TargetThing.TryGetComp<CompSelfDestruct>()?.Arm();
            };
            arm.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return arm;
        }
    }
}
