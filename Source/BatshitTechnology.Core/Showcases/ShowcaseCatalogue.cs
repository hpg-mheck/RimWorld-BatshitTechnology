using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Showcases;

/// <summary>
/// One mod in the Showcases menu: its id, its name, and the showcases it
/// offers, in menu order.
/// </summary>
public sealed class ShowcaseModGroup
{
    /// <summary>Creates a group.</summary>
    /// <param name="packageId">The mod's normalized package id.</param>
    /// <param name="name">The mod's display name.</param>
    /// <param name="showcases">Its showcases, already in menu order.</param>
    public ShowcaseModGroup(
        string packageId, string name, IReadOnlyList<ShowcaseEntry> showcases)
    {
        PackageId = packageId ?? string.Empty;
        Name = name ?? string.Empty;
        Showcases = showcases ?? Array.Empty<ShowcaseEntry>();
    }

    /// <summary>
    /// Gets the mod's normalized package id; empty when unknown.
    /// </summary>
    public string PackageId { get; }

    /// <summary>Gets the mod's display name.</summary>
    public string Name { get; }

    /// <summary>Gets the mod's showcases, in menu order.</summary>
    public IReadOnlyList<ShowcaseEntry> Showcases { get; }

    /// <summary>Gets how many showcases the mod offers.</summary>
    public int Count => Showcases.Count;

    /// <summary>A readable form, for logs and test failures.</summary>
    /// <returns>The name, id and count.</returns>
    public override string ToString() => $"{Name} ({PackageId}): {Count}";
}

/// <summary>
/// Groups showcases by the mod that declared them, for the first two
/// levels of the Showcases menu. A mod is listed only if it has at least
/// one showcase, because groups are built from the showcases themselves.
/// </summary>
public static class ShowcaseCatalogue
{
    /// <summary>
    /// The name shown for showcases whose mod is unknown (a def made in
    /// code, say).
    /// </summary>
    public const string UnknownModName = "Unknown mod";

    /// <summary>
    /// Groups showcases by mod. Groups sort by mod name, ignoring case,
    /// then by package id; showcases within a group by order, then label,
    /// then def name. A repeated def name keeps its first entry.
    /// </summary>
    /// <param name="entries">
    /// The showcases; may be null or hold nulls.
    /// </param>
    /// <returns>One group per mod with any showcase.</returns>
    public static IReadOnlyList<ShowcaseModGroup> GroupByMod(
        IEnumerable<ShowcaseEntry?>? entries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<ShowcaseEntry>();
        IEnumerable<ShowcaseEntry?> all =
            entries ?? Enumerable.Empty<ShowcaseEntry?>();
        foreach (ShowcaseEntry? entry in all)
        {
            if (entry != null && seen.Add(entry.DefName))
            {
                unique.Add(entry);
            }
        }

        return unique
            .GroupBy(e => e.ModPackageId, StringComparer.Ordinal)
            .Select(g => new ShowcaseModGroup(
                g.Key,
                GroupName(g.Key, g),
                g.OrderBy(e => e.Order)
                    .ThenBy(e => e.Label, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.DefName, StringComparer.Ordinal)
                    .ToList()))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.PackageId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Finds a mod's group by package id, in any case.</summary>
    /// <param name="groups">The groups; may be null.</param>
    /// <param name="packageId">The mod's package id.</param>
    /// <returns>The group, or null if the mod offers no showcase.</returns>
    public static ShowcaseModGroup? FindGroup(
        IEnumerable<ShowcaseModGroup>? groups, string? packageId)
    {
        string id = PackageIds.Normalize(packageId);
        return (groups ?? Enumerable.Empty<ShowcaseModGroup>())
            .FirstOrDefault(g => g != null && g.PackageId == id);
    }

    /// <summary>
    /// A group's display name: the first non-blank mod name among its
    /// showcases, else its package id, else <see cref="UnknownModName"/>.
    /// </summary>
    /// <param name="packageId">The group's package id.</param>
    /// <param name="entries">Its showcases.</param>
    /// <returns>The name.</returns>
    private static string GroupName(
        string packageId, IEnumerable<ShowcaseEntry> entries)
    {
        string? named = entries
            .Select(e => e.ModName)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        if (named != null)
        {
            return named;
        }

        return packageId.Length > 0 ? packageId : UnknownModName;
    }
}
