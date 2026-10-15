namespace BatshitTechnology.Showcases;

/// <summary>
/// What the held weather needs this tick (<see cref="WeatherHold.Next"/>).
/// </summary>
public enum WeatherHoldStep
{
    /// <summary>The weather is as held; nothing to do.</summary>
    None,

    /// <summary>
    /// The weather is as held, but its age has grown: set it back to 0,
    /// so the weather decider never reaches the end of the turn.
    /// </summary>
    ResetAge,

    /// <summary>
    /// The current weather is the held one, but the one before it is not
    /// (a transition back to it, finished or not): make both the held one
    /// and set the age to 0.
    /// </summary>
    SettleLast,

    /// <summary>
    /// The current weather is not the held one: make the current and the
    /// last weather the held one at once, with the age at 0.
    /// </summary>
    Restart,
}

/// <summary>
/// The rule of a showcase's held weather (the def's <c>holdWeather</c>,
/// docs/SHOWCASE-API.md, "The Held Weather"), without RimWorld, so it is
/// unit tested. Each tick, after the map's weather has ticked, the session
/// asks what the weather needs and applies it: the held weather is made
/// both the current and the last one (so no transition shows), and its
/// age is kept below the weather decider's shortest turn, so the decider
/// never starts another weather. No random number is drawn and nothing is
/// saved.
/// </summary>
/// <remarks>
/// RimWorld 1.6.4871's <c>WeatherDecider.WeatherDeciderTick</c> starts the
/// next weather once <c>WeatherManager.curWeatherAge</c> passes the
/// current turn: 10,000 ticks for a new game's first weather, else the
/// weather's own <c>durationRange</c> (15,000 at the least in vanilla),
/// cut to a quarter while a large fire burns or the temperature is out of
/// the weather's range. The shortest turn is therefore 2,500 ticks, and
/// the hold keeps the age at or below <see cref="MaxHeldAge"/>.
/// </remarks>
public static class WeatherHold
{
    /// <summary>
    /// The oldest the held weather may grow before its age is set back
    /// to 0: well under the decider's shortest turn of 2,500 ticks.
    /// </summary>
    public const int MaxHeldAge = 1000;

    /// <summary>
    /// The decider's shortest turn in RimWorld 1.6.4871, in ticks: a
    /// quarter of a new game's first 10,000-tick weather.
    /// </summary>
    public const int ShortestDeciderTurn = 2500;

    /// <summary>
    /// What the held weather needs now.
    /// </summary>
    /// <param name="held">The held weather's def name; null or empty
    /// holds nothing.</param>
    /// <param name="current">The current weather's def name.</param>
    /// <param name="last">The weather before it (still showing while a
    /// transition runs).</param>
    /// <param name="age">The current weather's age, in ticks.</param>
    /// <returns>The step to apply.</returns>
    public static WeatherHoldStep Next(
        string? held, string? current, string? last, int age)
    {
        if (string.IsNullOrEmpty(held))
        {
            return WeatherHoldStep.None;
        }

        if (current != held)
        {
            return WeatherHoldStep.Restart;
        }

        if (last != held)
        {
            return WeatherHoldStep.SettleLast;
        }

        return age > MaxHeldAge ? WeatherHoldStep.ResetAge : WeatherHoldStep.None;
    }

    /// <summary>
    /// Whether the weather shows as the held one: both the current and
    /// the last weather are it, or the current one is and the transition
    /// to it is complete. What the tour verifier checks each tick.
    /// </summary>
    /// <param name="held">The held weather's def name.</param>
    /// <param name="current">The current weather's def name.</param>
    /// <param name="last">The weather before it.</param>
    /// <param name="transition">How far the transition has come, 0 to 1
    /// (<c>WeatherManager.TransitionLerpFactor</c>).</param>
    /// <returns>True when nothing but the held weather shows.</returns>
    public static bool Shows(
        string? held, string? current, string? last, double transition) =>
        !string.IsNullOrEmpty(held) && current == held
        && (last == held || transition >= 1.0);
}
