using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the director's text and timing helpers: the beat schedule
/// inside a scene, narration lines picked by scene and moment, and the
/// caption bar's "2 of 8: ..." format.
/// </summary>
public sealed class ShowcaseTextTests
{
    /// <summary>
    /// Beats come out once each, in tick order with ties in declared
    /// order, however the scene is ticked.
    /// </summary>
    [Fact(DisplayName = "Beats fire once each, in tick order")]
    public void BeatSchedule_should_hand_out_each_beat_once_in_order()
    {
        var schedule = new BeatSchedule(new[] { 60, 0, 60, -10, 200 });

        Assert.Equal(new[] { 1, 3 }, schedule.TakeDue(0));
        Assert.Empty(schedule.TakeDue(59));
        Assert.Equal(new[] { 0, 2 }, schedule.TakeDue(150));
        Assert.Equal(1, schedule.Remaining);
        Assert.Equal(new[] { 4 }, schedule.TakeDue(10000));
        Assert.Empty(schedule.TakeDue(20000));
        Assert.Equal(5, schedule.Count);
        Assert.Equal(0, new BeatSchedule(null).Count);
    }

    /// <summary>
    /// Narration lines are picked by scene and moment without regard to
    /// case, by delay then declared order; a blank moment means the
    /// start, and blank lines are dropped.
    /// </summary>
    [Fact(DisplayName = "Narration picks lines by scene and moment")]
    public void Narration_should_pick_lines_by_scene_and_moment()
    {
        var lines = new[]
        {
            new NarrationCue("reactor", "", 120, "second"),
            new NarrationCue("reactor", null, 0, "first"),
            new NarrationCue("Reactor", "START", 120, "third"),
            new NarrationCue("reactor", "completed", 0, "done"),
            new NarrationCue("grenade", "start", 0, "elsewhere"),
            new NarrationCue("reactor", "start", 0, "  "),
            null,
        };

        Assert.Equal(
            new[] { "first", "second", "third" },
            NarrationScript.LinesFor(lines, "REACTOR", NarrationScript.Start)
                .Select(l => l.Text));
        Assert.Equal("done",
            Assert.Single(NarrationScript.LinesFor(lines, "reactor", "Completed")).Text);
        Assert.Empty(NarrationScript.LinesFor(lines, "reactor", "timedOut"));
        Assert.Empty(NarrationScript.LinesFor(null, "reactor", null));
    }

    /// <summary>Unknown scene keys are reported once each.</summary>
    [Fact(DisplayName = "Narration naming a missing scene is reported")]
    public void Narration_should_report_unknown_scenes()
    {
        var lines = new[]
        {
            new NarrationCue("intro", "start", 0, "a"),
            new NarrationCue("ghost", "start", 0, "b"),
            new NarrationCue("GHOST", "completed", 0, "c"),
        };

        Assert.Equal(
            new[] { "ghost" },
            NarrationScript.UnknownScenes(lines, new[] { "Intro", null }));
        Assert.Empty(NarrationScript.UnknownScenes(null, null));
    }

    /// <summary>Each ending has its own outcome key.</summary>
    [Fact(DisplayName = "Each ending has its own outcome key")]
    public void OutcomeKey_should_be_distinct_per_ending()
    {
        var keys = Enum.GetValues(typeof(SceneEnd)).Cast<SceneEnd>()
            .Select(NarrationScript.OutcomeKey)
            .ToList();

        Assert.Equal(
            new[] { "completed", "timedOut", "skipped", "stopped", "failed" }, keys);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NarrationScript.OutcomeKey((SceneEnd)99));
    }

    /// <summary>
    /// A "stopped" line with a delay would never be said, because Stop
    /// ends all narration; the configuration check reports exactly
    /// those, whatever their case, and nothing else.
    /// </summary>
    [Fact(DisplayName = "Delayed 'stopped' narration is reported as never said")]
    public void DroppedAtStop_should_report_delayed_stopped_lines()
    {
        var lines = new[]
        {
            new NarrationCue("reactor", "stopped", 0, "said at once"),
            new NarrationCue("reactor", "stopped", 60, "dropped"),
            new NarrationCue("grenade", " STOPPED ", 1, "dropped too"),
            new NarrationCue("reactor", "completed", 60, "another moment"),
            new NarrationCue("reactor", "start", 60, "the start"),
            null,
        };

        Assert.Equal(
            new[] { "dropped", "dropped too" },
            NarrationScript.DroppedAtStop(lines).Select(l => l.Text));
        Assert.Empty(NarrationScript.DroppedAtStop(null));
        Assert.Equal("stopped", NarrationScript.Stopped);
    }

    /// <summary>The caption reads "2 of 8: Title".</summary>
    [Fact(DisplayName = "Captions read '2 of 8: Title'")]
    public void Caption_should_number_scenes_from_one()
    {
        Assert.Equal("2 of 8: The Batshittium Reactor",
            CaptionText.Format(
                CaptionText.DefaultPattern, 1, 8, " The Batshittium Reactor "));
        Assert.Equal("Szene 1/3 - Anfang",
            CaptionText.Format("Szene {0}/{1} - {2}", 0, 3, "Anfang"));
    }

    /// <summary>
    /// A blank or malformed pattern falls back to the default, numbers are
    /// clamped, and a blank title drops the colon.
    /// </summary>
    [Fact(DisplayName = "Captions survive bad patterns and blank titles")]
    public void Caption_should_survive_bad_input()
    {
        Assert.Equal("1 of 1: X", CaptionText.Format(null, -4, 0, "X"));
        Assert.Equal("3 of 3: X", CaptionText.Format("{0} of {1}: {2", 9, 3, "X"));
        Assert.Equal(
            "2 of 4", CaptionText.Format(CaptionText.DefaultPattern, 1, 4, "  "));
    }

    /// <summary>Running times round to whole minutes, clamped.</summary>
    [Fact(DisplayName = "Running times round to whole minutes")]
    public void WholeMinutes_should_round_and_clamp()
    {
        Assert.Equal(1, CaptionText.WholeMinutes(0f));
        Assert.Equal(1, CaptionText.WholeMinutes(float.NaN));
        Assert.Equal(2, CaptionText.WholeMinutes(1.5f));
        Assert.Equal(3, CaptionText.WholeMinutes(3.4f));
        Assert.Equal(
            CaptionText.MaxMinutes,
            CaptionText.WholeMinutes(float.PositiveInfinity));
    }
}
