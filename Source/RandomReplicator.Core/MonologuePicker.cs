using System;

namespace RandomReplicator.Core;

/// <summary>
/// Picks Brain Squeezer monologue lines at random, never saying the same
/// line twice in a row. Kept free of RimWorld types so it can be unit
/// tested.
/// </summary>
public static class MonologuePicker
{
    /// <summary>
    /// Chooses the index of the next line to say.
    /// </summary>
    /// <param name="lineCount">How many lines there are. Must be positive.</param>
    /// <param name="previousIndex">
    /// The last line said, or a negative number if none yet. Out-of-range
    /// values are treated as none.
    /// </param>
    /// <param name="sampleSource">
    /// Supplies a random sample in the range [0, 1).
    /// </param>
    /// <returns>
    /// An index in [0, <paramref name="lineCount"/>), different from
    /// <paramref name="previousIndex"/> whenever more than one line exists.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="lineCount"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sampleSource"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sample is outside [0, 1).
    /// </exception>
    public static int NextIndex(int lineCount, int previousIndex, Func<double> sampleSource)
    {
        if (lineCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lineCount), lineCount, "At least one line is required.");
        }

        if (sampleSource == null)
        {
            throw new ArgumentNullException(nameof(sampleSource));
        }

        if (lineCount == 1)
        {
            return 0;
        }

        double sample = sampleSource();
        if (sample < 0 || sample >= 1)
        {
            throw new InvalidOperationException(
                "Sample source must return a value inside [0, 1).");
        }

        // Pick among the other lines, then step over the previous one.
        bool hasPrevious = previousIndex >= 0 && previousIndex < lineCount;
        int choices = hasPrevious ? lineCount - 1 : lineCount;
        int pick = (int)(sample * choices);
        if (hasPrevious && pick >= previousIndex)
        {
            pick++;
        }

        return pick;
    }
}
