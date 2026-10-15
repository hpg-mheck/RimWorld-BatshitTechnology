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
        /// <summary>
        /// The prefix of every API cue. It carries the host mod's tag
        /// while the API lives inside Batshit Technology, so capture
        /// tooling waits on one vocabulary ("[BT-SHOWCASE] READY" and so
        /// on); a framework mod split out later gets a prefix of its own.
        /// </summary>
        public const string Prefix = "[BT-SHOWCASE]";

        /// <summary>
        /// The record cues: what a run was (its seed, whether its map's
        /// seeds were pinned, its fingerprints, the storyteller's state),
        /// rather than how it went. Tooling comparing a run's flow of cues
        /// can leave them out.
        /// </summary>
        public static readonly IReadOnlyCollection<string> RecordCues =
            new HashSet<string>
            {
                "SEED", "MAPSEED", "MAPHASH", "STAGEHASH", "STORYTELLER",
            };

        /// <summary>
        /// Raised after each cue is logged, on the thread that logged it
        /// (the generation's cues come from the long event's worker
        /// thread), with the cue word and its detail. A handler that throws
        /// is logged as a warning and does not stop the cue. Capture
        /// tooling in this assembly, such as the tour verifier's shots,
        /// listens here rather than reading the log back.
        /// </summary>
        internal static event Action<string, string> Cued;

        /// <summary>Writes one cue: the prefix, a word, and details.</summary>
        /// <param name="cue">The cue word, such as SCENE or END.</param>
        /// <param name="detail">What it concerns; may be empty.</param>
        public static void Cue(string cue, string detail)
        {
            Log.Message(detail.NullOrEmpty()
                ? $"{Prefix} {cue}"
                : $"{Prefix} {cue} {detail}");
            try
            {
                Cued?.Invoke(cue, detail);
            }
            catch (Exception e)
            {
                Log.Warning($"{Prefix} a listener to the {cue} cue failed: {e.Message}");
            }
        }
    }

    /// <summary>
    /// One narration line as it was said: when, what, and what it was
    /// about. Kept for tests and capture tooling.
    /// </summary>
    public sealed class NarrationRecord
    {
        /// <summary>Creates a record.</summary>
        /// <param name="tick">The game tick it was said on.</param>
        /// <param name="scene">The scene or beat it belonged to.</param>
        /// <param name="text">The words.</param>
        /// <param name="targets">What it was about; may be null.</param>
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

        /// <summary>
        /// Gets what the line was about, or null. The narration overlay
        /// does not use it (the director frames the camera); it is kept
        /// for tests and tooling.
        /// </summary>
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
        /// Gets a value indicating whether narration lines are still
        /// waiting to be said (outcome lines with a delay, say, after the
        /// last scene ended).
        /// </summary>
        internal bool HasQueuedNarration => queued.Count > 0;

        /// <summary>
        /// Gets the game tick the next queued line is due, or null when
        /// none is queued; the overlay fades the line showing out a little
        /// before it.
        /// </summary>
        internal int? NextQueuedTick =>
            queued.Count == 0 ? (int?)null : queued.Min(q => q.Due);

        /// <summary>
        /// Gets or sets the narration overlay lines are shown on; set by
        /// the session before <see cref="Setup"/>. Null shows nothing (the
        /// lines are still recorded).
        /// </summary>
        internal NarrationOverlay Overlay { get; set; }

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
        /// Says the narration lines that have come due, without ticking
        /// the worker. Called by the API after the showcase has finished,
        /// so lines queued at its end are still said.
        /// </summary>
        internal void SayDueNarration()
        {
            SayDueLines();
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
        /// Stops the showcase. Any narration lines still queued are
        /// dropped, and there are no more camera moves or narration
        /// afterwards. The game keeps running; the colony is the viewer's.
        /// Subclasses that override this must call the base.
        /// (<see cref="ShowcaseDirector"/> first ends its running scene as
        /// stopped, which says that scene's "stopped" lines whose delay is
        /// 0; a delayed one is dropped here with the rest.)
        /// </summary>
        public virtual void Stop()
        {
            Stopped = true;
            queued.Clear();
        }

        /// <summary>
        /// Turns on the developer buttons of the devices a showcase stages,
        /// by lending it god mode, but only in dev mode, where those
        /// buttons show at all (<c>DebugSettings.ShowDevGizmos</c>). The
        /// API gives god mode back when the showcase stops or finishes and
        /// on every way out of the showcase game, so it never reaches the
        /// player's own colonies. Call it from <see cref="Setup"/>.
        /// </summary>
        protected void EnableDevButtons()
        {
            ShowcaseGodMode.Lend();
        }

        /// <summary>
        /// Queues the def's narration lines for a scene and moment (see
        /// <see cref="ShowcaseNarrationLine"/>), each said after its delay
        /// on the narration overlay (see <see cref="Say"/>).
        /// </summary>
        /// <param name="scene">The scene's key, or a beat name.</param>
        /// <param name="when">The moment; null means the start.</param>
        /// <param name="targets">What the lines are about; may be null.</param>
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
        /// Says one line at once and records it. For workers whose words
        /// are computed rather than declared. The line goes to the
        /// session's narration overlay, one line at a time, silently; it
        /// never goes to vanilla's messages (docs/SHOWCASE-API.md, "The
        /// Narration Overlay").
        /// </summary>
        /// <param name="scene">The scene or beat it belongs to.</param>
        /// <param name="text">The words.</param>
        /// <param name="targets">What the line is about; may be null.</param>
        public void Say(string scene, string text, LookTargets targets)
        {
            if (Stopped || text.NullOrEmpty())
            {
                return;
            }

            try
            {
                Overlay?.Say(text, NarrationOverlayDrawer.Now);
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
