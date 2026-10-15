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
        Assert.Equal(60, TourShotTiming.DelayTicks("BEAT", "volley"));
        Assert.Equal(60, TourShotTiming.DelayTicks("BEAT", "pull"));
        Assert.Equal(45, TourShotTiming.DelayTicks("BEAT", "herd"));
        Assert.Equal(15, TourShotTiming.DelayTicks("BEAT", "blast"));
        Assert.Equal(10, TourShotTiming.DelayTicks("BEAT", TourScript.Thrown));
        Assert.Equal(TourShotTiming.DefaultBeatTicks,
            TourShotTiming.DelayTicks("BEAT", "artificer"));
        Assert.Equal(TourShotTiming.DefaultBeatTicks, TourShotTiming.DelayTicks("BEAT", null));
    }

    /// <summary>
    /// Every beat of the tour's script waits more than nothing and at
    /// most a second, so a frame shows its action without lagging its
    /// scene.
    /// </summary>
    [Fact(DisplayName = "Every scripted beat waits up to a second")]
    public void DelayTicks_should_keep_every_scripted_beat_within_a_second()
    {
        foreach (string moment in TourScript.Scenes.SelectMany(s => s.Moments))
        {
            int ticks = TourShotTiming.DelayTicks("BEAT", moment);
            Assert.InRange(ticks, 1, TourShotTiming.MaxDelayTicks);
        }
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
