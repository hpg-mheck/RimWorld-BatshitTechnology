using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the town palettes and the def catalogue
/// (<see cref="TownPalette"/>, <see cref="TownDefs"/>) against
/// docs/COLONY-BOMB.md and, where the game is installed, against
/// RimWorld's own Core Defs with ParentName inheritance resolved. The mod's
/// own defs are always checked against its Defs folder.
/// </summary>
public sealed class TownDefsTests
{
    private const string DefaultRimWorld =
        @"C:\Program Files (x86)\Steam\steamapps\common\RimWorld";

    private static readonly string[] Qualities =
    {
        "Awful", "Poor", "Normal", "Good", "Excellent", "Masterwork", "Legendary",
    };

    private static readonly Lazy<DefLibrary?> Vanilla = new(LoadVanilla);

    private static readonly Lazy<DefLibrary> Mod = new(LoadMod);

    // ===== Palettes =====

    /// <summary>Medium is AnyRim exactly as docs/SHOWCASE.md lays it out.</summary>
    [Fact(DisplayName = "The Medium palette is AnyRim's")]
    public void Medium_palette_should_be_anyrim()
    {
        Assert.Equal(
            new TownPalette(TownQuality.Medium, "Concrete", "Structure_GreyDark",
                "Structure_YellowPastel", "PavedTile", "BrokenAsphalt", "Gravel",
                "PackedDirt", "WoodLog", "Normal"),
            TownPalette.For(TownQuality.Medium));
    }

    /// <summary>
    /// Each tier's roads and furniture follow the quality table in
    /// docs/COLONY-BOMB.md: packed dirt, gravel, concrete, lined concrete,
    /// paved tile and gold tile, at vanilla's quality for the tier.
    /// </summary>
    /// <param name="quality">The tier.</param>
    /// <param name="road">Its carriageway.</param>
    /// <param name="furniture">Its furniture quality.</param>
    [Theory(DisplayName = "Each tier follows the quality table")]
    [InlineData(TownQuality.Slum, "PackedDirt", "Awful")]
    [InlineData(TownQuality.Low, "Gravel", "Poor")]
    [InlineData(TownQuality.Medium, "Concrete", "Normal")]
    [InlineData(TownQuality.High, "Concrete", "Good")]
    [InlineData(TownQuality.Resort, "PavedTile", "Excellent")]
    [InlineData(TownQuality.Epic, "GoldTile", "Masterwork")]
    public void Palettes_should_follow_the_quality_table(
        TownQuality quality, string road, string furniture)
    {
        TownPalette palette = TownPalette.For(quality);
        Assert.Equal(quality, palette.Quality);
        Assert.Equal(road, palette.Carriageway);
        Assert.Equal(furniture, palette.FurnitureQuality);
        Assert.Contains(palette.FurnitureQuality, Qualities);
        Assert.True(quality < TownQuality.High || palette.CentreLinePaint != null, "lined");
        DefInfo fence = TownDefs.Get("Fence");
        Assert.True(TownDefs.Get(palette.FenceStuff).StuffCategories.Intersect(fence.AcceptsStuff).Any());
    }

    /// <summary>Only the six tiers have palettes.</summary>
    [Fact(DisplayName = "Unknown tiers have no palette")]
    public void For_should_reject_unknown_tiers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TownPalette.For((TownQuality)6));
        Assert.Equal(6, Enum.GetValues(typeof(TownQuality)).Length);
    }

    // ===== Catalogue =====

    /// <summary>
    /// The catalogue is internally consistent: positive sizes, items that
    /// stack and hold nothing, interaction cells only on buildings, and
    /// lookups that fail loudly.
    /// </summary>
    [Fact(DisplayName = "The catalogue is consistent")]
    public void Catalogue_should_be_consistent()
    {
        foreach ((string name, DefInfo d) in TownDefs.All)
        {
            Assert.True(d.SizeX >= 1 && d.SizeZ >= 1, name);
            Assert.True(d.StackLimit >= 1, name);
            Assert.True(d.Layer == ThingLayer.Item || d.StackLimit == 1, name);
            Assert.True(d.Layer == ThingLayer.Building || d.ItemCapacity == 0, name);
            Assert.True(d.Layer == ThingLayer.Building || d.InteractionCell == null, name);
            Assert.True(!d.IsWall || !d.Passable, name);
            Assert.True(!d.IsDoor || d.Passable, name);
        }

        Assert.Equal(3, TownDefs.Get("Shelf").ItemCapacity);
        Assert.Throws<KeyNotFoundException>(() => TownDefs.Get("Plant_Triffid"));
    }

    /// <summary>
    /// Every catalogued vanilla ThingDef exists in RimWorld's Core Defs
    /// with the catalogued size, stack limit, passability, interaction
    /// cell, rotation, power, stuff, quality, switches, fuel, bills,
    /// minification, item capacity and layer.
    /// </summary>
    [Fact(DisplayName = "Vanilla defs match RimWorld's data")]
    public void Vanilla_defs_should_match_rimworld()
    {
        DefLibrary? vanilla = Vanilla.Value;
        if (vanilla == null)
        {
            // RimWorld's data is not installed here (RIMWORLD_DIR unset and
            // no Steam copy at the default path), so only the mod's own
            // defs can be checked; see Mod_defs_should_match_the_mod.
            return;
        }

        int checkedDefs = 0;
        var vanillaDefs = TownDefs.All.Where(e => !e.Key.StartsWith("BT_")).ToList();
        foreach ((string name, DefInfo info) in vanillaDefs)
        {
            checkedDefs++;
            if (info.Layer == ThingLayer.Pawn)
            {
                Assert.True(vanilla.Exists("PawnKindDef", name), $"PawnKindDef {name}");
                Assert.True(vanilla.Exists("ThingDef", name), $"race {name}");
                continue;
            }

            XElement def = vanilla.Resolve("ThingDef", name)
                ?? throw new Xunit.Sdk.XunitException($"ThingDef {name} is not in Core");
            AssertMatches(name, info, def);
        }

        Assert.True(checkedDefs > 60, $"only {checkedDefs} vanilla defs checked");
    }

    /// <summary>
    /// Every catalogued BT_ def exists in the mod's Defs; where RimWorld is
    /// installed (so vanilla parents resolve) it matches in every fact,
    /// and the Ballista and magazine hold what the catalogue says.
    /// </summary>
    [Fact(DisplayName = "Mod defs match the mod's data")]
    public void Mod_defs_should_match_the_mod()
    {
        DefLibrary mod = Mod.Value;
        DefLibrary? both = Vanilla.Value?.With(mod);
        foreach ((string name, DefInfo info) in TownDefs.All.Where(e => e.Key.StartsWith("BT_")))
        {
            Assert.True(mod.Exists("ThingDef", name), $"BT def {name} is not in Defs/");
            XElement raw = mod.Raw("ThingDef", name)!;
            Assert.Equal((info.SizeX, info.SizeZ), Size(raw.Element("size")?.Value ?? "(1,1)"));
            if (raw.Element("stackLimit") is XElement stack)
            {
                Assert.Equal(info.StackLimit, int.Parse(stack.Value, CultureInfo.InvariantCulture));
            }

            if (info.LoadCapacity > 0)
            {
                Assert.Equal(info.LoadCapacity, int.Parse(raw.Descendants("capacity").First().Value,
                    CultureInfo.InvariantCulture));
            }

            if (both != null)
            {
                AssertMatches(name, info, both.Resolve("ThingDef", name)!);
            }
        }
    }

    /// <summary>
    /// Every terrain, colour, pawn kind and apparel name the plan uses,
    /// at every tier, exists in RimWorld's Core Defs; carpets exist through
    /// the Carpet template and a structure colour.
    /// </summary>
    [Fact(DisplayName = "Every terrain, colour, kind and apparel the plan uses exists")]
    public void Plan_names_should_exist_in_rimworld()
    {
        DefLibrary? vanilla = Vanilla.Value;
        if (vanilla == null)
        {
            // RimWorld's data is not installed here; nothing to check against.
            return;
        }

        foreach (TownQuality quality in Enum.GetValues(typeof(TownQuality)).Cast<TownQuality>())
        {
            TownPlan plan = TownPlan.AnyRim(1, TownPalette.For(quality));
            foreach (string terrain in plan.Terrain.Select(t => t.Def).Distinct())
            {
                Assert.True(TerrainExists(vanilla, terrain), $"TerrainDef {terrain} ({quality})");
            }

            IEnumerable<string?> paints = plan.Terrain.Select(t => t.Paint)
                .Concat(plan.Placements.Select(p => p.Paint));
            foreach (string paint in paints.OfType<string>().Distinct())
            {
                Assert.True(vanilla.Exists("ColorDef", paint), $"ColorDef {paint}");
            }

            foreach (Placement animal in plan.Placements.Where(p => p.Kind == PlacementKind.Animal))
            {
                Assert.True(vanilla.Exists("PawnKindDef", animal.Def), animal.Def);
            }

            foreach (string apparel in plan.Residents.SelectMany(r => r.Apparel).Distinct())
            {
                XElement def = vanilla.Resolve("ThingDef", apparel)
                    ?? throw new Xunit.Sdk.XunitException($"apparel {apparel}");
                Assert.Equal("Apparel", def.Element("thingClass")?.Value);
            }

            foreach (string stuff in plan.Placements.Select(p => p.Stuff).OfType<string>().Distinct())
            {
                Assert.True(vanilla.Resolve("ThingDef", stuff)?.Element("stuffProps") != null,
                    $"stuff {stuff}");
            }
        }

        Assert.Equal("Structure",
            vanilla.Resolve("ColorDef", "Structure_Mustard")?.Element("colorType")?.Value);
    }

    // ===== Assertions =====

    /// <summary>Checks one catalogue entry against its resolved XML.</summary>
    /// <param name="name">The defName.</param>
    /// <param name="info">The catalogue entry.</param>
    /// <param name="def">The resolved ThingDef.</param>
    private static void AssertMatches(string name, DefInfo info, XElement def)
    {
        string category = Text(def, "category") ?? "None";
        Assert.Equal((name, info.SizeX, info.SizeZ), (name, Size(Text(def, "size") ?? "(1,1)").X,
            Size(Text(def, "size") ?? "(1,1)").Z));
        Assert.Equal((name, info.StackLimit), (name, Int(def, "stackLimit", 1)));
        Assert.Equal((name, info.Passable), (name, Text(def, "passability") != "Impassable"));

        List<XElement> comps = def.Element("comps")?.Elements("li").ToList() ?? new List<XElement>();

        ThingLayer layer = category switch
        {
            "Item" => ThingLayer.Item,
            "Plant" => ThingLayer.Plant,
            _ when Text(def, "building/isEdifice") == "false" => ThingLayer.Conduit,
            _ => ThingLayer.Building,
        };
        Assert.Equal((name, info.Layer), (name, layer));
        if (category != "Building")
        {
            string stuffXml = string.Join(",", def.Elements("stuffProps")
                .Elements("categories").Elements("li")
                .Select(e => e.Value.Trim()).OrderBy(s => s, StringComparer.Ordinal));
            string stuffCatalogue = string.Join(",",
                info.StuffCategories.OrderBy(s => s, StringComparer.Ordinal));
            Assert.Equal((name, stuffCatalogue), (name, stuffXml));
            return;
        }

        int capacity = def.Element("building")?.Element("maxItemsInCell") is XElement max
            ? int.Parse(max.Value, CultureInfo.InvariantCulture)
            : Text(def, "surfaceType") is string surface && surface != "None" ? 1 : 0;
        Cell? interaction = Bool(def, "hasInteractionCell", false)
            ? Offset(Text(def, "interactionCellOffset") ?? "(0,0,0)")
            : null;
        string thingClass = Text(def, "thingClass") ?? "";
        var facts = new
        {
            ItemCapacity = capacity,
            InteractionCell = interaction,
            Powered = HasComp(comps, "CompPowerTrader"),
            TransmitsPower = Bool(def, "building/isPowerConduit", false)
                || comps.Any(c => c.Element("transmitsPower")?.Value.Trim().ToLowerInvariant() == "true"),
            IsDoor = thingClass is "Building_Door" or "Building_MultiTileDoor",
            IsWall = Bool(def, "holdsRoof", false) && Text(def, "passability") == "Impassable",
            IsFence = Bool(def, "building/isFence", false),
            Rotatable = Bool(def, "rotatable", true),
            AcceptsStuff = string.Join(",", def.Elements("stuffCategories").Elements("li")
                .Select(e => e.Value.Trim()).OrderBy(s => s)),
            HasQuality = HasComp(comps, "CompQuality"),
            Flickable = HasComp(comps, "CompProperties_Flickable"),
            Refuelable = HasComp(comps, "CompProperties_Refuelable"),
            HasBills = thingClass.StartsWith("Building_WorkTable", StringComparison.Ordinal),
            Minifiable = def.Element("minifiedDef") != null,
        };
        var expected = new
        {
            info.ItemCapacity,
            info.InteractionCell,
            info.Powered,
            info.TransmitsPower,
            info.IsDoor,
            info.IsWall,
            info.IsFence,
            info.Rotatable,
            AcceptsStuff = string.Join(",", info.AcceptsStuff.OrderBy(s => s)),
            info.HasQuality,
            info.Flickable,
            info.Refuelable,
            info.HasBills,
            info.Minifiable,
        };
        Assert.True(expected.Equals(facts), $"{name}:\n catalogue {expected}\n XML       {facts}");
    }

    /// <summary>Whether a def's comps include one, by Class or compClass.</summary>
    /// <param name="comps">The def's comps.</param>
    /// <param name="compClass">The comp properties or comp class.</param>
    /// <returns>True if present.</returns>
    private static bool HasComp(List<XElement> comps, string compClass) =>
        comps.Any(c => c.Attribute("Class")?.Value == compClass
            || c.Element("compClass")?.Value == compClass);

    /// <summary>Whether a terrain exists, directly or as a generated carpet.</summary>
    /// <param name="vanilla">RimWorld's defs.</param>
    /// <param name="terrain">The TerrainDef name.</param>
    /// <returns>True if RimWorld will have it.</returns>
    private static bool TerrainExists(DefLibrary vanilla, string terrain)
    {
        if (vanilla.Exists("TerrainDef", terrain))
        {
            return true;
        }

        // TerrainDefGenerator_Carpet: template defName + colour suffix, for
        // every ColorDef of colorType Structure.
        const string template = "Carpet";
        return terrain.StartsWith(template, StringComparison.Ordinal)
            && vanilla.Exists("TerrainTemplateDef", template)
            && vanilla.Resolve("ColorDef", "Structure_" + terrain.Substring(template.Length))?
                .Element("colorType")?.Value == "Structure";
    }

    // ===== XML helpers =====

    /// <summary>Parses "(x,z)" or "(x, z)".</summary>
    /// <param name="text">The size text.</param>
    /// <returns>The size.</returns>
    private static (int X, int Z) Size(string text)
    {
        int[] v = Numbers(text);
        return (v[0], v[1]);
    }

    /// <summary>Parses "(x,y,z)" into a cell, dropping y.</summary>
    /// <param name="text">The offset text.</param>
    /// <returns>The offset.</returns>
    private static Cell Offset(string text)
    {
        int[] v = Numbers(text);
        return new Cell(v[0], v[2]);
    }

    /// <summary>The integers in a parenthesised vector.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Its components.</returns>
    private static int[] Numbers(string text) =>
        text.Trim().Trim('(', ')').Split(',')
            .Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();

    /// <summary>The trimmed text at a path, or null.</summary>
    /// <param name="def">The def.</param>
    /// <param name="path">Slash-separated element names.</param>
    /// <returns>The text.</returns>
    private static string? Text(XElement def, string path)
    {
        XElement? e = def;
        foreach (string part in path.Split('/'))
        {
            e = e?.Element(part);
        }

        return e?.Value.Trim();
    }

    /// <summary>An integer field with a default.</summary>
    /// <param name="def">The def.</param>
    /// <param name="path">The field.</param>
    /// <param name="fallback">Its default.</param>
    /// <returns>The value.</returns>
    private static int Int(XElement def, string path, int fallback) =>
        Text(def, path) is string s ? int.Parse(s, CultureInfo.InvariantCulture) : fallback;

    /// <summary>A boolean field with a default (RimWorld accepts any case).</summary>
    /// <param name="def">The def.</param>
    /// <param name="path">The field.</param>
    /// <param name="fallback">Its default.</param>
    /// <returns>The value.</returns>
    private static bool Bool(XElement def, string path, bool fallback) =>
        Text(def, path) is string s ? s.Equals("true", StringComparison.OrdinalIgnoreCase) : fallback;

    /// <summary>
    /// RimWorld's Core Defs, from RIMWORLD_DIR or the default Steam path,
    /// or null if neither holds them.
    /// </summary>
    /// <returns>The library, or null.</returns>
    private static DefLibrary? LoadVanilla()
    {
        foreach (string? root in new[] { Environment.GetEnvironmentVariable("RIMWORLD_DIR"), DefaultRimWorld })
        {
            if (!string.IsNullOrWhiteSpace(root))
            {
                string defs = Path.Combine(root, "Data", "Core", "Defs");
                if (Directory.Exists(defs))
                {
                    var library = new DefLibrary();
                    library.Load(defs);
                    return library;
                }
            }
        }

        return null;
    }

    /// <summary>The mod's Defs, as copied beside the tests.</summary>
    /// <returns>The library.</returns>
    private static DefLibrary LoadMod()
    {
        string defs = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Defs");
        Assert.True(Directory.Exists(defs), "Defs fixtures were not copied.");
        var library = new DefLibrary();
        library.Load(defs);
        return library;
    }

    /// <summary>
    /// Defs loaded from XML, with RimWorld's inheritance: a def's
    /// ParentName names another node's Name; a child's fields replace the
    /// parent's, nested fields merge, and lists (li) append unless marked
    /// Inherit="False" (Verse.XmlInheritance).
    /// </summary>
    private sealed class DefLibrary
    {
        private readonly Dictionary<string, XElement> named = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Type, string Name), XElement> defs = new();

        /// <summary>Loads every XML file under a folder; later files win.</summary>
        /// <param name="root">The folder.</param>
        public void Load(string root)
        {
            foreach (string path in Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories))
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Load(path);
                }
                catch (XmlException)
                {
                    continue; // RimWorld skips malformed files too, with an error.
                }

                foreach (XElement node in doc.Root?.Elements() ?? Enumerable.Empty<XElement>())
                {
                    if (node.Attribute("Name")?.Value is string name)
                    {
                        named[name] = node;
                    }

                    if (node.Element("defName")?.Value.Trim() is string defName)
                    {
                        defs[(node.Name.LocalName, defName)] = node;
                    }
                }
            }
        }

        /// <summary>A new library holding this one's defs and then another's.</summary>
        /// <param name="other">The library loaded after this one.</param>
        /// <returns>The combined library.</returns>
        public DefLibrary With(DefLibrary other)
        {
            var both = new DefLibrary();
            foreach (DefLibrary source in new[] { this, other })
            {
                foreach (KeyValuePair<string, XElement> e in source.named)
                {
                    both.named[e.Key] = e.Value;
                }

                foreach (KeyValuePair<(string, string), XElement> e in source.defs)
                {
                    both.defs[e.Key] = e.Value;
                }
            }

            return both;
        }

        /// <summary>Whether a def exists.</summary>
        /// <param name="type">The def type (XML element name).</param>
        /// <param name="defName">The defName.</param>
        /// <returns>True if loaded.</returns>
        public bool Exists(string type, string defName) => defs.ContainsKey((type, defName));

        /// <summary>A def as written, without inheritance.</summary>
        /// <param name="type">The def type.</param>
        /// <param name="defName">The defName.</param>
        /// <returns>The node, or null.</returns>
        public XElement? Raw(string type, string defName) =>
            defs.TryGetValue((type, defName), out XElement? node) ? node : null;

        /// <summary>A def with its ancestors merged in.</summary>
        /// <param name="type">The def type.</param>
        /// <param name="defName">The defName.</param>
        /// <returns>The resolved node, or null if the def is missing.</returns>
        public XElement? Resolve(string type, string defName) =>
            Raw(type, defName) is XElement node ? Resolve(node, 0) : null;

        /// <summary>Merges a node's ancestors into it.</summary>
        /// <param name="node">The node.</param>
        /// <param name="depth">Recursion depth, against cycles.</param>
        /// <returns>The resolved node.</returns>
        private XElement Resolve(XElement node, int depth)
        {
            string? parent = node.Attribute("ParentName")?.Value;
            if (parent == null || depth > 40 || !named.TryGetValue(parent, out XElement? parentNode))
            {
                return node;
            }

            return Merge(Resolve(parentNode, depth + 1), node);
        }

        /// <summary>Overlays a child node on its parent.</summary>
        /// <param name="parent">The resolved parent.</param>
        /// <param name="child">The child.</param>
        /// <returns>A new merged node.</returns>
        private static XElement Merge(XElement parent, XElement child)
        {
            var result = new XElement(parent);
            foreach (XAttribute a in child.Attributes())
            {
                result.SetAttributeValue(a.Name, a.Value);
            }

            if (!child.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(child.Value))
                {
                    result.Value = child.Value;
                }

                return result;
            }

            foreach (XElement c in child.Elements())
            {
                if (c.Name.LocalName == "li")
                {
                    result.Add(new XElement(c));
                    continue;
                }

                XElement? existing = result.Element(c.Name);
                bool replace = existing == null || !c.HasElements
                    || string.Equals(c.Attribute("Inherit")?.Value, "false", StringComparison.OrdinalIgnoreCase);
                existing?.Remove();
                result.Add(replace ? new XElement(c) : Merge(existing!, c));
            }

            return result;
        }
    }
}
