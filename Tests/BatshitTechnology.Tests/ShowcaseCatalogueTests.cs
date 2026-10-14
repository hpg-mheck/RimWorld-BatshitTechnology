using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="ShowcaseCatalogue"/>: the Showcases menu lists each
/// mod with a showcase once, in name order, with its showcases in menu
/// order, and never lists a mod with none.
/// </summary>
public sealed class ShowcaseCatalogueTests
{
    /// <summary>Makes an entry for a mod.</summary>
    /// <param name="defName">The def name.</param>
    /// <param name="packageId">The mod's package id.</param>
    /// <param name="modName">The mod's name.</param>
    /// <param name="label">The label; null means the def name.</param>
    /// <param name="order">The sort order.</param>
    /// <returns>The entry.</returns>
    private static ShowcaseEntry Entry(
        string defName, string packageId, string modName,
        string? label = null, int order = 0) =>
        new ShowcaseEntry(defName, label, packageId, modName, order);

    /// <summary>Showcases from one mod share one group.</summary>
    [Fact(DisplayName = "Showcases group by their mod")]
    public void GroupByMod_should_put_a_mods_showcases_together()
    {
        var groups = ShowcaseCatalogue.GroupByMod(new[]
        {
            Entry("A1", "author.alpha", "Alpha"),
            Entry("B1", "author.beta", "Beta"),
            Entry("A2", "author.alpha", "Alpha"),
        });

        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { "A1", "A2" },
            groups[0].Showcases.Select(s => s.DefName));
        Assert.Equal(2, groups[0].Count);
        Assert.Equal(1, groups[1].Count);
    }

    /// <summary>
    /// Package ids differing only in case or the Workshop postfix are one
    /// mod.
    /// </summary>
    [Fact(DisplayName = "Package ids group without regard to case or postfix")]
    public void GroupByMod_should_ignore_case_and_steam_postfix()
    {
        var groups = ShowcaseCatalogue.GroupByMod(new[]
        {
            Entry("A1", "Breakpoint8088.BatshitTechnology", "Batshit Technology"),
            Entry("A2", "breakpoint8088.batshittechnology_steam", "Batshit Technology"),
        });

        ShowcaseModGroup group = Assert.Single(groups);
        Assert.Equal("breakpoint8088.batshittechnology", group.PackageId);
        Assert.Equal(2, group.Count);
    }

    /// <summary>
    /// Groups sort by name ignoring case; ties by package id.
    /// </summary>
    [Fact(DisplayName = "Mods are listed by name")]
    public void GroupByMod_should_sort_mods_by_name()
    {
        var groups = ShowcaseCatalogue.GroupByMod(new[]
        {
            Entry("Z", "z.zebra", "zebra works"),
            Entry("A", "a.apple", "Apple Works"),
            Entry("M", "m.second", "Mango"),
            Entry("N", "m.first", "Mango"),
        });

        Assert.Equal(
            new[] { "a.apple", "m.first", "m.second", "z.zebra" },
            groups.Select(g => g.PackageId));
    }

    /// <summary>
    /// Within a mod, showcases sort by order, then label (ignoring case),
    /// then def name.
    /// </summary>
    [Fact(DisplayName = "A mod's showcases sort by order, then label")]
    public void GroupByMod_should_sort_showcases_by_order_then_label()
    {
        var groups = ShowcaseCatalogue.GroupByMod(new[]
        {
            Entry("Late", "a.mod", "Mod", "aardvark", order: 5),
            Entry("Beta", "a.mod", "Mod", "beta"),
            Entry("Alpha", "a.mod", "Mod", "Alpha"),
            Entry("Alpha2", "a.mod", "Mod", "alpha"),
        });

        Assert.Equal(
            new[] { "Alpha", "Alpha2", "Beta", "Late" },
            Assert.Single(groups).Showcases.Select(s => s.DefName));
    }

    /// <summary>
    /// A mod with no showcase is never listed, because groups come from
    /// showcases; an empty or null input gives no groups.
    /// </summary>
    [Fact(DisplayName = "A mod with no showcase is not listed")]
    public void GroupByMod_should_list_nothing_for_no_showcases()
    {
        Assert.Empty(ShowcaseCatalogue.GroupByMod(null));
        Assert.Empty(ShowcaseCatalogue.GroupByMod(Array.Empty<ShowcaseEntry>()));
        Assert.Empty(ShowcaseCatalogue.GroupByMod(new ShowcaseEntry?[] { null }));
    }

    /// <summary>
    /// A group's name is its first non-blank mod name, else its package id,
    /// else "Unknown mod" (a def made in code has no mod).
    /// </summary>
    [Fact(DisplayName = "Group names fall back to the package id, then Unknown mod")]
    public void GroupByMod_should_fall_back_for_names()
    {
        var groups = ShowcaseCatalogue.GroupByMod(new[]
        {
            Entry("A", "a.nameless", "  "),
            Entry("B", "a.named", ""),
            Entry("C", "a.named", "Named Mod"),
            Entry("D", "", ""),
        });

        Assert.Equal(
            "a.nameless",
            ShowcaseCatalogue.FindGroup(groups, "a.nameless")!.Name);
        Assert.Equal(
            "Named Mod", ShowcaseCatalogue.FindGroup(groups, "A.NAMED")!.Name);
        Assert.Equal(ShowcaseCatalogue.UnknownModName,
            ShowcaseCatalogue.FindGroup(groups, null)!.Name);
        Assert.Null(ShowcaseCatalogue.FindGroup(groups, "no.such"));
    }

    /// <summary>A repeated def name keeps only its first entry.</summary>
    [Fact(DisplayName = "A repeated def name is listed once")]
    public void GroupByMod_should_drop_repeated_def_names()
    {
        var groups = ShowcaseCatalogue.GroupByMod(new[]
        {
            Entry("Same", "a.mod", "Mod", "first"),
            Entry("Same", "b.mod", "Other", "second"),
        });

        ShowcaseModGroup group = Assert.Single(groups);
        Assert.Equal("first", Assert.Single(group.Showcases).Label);
    }

    /// <summary>
    /// Entries normalize what they are given: a blank label shows the def
    /// name, requirement ids are distinct and lowercase, and blank flags
    /// are dropped. A blank def name is refused.
    /// </summary>
    [Fact(DisplayName = "Entries normalize labels, requirements and flags")]
    public void Entry_should_normalize_its_inputs()
    {
        var entry = new ShowcaseEntry(
            " BT_Demo ", " ", "A.Mod", "Mod", 0,
            new[] { "Ludeon.RimWorld.Biotech", "ludeon.rimworld.biotech", " ", null },
            new CompatibilityFlag?[]
            {
                new CompatibilityFlag("Other.Mod", ShowcaseSeverity.Warning, " slow "),
                new CompatibilityFlag(" ", ShowcaseSeverity.Fatal, "no id"),
                null,
            });

        Assert.Equal("BT_Demo", entry.DefName);
        Assert.Equal("BT_Demo", entry.Label);
        Assert.Equal(new[] { "ludeon.rimworld.biotech" }, entry.RequiredMods);
        CompatibilityFlag flag = Assert.Single(entry.Compatibility);
        Assert.Equal("other.mod", flag.PackageId);
        Assert.Equal("slow", flag.Note);
        Assert.Throws<ArgumentException>(() => new ShowcaseEntry(" ", "x", "a.b", "m"));
    }
}
