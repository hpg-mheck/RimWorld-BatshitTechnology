using System;
using System.Collections.Generic;
using System.Linq;
using RandomReplicator.Core;
using Xunit;

namespace RandomReplicator.Tests;

/// <summary>
/// Verifies <see cref="MonologuePicker"/>: random lines, never the same
/// one twice in a row.
/// </summary>
public sealed class MonologuePickerTests
{
    /// <summary>Samples spread evenly across [0, 1).</summary>
    /// <returns>Samples from 0 up to (but excluding) 1.</returns>
    private static IEnumerable<double> SampleGrid() =>
        Enumerable.Range(0, 1000).Select(i => i / 1000d);

    /// <summary>No sample ever repeats the previous line.</summary>
    [Fact(DisplayName = "Never repeats the previous line")]
    public void NextIndex_should_never_repeat_previous_line()
    {
        for (int previous = 0; previous < 28; previous++)
        {
            foreach (double sample in SampleGrid())
            {
                int next = MonologuePicker.NextIndex(28, previous, () => sample);
                Assert.NotEqual(previous, next);
                Assert.InRange(next, 0, 27);
            }
        }
    }

    /// <summary>Every other line stays reachable after any line.</summary>
    [Fact(DisplayName = "Every other line is reachable")]
    public void NextIndex_should_reach_every_other_line()
    {
        var seen = SampleGrid()
            .Select(sample => MonologuePicker.NextIndex(28, 5, () => sample))
            .ToHashSet();

        Assert.Equal(27, seen.Count);
        Assert.DoesNotContain(5, seen);
    }

    /// <summary>With no previous line, any line can come first.</summary>
    [Theory(DisplayName = "Any line can come first")]
    [InlineData(-1)]
    [InlineData(99)]
    public void NextIndex_should_use_all_lines_without_a_valid_previous(int previous)
    {
        var seen = SampleGrid()
            .Select(sample => MonologuePicker.NextIndex(28, previous, () => sample))
            .ToHashSet();

        Assert.Equal(28, seen.Count);
    }

    /// <summary>A single line is always chosen, even if it was just said.</summary>
    [Fact(DisplayName = "A single line repeats")]
    public void NextIndex_should_return_zero_for_one_line()
    {
        Assert.Equal(0, MonologuePicker.NextIndex(1, 0, () => 0.5));
    }

    /// <summary>Invalid input fails fast.</summary>
    [Fact(DisplayName = "Rejects no lines, a null source or an out-of-range sample")]
    public void NextIndex_should_reject_invalid_input()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonologuePicker.NextIndex(0, -1, () => 0.5));
        Assert.Throws<ArgumentNullException>(
            () => MonologuePicker.NextIndex(3, -1, null!));
        Assert.Throws<InvalidOperationException>(
            () => MonologuePicker.NextIndex(3, -1, () => 1.0));
    }
}
