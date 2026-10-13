using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace RandomReplicator.Tests;

/// <summary>
/// Provides regression coverage for XML definitions that RimWorld consumes.
/// These tests pin the corrected Defs (several v0.2 values were invalid in
/// RimWorld 1.6) and the cross-references between them, and run without the
/// game binaries present.
/// </summary>
public sealed class XmlDefinitionTests
{
    private const string ReplicatorCompClass =
        "RandomReplicator.CompProperties_RandomReplicator";

    /// <summary>Gets the directory the Defs, About and Textures are copied to.</summary>
    private static string FixturesDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    /// Verifies the mod's identity, version and supported game version.
    /// </summary>
    [Fact(DisplayName = "About.xml exposes stable identity and targets 1.6")]
    public void AboutMetadata_should_define_identity_version_and_game()
    {
        var root = LoadFixture("About.xml").Root
            ?? throw new InvalidOperationException("About.xml has no root.");

        Assert.Equal("Matt.RandomReplicator", root.Element("packageId")?.Value);
        Assert.Equal("Random Replicator", root.Element("name")?.Value);
        Assert.Equal("0.5.0", root.Element("modVersion")?.Value);
        Assert.Equal(new[] { "1.6" }, ListValues(root.Element("supportedVersions")));
    }

    /// <summary>
    /// Verifies the replicator's production, storage and power settings.
    /// </summary>
    [Fact(DisplayName = "Replicator keeps its production and storage settings")]
    public void Replicator_should_expose_expected_comp_settings()
    {
        var comp = ReplicatorComp();

        Assert.Equal("15000", comp.Element("ticksPerSpawn")?.Value);
        Assert.Equal("10", comp.Element("storageCapacity")?.Value);
        Assert.Equal("true", comp.Element("requirePower")?.Value);
        Assert.Equal("50", comp.Element("refrigerationPowerConsumption")?.Value);
        Assert.Equal(
            new[] { "FoodMeals", "FoodRaw" },
            ListValues(comp.Element("allowedThingCategoryDefs")));
    }

    /// <summary>
    /// Verifies the replicator uses its storage building, has the
    /// interaction cell its code needs, and is unlocked by Batshittium Lore.
    /// </summary>
    [Fact(DisplayName = "Replicator uses its building class, interaction cell and research")]
    public void Replicator_should_use_storage_building_and_lore_research()
    {
        var def = ThingDef("RandomReplicator");

        Assert.Equal(
            "RandomReplicator.Building_RandomReplicator",
            def.Element("thingClass")?.Value);
        Assert.Equal("true", def.Element("hasInteractionCell")?.Value);
        Assert.Equal(
            new[] { "RR_BatshittiumLore" },
            ListValues(def.Element("researchPrerequisites")));
    }

    /// <summary>
    /// Regression: v0.2 used icon, saveKey and removeOnDeath, which
    /// DesignationDef does not have; the overlay texture field is
    /// texturePath.
    /// </summary>
    [Fact(DisplayName = "Self-destruct designation uses only real DesignationDef fields")]
    public void SelfDestructDesignation_should_use_valid_fields()
    {
        var designation = SingleDef("DesignationDef", "RR_SelfDestruct");

        Assert.Equal("arm self-destruct", designation.Element("label")?.Value);
        Assert.Equal("Thing", designation.Element("targetType")?.Value);
        Assert.Equal(
            "UI/Commands/Detonate", designation.Element("texturePath")?.Value);
        foreach (var invalid in new[] { "icon", "saveKey", "removeOnDeath" })
        {
            Assert.Null(designation.Element(invalid));
        }
    }

    /// <summary>
    /// Regression: v0.2 used workType Flick, which does not exist (vanilla
    /// flicking is BasicWorker), so nobody could ever arm the self-destruct.
    /// </summary>
    [Fact(DisplayName = "Self-destruct work giver uses a work type that exists")]
    public void SelfDestructWorkGiver_should_use_basic_worker()
    {
        var workGiver = SingleDef("WorkGiverDef", "RR_WorkGiverArmSelfDestruct");

        Assert.Equal("BasicWorker", workGiver.Element("workType")?.Value);
        Assert.Equal("999", workGiver.Element("priorityInType")?.Value);
        Assert.Contains(
            "Manipulation", ListValues(workGiver.Element("requiredCapacities")));
        Assert.Null(workGiver.Element("equivalenceGroup"));
    }

    /// <summary>
    /// Regression: checkOverrideOnDamage is an enum in RimWorld 1.6, not a
    /// boolean.
    /// </summary>
    [Fact(DisplayName = "Arming job uses an enum value for checkOverrideOnDamage")]
    public void ArmingJob_should_use_enum_damage_override()
    {
        var job = SingleDef("JobDef", "RR_ArmSelfDestruct");

        Assert.Equal("Never", job.Element("checkOverrideOnDamage")?.Value);
    }

    /// <summary>
    /// Verifies the research tab and the Lore and Arcana projects.
    /// </summary>
    [Fact(DisplayName = "Batshit Technology research tab and projects")]
    public void Research_should_define_tab_lore_and_arcana()
    {
        Assert.Equal(
            "Batshit Technology",
            SingleDef("ResearchTabDef", "RR_BatshitTechnology").Element("label")?.Value);

        var lore = SingleDef("ResearchProjectDef", "RR_BatshittiumLore");
        Assert.Equal("500", lore.Element("baseCost")?.Value);
        Assert.Equal("Neolithic", lore.Element("techLevel")?.Value);
        Assert.Null(lore.Element("prerequisites"));

        var arcana = SingleDef("ResearchProjectDef", "RR_BatshittiumArcana");
        Assert.Equal("750", arcana.Element("baseCost")?.Value);
        Assert.Equal(
            new[] { "RR_BatshittiumLore" }, ListValues(arcana.Element("prerequisites")));
    }

    /// <summary>
    /// Verifies the transmogrifier turns ten stone chunks into one unit of
    /// batshittium and that crafters can work it.
    /// </summary>
    [Fact(DisplayName = "Transmogrifier makes 1 batshittium from 10 stone chunks")]
    public void Transmogrifier_should_convert_ten_chunks_to_one_batshittium()
    {
        var recipe = SingleDef("RecipeDef", "RR_TransmogrifyRockChunks");
        var ingredient = recipe.Element("ingredients")?.Element("li");
        Assert.Equal("10", ingredient?.Element("count")?.Value);
        Assert.Equal(
            new[] { "StoneChunks" },
            ListValues(ingredient?.Element("filter")?.Element("categories")));
        Assert.Equal("1", recipe.Element("products")?.Element("RR_Batshittium")?.Value);

        var bench = ThingDef("RR_BatshittiumTransmogrifier");
        Assert.Contains("RR_TransmogrifyRockChunks", ListValues(bench.Element("recipes")));
        var workGiver = SingleDef("WorkGiverDef", "RR_DoBillsTransmogrify");
        Assert.Contains(
            "RR_BatshittiumTransmogrifier",
            ListValues(workGiver.Element("fixedBillGiverDefs")));
    }

    /// <summary>
    /// Verifies the reactor's output, fuel and burn settings.
    /// </summary>
    [Fact(DisplayName = "Reactor burns batshittium for 250 kW")]
    public void Reactor_should_burn_batshittium_for_250_kilowatts()
    {
        var comps = ThingDef("RR_BatshittiumReactor").Element("comps")?.Elements("li")
            .ToList() ?? new List<XElement>();

        var power = comps.Single(c => c.Attribute("Class")?.Value == "CompProperties_Power");
        Assert.Equal("CompPowerPlant", power.Element("compClass")?.Value);
        Assert.Equal("-250000", power.Element("basePowerConsumption")?.Value);

        var fuel = comps.Single(c => c.Attribute("Class")?.Value == "CompProperties_Refuelable");
        Assert.Equal("true", fuel.Element("consumeFuelOnlyWhenUsed")?.Value);
        Assert.Equal(
            new[] { "RR_Batshittium" },
            ListValues(fuel.Element("fuelFilter")?.Element("thingDefs")));

        var reactor = comps.Single(c => c.Attribute("Class")?.Value
            == "RandomReplicator.CompProperties_BatshittiumReactor");
        Assert.Equal("0.1~36", reactor.Element("burnHoursRange")?.Value);
    }

    /// <summary>
    /// Verifies every RR_-prefixed name the Defs reference is declared as a
    /// defName somewhere in the mod, catching typos in cross-references.
    /// </summary>
    [Fact(DisplayName = "Every RR_ reference resolves to a declared def")]
    public void Defs_should_only_reference_declared_mod_defs()
    {
        var documents = AllDefDocuments().ToList();
        var declared = documents
            .SelectMany(d => d.Descendants("defName"))
            .Select(e => e.Value.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var referenced = documents
            .SelectMany(d => d.Descendants())
            .Where(e => e.Name.LocalName != "defName")
            .SelectMany(e => new[] { e.Name.LocalName }
                .Concat(e.HasElements ? Array.Empty<string>() : new[] { e.Value.Trim() }))
            .Where(name => name.StartsWith("RR_", StringComparison.Ordinal));

        Assert.Empty(referenced.Where(name => !declared.Contains(name)).Distinct());
    }

    /// <summary>
    /// Verifies every texture path that names a mod asset (an RR_ file) has
    /// a PNG in Textures/. Vanilla paths cannot be checked without the game.
    /// </summary>
    [Fact(DisplayName = "Every mod texture path has a PNG")]
    public void Defs_should_have_textures_for_mod_texture_paths()
    {
        var texturePaths = AllDefDocuments()
            .SelectMany(d => d.Descendants())
            .Where(e => e.Name.LocalName is "texPath" or "fuelIconPath" or "texturePath")
            .Select(e => e.Value.Trim())
            .Where(path => Path.GetFileName(path).StartsWith("RR_", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.NotEmpty(texturePaths);
        var missing = texturePaths.Where(path => !File.Exists(
            Path.Combine(FixturesDirectory, "Textures", path + ".png")));
        Assert.Empty(missing);
    }

    /// <summary>
    /// Regression (found by the in-game smoke test): RimWorld's config check
    /// rejects impassable, player-buildable buildings that can be shot or
    /// seen over, so each needs fillPercent 1.
    /// </summary>
    [Fact(DisplayName = "Impassable buildable buildings block shots fully")]
    public void ImpassableBuildings_should_have_full_fill()
    {
        var offenders = AllThingDefs()
            .Where(d => d.Element("passability")?.Value == "Impassable"
                && d.Element("designationCategory") != null
                && d.Element("fillPercent")?.Value != "1")
            .Select(DefName);

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Regression (found by the in-game smoke test): a minifiable building
    /// needs a thing category for its minified form. BenchBase supplies one.
    /// </summary>
    [Fact(DisplayName = "Minifiable buildings have a thing category")]
    public void MinifiableBuildings_should_have_a_thing_category()
    {
        var offenders = AllThingDefs()
            .Where(d => d.Element("minifiedDef") != null
                && d.Element("thingCategories") == null
                && d.Attribute("ParentName")?.Value != "BenchBase")
            .Select(DefName);

        Assert.Empty(offenders);
    }

    /// <summary>Every ThingDef across the Defs files.</summary>
    /// <returns>The ThingDef elements.</returns>
    private static IEnumerable<XElement> AllThingDefs() =>
        AllDefDocuments().SelectMany(
            d => d.Root?.Elements("ThingDef") ?? Enumerable.Empty<XElement>());

    /// <summary>A def element's defName, for failure messages.</summary>
    /// <param name="def">The def element.</param>
    /// <returns>Its defName, or "?".</returns>
    private static string DefName(XElement def) =>
        def.Element("defName")?.Value ?? "?";

    /// <summary>Finds the replicator's CompProperties_RandomReplicator node.</summary>
    /// <returns>The comp element.</returns>
    private static XElement ReplicatorComp() =>
        ThingDef("RandomReplicator").Element("comps")?.Elements("li")
            .Single(e => e.Attribute("Class")?.Value == ReplicatorCompClass)
        ?? throw new InvalidOperationException("Replicator comp not found.");

    /// <summary>Finds a ThingDef by defName in any Defs file.</summary>
    /// <param name="defName">The defName to find.</param>
    /// <returns>The ThingDef element.</returns>
    private static XElement ThingDef(string defName) => SingleDef("ThingDef", defName);

    /// <summary>Finds exactly one def of a type by defName across all Defs files.</summary>
    /// <param name="defType">The XML element name, e.g. ThingDef.</param>
    /// <param name="defName">The defName to find.</param>
    /// <returns>The def element.</returns>
    private static XElement SingleDef(string defType, string defName) =>
        AllDefDocuments()
            .SelectMany(d => d.Root?.Elements(defType) ?? Enumerable.Empty<XElement>())
            .Single(e => string.Equals(
                e.Element("defName")?.Value, defName, StringComparison.Ordinal));

    /// <summary>The text of each li child of a list element.</summary>
    /// <param name="list">The list element; may be null.</param>
    /// <returns>The values, or an empty array.</returns>
    private static string[] ListValues(XElement? list) =>
        list?.Elements("li").Select(e => e.Value.Trim()).ToArray() ?? Array.Empty<string>();

    /// <summary>Loads every XML file under the Defs fixtures.</summary>
    /// <returns>One document per file.</returns>
    private static IEnumerable<XDocument> AllDefDocuments()
    {
        var defsDirectory = Path.Combine(FixturesDirectory, "Defs");
        Assert.True(Directory.Exists(defsDirectory), "Defs fixtures were not copied.");
        return Directory.EnumerateFiles(defsDirectory, "*.xml", SearchOption.AllDirectories)
            .Select(path => XDocument.Load(path, LoadOptions.SetLineInfo));
    }

    /// <summary>Loads one fixture file relative to the fixtures directory.</summary>
    /// <param name="relativePath">The path under Fixtures.</param>
    /// <returns>The parsed document.</returns>
    private static XDocument LoadFixture(string relativePath)
    {
        var path = Path.Combine(FixturesDirectory, relativePath);
        Assert.True(File.Exists(path), $"Expected fixture '{relativePath}' to exist.");
        return XDocument.Load(path, LoadOptions.SetLineInfo);
    }
}
