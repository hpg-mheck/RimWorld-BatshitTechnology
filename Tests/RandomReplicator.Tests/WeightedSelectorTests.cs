using System;
using System.Collections.Generic;
using RandomReplicator.Core;
using Xunit;

namespace RandomReplicator.Tests;

/// <summary>
/// Verifies the deterministic behaviour of <see cref="WeightedSelector{T}"/>.
/// </summary>
public sealed class WeightedSelectorTests
{
    /// <summary>
    /// Verifies that a zero sample always returns the first option.
    /// </summary>
    [Fact(DisplayName = "Selector chooses first option when sample is zero")]
    public void Pick_should_return_first_option_when_sample_is_zero()
    {
        var selector = new WeightedSelector<string>(() => 0);
        var options = new List<WeightedOption<string>>
        {
            new("First", 1),
            new("Second", 1)
        };

        Assert.Equal("First", selector.Pick(options));
    }

    /// <summary>
    /// Ensures that samples close to one return the final option when weights
    /// allow.
    /// </summary>
    [Fact(DisplayName = "Selector chooses final option near upper bound")]
    public void Pick_should_return_final_option_when_sample_is_nearly_one()
    {
        var selector = new WeightedSelector<string>(() => 0.9999999);
        var options = new List<WeightedOption<string>>
        {
            new("Low", 1),
            new("High", 3)
        };

        Assert.Equal("High", selector.Pick(options));
    }

    /// <summary>
    /// Confirms that selecting from an empty set fails fast.
    /// </summary>
    [Fact(DisplayName = "Selector throws when provided empty options")]
    public void Pick_should_throw_for_empty_option_list()
    {
        var selector = new WeightedSelector<int>(() => 0.5);
        Assert.Throws<ArgumentException>(() => selector.Pick(Array.Empty<WeightedOption<int>>()));
    }

    /// <summary>
    /// Validates that non-positive weights are rejected.
    /// </summary>
    [Fact(DisplayName = "Selector rejects non-positive weights")]
    public void Pick_should_throw_when_option_weight_is_non_positive()
    {
        var selector = new WeightedSelector<int>(() => 0.2);
        var options = new List<WeightedOption<int>>
        {
            new(1, 0),
            new(2, 3)
        };

        Assert.Throws<ArgumentException>(() => selector.Pick(options));
    }

    /// <summary>
    /// Verifies that out-of-range samples trigger a guard.
    /// </summary>
    [Fact(DisplayName = "Selector rejects samples outside inclusive range")]
    public void Pick_should_throw_when_sample_is_out_of_range()
    {
        var selector = new WeightedSelector<int>(() => 1);
        var options = new List<WeightedOption<int>>
        {
            new(1, 1),
            new(2, 1)
        };

        Assert.Throws<InvalidOperationException>(() => selector.Pick(options));
    }
}
