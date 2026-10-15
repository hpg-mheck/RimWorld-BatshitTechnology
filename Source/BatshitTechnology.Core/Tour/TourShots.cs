using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// When the tour verifier's frames (<c>-btshots</c>) are taken: a few
/// ticks after the cue that asks for each, so the action is in frame
/// rather than the moment before it (slice 19000b; the defence volley's
/// frame, taken at the cue, showed only the Artificer). The delay depends
/// on the cue and, for a beat, on its moment and scene: a blast is caught
/// while its fireball spreads, a pull while things slide. A volley's frame
/// waits for its bolts to land (slice 19000d: a fixed delay caught the
/// defence's bolts just after and the stampede's still in the air), with
/// its delay as the longest it waits.
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

    /// <summary>
    /// The longest any delay is: five seconds, the stampede's volley's
    /// longest flight (about 110 tiles at 0.45 tiles a tick is 245
    /// ticks), which its frame waits out only if no bolt is seen to land.
    /// </summary>
    public const int MaxDelayTicks = TicksPerSecond * 5;

    /// <summary>
    /// Ticks a volley's frame waits after the first of its bolts lands:
    /// the rest land within a few ticks of it, and each blast shows for
    /// a quarter of a second or so.
    /// </summary>
    public const int AfterLandingTicks = 8;

    /// <summary>
    /// Delays for beats whose action takes a moment to show, by moment.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>volley, stragglers: up to 300, but taken
    /// <see cref="AfterLandingTicks"/> after the first bolt lands. A
    /// volley launches every bolt at once at 0.45 tiles a tick (the really
    /// painful bolt's speed 45): about 130 ticks to the defence's target,
    /// up to 245 to the stampede's herd.</item>
    /// <item>pull: 60. An implosion pulls for 2 to 8 seconds; one second
    /// in, things are visibly on the move.</item>
    /// <item>herd: 45. The stampede is well in from the map's edge.</item>
    /// <item>blast: 15. The beat plays as the device goes off; a quarter
    /// second in, the fireball is spreading, not yet gone. The pads'
    /// blasts are slower: see <see cref="SceneBeatTicks"/>.</item>
    /// <item>thrown: 10. The grenade is in the air.</item>
    /// </list>
    /// </remarks>
    private static readonly Dictionary<string, int> BeatTicks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "volley", MaxDelayTicks },
            { TourScript.Stragglers, MaxDelayTicks },
            { "pull", 60 },
            { "herd", 45 },
            { "blast", 15 },
            { TourScript.Thrown, 10 },
            { TourScript.ThrownAlone, 10 },
        };

    /// <summary>
    /// Delays for one scene's beat that differ from the moment's own, by
    /// scene and moment.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>retire-replicator's blast: 45. A 75-tile bomb blast spreads
    /// from the pad for about two seconds; at 15 ticks it was a ring ten
    /// tiles across, at 45 it is a fireball about 30 tiles out.</item>
    /// <item>retire-reactor's blast: 100. The core blast is small; the
    /// barrage's three volleys (at 0, 30 and 60 ticks) land in a wave up
    /// to 70 tiles out over about 155 ticks, and at 100 all three are
    /// landing. At 15 the frame showed the core blast alone.</item>
    /// </list>
    /// </remarks>
    private static readonly Dictionary<(string Scene, string Moment), int> SceneBeatTicks =
        new Dictionary<(string Scene, string Moment), int>
        {
            { ("retire-replicator", "blast"), 45 },
            { ("retire-reactor", "blast"), 100 },
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
    public static int DelayTicks(string? cue, string? moment) =>
        DelayTicks(cue, null, moment);

    /// <summary>
    /// The ticks to wait after a cue before its frame is taken, with the
    /// scene's own delay for its beat where it has one.
    /// </summary>
    /// <param name="cue">The cue word: READY, SCENE, BEAT or END.</param>
    /// <param name="scene">The cue's scene key, or null.</param>
    /// <param name="moment">For a beat, its moment; otherwise ignored.</param>
    /// <returns>
    /// As <see cref="DelayTicks(string?, string?)"/>, but a beat the
    /// scene times itself (<see cref="SceneBeatTicks"/>) takes that.
    /// </returns>
    public static int DelayTicks(string? cue, string? scene, string? moment)
    {
        switch (cue)
        {
            case "SCENE":
                return SceneTicks;
            case "BEAT":
                if (scene != null && moment != null
                    && SceneBeatTicks.TryGetValue((scene, moment), out int own))
                {
                    return own;
                }

                return moment != null && BeatTicks.TryGetValue(moment, out int ticks)
                    ? ticks
                    : DefaultBeatTicks;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Whether a cue's frame is taken once bolts land rather than after a
    /// set time: a volley's beat.
    /// </summary>
    /// <param name="cue">The cue word.</param>
    /// <param name="moment">For a beat, its moment.</param>
    /// <returns>
    /// <see cref="AfterLandingTicks"/> for a volley's beat (the frame is
    /// taken that long after the first bolt lands, or at its delay if
    /// none is seen to), otherwise -1.
    /// </returns>
    public static int LandingTicks(string? cue, string? moment) =>
        cue == "BEAT" && (moment == "volley" || moment == TourScript.Stragglers)
            ? AfterLandingTicks
            : -1;
}

/// <summary>
/// The verifier's frames waiting to be taken, first in, first out: the
/// first is taken once its tick has come, and every later one waits for
/// it, so frames are taken in the order their cues came, one a frame. A
/// frame that waits for a landing is taken sooner, a few ticks after the
/// first landing seen once it was asked for. A frame whose tick does not
/// come (the game paused, or the run ending) is taken anyway after
/// <see cref="MaxWaitSeconds"/> of real time, or at once when flushing.
/// </summary>
public sealed class ShotQueue
{
    /// <summary>
    /// The longest a frame waits in real time, in seconds: longer than
    /// the longest delay (<see cref="TourShotTiming.MaxDelayTicks"/>) at
    /// normal speed, even at the 48 ticks a second a presentation runs.
    /// </summary>
    public const double MaxWaitSeconds = 8.0;

    /// <summary>The waiting frames, in the order they were asked for.</summary>
    private readonly Queue<Entry> waiting = new Queue<Entry>();

    /// <summary>Gets how many frames are waiting.</summary>
    public int Count => waiting.Count;

    /// <summary>Asks for a frame.</summary>
    /// <param name="name">The frame's file name.</param>
    /// <param name="dueTick">The game tick from which it may be taken.</param>
    /// <param name="queuedAt">The real time it was asked for, in seconds.</param>
    /// <param name="afterLanding">
    /// Ticks after a landing (<see cref="Landed"/>) the frame may be taken
    /// before its tick, or -1 (the default) if it does not wait for one.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null or empty.
    /// </exception>
    public void Add(string name, int dueTick, double queuedAt, int afterLanding = -1)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A frame needs a name.", nameof(name));
        }

        waiting.Enqueue(new Entry(name, dueTick, queuedAt, afterLanding));
    }

    /// <summary>
    /// Notes that something landed (a bolt): every waiting frame that
    /// waits for a landing and has seen none yet counts it from this tick.
    /// </summary>
    /// <param name="tick">The game tick it was seen on.</param>
    public void Landed(int tick)
    {
        foreach (Entry entry in waiting)
        {
            if (entry.AfterLanding >= 0 && entry.LandedAt < 0)
            {
                entry.LandedAt = tick;
            }
        }
    }

    /// <summary>
    /// Takes the first waiting frame if it is due: its tick has come, a
    /// landing it waits for was long enough ago, it has waited
    /// <see cref="MaxWaitSeconds"/>, or the queue is being flushed. Later
    /// frames never pass it.
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

        Entry first = waiting.Peek();
        bool landed = first.AfterLanding >= 0 && first.LandedAt >= 0
            && nowTick >= first.LandedAt + first.AfterLanding;
        if (!flush && !landed && nowTick < first.DueTick
            && nowSeconds - first.QueuedAt < MaxWaitSeconds)
        {
            return null;
        }

        waiting.Dequeue();
        return first.Name;
    }

    /// <summary>A frame waiting to be taken.</summary>
    private sealed class Entry
    {
        /// <summary>Creates a waiting frame.</summary>
        /// <param name="name">Its file name.</param>
        /// <param name="dueTick">The tick from which it may be taken.</param>
        /// <param name="queuedAt">The real time it was asked for.</param>
        /// <param name="afterLanding">Ticks after a landing, or -1.</param>
        public Entry(string name, int dueTick, double queuedAt, int afterLanding)
        {
            Name = name;
            DueTick = dueTick;
            QueuedAt = queuedAt;
            AfterLanding = afterLanding;
        }

        /// <summary>Gets the frame's file name.</summary>
        public string Name { get; }

        /// <summary>Gets the tick from which it may be taken.</summary>
        public int DueTick { get; }

        /// <summary>Gets the real time it was asked for, in seconds.</summary>
        public double QueuedAt { get; }

        /// <summary>Gets the ticks after a landing it waits, or -1.</summary>
        public int AfterLanding { get; }

        /// <summary>Gets or sets the tick of the landing seen, or -1.</summary>
        public int LandedAt { get; set; } = -1;
    }
}
