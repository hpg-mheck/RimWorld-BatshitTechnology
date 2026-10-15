using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// When the tour verifier's frames (<c>-btshots</c>) are taken: a few
/// ticks after the cue that asks for each, so the action is in frame
/// rather than the moment before it (slice 19000b; the defence volley's
/// frame, taken at the cue, showed only the Artificer). The delay depends
/// on the cue and, for a beat, on its moment: a blast is caught while its
/// fireball spreads, a volley while its bolts land, a pull while things
/// slide. The delays are short, at most a second of game time, so a frame
/// never lags its scene by much.
/// </summary>
public static class TourShotTiming
{
    /// <summary>Game ticks in a second at normal speed.</summary>
    public const int TicksPerSecond = 60;

    /// <summary>
    /// A beat's delay when its moment has none of its own: half a second,
    /// for the cut and the selection to land.
    /// </summary>
    public const int DefaultBeatTicks = TicksPerSecond / 2;

    /// <summary>
    /// A scene's opening frame: a third of a second after SCENE, once the
    /// opening cut and selection have landed.
    /// </summary>
    public const int SceneTicks = 20;

    /// <summary>The longest any delay is: one second.</summary>
    public const int MaxDelayTicks = TicksPerSecond;

    /// <summary>
    /// Delays for beats whose action takes a moment to show, by moment.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>volley, stragglers: 60. The Ballista's painful bolt flies at
    /// 0.6 tiles a tick (speed 60), so a bolt reaches the defence's
    /// target, 30 tiles out, in about 50 ticks; a second shows them
    /// landing.</item>
    /// <item>pull: 60. An implosion pulls for 2 to 8 seconds; one second
    /// in, things are visibly on the move.</item>
    /// <item>herd: 45. The stampede is well in from the map's edge.</item>
    /// <item>blast: 15. The beat plays as the device goes off; a quarter
    /// second in, the fireball is spreading, not yet gone.</item>
    /// <item>thrown: 10. The grenade is in the air.</item>
    /// </list>
    /// </remarks>
    private static readonly Dictionary<string, int> BeatTicks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "volley", 60 },
            { TourScript.Stragglers, 60 },
            { "pull", 60 },
            { "herd", 45 },
            { "blast", 15 },
            { TourScript.Thrown, 10 },
            { TourScript.ThrownAlone, 10 },
        };

    /// <summary>
    /// The ticks to wait after a cue before its frame is taken.
    /// </summary>
    /// <param name="cue">The cue word: READY, SCENE, BEAT or END.</param>
    /// <param name="moment">For a beat, its moment; otherwise ignored.</param>
    /// <returns>
    /// The delay: 0 for READY, END and any other cue (the moment itself
    /// is the frame), <see cref="SceneTicks"/> for SCENE, and the beat's
    /// own delay or <see cref="DefaultBeatTicks"/> for BEAT.
    /// </returns>
    public static int DelayTicks(string? cue, string? moment)
    {
        switch (cue)
        {
            case "SCENE":
                return SceneTicks;
            case "BEAT":
                return moment != null && BeatTicks.TryGetValue(moment, out int ticks)
                    ? ticks
                    : DefaultBeatTicks;
            default:
                return 0;
        }
    }
}

/// <summary>
/// The verifier's frames waiting to be taken, first in, first out: the
/// first is taken once its tick has come, and every later one waits for
/// it, so frames are taken in the order their cues came, one a frame. A
/// frame whose tick does not come (the game paused, or the run ending)
/// is taken anyway after <see cref="MaxWaitSeconds"/> of real time, or at
/// once when flushing.
/// </summary>
public sealed class ShotQueue
{
    /// <summary>The longest a frame waits in real time, in seconds.</summary>
    public const double MaxWaitSeconds = 3.0;

    /// <summary>The waiting frames, in the order they were asked for.</summary>
    private readonly Queue<(string Name, int DueTick, double QueuedAt)> waiting =
        new Queue<(string Name, int DueTick, double QueuedAt)>();

    /// <summary>Gets how many frames are waiting.</summary>
    public int Count => waiting.Count;

    /// <summary>Asks for a frame.</summary>
    /// <param name="name">The frame's file name.</param>
    /// <param name="dueTick">The game tick from which it may be taken.</param>
    /// <param name="queuedAt">The real time it was asked for, in seconds.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null or empty.
    /// </exception>
    public void Add(string name, int dueTick, double queuedAt)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A frame needs a name.", nameof(name));
        }

        waiting.Enqueue((name, dueTick, queuedAt));
    }

    /// <summary>
    /// Takes the first waiting frame if it is due: its tick has come, it
    /// has waited <see cref="MaxWaitSeconds"/>, or the queue is being
    /// flushed. Later frames never pass it.
    /// </summary>
    /// <param name="nowTick">The game tick now.</param>
    /// <param name="nowSeconds">The real time now, in seconds.</param>
    /// <param name="flush">Whether to take it whether or not it is due.</param>
    /// <returns>The frame's name, or null if none is due.</returns>
    public string? TakeDue(int nowTick, double nowSeconds, bool flush = false)
    {
        if (waiting.Count == 0)
        {
            return null;
        }

        (string name, int dueTick, double queuedAt) = waiting.Peek();
        if (!flush && nowTick < dueTick && nowSeconds - queuedAt < MaxWaitSeconds)
        {
            return null;
        }

        waiting.Dequeue();
        return name;
    }
}
