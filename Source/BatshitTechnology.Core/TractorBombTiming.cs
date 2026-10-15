using System;

namespace BatshitTechnology.Core;

/// <summary>
/// The Tractor Bomb's clock, from the moment it is ready to emplace to the
/// moment it goes off (docs/TRACTOR-BOMB.md): an optional protest, the
/// tyres flipping flat, a rolled countdown, then the implosion for at least
/// two seconds and at most eight, ending early once everything is gathered
/// or settled (<see cref="ImplosionMath.IsOver"/>). Kept free of RimWorld
/// types so it can be unit tested. All times are game ticks, 60 to the
/// second.
/// </summary>
public static class TractorBombTiming
{
    /// <summary>Game ticks per second.</summary>
    public const int TicksPerSecond = 60;

    /// <summary>
    /// How long it protests, beeping and flashing, when a colonist is
    /// inside its pull radius as it is ready to emplace: three seconds.
    /// </summary>
    public const int ProtestTicks = 3 * TicksPerSecond;

    /// <summary>
    /// How long its tyres take to flip out flat once it emplaces: about
    /// half a second.
    /// </summary>
    public const int DeployTicks = TicksPerSecond / 2;

    /// <summary>The shortest countdown, in seconds.</summary>
    public const double MinCountdownSeconds = 3;

    /// <summary>The longest countdown, in seconds.</summary>
    public const double MaxCountdownSeconds = 5;

    /// <summary>
    /// The shortest its implosion runs, in ticks: two seconds, even if
    /// everything is gathered sooner.
    /// </summary>
    public const int MinImplosionTicks = 2 * TicksPerSecond;

    /// <summary>
    /// The longest its implosion runs, in ticks: eight seconds, however
    /// things stand (a tug-of-war with a neighbour's pull included).
    /// </summary>
    public const int MaxImplosionTicks = 8 * TicksPerSecond;

    /// <summary>
    /// How long it protests before emplacing.
    /// </summary>
    /// <param name="colonistInPull">
    /// Whether a colonist is inside its pull radius when it is ready.
    /// </param>
    /// <returns>
    /// <see cref="ProtestTicks"/> if so; otherwise 0, and it emplaces at
    /// once.
    /// </returns>
    public static int ProtestDelay(bool colonistInPull) =>
        colonistInPull ? ProtestTicks : 0;

    /// <summary>
    /// A countdown rolled uniformly between two lengths, both included.
    /// </summary>
    /// <param name="unit">
    /// A random number in [0, 1): 0 gives the shortest countdown and values
    /// near 1 the longest. Clamped, and 0 if it is not a number.
    /// </param>
    /// <param name="minSeconds">
    /// The shortest countdown, in seconds; at least one tick.
    /// </param>
    /// <param name="maxSeconds">
    /// The longest countdown, in seconds; taken as the shortest if smaller.
    /// </param>
    /// <returns>The countdown, in ticks, at least 1.</returns>
    public static int RollCountdownTicks(
        double unit,
        double minSeconds = MinCountdownSeconds,
        double maxSeconds = MaxCountdownSeconds)
    {
        int min = Math.Max(1, ToTicks(minSeconds));
        int max = Math.Max(min, ToTicks(maxSeconds));
        double u = double.IsNaN(unit) ? 0
            : unit < 0 ? 0
            : unit >= 1 ? 1 - 1e-9
            : unit;
        // Inclusive at both ends: max - min + 1 equally likely lengths.
        return min + (int)Math.Floor(u * (max - min + 1));
    }

    /// <summary>
    /// Which colour the protest lamp shows: it blinks red and yellow,
    /// starting red.
    /// </summary>
    /// <param name="protestTicks">Ticks the protest has run.</param>
    /// <param name="halfPeriodTicks">
    /// Ticks each colour lasts; taken as 1 if smaller.
    /// </param>
    /// <returns>True for red, false for yellow.</returns>
    public static bool LampIsRed(int protestTicks, int halfPeriodTicks)
    {
        int half = Math.Max(1, halfPeriodTicks);
        int t = Math.Max(0, protestTicks);
        return (t / half) % 2 == 0;
    }

    /// <summary>
    /// Whether the protest beeps on this tick: at its start and then once
    /// every interval.
    /// </summary>
    /// <param name="protestTicks">Ticks the protest has run.</param>
    /// <param name="intervalTicks">
    /// Ticks between beeps; taken as 1 if smaller.
    /// </param>
    /// <returns>True if it beeps.</returns>
    public static bool BeepsAt(int protestTicks, int intervalTicks) =>
        protestTicks >= 0 && protestTicks % Math.Max(1, intervalTicks) == 0;

    /// <summary>
    /// Seconds to whole ticks, rounded; negative or NaN as 0.
    /// </summary>
    /// <param name="seconds">The seconds.</param>
    /// <returns>The ticks.</returns>
    private static int ToTicks(double seconds)
    {
        if (!(seconds > 0))
        {
            return 0;
        }

        double ticks = Math.Round(seconds * TicksPerSecond);
        return ticks >= int.MaxValue ? int.MaxValue : (int)ticks;
    }
}
