// Batshit Technology - bolt storage shared by the Batshittium Ballista's
// internal magazine and the Ballista Magazine. Both are haul destinations
// that only take bolts (the vanilla bookcase pattern): haulers fill them up
// to a capacity, nothing hauls bolts back out, and a storage tab sets the
// filter and priority.

using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace BatshitTechnology
{
    /// <summary>
    /// XML: how many bolts a building holds.
    /// <c>&lt;li Class="BatshitTechnology.BoltStoreExtension"&gt;&lt;capacity&gt;16...</c>
    /// </summary>
    public class BoltStoreExtension : DefModExtension
    {
        /// <summary>Most bolts held, across all stacks and types.</summary>
        public int capacity = 16;
    }

    /// <summary>
    /// A ThingOwner with a total item cap. Vanilla containers accept any
    /// amount, and haul jobs size themselves from GetCountCanAccept, so
    /// capping it (and both TryAdd overloads) stops haulers overfilling.
    /// </summary>
    public class CappedThingOwner : ThingOwner<Thing>
    {
        /// <summary>Most items held in total. Set by the owner; not saved.</summary>
        public int capacity = int.MaxValue;

        /// <summary>For the save loader.</summary>
        public CappedThingOwner()
        {
        }

        /// <summary>Creates an empty container for a holder.</summary>
        /// <param name="owner">The building holding it.</param>
        public CappedThingOwner(IThingHolder owner)
            : base(owner)
        {
        }

        /// <summary>Room left, in items.</summary>
        public int SpaceLeft => Math.Max(0, capacity - TotalStackCount);

        /// <summary>How much of an item fits, capped by the room left.</summary>
        /// <param name="item">The item offered.</param>
        /// <param name="canMergeWithExistingStacks">Whether stacks may merge.</param>
        /// <returns>How many fit.</returns>
        public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true) =>
            Math.Min(base.GetCountCanAccept(item, canMergeWithExistingStacks), SpaceLeft);

        /// <summary>Adds up to <paramref name="count"/>, capped by the room left.</summary>
        /// <param name="item">The item to add from.</param>
        /// <param name="count">How many to add.</param>
        /// <param name="canMergeWithExistingStacks">Whether stacks may merge.</param>
        /// <returns>How many were added.</returns>
        public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true) =>
            base.TryAdd(item, Math.Min(count, SpaceLeft), canMergeWithExistingStacks);

        /// <summary>Adds a whole stack only if all of it fits.</summary>
        /// <param name="item">The stack to add.</param>
        /// <param name="canMergeWithExistingStacks">Whether stacks may merge.</param>
        /// <returns>True if added.</returns>
        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true) =>
            item != null && item.stackCount <= SpaceLeft
            && base.TryAdd(item, canMergeWithExistingStacks);
    }

    /// <summary>
    /// The container and storage settings behind a bolt-holding building.
    /// The building implements the hauling interfaces and forwards here.
    /// </summary>
    public class BoltStorage
    {
        private readonly Building building;
        private CappedThingOwner container;
        private StorageSettings settings;

        /// <summary>Creates empty storage for a building.</summary>
        /// <param name="building">The building; it must be an IThingHolder.</param>
        public BoltStorage(Building building)
        {
            this.building = building;
            container = new CappedThingOwner((IThingHolder)building);
        }

        /// <summary>The bolts held.</summary>
        public CappedThingOwner Container => container;

        /// <summary>The building's storage settings.</summary>
        public StorageSettings Settings => settings;

        /// <summary>Capacity from the Def's BoltStoreExtension.</summary>
        public int Capacity =>
            building.def.GetModExtension<BoltStoreExtension>()?.capacity ?? 0;

        /// <summary>Bolts of one type held.</summary>
        /// <param name="boltDef">The bolt type.</param>
        /// <returns>The count.</returns>
        public int CountOf(ThingDef boltDef) => container.TotalStackCountOfDef(boltDef);

        /// <summary>Creates settings from the Def's defaults (new buildings).</summary>
        public void PostMake()
        {
            settings = new StorageSettings((IStoreSettingsParent)building);
            if (building.def.building.defaultStorageSettings != null)
            {
                settings.CopyFrom(building.def.building.defaultStorageSettings);
            }

            container.capacity = Capacity;
        }

        /// <summary>Re-applies the capacity, which is not saved.</summary>
        public void Restore() => container.capacity = Capacity;

        /// <summary>
        /// Takes what the filter allows and what fits. Bolts already inside
        /// count as stored here.
        /// </summary>
        /// <param name="t">The item.</param>
        /// <returns>True if it belongs here.</returns>
        public bool Accepts(Thing t) =>
            settings.AllowedToAccept(t)
            && (container.Contains(t) || container.GetCountCanAccept(t) > 0);

        /// <summary>Removes and destroys up to <paramref name="max"/> bolts of a type.</summary>
        /// <param name="boltDef">The bolt type.</param>
        /// <param name="max">Most to take.</param>
        /// <returns>How many were taken.</returns>
        public int Take(ThingDef boltDef, int max)
        {
            int taken = 0;
            for (int i = container.Count - 1; i >= 0 && taken < max; i--)
            {
                Thing stack = container[i];
                if (stack.def != boltDef)
                {
                    continue;
                }

                int n = Math.Min(stack.stackCount, max - taken);
                container.Take(stack, n).Destroy();
                taken += n;
            }

            return taken;
        }

        /// <summary>Spills the bolts when the building leaves the map.</summary>
        /// <param name="mode">How it is leaving.</param>
        public void Spill(DestroyMode mode)
        {
            if (mode != DestroyMode.WillReplace && building.Spawned)
            {
                container.TryDropAll(building.Position, building.Map, ThingPlaceMode.Near);
            }
        }

        /// <summary>Saves and loads the bolts and settings.</summary>
        public void ExposeData()
        {
            Scribe_Deep.Look(ref container, "bolts", (IThingHolder)building);
            Scribe_Deep.Look(ref settings, "boltSettings", (IStoreSettingsParent)building);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Restore();
            }
        }

        /// <summary>One inspect line, e.g. "Bolts: 12/16 (painful bolt x12)".</summary>
        /// <returns>The line.</returns>
        public string InspectLine() =>
            $"Bolts: {container.TotalStackCount}/{Capacity}"
            + (container.Any ? $" ({container.ContentsString})" : "");
    }
}
