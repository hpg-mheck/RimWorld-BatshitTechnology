// Showcase API - the worker base class.
// A ShowcaseWorker is the code side of a showcase: Setup builds what it
// needs once the fresh map exists, Tick runs every game tick, and
// Finished says when it is done. Mods with simple needs subclass it
// directly; ShowcaseDirector subclasses it to run data-driven scenes.

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// The log cues the API writes, for screen-capture tooling, which waits
    /// on cues rather than wall-clock delays (heavy effects slow the game
    /// below real time).
    /// </summary>
    public static class ShowcaseLog
    {
        /// <summary>The prefix of every API cue.</summary>
        public const string Prefix = "[SHOWCASE]";

        /// <summary>Writes one cue: the prefix, a word, and details.</summary>
        /// <param name="cue">The cue word, such as SCENE or END.</param>
        /// <param name="detail">What it concerns; may be empty.</param>
        public static void Cue(string cue, string detail)
        {
            Log.Message(detail.NullOrEmpty()
                ? $"{Prefix} {cue}"
                : $"{Prefix} {cue} {detail}");
        }
    }

    /// <summary>
    /// One narration line as it was said: when, what, and where it
    /// pointed. Kept for tests and capture tooling.
    /// </summary>
    public sealed class NarrationRecord
    {
        /// <summary>Creates a record.</summary>
        /// <param name="tick">The game tick it was said on.</param>
        /// <param name="scene">The scene or beat it belonged to.</param>
        /// <param name="text">The words.</param>
        /// <param name="targets">Where the message pointed; may be null.</param>
        public NarrationRecord(
            int tick, string scene, string text, LookTargets targets)
        {
            Tick = tick;
            Scene = scene ?? string.Empty;
            Text = text ?? string.Empty;
            Targets = targets;
        }

        /// <summary>Gets the tick it was said on.</summary>
        public int Tick { get; }

        /// <summary>Gets the scene or beat it belonged to.</summary>
        public string Scene { get; }

        /// <summary>Gets the words.</summary>
        public string Text { get; }

        /// <summary>Gets where the message pointed, or null.</summary>
        public LookTargets Targets { get; }
    }

    /// <summary>
    /// The code side of a showcase. The API makes one per run, calls
    /// <see cref="Setup"/> once the fresh map has settled, then
    /// <see cref="Tick"/> every game tick until <see cref="Finished"/>.
    /// </summary>
    public class ShowcaseWorker
    {
        /// <summary>The showcase this worker runs.</summary>
        public ShowcaseDef def;

        /// <summary>Lines waiting to be said: due tick, scene, line, target.</summary>
        private readonly List<(int Due, string Scene, string Text, LookTargets At)>
            queued = new List<(int, string, string, LookTargets)>();

        /// <summary>Every line said so far.</summary>
        private readonly List<NarrationRecord> said = new List<NarrationRecord>();

        /// <summary>Gets the map the showcase runs on.</summary>
        public Map Map { get; private set; }

        /// <summary>Gets the game tick <see cref="Setup"/> ran on.</summary>
        public int StartTick { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the viewer stopped the showcase.
        /// </summary>
        public bool Stopped { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the showcase is done. The base
        /// worker does nothing, so it is done at once; subclasses override.
        /// </summary>
        public virtual bool Finished => true;

        /// <summary>
        /// Gets a value indicating whether the caption bar offers Skip.
        /// </summary>
        public virtual bool CanSkip => false;

        /// <summary>
        /// Gets the caption bar's text, or null to show the showcase's
        /// name.
        /// </summary>
        public virtual string Caption => null;

        /// <summary>Gets every narration line said so far, in order.</summary>
        public IReadOnlyList<NarrationRecord> NarrationLog => said;

        /// <summary>
        /// Starts the worker on a map. Called by the API; mods override
        /// <see cref="Setup"/> instead.
        /// </summary>
        /// <param name="map">The showcase's map.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="map"/> is null.
        /// </exception>
        internal void Begin(Map map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            StartTick = Find.TickManager.TicksGame;
            Setup(map);
        }

        /// <summary>
        /// Advances the worker one tick: says any narration that is due,
        /// then calls <see cref="Tick"/>. Called by the API.
        /// </summary>
        internal void DoTick()
        {
            SayDueLines();
            if (!Stopped)
            {
                Tick();
            }
        }

        /// <summary>
        /// Builds what the showcase needs, once the fresh map exists.
        /// </summary>
        /// <param name="map">The showcase's map.</param>
        public virtual void Setup(Map map)
        {
        }

        /// <summary>Runs every game tick until the showcase is finished.</summary>
        public virtual void Tick()
        {
        }

        /// <summary>
        /// Skips the current part, if the worker has parts. The base worker
        /// has none.
        /// </summary>
        public virtual void Skip()
        {
        }

        /// <summary>
        /// Stops the showcase: no more camera moves or narration. The game
        /// keeps running; the colony is the viewer's. Subclasses that
        /// override this must call the base.
        /// </summary>
        public virtual void Stop()
        {
            Stopped = true;
            queued.Clear();
        }

        /// <summary>
        /// Queues the def's narration lines for a scene and moment (see
        /// <see cref="ShowcaseNarrationLine"/>), each said after its delay
        /// as a vanilla message that jumps to <paramref name="targets"/>
        /// when clicked.
        /// </summary>
        /// <param name="scene">The scene's key, or a beat name.</param>
        /// <param name="when">The moment; null means the start.</param>
        /// <param name="targets">Where the messages point; may be null.</param>
        /// <returns>How many lines were queued.</returns>
        public int Narrate(string scene, string when, LookTargets targets)
        {
            if (Stopped || def == null)
            {
                return 0;
            }

            int now = Find.TickManager.TicksGame;
            IReadOnlyList<NarrationCue> lines =
                NarrationScript.LinesFor(def.NarrationCues(), scene, when);
            foreach (NarrationCue line in lines)
            {
                queued.Add((now + line.DelayTicks, scene, line.Text, targets));
            }

            SayDueLines();
            return lines.Count;
        }

        /// <summary>
        /// Says one line at once, as a vanilla message, and records it.
        /// For workers whose words are computed rather than declared.
        /// </summary>
        /// <param name="scene">The scene or beat it belongs to.</param>
        /// <param name="text">The words.</param>
        /// <param name="targets">Where the message points; may be null.</param>
        public void Say(string scene, string text, LookTargets targets)
        {
            if (Stopped || text.NullOrEmpty())
            {
                return;
            }

            try
            {
                if (targets != null && targets.IsValid)
                {
                    Messages.Message(
                        text, targets, MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    Messages.Message(text, MessageTypeDefOf.NeutralEvent);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not show a line: {e}");
            }

            said.Add(new NarrationRecord(
                Find.TickManager.TicksGame, scene, text, targets));
        }

        /// <summary>Says every queued line whose time has come.</summary>
        private void SayDueLines()
        {
            if (queued.Count == 0)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            var due = queued.Where(q => q.Due <= now).ToList();
            queued.RemoveAll(q => q.Due <= now);
            foreach (var line in due)
            {
                Say(line.Scene, line.Text, line.At);
            }
        }
    }
}
