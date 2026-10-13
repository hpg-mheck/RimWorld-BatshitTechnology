// RandomReplicator - C# assembly
// RimWorld 1.6. Comp that periodically fabricates a random food item into the building's chilled storage
// (Building_RandomReplicator), runs its refrigeration, sends power-cut letters, and drives a pawn-armed
// self-destruct (designation, work giver, job and countdown overlay live in SelfDestruct.cs).
//
// Compile: .NET Framework 4.7.2; refs: Assembly-CSharp.dll, UnityEngine.CoreModule.dll, UnityEngine.dll

using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;
using UnityEngine;

namespace RandomReplicator
{
    // Vanilla categories that 1.6 no longer exposes through ThingCategoryDefOf.
    [DefOf]
    public static class RR_ThingCategoryDefOf
    {
        public static ThingCategoryDef FoodMeals;
        public static ThingCategoryDef FoodRaw;
        static RR_ThingCategoryDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(RR_ThingCategoryDefOf)); }
    }

    // XML-facing properties for the comp. See Defs/ThingDefs/Buildings_RandomReplicator.xml.
    public class CompProperties_RandomReplicator : CompProperties
    {
        // ===== Replication =====
        public int ticksPerSpawn = 15000; // 6 hours (60,000/day)
        public int storageCapacity = 10;  // items held inside; production pauses when full
        public List<ThingCategoryDef> allowedThingCategoryDefs; // empty = FoodMeals + FoodRaw
        public List<ThingDef> allowedThingDefs;                 // allowed regardless of category
        public List<ThingDef> disallowedThingDefs;              // never produced
        public float mealWeight = 5f;                           // meals are this much likelier than raw food
        public bool requirePower = true;   // unpowered: no production, and no refrigeration
        public bool writeTimeLeftToSpawn = true;

        // ===== Refrigeration =====
        public float refrigerationPowerConsumption = 50f; // W on top of the base draw; a vanilla cooler is 200 W
        public bool sendPowerCutLetters = true;

        // ===== Self-destruct =====
        public int selfDestructArmingTicks = 120;   // how long a pawn works to arm it
        public float selfDestructSeconds = 10f;
        public float selfDestructRadius = 75f;      // engine cap is GenRadial.MaxRadialPatternRadius (~79.8 in 1.6)
        public int selfDestructDamage = -1;         // -1 = Bomb's default damage
        public float selfDestructFireChance = 0.8f; // per cell inside the blast radius
        public float selfDestructFireSize = 0.3f;
        public float selfDestructFleeRadius = 25f;  // pawns this close run during the countdown

        [Unsaved] private List<ThingDef> cachedFoodCandidates;

        public CompProperties_RandomReplicator() { compClass = typeof(CompRandomReplicator); }

        // Everything the replicator may produce. Built on first use, after all defs have loaded.
        public List<ThingDef> FoodCandidates => cachedFoodCandidates ??= DefDatabase<ThingDef>.AllDefs.Where(IsFoodAllowed).ToList();

        public bool IsFoodAllowed(ThingDef def)
        {
            if (def?.ingestible == null || def.IsCorpse || def.IsDrug) return false;
            if (disallowedThingDefs != null && disallowedThingDefs.Contains(def)) return false;
            if (allowedThingDefs != null && allowedThingDefs.Contains(def)) return true;
            if (!allowedThingCategoryDefs.NullOrEmpty()) return allowedThingCategoryDefs.Any(def.IsWithinCategory);
            return def.IsWithinCategory(RR_ThingCategoryDefOf.FoodMeals) || def.IsWithinCategory(RR_ThingCategoryDefOf.FoodRaw);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (!typeof(Building_RandomReplicator).IsAssignableFrom(parentDef.thingClass))
                yield return "thingClass must be RandomReplicator.Building_RandomReplicator, which holds the storage";
            if (!parentDef.hasInteractionCell)
                yield return "needs hasInteractionCell: food is ejected and the self-destruct armed at the interaction cell";
            if (storageCapacity < 1)
                yield return "storageCapacity must be at least 1";
            // Both radii are drawn with GenDraw.DrawRadiusRing, which shares the engine's radial cap.
            if (Mathf.Max(selfDestructRadius, selfDestructFleeRadius) > GenRadial.MaxRadialPatternRadius)
                yield return $"selfDestructRadius and selfDestructFleeRadius must not exceed the engine maximum of {GenRadial.MaxRadialPatternRadius:F1}";
        }
    }

    public class CompRandomReplicator : ThingComp
    {
        private const int TicksPerSecond = 60;
        private const int PowerCutLetterDelayTicks = 250; // ignore momentary brownouts

        private int ticksUntilSpawn = -1;
        private bool refrigeration = true;
        private bool autoEject;
        private CompPowerTrader powerComp;
        private int powerCutTick = -1; // when an unintended outage began; -1 = none pending

        // ===== Self-destruct state =====
        private int selfDestructTicksLeft; // > 0 while counting down
        private Sustainer alarmSustainer;

        private static readonly Color BlastRingColor = new Color(1f, 0.25f, 0.15f);
        private static readonly Color BlastRingDimColor = new Color(1f, 0.25f, 0.15f, 0.3f);
        private static readonly Color FleeRingColor = new Color(1f, 0.85f, 0.2f);

        public CompProperties_RandomReplicator Props => (CompProperties_RandomReplicator)this.props;
        public bool IsSelfDestructCountingDown => selfDestructTicksLeft > 0;
        public bool IsChilling => refrigeration && HasPower;
        private ThingOwner Storage => ((Building_RandomReplicator)parent).GetDirectlyHeldThings();
        private int StoredCount => Storage.TotalStackCount;
        private bool StorageFull => StoredCount >= Props.storageCapacity;
        private bool HasPower => !Props.requirePower || (powerComp != null && powerComp.PowerOn);
        private Designation SelfDestructDesignation => parent.Map?.designationManager.DesignationOn(parent, RR_DesignationDefOf.RR_SelfDestruct);

        // ===== Lifecycle =====

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.GetComp<CompPowerTrader>();
            UpdatePowerDraw();
            if (ticksUntilSpawn < 0) ResetTimer();
            if (IsSelfDestructCountingDown) Overlay(parent.Map).Register(this); // resumed from a save
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            StopCountdown(map); // uninstalled or destroyed mid-countdown: the countdown goes with it
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilSpawn, "ticksUntilSpawn", -1);
            Scribe_Values.Look(ref refrigeration, "refrigeration", true);
            Scribe_Values.Look(ref autoEject, "autoEject", false);
            Scribe_Values.Look(ref powerCutTick, "powerCutTick", -1);
            Scribe_Values.Look(ref selfDestructTicksLeft, "selfDestructTicksLeft", 0);
        }

        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            // A flick-off also cuts power, but the switch is already off by then, so it doesn't count as an outage.
            if (signal == CompPowerTrader.PowerTurnedOffSignal && FlickUtility.WantsToBeOn(parent))
                powerCutTick = Find.TickManager.TicksGame;
            else if (signal == CompPowerTrader.PowerTurnedOnSignal)
                powerCutTick = -1;
        }

        public override void CompTick()
        {
            base.CompTick();
            UpdatePowerDraw();
            if (IsSelfDestructCountingDown)
            {
                TickSelfDestruct();
                if (!parent.Spawned) return; // detonated
            }

            if (!HasPower)
            {
                TickPowerCutLetter();
                return;
            }

            // Replicator production; pauses while storage is full (unless new food is ejected anyway)
            if (!autoEject && StorageFull) return;
            ticksUntilSpawn--;
            if (ticksUntilSpawn <= 0)
            {
                Replicate();
                ResetTimer();
            }
        }

        private void ResetTimer() => ticksUntilSpawn = Mathf.Max(60, Props.ticksPerSpawn);

        // Base draw plus refrigeration. Re-applied every tick, like vanilla coolers re-apply theirs:
        // the power net resets PowerOutput to the base draw whenever it rewires.
        private void UpdatePowerDraw()
        {
            if (powerComp == null) return;
            float draw = powerComp.Props.PowerConsumption + (refrigeration ? Props.refrigerationPowerConsumption : 0f);
            if (powerComp.PowerOutput != -draw) powerComp.PowerOutput = -draw;
        }

        // ===== Production and storage =====

        private void Replicate()
        {
            List<ThingDef> candidates = Props.FoodCandidates;
            if (candidates.Count == 0)
            {
                Log.WarningOnce("[RandomReplicator] No valid food defs found.", 0x52520001);
                return;
            }
            ThingDef foodDef = candidates.RandomElementByWeight(d => d.IsWithinCategory(RR_ThingCategoryDefOf.FoodMeals) ? Props.mealWeight : 1f);
            Thing newThing = ThingMaker.MakeThing(foodDef);
            newThing.stackCount = 1;
            if (autoEject) GenPlace.TryPlaceThing(newThing, DropCell(parent.Map), parent.Map, ThingPlaceMode.Near);
            else if (!Storage.TryAdd(newThing)) newThing.Destroy();
        }

        private void EjectAll()
        {
            Map map = parent.Map;
            Storage.TryDropAll(DropCell(map), map, ThingPlaceMode.Near);
        }

        private void Eject(Thing thing)
        {
            if (!Storage.Contains(thing)) return; // stale menu option: already ejected or hauled out
            Map map = parent.Map;
            Storage.TryDrop(thing, DropCell(map), map, ThingPlaceMode.Near, out _);
        }

        private IntVec3 DropCell(Map map)
        {
            IntVec3 ic = parent.InteractionCell;
            if (ic.IsValid && ic.Standable(map)) return ic;
            if (CellFinder.TryFindRandomCellNear(parent.Position, map, 2, c => c.Standable(map), out var result)) return result;
            return parent.Position;
        }

        // One letter per outage, once it has outlasted a brownout flicker, and only if chilled food is at risk.
        private void TickPowerCutLetter()
        {
            if (powerCutTick < 0 || Find.TickManager.TicksGame - powerCutTick < PowerCutLetterDelayTicks) return;
            powerCutTick = -1;
            if (!Props.sendPowerCutLetters || !refrigeration || StoredCount == 0 || !FlickUtility.WantsToBeOn(parent)) return;
            Find.LetterStack.ReceiveLetter(
                "Replicator power cut",
                $"{parent.LabelCap} has lost power. Its stored food ({Storage.ContentsString}) is no longer chilled and will start to spoil.\n\nRestore power, or eject the food and store it somewhere cold.",
                LetterDefOf.NegativeEvent, parent);
        }

        // ===== Self-destruct =====

        // Called by JobDriver_ArmSelfDestruct once a pawn finishes arming.
        public void ArmSelfDestruct()
        {
            if (IsSelfDestructCountingDown) return;
            selfDestructTicksLeft = Mathf.Max(1, Mathf.RoundToInt(Props.selfDestructSeconds * TicksPerSecond));
            Overlay(parent.Map).Register(this);
            SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
            Find.LetterStack.ReceiveLetter(
                "Self-destruct armed",
                $"{parent.LabelCap} will detonate in {Props.selfDestructSeconds:0.#} seconds. Everything within {Props.selfDestructRadius:0} tiles will be caught in the blast and the fires that follow.\n\nAbort it from the device, or get clear.",
                LetterDefOf.ThreatBig, parent);
        }

        public void AbortSelfDestruct()
        {
            StopCountdown(parent.Map);
            Messages.Message("Self-destruct aborted.", parent, MessageTypeDefOf.NeutralEvent);
        }

        private void StopCountdown(Map map)
        {
            selfDestructTicksLeft = 0;
            EndAlarm();
            if (map != null) Overlay(map).Deregister(this);
        }

        private void TickSelfDestruct()
        {
            if (alarmSustainer == null) StartAlarm(); // also restarts the alarm after a load
            else alarmSustainer.Maintain();

            // Once a second: mild camera shake, and send anyone nearby running.
            if (selfDestructTicksLeft % TicksPerSecond == 0)
            {
                if (parent.Map == Find.CurrentMap) Find.CameraDriver.shaker.DoShake(0.25f);
                SendNearbyPawnsFleeing();
            }

            selfDestructTicksLeft--;
            if (selfDestructTicksLeft <= 0) Detonate();
        }

        private void StartAlarm()
        {
            alarmSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
        }

        private void EndAlarm()
        {
            alarmSustainer?.End();
            alarmSustainer = null;
        }

        // A plain flee job, not the PanicFlee mental state: PanicFlee sends pawns off the map for good.
        private void SendNearbyPawnsFleeing()
        {
            float radiusSq = Props.selfDestructFleeRadius * Props.selfDestructFleeRadius;
            int fleeDistance = Mathf.CeilToInt(Props.selfDestructFleeRadius);
            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned)
            {
                if (p.Dead || p.Downed || p.Drafted || p.InMentalState || p.jobs == null) continue;
                if (p.CurJobDef == JobDefOf.Flee) continue;
                if ((p.Position - parent.Position).LengthHorizontalSquared > radiusSq) continue;
                Job job = FleeUtility.FleeJob(p, parent, fleeDistance);
                if (job != null) p.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }

        private void Detonate()
        {
            Map map = parent.Map;
            IntVec3 center = parent.Position;
            float radius = Props.selfDestructRadius;
            StopCountdown(map);
            Storage.ClearAndDestroyContents(); // stored food is vaporized with the device

            // Kill first so the device isn't caught in its own blast (same order as vanilla CompExplosive).
            parent.Kill();
            GenExplosion.DoExplosion(center, map, radius, DamageDefOf.Bomb, parent, Props.selfDestructDamage);

            // Blanket ignite: high chance to light each cell in the radius
            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (c.InBounds(map) && Rand.Chance(Props.selfDestructFireChance))
                    FireUtility.TryStartFireIn(c, map, Props.selfDestructFireSize, null);
            }
        }

        private static MapComponent_SelfDestructOverlay Overlay(Map map) => map.GetComponent<MapComponent_SelfDestructOverlay>();

        // ===== Blast ring overlay =====

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            // While counting down, the overlay map component already draws the (flashing) blast ring.
            if (!IsSelfDestructCountingDown) GenDraw.DrawRadiusRing(parent.Position, Props.selfDestructRadius, BlastRingColor);
            GenDraw.DrawRadiusRing(parent.Position, Props.selfDestructFleeRadius, FleeRingColor);
        }

        // Called every frame by MapComponent_SelfDestructOverlay, selected or not.
        public void DrawCountdownRing()
        {
            // Flash at 2 Hz. Only two fixed colors, because DrawRadiusRing pools one material per color.
            bool bright = (int)(Time.realtimeSinceStartup * 4f) % 2 == 0;
            GenDraw.DrawRadiusRing(parent.Position, Props.selfDestructRadius, bright ? BlastRingColor : BlastRingDimColor);
        }

        // ===== UI =====

        public override string CompInspectStringExtra()
        {
            List<string> lines = new List<string>();
            string chill = !refrigeration ? "not chilled: refrigeration off" : HasPower ? "chilled" : "not chilled: no power";
            lines.Add($"Stored food: {StoredCount}/{Props.storageCapacity} ({chill})");
            if (Storage.Any) lines.Add("Contents: " + Storage.ContentsString);
            if (Props.writeTimeLeftToSpawn)
            {
                if (!HasPower) lines.Add("Replication paused: no power");
                else if (!autoEject && StorageFull) lines.Add("Replication paused: storage full");
                else lines.Add($"Next replication in: {ticksUntilSpawn.ToStringTicksToPeriod()}" + (autoEject ? " (auto-eject)" : ""));
            }
            if (IsSelfDestructCountingDown)
            {
                lines.Add($"SELF-DESTRUCT: {selfDestructTicksLeft / (float)TicksPerSecond:F1}s");
            }
            return string.Join("\n", lines);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra()) yield return g;
            if (parent.Faction != Faction.OfPlayer) yield break;

            var ejectAll = new Command_Action
            {
                defaultLabel = "Eject all",
                defaultDesc = "Drop all stored food at the interaction spot.",
                icon = RR_Textures.Eject,
                action = EjectAll
            };
            var ejectOne = new Command_Action
            {
                defaultLabel = "Eject item...",
                defaultDesc = "Choose one stored stack to drop at the interaction spot.",
                icon = RR_Textures.Eject,
                action = () => Find.WindowStack.Add(new FloatMenu(Storage.Select(t => new FloatMenuOption(t.LabelCap, () => Eject(t), t.def)).ToList()))
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
                defaultDesc = "Drop new food at the interaction spot as soon as it's made, instead of storing it.\n\nHaulers only pull stored food out to better storage, and nobody eats straight from the device, so turn this on if no stockpile takes the food.",
                icon = RR_Textures.Eject,
                isActive = () => autoEject,
                toggleAction = () => autoEject = !autoEject
            };
            yield return new Command_Toggle
            {
                defaultLabel = "Refrigeration",
                defaultDesc = $"Chill stored food so it doesn't spoil. Draws an extra {Props.refrigerationPowerConsumption:0} W while on.\n\nWhen off, or without power, stored food spoils at the temperature of the room around the device.",
                icon = RR_Textures.Refrigeration,
                isActive = () => refrigeration,
                toggleAction = () => refrigeration = !refrigeration
            };

            if (IsSelfDestructCountingDown)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Abort self-destruct",
                    defaultDesc = "Stop the countdown.",
                    icon = RR_Textures.Cancel,
                    action = AbortSelfDestruct
                };
            }
            else if (SelfDestructDesignation is Designation designation)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Cancel self-destruct order",
                    defaultDesc = "Withdraw the order to arm this device's self-destruct.",
                    icon = RR_Textures.Cancel,
                    action = () => parent.Map.designationManager.RemoveDesignation(designation)
                };
            }
            else
            {
                yield return new Command_Action
                {
                    defaultLabel = "Arm self-destruct",
                    defaultDesc = $"Order a colonist to arm this device's self-destruct at its interaction spot. Once armed, it counts down {Props.selfDestructSeconds:0.#} seconds, then detonates: a ~{Props.selfDestructRadius:0}-tile blast with widespread fires. Pawns within {Props.selfDestructFleeRadius:0} tiles flee during the countdown. Stored food is destroyed.",
                    icon = RR_Textures.ArmSelfDestruct,
                    action = () =>
                    {
                        parent.Map.designationManager.AddDesignation(new Designation(parent, RR_DesignationDefOf.RR_SelfDestruct));
                        Messages.Message("Self-destruct ordered. A colonist with Basic work enabled will arm it.", parent, MessageTypeDefOf.NeutralEvent, false);
                    }
                };
            }
        }
    }
}
