using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the self-destruct warning pulse in <see cref="CountdownPulse"/>.
/// </summary>
public sealed class CountdownPulseTests
{
    /// <summary>
    /// The final phase starts exactly at the warning threshold, not before.
    /// </summary>
    [Theory(DisplayName = "Final phase starts at the warning threshold")]
    [InlineData(10, false)]
    [InlineData(3.01, false)]
    [InlineData(3, true)]
    [InlineData(0.5, true)]
    public void At_should_switch_to_final_at_threshold(double secondsLeft, bool final)
    {
        Assert.Equal(final, CountdownPulse.At(secondsLeft, 3, 1, 0.25, 0).Final);
    }

    /// <summary>The fade is a sine: brightest a quarter period in, faintest at three quarters.</summary>
    [Fact(DisplayName = "Pulse fades sinusoidally between the floor and full")]
    public void At_should_fade_sinusoidally()
    {
        Assert.Equal(1d, CountdownPulse.At(10, 3, 1, 0.25, 0.25, 0.2).Alpha, 10);
        Assert.Equal(0.2, CountdownPulse.At(10, 3, 1, 0.25, 0.75, 0.2).Alpha, 10);
        Assert.Equal(0.6, CountdownPulse.At(10, 3, 1, 0.25, 0, 0.2).Alpha, 10);
    }

    /// <summary>
    /// In the final phase the pulse cycles faster: over one slow period it
    /// peaks four times instead of once.
    /// </summary>
    [Fact(DisplayName = "Final phase cycles faster")]
    public void At_should_cycle_faster_in_final_phase()
    {
        int Peaks(double secondsLeft)
        {
            double[] alpha = Enumerable.Range(0, 1000)
                .Select(i => CountdownPulse.At(secondsLeft, 3, 1, 0.25, i / 1000d).Alpha)
                .ToArray();
            return Enumerable.Range(1, alpha.Length - 1)
                .Count(i => alpha[i - 1] < 0.9 && alpha[i] >= 0.9); // rising past 0.9
        }

        Assert.Equal(1, Peaks(9));
        Assert.Equal(4, Peaks(1));
    }

    /// <summary>Opacity always stays within the floor and 1.</summary>
    [Fact(DisplayName = "Pulse opacity stays in range")]
    public void At_should_stay_in_range()
    {
        for (int i = 0; i < 500; i++)
        {
            double alpha = CountdownPulse.At(i % 7, 3, 1, 0.25, i * 0.0137).Alpha;
            Assert.InRange(alpha, 0.15, 1);
        }
    }

    /// <summary>A non-positive period is a configuration error.</summary>
    [Fact(DisplayName = "Pulse rejects non-positive periods")]
    public void At_should_reject_bad_periods()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CountdownPulse.At(5, 3, 0, 0.25, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CountdownPulse.At(5, 3, 1, -1, 0));
    }

    /// <summary>Quantizing keeps only a few distinct levels, and clamps.</summary>
    [Fact(DisplayName = "Quantize keeps a few clamped levels")]
    public void Quantize_should_round_to_levels()
    {
        Assert.Equal(0.5, CountdownPulse.Quantize(0.52, 10), 10);
        Assert.Equal(1d, CountdownPulse.Quantize(3, 10), 10);
        Assert.Equal(0d, CountdownPulse.Quantize(-1, 10), 10);
        int distinct = Enumerable.Range(0, 1000)
            .Select(i => CountdownPulse.Quantize(i / 1000d, 12))
            .Distinct()
            .Count();
        Assert.Equal(13, distinct);
    }
}
