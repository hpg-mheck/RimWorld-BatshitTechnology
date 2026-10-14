// Showcase API - the library.
// Finds every loaded ShowcaseDef, groups them by the mod that declared
// them (Core's ShowcaseCatalogue), and decides what the menu may do with
// each one against the running mod list (Core's LaunchDecision). The
// rules live in Core and are unit tested; this file only feeds them the
// game's state.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// The loaded showcases, grouped by mod, and their launch decisions.
    /// </summary>
    public static class ShowcaseLibrary
    {
        /// <summary>Gets every loaded showcase, in load order.</summary>
        public static IReadOnlyList<ShowcaseDef> All =>
            DefDatabase<ShowcaseDef>.AllDefsListForReading;

        /// <summary>Gets a value indicating whether any showcase is loaded.</summary>
        public static bool Any => DefDatabase<ShowcaseDef>.DefCount > 0;

        /// <summary>
        /// Groups the loaded showcases by mod, in menu order: mods by name,
        /// showcases by their order, then label.
        /// </summary>
        /// <returns>One group per mod with any showcase.</returns>
        public static IReadOnlyList<ShowcaseModGroup> Groups()
        {
            var entries = new List<ShowcaseEntry>();
            foreach (ShowcaseDef def in All)
            {
                try
                {
                    entries.Add(def.ToEntry());
                }
                catch (Exception e)
                {
                    Log.ErrorOnce(
                        $"{ShowcaseLog.Prefix} could not list {def?.defName}: {e}",
                        (def?.defName ?? "null").GetHashCode() ^ 0x5C0A5E);
                }
            }

            return ShowcaseCatalogue.GroupByMod(entries);
        }

        /// <summary>Finds a showcase by def name.</summary>
        /// <param name="defName">The def name; may be null.</param>
        /// <returns>The showcase, or null.</returns>
        public static ShowcaseDef Named(string defName) =>
            defName.NullOrEmpty()
                ? null
                : DefDatabase<ShowcaseDef>.GetNamedSilentFail(defName.Trim());

        /// <summary>
        /// The showcases in a group, as defs, in the group's order.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <returns>The defs; any that vanished are skipped.</returns>
        public static List<ShowcaseDef> DefsIn(ShowcaseModGroup group) =>
            (group?.Showcases ?? Array.Empty<ShowcaseEntry>())
                .Select(e => Named(e.DefName))
                .Where(d => d != null)
                .ToList();

        /// <summary>
        /// The package ids of the running mods, official content included.
        /// </summary>
        /// <returns>The ids.</returns>
        public static List<string> LoadedPackageIds() =>
            LoadedModManager.RunningModsListForReading
                .Where(m => m != null)
                .Select(m => m.PackageId)
                .ToList();

        /// <summary>
        /// Decides what the menu may do with a showcase now.
        /// </summary>
        /// <param name="def">The showcase.</param>
        /// <param name="force">Whether Force start was confirmed.</param>
        /// <returns>The decision.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="def"/> is null.
        /// </exception>
        public static LaunchDecision Evaluate(ShowcaseDef def, bool force)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }

            return LaunchDecision.Evaluate(def.ToEntry(), LoadedPackageIds(), force);
        }

        /// <summary>
        /// A mod's name for the player, from its package id: the installed
        /// mod's name (DLCs included), or the id itself.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <returns>The name.</returns>
        public static string ModName(string packageId)
        {
            if (packageId.NullOrEmpty())
            {
                return "BT_Showcases_UnknownMod".Translate();
            }

            string name = ModLister.GetModWithIdentifier(
                packageId, ignorePostfix: true)?.Name;
            return name.NullOrEmpty() ? packageId : name;
        }

        /// <summary>
        /// A group's display name: the catalogue's, or "Unknown mod" for
        /// showcases whose mod is unknown.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <returns>The name.</returns>
        public static string GroupName(ShowcaseModGroup group)
        {
            if (group == null || group.PackageId.NullOrEmpty())
            {
                return "BT_Showcases_UnknownMod".Translate();
            }

            return group.Name.NullOrEmpty() ? group.PackageId : group.Name;
        }

        /// <summary>
        /// A mod's preview picture for the mods list: its own
        /// About/Preview.png, or else the first of its showcases' previews.
        /// </summary>
        /// <param name="group">The mod's group.</param>
        /// <returns>The texture, or null.</returns>
        public static Texture2D ModPreview(ShowcaseModGroup group)
        {
            if (group == null)
            {
                return null;
            }

            try
            {
                Texture2D own = ModLister.GetModWithIdentifier(
                    group.PackageId, ignorePostfix: true)?.PreviewImage;
                if (own != null)
                {
                    return own;
                }
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not load the preview "
                    + $"of {group.PackageId}: {e.Message}");
            }

            return DefsIn(group)
                .Select(d => d.PreviewTexture)
                .FirstOrDefault(t => t != null);
        }
    }
}
