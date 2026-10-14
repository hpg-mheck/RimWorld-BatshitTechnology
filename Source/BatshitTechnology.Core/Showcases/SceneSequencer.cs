using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Showcases;

/// <summary>
/// A scene's timing rules, in game ticks: it runs at least
/// <see cref="MinTicks"/>, ends when its wait-until condition holds (after
/// <see cref="SettleTicks"/> more, for the action to land), and gives up
/// at <see cref="TimeoutTicks"/> if the condition never holds.
/// </summary>
public readonly struct SceneTiming
{
    /// <summary>
    /// Creates timing rules, clamping nonsense: negatives become zero, and
    /// the timeout is at least the minimum and at least one tick.
    /// </summary>
    /// <param name="minTicks">The shortest the scene runs.</param>
    /// <param name="timeoutTicks">When it gives up waiting.</param>
    /// <param name="settleTicks">
    /// How long to hold after the condition holds.
    /// </param>
    public SceneTiming(int minTicks, int timeoutTicks, int settleTicks = 0)
    {
        MinTicks = Math.Max(0, minTicks);
        TimeoutTicks = Math.Max(Math.Max(1, timeoutTicks), MinTicks);
        SettleTicks = Math.Max(0, settleTicks);
    }

    /// <summary>Gets the shortest the scene runs, in ticks.</summary>
    public int MinTicks { get; }

    /// <summary>
    /// Gets the tick, counted from the scene's start, at which it gives up
    /// waiting for its condition.
    /// </summary>
    public int TimeoutTicks { get; }

    /// <summary>
    /// Gets how many ticks the scene holds after its condition first holds.
    /// </summary>
    public int SettleTicks { get; }
}

/// <summary>How a scene ended.</summary>
public enum SceneEnd
{
    /// <summary>
    /// Its condition held, and it ran its minimum and settled.
    /// </summary>
    Completed,

    /// <summary>Its condition never held before the timeout.</summary>
    TimedOut,

    /// <summary>The viewer skipped it.</summary>
    Skipped,

    /// <summary>The viewer stopped the whole showcase during it.</summary>
    Stopped,
}

/// <summary>Where a sequencer is in its scenes.</summary>
public enum SequencerState
{
    /// <summary>No scene has started.</summary>
    NotStarted,

    /// <summary>A scene is running.</summary>
    Running,

    /// <summary>Every scene ended.</summary>
    Finished,

    /// <summary>The viewer stopped it; later scenes never run.</summary>
    Stopped,
}

/// <summary>
/// What one call to the sequencer did: at most one scene ended and at
/// most one began, in that order.
/// </summary>
public sealed class SequencerStep
{
    /// <summary>A step in which nothing happened.</summary>
    public static readonly SequencerStep None =
        new SequencerStep(-1, null, -1, false, false);

    /// <summary>Creates a step.</summary>
    /// <param name="endedScene">The scene that ended, or -1.</param>
    /// <param name="ending">How it ended, or null.</param>
    /// <param name="startedScene">The scene that began, or -1.</param>
    /// <param name="finished">Whether the last scene just ended.</param>
    /// <param name="stopped">
    /// Whether the viewer just stopped the showcase.
    /// </param>
    public SequencerStep(
        int endedScene,
        SceneEnd? ending,
        int startedScene,
        bool finished,
        bool stopped)
    {
        EndedScene = endedScene;
        Ending = ending;
        StartedScene = startedScene;
        Finished = finished;
        Stopped = stopped;
    }

    /// <summary>Gets the index of the scene that ended, or -1.</summary>
    public int EndedScene { get; }

    /// <summary>Gets how that scene ended, or null.</summary>
    public SceneEnd? Ending { get; }

    /// <summary>Gets the index of the scene that began, or -1.</summary>
    public int StartedScene { get; }

    /// <summary>
    /// Gets a value indicating whether the last scene just ended.
    /// </summary>
    public bool Finished { get; }

    /// <summary>
    /// Gets a value indicating whether the showcase was just stopped.
    /// </summary>
    public bool Stopped { get; }

    /// <summary>Gets a value indicating whether anything happened.</summary>
    public bool Any =>
        EndedScene >= 0 || StartedScene >= 0 || Finished || Stopped;
}

/// <summary>
/// The director's state machine: runs scenes in order, one at a time,
/// ending each on its wait-until condition (or its timeout) rather than
/// on a fixed delay, and handles Skip and Stop. It knows only ticks and
/// booleans, so every rule is unit tested without RimWorld.
/// </summary>
public sealed class SceneSequencer
{
    /// <summary>Each scene's timing, in order.</summary>
    private readonly IReadOnlyList<SceneTiming> scenes;

    /// <summary>How each ended scene ended, in order.</summary>
    private readonly List<SceneEnd> endings = new List<SceneEnd>();

    /// <summary>Creates a sequencer.</summary>
    /// <param name="scenes">
    /// Each scene's timing, in order; may be empty.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="scenes"/> is null.
    /// </exception>
    public SceneSequencer(IEnumerable<SceneTiming> scenes)
    {
        if (scenes == null)
        {
            throw new ArgumentNullException(nameof(scenes));
        }

        this.scenes = scenes.ToList();
    }

    /// <summary>Gets how many scenes there are.</summary>
    public int Count => scenes.Count;

    /// <summary>Gets where the sequencer is.</summary>
    public SequencerState State { get; private set; } =
        SequencerState.NotStarted;

    /// <summary>
    /// Gets the index of the running scene; -1 before the first starts,
    /// and the last scene's index once finished or stopped.
    /// </summary>
    public int Current { get; private set; } = -1;

    /// <summary>Gets the tick the running scene began on.</summary>
    public int SceneStartTick { get; private set; }

    /// <summary>
    /// Gets the tick the running scene's condition first held, or -1.
    /// </summary>
    public int ConditionMetTick { get; private set; } = -1;

    /// <summary>Gets how each ended scene ended, in order.</summary>
    public IReadOnlyList<SceneEnd> Endings => endings;

    /// <summary>
    /// Gets a value indicating whether no more scenes will run.
    /// </summary>
    public bool IsOver =>
        State == SequencerState.Finished || State == SequencerState.Stopped;

    /// <summary>Ticks since the running scene began.</summary>
    /// <param name="now">The current tick.</param>
    /// <returns>The elapsed ticks, or 0 when no scene runs.</returns>
    public int Elapsed(int now) =>
        State == SequencerState.Running
            ? Math.Max(0, now - SceneStartTick)
            : 0;

    /// <summary>
    /// Advances one tick. The first call starts scene 0. After that, the
    /// running scene ends <see cref="SceneEnd.Completed"/> once its
    /// condition has held (latched from the first tick it does), it has
    /// run its minimum, and it has settled; or
    /// <see cref="SceneEnd.TimedOut"/> at its timeout if the condition never
    /// held. Once the condition holds, the timeout no longer applies. The
    /// next scene starts in the same step.
    /// </summary>
    /// <param name="now">The current game tick.</param>
    /// <param name="conditionMet">
    /// Whether the running scene's condition holds.
    /// </param>
    /// <returns>What happened.</returns>
    public SequencerStep Advance(int now, bool conditionMet)
    {
        switch (State)
        {
            case SequencerState.NotStarted:
                return StartAt(0, now, -1, null);
            case SequencerState.Running:
                break;
            default:
                return SequencerStep.None;
        }

        SceneTiming timing = scenes[Current];
        int elapsed = now - SceneStartTick;
        if (conditionMet && ConditionMetTick < 0)
        {
            ConditionMetTick = now;
        }

        if (ConditionMetTick >= 0)
        {
            bool settled = now - ConditionMetTick >= timing.SettleTicks;
            return elapsed >= timing.MinTicks && settled
                ? EndCurrent(SceneEnd.Completed, now)
                : SequencerStep.None;
        }

        return elapsed >= timing.TimeoutTicks
            ? EndCurrent(SceneEnd.TimedOut, now)
            : SequencerStep.None;
    }

    /// <summary>
    /// Skips the running scene: it ends <see cref="SceneEnd.Skipped"/> and
    /// the next begins at once. Does nothing unless a scene is running.
    /// </summary>
    /// <param name="now">The current game tick.</param>
    /// <returns>What happened.</returns>
    public SequencerStep Skip(int now) =>
        State == SequencerState.Running
            ? EndCurrent(SceneEnd.Skipped, now)
            : SequencerStep.None;

    /// <summary>
    /// Stops the showcase: the running scene, if any, ends
    /// <see cref="SceneEnd.Stopped"/>, and no later scene runs. Stopping
    /// before the first scene, or twice, only marks it stopped.
    /// </summary>
    /// <param name="now">The current game tick.</param>
    /// <returns>What happened.</returns>
    public SequencerStep Stop(int now)
    {
        if (IsOver)
        {
            return SequencerStep.None;
        }

        int ended = -1;
        SceneEnd? ending = null;
        if (State == SequencerState.Running)
        {
            ended = Current;
            ending = SceneEnd.Stopped;
            endings.Add(SceneEnd.Stopped);
        }

        State = SequencerState.Stopped;
        return new SequencerStep(ended, ending, -1, false, true);
    }

    /// <summary>
    /// Ends the running scene and starts the next, or finishes.
    /// </summary>
    /// <param name="ending">How the scene ended.</param>
    /// <param name="now">The current tick.</param>
    /// <returns>What happened.</returns>
    private SequencerStep EndCurrent(SceneEnd ending, int now)
    {
        int ended = Current;
        endings.Add(ending);
        return StartAt(ended + 1, now, ended, ending);
    }

    /// <summary>
    /// Starts a scene, or finishes if there is none at that index.
    /// </summary>
    /// <param name="index">The scene to start.</param>
    /// <param name="now">The current tick.</param>
    /// <param name="ended">The scene that just ended, or -1.</param>
    /// <param name="ending">How it ended, or null.</param>
    /// <returns>What happened.</returns>
    private SequencerStep StartAt(
        int index, int now, int ended, SceneEnd? ending)
    {
        if (index >= scenes.Count)
        {
            State = SequencerState.Finished;
            Current = scenes.Count - 1;
            return new SequencerStep(ended, ending, -1, true, false);
        }

        State = SequencerState.Running;
        Current = index;
        SceneStartTick = now;
        ConditionMetTick = -1;
        return new SequencerStep(ended, ending, index, false, false);
    }
}
