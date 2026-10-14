using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BatshitTechnology.Showcases;

/// <summary>
/// Package-id helpers. RimWorld compares package ids without regard to
/// case (it lowercases them), so the showcase API normalizes every id it
/// is given before comparing.
/// </summary>
public static class PackageIds
{
    /// <summary>
    /// The postfix RimWorld adds to a Workshop copy's id when a local copy
    /// with the same id is also installed (<c>ModMetaData.SteamModPostfix</c>
    /// in RimWorld 1.6.4871).
    /// </summary>
    public const string SteamPostfix = "_steam";

    /// <summary>
    /// RimWorld's own package-id rule
    /// (<c>ModMetaData.PackageIdFormatRegex</c>
    /// in 1.6.4871): 1 to 60 letters, digits and periods, at least one
    /// period, none leading, trailing or doubled.
    /// </summary>
    private static readonly Regex FormatRegex = new Regex(
        "(?=.{1,60}$)^(?!\\.)(?=.*?[.])(?!.*([.])\\1+)"
        + "[a-zA-Z0-9.]{1,}[a-zA-Z0-9]{1}$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Lowercases and trims an id, and drops RimWorld's Workshop postfix,
    /// so two spellings of one mod compare equal.
    /// </summary>
    /// <param name="id">The id; may be null.</param>
    /// <returns>The normalized id, or an empty string.</returns>
    public static string Normalize(string? id)
    {
        string normal = (id ?? string.Empty).Trim().ToLowerInvariant();
        if (normal.EndsWith(SteamPostfix, StringComparison.Ordinal))
        {
            normal = normal.Substring(0, normal.Length - SteamPostfix.Length);
        }

        return normal;
    }

    /// <summary>
    /// Whether an id is one RimWorld would accept in About.xml.
    /// </summary>
    /// <param name="id">The id, as written; may be null.</param>
    /// <returns>True if it is well formed.</returns>
    public static bool IsWellFormed(string? id) =>
        id != null && FormatRegex.IsMatch(id);

    /// <summary>
    /// Normalizes a list of ids, dropping blanks and duplicates and keeping
    /// the first spelling's position.
    /// </summary>
    /// <param name="ids">The ids; may be null or hold nulls.</param>
    /// <returns>The distinct, normalized ids.</returns>
    public static IReadOnlyList<string> Distinct(IEnumerable<string?>? ids) =>
        (ids ?? Enumerable.Empty<string?>())
            .Select(Normalize)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>A set of normalized ids, for fast membership tests.</summary>
    /// <param name="ids">The ids; may be null or hold nulls.</param>
    /// <returns>The set.</returns>
    public static HashSet<string> ToSet(IEnumerable<string?>? ids) =>
        new HashSet<string>(Distinct(ids), StringComparer.Ordinal);
}
