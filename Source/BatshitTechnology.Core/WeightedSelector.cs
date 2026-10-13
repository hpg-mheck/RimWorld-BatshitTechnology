using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>
/// Provides deterministic weighted random selection for replicator outputs
/// while remaining testable outside of RimWorld's <c>Rand</c> static class.
/// </summary>
/// <typeparam name="T">The payload carried by each weighted option.</typeparam>
public sealed class WeightedSelector<T>
{
    private readonly Func<double> _sampleSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeightedSelector{T}"/> class.
    /// </summary>
    /// <param name="sampleSource">
    /// Supplies the next random sample in the inclusive range [0, 1).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sampleSource"/> is <see langword="null"/>.
    /// </exception>
    public WeightedSelector(Func<double> sampleSource)
    {
        _sampleSource = sampleSource ?? throw new ArgumentNullException(
            nameof(sampleSource));
    }

    /// <summary>
    /// Picks a value from <paramref name="options"/> according to the specified
    /// weights. Options must expose strictly positive weights.
    /// </summary>
    /// <param name="options">The weighted options to select from.</param>
    /// <returns>The value associated with the sampled option.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="options"/> is empty or contains an option
    /// with a non-positive weight.
    /// </exception>
    public T Pick(IReadOnlyList<WeightedOption<T>> options)
    {
        if (options == null || options.Count == 0)
        {
            throw new ArgumentException(
                "At least one weighted option is required.",
                nameof(options));
        }

        var totalWeight = 0.0d;
        foreach (var option in options)
        {
            if (option.Weight <= 0)
            {
                throw new ArgumentException(
                    "Weighted options must expose positive weights.",
                    nameof(options));
            }

            totalWeight += option.Weight;
        }

        var sample = _sampleSource();
        if (sample < 0 || sample >= 1)
        {
            throw new InvalidOperationException(
                "Sample source must return a value inside [0, 1).");
        }

        var threshold = sample * totalWeight;
        var cumulative = 0.0d;
        foreach (var option in options)
        {
            cumulative += option.Weight;
            if (threshold < cumulative)
            {
                return option.Value;
            }
        }

        // Because floating point rounding is involved we defensively return the
        // final option if the loop above falls through. (Not options[^1]:
        // System.Index does not exist on netstandard2.0.)
        return options[options.Count - 1].Value;
    }
}

/// <summary>
/// Represents a value paired with a strictly positive selection weight.
/// </summary>
/// <param name="Value">The option's payload.</param>
/// <param name="Weight">The non-zero weight applied to the option.</param>
public readonly record struct WeightedOption<T>(T Value, double Weight)
{
    /// <summary>
    /// Gets a human-readable representation useful for diagnostics.
    /// </summary>
    /// <returns>A string that displays the value and weight.</returns>
    public override string ToString() => $"{Value} (weight {Weight})";
}
