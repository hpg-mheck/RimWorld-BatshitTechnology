using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Tractor Bomb's yield in <see cref="TractorBombYield"/>:
/// the named stops and their radii from docs/TRACTOR-BOMB.md ("Named
/// Settings"), what applies before Dial-A-Yield, how an older save's
/// dialled numbers find their stop, and the radius clamps the flee and
/// approach arithmetic rely on.
/// </summary>
public sealed class TractorBombYieldTests
{
    /// <summary>
    /// The operator's numbers (2026-10-14, with the middle and large
    /// settings raised by half): pull 6, 15 and 22.5 tiles; blast 3, 7.5
    /// and 12; the defaults in the middle.
    /// </summary>
    [Fact(DisplayName = "Named stops have the operator's radii")]
    public void Radii_should_match_the_spec()
    {
        Assert.Equal(6.0, TractorBombYield.Radius(TractorBombPull.Intimate));
        Assert.Equal(15.0, TractorBombYield.Radius(TractorBombPull.Neighborly));
        Assert.Equal(22.5, TractorBombYield.Radius(TractorBombPull.Popular));
        Assert.Equal(3.0, TractorBombYield.Radius(TractorBombBlast.BirthdayCake));
        Assert.Equal(7.5, TractorBombYield.Radius(TractorBombBlast.Firework));
        Assert.Equal(12.0, TractorBombYield.Radius(TractorBombBlast.BlockParty));
        Assert.Equal(TractorBombPull.Neighborly, TractorBombYield.DefaultPull);
        Assert.Equal(TractorBombBlast.Firework, TractorBombYield.DefaultBlast);
        Assert.Equal(15.0, TractorBombYield.DefaultPullRadius);
        Assert.Equal(7.5, TractorBombYield.DefaultBlastRadius);
        Assert.Equal(6.0, TractorBombYield.MinPull);
        Assert.Equal(22.5, TractorBombYield.MaxPull);
        Assert.Equal(3.0, TractorBombYield.MinBlast);
        Assert.Equal(12.0, TractorBombYield.MaxBlast);
    }

    /// <summary>
    /// The stops are saved by name and numbered 0 to 2 from the smallest,
    /// so the buttons and their icons index them in that order.
    /// </summary>
    [Fact(DisplayName = "Stops are numbered smallest first")]
    public void Stops_should_be_numbered_smallest_first()
    {
        Assert.Equal(new[] { 0, 1, 2 },
            Enum.GetValues(typeof(TractorBombPull)).Cast<TractorBombPull>()
                .Select(v => (int)v).ToArray());
        Assert.Equal(new[] { 0, 1, 2 },
            Enum.GetValues(typeof(TractorBombBlast)).Cast<TractorBombBlast>()
                .Select(v => (int)v).ToArray());
        double[] pulls = Enum.GetValues(typeof(TractorBombPull))
            .Cast<TractorBombPull>().Select(TractorBombYield.Radius).ToArray();
        double[] blasts = Enum.GetValues(typeof(TractorBombBlast))
            .Cast<TractorBombBlast>().Select(TractorBombYield.Radius).ToArray();
        Assert.Equal(pulls.OrderBy(r => r), pulls);
        Assert.Equal(blasts.OrderBy(r => r), blasts);
    }

    /// <summary>
    /// Before Dial-A-Yield is researched a bomb uses the defaults,
    /// whatever was set; afterwards it uses the stops set.
    /// </summary>
    [Fact(DisplayName = "Set stops apply only once researched")]
    public void Effective_should_ignore_settings_until_researched()
    {
        Assert.Equal((TractorBombPull.Neighborly, TractorBombBlast.Firework),
            TractorBombYield.Effective(
                TractorBombPull.Popular, TractorBombBlast.BirthdayCake, false));
        Assert.Equal((TractorBombPull.Popular, TractorBombBlast.BirthdayCake),
            TractorBombYield.Effective(
                TractorBombPull.Popular, TractorBombBlast.BirthdayCake, true));
        Assert.Equal((TractorBombPull.Neighborly, TractorBombBlast.Firework),
            TractorBombYield.Effective(
                (TractorBombPull)9, (TractorBombBlast)(-1), true));
    }

    /// <summary>
    /// A stop no setting has (a save edited by hand, a later version's)
    /// becomes the default, and so does its radius.
    /// </summary>
    [Fact(DisplayName = "Unknown stops become the defaults")]
    public void Sanitize_should_default_unknown_stops()
    {
        Assert.Equal(TractorBombPull.Neighborly,
            TractorBombYield.Sanitize((TractorBombPull)7));
        Assert.Equal(TractorBombBlast.Firework,
            TractorBombYield.Sanitize((TractorBombBlast)(-2)));
        Assert.Equal(15.0, TractorBombYield.Radius((TractorBombPull)7));
        Assert.Equal(7.5, TractorBombYield.Radius((TractorBombBlast)3));
        Assert.Equal(TractorBombPull.Popular,
            TractorBombYield.Sanitize(TractorBombPull.Popular));
    }

    /// <summary>
    /// An older save's dialled tractor beam (3 to 20 whole tiles) snaps to
    /// the nearest stop: 3 to 10 Intimate, 11 to 18 Neighborly, 19 and 20
    /// Popular. The midpoints (10.5 and 18.75) go to the larger stop;
    /// nonsense goes to an end, and NaN to the default.
    /// </summary>
    /// <param name="tiles">The radius dialled.</param>
    /// <param name="expected">The stop it loads as.</param>
    [Theory(DisplayName = "An old dialled pull snaps to the nearest stop")]
    [InlineData(3, TractorBombPull.Intimate)]
    [InlineData(10, TractorBombPull.Intimate)]
    [InlineData(10.49, TractorBombPull.Intimate)]
    [InlineData(10.5, TractorBombPull.Neighborly)]
    [InlineData(11, TractorBombPull.Neighborly)]
    [InlineData(18, TractorBombPull.Neighborly)]
    [InlineData(18.75, TractorBombPull.Popular)]
    [InlineData(19, TractorBombPull.Popular)]
    [InlineData(20, TractorBombPull.Popular)]
    [InlineData(-4, TractorBombPull.Intimate)]
    [InlineData(1e9, TractorBombPull.Popular)]
    [InlineData(double.NegativeInfinity, TractorBombPull.Intimate)]
    [InlineData(double.PositiveInfinity, TractorBombPull.Popular)]
    [InlineData(double.NaN, TractorBombPull.Neighborly)]
    public void NearestPull_should_snap(double tiles, TractorBombPull expected)
    {
        Assert.Equal(expected, TractorBombYield.NearestPull(tiles));
    }

    /// <summary>
    /// An older save's dialled blast (2 to 10 whole tiles) snaps to the
    /// nearest stop: 2 to 5 Birthday Cake, 6 to 9 Firework, 10 Block
    /// Party; midpoints go to the larger stop, NaN to the default.
    /// </summary>
    /// <param name="tiles">The radius dialled.</param>
    /// <param name="expected">The stop it loads as.</param>
    [Theory(DisplayName = "An old dialled blast snaps to the nearest stop")]
    [InlineData(2, TractorBombBlast.BirthdayCake)]
    [InlineData(5, TractorBombBlast.BirthdayCake)]
    [InlineData(5.25, TractorBombBlast.Firework)]
    [InlineData(6, TractorBombBlast.Firework)]
    [InlineData(9, TractorBombBlast.Firework)]
    [InlineData(9.75, TractorBombBlast.BlockParty)]
    [InlineData(10, TractorBombBlast.BlockParty)]
    [InlineData(-1, TractorBombBlast.BirthdayCake)]
    [InlineData(double.NaN, TractorBombBlast.Firework)]
    public void NearestBlast_should_snap(double tiles, TractorBombBlast expected)
    {
        Assert.Equal(expected, TractorBombYield.NearestBlast(tiles));
    }

    /// <summary>
    /// Each stop's own radius loads as itself, so a radius read back from
    /// anywhere names its stop.
    /// </summary>
    [Fact(DisplayName = "Each stop's radius snaps to itself")]
    public void Nearest_should_round_trip_every_stop()
    {
        foreach (TractorBombPull p in Enum.GetValues(typeof(TractorBombPull)))
        {
            Assert.Equal(p, TractorBombYield.NearestPull(TractorBombYield.Radius(p)));
        }

        foreach (TractorBombBlast b in Enum.GetValues(typeof(TractorBombBlast)))
        {
            Assert.Equal(b, TractorBombYield.NearestBlast(TractorBombYield.Radius(b)));
        }
    }

    /// <summary>
    /// The radius clamps keep fractions, hold to the stops' range and
    /// turn NaN into the default radius.
    /// </summary>
    /// <param name="asked">The radius asked for.</param>
    /// <param name="pull">The clamped pull.</param>
    /// <param name="blast">The clamped blast.</param>
    [Theory(DisplayName = "Radius clamps keep fractions within range")]
    [InlineData(-5, 6, 3)]
    [InlineData(2.9, 6, 3)]
    [InlineData(4.9, 6, 4.9)]
    [InlineData(7.5, 7.5, 7.5)]
    [InlineData(22.5, 22.5, 12)]
    [InlineData(1e9, 22.5, 12)]
    [InlineData(double.PositiveInfinity, 22.5, 12)]
    [InlineData(double.NegativeInfinity, 6, 3)]
    [InlineData(double.NaN, 15, 7.5)]
    public void Clamps_should_keep_fractions_in_range(
        double asked, double pull, double blast)
    {
        Assert.Equal(pull, TractorBombYield.ClampPull(asked));
        Assert.Equal(blast, TractorBombYield.ClampBlast(asked));
    }
}
