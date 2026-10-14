// Showcase API - scenes and camera shots for the director.
// A scene does four things (docs/SHOWCASE-API.md): it sets up, frames the
// camera, shows its caption and narration, and runs until it reports that
// it has finished. Scenes are configured in the def's XML; a mod's own
// scene is a subclass named with Class="...".

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// One camera move inside a scene: where to look, how close, and when.
    /// </summary>
    public class ShowcaseShot
    {
        /// <summary>Ticks after the scene starts; 0 frames it at once.</summary>
        public int atTick;

        /// <summary>
        /// Cells from the scene's focus (or the map's centre, with
        /// <see cref="fromMapCenter"/>), as (x,z).
        /// </summary>
        public IntVec2 offset = IntVec2.Zero;

        /// <summary>Measure the offset from the map's centre instead.</summary>
        public bool fromMapCenter;

        /// <summary>
        /// The zoom: the camera's root size, about 11 (closest) to 60
        /// (farthest). Pull back for anything with a big radius.
        /// </summary>
        public float zoom = 24f;

        /// <summary>Glide there (true) or cut straight to it (false).</summary>
        public bool pan = true;

        /// <summary>How long a glide takes, in real seconds.</summary>
        public float panSeconds = 0.6f;

        /// <summary>The closest zoom the API allows.</summary>
        public const float MinZoom = 8f;

        /// <summary>The farthest zoom the API allows.</summary>
        public const float MaxZoom = 80f;

        /// <summary>Gets the zoom, clamped to what the camera can do.</summary>
        public float ZoomClamped => Mathf.Clamp(zoom, MinZoom, MaxZoom);
    }

    /// <summary>
    /// One scene of a directed showcase. The base class is usable as it
    /// is: it frames <see cref="focusOffset"/>, says its lines, and holds
    /// for <see cref="minTicks"/>. Subclasses build things in
    /// <see cref="Setup"/>, act in <see cref="Tick"/>, and report through
    /// <see cref="IsDone"/> when their action has played out, so the
    /// director waits on events rather than fixed delays.
    /// </summary>
    public class ShowcaseScene
    {
        /// <summary>
        /// The scene's key: its log cue (SCENE key) and the key its
        /// narration lines use.
        /// </summary>
        [NoTranslate]
        public string key;

        /// <summary>The caption bar's title for this scene.</summary>
        [MustTranslate]
        public string title;

        /// <summary>The shortest the scene runs, in ticks.</summary>
        public int minTicks = 120;

        /// <summary>
        /// When the scene gives up waiting for <see cref="IsDone"/>, in
        /// ticks from its start.
        /// </summary>
        public int timeoutTicks = 3600;

        /// <summary>
        /// How long to hold once <see cref="IsDone"/> first holds, in
        /// ticks, so the end of the action stays on screen.
        /// </summary>
        public int settleTicks;

        /// <summary>
        /// Where the scene looks when it has no <see cref="Subject"/>:
        /// cells from the map's centre, as (x,z).
        /// </summary>
        public IntVec2 focusOffset = IntVec2.Zero;

        /// <summary>Select the subject, so its buttons and status show.</summary>
        public bool selectSubject = true;

        /// <summary>The camera moves, by tick.</summary>
        public List<ShowcaseShot> shots = new List<ShowcaseShot>();

        /// <summary>The director running this copy of the scene.</summary>
        [Unsaved]
        private ShowcaseDirector director;

        /// <summary>Gets the director running this scene.</summary>
        public ShowcaseDirector Director => director;

        /// <summary>Gets the showcase's map, or null before it runs.</summary>
        public Map Map => director?.Map;

        /// <summary>
        /// Gets the thing the scene is about, if any: framed, selected and
        /// pointed at by its narration. The base scene has none.
        /// </summary>
        public virtual Thing Subject => null;

        /// <summary>
        /// Gets where the scene looks: its subject, if spawned on the map,
        /// else <see cref="focusOffset"/> from the map's centre.
        /// </summary>
        public virtual IntVec3 FocusCell
        {
            get
            {
                Map map = Map;
                if (map == null)
                {
                    return IntVec3.Invalid;
                }

                Thing subject = Subject;
                if (subject != null && subject.Spawned && subject.Map == map)
                {
                    return subject.Position;
                }

                IntVec3 cell = map.Center
                    + new IntVec3(focusOffset.x, 0, focusOffset.z);
                return cell.ClampInsideMap(map);
            }
        }

        /// <summary>
        /// Gets where the scene's narration points: the subject, or the
        /// focus cell.
        /// </summary>
        public virtual LookTargets LookTargets
        {
            get
            {
                Thing subject = Subject;
                if (subject != null && subject.Spawned)
                {
                    return new LookTargets(subject);
                }

                IntVec3 cell = FocusCell;
                return cell.IsValid && Map != null
                    ? new LookTargets(cell, Map)
                    : null;
            }
        }

        /// <summary>Gets the scene's timing, for the sequencer.</summary>
        public SceneTiming Timing =>
            new SceneTiming(minTicks, timeoutTicks, settleTicks);

        /// <summary>
        /// Makes this run's copy of the scene, so state set while running
        /// never touches the def. Subclasses holding mutable lists or
        /// objects override this to copy them as well.
        /// </summary>
        /// <returns>The copy.</returns>
        public virtual ShowcaseScene CloneForRun() =>
            (ShowcaseScene)MemberwiseClone();

        /// <summary>Attaches this copy to the director running it.</summary>
        /// <param name="runner">The director.</param>
        internal void Attach(ShowcaseDirector runner)
        {
            director = runner;
        }

        /// <summary>
        /// Builds what the scene needs. Runs before the camera moves and
        /// before the caption and narration.
        /// </summary>
        public virtual void Setup()
        {
        }

        /// <summary>Runs every tick while the scene is current.</summary>
        public virtual void Tick()
        {
        }

        /// <summary>
        /// The wait-until condition: whether the scene's action has played
        /// out. The base scene has no action, so it holds only for
        /// <see cref="minTicks"/>.
        /// </summary>
        /// <returns>True when the scene may end.</returns>
        public virtual bool IsDone() => true;

        /// <summary>
        /// Called once when the scene ends, however it ended.
        /// </summary>
        /// <param name="ending">How it ended.</param>
        public virtual void End(SceneEnd ending)
        {
        }

        /// <summary>
        /// The narration moment for an ending. Subclasses can read the
        /// result instead ("the raccoon survived"), so the narrator speaks
        /// to what happened rather than to a script.
        /// </summary>
        /// <param name="ending">How the scene ended.</param>
        /// <returns>The narration moment key.</returns>
        public virtual string OutcomeKey(SceneEnd ending) =>
            NarrationScript.OutcomeKey(ending);

        /// <summary>Checks the scene's configuration.</summary>
        /// <returns>Each problem found.</returns>
        public virtual IEnumerable<string> ConfigErrors()
        {
            if (minTicks < 0 || timeoutTicks < 1 || settleTicks < 0)
            {
                yield return "minTicks and settleTicks must not be negative, "
                    + "and timeoutTicks must be positive";
            }

            if (timeoutTicks < minTicks)
            {
                yield return $"timeoutTicks {timeoutTicks} is below "
                    + $"minTicks {minTicks}";
            }

            foreach (ShowcaseShot shot in shots ?? new List<ShowcaseShot>())
            {
                if (shot == null)
                {
                    yield return "has an empty shot";
                }
                else if (shot.zoom < ShowcaseShot.MinZoom
                    || shot.zoom > ShowcaseShot.MaxZoom)
                {
                    yield return $"a shot's zoom {shot.zoom} is outside "
                        + $"{ShowcaseShot.MinZoom}..{ShowcaseShot.MaxZoom}";
                }
            }
        }
    }
}
