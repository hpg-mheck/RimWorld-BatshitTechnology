using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the shared implosion's settings in
/// <see cref="ImplosionSettings"/>: their defaults, the problems a Def
/// reports, and the normalization every implosion applies.
/// </summary>
public sealed class ImplosionSettingsTests
{
    /// <summary>
    /// RimWorld's largest radius in 1.6
    /// (<c>GenRadial.MaxRadialPatternRadius</c>), as a double that a float
    /// cannot hold exactly.
    /// </summary>
    private const double MaxRadius = 79.8;

    /// <summary>The mistakes a Def or a slider could make, by name.</summary>
    private static readonly Dictionary<string, Action<ImplosionSettings>>
        Mistakes = new()
        {
            ["no pull radius"] = s => s.pullRadius = 0,
            ["pull radius under a tile"] = s => s.pullRadius = 0.5f,
            ["pull radius beyond the engine"] = s => s.pullRadius = 200,
            ["pull radius not a number"] = s => s.pullRadius = float.NaN,
            ["negative gather radius"] = s => s.gatherRadius = -1,
            ["gather radius beyond the pull"] = s => s.gatherRadius = 11,
            ["gather radius not a number"] = s => s.gatherRadius = float.NaN,
            ["no pull time"] = s => s.pullTicks = 0,
            ["negative rip time"] = s => s.ripTicks = -1,
            ["negative start rate"] = s => s.pullRateStart = -0.1f,
            ["slowing pull"] = s => s.pullRateEnd = 0.1f,
            ["rate beyond a cell a tick"] = s => s.pullRateEnd = 61,
            ["infinite rate"] = s => s.pullRateEnd = float.PositiveInfinity,
            ["end rate not a number"] = s => s.pullRateEnd = float.NaN,
            ["no maximum duration"] = s => s.maxDurationTicks = 0,
            ["negative minimum duration"] = s => s.minDurationTicks = -1,
            ["minimum beyond the maximum"] = s => s.minDurationTicks = 301,
        };

    /// <summary>The names of <see cref="Mistakes"/>, for xUnit.</summary>
    public static IEnumerable<object[]> MistakeNames =>
        Mistakes.Keys.Select(k => new object[] { k });

    /// <summary>
    /// The defaults are the Ergonomic Singularity Grenade's settings, and
    /// sound: exactly five seconds, its minimum and maximum equal.
    /// </summary>
    [Fact(DisplayName = "Implosion defaults are the grenade's, and sound")]
    public void Defaults_should_be_the_grenade()
    {
        var s = new ImplosionSettings();
        Assert.Equal(10f, s.pullRadius);
        Assert.Equal(0f, s.gatherRadius);
        Assert.Equal(300, s.pullTicks);
        Assert.Equal(0.6f, s.pullRateStart);
        Assert.Equal(12f, s.pullRateEnd);
        Assert.True(s.ripFixtures);
        Assert.Equal(120, s.ripTicks);
        Assert.Equal(300, s.minDurationTicks);
        Assert.Equal(300, s.maxDurationTicks);
        Assert.Empty(s.Problems(MaxRadius));
    }

    /// <summary>
    /// The Tractor Bomb's settings (10 tiles in to 1.5, for two to eight
    /// seconds) are sound.
    /// </summary>
    [Fact(DisplayName = "Tractor Bomb's gather radius and durations are sound")]
    public void Tractor_bomb_settings_should_be_sound()
    {
        var s = new ImplosionSettings
        {
            gatherRadius = 1.5f,
            pullTicks = 600,
            minDurationTicks = TractorBombTiming.MinImplosionTicks,
            maxDurationTicks = TractorBombTiming.MaxImplosionTicks,
        };
        Assert.Empty(s.Problems(MaxRadius));
    }

    /// <summary>Each kind of mistake is reported.</summary>
    /// <param name="name">Which mistake.</param>
    [Theory(DisplayName = "Each mistake in implosion settings is reported")]
    [MemberData(nameof(MistakeNames))]
    public void Problems_should_report_each_mistake(string name)
    {
        var s = new ImplosionSettings();
        Mistakes[name](s);
        Assert.NotEmpty(s.Problems(MaxRadius));
    }

    /// <summary>
    /// Normalized settings never have a problem, however bad the input:
    /// every mistake, and every pair of mistakes, is brought within bounds.
    /// The input itself is left alone.
    /// </summary>
    [Fact(DisplayName = "Normalized implosion settings are always sound")]
    public void Normalized_should_always_be_sound()
    {
        foreach (string first in Mistakes.Keys)
        {
            foreach (string second in Mistakes.Keys)
            {
                var s = new ImplosionSettings();
                Mistakes[first](s);
                Mistakes[second](s);
                string before = Describe(s);
                Assert.Empty(s.Normalized(MaxRadius).Problems(MaxRadius));
                Assert.Equal(before, Describe(s));
            }
        }
    }

    /// <summary>
    /// Sound settings come through normalization unchanged.
    /// </summary>
    [Fact(DisplayName = "Sound implosion settings survive normalizing")]
    public void Normalized_should_keep_sound_settings()
    {
        var s = new ImplosionSettings
        {
            pullRadius = 20,
            gatherRadius = 1.5f,
            pullTicks = 900,
            pullRateStart = 0,
            pullRateEnd = 60,
            ripFixtures = false,
            ripTicks = 0,
            minDurationTicks = 0,
            maxDurationTicks = 1,
        };
        Assert.Equal(Describe(s), Describe(s.Normalized(MaxRadius)));
    }

    /// <summary>
    /// Out-of-range values are clamped to the nearest sound value; values
    /// that are not numbers fall back to the smallest sound value.
    /// </summary>
    [Fact(DisplayName = "Normalizing clamps implosion settings")]
    public void Normalized_should_clamp()
    {
        var s = new ImplosionSettings
        {
            pullRadius = 500,
            gatherRadius = 600,
            pullTicks = -3,
            pullRateStart = -1,
            pullRateEnd = 1e9f,
            ripTicks = -7,
            minDurationTicks = -5,
            maxDurationTicks = -9,
        }.Normalized(MaxRadius);
        Assert.Equal((float)MaxRadius, s.pullRadius);
        Assert.Equal(s.pullRadius, s.gatherRadius);
        Assert.Equal(1, s.pullTicks);
        Assert.Equal(0f, s.pullRateStart);
        Assert.Equal((float)ImplosionSettings.MaxRate, s.pullRateEnd);
        Assert.Equal(0, s.ripTicks);
        Assert.Equal(1, s.maxDurationTicks);
        Assert.Equal(0, s.minDurationTicks);

        var inverted = new ImplosionSettings
        {
            minDurationTicks = 900,
            maxDurationTicks = 480,
        }.Normalized(MaxRadius);
        Assert.Equal(480, inverted.minDurationTicks);
        Assert.Equal(480, inverted.maxDurationTicks);

        var nan = new ImplosionSettings
        {
            pullRadius = float.NaN,
            gatherRadius = float.NaN,
            pullRateStart = 3,
            pullRateEnd = float.NaN,
        }.Normalized(MaxRadius);
        Assert.Equal((float)ImplosionSettings.MinPullRadius, nan.pullRadius);
        Assert.Equal(0f, nan.gatherRadius);
        Assert.Equal(3f, nan.pullRateEnd);

        var slowing = new ImplosionSettings
        {
            pullRateStart = 9,
            pullRateEnd = 2,
        }.Normalized(MaxRadius);
        Assert.Equal(9f, slowing.pullRateEnd);
    }

    /// <summary>A copy is independent of its original.</summary>
    [Fact(DisplayName = "Implosion settings copies are independent")]
    public void Copy_should_be_independent()
    {
        var s = new ImplosionSettings();
        ImplosionSettings copy = s.Copy();
        copy.pullRadius = 3;
        copy.ripFixtures = false;
        Assert.Equal(10f, s.pullRadius);
        Assert.True(s.ripFixtures);
    }

    /// <summary>
    /// A nonsensical engine radius limit is a programming error.
    /// </summary>
    /// <param name="maxRadius">The limit.</param>
    [Theory(DisplayName = "Implosion settings reject a bad radius limit")]
    [InlineData(0.5)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Should_reject_a_bad_radius_limit(double maxRadius)
    {
        var s = new ImplosionSettings();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => s.Problems(maxRadius));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => s.Normalized(maxRadius));
    }

    /// <summary>Every field of some settings, for comparing them.</summary>
    /// <param name="s">The settings.</param>
    /// <returns>A string naming every field's value.</returns>
    private static string Describe(ImplosionSettings s) =>
        $"{s.pullRadius} {s.gatherRadius} {s.pullTicks} {s.pullRateStart} "
        + $"{s.pullRateEnd} {s.ripFixtures} {s.ripTicks} "
        + $"{s.minDurationTicks} {s.maxDurationTicks}";
}
