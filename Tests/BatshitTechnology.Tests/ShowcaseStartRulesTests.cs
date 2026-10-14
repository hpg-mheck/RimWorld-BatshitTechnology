using System;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies when Force start is offered and what a
/// <c>-showcase=&lt;defName&gt;</c> command line does
/// (docs/SHOWCASE-API.md, "Other Mods" and "The command line").
/// </summary>
public sealed class ShowcaseStartRulesTests
{
    /// <summary>The mods loaded in these tests.</summary>
    private static readonly string[] Loaded =
    {
        "ludeon.rimworld", "brrainz.harmony", "some.author.crashmod",
        "some.author.slowmod",
    };

    /// <summary>A showcase entry with optional requirements and flags.</summary>
    /// <param name="required">A required package id, or null.</param>
    /// <param name="flagged">A flagged package id, or null.</param>
    /// <param name="severity">The flag's severity.</param>
    /// <returns>The entry.</returns>
    private static ShowcaseEntry Entry(
        string? required = null,
        string? flagged = null,
        ShowcaseSeverity severity = ShowcaseSeverity.Warning) =>
        new ShowcaseEntry(
            "BT_Test",
            "test",
            "a.mod",
            "A Mod",
            0,
            required == null ? null : new[] { required },
            flagged == null
                ? null
                : new[] { new CompatibilityFlag(flagged, severity, "note") });

    /// <summary>Evaluates an entry against <see cref="Loaded"/>.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="force">Whether to force.</param>
    /// <returns>The decision.</returns>
    private static LaunchDecision Decide(ShowcaseEntry entry, bool force) =>
        LaunchDecision.Evaluate(entry, Loaded, force);

    /// <summary>Force start is a testing tool, hidden from players.</summary>
    /// <param name="devMode">Whether dev mode is on.</param>
    /// <param name="flag">Whether -showcaseforce was passed.</param>
    /// <param name="expected">Whether Force may be offered.</param>
    [Theory(DisplayName = "Force start is offered only in dev mode or with -showcaseforce")]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void MayOfferForce_should_need_dev_mode_or_flag(
        bool devMode, bool flag, bool expected)
    {
        Assert.Equal(expected, ShowcaseStartRules.MayOfferForce(devMode, flag));
    }

    /// <summary>
    /// The Force button appears only for a fatal flag, and only where
    /// forcing may be offered.
    /// </summary>
    [Fact(DisplayName = "Force button appears only for a refused showcase")]
    public void ShowForceButton_should_need_a_refusal()
    {
        LaunchDecision fatal = Decide(
            Entry(flagged: "some.author.crashmod",
                severity: ShowcaseSeverity.Fatal),
            force: false);
        LaunchDecision warned = Decide(
            Entry(flagged: "some.author.slowmod"), force: false);
        LaunchDecision missing = Decide(
            Entry(required: "ludeon.rimworld.odyssey"), force: false);

        Assert.True(ShowcaseStartRules.ShowForceButton(fatal, true, false));
        Assert.True(ShowcaseStartRules.ShowForceButton(fatal, false, true));
        Assert.False(ShowcaseStartRules.ShowForceButton(fatal, false, false));
        Assert.False(ShowcaseStartRules.ShowForceButton(warned, true, true));
        Assert.False(ShowcaseStartRules.ShowForceButton(missing, true, true));
        Assert.False(ShowcaseStartRules.ShowForceButton(null, true, true));
    }

    /// <summary>A blank request means no command-line showcase.</summary>
    /// <param name="requested">The requested def name.</param>
    [Theory(DisplayName = "No -showcase= request leaves the menu alone")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForCommandLine_should_ignore_blank_requests(string? requested)
    {
        Assert.Equal(
            CommandLineVerdict.None,
            ShowcaseStartRules.ForCommandLine(
                requested, Decide(Entry(), force: false)));
    }

    /// <summary>A name no showcase has is reported as unknown.</summary>
    [Fact(DisplayName = "An unknown -showcase= name is reported")]
    public void ForCommandLine_should_report_unknown_names()
    {
        Assert.Equal(
            CommandLineVerdict.UnknownShowcase,
            ShowcaseStartRules.ForCommandLine("BT_Nope", null));
    }

    /// <summary>
    /// Every launch verdict maps to the matching command-line verdict, and
    /// only the launching ones start a game.
    /// </summary>
    [Fact(DisplayName = "Command-line verdicts follow the launch policy")]
    public void ForCommandLine_should_follow_the_launch_policy()
    {
        ShowcaseEntry fatal = Entry(
            flagged: "some.author.crashmod", severity: ShowcaseSeverity.Fatal);
        var cases = new (LaunchDecision Decision, CommandLineVerdict Expected)[]
        {
            (Decide(Entry(), false), CommandLineVerdict.Launch),
            (Decide(Entry(flagged: "some.author.slowmod"), false),
                CommandLineVerdict.Launch),
            (Decide(fatal, false), CommandLineVerdict.Refused),
            (Decide(fatal, true), CommandLineVerdict.LaunchForced),
            (Decide(Entry(required: "ludeon.rimworld.odyssey"), true),
                CommandLineVerdict.Unavailable),
        };

        foreach (var (decision, expected) in cases)
        {
            CommandLineVerdict verdict =
                ShowcaseStartRules.ForCommandLine("BT_Test", decision);
            Assert.Equal(expected, verdict);
            Assert.Equal(
                expected == CommandLineVerdict.Launch
                    || expected == CommandLineVerdict.LaunchForced,
                ShowcaseStartRules.Starts(verdict));
        }
    }

    /// <summary>Only the two launch verdicts start a game.</summary>
    [Fact(DisplayName = "Only Launch and LaunchForced start a game")]
    public void Starts_should_be_true_only_for_launches()
    {
        foreach (CommandLineVerdict verdict in
            Enum.GetValues(typeof(CommandLineVerdict)))
        {
            bool expected = verdict == CommandLineVerdict.Launch
                || verdict == CommandLineVerdict.LaunchForced;
            Assert.Equal(expected, ShowcaseStartRules.Starts(verdict));
        }
    }

    /// <summary>The command-line keys are the documented ones.</summary>
    [Fact(DisplayName = "Command-line keys are showcase and showcaseforce")]
    public void Keys_should_match_the_documentation()
    {
        Assert.Equal("showcase", ShowcaseStartRules.ShowcaseArg);
        Assert.Equal("showcaseforce", ShowcaseStartRules.ForceArg);
    }
}
