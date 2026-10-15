// Batshit Technology - the tour verifier's camera checks (slice 19000d;
// docs/SHOWCASE.md, "Camera and Framing"). Every frame until FINISHED,
// while a scene runs: how far the camera's view reaches beyond the map's
// edge (the stampede's herd enters at the east edge, the pads and the
// yard lie near others). One frame after the director frames a scene's
// opening shot: the camera is there, at its zoom, by a cut, so the scene
// never opens on the last scene's place. One frame after the raccoon's
// thrown beat: Ned, the raccoon and the grenade are on screen, clear of
// the caption bar and the narration. At each scene's end: a fact on each.
// The SUMMARY's "camera" holds the counts.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The tour verifier's camera checks.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>The tour watch's camera measures.</summary>
        private sealed partial class TourWatch
        {
            /// <summary>
            /// How far beyond the map's edge a view may reach before the
            /// frame counts as showing the black beyond it, in tiles: the
            /// camera's shake and the half tile a framed cell's centre
            /// adds.
            /// </summary>
            private const double OffMapSlackTiles = 1.5;

            /// <summary>
            /// How far from an opening shot's centre the camera may be one
            /// frame after it, in tiles: a cut lands at once (a glide from
            /// the last scene's place would still be far off).
            /// </summary>
            private const float OpeningToleranceTiles = 2f;

            /// <summary>The last camera move seen, or null.</summary>
            private ShowcaseFraming framingSeen;

            /// <summary>Frames since <see cref="framingSeen"/> was seen.</summary>
            private int framingFrames;

            /// <summary>
            /// Each scene's opening, judged a frame after it was framed:
            /// null when it held, otherwise what was wrong; by key.
            /// </summary>
            private readonly Dictionary<string, string> openings =
                new Dictionary<string, string>();

            /// <summary>
            /// Each scene's frames sampled, frames beyond the map's edge,
            /// and the farthest reach beyond it, by key.
            /// </summary>
            private readonly Dictionary<string, (int Frames, int Off, double Worst)> edges =
                new Dictionary<string, (int Frames, int Off, double Worst)>();

            /// <summary>
            /// The throw's framing, judged a frame after it: null until
            /// then, then what was seen.
            /// </summary>
            private string throwSeen;

            /// <summary>Whether the throw's framing held, once judged.</summary>
            private bool throwHeld;

            /// <summary>
            /// Samples the camera once a frame, until FINISHED: how far its
            /// view reaches beyond the map, and, a frame after the director
            /// moves it, whether it went where it was sent.
            /// </summary>
            public void SampleCameraFrame()
            {
                Map map = Director.Map;
                if (FinishedTick >= 0 || map == null || Find.CurrentMap != map
                    || UI.screenHeight <= 0 || Find.Camera == null)
                {
                    return;
                }

                Vector3 at = Find.Camera.transform.position;
                float size = Find.CameraDriver.RootSize;
                double aspect = (double)UI.screenWidth / UI.screenHeight;
                ShowcaseScene scene = Director.CurrentScene;
                if (scene != null)
                {
                    double over = ShotFraming.Overhang(
                        new Spot(at.x, at.z), size, aspect, map.Size.x, map.Size.z);
                    edges.TryGetValue(scene.key, out var e);
                    edges[scene.key] = (e.Frames + 1,
                        e.Off + (over > OffMapSlackTiles ? 1 : 0),
                        Math.Max(e.Worst, over));
                }

                ShowcaseFraming framing = Director.LastFraming;
                if (framing == null)
                {
                    return;
                }

                if (!ReferenceEquals(framing, framingSeen))
                {
                    framingSeen = framing;
                    framingFrames = 0;
                    return; // judged on the next frame, once the camera has drawn
                }

                if (++framingFrames != 1)
                {
                    return;
                }

                if (framing.Beat == null && !openings.ContainsKey(framing.Scene))
                {
                    JudgeOpening(framing, at, size);
                }

                if (framing.Scene == "raccoon" && framing.Beat == TourScript.Thrown
                    && throwSeen == null)
                {
                    JudgeThrow(at, size, aspect);
                }
            }

            /// <summary>
            /// Judges a scene's opening shot a frame after it: the camera
            /// cut there, and is at its place and zoom.
            /// </summary>
            /// <param name="framing">The opening shot.</param>
            /// <param name="at">Where the camera is.</param>
            /// <param name="size">Its root size.</param>
            private void JudgeOpening(ShowcaseFraming framing, Vector3 at, float size)
            {
                float off = new Vector2(at.x - framing.X, at.z - framing.Z).magnitude;
                bool held = framing.Cut && off <= OpeningToleranceTiles
                    && Mathf.Abs(size - framing.Zoom) <= 0.5f;
                openings[framing.Scene] = held
                    ? null
                    : $"{(framing.Cut ? "a cut" : "a glide")} to ({framing.X:F0}, "
                        + $"{framing.Z:F0}) at {framing.Zoom:F0}; a frame later the "
                        + $"camera was {off:F1} tiles off, at {size:F1}";
            }

            /// <summary>
            /// Judges the throw's shot a frame after it: Ned, the raccoon
            /// and the grenade (in the air, and where it is going) each on
            /// screen and clear of the caption bar and the narration.
            /// </summary>
            /// <param name="at">Where the camera is.</param>
            /// <param name="size">Its root size.</param>
            /// <param name="aspect">The screen's width over its height.</param>
            private void JudgeThrow(Vector3 at, float size, double aspect)
            {
                if (!(Director.CurrentScene is RaccoonScene incident))
                {
                    throwSeen = "the raccoon scene was not showing";
                    return;
                }

                List<Spot> subjects = incident.ThrowSubjects();
                var camera = new Spot(at.x, at.z);
                var outside = new List<string>();
                foreach (Spot s in subjects)
                {
                    (double x, double y) = ShotFraming.OnScreen(s, camera, size, aspect);
                    if (!ShotFraming.InClearView((x, y)))
                    {
                        outside.Add($"({s.X:F0}, {s.Z:F0}) at {x:P0} across, {y:P0} down");
                    }
                }

                throwHeld = subjects.Count > 0 && outside.Count == 0;
                throwSeen = $"{subjects.Count} things, camera ({at.x:F0}, {at.z:F0}) at "
                    + $"{size:F1}; outside the clear view: [{string.Join("; ", outside)}]";
            }

            /// <summary>
            /// The camera's facts at a scene's end: it opened on the scene's
            /// place by a cut, never showed beyond the map's edge, and (for
            /// the raccoon) framed the throw clear of the overlays.
            /// </summary>
            /// <param name="scene">The scene that ended.</param>
            private void CameraFacts(ShowcaseScene scene)
            {
                bool judged = openings.TryGetValue(scene.key, out string wrong);
                Fact(scene, "the camera cut to the scene's place as it opened ("
                        + (judged ? wrong ?? "it did" : "not judged") + ")",
                    judged && wrong == null);
                edges.TryGetValue(scene.key, out var e);
                Fact(scene, $"the camera kept on the map ({e.Off} of {e.Frames} frames "
                        + $"beyond its edge, at most {e.Worst:F1} tiles)",
                    e.Off == 0);
                if (scene is RaccoonScene && throwSeen != null)
                {
                    Fact(scene, "the throw's shot showed Ned, the raccoon and the "
                            + $"grenade clear of the caption and narration ({throwSeen})",
                        throwHeld);
                }
            }

            /// <summary>Adds the camera's counts to the SUMMARY.</summary>
            /// <param name="summary">The SUMMARY.</param>
            private void AddCameraTo(JsonLine summary)
            {
                summary.Add("camera", new JsonLine
                {
                    { "openingsMissed", openings.Where(o => o.Value != null)
                        .Select(o => $"{o.Key}: {o.Value}").ToList() },
                    { "offMapFrames", edges.Values.Sum(e => e.Off) },
                    { "framesSampled", edges.Values.Sum(e => e.Frames) },
                    { "worstOverhang", Math.Round(
                        edges.Values.Select(e => e.Worst).DefaultIfEmpty(0).Max(), 1) },
                    { "throwFramed", throwSeen == null ? (object)null : throwHeld },
                });
            }
        }
    }
}
