using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// XML regression coverage for the implosions' shared defs
/// (Defs/Misc/BT_Implosion.xml): the stretching a pawn takes between two
/// opposing pulls, and the injury it leaves.
/// </summary>
public sealed partial class XmlDefinitionTests
{
    /// <summary>
    /// Stretching adds a stretch mark (a crack on solid parts), is
    /// external violence with a spaghettification death message, and has
    /// no armour category, so armour does not help. The stretch mark is
    /// an ordinary injury: lowercase label, tendable, able to scar, and
    /// mergeable, with labels for a part pulled off.
    /// </summary>
    [Fact(DisplayName = "Stretching and its stretch mark are wired up")]
    public void Stretching_should_match_spec()
    {
        var stretch = SingleDef("DamageDef", "BT_Stretch");
        Assert.Equal("stretching", stretch.Element("label")?.Value);
        Assert.Equal("DamageWorker_AddInjury",
            stretch.Element("workerClass")?.Value);
        Assert.Equal("true", stretch.Element("externalViolence")?.Value);
        Assert.Equal("BT_StretchMark", stretch.Element("hediff")?.Value);
        Assert.Equal("Crack", stretch.Element("hediffSolid")?.Value);
        Assert.Null(stretch.Element("armorCategory"));
        Assert.Contains("spaghettified",
            stretch.Element("deathMessage")?.Value ?? "");
        Assert.Contains("{0}", stretch.Element("deathMessage")?.Value ?? "");

        var mark = SingleDef("HediffDef", "BT_StretchMark");
        Assert.Equal("InjuryBase", mark.Attribute("ParentName")?.Value);
        string label = mark.Element("label")?.Value ?? "";
        Assert.Equal("stretch mark", label);
        Assert.Equal(label.ToLowerInvariant(), label);
        var comps = mark.Element("comps")?.Elements("li")
            .Select(li => li.Attribute("Class")?.Value)
            .ToList() ?? new List<string?>();
        Assert.Contains("HediffCompProperties_TendDuration", comps);
        Assert.Contains("HediffCompProperties_GetsPermanent", comps);
        var injury = mark.Element("injuryProps");
        Assert.Equal("true", injury?.Element("canMerge")?.Value);
        Assert.False(string.IsNullOrEmpty(
            injury?.Element("destroyedLabel")?.Value));
    }
}
