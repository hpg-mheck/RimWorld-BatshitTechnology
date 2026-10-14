using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Showcases;

/// <summary>
/// Whether a showcase's required mods (DLCs included) are all loaded.
/// </summary>
public sealed class RequirementCheck
{
    /// <summary>Creates a result.</summary>
    /// <param name="missing">The required ids that are not loaded.</param>
    private RequirementCheck(IReadOnlyList<string> missing)
    {
        Missing = missing;
    }

    /// <summary>Gets a result with nothing missing.</summary>
    public static RequirementCheck AllMet { get; } =
        new RequirementCheck(Array.Empty<string>());

    /// <summary>Gets the required package ids that are not loaded.</summary>
    public IReadOnlyList<string> Missing { get; }

    /// <summary>Gets a value indicating whether nothing is missing.</summary>
    public bool Met => Missing.Count == 0;

    /// <summary>
    /// Checks required ids against the loaded mod list. Ids compare without
    /// regard to case or RimWorld's Workshop postfix; blanks are ignored.
    /// </summary>
    /// <param name="required">The required ids; may be null.</param>
    /// <param name="loaded">The loaded mods' ids; may be null.</param>
    /// <returns>The result, listing missing ids in declared order.</returns>
    public static RequirementCheck Evaluate(
        IEnumerable<string?>? required, IEnumerable<string?>? loaded)
    {
        HashSet<string> active = PackageIds.ToSet(loaded);
        List<string> missing = PackageIds.Distinct(required)
            .Where(id => !active.Contains(id))
            .ToList();
        return missing.Count == 0 ? AllMet : new RequirementCheck(missing);
    }
}

/// <summary>
/// Matches a showcase's compatibility flags against the loaded mods. The
/// menu warns only about mods the showcase itself flags.
/// </summary>
public static class ShowcaseCompatibility
{
    /// <summary>
    /// The flags whose mod is loaded, worst first, then by package id. A
    /// mod flagged twice keeps only its worst flag.
    /// </summary>
    /// <param name="flags">
    /// The showcase's flags; may be null or hold nulls.
    /// </param>
    /// <param name="loaded">The loaded mods' ids; may be null.</param>
    /// <returns>The matching flags.</returns>
    public static IReadOnlyList<CompatibilityFlag> Matches(
        IEnumerable<CompatibilityFlag?>? flags, IEnumerable<string?>? loaded)
    {
        HashSet<string> active = PackageIds.ToSet(loaded);
        return (flags ?? Enumerable.Empty<CompatibilityFlag?>())
            .Where(f => f != null && f.PackageId.Length > 0
                && active.Contains(f.PackageId))
            .Select(f => f!)
            .GroupBy(f => f.PackageId, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(f => f.Severity).First())
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.PackageId, StringComparer.Ordinal)
            .ToList();
    }
}

/// <summary>What the menu may do with a showcase right now.</summary>
public enum LaunchVerdict
{
    /// <summary>Nothing flagged; Start works.</summary>
    Clear,

    /// <summary>
    /// A loaded mod is flagged as a concern, a warning or a known
    /// incompatibility; the details page lists it, and Start works.
    /// </summary>
    Warned,

    /// <summary>
    /// A loaded mod is flagged fatal; Start is refused, and only a
    /// confirmed Force start launches it.
    /// </summary>
    Refused,

    /// <summary>A fatal flag, overridden by Force start.</summary>
    Forced,

    /// <summary>
    /// A required mod is not loaded. The showcase is greyed out; forcing
    /// does not help, because its content is not there.
    /// </summary>
    Unavailable,
}

/// <summary>
/// The decision for one showcase: its verdict, and the evidence for it
/// (missing requirements and matching flags), for the menu to show.
/// </summary>
public sealed class LaunchDecision
{
    /// <summary>Creates a decision.</summary>
    /// <param name="verdict">The verdict.</param>
    /// <param name="requirements">The requirement check.</param>
    /// <param name="matches">The matching compatibility flags.</param>
    private LaunchDecision(
        LaunchVerdict verdict,
        RequirementCheck requirements,
        IReadOnlyList<CompatibilityFlag> matches)
    {
        Verdict = verdict;
        Requirements = requirements;
        Matches = matches;
    }

    /// <summary>Gets the verdict.</summary>
    public LaunchVerdict Verdict { get; }

    /// <summary>Gets the requirement check behind the verdict.</summary>
    public RequirementCheck Requirements { get; }

    /// <summary>
    /// Gets the loaded mods the showcase flags, worst first.
    /// </summary>
    public IReadOnlyList<CompatibilityFlag> Matches { get; }

    /// <summary>
    /// Gets a value indicating whether Start may launch it.
    /// </summary>
    public bool CanStart => Verdict == LaunchVerdict.Clear
        || Verdict == LaunchVerdict.Warned
        || Verdict == LaunchVerdict.Forced;

    /// <summary>
    /// Gets a value indicating whether Force start would launch it, that
    /// is, whether only a fatal flag stands in the way.
    /// </summary>
    public bool CanForce => Verdict == LaunchVerdict.Refused;

    /// <summary>Gets the matching flags that are fatal.</summary>
    public IReadOnlyList<CompatibilityFlag> FatalMatches =>
        Matches.Where(m => m.Severity == ShowcaseSeverity.Fatal).ToList();

    /// <summary>
    /// Decides. Missing requirements make a showcase unavailable, whatever
    /// else; a fatal flag refuses it unless <paramref name="force"/> is
    /// set; any other flag warns; otherwise it is clear.
    /// </summary>
    /// <param name="requirements">
    /// The requirement check; null means all met.
    /// </param>
    /// <param name="matches">The matching flags; null means none.</param>
    /// <param name="force">Whether the player confirmed Force start.</param>
    /// <returns>The decision.</returns>
    public static LaunchDecision Decide(
        RequirementCheck? requirements,
        IReadOnlyList<CompatibilityFlag?>? matches,
        bool force)
    {
        RequirementCheck req = requirements ?? RequirementCheck.AllMet;
        IEnumerable<CompatibilityFlag?> given =
            matches ?? Array.Empty<CompatibilityFlag?>();
        List<CompatibilityFlag> found = given
            .Where(m => m != null)
            .Select(m => m!)
            .OrderByDescending(m => m.Severity)
            .ThenBy(m => m.PackageId, StringComparer.Ordinal)
            .ToList();

        LaunchVerdict verdict;
        if (!req.Met)
        {
            verdict = LaunchVerdict.Unavailable;
        }
        else if (found.Any(m => m.Severity == ShowcaseSeverity.Fatal))
        {
            verdict = force ? LaunchVerdict.Forced : LaunchVerdict.Refused;
        }
        else
        {
            verdict = found.Count > 0
                ? LaunchVerdict.Warned
                : LaunchVerdict.Clear;
        }

        return new LaunchDecision(verdict, req, found);
    }

    /// <summary>
    /// Evaluates a showcase against the loaded mods in one step.
    /// </summary>
    /// <param name="entry">The showcase.</param>
    /// <param name="loaded">The loaded mods' ids; may be null.</param>
    /// <param name="force">Whether the player confirmed Force start.</param>
    /// <returns>The decision.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entry"/> is null.
    /// </exception>
    public static LaunchDecision Evaluate(
        ShowcaseEntry entry, IEnumerable<string?>? loaded, bool force)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        List<string?> mods = (loaded ?? Enumerable.Empty<string?>()).ToList();
        return Decide(
            RequirementCheck.Evaluate(entry.RequiredMods, mods),
            ShowcaseCompatibility.Matches(entry.Compatibility, mods),
            force);
    }
}
