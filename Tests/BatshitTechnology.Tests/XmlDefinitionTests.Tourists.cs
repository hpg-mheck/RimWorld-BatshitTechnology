using System;
using System.Linq;
using System.Xml.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// XML regression coverage for the tour group (docs/SHOWCASE.md,
/// "Tourists"; slice 19000c): its hidden faction, the tourists' kind,
/// duty and composure, the narration for a lost guest and the grenade's
/// guest volunteer, and the Tractor Bomb's texts on whom it protests
/// about.
/// </summary>
public sealed partial class XmlDefinitionTests
{
    /// <summary>The operator's line for a lost tourist, word for word.</summary>
    private const string GuestsLostLine = "Our guests are enjoying an unusually close look.";

    /// <summary>
    /// The tourists' faction is hidden (no faction list, no goodwill, so
    /// no goodwill letters), never made with a world or offered at world
    /// creation, raids no one, and has no leader to lose: no pawn group
    /// makers and no fixed leader kinds.
    /// </summary>
    [Fact(DisplayName = "The tour group's faction is hidden and made only for showcases")]
    public void TouristFaction_should_be_hidden_and_never_generated()
    {
        XElement faction = SingleDef("FactionDef", "BT_ShowcaseTourists");
        Assert.Equal("true", faction.Element("hidden")?.Value);
        Assert.Equal("0", faction.Element("requiredCountAtGameStart")?.Value);
        Assert.Equal("0", faction.Element("maxConfigurableAtWorldCreation")?.Value);
        Assert.Equal("false", faction.Element("displayInFactionSelection")?.Value);
        Assert.Equal("true", faction.Element("raidsForbidden")?.Value);
        Assert.Equal("true", faction.Element("humanlikeFaction")?.Value);
        Assert.Null(faction.Element("permanentEnemy"));
        Assert.Null(faction.Element("naturalEnemy"));
        Assert.Null(faction.Element("pawnGroupMakers"));
        Assert.Null(faction.Element("fixedLeaderKinds"));
        Assert.Null(faction.Element("generateNewLeaderFromMapMembersOnly"));
        Assert.Equal("BT_ShowcaseTourist", faction.Element("basicMemberKind")?.Value);
    }

    /// <summary>
    /// A tourist is a human of that faction who carries nothing the tour
    /// must pick up (no weapon money, no food) and never flees when hurt.
    /// </summary>
    [Fact(DisplayName = "A tourist is an unarmed human with empty pockets")]
    public void TouristKind_should_be_unarmed_with_empty_pockets()
    {
        XElement kind = SingleDef("PawnKindDef", "BT_ShowcaseTourist");
        Assert.Equal("Human", kind.Element("race")?.Value);
        Assert.Equal("BT_ShowcaseTourists", kind.Element("defaultFactionDef")?.Value);
        Assert.Equal("false", kind.Element("isFighter")?.Value);
        Assert.Equal("0~0", kind.Element("weaponMoney")?.Value);
        Assert.Equal("0", kind.Element("invNutrition")?.Value);
        Assert.Equal("-1~-1", kind.Element("fleeHealthThresholdRange")?.Value);
        Assert.Equal("0", kind.Element("techHediffsChance")?.Value);
    }

    /// <summary>
    /// The tourists' duty has no combat in it: only a wander near the
    /// spot the director sets, at a jog when far from it. Their composure
    /// blocks mental breaks and stops hunger.
    /// </summary>
    [Fact(DisplayName = "A tourist's duty is a wander, and composure holds it")]
    public void TouristDuty_should_have_no_combat()
    {
        XElement duty = SingleDef("DutyDef", "BT_ShowcaseTourist");
        string[] nodes = duty.Descendants("li")
            .Select(l => l.Attribute("Class")?.Value)
            .Where(c => c != null)
            .Select(c => c!)
            .ToArray();
        Assert.Equal(new[] { "JobGiver_WanderNearDutyLocation" }, nodes);
        Assert.Equal("Jog", duty.Descendants("locomotionUrgencyOutsideRadius")
            .Single().Value);
        XElement stage = SingleDef("HediffDef", "BT_TourComposure")
            .Element("stages")!.Elements("li").Single();
        Assert.Equal("true", stage.Element("blocksMentalBreaks")?.Value);
        Assert.Equal("0", stage.Element("hungerRateFactor")?.Value);
    }

    /// <summary>
    /// Every scene that narrates a lost guest says the operator's line,
    /// and only those scenes; the grenade's volunteer is a guest, not a
    /// member of the family.
    /// </summary>
    [Fact(DisplayName = "A lost guest gets the operator's line; the volunteer is a guest")]
    public void Narration_should_speak_of_guests()
    {
        XElement tour = LoadFixture(System.IO.Path.Combine("Defs", "Misc", "BT_Showcases.xml"))
            .Root!.Elements("BatshitTechnology.Showcases.ShowcaseDef").Single();
        var lines = tour.Element("narration")!.Elements("li")
            .Select(l => (
                Scene: l.Element("scene")?.Value.Trim(),
                When: l.Element("when")?.Value.Trim(),
                Text: l.Element("text")?.Value))
            .ToList();
        var lost = lines.Where(l => l.When == TourScript.GuestsLost).ToList();
        Assert.Equal(
            TourScript.Scenes.Where(s => TourScript.NarratesGuestsLost(s.Key))
                .Select(s => s.Key).OrderBy(k => k, StringComparer.Ordinal),
            lost.Select(l => l.Scene).OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(lost, l => Assert.Equal(GuestsLostLine, l.Text));
        string thrown = lines.Single(l => l.Scene == TourScript.Grenade
            && l.When == TourScript.Thrown).Text!;
        string alone = lines.Single(l => l.Scene == TourScript.Grenade
            && l.When == TourScript.ThrownAlone).Text!;
        Assert.Contains("guest", thrown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("guests", alone, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("family", thrown, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The robot's and the research's descriptions say it protests about
    /// a colonist or a guest in its tractor beam, as the code does.
    /// </summary>
    [Fact(DisplayName = "The Tractor Bomb's texts protest about guests too")]
    public void TractorBomb_texts_should_name_guests()
    {
        string robot = ThingDef("BT_TractorBomb").Element("description")!.Value;
        Assert.Contains("colonist or guest", robot, StringComparison.Ordinal);
        string research = SingleDef("ResearchProjectDef", "BT_Research_TractorBomb")
            .Element("description")!.Value;
        Assert.Contains("colonist or guest", research, StringComparison.Ordinal);
    }
}
