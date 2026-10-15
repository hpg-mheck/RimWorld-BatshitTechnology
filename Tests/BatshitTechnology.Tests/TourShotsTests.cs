using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="TourShotTiming"/> and <see cref="ShotQueue"/>: when
/// the tour verifier's frames are taken (slice 19000b).
/// </summary>
public sealed class TourShotsTests
{
    /// <summary>
    /// READY and END are taken at the moment itself; SCENE a third of a
    /// second later; any other cue at once.
    /// </summary>
    [Fact(DisplayName = "READY and END at once, SCENE a moment later")]
    public void DelayTicks_should_time_cues_other_than_beats()
    {
        Assert.Equal(0, TourShotTiming.DelayTicks("READY", null));
        Assert.Equal(0, TourShotTiming.DelayTicks("END", "completed"));
        Assert.Equal(TourShotTiming.SceneTicks, TourShotTiming.DelayTicks("SCENE", null));
        Assert.Equal(0, TourShotTiming.DelayTicks("FINISHED", null));
        Assert.Equal(0, TourShotTiming.DelayTicks(null, null));
    }

    /// <summary>
    /// The beats whose action takes a moment get their own delays; the
    /// others the default half second.
    /// </summary>
    [Fact(DisplayName = "Each beat kind has its delay")]
    public void DelayTicks_should_time_beats_by_moment()
    {
        Assert.Equal(TourShotTiming.MaxDelayTicks, TourShotTiming.DelayTicks("BEAT", "volley"));
        Assert.Equal(
            TourShotTiming.MaxDelayTicks,
            TourShotTiming.DelayTicks("BEAT", TourScript.Stragglers));
        Assert.Equal(60, TourShotTiming.DelayTicks("BEAT", "pull"));
        Assert.Equal(45, TourShotTiming.DelayTicks("BEAT", "herd"));
        Assert.Equal(15, TourShotTiming.DelayTicks("BEAT", "blast"));
        Assert.Equal(10, TourShotTiming.DelayTicks("BEAT", TourScript.Thrown));
        Assert.Equal(TourShotTiming.DefaultBeatTicks,
            TourShotTiming.DelayTicks("BEAT", "artificer"));
        Assert.Equal(TourShotTiming.DefaultBeatTicks, TourShotTiming.DelayTicks("BEAT", null));
    }

    /// <summary>
    /// Every beat of the tour's script, in its own scene, waits more than
    /// nothing and at most five seconds (a volley's longest flight, which
    /// its frame waits out only if no bolt is seen to land), so a frame
    /// shows its action without lagging its scene.
    /// </summary>
    [Fact(DisplayName = "Every scripted beat waits up to 5 seconds")]
    public void DelayTicks_should_keep_every_scripted_beat_within_the_limit()
    {
        foreach (var scene in TourScript.Scenes)
        {
            foreach (string moment in scene.Moments)
            {
                int ticks = TourShotTiming.DelayTicks("BEAT", scene.Key, moment);
                Assert.InRange(ticks, 1, TourShotTiming.MaxDelayTicks);
            }
        }
    }

    /// <summary>
    /// The pads' blasts wait for their own action: the replicator's bomb
    /// blast to spread, the reactor's barrage to land (slice 19000d);
    /// every other scene's blast keeps the moment's quarter second.
    /// </summary>
    [Fact(DisplayName = "The pads' blasts wait longer than the tractors'")]
    public void DelayTicks_should_time_the_pads_blasts_by_scene()
    {
        Assert.Equal(45, TourShotTiming.DelayTicks("BEAT", "retire-replicator", "blast"));
        Assert.Equal(100, TourShotTiming.DelayTicks("BEAT", "retire-reactor", "blast"));
        Assert.Equal(15, TourShotTiming.DelayTicks("BEAT", "tractor", "blast"));
        Assert.Equal(15, TourShotTiming.DelayTicks("BEAT", "twins", "blast"));
        Assert.Equal(TourShotTiming.SceneTicks,
            TourShotTiming.DelayTicks("SCENE", "retire-reactor", "blast"));
        Assert.Equal(0, TourShotTiming.DelayTicks("END", "retire-reactor", "blast"));
    }

    /// <summary>
    /// Only a volley's beats wait for their bolts to land, a few ticks
    /// after the first.
    /// </summary>
    [Fact(DisplayName = "Volley frames wait for a landing; others do not")]
    public void LandingTicks_should_be_for_volleys_only()
    {
        Assert.Equal(TourShotTiming.AfterLandingTicks, TourShotTiming.LandingTicks("BEAT", "volley"));
        Assert.Equal(
            TourShotTiming.AfterLandingTicks,
            TourShotTiming.LandingTicks("BEAT", TourScript.Stragglers));
        Assert.Equal(-1, TourShotTiming.LandingTicks("BEAT", "blast"));
        Assert.Equal(-1, TourShotTiming.LandingTicks("SCENE", "volley"));
        Assert.Equal(-1, TourShotTiming.LandingTicks(null, null));
    }

    /// <summary>
    /// The real-time fallback outlasts the longest delay even at the 48
    /// ticks a second a presentation runs, so it never takes a volley's
    /// frame before its bolts can land.
    /// </summary>
    [Fact(DisplayName = "The real-time fallback outlasts the longest delay")]
    public void MaxWaitSeconds_should_outlast_the_longest_delay()
    {
        Assert.True(ShotQueue.MaxWaitSeconds * 48 > TourShotTiming.MaxDelayTicks);
    }

    /// <summary>
    /// A frame that waits for a landing is taken a few ticks after the
    /// first landing seen once it was asked for, before its own tick; a
    /// landing seen before it was asked for does not count, and a frame
    /// that does not wait for one ignores landings.
    /// </summary>
    [Fact(DisplayName = "A volley's frame is taken a few ticks after a landing")]
    public void TakeDue_should_take_a_landing_frame_early()
    {
        var queue = new ShotQueue();
        queue.Landed(90);
        queue.Add("01-volley.png", 400, 0.0, afterLanding: 8);
        queue.Add("02-blast.png", 400, 0.0);
        Assert.Null(queue.TakeDue(130, 0.1));
        queue.Landed(131);
        Assert.Null(queue.TakeDue(138, 0.1));
        Assert.Equal("01-volley.png", queue.TakeDue(139, 0.1));
        queue.Landed(140);
        Assert.Null(queue.TakeDue(200, 0.2));
        Assert.Equal("02-blast.png", queue.TakeDue(400, 0.3));
    }

    /// <summary>
    /// A frame that waits for a landing and sees none is taken at its own
    /// tick, the longest wait.
    /// </summary>
    [Fact(DisplayName = "A volley's frame with no landing waits for its tick")]
    public void TakeDue_should_fall_back_to_the_tick_without_a_landing()
    {
        var queue = new ShotQueue();
        queue.Add("01-volley.png", 300, 0.0, afterLanding: 8);
        Assert.Null(queue.TakeDue(299, 1.0));
        Assert.Equal("01-volley.png", queue.TakeDue(300, 1.0));
    }

    /// <summary>An empty queue has nothing to take.</summary>
    [Fact(DisplayName = "An empty queue takes nothing")]
    public void TakeDue_should_take_nothing_from_an_empty_queue()
    {
        var queue = new ShotQueue();
        Assert.Null(queue.TakeDue(100, 0.0));
        Assert.Null(queue.TakeDue(100, 0.0, flush: true));
    }

    /// <summary>A frame waits for its tick, then is taken once.</summary>
    [Fact(DisplayName = "A frame is taken once its tick comes")]
    public void TakeDue_should_wait_for_the_frames_tick()
    {
        var queue = new ShotQueue();
        queue.Add("01-a.png", 160, 10.0);
        Assert.Null(queue.TakeDue(159, 10.5));
        Assert.Equal("01-a.png", queue.TakeDue(160, 10.5));
        Assert.Equal(0, queue.Count);
        Assert.Null(queue.TakeDue(200, 11.0));
    }

    /// <summary>
    /// Frames are taken in the order asked for: a later frame due sooner
    /// waits for the first.
    /// </summary>
    [Fact(DisplayName = "Frames are taken first in, first out")]
    public void TakeDue_should_keep_the_order_asked_for()
    {
        var queue = new ShotQueue();
        queue.Add("01-beat.png", 160, 0.0);
        queue.Add("02-end.png", 101, 0.0);
        Assert.Null(queue.TakeDue(120, 0.1));
        Assert.Equal("01-beat.png", queue.TakeDue(160, 0.2));
        Assert.Equal("02-end.png", queue.TakeDue(160, 0.2));
    }

    /// <summary>
    /// A frame whose tick does not come (a paused game) is taken after
    /// the real-time limit; flushing takes it at once.
    /// </summary>
    [Fact(DisplayName = "A paused game's frame is taken in real time")]
    public void TakeDue_should_give_up_waiting_in_real_time_or_when_flushed()
    {
        var queue = new ShotQueue();
        queue.Add("01-a.png", 500, 2.0);
        queue.Add("02-b.png", 500, 2.0);
        Assert.Null(queue.TakeDue(100, 2.0 + ShotQueue.MaxWaitSeconds - 0.01));
        Assert.Equal("01-a.png", queue.TakeDue(100, 2.0 + ShotQueue.MaxWaitSeconds));
        Assert.Equal("02-b.png", queue.TakeDue(100, 2.1, flush: true));
    }

    /// <summary>A frame needs a name.</summary>
    [Fact(DisplayName = "A frame without a name is refused")]
    public void Add_should_refuse_a_frame_without_a_name()
    {
        var queue = new ShotQueue();
        Assert.Throws<System.ArgumentException>(() => queue.Add(string.Empty, 1, 0.0));
        Assert.Throws<System.ArgumentException>(() => queue.Add(null!, 1, 0.0));
    }
}
