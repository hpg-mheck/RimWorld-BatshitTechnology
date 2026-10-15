using System;
using System.Collections.Generic;
using System.Text;

namespace BatshitTechnology.Showcases;

/// <summary>Where a showcase's seed came from.</summary>
public enum SeedSource
{
    /// <summary>The <c>-showcaseseed=&lt;text&gt;</c> command line.</summary>
    CommandLine,

    /// <summary>The showcase def's own <c>seed</c>.</summary>
    Def,

    /// <summary>A fresh random seed, made at launch.</summary>
    Random,
}

/// <summary>
/// A showcase's seed as resolved at launch: its text, where it came from,
/// and any seed offered but refused because it was malformed.
/// </summary>
public sealed class ResolvedSeed
{
    /// <summary>Creates a resolved seed.</summary>
    /// <param name="text">The seed; well formed.</param>
    /// <param name="source">Where it came from.</param>
    /// <param name="refused">Seeds offered but refused, with why.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="text"/> is not well formed.
    /// </exception>
    public ResolvedSeed(
        string text, SeedSource source, IReadOnlyList<string>? refused = null)
    {
        if (!ShowcaseSeed.IsWellFormed(text))
        {
            throw new ArgumentException(
                $"'{text}' is not a well-formed showcase seed.", nameof(text));
        }

        Text = text;
        Source = source;
        Refused = refused ?? Array.Empty<string>();
    }

    /// <summary>Gets the seed's text.</summary>
    public string Text { get; }

    /// <summary>Gets where the seed came from.</summary>
    public SeedSource Source { get; }

    /// <summary>
    /// Gets the seeds offered but refused as malformed, each with where
    /// it came from, for an error message.
    /// </summary>
    public IReadOnlyList<string> Refused { get; }

    /// <summary>
    /// Gets the SEED cue's detail: the text and, in brackets, the source,
    /// as in <c>tour-07 (command line)</c>. Seeds hold no spaces or
    /// brackets, so tooling can split it.
    /// </summary>
    public string CueDetail =>
        $"{Text} ({ShowcaseSeed.SourceName(Source)})";

    /// <summary>Derives a Rand seed for one purpose from this seed.</summary>
    /// <param name="purpose">The purpose, such as <see cref="ShowcaseSeed.World"/>.</param>
    /// <returns>The derived seed.</returns>
    public int Derive(string purpose) => ShowcaseSeed.Derive(Text, purpose);
}

/// <summary>
/// The rules for a showcase's seed: which seed a launch uses, what a seed
/// may hold, and how the Rand seeds for each stage of a launch are
/// derived from it (docs/SHOWCASE-API.md, "Seeds").
/// </summary>
public static class ShowcaseSeed
{
    /// <summary>
    /// The command-line key giving a seed, as in
    /// <c>-showcaseseed=tour-07</c>. It overrides the def's seed, for
    /// trials.
    /// </summary>
    public const string CommandLineArg = "showcaseseed";

    /// <summary>The longest seed allowed, in characters.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// The purpose for generating the game: the quick-test set-up (the
    /// world's seed string, the starting tile and colonists) and the
    /// choice of the def's biome.
    /// </summary>
    public const string World = "world";

    /// <summary>
    /// The purpose for preparing the map's generation: the colonists'
    /// work priorities and the player's settlement.
    /// </summary>
    public const string Prep = "prep";

    /// <summary>
    /// The purpose for the global random state when the showcase's worker
    /// is set up, so it stages the same things in the same places.
    /// </summary>
    public const string Stage = "stage";

    /// <summary>
    /// Whether text is a usable seed: 1 to <see cref="MaxLength"/>
    /// characters, each an ASCII letter or digit, '-', '_' or '.'. That
    /// keeps a seed whole on RimWorld's command line (which splits at
    /// '='), in the SEED cue (which a space or bracket would break), and
    /// in a folder name.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>True if it is well formed.</returns>
    public static bool IsWellFormed(string? text)
    {
        if (string.IsNullOrEmpty(text) || text!.Length > MaxLength)
        {
            return false;
        }

        foreach (char c in text)
        {
            if (!Allowed(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Trims text and gives null for a blank, the way an unset def field
    /// or command line reads.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text, or null.</returns>
    public static string? Normalize(string? text)
    {
        string? trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Makes any text into a well-formed seed: its allowed characters, at
    /// most <see cref="MaxLength"/> of them. Used for a random seed made
    /// from words, which should never hold anything else.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The seed, or null if nothing allowed was left.</returns>
    public static string? Sanitize(string? text)
    {
        if (text == null)
        {
            return null;
        }

        var kept = new StringBuilder();
        foreach (char c in text)
        {
            if (Allowed(c) && kept.Length < MaxLength)
            {
                kept.Append(c);
            }
        }

        return kept.Length == 0 ? null : kept.ToString();
    }

    /// <summary>
    /// Resolves a launch's seed, in order: the command line's, else the
    /// def's, else a fresh random one. A blank one is unset; a malformed
    /// one is refused (and listed in <see cref="ResolvedSeed.Refused"/>),
    /// and the next in order is used.
    /// </summary>
    /// <param name="commandLine">The <c>-showcaseseed=</c> value, or null.</param>
    /// <param name="defSeed">The def's seed, or null.</param>
    /// <param name="makeRandom">
    /// Makes a fresh random seed; its result is sanitized, and a fixed
    /// fallback is used if nothing usable comes back or it throws.
    /// </param>
    /// <returns>The resolved seed.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="makeRandom"/> is null.
    /// </exception>
    public static ResolvedSeed Resolve(
        string? commandLine, string? defSeed, Func<string?> makeRandom)
    {
        if (makeRandom == null)
        {
            throw new ArgumentNullException(nameof(makeRandom));
        }

        var refused = new List<string>();
        string? line = Normalize(commandLine);
        if (line != null)
        {
            if (IsWellFormed(line))
            {
                return new ResolvedSeed(line, SeedSource.CommandLine, refused);
            }

            refused.Add($"-{CommandLineArg}={line}: {WhyMalformed(line)}");
        }

        string? fromDef = Normalize(defSeed);
        if (fromDef != null)
        {
            if (IsWellFormed(fromDef))
            {
                return new ResolvedSeed(fromDef, SeedSource.Def, refused);
            }

            refused.Add($"the def's seed '{fromDef}': {WhyMalformed(fromDef)}");
        }

        string? random;
        try
        {
            random = Sanitize(makeRandom());
        }
        catch (Exception)
        {
            random = null;
        }

        return new ResolvedSeed(random ?? FallbackRandom(), SeedSource.Random, refused);
    }

    /// <summary>Why text is not a well-formed seed, for a message.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The reason, or null if it is well formed.</returns>
    public static string? WhyMalformed(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "it is empty";
        }

        if (text!.Length > MaxLength)
        {
            return $"it is longer than {MaxLength} characters";
        }

        foreach (char c in text)
        {
            if (!Allowed(c))
            {
                return $"'{c}' is not allowed (only ASCII letters, digits, "
                    + "'-', '_' and '.')";
            }
        }

        return null;
    }

    /// <summary>The SEED cue's name for a source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>"command line", "def" or "random".</returns>
    public static string SourceName(SeedSource source) => source switch
    {
        SeedSource.CommandLine => "command line",
        SeedSource.Def => "def",
        _ => "random",
    };

    /// <summary>
    /// Derives a Rand seed for one purpose: the 32-bit FNV-1a hash of the
    /// seed, a '/', and the purpose. Each stage of a launch gets its own,
    /// so they do not repeat one another's random sequences.
    /// </summary>
    /// <param name="seed">The showcase's seed.</param>
    /// <param name="purpose">The purpose.</param>
    /// <returns>The derived seed.</returns>
    public static int Derive(string seed, string purpose)
    {
        unchecked
        {
            return (int)Fnv1a.Hash32((seed ?? string.Empty) + "/" + (purpose ?? string.Empty));
        }
    }

    /// <summary>Whether a character may appear in a seed.</summary>
    /// <param name="c">The character.</param>
    /// <returns>True if allowed.</returns>
    private static bool Allowed(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
        || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.';

    /// <summary>
    /// A random seed when the game's own maker gave nothing usable: eight
    /// hex digits from a fresh GUID.
    /// </summary>
    /// <returns>The seed.</returns>
    private static string FallbackRandom() =>
        "r" + Guid.NewGuid().ToString("N").Substring(0, 8);
}
