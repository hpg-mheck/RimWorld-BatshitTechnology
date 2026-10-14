using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Regression tests over the Showcase API's player-facing text: the keyed
/// strings in Languages/English/Keyed/BT_Showcases.xml and the keys the
/// game code asks for. They run without the game.
/// </summary>
public sealed class ShowcaseXmlTests
{
    /// <summary>The prefix of every Showcase API key.</summary>
    private const string KeyPrefix = "BT_Showcases_";

    /// <summary>
    /// The prefix the code completes with a <see cref="ShowcaseSeverity"/>
    /// name ("BT_Showcases_Severity_" + severity).
    /// </summary>
    private const string SeverityPrefix = KeyPrefix + "Severity_";

    /// <summary>
    /// Matches a string literal naming a Showcase API key, or a key prefix
    /// (ending in an underscore).
    /// </summary>
    private static readonly Regex KeyLiteral =
        new Regex("\"(BT_Showcases_[A-Za-z0-9_]*)\"", RegexOptions.CultureInvariant);

    /// <summary>Gets the directory the fixtures are copied to.</summary>
    private static string FixturesDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    /// The keyed file parses, every key is prefixed and unique, and every
    /// value is non-blank and on one line (the game shows a literal line
    /// break as one; \n makes a break).
    /// </summary>
    [Fact(DisplayName = "Showcase keyed text is prefixed, unique and one line each")]
    public void Keyed_should_be_prefixed_unique_and_single_line()
    {
        List<XElement> entries = KeyedEntries().ToList();

        Assert.NotEmpty(entries);
        Assert.All(entries, e =>
        {
            Assert.StartsWith(KeyPrefix, e.Name.LocalName, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(e.Value), e.Name.LocalName);
            Assert.DoesNotContain("\n", e.Value, StringComparison.Ordinal);
            Assert.Equal(e.Value.Trim(), e.Value);
        });
        Assert.Empty(entries
            .GroupBy(e => e.Name.LocalName, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
    }

    /// <summary>
    /// Every key the code asks for exists, so no player ever sees a raw
    /// key; a key prefix stands for one key per severity.
    /// </summary>
    [Fact(DisplayName = "Every Showcase key the code uses has English text")]
    public void Code_keys_should_all_have_text()
    {
        HashSet<string> keyed = KeyedKeys();
        List<string> missing = KeysUsedInCode()
            .Where(k => !keyed.Contains(k))
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// Every keyed string is used by the code, so the file holds no
    /// leftovers for translators to work on in vain.
    /// </summary>
    [Fact(DisplayName = "Every Showcase keyed string is used by the code")]
    public void Keyed_strings_should_all_be_used()
    {
        HashSet<string> used = KeysUsedInCode();
        List<string> orphans = KeyedKeys().Where(k => !used.Contains(k)).ToList();

        Assert.Empty(orphans);
    }

    /// <summary>
    /// The caption pattern formats as the specification's example, "2 of
    /// 8: The Batshittium Reactor", without falling back.
    /// </summary>
    [Fact(DisplayName = "The caption pattern reads 2 of 8: title")]
    public void CaptionPattern_should_format_as_specified()
    {
        string pattern = KeyedValue("BT_Showcases_CaptionPattern");

        Assert.Equal(
            "2 of 8: The Batshittium Reactor",
            CaptionText.Format(pattern, 1, 8, "The Batshittium Reactor"));
        Assert.Equal(CaptionText.DefaultPattern, pattern);
    }

    /// <summary>
    /// Each value with arguments uses exactly the placeholders the code
    /// passes, from {0} up, so translations cannot drop one unnoticed.
    /// </summary>
    [Fact(DisplayName = "Showcase keyed placeholders match their arguments")]
    public void Keyed_placeholders_should_match_arguments()
    {
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["BT_Showcases_CountMany"] = 1,
            ["BT_Showcases_ModTitle"] = 1,
            ["BT_Showcases_Minutes"] = 1,
            ["BT_Showcases_Requires"] = 1,
            ["BT_Showcases_FactsPair"] = 2,
            ["BT_Showcases_NeedsMods"] = 1,
            ["BT_Showcases_FlagLine"] = 3,
            ["BT_Showcases_FlaggedMany"] = 1,
            ["BT_Showcases_FatalLine"] = 2,
            ["BT_Showcases_Refused"] = 1,
            ["BT_Showcases_ForceConfirm"] = 1,
            ["BT_Showcases_CaptionPattern"] = 3,
        };

        foreach (XElement entry in KeyedEntries())
        {
            string key = entry.Name.LocalName;
            int count = expected.TryGetValue(key, out int n) ? n : 0;
            List<int> found = Regex.Matches(entry.Value, @"\{(\d+)\}")
                .Select(m => int.Parse(m.Groups[1].Value))
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            Assert.True(
                found.SequenceEqual(Enumerable.Range(0, count)),
                $"{key} uses {{{string.Join("},{", found)}}}, expected {count}");
        }
    }

    /// <summary>
    /// The mod depends on the Harmony mod, with links the Mods page can
    /// follow, and loads after it: the Showcases button's patch needs
    /// 0Harmony.dll, which the Harmony mod supplies and this mod never
    /// ships.
    /// </summary>
    [Fact(DisplayName = "About.xml depends on and loads after Harmony")]
    public void About_should_depend_on_and_load_after_harmony()
    {
        XElement root = XDocument.Load(Path.Combine(FixturesDirectory, "About.xml")).Root
            ?? throw new InvalidOperationException("About.xml has no root.");
        List<XElement> dependencies = root.Element("modDependencies")
            ?.Elements("li").ToList() ?? new List<XElement>();
        XElement harmony = Assert.Single(dependencies);

        Assert.Equal("brrainz.harmony", harmony.Element("packageId")?.Value);
        Assert.Equal("Harmony", harmony.Element("displayName")?.Value);
        Assert.Equal(
            "steam://url/CommunityFilePage/2009463077",
            harmony.Element("steamWorkshopUrl")?.Value);
        Assert.StartsWith(
            "https://",
            harmony.Element("downloadUrl")?.Value ?? string.Empty,
            StringComparison.Ordinal);
        Assert.Equal(
            new[] { "brrainz.harmony" },
            root.Element("loadAfter")?.Elements("li").Select(e => e.Value).ToArray());
    }

    /// <summary>
    /// The Showcases button is the mod's one Harmony patch: one class
    /// creates a Harmony instance, with an id under the mod's package id,
    /// and nothing else patches.
    /// </summary>
    [Fact(DisplayName = "The Showcases button is the mod's only Harmony patch")]
    public void Harmony_should_patch_only_the_menu_button()
    {
        string source = Path.Combine(FixturesDirectory, "Source");
        List<string> patching = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(
                File.ReadAllText(f),
                @"new\s+Harmony\s*\(|\[HarmonyPatch|\.PatchAll\s*\("))
            .Select(Path.GetFileName)
            .Select(n => n!)
            .ToList();
        string button = File.ReadAllText(
            Path.Combine(source, "Showcases", "ShowcaseMenuButton.cs"));

        Assert.Equal(new[] { "ShowcaseMenuButton.cs" }, patching);
        Assert.Contains(
            "HarmonyId = \"breakpoint8088.BatshitTechnology.",
            button,
            StringComparison.Ordinal);
        Assert.Single(Regex.Matches(button, @"\.Patch\s*\("));
    }

    /// <summary>The keyed file's entries.</summary>
    /// <returns>The key elements.</returns>
    private static IEnumerable<XElement> KeyedEntries()
    {
        string path = Path.Combine(
            FixturesDirectory, "Languages", "English", "Keyed", "BT_Showcases.xml");
        XElement root = XDocument.Load(path).Root
            ?? throw new InvalidOperationException("BT_Showcases.xml has no root.");
        Assert.Equal("LanguageData", root.Name.LocalName);
        return root.Elements();
    }

    /// <summary>The keyed file's keys.</summary>
    /// <returns>The keys.</returns>
    private static HashSet<string> KeyedKeys() =>
        KeyedEntries()
            .Select(e => e.Name.LocalName)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>One keyed value.</summary>
    /// <param name="key">The key.</param>
    /// <returns>Its English text.</returns>
    private static string KeyedValue(string key) =>
        KeyedEntries().Single(e => e.Name.LocalName == key).Value;

    /// <summary>
    /// Every Showcase key named in the game code: literal keys, and one
    /// key per severity for the severity prefix.
    /// </summary>
    /// <returns>The keys.</returns>
    private static HashSet<string> KeysUsedInCode()
    {
        string source = Path.Combine(FixturesDirectory, "Source");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(
            source, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in KeyLiteral.Matches(File.ReadAllText(file)))
            {
                string literal = m.Groups[1].Value;
                if (literal == SeverityPrefix)
                {
                    foreach (string name in Enum.GetNames(typeof(ShowcaseSeverity)))
                    {
                        keys.Add(SeverityPrefix + name);
                    }
                }
                else if (!literal.EndsWith("_", StringComparison.Ordinal))
                {
                    keys.Add(literal);
                }
            }
        }

        Assert.NotEmpty(keys);
        return keys;
    }
}
