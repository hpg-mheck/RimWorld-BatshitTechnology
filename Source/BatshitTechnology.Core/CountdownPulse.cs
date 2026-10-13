using System;

namespace BatshitTechnology.Core;

/// <summary>
/// The pulsing warning drawn around a device whose self-destruct is
/// counting down: a slow sinusoidal fade, switching to a fast one in the
/// final seconds. Kept free of RimWorld types so it can be unit tested.
/// </summary>
public static class CountdownPulse
{
    /// <summary>
    /// The warning's state at a moment in time.
    /// </summary>
    /// <param name="secondsLeft">Countdown seconds remaining.</param>
    /// <param name="finalWarningSeconds">
    /// When this many seconds or fewer remain, the final (fast, red) phase
    /// applies.
    /// </param>
    /// <param name="periodSeconds">Pulse period before the final phase.</param>
    /// <param name="finalPeriodSeconds">Pulse period in the final phase.</param>
    /// <param name="timeSeconds">A steadily increasing clock, in seconds.</param>
    /// <param name="minAlpha">The faintest the pulse gets, in [0, 1].</param>
    /// <returns>
    /// Whether the final phase applies, and the opacity in
    /// [<paramref name="minAlpha"/>, 1].
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a period is not positive.
    /// </exception>
    public static (bool Final, double Alpha) At(
        double secondsLeft, double finalWarningSeconds,
        double periodSeconds, double finalPeriodSeconds,
        double timeSeconds, double minAlpha = 0.15)
    {
        if (periodSeconds <= 0 || finalPeriodSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(periodSeconds), "Pulse periods must be positive.");
        }

        bool final = secondsLeft <= finalWarningSeconds;
        double period = final ? finalPeriodSeconds : periodSeconds;
        double wave = 0.5 + (0.5 * Math.Sin(2 * Math.PI * timeSeconds / period));
        double floor = Math.Min(1, Math.Max(0, minAlpha));
        return (final, floor + ((1 - floor) * wave));
    }

    /// <summary>
    /// Rounds an opacity to one of a fixed number of levels, so a renderer
    /// that pools one material per colour only ever needs a few of them.
    /// </summary>
    /// <param name="alpha">The opacity, clamped to [0, 1].</param>
    /// <param name="levels">How many steps above zero; at least 1.</param>
    /// <returns>The nearest level.</returns>
    public static double Quantize(double alpha, int levels)
    {
        int steps = Math.Max(1, levels);
        double clamped = Math.Min(1, Math.Max(0, alpha));
        return Math.Round(clamped * steps) / steps;
    }
}
