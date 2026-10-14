using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="SceneSequencer"/>, the director's state machine:
/// scenes run in order and end on their wait-until conditions, after
/// their minimum and settle time, or at their timeouts; Skip and Stop
/// behave as docs/SHOWCASE-API.md says.
/// </summary>
public sealed class SceneSequencerTests
{
    /// <summary>
    /// Advances a sequencer tick by tick until it is over or a tick limit
    /// passes, with a condition chosen per scene and tick.
    /// </summary>
    /// <param name="sequencer">The sequencer.</param>
    /// <param name="condition">
    /// The condition for (scene index, elapsed ticks in that scene).
    /// </param>
    /// <param name="limit">The last tick to run.</param>
    /// <returns>
    /// Each scene's (start tick, end tick, ending), in order.
    /// </returns>
    private static List<(int Start, int End, SceneEnd Ending)> Run(
        SceneSequencer sequencer,
        Func<int, int, bool> condition,
        int limit = 10000)
    {
        var log = new List<(int Start, int End, SceneEnd Ending)>();
        var starts = new Dictionary<int, int>();
        for (int now = 0; now <= limit && !sequencer.IsOver; now++)
        {
            int scene = sequencer.Current;
            bool met = sequencer.State == SequencerState.Running
                && condition(scene, sequencer.Elapsed(now));
            SequencerStep step = sequencer.Advance(now, met);
            if (step.EndedScene >= 0)
            {
                log.Add((starts[step.EndedScene], now, step.Ending!.Value));
            }

            if (step.StartedScene >= 0)
            {
                starts[step.StartedScene] = now;
            }
        }

        return log;
    }

    /// <summary>The first advance starts scene 0 and nothing ends.</summary>
    [Fact(DisplayName = "The first advance starts the first scene")]
    public void Advance_should_start_the_first_scene()
    {
        var sequencer = new SceneSequencer(new[] { new SceneTiming(10, 100) });

        SequencerStep step = sequencer.Advance(5, conditionMet: true);

        Assert.Equal(0, step.StartedScene);
        Assert.Equal(-1, step.EndedScene);
        Assert.Equal(SequencerState.Running, sequencer.State);
        Assert.Equal(5, sequencer.SceneStartTick);
    }

    /// <summary>
    /// A scene whose condition holds early still runs its minimum, then
    /// ends Completed; the next starts on the same tick.
    /// </summary>
    [Fact(DisplayName = "A met condition waits out the minimum, then completes")]
    public void Scene_should_complete_after_minimum_when_condition_met()
    {
        var sequencer = new SceneSequencer(new[]
        {
            new SceneTiming(30, 600), new SceneTiming(0, 600),
        });

        var log = Run(sequencer, (scene, elapsed) => true);

        Assert.Equal((0, 30, SceneEnd.Completed), log[0]);
        Assert.Equal(30, log[1].Start);
        Assert.Equal(SequencerState.Finished, sequencer.State);
    }

    /// <summary>
    /// A scene ends when its condition holds, not at its timeout: the
    /// director waits on events, not fixed delays.
    /// </summary>
    [Fact(DisplayName = "A scene ends when its condition holds, not at its timeout")]
    public void Scene_should_end_on_condition_not_timeout()
    {
        var sequencer = new SceneSequencer(new[] { new SceneTiming(0, 5000) });

        var log = Run(sequencer, (scene, elapsed) => elapsed >= 50);

        Assert.Equal((0, 50, SceneEnd.Completed), Assert.Single(log));
    }

    /// <summary>
    /// A condition that never holds ends the scene at its timeout.
    /// </summary>
    [Fact(DisplayName = "A condition that never holds times out")]
    public void Scene_should_time_out_when_condition_never_holds()
    {
        var sequencer = new SceneSequencer(new[]
        {
            new SceneTiming(10, 90), new SceneTiming(0, 40),
        });

        var log = Run(sequencer, (scene, elapsed) => false);

        Assert.Equal(
            new[] { (0, 90, SceneEnd.TimedOut), (90, 130, SceneEnd.TimedOut) },
            log);
        Assert.Equal(new[] { SceneEnd.TimedOut, SceneEnd.TimedOut }, sequencer.Endings);
    }

    /// <summary>
    /// Settle time holds the scene after its condition first holds; the
    /// condition is latched, so it may flicker off while settling.
    /// </summary>
    [Fact(DisplayName = "Settle time holds after the condition, which is latched")]
    public void Scene_should_settle_after_condition_and_latch_it()
    {
        var sequencer = new SceneSequencer(new[] { new SceneTiming(0, 1000, 60) });

        var log = Run(sequencer, (scene, elapsed) => elapsed == 20);

        Assert.Equal((0, 80, SceneEnd.Completed), Assert.Single(log));
    }

    /// <summary>
    /// Once the condition holds, the timeout no longer applies: a scene
    /// settling past its timeout still completes rather than timing out.
    /// </summary>
    [Fact(DisplayName = "Settling past the timeout still completes")]
    public void Scene_should_complete_when_settling_past_timeout()
    {
        var sequencer = new SceneSequencer(new[] { new SceneTiming(0, 100, 50) });

        var log = Run(sequencer, (scene, elapsed) => elapsed >= 90);

        Assert.Equal((0, 140, SceneEnd.Completed), Assert.Single(log));
    }

    /// <summary>
    /// Skip ends the running scene as Skipped and starts the next at once;
    /// skipping the last finishes.
    /// </summary>
    [Fact(DisplayName = "Skip ends the scene and starts the next")]
    public void Skip_should_end_scene_and_start_next()
    {
        var sequencer = new SceneSequencer(new[]
        {
            new SceneTiming(0, 1000), new SceneTiming(0, 1000),
        });
        sequencer.Advance(0, false);
        sequencer.Advance(10, false);

        SequencerStep first = sequencer.Skip(12);
        SequencerStep last = sequencer.Skip(20);

        Assert.Equal(0, first.EndedScene);
        Assert.Equal(SceneEnd.Skipped, first.Ending);
        Assert.Equal(1, first.StartedScene);
        Assert.Equal(12, sequencer.SceneStartTick);
        Assert.True(last.Finished);
        Assert.Equal(SequencerState.Finished, sequencer.State);
        Assert.Equal(new[] { SceneEnd.Skipped, SceneEnd.Skipped }, sequencer.Endings);
        Assert.False(sequencer.Skip(30).Any);
    }

    /// <summary>
    /// Fail ends the running scene as Failed on the same tick, before its
    /// minimum and whatever its settle time, and starts the next; it does
    /// nothing when no scene runs.
    /// </summary>
    [Fact(DisplayName = "Fail ends a scene at once and starts the next")]
    public void Fail_should_end_scene_at_once_and_start_next()
    {
        var sequencer = new SceneSequencer(new[]
        {
            new SceneTiming(500, 1000, 200), new SceneTiming(0, 1000),
        });
        Assert.False(sequencer.Fail(0).Any);
        sequencer.Advance(0, false);

        SequencerStep step = sequencer.Fail(3);

        Assert.Equal(0, step.EndedScene);
        Assert.Equal(SceneEnd.Failed, step.Ending);
        Assert.Equal(1, step.StartedScene);
        Assert.Equal(3, sequencer.SceneStartTick);
        Assert.True(sequencer.Fail(4).Finished);
        Assert.Equal(new[] { SceneEnd.Failed, SceneEnd.Failed }, sequencer.Endings);
        Assert.False(sequencer.Fail(5).Any);
    }

    /// <summary>
    /// Stop ends the running scene as Stopped, and no later scene runs.
    /// </summary>
    [Fact(DisplayName = "Stop ends the showcase; later scenes never run")]
    public void Stop_should_end_everything()
    {
        var sequencer = new SceneSequencer(new[]
        {
            new SceneTiming(0, 1000),
            new SceneTiming(0, 1000),
            new SceneTiming(0, 1000),
        });
        sequencer.Advance(0, false);
        sequencer.Skip(5);

        SequencerStep step = sequencer.Stop(9);

        Assert.True(step.Stopped);
        Assert.Equal(1, step.EndedScene);
        Assert.Equal(SceneEnd.Stopped, step.Ending);
        Assert.Equal(SequencerState.Stopped, sequencer.State);
        Assert.True(sequencer.IsOver);
        Assert.False(sequencer.Advance(10, true).Any);
        Assert.False(sequencer.Stop(11).Any);
        Assert.Equal(new[] { SceneEnd.Skipped, SceneEnd.Stopped }, sequencer.Endings);
    }

    /// <summary>Stopping before the first scene ends nothing.</summary>
    [Fact(DisplayName = "Stop before the first scene only marks it stopped")]
    public void Stop_before_start_should_end_no_scene()
    {
        var sequencer = new SceneSequencer(new[] { new SceneTiming(0, 10) });

        SequencerStep step = sequencer.Stop(0);

        Assert.True(step.Stopped);
        Assert.Equal(-1, step.EndedScene);
        Assert.Empty(sequencer.Endings);
        Assert.False(sequencer.Advance(1, true).Any);
    }

    /// <summary>No scenes: the first advance finishes at once.</summary>
    [Fact(DisplayName = "No scenes finish at once")]
    public void Empty_sequencer_should_finish_at_once()
    {
        var sequencer = new SceneSequencer(Array.Empty<SceneTiming>());

        SequencerStep step = sequencer.Advance(0, false);

        Assert.True(step.Finished);
        Assert.Equal(-1, step.StartedScene);
        Assert.Equal(SequencerState.Finished, sequencer.State);
        Assert.Throws<ArgumentNullException>(() => new SceneSequencer(null!));
    }

    /// <summary>
    /// Timing clamps nonsense: negatives become zero, and the timeout is
    /// at least the minimum and at least one tick.
    /// </summary>
    [Fact(DisplayName = "Scene timing clamps nonsense")]
    public void Timing_should_clamp_nonsense()
    {
        var timing = new SceneTiming(-5, -5, -5);
        var longMin = new SceneTiming(200, 100);

        Assert.Equal(0, timing.MinTicks);
        Assert.Equal(1, timing.TimeoutTicks);
        Assert.Equal(0, timing.SettleTicks);
        Assert.Equal(200, longMin.TimeoutTicks);
    }

    /// <summary>
    /// A whole tour: scenes that complete, time out and finish in order,
    /// each starting the tick the previous ended.
    /// </summary>
    [Fact(DisplayName = "A tour runs its scenes in order, back to back")]
    public void Tour_should_run_scenes_back_to_back()
    {
        var sequencer = new SceneSequencer(new[]
        {
            new SceneTiming(30, 600),
            new SceneTiming(0, 600),
            new SceneTiming(0, 90),
        });

        var log = Run(sequencer, (scene, elapsed) => scene switch
        {
            0 => true,
            1 => elapsed >= 50,
            _ => false,
        });

        Assert.Equal(
            new[]
            {
                (0, 30, SceneEnd.Completed),
                (30, 80, SceneEnd.Completed),
                (80, 170, SceneEnd.TimedOut),
            },
            log);
        Assert.Equal(2, sequencer.Current);
    }
}
