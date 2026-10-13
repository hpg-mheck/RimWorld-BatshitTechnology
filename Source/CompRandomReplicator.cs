// Batshit Technology - C# assembly
// RimWorld 1.6. Comp that periodically fabricates a random food item into
// the building's chilled storage (Building_RandomReplicator), runs its
// refrigeration, sends power-cut letters, and drives a pawn-armed
// self-destruct (designation, work giver, job and countdown overlay live in
// SelfDestruct.cs).
//
// Compile: .NET Framework 4.7.2; refs: Assembly-CSharp.dll,
// UnityEngine.CoreModule.dll, UnityEngine.dll, BatshitTechnology.Core.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;
using UnityEngine;

namespace BatshitTechnology
{
    /// <summary>
    /// Vanilla categories that 1.6 no longer exposes through
    /// ThingCategoryDefOf.
    /// </summary>
    [DefOf]
    public static class BT_ThingCategoryDefOf
    {
        /// <summary>Cooked meals.</summary>
        public static ThingCategoryDef FoodMeals;

        /// <summary>Raw food: meat, plants, animal products.</summary>
        public static ThingCategoryDef FoodRaw;

        /// <summary>Lets RimWorld bind the fields above at load.</summary>
        static BT_ThingCategoryDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_ThingCategoryDefOf));
        }
    }

    /// <summary>
    /// XML-facing properties for <see cref="CompRandomReplicator"/>. See
    /// Defs/ThingDefs/Buildings_RandomReplicator.xml.
    /// </summary>
    public class CompProperties_RandomReplicator : CompProperties
    {
        // ===== Replication =====

        /// <summary>Ticks between replications (15000 = 6 hours).</summary>
        public int ticksPerSpawn = 15000;

        /// <summary>Items held inside; production pauses when full.</summary>
        public int storageCapacity = 10;

        /// <summary>Categories to draw from; empty = FoodMeals + FoodRaw.</summary>
        public List<ThingCategoryDef> allowedThingCategoryDefs;

        /// <summary>Foods allowed regardless of category.</summary>
        public List<ThingDef> allowedThingDefs;

        /// <summary>Foods never produced.</summary>
        public List<ThingDef> disallowedThingDefs;

        /// <summary>Meals are this much likelier than any one raw food.</summary>
        public float mealWeight = 5f;

        /// <summary>Unpowered: no production, and no refrigeration.</summary>
        public bool requirePower = true;

        /// <summary>Show production status in the inspect pane.</summary>
        public bool writeTimeLeftToSpawn = true;

        // ===== Refrigeration =====

        /// <summary>
        /// Extra watts while refrigeration is on (a vanilla cooler is 200).
        /// </summary>
        public float refrigerationPowerConsumption = 50f;

        /// <summary>Send a letter when refrigerated food loses power.</summary>
        public bool sendPowerCutLetters = true;

        // ===== Self-destruct =====

        /// <summary>Ticks of pawn work to arm the self-destruct.</summary>
        public int selfDestructArmingTicks = 120;

        /// <summary>Countdown length, in seconds.</summary>
        public float selfDestructSeconds = 10f;

        /// <summary>
        /// Blast radius in tiles; the engine cap is
        /// GenRadial.MaxRadialPatternRadius (~79.8 in 1.6).
        /// </summary>
        public float selfDestructRadius = 75f;

        /// <summary>Explosion damage; -1 uses Bomb's default.</summary>
        public int selfDestructDamage = -1;

        /// <summary>Per-cell chance to start a fire in the blast radius.</summary>
        public float selfDestructFireChance = 0.8f;

        /// <summary>Size of each fire the detonation starts.</summary>
        public float selfDestructFireSize = 0.3f;

        /// <summary>Pawns this close run during the countdown.</summary>
        public float selfDestructFleeRadius = 25f;

        [Unsaved] private List<WeightedOption<ThingDef>> cachedFoodOptions;

        /// <summary>Binds these properties to their comp class.</summary>
        public CompProperties_RandomReplicator()
        {
            compClass = typeof(CompRandomReplicator);
        }

        /// <summary>
        /// Everything the replicator may produce, with meals weighted up.
        /// Built on first use, after all defs have loaded.
        /// </summary>
        public List<WeightedOption<ThingDef>> FoodOptions =>
            cachedFoodOptions ??= DefDatabase<ThingDef>.AllDefs
                .Where(IsFoodAllowed)
                .Select(d => new WeightedOption<ThingDef>(d, FoodWeight(d)))
                .ToList();

        /// <summary>Selection weight for one food.</summary>
        /// <param name="def">The food.</param>
        /// <returns><see cref="mealWeight"/> for meals, otherwise 1.</returns>
        private double FoodWeight(ThingDef def) =>
            def.IsWithinCategory(BT_ThingCategoryDefOf.FoodMeals)
                ? mealWeight
                : 1d;

        /// <summary>
        /// Whether the replicator may produce a food: ingestible, not a
        /// corpse or drug, and allowed by the lists and categories.
        /// </summary>
        /// <param name="def">The candidate.</param>
        /// <returns>True if it can be produced.</returns>
        public bool IsFoodAllowed(ThingDef def)
        {
            if (def?.ingestible == null || def.IsCorpse || def.IsDrug)
            {
                return false;
            }

            if (disallowedThingDefs != null && disallowedThingDefs.Contains(def))
            {
                return false;
            }

            if (allowedThingDefs != null && allowedThingDefs.Contains(def))
            {
                return true;
            }

            if (!allowedThingCategoryDefs.NullOrEmpty())
            {
                return allowedThingCategoryDefs.Any(def.IsWithinCategory);
            }

            return def.IsWithinCategory(BT_ThingCategoryDefOf.FoodMeals)
                || def.IsWithinCategory(BT_ThingCategoryDefOf.FoodRaw);
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

            if (!typeof(Building_RandomReplicator)
                .IsAssignableFrom(parentDef.thingClass))
            {
                yield return "thingClass must be "
                    + "BatshitTechnology.Building_RandomReplicator, which holds "
                    + "the storage";
            }

            if (!parentDef.hasInteractionCell)
            {
                yield return "needs hasInteractionCell: food is ejected and "
                    + "the self-destruct armed at the interaction cell";
            }

            if (storageCapacity < 1)
            {
                yield return "storageCapacity must be at least 1";
            }

            // Both radii are drawn with GenDraw.DrawRadiusRing, which shares
            // the engine's radial cap.
            float maxRadius = Mathf.Max(selfDestructRadius, selfDestructFleeRadius);
            if (maxRadius > GenRadial.MaxRadialPatternRadius)
            {
                yield return "selfDestructRadius and selfDestructFleeRadius "
                    + "must not exceed the engine maximum of "
                    + $"{GenRadial.MaxRadialPatternRadius:F1}";
            }
        }
    }

    /// <summary>
    /// Fabricates food into the building's storage, runs refrigeration and
    /// its power draw, sends power-cut letters, and drives the
    /// self-destruct countdown and overlay.
    /// </summary>
    public class CompRandomReplicator : ThingComp
    {
        private const int TicksPerSecond = 60;
        private const int PowerCutLetterDelayTicks = 250; // skip brownouts

        // Rand.Value can return exactly 1.0 (and the top of its range rounds
        // up to 1.0f), which WeightedSelector rejects; keep samples below 1.
        private static readonly WeightedSelector<ThingDef> FoodSelector =
            new WeightedSelector<ThingDef>(
                () => Math.Min(Rand.Value, 0.99999994d));

        private static readonly Color BlastRingColor =
            new Color(1f, 0.25f, 0.15f);
        private static readonly Color BlastRingDimColor =
            new Color(1f, 0.25f, 0.15f, 0.3f);
        private static readonly Color FleeRingColor = new Color(1f, 0.85f, 0.2f);

        private int ticksUntilSpawn = -1;
        private bool refrigeration = true;
        private bool autoEject;
        private CompPowerTrader powerComp;
        private int powerCutTick = -1; // outage start; -1 = none pending

        // ===== Self-destruct state =====
        private int selfDestructTicksLeft; // > 0 while counting down
        private Sustainer alarmSustainer;

        /// <summary>Typed access to this comp's XML properties.</summary>
        public CompProperties_RandomReplicator Props =>
            (CompProperties_RandomReplicator)this.props;

        /// <summary>True while the self-destruct countdown is running.</summary>
        public bool IsSelfDestructCountingDown => selfDestructTicksLeft > 0;

        /// <summary>True while refrigeration is on and powered.</summary>
        public bool IsChilling => refrigeration && HasPower;

        /// <summary>The building's food storage.</summary>
        private ThingOwner Storage =>
            ((Building_RandomReplicator)parent).GetDirectlyHeldThings();

        /// <summary>Total items stored.</summary>
        private int StoredCount => Storage.TotalStackCount;

        /// <summary>True when storage holds its capacity or more.</summary>
        private bool StorageFull => StoredCount >= Props.storageCapacity;

        /// <summary>True when powered, or when power isn't required.</summary>
        private bool HasPower =>
            !Props.requirePower || (powerComp != null && powerComp.PowerOn);

        /// <summary>The pending self-destruct order, if any.</summary>
        private Designation SelfDestructDesignation =>
            parent.Map?.designationManager.DesignationOn(
                parent, BT_DesignationDefOf.BT_SelfDestruct);

        // ===== Test and dev hooks (the smoke test and DEV gizmos) =====

        /// <summary>The refrigeration toggle.</summary>
        internal bool Refrigeration
        {
            get => refrigeration;
            set => refrigeration = value;
        }

        /// <summary>The auto-eject toggle.</summary>
        internal bool AutoEject
        {
            get => autoEject;
            set => autoEject = value;
        }

        /// <summary>Replicates immediately and restarts the timer.</summary>
        internal void ReplicateNow()
        {
            Replicate();
            ResetTimer();
        }

        /// <summary>Drops all stored food, as the Eject all button does.</summary>
        internal void EjectAllNow() => EjectAll();

        // ===== Lifecycle =====

        /// <summary>
        /// Caches the power comp, applies the power draw, starts the timer
        /// on a fresh build, and resumes a countdown's overlay after a load.
        /// </summary>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.GetComp<CompPowerTrader>();
            UpdatePowerDraw();
            if (ticksUntilSpawn < 0)
            {
                ResetTimer();
            }

            if (IsSelfDestructCountingDown)
            {
                Overlay(parent.Map).Register(this); // resumed from a save
            }
        }

        /// <summary>
        /// Uninstalled or destroyed mid-countdown: the countdown goes with it.
        /// </summary>
        /// <param name="map">The map being left.</param>
        /// <param name="mode">How the building is leaving.</param>
        public override void PostDeSpawn(
            Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            StopCountdown(map);
        }

        /// <summary>Saves and loads timers and toggle states.</summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilSpawn, "ticksUntilSpawn", -1);
            Scribe_Values.Look(ref refrigeration, "refrigeration", true);
            Scribe_Values.Look(ref autoEject, "autoEject", false);
            Scribe_Values.Look(ref powerCutTick, "powerCutTick", -1);
            Scribe_Values.Look(
                ref selfDestructTicksLeft, "selfDestructTicksLeft", 0);
        }

        /// <summary>
        /// Notes when an unintended outage starts, and clears it when power
        /// returns. A flick-off also cuts power, but the switch is already
        /// off by then, so it doesn't count as an outage.
        /// </summary>
        /// <param name="signal">The comp signal broadcast.</param>
        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            if (signal == CompPowerTrader.PowerTurnedOffSignal
                && FlickUtility.WantsToBeOn(parent))
            {
                powerCutTick = Find.TickManager.TicksGame;
            }
            else if (signal == CompPowerTrader.PowerTurnedOnSignal)
            {
                powerCutTick = -1;
            }
        }

        /// <summary>
        /// Keeps the power draw current, runs any countdown, and replicates
        /// on schedule while powered and not full.
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            UpdatePowerDraw();
            if (IsSelfDestructCountingDown)
            {
                TickSelfDestruct();
                if (!parent.Spawned)
                {
                    return; // detonated
                }
            }

            if (!HasPower)
            {
                TickPowerCutLetter();
                return;
            }

            // Pauses while storage is full, unless new food is ejected anyway.
            if (!autoEject && StorageFull)
            {
                return;
            }

            ticksUntilSpawn--;
            if (ticksUntilSpawn <= 0)
            {
                Replicate();
                ResetTimer();
            }
        }

        /// <summary>Restarts the replication timer.</summary>
        private void ResetTimer() =>
            ticksUntilSpawn = Mathf.Max(60, Props.ticksPerSpawn);

        /// <summary>
        /// Base draw plus refrigeration. Re-applied every tick, as vanilla
        /// coolers re-apply theirs: the power net resets PowerOutput to the
        /// base draw whenever it rewires.
        /// </summary>
        private void UpdatePowerDraw()
        {
            if (powerComp == null)
            {
                return;
            }

            float draw = powerComp.Props.PowerConsumption
                + (refrigeration ? Props.refrigerationPowerConsumption : 0f);
            if (powerComp.PowerOutput != -draw)
            {
                powerComp.PowerOutput = -draw;
            }
        }

        // ===== Production and storage =====

        /// <summary>
        /// Makes one random food and stores it, or drops it at the
        /// interaction spot when auto-eject is on.
        /// </summary>
        private void Replicate()
        {
            List<WeightedOption<ThingDef>> options = Props.FoodOptions;
            if (options.Count == 0)
            {
                Log.WarningOnce(
                    "[RandomReplicator] No valid food defs found.", 0x52520001);
                return;
            }

            Thing newThing = ThingMaker.MakeThing(FoodSelector.Pick(options));
            newThing.stackCount = 1;
            if (autoEject)
            {
                GenPlace.TryPlaceThing(
                    newThing, DropCell(parent.Map), parent.Map,
                    ThingPlaceMode.Near);
            }
            else if (!Storage.TryAdd(newThing))
            {
                newThing.Destroy();
            }
        }

        /// <summary>Drops all stored food at the interaction spot.</summary>
        private void EjectAll()
        {
            Map map = parent.Map;
            Storage.TryDropAll(DropCell(map), map, ThingPlaceMode.Near);
        }

        /// <summary>Drops one stored stack at the interaction spot.</summary>
        /// <param name="thing">The stack to drop.</param>
        private void Eject(Thing thing)
        {
            if (!Storage.Contains(thing))
            {
                return; // stale menu option: already ejected or hauled out
            }

            Map map = parent.Map;
            Storage.TryDrop(
                thing, DropCell(map), map, ThingPlaceMode.Near, out _);
        }

        /// <summary>
        /// Where ejected food lands: the interaction spot if standable,
        /// otherwise a standable cell nearby.
        /// </summary>
        /// <param name="map">The building's map.</param>
        /// <returns>The drop cell.</returns>
        private IntVec3 DropCell(Map map)
        {
            IntVec3 ic = parent.InteractionCell;
            if (ic.IsValid && ic.Standable(map))
            {
                return ic;
            }

            if (CellFinder.TryFindRandomCellNear(
                parent.Position, map, 2, c => c.Standable(map), out var result))
            {
                return result;
            }

            return parent.Position;
        }

        /// <summary>
        /// Sends one letter per outage, once it has outlasted a brownout
        /// flicker, and only if refrigerated food is at risk.
        /// </summary>
        private void TickPowerCutLetter()
        {
            if (powerCutTick < 0
                || Find.TickManager.TicksGame - powerCutTick
                    < PowerCutLetterDelayTicks)
            {
                return;
            }

            powerCutTick = -1;
            if (!Props.sendPowerCutLetters || !refrigeration || StoredCount == 0
                || !FlickUtility.WantsToBeOn(parent))
            {
                return;
            }

            Find.LetterStack.ReceiveLetter(
                "Replicator power cut",
                $"{parent.LabelCap} has lost power. Its stored food "
                    + $"({Storage.ContentsString}) is no longer chilled and "
                    + "will start to spoil.\n\nRestore power, or eject the "
                    + "food and store it somewhere cold.",
                LetterDefOf.NegativeEvent, parent);
        }

        // ===== Self-destruct =====

        /// <summary>
        /// Starts the countdown. Called by JobDriver_ArmSelfDestruct once a
        /// pawn finishes arming.
        /// </summary>
        public void ArmSelfDestruct()
        {
            if (IsSelfDestructCountingDown)
            {
                return;
            }

            selfDestructTicksLeft = Mathf.Max(
                1, Mathf.RoundToInt(Props.selfDestructSeconds * TicksPerSecond));
            Overlay(parent.Map).Register(this);
            SoundDefOf.PsychicPulseGlobal.PlayOneShot(
                new TargetInfo(parent.Position, parent.Map));
            Find.LetterStack.ReceiveLetter(
                "Self-destruct armed",
                $"{parent.LabelCap} will detonate in "
                    + $"{Props.selfDestructSeconds:0.#} seconds. Everything "
                    + $"within {Props.selfDestructRadius:0} tiles will be "
                    + "caught in the blast and the fires that follow.\n\n"
                    + "Abort it from the device, or get clear.",
                LetterDefOf.ThreatBig, parent);
        }

        /// <summary>Stops a running countdown at the player's request.</summary>
        public void AbortSelfDestruct()
        {
            StopCountdown(parent.Map);
            Messages.Message(
                "Self-destruct aborted.", parent, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Ends the countdown, its alarm and its overlay.</summary>
        /// <param name="map">The map whose overlay to update; may be null.</param>
        private void StopCountdown(Map map)
        {
            selfDestructTicksLeft = 0;
            EndAlarm();
            if (map != null)
            {
                Overlay(map).Deregister(this);
            }
        }

        /// <summary>
        /// One countdown tick: keeps the alarm going, shakes the camera and
        /// sends pawns running once a second, and detonates at zero.
        /// </summary>
        private void TickSelfDestruct()
        {
            if (alarmSustainer == null)
            {
                StartAlarm(); // also restarts the alarm after a load
            }
            else
            {
                alarmSustainer.Maintain();
            }

            if (selfDestructTicksLeft % TicksPerSecond == 0)
            {
                if (parent.Map == Find.CurrentMap)
                {
                    Find.CameraDriver.shaker.DoShake(0.25f);
                }

                SendNearbyPawnsFleeing();
            }

            selfDestructTicksLeft--;
            if (selfDestructTicksLeft <= 0)
            {
                Detonate();
            }
        }

        /// <summary>Starts the looping countdown hiss.</summary>
        private void StartAlarm()
        {
            alarmSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(
                SoundInfo.InMap(parent, MaintenanceType.PerTick));
        }

        /// <summary>Stops the countdown hiss.</summary>
        private void EndAlarm()
        {
            alarmSustainer?.End();
            alarmSustainer = null;
        }

        /// <summary>
        /// Gives nearby pawns a plain flee job. Not the PanicFlee mental
        /// state: PanicFlee sends pawns off the map for good.
        /// </summary>
        private void SendNearbyPawnsFleeing()
        {
            float radiusSq =
                Props.selfDestructFleeRadius * Props.selfDestructFleeRadius;
            int fleeDistance = Mathf.CeilToInt(Props.selfDestructFleeRadius);
            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned)
            {
                if (p.Dead || p.Downed || p.Drafted || p.InMentalState
                    || p.jobs == null || p.CurJobDef == JobDefOf.Flee)
                {
                    continue;
                }

                if ((p.Position - parent.Position).LengthHorizontalSquared
                    > radiusSq)
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
        /// Destroys the device and its stored food, then sets off the blast
        /// and the fires.
        /// </summary>
        private void Detonate()
        {
            Map map = parent.Map;
            IntVec3 center = parent.Position;
            float radius = Props.selfDestructRadius;
            StopCountdown(map);
            Storage.ClearAndDestroyContents(); // vaporized with the device

            // Kill first so the device isn't caught in its own blast (same
            // order as vanilla CompExplosive).
            parent.Kill();
            GenExplosion.DoExplosion(
                center, map, radius, DamageDefOf.Bomb, parent,
                Props.selfDestructDamage);

            // Blanket ignite: high chance to light each cell in the radius
            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (c.InBounds(map) && Rand.Chance(Props.selfDestructFireChance))
                {
                    FireUtility.TryStartFireIn(
                        c, map, Props.selfDestructFireSize, null);
                }
            }
        }

        /// <summary>This map's countdown overlay component.</summary>
        /// <param name="map">The map.</param>
        /// <returns>The overlay component.</returns>
        private static MapComponent_SelfDestructOverlay Overlay(Map map) =>
            map.GetComponent<MapComponent_SelfDestructOverlay>();

        // ===== Blast ring overlay =====

        /// <summary>
        /// Shows the blast and flee radii while selected. During a countdown
        /// the overlay map component draws the flashing blast ring instead.
        /// </summary>
        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (!IsSelfDestructCountingDown)
            {
                GenDraw.DrawRadiusRing(
                    parent.Position, Props.selfDestructRadius, BlastRingColor);
            }

            GenDraw.DrawRadiusRing(
                parent.Position, Props.selfDestructFleeRadius, FleeRingColor);
        }

        /// <summary>
        /// Draws the flashing blast ring. Called every frame by
        /// MapComponent_SelfDestructOverlay, selected or not.
        /// </summary>
        public void DrawCountdownRing()
        {
            // Flash at 2 Hz. Only two fixed colors, because DrawRadiusRing
            // pools one material per color.
            bool bright = (int)(Time.realtimeSinceStartup * 4f) % 2 == 0;
            GenDraw.DrawRadiusRing(
                parent.Position, Props.selfDestructRadius,
                bright ? BlastRingColor : BlastRingDimColor);
        }

        // ===== UI =====

        /// <summary>
        /// Stored food, chill status, production status and any countdown.
        /// </summary>
        /// <returns>Inspect pane lines.</returns>
        public override string CompInspectStringExtra()
        {
            var lines = new List<string>();
            string chill = !refrigeration
                ? "not chilled: refrigeration off"
                : HasPower ? "chilled" : "not chilled: no power";
            lines.Add($"Stored food: {StoredCount}/{Props.storageCapacity} ({chill})");
            if (Storage.Any)
            {
                lines.Add("Contents: " + Storage.ContentsString);
            }

            if (Props.writeTimeLeftToSpawn)
            {
                if (!HasPower)
                {
                    lines.Add("Replication paused: no power");
                }
                else if (!autoEject && StorageFull)
                {
                    lines.Add("Replication paused: storage full");
                }
                else
                {
                    lines.Add("Next replication in: "
                        + ticksUntilSpawn.ToStringTicksToPeriod()
                        + (autoEject ? " (auto-eject)" : ""));
                }
            }

            if (IsSelfDestructCountingDown)
            {
                float seconds = selfDestructTicksLeft / (float)TicksPerSecond;
                lines.Add($"SELF-DESTRUCT: {seconds:F1}s");
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Eject, auto-eject, refrigeration and self-destruct controls, for
        /// the player's own replicators only.
        /// </summary>
        /// <returns>The comp's gizmos.</returns>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            foreach (Gizmo g in StorageGizmos())
            {
                yield return g;
            }

            yield return SelfDestructGizmo();
            if (DebugSettings.godMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Replicate now",
                    action = ReplicateNow
                };
            }
        }

        /// <summary>Eject, auto-eject and refrigeration controls.</summary>
        /// <returns>The storage gizmos.</returns>
        private IEnumerable<Gizmo> StorageGizmos()
        {
            var ejectAll = new Command_Action
            {
                defaultLabel = "Eject all",
                defaultDesc = "Drop all stored food at the interaction spot.",
                icon = BT_Textures.Eject,
                action = EjectAll
            };
            var ejectOne = new Command_Action
            {
                defaultLabel = "Eject item...",
                defaultDesc =
                    "Choose one stored stack to drop at the interaction spot.",
                icon = BT_Textures.Eject,
                action = () => Find.WindowStack.Add(new FloatMenu(Storage
                    .Select(t => new FloatMenuOption(t.LabelCap, () => Eject(t), t.def))
                    .ToList()))
            };
            if (!Storage.Any)
            {
                ejectAll.Disable("Nothing stored.");
                ejectOne.Disable("Nothing stored.");
            }

            yield return ejectAll;
            yield return ejectOne;
            yield return new Command_Toggle
            {
                defaultLabel = "Auto-eject",
                defaultDesc = "Drop new food at the interaction spot as soon "
                    + "as it's made, instead of storing it.\n\nHaulers only "
                    + "pull stored food out to better storage, and nobody eats "
                    + "straight from the device, so turn this on if no "
                    + "stockpile takes the food.",
                icon = BT_Textures.Eject,
                isActive = () => autoEject,
                toggleAction = () => autoEject = !autoEject
            };
            yield return new Command_Toggle
            {
                defaultLabel = "Refrigeration",
                defaultDesc = "Chill stored food so it doesn't spoil. Draws an "
                    + $"extra {Props.refrigerationPowerConsumption:0} W while "
                    + "on.\n\nWhen off, or without power, stored food spoils "
                    + "at the temperature of the room around the device.",
                icon = BT_Textures.Refrigeration,
                isActive = () => refrigeration,
                toggleAction = () => refrigeration = !refrigeration
            };
        }

        /// <summary>
        /// The one self-destruct control that fits the current state: arm,
        /// cancel the order, or abort the countdown.
        /// </summary>
        /// <returns>The self-destruct gizmo.</returns>
        private Gizmo SelfDestructGizmo()
        {
            if (IsSelfDestructCountingDown)
            {
                return new Command_Action
                {
                    defaultLabel = "Abort self-destruct",
                    defaultDesc = "Stop the countdown.",
                    icon = BT_Textures.Cancel,
                    action = AbortSelfDestruct
                };
            }

            if (SelfDestructDesignation is Designation designation)
            {
                return new Command_Action
                {
                    defaultLabel = "Cancel self-destruct order",
                    defaultDesc =
                        "Withdraw the order to arm this device's self-destruct.",
                    icon = BT_Textures.Cancel,
                    action = () =>
                        parent.Map.designationManager.RemoveDesignation(designation)
                };
            }

            return new Command_Action
            {
                defaultLabel = "Arm self-destruct",
                defaultDesc = "Order a colonist to arm this device's "
                    + "self-destruct at its interaction spot. Once armed, it "
                    + $"counts down {Props.selfDestructSeconds:0.#} seconds, "
                    + $"then detonates: a ~{Props.selfDestructRadius:0}-tile "
                    + "blast with widespread fires. Pawns within "
                    + $"{Props.selfDestructFleeRadius:0} tiles flee during the "
                    + "countdown. Stored food is destroyed.",
                icon = BT_Textures.ArmSelfDestruct,
                action = OrderSelfDestruct
            };
        }

        /// <summary>
        /// Designates the device for a colonist to arm, and says who will.
        /// </summary>
        private void OrderSelfDestruct()
        {
            parent.Map.designationManager.AddDesignation(
                new Designation(parent, BT_DesignationDefOf.BT_SelfDestruct));
            Messages.Message(
                "Self-destruct ordered. A colonist with Basic work enabled "
                    + "will arm it.",
                parent, MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
