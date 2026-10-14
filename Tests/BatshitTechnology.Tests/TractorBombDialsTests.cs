using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the words of the Tractor Bomb's named settings in
/// <see cref="TractorBombDials"/> and the settings record
/// <see cref="TractorBombSettings"/> (docs/TRACTOR-BOMB.md, "Named
/// Settings"): the operator's names, the menu entries with their sizes,
/// the inspect pane's summary, and changing one setting at a time.
/// </summary>
public sealed class TractorBombDialsTests
{
    /// <summary>
    /// The operator's names (2026-10-14), smallest first, for each of the
    /// three settings, with their titles.
    /// </summary>
    [Fact(DisplayName = "Each setting has the operator's three names")]
    public void Names_should_match_the_spec()
    {
        Assert.Equal(new[] { "Timid", "Prudent", "Nosy" },
            Names(TractorBombDial.Proximity));
        Assert.Equal(new[] { "Intimate", "Neighborly", "Popular" },
            Names(TractorBombDial.Pull));
        Assert.Equal(new[] { "Birthday Cake", "Firework", "Block Party" },
            Names(TractorBombDial.Blast));
        Assert.Equal(new[] { "Proximity", "Pull", "Blast" },
            TractorBombDials.All.Select(TractorBombDials.Title));
        Assert.Equal(1, TractorBombDials.DefaultStop);
    }

    /// <summary>
    /// The menu entries give each stop's size: the radius for a pull or
    /// a blast, and how far a robot stops for a proximity, worked out with
    /// the pull in effect.
    /// </summary>
    [Fact(DisplayName = "Menu entries name each stop with its size")]
    public void MenuLabel_should_name_and_size_each_stop()
    {
        Assert.Equal(
            new[] { "Intimate, 6 tiles", "Neighborly, 15 tiles", "Popular, 22.5 tiles" },
            Menu(TractorBombDial.Pull, 15));
        Assert.Equal(
            new[] { "Birthday Cake, 3 tiles", "Firework, 7.5 tiles", "Block Party, 12 tiles" },
            Menu(TractorBombDial.Blast, 15));
        Assert.Equal(
            new[]
            {
                "Timid, stops 15 tiles out", "Prudent, stops 14 tiles out",
                "Nosy, stops 3 tiles out",
            },
            Menu(TractorBombDial.Proximity, 15));
        Assert.Equal("Prudent, stops 5 tiles out",
            TractorBombDials.MenuLabel(TractorBombDial.Proximity, 1, 6, 15));
        Assert.Equal("Prudent, stops 15 tiles out",
            TractorBombDials.MenuLabel(TractorBombDial.Proximity, 1, 22.5, 15));
    }

    /// <summary>
    /// The inspect pane names the settings in order, without the
    /// proximity for a planted bomb.
    /// </summary>
    [Fact(DisplayName = "The pane's summary names the settings")]
    public void Summary_should_name_the_settings()
    {
        var tour = new TractorBombSettings(
            TractorBombProximity.Prudent, TractorBombPull.Popular,
            TractorBombBlast.BlockParty);
        Assert.Equal("Prudent, Popular, Block Party",
            TractorBombDials.Summary(tour, withProximity: true));
        Assert.Equal("Popular, Block Party",
            TractorBombDials.Summary(tour, withProximity: false));
        Assert.Equal("Prudent, Neighborly, Firework",
            TractorBombDials.Summary(TractorBombSettings.Default, true));
    }

    /// <summary>
    /// Distances show a decimal only when they have one, and one tile is
    /// singular.
    /// </summary>
    /// <param name="tiles">The distance.</param>
    /// <param name="expected">The text.</param>
    [Theory(DisplayName = "Tile counts read naturally")]
    [InlineData(22.5, "22.5 tiles")]
    [InlineData(15.0, "15 tiles")]
    [InlineData(7.5, "7.5 tiles")]
    [InlineData(1.0, "1 tile")]
    [InlineData(14.0000001, "14 tiles")]
    [InlineData(0.0, "0 tiles")]
    [InlineData(double.NaN, "? tiles")]
    public void TilesText_should_read_naturally(double tiles, string expected)
    {
        Assert.Equal(expected, TractorBombDials.TilesText(tiles));
    }

    /// <summary>
    /// A stop outside 0 to 2 counts as the default, the middle, in names,
    /// sizes and changes.
    /// </summary>
    [Fact(DisplayName = "Unknown stops count as the middle")]
    public void Sanitize_should_default_unknown_stops()
    {
        Assert.Equal(1, TractorBombDials.Sanitize(-1));
        Assert.Equal(1, TractorBombDials.Sanitize(3));
        Assert.Equal(2, TractorBombDials.Sanitize(2));
        Assert.Equal("Neighborly", TractorBombDials.Name(TractorBombDial.Pull, 9));
        Assert.Equal(15.0, TractorBombDials.Tiles(TractorBombDial.Pull, 9, 15, 15));
        Assert.Equal(TractorBombBlast.Firework,
            TractorBombSettings.Default
                .With(TractorBombDial.Blast, 0)
                .With(TractorBombDial.Blast, 5).Blast);
    }

    /// <summary>
    /// Changing one setting leaves the other two alone, and each
    /// setting's stop reads back as set.
    /// </summary>
    [Fact(DisplayName = "One setting changes at a time")]
    public void With_should_change_one_setting()
    {
        TractorBombSettings s = TractorBombSettings.Default
            .With(TractorBombDial.Proximity, 2)
            .With(TractorBombDial.Pull, 2)
            .With(TractorBombDial.Blast, 0);
        Assert.Equal(new TractorBombSettings(
            TractorBombProximity.Nosy, TractorBombPull.Popular,
            TractorBombBlast.BirthdayCake), s);
        Assert.Equal(2, s.Stop(TractorBombDial.Proximity));
        Assert.Equal(2, s.Stop(TractorBombDial.Pull));
        Assert.Equal(0, s.Stop(TractorBombDial.Blast));
        Assert.Equal(TractorBombDials.DefaultStop, s.Stop((TractorBombDial)8));
        Assert.Equal(s, s.With((TractorBombDial)8, 0));
    }

    /// <summary>A setting's three names, smallest first.</summary>
    /// <param name="d">The setting.</param>
    /// <returns>The names.</returns>
    private static string[] Names(TractorBombDial d) => Enumerable
        .Range(0, TractorBombDials.StopCount)
        .Select(s => TractorBombDials.Name(d, s)).ToArray();

    /// <summary>
    /// A setting's three menu entries, smallest first, for a robot with a
    /// 15-tile emplacing range.
    /// </summary>
    /// <param name="d">The setting.</param>
    /// <param name="pull">The tractor beam in effect, in tiles.</param>
    /// <returns>The entries.</returns>
    private static string[] Menu(TractorBombDial d, double pull) => Enumerable
        .Range(0, TractorBombDials.StopCount)
        .Select(s => TractorBombDials.MenuLabel(d, s, pull, 15)).ToArray();

    /// <summary>
    /// Settings read from a save edited by hand come out defined: each
    /// unknown value becomes its default.
    /// </summary>
    [Fact(DisplayName = "Sanitized settings are all defined")]
    public void Sanitized_should_default_each_unknown_value()
    {
        var odd = new TractorBombSettings(
            (TractorBombProximity)5, (TractorBombPull)(-1),
            TractorBombBlast.BlockParty);
        Assert.Equal(new TractorBombSettings(
            TractorBombProximity.Prudent, TractorBombPull.Neighborly,
            TractorBombBlast.BlockParty), odd.Sanitized());
        Assert.Equal(TractorBombSettings.Default,
            new TractorBombSettings(
                TractorBombProximity.Prudent, TractorBombPull.Neighborly,
                TractorBombBlast.Firework));
    }
}
