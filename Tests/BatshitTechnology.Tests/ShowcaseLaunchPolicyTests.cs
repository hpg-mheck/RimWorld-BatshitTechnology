using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Showcases menu's launch rules (docs/SHOWCASE-API.md,
/// "Other Mods"): requirements, compatibility severities, and the force
/// override for fatal incompatibilities.
/// </summary>
public sealed class ShowcaseLaunchPolicyTests
{
    /// <summary>The mods loaded in most of these tests.</summary>
    private static readonly string[] Loaded =
    {
        "ludeon.rimworld", "ludeon.rimworld.biotech", "brrainz.harmony",
        "some.author.slowmod", "some.author.crashmod_steam",
    };

    /// <summary>An entry with requirements and flags.</summary>
    /// <param name="required">Required package ids.</param>
    /// <param name="flags">Compatibility flags.</param>
    /// <returns>The entry.</returns>
    private static ShowcaseEntry Entry(
        IEnumerable<string>? required = null,
        IEnumerable<CompatibilityFlag>? flags = null) =>
        new ShowcaseEntry("BT_Test", "test", "a.mod", "Mod", 0, required, flags);

    /// <summary>A compatibility flag.</summary>
    /// <param name="id">The flagged mod's package id.</param>
    /// <param name="severity">How badly they get on.</param>
    /// <param name="note">The note.</param>
    /// <returns>The flag.</returns>
    private static CompatibilityFlag Flag(
        string id, ShowcaseSeverity severity, string note) =>
        new CompatibilityFlag(id, severity, note);

    /// <summary>Every requirement loaded means the check is met.</summary>
    [Fact(DisplayName = "Requirements are met when every required mod is loaded")]
    public void Requirements_should_be_met_when_all_loaded()
    {
        var check = RequirementCheck.Evaluate(
            new[] { "Ludeon.RimWorld.Biotech", "ludeon.rimworld" }, Loaded);

        Assert.True(check.Met);
        Assert.Empty(check.Missing);
    }

    /// <summary>Missing requirements are listed in declared order.</summary>
    [Fact(DisplayName = "Missing requirements are listed in order")]
    public void Requirements_should_list_missing_mods_in_order()
    {
        var check = RequirementCheck.Evaluate(
            new[]
            {
                "ludeon.rimworld.odyssey",
                "ludeon.rimworld",
                "Ludeon.RimWorld.Anomaly",
            },
            Loaded);

        Assert.False(check.Met);
        Assert.Equal(
            new[] { "ludeon.rimworld.odyssey", "ludeon.rimworld.anomaly" },
            check.Missing);
    }

    /// <summary>Nothing required, or nulls, are always met.</summary>
    [Fact(DisplayName = "No requirements are always met")]
    public void Requirements_should_tolerate_null_and_blank()
    {
        Assert.True(RequirementCheck.Evaluate(null, null).Met);
        Assert.True(RequirementCheck.Evaluate(new[] { " ", null }, null).Met);
        Assert.False(RequirementCheck.Evaluate(new[] { "a.b" }, null).Met);
    }

    /// <summary>
    /// Only flags for loaded mods match, worst first, and a mod flagged
    /// twice keeps its worst flag. The Workshop postfix is ignored.
    /// </summary>
    [Fact(DisplayName = "Compatibility matches only loaded mods, worst first")]
    public void Compatibility_should_match_loaded_mods_worst_first()
    {
        var matches = ShowcaseCompatibility.Matches(new[]
        {
            Flag("some.author.slowmod", ShowcaseSeverity.Concern, "slow"),
            Flag("not.loaded", ShowcaseSeverity.Fatal, "absent"),
            Flag("Some.Author.CrashMod", ShowcaseSeverity.Fatal, "crash"),
            Flag("some.author.slowmod", ShowcaseSeverity.Warning, "slower"),
        }, Loaded);

        Assert.Equal(
            new[] { ShowcaseSeverity.Fatal, ShowcaseSeverity.Warning },
            matches.Select(m => m.Severity));
        Assert.Equal("some.author.crashmod", matches[0].PackageId);
        Assert.Equal("slower", matches[1].Note);
    }

    /// <summary>Nothing flagged and nothing missing is clear.</summary>
    [Fact(DisplayName = "No flags and no missing mods: clear")]
    public void Decision_should_be_clear_without_flags()
    {
        var decision = LaunchDecision.Evaluate(Entry(), Loaded, force: false);

        Assert.Equal(LaunchVerdict.Clear, decision.Verdict);
        Assert.True(decision.CanStart);
        Assert.False(decision.CanForce);
    }

    /// <summary>
    /// A flag for a mod that is not loaded is no warning: the menu warns
    /// only about loaded mods the showcase flags.
    /// </summary>
    [Fact(DisplayName = "Flags for mods that are not loaded do not warn")]
    public void Decision_should_ignore_flags_for_absent_mods()
    {
        var decision = LaunchDecision.Evaluate(
            Entry(flags: new[]
            {
                Flag("not.loaded", ShowcaseSeverity.Fatal, "x"),
            }),
            Loaded, force: false);

        Assert.Equal(LaunchVerdict.Clear, decision.Verdict);
        Assert.Empty(decision.Matches);
    }

    /// <summary>
    /// A concern, a warning or a known incompatibility warns, and Start
    /// still works.
    /// </summary>
    /// <param name="severity">The flag's severity.</param>
    [Theory(DisplayName = "Non-fatal flags warn and still start")]
    [InlineData(ShowcaseSeverity.Concern)]
    [InlineData(ShowcaseSeverity.Warning)]
    [InlineData(ShowcaseSeverity.KnownIncompatibility)]
    public void Decision_should_warn_but_start_for_non_fatal(ShowcaseSeverity severity)
    {
        var decision = LaunchDecision.Evaluate(
            Entry(flags: new[]
            {
                Flag("some.author.slowmod", severity, "note"),
            }),
            Loaded, force: false);

        Assert.Equal(LaunchVerdict.Warned, decision.Verdict);
        Assert.True(decision.CanStart);
        Assert.False(decision.CanForce);
        Assert.Equal(severity, Assert.Single(decision.Matches).Severity);
    }

    /// <summary>
    /// A fatal flag refuses Start; forcing launches it anyway, with the
    /// match still reported.
    /// </summary>
    [Fact(DisplayName = "A fatal flag refuses; force overrides it")]
    public void Decision_should_refuse_fatal_unless_forced()
    {
        ShowcaseEntry entry = Entry(flags: new[]
        {
            Flag("some.author.slowmod", ShowcaseSeverity.Concern, "slow"),
            Flag("some.author.crashmod", ShowcaseSeverity.Fatal, "crash"),
        });

        var refused = LaunchDecision.Evaluate(entry, Loaded, force: false);
        var forced = LaunchDecision.Evaluate(entry, Loaded, force: true);

        Assert.Equal(LaunchVerdict.Refused, refused.Verdict);
        Assert.False(refused.CanStart);
        Assert.True(refused.CanForce);
        Assert.Equal("crash", Assert.Single(refused.FatalMatches).Note);
        Assert.Equal(LaunchVerdict.Forced, forced.Verdict);
        Assert.True(forced.CanStart);
        Assert.False(forced.CanForce);
        Assert.Equal(2, forced.Matches.Count);
    }

    /// <summary>
    /// A missing requirement makes a showcase unavailable, and forcing does
    /// not help, even with a fatal flag too.
    /// </summary>
    [Fact(DisplayName = "Missing requirements: unavailable, even when forced")]
    public void Decision_should_be_unavailable_when_requirements_missing()
    {
        ShowcaseEntry entry = Entry(
            required: new[] { "ludeon.rimworld.odyssey" },
            flags: new[]
            {
                Flag("some.author.crashmod", ShowcaseSeverity.Fatal, "crash"),
            });

        foreach (bool force in new[] { false, true })
        {
            var decision = LaunchDecision.Evaluate(entry, Loaded, force);
            Assert.Equal(LaunchVerdict.Unavailable, decision.Verdict);
            Assert.False(decision.CanStart);
            Assert.False(decision.CanForce);
            Assert.Equal("ludeon.rimworld.odyssey",
                Assert.Single(decision.Requirements.Missing));
        }
    }

    /// <summary>
    /// Decide tolerates nulls and orders matches worst first.
    /// </summary>
    [Fact(DisplayName = "Decide tolerates nulls")]
    public void Decide_should_tolerate_nulls()
    {
        Assert.Equal(
            LaunchVerdict.Clear,
            LaunchDecision.Decide(null, null, false).Verdict);
        var decision = LaunchDecision.Decide(
            RequirementCheck.AllMet,
            new CompatibilityFlag?[]
            {
                null,
                Flag("a.b", ShowcaseSeverity.Concern, ""),
                Flag("c.d", ShowcaseSeverity.KnownIncompatibility, ""),
            },
            force: false);

        Assert.Equal(LaunchVerdict.Warned, decision.Verdict);
        Assert.Equal("c.d", decision.Matches[0].PackageId);
        Assert.Throws<ArgumentNullException>(
            () => LaunchDecision.Evaluate(null!, Loaded, false));
    }

    /// <summary>
    /// Package ids follow RimWorld's own format rule, and normalization
    /// lowercases, trims and drops the Workshop postfix.
    /// </summary>
    [Fact(DisplayName = "Package ids normalize and validate like RimWorld's")]
    public void PackageIds_should_normalize_and_validate()
    {
        Assert.Equal("a.mod", PackageIds.Normalize(" A.Mod_steam "));
        Assert.Equal(string.Empty, PackageIds.Normalize(null));
        Assert.True(PackageIds.IsWellFormed("breakpoint8088.BatshitTechnology"));
        Assert.True(PackageIds.IsWellFormed("brrainz.harmony"));
        foreach (string bad in new[]
        {
            "nodot", ".lead", "trail.", "dou..ble", "hy-phen.mod", "under_score.mod",
            new string('a', 58) + ".bc", "",
        })
        {
            Assert.False(PackageIds.IsWellFormed(bad), bad);
        }

        Assert.False(PackageIds.IsWellFormed(null));
    }
}
