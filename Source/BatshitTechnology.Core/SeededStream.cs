using System;

namespace BatshitTechnology.Core;

/// <summary>
/// A small, deterministic random stream (SplitMix64) for town details
/// such as the wander of a dirt road. Unlike System.Random it is defined
/// here, so the same seed gives the same town under .NET in the tests and
/// under RimWorld's Mono runtime.
/// </summary>
internal sealed class SeededStream
{
    private const ulong Golden = 0x9E3779B97F4A7C15UL;

    private ulong state;

    /// <summary>Starts a stream.</summary>
    /// <param name="seed">The seed; any value, negative included.</param>
    internal SeededStream(int seed)
    {
        unchecked
        {
            state = ((ulong)(uint)seed * Golden) ^ 0xD1B54A32D192ED03UL;
        }
    }

    /// <summary>The next 64 random bits.</summary>
    /// <returns>A uniformly distributed value.</returns>
    internal ulong NextBits()
    {
        unchecked
        {
            ulong z = state += Golden;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>A value from 0 up to, but not including, a bound.</summary>
    /// <param name="bound">The exclusive upper bound; at least 1.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A bound below 1.</exception>
    internal int Next(int bound)
    {
        if (bound < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bound), bound, "The bound must be at least 1.");
        }

        // The modulo bias is below 1e-17 for the tiny bounds used here.
        return (int)(NextBits() % (ulong)bound);
    }
}
