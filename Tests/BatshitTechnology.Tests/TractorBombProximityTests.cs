using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies a Tractor Bomb's proximity in <see cref="TractorBombApproach"/>
/// (docs/TRACTOR-BOMB.md, "Named Settings"; it replaced Disposition): each
/// setting's stop at the named pulls, the order of the stops, sanitising,
/// and how an older save's Disposition loads.
/// </summary>
public sealed class TractorBombProximityTests
{
    /// <summary>
    /// At the default pull (Neighborly, 22.5 tiles since the pulls were
    /// raised by half) Timid stops 15 tiles out, Prudent at 15 too (one
    /// inside its beam would be 21.5, beyond the emplacing range) and Nosy
    /// at 3.
    /// </summary>
    /// <param name="proximity">The proximity.</param>
    /// <param name="expected">Its stop, in tiles.</param>
    [Theory(DisplayName = "Each proximity stops at its distance")]
    [InlineData(TractorBombProximity.Timid, 15.0)]
    [InlineData(TractorBombProximity.Prudent, 15.0)]
    [InlineData(TractorBombProximity.Nosy, 3.0)]
    public void StopDistance_should_match_the_spec(
        TractorBombProximity proximity, double expected)
    {
        Assert.Equal(expected, TractorBombApproach.StopDistance(
            proximity, TractorBombApproach.DefaultTimidStop,
            TractorBombYield.DefaultPullRadius));
    }

    /// <summary>
    /// The pull moves the Prudent stop: one inside Intimate's 9 tiles is
    /// 8; Neighborly's would be 21.5 and Popular's 32.75, so the 15-tile
    /// emplacing range wins at both. Timid never moves; Nosy is 3
    /// throughout.
    /// </summary>
    /// <param name="pull">The pull.</param>
    /// <param name="timid">The Timid stop.</param>
    /// <param name="prudent">The Prudent stop.</param>
    /// <param name="nosy">The Nosy stop.</param>
    [Theory(DisplayName = "The pull moves the Prudent stop")]
    [InlineData(TractorBombPull.Intimate, 15.0, 8.0, 3.0)]
    [InlineData(TractorBombPull.Neighborly, 15.0, 15.0, 3.0)]
    [InlineData(TractorBombPull.Popular, 15.0, 15.0, 3.0)]
    public void StopDistance_should_follow_the_pull(
        TractorBombPull pull, double timid, double prudent, double nosy)
    {
        double radius = TractorBombYield.Radius(pull);
        Assert.Equal(timid, TractorBombApproach.StopDistance(
            TractorBombProximity.Timid, 15, radius));
        Assert.Equal(prudent, TractorBombApproach.StopDistance(
            TractorBombProximity.Prudent, 15, radius));
        Assert.Equal(nosy, TractorBombApproach.StopDistance(
            TractorBombProximity.Nosy, 15, radius));
    }

    /// <summary>
    /// At every pull, Nosy stops no farther than Prudent, Prudent no
    /// farther than Timid, and Prudent and Nosy inside the beam, so a
    /// target standing still is caught.
    /// </summary>
    [Fact(DisplayName = "Nosy <= Prudent <= Timid, inside the pull")]
    public void StopDistance_should_be_ordered()
    {
        foreach (TractorBombPull pull in Enum.GetValues(typeof(TractorBombPull)))
        {
            double r = TractorBombYield.Radius(pull);
            double t = TractorBombApproach.StopDistance(
                TractorBombProximity.Timid, 15, r);
            double p = TractorBombApproach.StopDistance(
                TractorBombProximity.Prudent, 15, r);
            double n = TractorBombApproach.StopDistance(
                TractorBombProximity.Nosy, 15, r);
            Assert.True(n <= p && p <= t, $"{pull}: {n} {p} {t}");
            Assert.True(p < r && n < r && n > 0, $"{pull}");
        }
    }

    /// <summary>
    /// A nonsense emplacing range falls back to 15; a nonsense pull is
    /// clamped to the stops' range (9 to 33.75 tiles), or the default
    /// (22.5) if not a number.
    /// </summary>
    /// <param name="timidStop">The emplacing range given.</param>
    /// <param name="pull">The pull radius given.</param>
    /// <param name="expectedTimid">The Timid stop.</param>
    /// <param name="expectedPrudent">The Prudent stop.</param>
    [Theory(DisplayName = "Nonsense stops and pulls fall back to the spec")]
    [InlineData(12.0, 15.0, 12.0, 12.0)]
    [InlineData(6.0, 15.0, 6.0, 6.0)]
    [InlineData(0.0, 15.0, 15.0, 14.0)]
    [InlineData(-3.0, 15.0, 15.0, 14.0)]
    [InlineData(double.NaN, 15.0, 15.0, 14.0)]
    [InlineData(15.0, double.NaN, 15.0, 15.0)]
    [InlineData(15.0, 500.0, 15.0, 15.0)]
    [InlineData(15.0, -2.0, 15.0, 8.0)]
    public void StopDistance_should_sanitize_inputs(
        double timidStop, double pull, double expectedTimid,
        double expectedPrudent)
    {
        Assert.Equal(expectedTimid, TractorBombApproach.StopDistance(
            TractorBombProximity.Timid, timidStop, pull));
        Assert.Equal(expectedPrudent, TractorBombApproach.StopDistance(
            TractorBombProximity.Prudent, timidStop, pull));
    }

    /// <summary>
    /// Prudent is the default, an unknown value from a save becomes it,
    /// and the stops keep Disposition's numbers (Timid 0, Prudent 1,
    /// Nosy 2), smallest first, which the buttons' icons follow.
    /// </summary>
    [Fact(DisplayName = "Prudent by default; unknown values become it")]
    public void Sanitize_should_default_unknown_values()
    {
        Assert.Equal(TractorBombProximity.Prudent, TractorBombApproach.Default);
        var unknown = (TractorBombProximity)7;
        Assert.Equal(TractorBombProximity.Prudent,
            TractorBombApproach.Sanitize(unknown));
        Assert.Equal(14.0, TractorBombApproach.StopDistance(unknown, 15, 15));
        Assert.Equal(TractorBombProximity.Nosy,
            TractorBombApproach.Sanitize(TractorBombProximity.Nosy));
        Assert.Equal(new[] { 0, 1, 2 },
            Enum.GetValues(typeof(TractorBombProximity))
                .Cast<TractorBombProximity>().Select(v => (int)v).ToArray());
        Assert.Equal(0, (int)TractorBombProximity.Timid);
        Assert.Equal(2, (int)TractorBombProximity.Nosy);
    }

    /// <summary>
    /// An older save's Disposition loads as the proximity that replaced
    /// it, by name (any case, with spaces) or by its old number; anything
    /// else, or nothing, gives null so the caller keeps what it has.
    /// </summary>
    /// <param name="saved">What the old save holds.</param>
    /// <param name="expected">The proximity, or null.</param>
    [Theory(DisplayName = "Cautious, Moderate and Nosy load as Timid, Prudent and Nosy")]
    [InlineData("Cautious", TractorBombProximity.Timid)]
    [InlineData("Moderate", TractorBombProximity.Prudent)]
    [InlineData("Nosy", TractorBombProximity.Nosy)]
    [InlineData(" moderate ", TractorBombProximity.Prudent)]
    [InlineData("NOSY", TractorBombProximity.Nosy)]
    [InlineData("0", TractorBombProximity.Timid)]
    [InlineData("1", TractorBombProximity.Prudent)]
    [InlineData("2", TractorBombProximity.Nosy)]
    [InlineData("Reckless", null)]
    [InlineData("3", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void FromDisposition_should_map_old_names(
        string? saved, TractorBombProximity? expected)
    {
        Assert.Equal(expected, TractorBombApproach.FromDisposition(saved));
    }
}
