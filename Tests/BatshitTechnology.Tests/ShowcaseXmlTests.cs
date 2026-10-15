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
    /// The mod has exactly two Harmony patches, both made in the button's
    /// file: the Showcases button's postfix on
    /// OptionListingUtility.DrawOptionListing (approved 2026-10-13) and
    /// the showcase map seed's prefix on the Map.NextGenSeed getter
    /// (approved 2026-10-14, requests item 22). Any C# use of Harmony
    /// needs the HarmonyLib namespace (a using, an alias, a fully
    /// qualified name) or a Harmony attribute, so no other file may
    /// mention either; and the button's file makes one Harmony instance,
    /// under the mod's package id, and two Patch calls, one postfix and
    /// one prefix on those targets, with no transpiler, finalizer, bulk
    /// or reverse patching.
    /// </summary>
    [Fact(DisplayName = "The Showcases button and the map seed are the mod's only Harmony patches")]
    public void Harmony_should_patch_only_the_menu_button_and_the_map_seed()
    {
        string source = Path.Combine(FixturesDirectory, "Source");
        string buttonFile = Path.Combine(source, "Showcases", "ShowcaseMenuButton.cs");
        List<string> others = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(f => !string.Equals(
                Path.GetFullPath(f), Path.GetFullPath(buttonFile),
                StringComparison.OrdinalIgnoreCase))
            .Where(f => Regex.IsMatch(
                WithoutComments(File.ReadAllText(f)), @"\bHarmonyLib\b|\[\s*Harmony"))
            .Select(Path.GetFileName)
            .Select(n => n!)
            .ToList();
        string button = WithoutComments(File.ReadAllText(buttonFile));

        Assert.Empty(others);
        Assert.Contains(
            "HarmonyId = \"breakpoint8088.BatshitTechnology.",
            button,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(button, @"\.Patch\s*\(").Count);
        Assert.Single(Regex.Matches(
            button, @"new\s*(Harmony(Lib\.Harmony)?)?\s*\(\s*HarmonyId\s*\)"));
        Assert.Single(Regex.Matches(
            button,
            @"postfix\s*:\s*new\s+HarmonyMethod\s*\(\s*typeof\s*\(\s*ShowcaseMenuButton\s*\)"
                + @"\s*,\s*nameof\s*\(\s*Postfix\s*\)\s*\)"));
        Assert.Single(Regex.Matches(
            button,
            @"prefix\s*:\s*new\s+HarmonyMethod\s*\(\s*typeof\s*\(\s*ShowcaseMapSeed\s*\)"
                + @"\s*,\s*nameof\s*\(\s*ShowcaseMapSeed\.Prefix\s*\)\s*\)"));
        Assert.Single(Regex.Matches(
            button, @"nameof\s*\(\s*OptionListingUtility\.DrawOptionListing\s*\)\s*,"));
        Assert.Single(Regex.Matches(
            button,
            @"PropertyGetter\s*\(\s*typeof\s*\(\s*Map\s*\)\s*,"
                + @"\s*nameof\s*\(\s*Map\.NextGenSeed\s*\)"));
        Assert.DoesNotMatch(
            @"PatchAll|PatchCategory|CreateClassProcessor|CreateProcessor|ReversePatch"
                + @"|transpiler\s*:|finalizer\s*:|\[\s*Harmony",
            button);
    }

    /// <summary>
    /// The map seed's prefix decides before it does anything: its first
    /// statement asks the window (GenerationPin.Take, bound to the game
    /// being generated) and returns true, running vanilla's getter, when
    /// the window does not pin this game. The window's rules themselves
    /// are unit tested (GenerationPinTests).
    /// </summary>
    [Fact(DisplayName = "The map seed's prefix passes through unless its window pins the game")]
    public void Map_seed_prefix_should_pass_through_outside_its_window()
    {
        string file = Path.Combine(
            FixturesDirectory, "Source", "Showcases", "ShowcaseMapSeed.cs");
        string code = WithoutComments(File.ReadAllText(file));
        Match prefix = Regex.Match(
            code,
            @"public\s+static\s+bool\s+Prefix\s*\([^)]*\)\s*\{\s*(?<first>if\s*\((?<cond>[^{]*)\)"
                + @"\s*\{\s*return\s+true\s*;\s*\})");

        Assert.True(prefix.Success, "Prefix must open with its pass-through test.");
        Assert.Matches(@"!\s*Pin\.Take\s*\(\s*Current\.Game\s*\)", prefix.Groups["cond"].Value);
        Assert.Single(Regex.Matches(code, @"return\s+false\s*;"));
        Assert.Matches(
            @"static\s+readonly\s+GenerationPin\s+Pin\s*=\s*new\s+GenerationPin\s*\(\s*\)",
            code);
    }

    /// <summary>
    /// God mode is a static flag that outlives a showcase game, so only the
    /// Showcase API's loan sets it (Source/Showcases/ShowcaseGodMode.cs),
    /// besides the smoke runner, which sets up and restores its own cases;
    /// and every DEV button the mod draws needs dev mode as well, through
    /// DebugSettings.ShowDevGizmos, never god mode alone.
    /// </summary>
    [Fact(DisplayName = "Only the showcase's loan sets god mode; DEV buttons need dev mode")]
    public void God_mode_should_be_set_only_by_the_loan()
    {
        string source = Path.Combine(FixturesDirectory, "Source");
        var files = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Select(f => (Name: Path.GetFileName(f), Code: WithoutComments(File.ReadAllText(f))))
            .ToList();

        List<string> setters = files
            .Where(f => Regex.IsMatch(f.Code, @"DebugSettings\.godMode\s*=(?!=)"))
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        List<string> devButtons = files
            .Where(f => f.Code.Contains("\"DEV:", StringComparison.Ordinal))
            .Select(f => f.Name)
            .ToList();

        Assert.Equal(new[] { "ShowcaseGodMode.cs", "SmokeTestShowcases.cs" }, setters);
        Assert.NotEmpty(devButtons);
        Assert.All(files.Where(f => devButtons.Contains(f.Name)), f =>
        {
            Assert.Contains("DebugSettings.ShowDevGizmos", f.Code, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"if\s*\(\s*DebugSettings\.godMode\b", f.Code);
        });
    }

    /// <summary>
    /// The worker-class check matches a namespace exactly, and ignores
    /// comments: a class in BatshitTechnology.Showcases is not a class in
    /// BatshitTechnology, so a def naming the wrong namespace fails here
    /// rather than in game.
    /// </summary>
    [Fact(DisplayName = "The worker-class check matches the namespace exactly")]
    public void ClassDeclared_should_match_the_namespace_exactly()
    {
        Assert.True(ClassDeclared("BatshitTechnology.Showcases.ShowcaseDirector"));
        Assert.True(ClassDeclared("BatshitTechnology.BatshitTourDirector"));
        Assert.False(ClassDeclared("BatshitTechnology.ShowcaseDirector"));
        Assert.False(ClassDeclared("BatshitTechnology.Showcases.BatshitTourDirector"));
        Assert.False(ClassDeclared("BatshitTechnology.BatshitShowcaseWorker"));
        Assert.False(ClassDeclared("BatshitTechnology.NoSuchWorker"));
    }

    /// <summary>The XML element naming a ShowcaseDef.</summary>
    private const string ShowcaseDefTag = "BatshitTechnology.Showcases.ShowcaseDef";

    /// <summary>
    /// Every field a ShowcaseDef declares in XML may use
    /// (Source/Showcases/ShowcaseDef.cs and its Def base).
    /// </summary>
    private static readonly HashSet<string> ShowcaseDefFields = new(StringComparer.Ordinal)
    {
        "defName", "label", "description", "summary", "previewTexPath",
        "estimatedMinutes", "order", "requiredMods", "compatibility",
        "mapSize", "biome", "seed", "holdWeather", "quietScreen", "workerClass",
        "scenes", "narration",
    };

    /// <summary>
    /// The def's <c>seed</c> field exists in the source as a string, and a
    /// ShowcaseDef that pins a seed pins a usable one (1 to 64 ASCII
    /// letters, digits, '-', '_' or '.'), which the game would otherwise
    /// refuse with an error at launch.
    /// </summary>
    [Fact(DisplayName = "A ShowcaseDef's seed, if any, is well formed")]
    public void ShowcaseDefs_should_pin_only_well_formed_seeds()
    {
        string source = File.ReadAllText(Path.Combine(
            FixturesDirectory, "Source", "Showcases", "ShowcaseDef.cs"));
        Assert.Matches(new Regex(@"\[NoTranslate\]\s+public string seed;"), source);

        foreach (XElement def in ShowcaseDefs())
        {
            XElement? seed = def.Element("seed");
            if (seed == null || string.IsNullOrWhiteSpace(seed.Value))
            {
                continue;
            }

            string name = def.Element("defName")?.Value ?? "(no defName)";
            Assert.True(ShowcaseSeed.IsWellFormed(seed.Value.Trim()),
                $"{name}: seed '{seed.Value}': {ShowcaseSeed.WhyMalformed(seed.Value.Trim())}");
        }
    }

    /// <summary>
    /// The def's <c>holdWeather</c> field exists in the source as a
    /// WeatherDef, and the directed tour holds the weather clear
    /// (slice 19000a: rain and fog greyed out its middle scenes).
    /// </summary>
    [Fact(DisplayName = "BT_Showcase holds the weather clear")]
    public void BT_Showcase_should_hold_the_weather_clear()
    {
        string source = File.ReadAllText(Path.Combine(
            FixturesDirectory, "Source", "Showcases", "ShowcaseDef.cs"));
        Assert.Matches(new Regex(@"public WeatherDef holdWeather;"), source);
        XElement def = Assert.Single(
            ShowcaseDefs(), d => d.Element("defName")?.Value == "BT_Showcase");

        Assert.Equal("Clear", def.Element("holdWeather")?.Value);
    }

    /// <summary>
    /// The def's <c>quietScreen</c> field exists in the source as a bool,
    /// and the directed tour keeps the screen quiet (slice 19000b:
    /// alerts, letters and messages piled up over the film).
    /// </summary>
    [Fact(DisplayName = "BT_Showcase keeps the screen quiet")]
    public void BT_Showcase_should_keep_the_screen_quiet()
    {
        string source = File.ReadAllText(Path.Combine(
            FixturesDirectory, "Source", "Showcases", "ShowcaseDef.cs"));
        Assert.Matches(new Regex(@"public bool quietScreen;"), source);
        XElement def = Assert.Single(
            ShowcaseDefs(), d => d.Element("defName")?.Value == "BT_Showcase");

        Assert.Equal("true", def.Element("quietScreen")?.Value);
    }

    /// <summary>
    /// The directed tour asks for no DEV buttons: its director never
    /// lends god mode (slice 19000b: viewing runs show no developer
    /// clutter).
    /// </summary>
    [Fact(DisplayName = "The tour asks for no DEV buttons")]
    public void Tour_director_should_not_ask_for_dev_buttons()
    {
        string source = File.ReadAllText(Path.Combine(
            FixturesDirectory, "Source", "Tour", "BatshitTourDirector.cs"));
        Assert.DoesNotMatch(new Regex(@"EnableDevButtons\s*\("), source);
    }

    /// <summary>
    /// Batshit Technology's first showcase is the directed tour, with the
    /// agreed preview, size and running time; its scenes and narration
    /// are checked by <see cref="TourXmlTests"/>.
    /// </summary>
    [Fact(DisplayName = "BT_Showcase is the directed tour")]
    public void BT_Showcase_should_be_the_directed_tour()
    {
        XElement def = Assert.Single(
            ShowcaseDefs(), d => d.Element("defName")?.Value == "BT_Showcase");

        Assert.Equal("living better with batshittium", def.Element("label")?.Value);
        Assert.Equal(
            "BatshitTechnology.BatshitTourDirector",
            def.Element("workerClass")?.Value);
        Assert.Equal("UI/Showcases/BT_Showcase", def.Element("previewTexPath")?.Value);
        Assert.Equal("250", def.Element("mapSize")?.Value);
        Assert.Equal("8", def.Element("estimatedMinutes")?.Value);
        Assert.NotNull(def.Element("scenes"));
        Assert.Null(def.Element("requiredMods"));
    }

    /// <summary>
    /// Every ShowcaseDef uses only real fields, carries the mod prefix, a
    /// lowercase label, one-line text, a running time and map size inside
    /// the API's limits, and a worker class that exists in the source.
    /// </summary>
    [Fact(DisplayName = "Every ShowcaseDef's fields are well formed")]
    public void ShowcaseDefs_should_have_well_formed_fields()
    {
        List<XElement> defs = ShowcaseDefs().ToList();
        Assert.NotEmpty(defs);
        foreach (XElement def in defs)
        {
            string name = def.Element("defName")?.Value ?? "(no defName)";
            Assert.StartsWith("BT_", name, StringComparison.Ordinal);
            Assert.Empty(def.Elements()
                .Select(e => e.Name.LocalName)
                .Where(f => !ShowcaseDefFields.Contains(f)));

            string label = def.Element("label")?.Value ?? string.Empty;
            Assert.False(string.IsNullOrWhiteSpace(label), $"{name} has no label");
            Assert.Equal(label.ToLowerInvariant(), label);
            foreach (string field in new[] { "label", "summary", "description" })
            {
                string text = def.Element(field)?.Value ?? string.Empty;
                Assert.False(string.IsNullOrWhiteSpace(text), $"{name} has no {field}");
                Assert.DoesNotContain("\n", text, StringComparison.Ordinal);
            }

            float minutes = float.Parse(
                def.Element("estimatedMinutes")?.Value ?? "5",
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(minutes, 1f, CaptionText.MaxMinutes);
            int mapSize = int.Parse(def.Element("mapSize")?.Value ?? "250");
            Assert.InRange(mapSize, 75, 400);

            string worker = def.Element("workerClass")?.Value ?? string.Empty;
            Assert.Contains(".", worker, StringComparison.Ordinal);
            Assert.True(ClassDeclared(worker), $"{name}: {worker} is not in Source/");
        }
    }

    /// <summary>
    /// Every ShowcaseDef's preview exists, carries the mod prefix, and is a
    /// 512 by 288 (16:9) PNG, the size the menus are laid out for
    /// (docs/ARTISTIC-MOTIF.md, "Showcase Previews").
    /// </summary>
    [Fact(DisplayName = "Every ShowcaseDef has a 512x288 preview")]
    public void ShowcaseDefs_should_have_16_by_9_previews()
    {
        foreach (XElement def in ShowcaseDefs())
        {
            string path = def.Element("previewTexPath")?.Value
                ?? throw new InvalidOperationException("no previewTexPath");
            string file = Path.Combine(FixturesDirectory, "Textures", path + ".png");

            Assert.StartsWith("UI/Showcases/BT_", path, StringComparison.Ordinal);
            Assert.True(File.Exists(file), $"{path}.png is missing");
            Assert.Equal((512, 288), PngSize(file));
        }
    }

    /// <summary>
    /// The old -btshowcase flag is gone: the showcase starts only through
    /// the API (-showcase=BT_Showcase), so there is one way in.
    /// </summary>
    [Fact(DisplayName = "The -btshowcase flag is retired")]
    public void Btshowcase_flag_should_be_retired()
    {
        string source = Path.Combine(FixturesDirectory, "Source");
        List<string> users = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains(
                "\"btshowcase\"", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Select(n => n!)
            .ToList();

        Assert.Empty(users);
    }

    /// <summary>
    /// The smoke runner's test showcases are made in code
    /// (Source/SmokeTestShowcases.cs) and never shipped: no def file names
    /// one, so players never see them in the Showcases menu.
    /// </summary>
    [Fact(DisplayName = "Test showcases are made in code, never shipped")]
    public void Test_showcases_should_not_be_shipped()
    {
        const string TestPrefix = "BT_SmokeShowcase";
        List<string> shipping = Directory
            .EnumerateFiles(
                Path.Combine(FixturesDirectory, "Defs"), "*.xml",
                SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains(TestPrefix, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Select(n => n!)
            .ToList();
        string runner = File.ReadAllText(
            Path.Combine(FixturesDirectory, "Source", "SmokeTestShowcases.cs"));

        Assert.Empty(shipping);
        Assert.Contains("\"" + TestPrefix + "_Director\"", runner, StringComparison.Ordinal);
    }

    /// <summary>Every ShowcaseDef in the mod's Defs.</summary>
    /// <returns>The def elements.</returns>
    internal static IEnumerable<XElement> ShowcaseDefs() =>
        Directory.EnumerateFiles(
                Path.Combine(FixturesDirectory, "Defs"), "*.xml",
                SearchOption.AllDirectories)
            .Select(XDocument.Load)
            .SelectMany(d => d.Root?.Elements(ShowcaseDefTag)
                ?? Enumerable.Empty<XElement>());

    /// <summary>
    /// Whether the game code declares a class by its full name: a file
    /// whose namespace is exactly the class's (block or file-scoped, so
    /// "BatshitTechnology" never matches "BatshitTechnology.Showcases")
    /// declares a class of that short name. Comments are ignored.
    /// </summary>
    /// <param name="fullName">The namespace-qualified class name.</param>
    /// <returns>True if it is declared.</returns>
    internal static bool ClassDeclared(string fullName)
    {
        int dot = fullName.LastIndexOf('.');
        string ns = fullName.Substring(0, dot);
        string shortName = fullName.Substring(dot + 1);
        return Directory
            .EnumerateFiles(Path.Combine(FixturesDirectory, "Source"), "*.cs",
                SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Select(WithoutComments)
            .Any(code => Regex.IsMatch(
                    code, @"\bnamespace\s+" + Regex.Escape(ns) + @"\s*[;{]")
                && Regex.IsMatch(
                    code, @"\bclass\s+" + Regex.Escape(shortName) + @"\b"));
    }

    /// <summary>
    /// C# source without its comments (line, block and XML documentation
    /// comments), so a name mentioned only in prose never counts as code.
    /// String literals are left alone; a comment marker inside one is
    /// rare enough in this code base not to matter.
    /// </summary>
    /// <param name="code">The source text.</param>
    /// <returns>The text with comments removed.</returns>
    private static string WithoutComments(string code) =>
        Regex.Replace(code, @"//[^\r\n]*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    /// <summary>A PNG's width and height, from its IHDR chunk.</summary>
    /// <param name="file">The PNG.</param>
    /// <returns>The size.</returns>
    private static (int Width, int Height) PngSize(string file)
    {
        byte[] head = new byte[24];
        using (FileStream stream = File.OpenRead(file))
        {
            Assert.Equal(head.Length, stream.Read(head, 0, head.Length));
        }

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, head.Take(4).ToArray());
        return (BigEndian(head, 16), BigEndian(head, 20));
    }

    /// <summary>Reads a big-endian 32-bit integer, as PNG stores sizes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="at">Where the integer starts.</param>
    /// <returns>The integer.</returns>
    private static int BigEndian(byte[] bytes, int at) =>
        (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];

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
