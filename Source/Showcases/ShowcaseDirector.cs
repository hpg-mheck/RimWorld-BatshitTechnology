// Showcase API - the director.
// A ShowcaseWorker that runs the def's scenes in order, one at a time
// (docs/SHOWCASE-API.md, "The API"): each sets up, frames the camera,
// shows its caption and narration, and runs until its wait-until
// condition holds or it times out. The Core SceneSequencer decides when
// scenes end; a Core BeatSchedule hands out each scene's camera shots.
// The director only executes those decisions, so its rules are unit
// tested without RimWorld.

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Runs a showcase as a directed tour of data-driven scenes, with a
    /// caption ("2 of 8: ..."), Skip, Stop and a log cue per scene.
    /// </summary>
    public class ShowcaseDirector : ShowcaseWorker
    {
        /// <summary>The zoom of a scene's implicit opening shot.</summary>
        private const float DefaultZoom = 24f;

        /// <summary>This run's copies of the def's scenes, in order.</summary>
        private readonly List<ShowcaseScene> scenes = new List<ShowcaseScene>();

        /// <summary>Each scene's shots, as run (an implicit one if none).</summary>
        private readonly List<List<ShowcaseShot>> shots =
            new List<List<ShowcaseShot>>();

        /// <summary>Scenes whose Tick or IsDone threw, by index.</summary>
        private readonly HashSet<int> faulted = new HashSet<int>();

        /// <summary>Decides when scenes begin and end.</summary>
        private SceneSequencer sequencer;

        /// <summary>The running scene's shot schedule, or null.</summary>
        private BeatSchedule beats;

        /// <summary>Gets the scenes as this run sees them.</summary>
        public IReadOnlyList<ShowcaseScene> Scenes => scenes;

        /// <summary>
        /// Gets the running scene, or null before the first and after the
        /// last.
        /// </summary>
        public ShowcaseScene CurrentScene =>
            sequencer != null && sequencer.State == SequencerState.Running
                ? scenes[sequencer.Current]
                : null;

        /// <summary>
        /// Gets the sequencer's state, for tests and capture tooling.
        /// </summary>
        public SequencerState State =>
            sequencer?.State ?? SequencerState.NotStarted;

        /// <summary>
        /// Gets how each scene that has ended so far ended, in order, for
        /// tests and capture tooling.
        /// </summary>
        public IReadOnlyList<SceneEnd> Endings =>
            sequencer?.Endings ?? Array.Empty<SceneEnd>();

        /// <summary>
        /// Gets a value indicating whether every scene has ended or the
        /// viewer stopped the tour.
        /// </summary>
        public override bool Finished => sequencer != null && sequencer.IsOver;

        /// <summary>
        /// Gets a value indicating whether Skip is offered: while a scene
        /// runs.
        /// </summary>
        public override bool CanSkip => !Stopped && CurrentScene != null;

        /// <summary>
        /// Gets the caption bar's text, "2 of 8: The Batshittium Reactor",
        /// from the translatable pattern; null between scenes.
        /// </summary>
        public override string Caption
        {
            get
            {
                ShowcaseScene scene = CurrentScene;
                if (scene == null)
                {
                    return null;
                }

                string title = scene.title.NullOrEmpty()
                    ? def?.LabelCap.Resolve()
                    : scene.title;
                return CaptionText.Format(
                    "BT_Showcases_CaptionPattern".Translate().Resolve(),
                    sequencer.Current,
                    sequencer.Count,
                    title);
            }
        }

        /// <summary>
        /// Makes this run's copies of the def's scenes. The first scene
        /// starts on the first tick.
        /// </summary>
        /// <param name="map">The showcase's map.</param>
        public override void Setup(Map map)
        {
            scenes.Clear();
            shots.Clear();
            faulted.Clear();
            foreach (ShowcaseScene template in
                def?.scenes ?? new List<ShowcaseScene>())
            {
                if (template == null)
                {
                    continue;
                }

                ShowcaseScene scene = template.CloneForRun();
                scene.Attach(this);
                scenes.Add(scene);
                List<ShowcaseShot> list = (scene.shots ?? new List<ShowcaseShot>())
                    .Where(s => s != null)
                    .ToList();
                if (list.Count == 0)
                {
                    list.Add(new ShowcaseShot { atTick = 0, zoom = DefaultZoom });
                }

                shots.Add(list);
            }

            sequencer = new SceneSequencer(scenes.Select(s => s.Timing));
        }

        /// <summary>
        /// Runs the current scene for one tick, frames any shot that has
        /// come due, and lets the sequencer decide whether it ends.
        /// </summary>
        public override void Tick()
        {
            if (sequencer == null || sequencer.IsOver)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            bool conditionMet = false;
            if (sequencer.State == SequencerState.Running)
            {
                int index = sequencer.Current;
                RunSceneTick(index);
                FrameDueShots(index, now);
                conditionMet = SceneIsDone(index);
            }

            Apply(sequencer.Advance(now, conditionMet), now);
        }

        /// <summary>
        /// Skips the running scene; the next begins at once.
        /// </summary>
        public override void Skip()
        {
            if (sequencer == null || Stopped)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            Apply(sequencer.Skip(now), now);
        }

        /// <summary>
        /// Stops the tour: the running scene ends as stopped, and no later
        /// scene runs. The camera and the colony are the viewer's.
        /// </summary>
        public override void Stop()
        {
            if (sequencer != null && !Stopped)
            {
                int now = Find.TickManager.TicksGame;
                Apply(sequencer.Stop(now), now);
            }

            base.Stop();
        }

        /// <summary>
        /// Carries out one sequencer step: ends a scene and begins the
        /// next. The session logs the end of the tour (FINISHED).
        /// </summary>
        /// <param name="step">What the sequencer did.</param>
        /// <param name="now">The current tick.</param>
        private void Apply(SequencerStep step, int now)
        {
            if (step == null || !step.Any)
            {
                return;
            }

            if (step.EndedScene >= 0 && step.Ending.HasValue)
            {
                EndScene(step.EndedScene, step.Ending.Value);
            }

            if (step.StartedScene >= 0)
            {
                BeginScene(step.StartedScene, now);
            }
        }

        /// <summary>
        /// Begins a scene: sets it up, logs its cue, selects its subject,
        /// frames its opening shots and says its opening lines. A scene
        /// whose set-up throws is skipped, with the reason logged.
        /// </summary>
        /// <param name="index">The scene's index.</param>
        /// <param name="now">The current tick.</param>
        private void BeginScene(int index, int now)
        {
            ShowcaseScene scene = scenes[index];
            beats = new BeatSchedule(shots[index].Select(s => s.atTick));
            try
            {
                scene.Setup();
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} scene '{scene.key}' of "
                    + $"{def?.defName} could not set up: {e}");
                ShowcaseLog.Cue("SKIPPED", $"{scene.key}: set-up failed");
                Apply(sequencer.Skip(now), now);
                return;
            }

            ShowcaseLog.Cue("SCENE", scene.key);
            SelectSubject(scene);
            FrameDueShots(index, now);
            Narrate(scene.key, NarrationScript.Start, SafeLookTargets(scene));
        }

        /// <summary>
        /// Ends a scene: tells it how it ended, logs the cue and says its
        /// outcome lines.
        /// </summary>
        /// <param name="index">The scene's index.</param>
        /// <param name="ending">How it ended.</param>
        private void EndScene(int index, SceneEnd ending)
        {
            ShowcaseScene scene = scenes[index];
            string outcome = NarrationScript.OutcomeKey(ending);
            try
            {
                scene.End(ending);
                outcome = scene.OutcomeKey(ending) ?? outcome;
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} scene '{scene.key}' of "
                    + $"{def?.defName} failed while ending: {e}");
            }

            ShowcaseLog.Cue("END", $"{scene.key} {outcome}");
            Narrate(scene.key, outcome, SafeLookTargets(scene));
            beats = null;
        }

        /// <summary>
        /// Runs a scene's own tick. A scene that throws is logged once and
        /// treated as finished, so one bad scene cannot stall the tour.
        /// </summary>
        /// <param name="index">The scene's index.</param>
        private void RunSceneTick(int index)
        {
            if (faulted.Contains(index))
            {
                return;
            }

            try
            {
                scenes[index].Tick();
            }
            catch (Exception e)
            {
                Fault(index, "tick", e);
            }
        }

        /// <summary>
        /// Asks a scene whether its action has played out; a faulted scene
        /// always has.
        /// </summary>
        /// <param name="index">The scene's index.</param>
        /// <returns>True when the scene may end.</returns>
        private bool SceneIsDone(int index)
        {
            if (faulted.Contains(index))
            {
                return true;
            }

            try
            {
                return scenes[index].IsDone();
            }
            catch (Exception e)
            {
                Fault(index, "IsDone", e);
                return true;
            }
        }

        /// <summary>Records a scene's failure, logging it once.</summary>
        /// <param name="index">The scene's index.</param>
        /// <param name="where">What it was doing.</param>
        /// <param name="e">The exception.</param>
        private void Fault(int index, string where, Exception e)
        {
            if (faulted.Add(index))
            {
                Log.Error($"{ShowcaseLog.Prefix} scene '{scenes[index].key}' "
                    + $"of {def?.defName} threw in {where}: {e}");
            }
        }

        /// <summary>Frames every shot of the scene that has come due.</summary>
        /// <param name="index">The scene's index.</param>
        /// <param name="now">The current tick.</param>
        private void FrameDueShots(int index, int now)
        {
            if (beats == null || Stopped)
            {
                return;
            }

            foreach (int i in beats.TakeDue(sequencer.Elapsed(now)))
            {
                Frame(scenes[index], shots[index][i]);
            }
        }

        /// <summary>
        /// Moves the camera for one shot, if the viewer is looking at the
        /// showcase's map: a glide or a cut, to the scene's focus (or the
        /// map's centre) plus the shot's offset, at the shot's zoom.
        /// </summary>
        /// <param name="scene">The scene.</param>
        /// <param name="shot">The shot.</param>
        private void Frame(ShowcaseScene scene, ShowcaseShot shot)
        {
            Map map = Map;
            if (map == null || Find.CurrentMap != map
                || WorldRendererUtility.WorldRendered)
            {
                return;
            }

            try
            {
                IntVec3 origin = shot.fromMapCenter ? map.Center : scene.FocusCell;
                if (!origin.IsValid)
                {
                    origin = map.Center;
                }

                IntVec3 cell = (origin + new IntVec3(shot.offset.x, 0, shot.offset.z))
                    .ClampInsideMap(map);
                CameraDriver camera = Find.CameraDriver;
                FloatRange range = camera.config.sizeRange;
                float zoom = Mathf.Clamp(shot.ZoomClamped, range.min, range.max);
                if (shot.pan && shot.panSeconds > 0f)
                {
                    camera.PanToMapLocAndSize(
                        cell.ToVector3Shifted(), zoom, shot.panSeconds);
                }
                else
                {
                    camera.JumpToCurrentMapLoc(cell);
                    camera.SetRootSize(zoom);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not frame scene "
                    + $"'{scene.key}': {e.Message}");
            }
        }

        /// <summary>
        /// Selects the scene's subject, so its buttons and inspect text
        /// show while it is explained.
        /// </summary>
        /// <param name="scene">The scene.</param>
        private static void SelectSubject(ShowcaseScene scene)
        {
            if (!scene.selectSubject)
            {
                return;
            }

            try
            {
                Thing subject = scene.Subject;
                if (subject != null && subject.Spawned
                    && subject.Map == Find.CurrentMap)
                {
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(subject, playSound: false);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not select the "
                    + $"subject of scene '{scene.key}': {e.Message}");
            }
        }

        /// <summary>
        /// A scene's look targets, or null if computing them throws.
        /// </summary>
        /// <param name="scene">The scene.</param>
        /// <returns>The targets, or null.</returns>
        private static LookTargets SafeLookTargets(ShowcaseScene scene)
        {
            try
            {
                return scene.LookTargets;
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} scene '{scene.key}' has "
                    + $"no usable look target: {e.Message}");
                return null;
            }
        }
    }
}
