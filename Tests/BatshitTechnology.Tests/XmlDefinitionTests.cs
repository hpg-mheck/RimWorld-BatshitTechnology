using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Provides regression coverage for XML definitions that RimWorld consumes.
/// These tests pin the corrected Defs (several v0.2 values were invalid in
/// RimWorld 1.6) and the cross-references between them, and run without the
/// game binaries present.
/// </summary>
public sealed class XmlDefinitionTests
{
    private const string ReplicatorCompClass =
        "BatshitTechnology.CompProperties_RandomReplicator";

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

        Assert.Equal(
            "breakpoint8088.BatshitTechnology",
            root.Element("packageId")?.Value);
        Assert.Equal("Batshit Technology", root.Element("name")?.Value);
        Assert.Equal("0.8.0", root.Element("modVersion")?.Value);
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
    /// interaction cell its code needs, and is unlocked by its own project.
    /// </summary>
    [Fact(DisplayName = "Replicator uses its building class, interaction cell and research")]
    public void Replicator_should_use_storage_building_and_lore_research()
    {
        var def = ThingDef("BT_RandomReplicator");

        Assert.Equal(
            "BatshitTechnology.Building_RandomReplicator",
            def.Element("thingClass")?.Value);
        Assert.Equal("true", def.Element("hasInteractionCell")?.Value);
        Assert.Equal(
            new[] { "BT_Research_RandomReplicator" },
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
        var designation = SingleDef("DesignationDef", "BT_SelfDestruct");

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
        var workGiver = SingleDef("WorkGiverDef", "BT_WorkGiverArmSelfDestruct");

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
        var job = SingleDef("JobDef", "BT_ArmSelfDestruct");

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
            SingleDef("ResearchTabDef", "BT_BatshitTechnology").Element("label")?.Value);

        var lore = SingleDef("ResearchProjectDef", "BT_BatshittiumLore");
        Assert.Equal("500", lore.Element("baseCost")?.Value);
        Assert.Equal("Neolithic", lore.Element("techLevel")?.Value);
        Assert.Null(lore.Element("prerequisites"));

        var arcana = SingleDef("ResearchProjectDef", "BT_BatshittiumArcana");
        Assert.Equal("750", arcana.Element("baseCost")?.Value);
        Assert.Equal(
            new[] { "BT_BatshittiumLore" }, ListValues(arcana.Element("prerequisites")));
    }

    /// <summary>
    /// Verifies the transmogrifier turns ten stone chunks into one unit of
    /// batshittium and that crafters can work it.
    /// </summary>
    [Fact(DisplayName = "Transmogrifier makes 1 batshittium from 10 stone chunks")]
    public void Transmogrifier_should_convert_ten_chunks_to_one_batshittium()
    {
        var recipe = SingleDef("RecipeDef", "BT_TransmogrifyRockChunks");
        var ingredient = recipe.Element("ingredients")?.Element("li");
        Assert.Equal("10", ingredient?.Element("count")?.Value);
        Assert.Equal(
            new[] { "StoneChunks" },
            ListValues(ingredient?.Element("filter")?.Element("categories")));
        Assert.Equal("1", recipe.Element("products")?.Element("BT_Batshittium")?.Value);

        var bench = ThingDef("BT_BatshittiumTransmogrifier");
        Assert.Contains("BT_TransmogrifyRockChunks", ListValues(bench.Element("recipes")));
        var workGiver = SingleDef("WorkGiverDef", "BT_DoBillsTransmogrify");
        Assert.Contains(
            "BT_BatshittiumTransmogrifier",
            ListValues(workGiver.Element("fixedBillGiverDefs")));
    }

    /// <summary>
    /// Verifies the reactor's output, fuel and burn settings.
    /// </summary>
    [Fact(DisplayName = "Reactor burns batshittium for 250 kW")]
    public void Reactor_should_burn_batshittium_for_250_kilowatts()
    {
        var comps = ThingDef("BT_BatshittiumReactor").Element("comps")?.Elements("li")
            .ToList() ?? new List<XElement>();

        var power = comps.Single(c => c.Attribute("Class")?.Value == "CompProperties_Power");
        Assert.Equal("CompPowerPlant", power.Element("compClass")?.Value);
        Assert.Equal("-250000", power.Element("basePowerConsumption")?.Value);

        var fuel = comps.Single(c => c.Attribute("Class")?.Value == "CompProperties_Refuelable");
        Assert.Equal("true", fuel.Element("consumeFuelOnlyWhenUsed")?.Value);
        Assert.Equal(
            new[] { "BT_Batshittium" },
            ListValues(fuel.Element("fuelFilter")?.Element("thingDefs")));

        var reactor = comps.Single(c => c.Attribute("Class")?.Value
            == "BatshitTechnology.CompProperties_BatshittiumReactor");
        Assert.Equal("0.1~36", reactor.Element("burnHoursRange")?.Value);
    }

    /// <summary>
    /// Verifies the Brain Squeezer: a Lore-gated seat whose joy giver and
    /// job are wired up, a positive memory with 20 to 30 short, unique
    /// monologue lines, and a calm hediff that lowers the break threshold.
    /// </summary>
    [Fact(DisplayName = "Brain Squeezer is wired up with 20-30 short monologue lines")]
    public void BrainSqueezer_should_be_wired_up_with_monologue()
    {
        var chair = ThingDef("BT_BrainSqueezer");
        Assert.Equal("true", chair.Element("building")?.Element("isSittable")?.Value);
        Assert.Equal(
            new[] { "BT_Research_BrainSqueezer" }, ListValues(chair.Element("researchPrerequisites")));

        var giver = SingleDef("JoyGiverDef", "BT_UseBrainSqueezer");
        Assert.Equal(new[] { "BT_BrainSqueezer" }, ListValues(giver.Element("thingDefs")));
        Assert.Equal("BT_UseBrainSqueezer", giver.Element("jobDef")?.Value);

        var thought = SingleDef("ThoughtDef", "BT_BrainSqueezed");
        string mood = thought.Element("stages")?.Element("li")?.Element("baseMoodEffect")?.Value
            ?? "0";
        Assert.True(double.Parse(mood, CultureInfo.InvariantCulture) > 0, "mood effect");
        var lines = ListValues(
            thought.Element("modExtensions")?.Element("li")?.Element("lines"));
        Assert.InRange(lines.Length, 20, 30);
        Assert.Equal(lines.Length, lines.Distinct().Count());
        Assert.All(lines, line => Assert.True(
            line.Length <= 45, $"too long for the mood panel: {line}"));

        var calm = SingleDef("HediffDef", "BT_BrainSqueezedCalm");
        string offset = calm.Descendants("MentalBreakThreshold").Single().Value;
        Assert.True(double.Parse(offset, CultureInfo.InvariantCulture) < 0, "break threshold");
    }

    /// <summary>
    /// Verifies the Ballista system: bolts map to projectiles, store
    /// capacities, volley cadence and limits, magazine power, the
    /// Artificer's outputs, and five 1000-point projects behind Arcana.
    /// </summary>
    [Fact(DisplayName = "Ballista system is wired up to the agreed spec")]
    public void Ballista_system_should_match_spec()
    {
        Assert.Equal("BT_Projectile_BoltPainful",
            ThingDef("BT_BoltPainful").Descendants("projectile").Single().Value);
        Assert.Equal("BT_Projectile_BoltReallyPainful",
            ThingDef("BT_BoltReallyPainful").Descendants("projectile").Single().Value);
        Assert.Equal("Projectile_Explosive",
            ThingDef("BT_Projectile_BoltReallyPainful").Element("thingClass")?.Value);

        var ballista = ThingDef("BT_BatshittiumBallista");
        Assert.Equal("batshit ballista", ballista.Element("label")?.Value);
        Assert.Equal(
            "Batshit Ballista",
            SingleDef("ResearchProjectDef", "BT_Research_Ballista").Element("label")?.Value);
        Assert.Equal("16", ballista.Descendants("capacity").Single().Value);
        Assert.Equal(
            "5", ballista.Element("building")?.Element("turretBurstCooldownTime")?.Value);
        var ranges = ListValues(ballista.Descendants("rangePresets").Single())
            .Select(int.Parse).ToList();
        Assert.Equal(325, ranges.Max());
        Assert.DoesNotContain(ranges, r => r >= 80 && r < 90); // ring cap ~79.8
        Assert.Equal(256, ListValues(ballista.Descendants("countPresets").Single())
            .Select(int.Parse).Max());

        var magazine = ThingDef("BT_BallistaMagazine");
        Assert.Equal("2560", magazine.Descendants("capacity").Single().Value);
        Assert.Equal("5", magazine.Descendants("standbyWatts").Single().Value);
        Assert.Equal("500", magazine.Descendants("feedingWatts").Single().Value);

        var outputs = ThingDef("BT_BatshittiumArtificer").Descendants("outputs").Single()
            .Elements("li")
            .Select(o => $"{o.Element("thing")?.Value}:{o.Element("countPerUnit")?.Value}:"
                + o.Element("research")?.Value);
        Assert.Equal(
            new[]
            {
                "BT_BoltPainful:10:BT_Research_PainfulBolts",
                "BT_BoltReallyPainful:5:BT_Research_ReallyPainfulBolts",
                "BT_ErgonomicSingularityGrenade:1:BT_Research_SingularityGrenade",
            },
            outputs);

        var projects = new[]
        {
            "BT_Research_PainfulBolts", "BT_Research_ReallyPainfulBolts",
            "BT_Research_Artificer", "BT_Research_Magazine", "BT_Research_Ballista",
        };
        foreach (string name in projects)
        {
            Assert.Equal("1000", SingleDef("ResearchProjectDef", name).Element("baseCost")?.Value);
            Assert.True(Requires(name, "BT_BatshittiumArcana"), $"{name} must follow Arcana");
        }

        Assert.Equal(
            new[] { "BT_Research_Ballista" }, ListValues(ballista.Element("researchPrerequisites")));
        Assert.Equal(
            new[] { "BT_Research_Magazine" }, ListValues(magazine.Element("researchPrerequisites")));
    }

    /// <summary>
    /// Every non-weapon device has its own research project, after
    /// Batshittium Lore, and builds only once it is done.
    /// </summary>
    [Theory(DisplayName = "Each non-weapon device has its own project after Lore")]
    [InlineData("BT_BatshittiumTransmogrifier", "BT_Research_Transmogrifier")]
    [InlineData("BT_BatshittiumReactor", "BT_Research_Reactor")]
    [InlineData("BT_RandomReplicator", "BT_Research_RandomReplicator")]
    [InlineData("BT_BrainSqueezer", "BT_Research_BrainSqueezer")]
    public void Devices_should_have_their_own_research(string device, string project)
    {
        Assert.Equal(new[] { project }, ListValues(ThingDef(device).Element("researchPrerequisites")));
        var def = SingleDef("ResearchProjectDef", project);
        Assert.Equal("500", def.Element("baseCost")?.Value);
        Assert.Equal("BT_BatshitTechnology", def.Element("tab")?.Value);
        Assert.Equal(new[] { "BT_BatshittiumLore" }, ListValues(def.Element("prerequisites")));
    }

    /// <summary>
    /// No two projects in the tab share a spot in the research view, so the
    /// tree draws without overlaps.
    /// </summary>
    [Fact(DisplayName = "Research projects don't overlap in the tree")]
    public void Research_projects_should_not_overlap()
    {
        var spots = AllDefDocuments()
            .SelectMany(d => d.Root?.Elements("ResearchProjectDef") ?? Enumerable.Empty<XElement>())
            .Where(r => r.Element("tab")?.Value == "BT_BatshitTechnology")
            .Select(r => $"{r.Element("researchViewX")?.Value},{r.Element("researchViewY")?.Value}")
            .ToList();

        Assert.Equal(12, spots.Count);
        Assert.Equal(spots.Count, spots.Distinct().Count());
    }

    /// <summary>
    /// The replicator and reactor self-destruct: the comp is their last
    /// (they are destroyed mid-tick at zero), damage can trigger it 5% of
    /// the time with half of those skipping the countdown, and each has its
    /// own detonation: the replicator's bomb and the reactor's barrage of
    /// three full Ballista volleys.
    /// </summary>
    [Fact(DisplayName = "Replicator and reactor self-destruct as agreed")]
    public void SelfDestructs_should_match_spec()
    {
        var replicator = LastComp("BT_RandomReplicator");
        Assert.Equal("BatshitTechnology.CompProperties_SelfDestruct",
            replicator.Attribute("Class")?.Value);
        Assert.Equal("75", replicator.Element("blastRadius")?.Value);
        Assert.Equal("BatshitTechnology.Detonation_Bomb",
            replicator.Element("detonation")?.Attribute("Class")?.Value);
        Assert.Equal(3, replicator.Element("detonation")?.Element("secondaries")?.Elements("li").Count());

        var reactor = LastComp("BT_BatshittiumReactor");
        Assert.Equal("BatshitTechnology.CompProperties_SelfDestruct",
            reactor.Attribute("Class")?.Value);
        var barrage = reactor.Element("detonation");
        Assert.Equal("BatshitTechnology.Detonation_BoltBarrage", barrage?.Attribute("Class")?.Value);
        Assert.Equal("BT_Projectile_BoltReallyPainful", barrage?.Element("projectile")?.Value);
        Assert.Equal("256", barrage?.Element("boltsPerVolley")?.Value);

        foreach (var comp in new[] { replicator, reactor })
        {
            Assert.Equal("0.05", comp.Element("damageTriggerChance")?.Value);
            Assert.Equal("0.5", comp.Element("damageSkipCountdownChance")?.Value);
        }

        Assert.DoesNotContain(ReplicatorComp().Elements(), e => e.Name.LocalName.StartsWith("selfDestruct"));
    }

    /// <summary>
    /// Every buildable Batshit device either self-destructs or has its own
    /// death blast, never both, and as its last comp; no two death blasts
    /// share a kind except the Ballista and its magazine (both cook off).
    /// </summary>
    [Fact(DisplayName = "Every device self-destructs or has a unique death blast")]
    public void Devices_should_each_blow_up_their_own_way()
    {
        var devices = AllThingDefs()
            .Where(d => d.Element("designationCategory") != null)
            .ToList();
        Assert.Equal(7, devices.Count);

        var blasts = new List<string>();
        foreach (var device in devices)
        {
            var kinds = (device.Element("comps")?.Elements("li") ?? Enumerable.Empty<XElement>())
                .Select(c => c.Attribute("Class")?.Value)
                .Where(c => c is "BatshitTechnology.CompProperties_SelfDestruct"
                    or "BatshitTechnology.CompProperties_DeathBlast")
                .ToList();
            Assert.True(kinds.Count == 1, $"{DefName(device)}: {kinds.Count} blast comps");
            Assert.Equal(kinds[0], LastComp(DefName(device)).Attribute("Class")?.Value);

            var blast = LastComp(DefName(device)).Element("blast");
            if (blast != null)
            {
                blasts.Add(blast.Attribute("Class")?.Value ?? "");
            }
        }

        Assert.Equal(5, blasts.Count);
        Assert.Equal(4, blasts.Distinct().Count()); // Ballista and Magazine share the cook-off
    }

    /// <summary>
    /// The Ergonomic Singularity Grenade: an equippable thrown weapon whose
    /// projectile opens the singularity, with a raider-proof weapon tag, a
    /// 10-tile, five-second pull and a big explosion, behind its own project
    /// after the Artificer.
    /// </summary>
    [Fact(DisplayName = "Singularity grenade is wired up as agreed")]
    public void SingularityGrenade_should_match_spec()
    {
        var grenade = ThingDef("BT_ErgonomicSingularityGrenade");
        Assert.Equal("BaseWeapon", grenade.Attribute("ParentName")?.Value);
        var verb = grenade.Element("verbs")?.Elements("li").Single();
        Assert.Equal("Verb_LaunchProjectile", verb?.Element("verbClass")?.Value);
        Assert.Equal("BT_Projectile_SingularityGrenade", verb?.Element("defaultProjectile")?.Value);
        Assert.Equal(new[] { "BatshitSingularityGrenade" }, ListValues(grenade.Element("weaponTags")));

        var projectile = ThingDef("BT_Projectile_SingularityGrenade");
        Assert.Equal("BatshitTechnology.Projectile_SingularityGrenade",
            projectile.Element("thingClass")?.Value);
        // Regression (found by the smoke test): a thrown verb with a forced-miss
        // radius needs a Projectile_Explosive, or RimWorld reports a config error;
        // the class derives from it (checked by the Def validator's class
        // resolution and the smoke test's log scan), and goes off on landing.
        Assert.Equal("0", projectile.Descendants("explosionDelay").Single().Value);

        var field = ThingDef("BT_SingularityField");
        Assert.Equal("RealtimeOnly", field.Element("drawerType")?.Value);
        var settings = field.Descendants("li")
            .Single(e => e.Attribute("Class")?.Value == "BatshitTechnology.SingularityExtension");
        Assert.Equal("10", settings.Element("radius")?.Value);
        Assert.Equal("300", settings.Element("pullTicks")?.Value);
        Assert.Equal("BT_ErgonomicallySingularized", settings.Element("survivorThought")?.Value);

        // The vortex's outer, counter-rotating layer is loaded by path in code
        // (SingularityField.OuterTexPath), so no Def points at it.
        Assert.True(File.Exists(Path.Combine(
            FixturesDirectory, "Textures", "Things", "Mote", "BT_SingularityVortexOuter.png")));

        var research = SingleDef("ResearchProjectDef", "BT_Research_SingularityGrenade");
        Assert.Equal(new[] { "BT_Research_Artificer" }, ListValues(research.Element("prerequisites")));
    }

    /// <summary>
    /// Survivors get a huge, quip-labelled mood boost, drawn from 25 short,
    /// unique quips.
    /// </summary>
    [Fact(DisplayName = "Singularity survivors get a huge boost and one of 25 quips")]
    public void SingularitySurvivors_should_get_mood_and_quips()
    {
        var thought = SingleDef("ThoughtDef", "BT_ErgonomicallySingularized");
        Assert.Equal("BatshitTechnology.Thought_Quip", thought.Element("thoughtClass")?.Value);
        int mood = int.Parse(thought.Descendants("baseMoodEffect").Single().Value);
        Assert.True(mood >= 20, $"mood {mood}");

        string[] quips = ListValues(thought.Descendants("lines").Single());
        Assert.Equal(25, quips.Length);
        Assert.Equal(quips.Length, quips.Distinct().Count());
        Assert.All(quips, q => Assert.InRange(q.Length, 1, 70));
    }

    /// <summary>A ThingDef's last comp.</summary>
    /// <param name="defName">The ThingDef.</param>
    /// <returns>The last li under comps.</returns>
    private static XElement LastComp(string defName) =>
        ThingDef(defName).Element("comps")?.Elements("li").Last()
        ?? throw new InvalidOperationException($"{defName} has no comps.");

    /// <summary>Whether a research project requires another, directly or not.</summary>
    /// <param name="project">The project's defName.</param>
    /// <param name="required">The prerequisite to look for.</param>
    /// <returns>True if <paramref name="required"/> is upstream.</returns>
    private static bool Requires(string project, string required)
    {
        var direct = ListValues(
            SingleDef("ResearchProjectDef", project).Element("prerequisites"));
        return direct.Contains(required) || direct.Any(p => Requires(p, required));
    }

    /// <summary>
    /// Verifies every BT_-prefixed name the Defs reference is declared as a
    /// defName somewhere in the mod, catching typos in cross-references.
    /// </summary>
    [Fact(DisplayName = "Every BT_ reference resolves to a declared def")]
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
            .Where(name => name.StartsWith("BT_", StringComparison.Ordinal));

        Assert.Empty(referenced.Where(name => !declared.Contains(name)).Distinct());
    }

    /// <summary>
    /// Regression for the v0.8 rename: every def the mod declares, and every
    /// mod texture, carries the BT_ prefix, and the old RR_ prefix appears
    /// nowhere in the Defs (it would name a def that no longer exists).
    /// </summary>
    [Fact(DisplayName = "Defs and textures use the BT_ prefix, never RR_")]
    public void Defs_should_use_the_BT_prefix()
    {
        var documents = AllDefDocuments().ToList();
        var unprefixed = documents
            .SelectMany(d => d.Descendants("defName"))
            .Select(e => e.Value.Trim())
            .Where(name => !name.StartsWith("BT_", StringComparison.Ordinal));
        var oldPrefix = documents
            .SelectMany(d => d.Descendants())
            .SelectMany(e => e.Attributes().Select(a => a.Value)
                .Concat(e.HasElements ? Array.Empty<string>() : new[] { e.Value }))
            .Where(text => text.Contains("RR_", StringComparison.Ordinal));
        var textures = Directory.EnumerateFiles(
                Path.Combine(FixturesDirectory, "Textures"), "*.png",
                SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(name => !name!.StartsWith("BT_", StringComparison.Ordinal));

        Assert.Empty(unprefixed);
        Assert.Empty(oldPrefix);
        Assert.Empty(textures);
    }

    /// <summary>
    /// Every mod thing draws the mod's own art (docs/ARTISTIC-MOTIF.md), not
    /// a borrowed vanilla sprite, so the suite keeps one look. The Random
    /// Replicator borrowed the Infinite Chemreactor until v0.8.
    /// </summary>
    [Fact(DisplayName = "Every mod thing draws a mod texture")]
    public void ThingDefs_should_draw_mod_textures()
    {
        var borrowed = AllThingDefs()
            .Select(d => (Def: DefName(d),
                Path: d.Element("graphicData")?.Element("texPath")?.Value.Trim()))
            .Where(t => t.Path != null
                && !Path.GetFileName(t.Path).StartsWith("BT_", StringComparison.Ordinal))
            .Select(t => $"{t.Def}: {t.Path}");

        Assert.Empty(borrowed);
    }

    /// <summary>
    /// Verifies every texture path that names a mod asset (a BT_ file) has
    /// a PNG in Textures/. Vanilla paths cannot be checked without the game.
    /// </summary>
    [Fact(DisplayName = "Every mod texture path has a PNG")]
    public void Defs_should_have_textures_for_mod_texture_paths()
    {
        var texturePaths = AllDefDocuments()
            .SelectMany(d => d.Descendants())
            .Where(e => e.Name.LocalName is "texPath" or "fuelIconPath" or "texturePath")
            .Select(e => e.Value.Trim())
            .Where(path => Path.GetFileName(path).StartsWith("BT_", StringComparison.Ordinal))
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
        ThingDef("BT_RandomReplicator").Element("comps")?.Elements("li")
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
