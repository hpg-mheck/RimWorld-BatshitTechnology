using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// XML regression coverage for the Tractor Barn (docs/TRACTOR-BOMB.md,
/// "The Tractor Barn"; roadmap step 8): the barn's cost, research and
/// bills, its one autonomous recipe and the hauling job that loads it,
/// its repair hopper and dock job, its own death, and the removal of the
/// machining-table stopgap it replaces.
/// </summary>
public sealed partial class XmlDefinitionTests
{
    /// <summary>The barn's class.</summary>
    private const string BarnClass = "BatshitTechnology.Building_TractorBarn";

    /// <summary>
    /// The barn costs what the spec says (250 steel, 8 components, 20
    /// batshittium), sits behind the Tractor Bomb project, is a 3 by 3
    /// production building with a door cell and a Bills tab, and makes one
    /// thing: a tractor bomb.
    /// </summary>
    [Fact(DisplayName = "Tractor Barn costs and unlocks as specified")]
    public void TractorBarn_should_match_spec()
    {
        var barn = ThingDef("BT_TractorBarn");
        Assert.Equal(BarnClass, barn.Element("thingClass")?.Value);
        Assert.Equal("(3,3)", barn.Element("size")?.Value);
        Assert.Equal("Production", barn.Element("designationCategory")?.Value);
        Assert.Equal("true", barn.Element("hasInteractionCell")?.Value);
        Assert.Equal(new[] { "ITab_Bills" },
            ListValues(barn.Element("inspectorTabs")));
        Assert.Equal(new[] { "BT_Research_TractorBomb" },
            ListValues(barn.Element("researchPrerequisites")));
        Assert.Equal(new[] { "BT_BuildTractorBomb" },
            ListValues(barn.Element("recipes")));
        var costs = barn.Element("costList")?.Elements()
            .ToDictionary(e => e.Name.LocalName, e => e.Value.Trim());
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Steel"] = "250",
                ["ComponentIndustrial"] = "8",
                ["BT_Batshittium"] = "20",
            },
            costs);
        Assert.Equal("Things/Building/Production/BT_TractorBarn",
            barn.Element("graphicData")?.Element("texPath")?.Value);
        Assert.Equal("(3,3)",
            barn.Element("graphicData")?.Element("drawSize")?.Value);
    }

    /// <summary>
    /// Its repair hopper holds steel that haulers always keep stocked: a
    /// work table with a refuelable comp takes no bills while it is empty,
    /// so the player may neither switch refuelling off nor dial it down.
    /// Nothing burns the steel while the barn is idle.
    /// </summary>
    [Fact(DisplayName = "Tractor Barn repair hopper holds steel, always refilled")]
    public void TractorBarnHopper_should_hold_steel()
    {
        var hopper = ThingDef("BT_TractorBarn").Element("comps")
            ?.Elements("li").Single(e => e.Attribute("Class")?.Value
                == "CompProperties_Refuelable");
        Assert.NotNull(hopper);
        Assert.Equal(new[] { "Steel" },
            ListValues(hopper!.Element("fuelFilter")?.Element("thingDefs")));
        Assert.Equal("0", hopper.Element("fuelConsumptionRate")?.Value);
        Assert.Equal("true", hopper.Element("consumeFuelOnlyWhenUsed")?.Value);
        Assert.Equal("false",
            hopper.Element("showAllowAutoRefuelToggle")?.Value);
        Assert.Equal("false",
            hopper.Element("targetFuelLevelConfigurable")?.Value);
        Assert.True(float.Parse(
            hopper.Element("fuelCapacity")?.Value ?? "0",
            System.Globalization.CultureInfo.InvariantCulture) >= 10f);
    }

    /// <summary>
    /// The barn's recipe costs a tractor bomb's spec (60 steel, 3
    /// components, 5 batshittium), is autonomous (formingTicks, so vanilla
    /// makes it a Bill_Autonomous), makes no item, counts robots and
    /// planted bombs for "Do until you have X", shows the robot as its
    /// icon, and sits behind the Tractor Bomb project. It names no
    /// recipe users: the barn lists it.
    /// </summary>
    [Fact(DisplayName = "Barn recipe: 60 steel, 3 components, 5 batshittium, no crafter")]
    public void TractorBarnRecipe_should_cost_the_spec()
    {
        var recipe = SingleDef("RecipeDef", "BT_BuildTractorBomb");
        var costs = (recipe.Element("ingredients")?.Elements("li")
                ?? Enumerable.Empty<XElement>())
            .ToDictionary(
                li => li.Descendants("thingDefs").Single().Elements("li")
                    .Single().Value,
                li => li.Element("count")?.Value);
        Assert.Equal(
            new Dictionary<string, string?>
            {
                ["Steel"] = "60",
                ["ComponentIndustrial"] = "3",
                ["BT_Batshittium"] = "5",
            },
            costs);
        Assert.True(int.Parse(recipe.Element("formingTicks")?.Value ?? "0")
            > 0, "an autonomous bill needs formingTicks");
        Assert.Equal("BatshitTechnology.RecipeWorkerCounter_TractorBombs",
            recipe.Element("workerCounterClass")?.Value);
        Assert.Equal("BT_TractorBomb", recipe.Element("uiIconThing")?.Value);
        Assert.Equal("BT_Research_TractorBomb",
            recipe.Element("researchPrerequisite")?.Value);
        Assert.Null(recipe.Element("products"));
        Assert.Null(recipe.Element("recipeUsers"));
        Assert.Null(recipe.Element("workSkill"));
    }

    /// <summary>
    /// Haulers load the barn: vanilla's bill job under Hauling, for the
    /// barn alone, as vanilla's crematorium and campfire bills are done.
    /// </summary>
    [Fact(DisplayName = "Haulers load the Tractor Barn")]
    public void TractorBarnWorkGiver_should_be_hauling()
    {
        var giver = SingleDef("WorkGiverDef", "BT_DoBillsTractorBarn");
        Assert.Equal("WorkGiver_DoBill", giver.Element("giverClass")?.Value);
        Assert.Equal("Hauling", giver.Element("workType")?.Value);
        Assert.Equal(new[] { "BT_TractorBarn" },
            ListValues(giver.Element("fixedBillGiverDefs")));
    }

    /// <summary>
    /// A damaged robot's trip home is a job of its own, driven by the
    /// dock driver, that a hostile can break off but damage does not.
    /// </summary>
    [Fact(DisplayName = "Tractor Bomb docking job is wired up")]
    public void TractorBombDock_should_be_wired_up()
    {
        var dock = SingleDef("JobDef", "BT_TractorBombDock");
        Assert.Equal("BatshitTechnology.JobDriver_TractorBombDock",
            dock.Element("driverClass")?.Value);
        Assert.Equal("Never", dock.Element("checkOverrideOnDamage")?.Value);
        Assert.Contains("repairs", dock.Element("reportString")?.Value);
    }

    /// <summary>
    /// The barn's own death (docs/ARTISTIC-MOTIF.md, "Every death is
    /// distinct"): a blast and a stampede of spare tyres, flung as a
    /// projectile of its own that draws the mod's art.
    /// </summary>
    [Fact(DisplayName = "A destroyed Tractor Barn flings its spare tyres")]
    public void TractorBarnDeath_should_fling_tyres()
    {
        var blast = LastComp("BT_TractorBarn");
        Assert.Equal("BatshitTechnology.CompProperties_DeathBlast",
            blast.Attribute("Class")?.Value);
        var stampede = blast.Element("blast");
        Assert.Equal("BatshitTechnology.Detonation_TyreStampede",
            stampede?.Attribute("Class")?.Value);
        Assert.Equal("BT_Projectile_TractorBarnTyre",
            stampede?.Element("tyreProjectile")?.Value);

        var tyre = ThingDef("BT_Projectile_TractorBarnTyre");
        Assert.Equal("BaseBullet", tyre.Attribute("ParentName")?.Value);
        Assert.Equal("Blunt",
            tyre.Element("projectile")?.Element("damageDef")?.Value);
    }

    /// <summary>
    /// The machining-table stopgap is gone now the barn builds robots
    /// (roadmap "Decided after the core build"): no recipe of that name,
    /// no mod recipe at the machining table, and no def still naming its
    /// worker.
    /// </summary>
    [Fact(DisplayName = "The machining-table stopgap recipe is gone")]
    public void StopgapRecipe_should_be_gone()
    {
        var recipes = AllDefDocuments()
            .SelectMany(d => d.Root?.Elements("RecipeDef")
                ?? Enumerable.Empty<XElement>())
            .ToList();
        Assert.DoesNotContain(recipes,
            r => r.Element("defName")?.Value == "BT_AssembleTractorBomb");
        Assert.DoesNotContain(recipes,
            r => ListValues(r.Element("recipeUsers")).Contains("TableMachining"));
        Assert.DoesNotContain(AllDefDocuments().SelectMany(d => d.Descendants()),
            e => !e.HasElements && e.Value.Contains(
                "RecipeWorker_AssembleTractorBomb", StringComparison.Ordinal));
    }

    /// <summary>
    /// The robot's and the research's descriptions give the factory
    /// settings' real sizes (Neighborly and Firework, from Core) and the
    /// named Proximity stops, never the retired Disposition's names or the
    /// old 10-tile pull and 5-tile blast (review of slice 16000).
    /// </summary>
    [Fact(DisplayName = "Tractor Bomb descriptions give the named settings' sizes")]
    public void TractorBombDescriptions_should_match_named_settings()
    {
        string pull = Tiles(TractorBombYield.DefaultPullRadius);
        string blast = Tiles(TractorBombYield.DefaultBlastRadius);
        string robot = ThingDef("BT_TractorBomb").Element("description")?.Value
            ?? string.Empty;
        string research = SingleDef("ResearchProjectDef", "BT_Research_TractorBomb")
            .Element("description")?.Value ?? string.Empty;
        foreach ((string what, string text) in new[]
            { ("robot", robot), ("research", research) })
        {
            Assert.Contains($"within {pull} tiles", text, StringComparison.Ordinal);
            Assert.Contains($"over {blast} tiles", text, StringComparison.Ordinal);
            foreach (string retired in new[]
                { "Cautious", "Moderate", "disposition" })
            {
                Assert.False(
                    text.Contains(retired, StringComparison.OrdinalIgnoreCase),
                    $"the {what} description still says '{retired}'");
            }

            // "10 tiles" or "5 tiles" on its own, not "15" or "7.5".
            Assert.DoesNotMatch(@"(?<![\d.])(10|5) tiles", text);
        }

        foreach (string stop in new[] { "Timid", "Prudent", "Nosy" })
        {
            Assert.Contains(stop, robot, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A radius as the descriptions write it: whole numbers bare, others
    /// with one decimal place.
    /// </summary>
    /// <param name="radius">The radius, in tiles.</param>
    /// <returns>"15" or "7.5".</returns>
    private static string Tiles(double radius) =>
        radius.ToString("0.#", CultureInfo.InvariantCulture);
}
