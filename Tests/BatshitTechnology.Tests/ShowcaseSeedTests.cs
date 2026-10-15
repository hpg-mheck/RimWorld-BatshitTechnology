using System;
using System.Linq;
using System.Text;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the showcase seed's rules (docs/SHOWCASE-API.md, "Seeds"): the
/// stable FNV-1a hash they rest on, which seed a launch uses, what a seed
/// may hold, and the Rand seeds derived from it.
/// </summary>
public sealed class ShowcaseSeedTests
{
    /// <summary>A random-seed maker that must never be asked.</summary>
    /// <returns>Never returns.</returns>
    private static string NeverAsked() =>
        throw new InvalidOperationException("the random seed was made");

    /// <summary>
    /// The 32-bit and 64-bit hashes match FNV-1a's published test vectors,
    /// so a seed means the same in every process and runtime.
    /// </summary>
    [Fact(DisplayName = "FNV-1a matches its published test vectors")]
    public void Fnv1a_should_match_the_published_vectors()
    {
        Assert.Equal(0x811c9dc5u, Fnv1a.Hash32(string.Empty));
        Assert.Equal(0xe40c292cu, Fnv1a.Hash32("a"));
        Assert.Equal(0xbf9cf968u, Fnv1a.Hash32("foobar"));
        Assert.Equal(0xcbf29ce484222325UL, Fnv1a.Hash64(string.Empty));
        Assert.Equal(0xaf63dc4c8601ec8cUL, Fnv1a.Hash64("a"));
        Assert.Equal(0x85944171f73967e8UL, Fnv1a.Hash64("foobar"));
        Assert.Equal(Fnv1a.Hash32(string.Empty), Fnv1a.Hash32(null));
    }

    /// <summary>
    /// Text beyond ASCII hashes over its UTF-8 bytes, as the reference
    /// implementation does (é is C3 A9), and appending continues a hash.
    /// </summary>
    [Fact(DisplayName = "FNV-1a hashes UTF-8 bytes and appends exactly")]
    public void Fnv1a_should_hash_utf8_and_append()
    {
        Assert.Equal(0x1e9de8c1u, Fnv1a.Hash32("é"));
        Assert.Equal(Reference32(Encoding.UTF8.GetBytes("café ☃")),
            Fnv1a.Hash32("café ☃"));
        Assert.Equal(Fnv1a.Hash64("foobar"),
            Fnv1a.Append64(Fnv1a.Hash64("foo"), "bar"));
        ulong byBytes = Fnv1a.Offset64;
        foreach (byte b in BitConverter.GetBytes(0x0102030405060708UL))
        {
            byBytes = Fnv1a.AppendByte64(byBytes, b);
        }

        Assert.True(BitConverter.IsLittleEndian);
        Assert.Equal(byBytes, Fnv1a.Append64(Fnv1a.Offset64, 0x0102030405060708UL));
    }

    /// <summary>
    /// The command line's seed comes first, for trials; then the def's;
    /// then a fresh random one, made only when needed.
    /// </summary>
    [Fact(DisplayName = "Seed precedence: command line, then def, then random")]
    public void Resolve_should_prefer_command_line_then_def_then_random()
    {
        ResolvedSeed line = ShowcaseSeed.Resolve("tour-07", "pinned", NeverAsked);
        Assert.Equal("tour-07", line.Text);
        Assert.Equal(SeedSource.CommandLine, line.Source);
        Assert.Empty(line.Refused);

        ResolvedSeed fromDef = ShowcaseSeed.Resolve(null, "pinned", NeverAsked);
        Assert.Equal("pinned", fromDef.Text);
        Assert.Equal(SeedSource.Def, fromDef.Source);

        ResolvedSeed blankLine = ShowcaseSeed.Resolve("   ", " pinned ", NeverAsked);
        Assert.Equal("pinned", blankLine.Text);
        Assert.Equal(SeedSource.Def, blankLine.Source);

        int made = 0;
        ResolvedSeed random = ShowcaseSeed.Resolve(null, "", () =>
        {
            made++;
            return "parsnipmiller";
        });
        Assert.Equal("parsnipmiller", random.Text);
        Assert.Equal(SeedSource.Random, random.Source);
        Assert.Equal(1, made);
    }

    /// <summary>
    /// A malformed seed is refused, said why, and the next in order is
    /// used: so a bad trial seed never silently pins the wrong map.
    /// </summary>
    [Fact(DisplayName = "A malformed seed is refused and the next one used")]
    public void Resolve_should_refuse_malformed_seeds()
    {
        ResolvedSeed seed = ShowcaseSeed.Resolve("two words", "pinned", NeverAsked);
        Assert.Equal("pinned", seed.Text);
        Assert.Equal(SeedSource.Def, seed.Source);
        string refusal = Assert.Single(seed.Refused);
        Assert.Contains("-showcaseseed=two words", refusal, StringComparison.Ordinal);
        Assert.Contains("' ' is not allowed", refusal, StringComparison.Ordinal);

        ResolvedSeed both = ShowcaseSeed.Resolve("a=b", "(x)", () => "fresh");
        Assert.Equal("fresh", both.Text);
        Assert.Equal(SeedSource.Random, both.Source);
        Assert.Equal(2, both.Refused.Count);
        Assert.Contains("the def's seed '(x)'", both.Refused[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A random seed is made well formed, and when the maker gives
    /// nothing usable or throws, a fallback is used, so a launch always
    /// has a seed it can log for pinning.
    /// </summary>
    [Fact(DisplayName = "A random seed is always usable")]
    public void Resolve_should_always_make_a_usable_random_seed()
    {
        Assert.Equal("cometpicnic",
            ShowcaseSeed.Resolve(null, null, () => " comet picnic! ").Text);
        foreach (Func<string?> maker in new Func<string?>[]
        {
            () => null,
            () => "!!! ",
            () => throw new InvalidOperationException("no grammar"),
        })
        {
            ResolvedSeed seed = ShowcaseSeed.Resolve(null, null, maker);
            Assert.Equal(SeedSource.Random, seed.Source);
            Assert.True(ShowcaseSeed.IsWellFormed(seed.Text), seed.Text);
            Assert.StartsWith("r", seed.Text, StringComparison.Ordinal);
            Assert.Equal(9, seed.Text.Length);
        }

        Assert.Throws<ArgumentNullException>(
            () => ShowcaseSeed.Resolve(null, null, null!));
    }

    /// <summary>
    /// A seed holds 1 to 64 ASCII letters, digits, '-', '_' and '.': no
    /// '=' (RimWorld's command line splits there), no spaces or brackets
    /// (the SEED cue's), nothing a folder name cannot hold.
    /// </summary>
    [Fact(DisplayName = "A seed is 1 to 64 letters, digits, '-', '_' or '.'")]
    public void IsWellFormed_should_allow_only_the_safe_characters()
    {
        foreach (string good in new[]
        {
            "a", "tour-01", "Tour_01.b", "parsnipmiller", new string('x', 64),
        })
        {
            Assert.True(ShowcaseSeed.IsWellFormed(good), good);
            Assert.Null(ShowcaseSeed.WhyMalformed(good));
        }

        foreach (string? bad in new[]
        {
            null, string.Empty, " a", "a b", "a=b", "a(b)", "a/b", "a\\b",
            "café", "tab\t", "a:b", new string('x', 65),
        })
        {
            Assert.False(ShowcaseSeed.IsWellFormed(bad), bad ?? "(null)");
            Assert.NotNull(ShowcaseSeed.WhyMalformed(bad));
        }

        Assert.Equal("ab", ShowcaseSeed.Sanitize("a b"));
        Assert.Null(ShowcaseSeed.Sanitize("   "));
        Assert.Equal(64, ShowcaseSeed.Sanitize(new string('y', 100))!.Length);
    }

    /// <summary>
    /// Each purpose derives its own Rand seed: the 32-bit FNV-1a hash of
    /// "seed/purpose", pinned against an independent reference, case
    /// sensitive, and different for every seed and purpose tried.
    /// </summary>
    [Fact(DisplayName = "Derived Rand seeds are stable, and differ by seed and purpose")]
    public void Derive_should_be_stable_and_distinct()
    {
        Assert.Equal(-581421174, ShowcaseSeed.Derive("tour-01", ShowcaseSeed.World));
        Assert.Equal(-1268363200, ShowcaseSeed.Derive("tour-01", ShowcaseSeed.Stage));
        Assert.Equal(
            unchecked((int)Fnv1a.Hash32("tour-01/prep")),
            ShowcaseSeed.Derive("tour-01", ShowcaseSeed.Prep));

        string[] seeds = Enumerable.Range(1, 32).Select(i => $"tour-{i:00}")
            .Concat(new[] { "Tour-01", "parsnipmiller" }).ToArray();
        string[] purposes = { ShowcaseSeed.World, ShowcaseSeed.Prep, ShowcaseSeed.Stage };
        int[] derived = seeds.SelectMany(s => purposes.Select(p => ShowcaseSeed.Derive(s, p)))
            .ToArray();
        Assert.Equal(derived.Length, derived.Distinct().Count());

        var resolved = new ResolvedSeed("tour-01", SeedSource.Def);
        Assert.Equal(ShowcaseSeed.Derive("tour-01", "x"), resolved.Derive("x"));
    }

    /// <summary>
    /// The SEED cue reads "text (source)", so a run's seed, random or
    /// not, can be copied out of the log to pin it; a resolved seed must
    /// be well formed.
    /// </summary>
    [Fact(DisplayName = "The SEED cue's detail names the seed and its source")]
    public void CueDetail_should_name_the_seed_and_source()
    {
        Assert.Equal("tour-07 (command line)",
            new ResolvedSeed("tour-07", SeedSource.CommandLine).CueDetail);
        Assert.Equal("pinned (def)", new ResolvedSeed("pinned", SeedSource.Def).CueDetail);
        Assert.Equal("cometpicnic (random)",
            new ResolvedSeed("cometpicnic", SeedSource.Random).CueDetail);
        Assert.Equal("command line", ShowcaseSeed.SourceName(SeedSource.CommandLine));
        Assert.Equal("showcaseseed", ShowcaseSeed.CommandLineArg);
        Assert.Throws<ArgumentException>(() => new ResolvedSeed("a b", SeedSource.Def));
    }

    /// <summary>
    /// A byte-by-byte 32-bit FNV-1a, written out again here as the
    /// reference the library is checked against.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The hash.</returns>
    private static uint Reference32(byte[] bytes)
    {
        uint hash = 2166136261u;
        foreach (byte b in bytes)
        {
            hash = unchecked((hash ^ b) * 16777619u);
        }

        return hash;
    }
}
