using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Showcases;

/// <summary>
/// How badly a showcase gets on with another mod, mildest first. A
/// showcase declares these itself (docs/SHOWCASE-API.md, "Other Mods");
/// the menu warns about the first three and refuses to start on the last
/// unless forced.
/// </summary>
public enum ShowcaseSeverity
{
    /// <summary>Something the viewer may notice; it runs normally.</summary>
    Concern = 0,

    /// <summary>
    /// Likely to spoil part of the showcase; it still runs.
    /// </summary>
    Warning = 1,

    /// <summary>Known not to work properly together; it still runs.</summary>
    KnownIncompatibility = 2,

    /// <summary>
    /// Known to break the game. Start is refused unless forced.
    /// </summary>
    Fatal = 3,
}

/// <summary>
/// One entry in a showcase's compatibility list: another mod, by package
/// id, how badly the two get on, and a short note for the player.
/// </summary>
public sealed class CompatibilityFlag
{
    /// <summary>Creates a flag.</summary>
    /// <param name="packageId">
    /// The other mod's package id, in any case; it is normalized.
    /// </param>
    /// <param name="severity">How badly they get on.</param>
    /// <param name="note">
    /// What goes wrong, for the player; may be null.
    /// </param>
    public CompatibilityFlag(
        string? packageId, ShowcaseSeverity severity, string? note)
    {
        PackageId = PackageIds.Normalize(packageId);
        Severity = severity;
        Note = note?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Gets the other mod's package id, lowercase and trimmed.
    /// </summary>
    public string PackageId { get; }

    /// <summary>Gets how badly the two mods get on.</summary>
    public ShowcaseSeverity Severity { get; }

    /// <summary>Gets the note for the player, or an empty string.</summary>
    public string Note { get; }

    /// <summary>A readable form, for logs and test failures.</summary>
    /// <returns>The id, severity and note.</returns>
    public override string ToString() => $"{PackageId} ({Severity}): {Note}";
}

/// <summary>
/// A showcase as the catalogue sees it, free of RimWorld types: what to
/// list, which mod it came from, and what it needs from the mod list.
/// </summary>
public sealed class ShowcaseEntry
{
    /// <summary>Creates an entry.</summary>
    /// <param name="defName">
    /// The showcase's def name; must not be blank.
    /// </param>
    /// <param name="label">
    /// What the menu calls it; blank means the def name.
    /// </param>
    /// <param name="modPackageId">
    /// The package id of the mod that declared it; blank when unknown.
    /// </param>
    /// <param name="modName">That mod's display name; may be blank.</param>
    /// <param name="order">Sort order within its mod, lowest first.</param>
    /// <param name="requiredMods">
    /// Package ids it requires; may be null.
    /// </param>
    /// <param name="compatibility">
    /// Its compatibility flags; may be null.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="defName"/> is null or blank.
    /// </exception>
    public ShowcaseEntry(
        string defName,
        string? label,
        string? modPackageId,
        string? modName,
        int order = 0,
        IEnumerable<string?>? requiredMods = null,
        IEnumerable<CompatibilityFlag?>? compatibility = null)
    {
        if (string.IsNullOrWhiteSpace(defName))
        {
            throw new ArgumentException(
                "A showcase needs a def name.", nameof(defName));
        }

        DefName = defName.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? DefName : label!.Trim();
        ModPackageId = PackageIds.Normalize(modPackageId);
        ModName = modName?.Trim() ?? string.Empty;
        Order = order;
        RequiredMods = PackageIds.Distinct(requiredMods);
        IEnumerable<CompatibilityFlag?> flags =
            compatibility ?? Enumerable.Empty<CompatibilityFlag?>();
        Compatibility = flags
            .Where(f => f != null && f.PackageId.Length > 0)
            .Select(f => f!)
            .ToList();
    }

    /// <summary>Gets the showcase's def name.</summary>
    public string DefName { get; }

    /// <summary>Gets what the menu calls it.</summary>
    public string Label { get; }

    /// <summary>
    /// Gets the declaring mod's package id, normalized; empty when unknown.
    /// </summary>
    public string ModPackageId { get; }

    /// <summary>
    /// Gets the declaring mod's display name; may be empty.
    /// </summary>
    public string ModName { get; }

    /// <summary>Gets the sort order within its mod, lowest first.</summary>
    public int Order { get; }

    /// <summary>
    /// Gets the package ids it requires, normalized and distinct.
    /// </summary>
    public IReadOnlyList<string> RequiredMods { get; }

    /// <summary>Gets its compatibility flags, blank ids dropped.</summary>
    public IReadOnlyList<CompatibilityFlag> Compatibility { get; }

    /// <summary>A readable form, for logs and test failures.</summary>
    /// <returns>The def name and mod.</returns>
    public override string ToString() => $"{DefName} ({ModPackageId})";
}
