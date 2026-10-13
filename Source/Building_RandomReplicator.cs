// RandomReplicator storage building.
// Holds the replicator's chilled food queue and opens it to haulers the way
// vanilla bookcases do: a haul source (haulers may take things out) and a
// haul destination that only "accepts" what is already inside, so its
// contents count as stored at its priority but nothing is ever hauled in.
// Production, refrigeration, power-cut letters and the self-destruct live
// in CompRandomReplicator.

using System.Collections.Generic;
using Verse;
using RimWorld;

namespace BatshitTechnology
{
    /// <summary>
    /// The Random Replicator's building: owns the refrigerated food storage
    /// and exposes it to haulers and the storage tab.
    /// </summary>
    public class Building_RandomReplicator : Building, IHaulSource,
        IHaulDestination, ISuspendableThingHolder, IThingHolderEvents<Thing>,
        ISearchableContents
    {
        private ThingOwner<Thing> innerContainer;
        private StorageSettings settings;
        private CompRandomReplicator replicator;

        /// <summary>Creates the building with an empty food container.</summary>
        public Building_RandomReplicator()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        // ===== Storage (IThingHolder) =====

        /// <summary>
        /// Chilled food is suspended: Thing.DoTick skips the contents, so
        /// they don't rot. Otherwise it ticks them like any container's, and
        /// they spoil at the surrounding room temperature.
        /// </summary>
        public bool IsContentsSuspended =>
            replicator != null && replicator.IsChilling;

        /// <summary>Lets the map search dialog find stored food.</summary>
        public ThingOwner SearchableContents => innerContainer;

        /// <summary>The stored food.</summary>
        /// <returns>The food container.</returns>
        public ThingOwner GetDirectlyHeldThings() => innerContainer;

        /// <summary>Adds holders nested inside the stored items.</summary>
        /// <param name="outChildren">List to append child holders to.</param>
        public void GetChildHolders(List<IThingHolder> outChildren) =>
            ThingOwnerUtility.AppendThingHoldersFromThings(
                outChildren, GetDirectlyHeldThings());

        /// <summary>Tells haulers about food as soon as it is stored.</summary>
        /// <param name="item">The item just added.</param>
        public void Notify_ItemAdded(Thing item) =>
            MapHeld?.listerHaulables.Notify_AddedThing(item);

        /// <summary>No bookkeeping is needed when an item leaves.</summary>
        /// <param name="item">The item just removed.</param>
        public void Notify_ItemRemoved(Thing item)
        {
        }

        // ===== Hauling (IHaulSource, IHaulDestination) =====

        /// <summary>Haulers may always take food out.</summary>
        public bool HaulSourceEnabled => true;

        /// <summary>Never a hauling target: nothing is brought in.</summary>
        public bool HaulDestinationEnabled => false;

        /// <summary>Shows the storage tab.</summary>
        public bool StorageTabVisible => true;

        /// <summary>This building's storage settings.</summary>
        /// <returns>The player-editable settings.</returns>
        public StorageSettings GetStoreSettings() => settings;

        /// <summary>The fixed limits the storage tab offers (food only).</summary>
        /// <returns>The Def's fixed storage settings.</returns>
        public StorageSettings GetParentStoreSettings() =>
            def.building.fixedStorageSettings;

        /// <summary>
        /// Contents count as stored here, at this storage's priority, until
        /// the filter excludes them. Then they're no longer in valid
        /// storage, and haulers move them anywhere that takes them.
        /// </summary>
        /// <param name="t">The item to test.</param>
        /// <returns>True if the item is inside and allowed by the filter.</returns>
        public bool Accepts(Thing t) =>
            innerContainer.Contains(t) && settings.AllowedToAccept(t);

        /// <summary>Re-checks stored items after a settings change.</summary>
        public void Notify_SettingsChanged()
        {
            if (Spawned)
            {
                MapHeld.listerHaulables.Notify_HaulSourceChanged(this);
            }
        }

        // ===== Lifecycle =====

        /// <summary>Creates storage settings from the Def's defaults.</summary>
        public override void PostMake()
        {
            base.PostMake();
            settings = new StorageSettings(this);
            if (def.building.defaultStorageSettings != null)
            {
                settings.CopyFrom(def.building.defaultStorageSettings);
            }
        }

        /// <summary>
        /// Caches the replicator comp before spawning, because spawning
        /// already asks whether the contents are suspended.
        /// </summary>
        /// <param name="map">The map being spawned on.</param>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            replicator = GetComp<CompRandomReplicator>();
            base.SpawnSetup(map, respawningAfterLoad);
        }

        /// <summary>
        /// Spills stored food when deconstructed, uninstalled or destroyed.
        /// (A detonation empties storage first.)
        /// </summary>
        /// <param name="mode">How the building is leaving the map.</param>
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.WillReplace)
            {
                innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            }

            base.DeSpawn(mode);
        }

        /// <summary>Saves and loads the stored food and storage settings.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Deep.Look(ref settings, "settings", this);
        }

        // ===== UI =====

        /// <summary>Adds copy/paste buttons for the storage settings.</summary>
        /// <returns>All gizmos for this building.</returns>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            foreach (Gizmo g in StorageSettingsClipboard.CopyPasteGizmosFor(settings))
            {
                yield return g;
            }
        }

        /// <summary>
        /// Right-clicking with a colonist selected offers vanilla's "haul
        /// from source".
        /// </summary>
        /// <param name="selPawn">The selected colonist.</param>
        /// <returns>Float menu options for this building.</returns>
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(
            Pawn selPawn)
        {
            foreach (FloatMenuOption o in
                HaulSourceUtility.GetFloatMenuOptions(this, selPawn))
            {
                yield return o;
            }

            foreach (FloatMenuOption o in base.GetFloatMenuOptions(selPawn))
            {
                yield return o;
            }
        }
    }
}
