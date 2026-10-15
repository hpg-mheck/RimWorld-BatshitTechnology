using System;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="TourPacing"/>, the tour's timing of lines on the
/// narration overlay: each line read, faded and cleared before the next.
/// </summary>
public sealed class TourPacingTests
{
    /// <summary>
    /// A line clears after its reading time, the fade-out and the lead,
    /// at 60 ticks a second, rounded up.
    /// </summary>
    [Fact(DisplayName = "ClearTicks is reading, fade-out and lead in ticks")]
    public void ClearTicks_should_add_reading_fade_and_lead()
    {
        string line = new string('x', 50); // 2.5 + 2.5 s of reading
        double seconds = NarrationTiming.ReadingSeconds(line)
            + NarrationTiming.FadeOutSeconds + NarrationTiming.LeadSeconds;

        Assert.Equal((int)Math.Ceiling(60 * seconds), TourPacing.ClearTicks(line));
        Assert.Equal(480, TourPacing.ClearTicks(line));
        Assert.Equal(
            (int)Math.Ceiling(60 * (NarrationTiming.BaseReadingSeconds + 3)),
            TourPacing.ClearTicks(null));
    }

    /// <summary>
    /// A group of delayed lines clears when its last-clearing line does;
    /// no lines clear at once, and a negative delay counts as none.
    /// </summary>
    [Fact(DisplayName = "ClearAfter takes the latest line to clear")]
    public void ClearAfter_should_take_the_latest_line()
    {
        string shortLine = "Quiet."; // 2.8 s: clears in 348 ticks
        string longLine = new string('y', 100); // 7.5 s: clears in 630

        Assert.Equal(348, TourPacing.ClearTicks(shortLine));
        Assert.Equal(630, TourPacing.ClearTicks(longLine));
        Assert.Equal(0, TourPacing.ClearAfter(null));
        Assert.Equal(
            648, TourPacing.ClearAfter(new[] { (0, longLine), (300, shortLine) }));
        Assert.Equal(
            630, TourPacing.ClearAfter(new[] { (0, longLine), (200, shortLine) }));
        Assert.Equal(348, TourPacing.ClearAfter(new[] { (-40, shortLine) }));
    }

    /// <summary>
    /// A scene's intro lasts until its start lines have cleared, less the
    /// first beat's own tick, and is never negative.
    /// </summary>
    [Fact(DisplayName = "IntroTicks waits for the start lines, less the first beat")]
    public void IntroTicks_should_wait_for_the_start_lines()
    {
        string start = new string('z', 30); // 4 s: clears in 420 ticks

        Assert.Equal(420, TourPacing.ClearTicks(start));
        Assert.Equal(90 + 420 - 150, TourPacing.IntroTicks(new[] { (90, start) }, 150));
        Assert.Equal(0, TourPacing.IntroTicks(new[] { (0, start) }, 5000));
        Assert.Equal(0, TourPacing.IntroTicks(null, 150));
    }

    /// <summary>
    /// Lines of one cue are spaced when each, in order of delay, is due
    /// no sooner than the one before has cleared; one line or none is
    /// spaced, and the order they are listed in does not matter.
    /// </summary>
    [Fact(DisplayName = "Spaced needs each line to wait for the one before")]
    public void Spaced_should_need_each_line_to_clear()
    {
        string first = new string('a', 41); // the raccoon's fate: 453 ticks

        Assert.Equal(453, TourPacing.ClearTicks(first));
        Assert.True(TourPacing.Spaced(null));
        Assert.True(TourPacing.Spaced(new[] { (0, first) }));
        Assert.True(TourPacing.Spaced(new[] { (453, "Thanks."), (0, first) }));
        Assert.False(TourPacing.Spaced(new[] { (0, first), (452, "Thanks.") }));
        Assert.False(TourPacing.Spaced(new[] { (0, first), (60, "Thanks.") }));
    }
}
