// RandomReplicator storage building.
// Holds the replicator's chilled food queue and opens it to haulers the way vanilla bookcases do:
// a haul source (haulers may take things out) and a haul destination that only "accepts" what is
// already inside, so its contents count as stored at its priority but nothing is ever hauled in.
// Production, refrigeration, power-cut letters and the self-destruct live in CompRandomReplicator.

using System.Collections.Generic;
using Verse;
using RimWorld;

namespace RandomReplicator
{
    public class Building_RandomReplicator : Building, IHaulSource, IHaulDestination, ISuspendableThingHolder, IThingHolderEvents<Thing>, ISearchableContents
    {
        private ThingOwner<Thing> innerContainer;
        private StorageSettings settings;
        private CompRandomReplicator replicator;

        public Building_RandomReplicator() { innerContainer = new ThingOwner<Thing>(this); }

        // ===== Storage (IThingHolder) =====

        // Chilled food is suspended: Thing.DoTick skips the contents, so they don't rot.
        // Otherwise it ticks them like any container's, and they spoil at the surrounding room temperature.
        public bool IsContentsSuspended => replicator != null && replicator.IsChilling;
        public ThingOwner SearchableContents => innerContainer;
        public ThingOwner GetDirectlyHeldThings() => innerContainer;
        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public void Notify_ItemAdded(Thing item) => MapHeld?.listerHaulables.Notify_AddedThing(item);
        public void Notify_ItemRemoved(Thing item) { }

        // ===== Hauling (IHaulSource, IHaulDestination) =====

        public bool HaulSourceEnabled => true;
        public bool HaulDestinationEnabled => false; // never a target: haulers don't bring food in
        public bool StorageTabVisible => true;
        public StorageSettings GetStoreSettings() => settings;
        public StorageSettings GetParentStoreSettings() => def.building.fixedStorageSettings;

        // Contents count as stored here, at this storage's priority, until the filter excludes them.
        // Then they're no longer in valid storage, and haulers move them anywhere that takes them.
        public bool Accepts(Thing t) => innerContainer.Contains(t) && settings.AllowedToAccept(t);

        public void Notify_SettingsChanged()
        {
            if (Spawned) MapHeld.listerHaulables.Notify_HaulSourceChanged(this);
        }

        // ===== Lifecycle =====

        public override void PostMake()
        {
            base.PostMake();
            settings = new StorageSettings(this);
            if (def.building.defaultStorageSettings != null) settings.CopyFrom(def.building.defaultStorageSettings);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            replicator = GetComp<CompRandomReplicator>();
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // Deconstructed, uninstalled or destroyed: stored food spills out. (A detonation empties storage first.)
            if (mode != DestroyMode.WillReplace) innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Deep.Look(ref settings, "settings", this);
        }

        // ===== UI =====

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            foreach (Gizmo g in StorageSettingsClipboard.CopyPasteGizmosFor(settings)) yield return g;
        }

        // Right-clicking with a colonist selected offers vanilla's "haul from source".
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption o in HaulSourceUtility.GetFloatMenuOptions(this, selPawn)) yield return o;
            foreach (FloatMenuOption o in base.GetFloatMenuOptions(selPawn)) yield return o;
        }
    }
}
