// Batshit Technology - C# assembly
// RimWorld 1.6. Comp that periodically fabricates a random food item into
// the building's chilled storage (Building_RandomReplicator), runs its
// refrigeration and sends power-cut letters. Its self-destruct is the shared
// CompSelfDestruct (SelfDestruct.cs) with a Detonation_Bomb.
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
                yield return "needs hasInteractionCell: food is ejected at "
                    + "the interaction cell";
            }

            if (storageCapacity < 1)
            {
                yield return "storageCapacity must be at least 1";
            }
        }
    }

    /// <summary>
    /// Fabricates food into the building's storage, runs refrigeration and
    /// its power draw, and sends power-cut letters. Its self-destruct is a
    /// separate comp, CompSelfDestruct.
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

        private int ticksUntilSpawn = -1;
        private bool refrigeration = true;
        private bool autoEject;
        private CompPowerTrader powerComp;
        private int powerCutTick = -1; // outage start; -1 = none pending

        /// <summary>Typed access to this comp's XML properties.</summary>
        public CompProperties_RandomReplicator Props =>
            (CompProperties_RandomReplicator)this.props;

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
        /// Caches the power comp, applies the power draw, and starts the
        /// timer on a fresh build.
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
        }

        /// <summary>Saves and loads timers and toggle states.</summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilSpawn, "ticksUntilSpawn", -1);
            Scribe_Values.Look(ref refrigeration, "refrigeration", true);
            Scribe_Values.Look(ref autoEject, "autoEject", false);
            Scribe_Values.Look(ref powerCutTick, "powerCutTick", -1);
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
        /// Keeps the power draw current and replicates on schedule while
        /// powered and not full.
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return; // destroyed earlier this tick
            }

            UpdatePowerDraw();

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

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Eject, auto-eject and refrigeration controls, for the player's own
        /// replicators only.
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

            if (DebugSettings.ShowDevGizmos)
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

    }
}
