using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace RandomReplicator.Tests;

/// <summary>
/// Provides regression coverage for XML definitions that RimWorld consumes.
/// These tests ensure critical metadata stays aligned with the design notes
/// codified in the mod documentation while remaining runnable without the game
/// binaries present.
/// </summary>
public sealed class XmlDefinitionTests
{
    private static string FixturesDirectory => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures");

    [Fact(DisplayName = "About.xml should expose stable package identity")]
    public void AboutMetadata_should_define_expected_package_id_and_version()
    {
        var aboutPath = Path.Combine(FixturesDirectory, "About.xml");
        Assert.True(File.Exists(aboutPath), "About.xml was not copied to the test output directory.");

        var document = XDocument.Load(aboutPath);
        var root = document.Root ?? throw new InvalidOperationException("About.xml missing root node.");

        Assert.Equal("Matt.RandomReplicator", root.Element("packageId")?.Value);
        Assert.Equal("Random Replicator", root.Element("name")?.Value);
        Assert.Equal("0.4.0", root.Element("modVersion")?.Value);

        var supportedVersions = root.Element("supportedVersions")?
            .Elements("li")
            .Select(element => element.Value)
            .ToArray() ?? Array.Empty<string>();

        Assert.Contains("1.5", supportedVersions);
    }

    [Fact(DisplayName = "ThingDef should retain replicator production settings")]
    public void RandomReplicatorThing_should_expose_expected_comp_settings()
    {
        var replicatorDef = LoadThingDef("RandomReplicator");
        var compElement = replicatorDef
            .Element("comps")?
            .Elements("li")
            .Single(element => element.Attribute("Class")?.Value == "RandomReplicator.CompProperties_RandomReplicator");

        Assert.Equal("15000", compElement?.Element("ticksPerSpawn")?.Value);
        Assert.Equal("10", compElement?.Element("maxNearbyStacks")?.Value);
        Assert.Equal("1.9", compElement?.Element("searchRadius")?.Value);
        Assert.Equal("true", compElement?.Element("requirePower")?.Value);
        Assert.Equal("true", compElement?.Element("writeTimeLeftToSpawn")?.Value);
        Assert.Equal("true", compElement?.Element("preferInteractionCell")?.Value);
    }

    [Fact(DisplayName = "ThingDef should produce meals and raw foods")]
    public void RandomReplicatorThing_should_define_allowed_food_categories()
    {
        var replicatorDef = LoadThingDef("RandomReplicator");
        var categories = replicatorDef
            .Element("comps")?
            .Elements("li")
            .Single(element => element.Attribute("Class")?.Value == "RandomReplicator.CompProperties_RandomReplicator")
            .Element("allowedThingCategoryDefs")?
            .Elements("li")
            .Select(element => element.Value)
            .ToArray() ?? Array.Empty<string>();

        Assert.Equal(new[] { "FoodMeals", "FoodRaw" }, categories);
    }

    [Fact(DisplayName = "Self-destruct designation should remain player facing")]
    public void SelfDestructDesignation_should_retain_player_facing_settings()
    {
        var designationDocument = LoadDocumentFromFixtures(Path.Combine("Defs", "DesignationDefs", "RR_SelfDestructDesignation.xml"));
        var designation = designationDocument.Root?.Element("DesignationDef") ?? throw new InvalidOperationException("DesignationDef missing.");

        Assert.Equal("RR_SelfDestruct", designation.Element("defName")?.Value);
        Assert.Equal("arm self-destruct", designation.Element("label")?.Value);
        Assert.Equal("Thing", designation.Element("targetType")?.Value);
        Assert.Equal("true", designation.Element("removeOnDeath")?.Value);
    }

    [Fact(DisplayName = "Self-destruct work giver should remain high priority flick task")]
    public void WorkGiver_should_require_flick_priority_and_manipulation()
    {
        var workGiverDocument = LoadDocumentFromFixtures(Path.Combine("Defs", "WorkGiverDefs", "RR_SelfDestruct_WorkGiver.xml"));
        var workGiver = workGiverDocument.Root?.Element("WorkGiverDef") ?? throw new InvalidOperationException("WorkGiverDef missing.");

        Assert.Equal("Flick", workGiver.Element("workType")?.Value);
        Assert.Equal("999", workGiver.Element("priorityInType")?.Value);

        var capacities = workGiver.Element("requiredCapacities")?
            .Elements("li")
            .Select(element => element.Value)
            .ToArray() ?? Array.Empty<string>();

        Assert.Contains("Manipulation", capacities);
    }

    private static XElement LoadThingDef(string defName)
    {
        var document = LoadDocumentFromFixtures(Path.Combine("Defs", "ThingDefs", "Buildings_RandomReplicator.xml"));
        var thingDef = document.Root?
            .Elements("ThingDef")
            .SingleOrDefault(element => string.Equals(element.Element("defName")?.Value, defName, StringComparison.Ordinal));

        return thingDef ?? throw new InvalidOperationException($"ThingDef '{defName}' not found.");
    }

    private static XDocument LoadDocumentFromFixtures(string relativePath)
    {
        var path = Path.Combine(FixturesDirectory, relativePath);
        Assert.True(File.Exists(path), $"Expected fixture '{relativePath}' to exist.");
        using var stream = File.OpenRead(path);
        return XDocument.Load(stream, LoadOptions.SetLineInfo);
    }
}
