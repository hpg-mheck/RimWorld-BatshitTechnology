using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="WeatherHold"/>, the rule by which a showcase holds
/// its weather (docs/SHOWCASE-API.md, "The Held Weather").
/// </summary>
public sealed class WeatherHoldTests
{
    /// <summary>The held weather in these tests.</summary>
    private const string Clear = "Clear";

    /// <summary>
    /// With no held weather, nothing is done, whatever the weather.
    /// </summary>
    [Fact(DisplayName = "Nothing held, nothing done")]
    public void Next_should_do_nothing_without_a_held_weather()
    {
        Assert.Equal(WeatherHoldStep.None, WeatherHold.Next(null, "Rain", "Fog", 9000));
        Assert.Equal(WeatherHoldStep.None, WeatherHold.Next(string.Empty, "Rain", "Fog", 9000));
    }

    /// <summary>
    /// Another current weather is replaced at once, whatever came before
    /// it or how old it is.
    /// </summary>
    [Fact(DisplayName = "Another weather is replaced at once")]
    public void Next_should_restart_another_weather()
    {
        Assert.Equal(WeatherHoldStep.Restart, WeatherHold.Next(Clear, "Rain", Clear, 0));
        Assert.Equal(WeatherHoldStep.Restart, WeatherHold.Next(Clear, "Fog", "Rain", 5000));
        Assert.Equal(WeatherHoldStep.Restart, WeatherHold.Next(Clear, null, Clear, 0));
    }

    /// <summary>
    /// A transition back to the held weather (a scene's rain given back)
    /// is settled: the last weather becomes the held one.
    /// </summary>
    [Fact(DisplayName = "A transition back to the held weather is settled")]
    public void Next_should_settle_a_transition_back()
    {
        Assert.Equal(WeatherHoldStep.SettleLast, WeatherHold.Next(Clear, Clear, "Rain", 4000));
        Assert.Equal(WeatherHoldStep.SettleLast, WeatherHold.Next(Clear, Clear, "Rain", 10));
    }

    /// <summary>
    /// The held weather's age is set back once past the cap, and left
    /// alone at or under it; the cap is under the decider's shortest turn.
    /// </summary>
    [Fact(DisplayName = "The held weather's age stays under the decider's turn")]
    public void Next_should_keep_the_age_under_the_deciders_turn()
    {
        Assert.Equal(WeatherHoldStep.None, WeatherHold.Next(Clear, Clear, Clear, 0));
        Assert.Equal(
            WeatherHoldStep.None,
            WeatherHold.Next(Clear, Clear, Clear, WeatherHold.MaxHeldAge));
        Assert.Equal(
            WeatherHoldStep.ResetAge,
            WeatherHold.Next(Clear, Clear, Clear, WeatherHold.MaxHeldAge + 1));
        Assert.True(WeatherHold.MaxHeldAge < WeatherHold.ShortestDeciderTurn);
        Assert.Equal(10_000 / 4, WeatherHold.ShortestDeciderTurn);
    }

    /// <summary>
    /// The held weather shows when both ends are it, or the transition to
    /// it is complete; not while another weather still shows through.
    /// </summary>
    [Fact(DisplayName = "The held weather shows only when nothing else does")]
    public void Shows_should_hold_only_when_nothing_else_shows()
    {
        Assert.True(WeatherHold.Shows(Clear, Clear, Clear, 0));
        Assert.True(WeatherHold.Shows(Clear, Clear, "Rain", 1.0));
        Assert.False(WeatherHold.Shows(Clear, Clear, "Rain", 0.75));
        Assert.False(WeatherHold.Shows(Clear, "Rain", Clear, 1.0));
        Assert.False(WeatherHold.Shows(null, Clear, Clear, 1.0));
    }
}
