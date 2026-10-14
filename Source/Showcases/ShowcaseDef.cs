// Showcase API - the showcase def.
// A ShowcaseDef is how any mod offers a showcase: a fresh, throwaway game
// that demonstrates something, started from the main menu's Showcases
// button (docs/SHOWCASE-API.md). Free of Batshit-specific code, so the API
// can move into a framework mod of its own.

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// One showcase, declared in XML. The menu lists it under the mod that
    /// declared it; Start generates a fresh game of <see cref="mapSize"/>
    /// and runs <see cref="workerClass"/> on it.
    /// </summary>
    public class ShowcaseDef : Def
    {
        /// <summary>
        /// The short description shown in the mod's list; the details page
        /// shows <see cref="Def.description"/>. Blank means the first
        /// paragraph of the description.
        /// </summary>
        [MustTranslate]
        public string summary;

        /// <summary>
        /// The preview image, a texture path; 16 by 9 reads best. Blank
        /// shows no picture.
        /// </summary>
        [NoTranslate]
        public string previewTexPath;

        /// <summary>
        /// About how long the showcase runs, in minutes, counting the
        /// game's generation.
        /// </summary>
        public float estimatedMinutes = 5f;

        /// <summary>Sort order within its mod's list, lowest first.</summary>
        public int order;

        /// <summary>
        /// Package ids the showcase needs (DLCs included). If any is not
        /// active, the showcase is listed greyed out with the reason.
        /// </summary>
        [NoTranslate]
        public List<string> requiredMods = new List<string>();

        /// <summary>
        /// Other mods the showcase is known to have trouble with. The menu
        /// warns only about these, and refuses a fatal one unless forced.
        /// </summary>
        public List<ShowcaseCompatibilityEntry> compatibility =
            new List<ShowcaseCompatibilityEntry>();

        /// <summary>The generated map's width and height, in cells.</summary>
        public int mapSize = 250;

        /// <summary>
        /// The biome to start in, if any. Without one, or if the world has
        /// no tile of it, the start is a random tile, as in a quick test.
        /// </summary>
        public BiomeDef biome;

        /// <summary>
        /// The code that runs the showcase: a <see cref="ShowcaseWorker"/>
        /// subclass. The default, <see cref="ShowcaseDirector"/>, runs
        /// <see cref="scenes"/>.
        /// </summary>
        public Type workerClass = typeof(ShowcaseDirector);

        /// <summary>
        /// For a director: the scenes, in order. Each may be a subclass
        /// (<c>Class="..."</c>).
        /// </summary>
        public List<ShowcaseScene> scenes = new List<ShowcaseScene>();

        /// <summary>
        /// Narration lines, keyed by scene and moment, shown as vanilla
        /// messages.
        /// </summary>
        public List<ShowcaseNarrationLine> narration =
            new List<ShowcaseNarrationLine>();

        /// <summary>The smallest map the API will generate.</summary>
        public const int MinMapSize = 75;

        /// <summary>The largest map the API will generate.</summary>
        public const int MaxMapSize = 400;

        /// <summary>The preview texture, once looked up.</summary>
        [Unsaved]
        private Texture2D previewTex;

        /// <summary>Whether the preview has been looked up.</summary>
        [Unsaved]
        private bool previewLooked;

        /// <summary>
        /// Gets the preview texture, or null if there is none. Loaded on
        /// first use, which must be on the main thread (any UI code is).
        /// </summary>
        public Texture2D PreviewTexture
        {
            get
            {
                if (!previewLooked)
                {
                    previewLooked = true;
                    if (!previewTexPath.NullOrEmpty())
                    {
                        previewTex = ContentFinder<Texture2D>.Get(
                            previewTexPath, reportFailure: false);
                    }
                }

                return previewTex;
            }
        }

        /// <summary>
        /// Gets the short description for the list: <see cref="summary"/>,
        /// or the description's first paragraph.
        /// </summary>
        public string SummaryText
        {
            get
            {
                if (!summary.NullOrEmpty())
                {
                    return summary;
                }

                string text = description ?? string.Empty;
                int cut = text.IndexOf('\n');
                return cut < 0 ? text : text.Substring(0, cut);
            }
        }

        /// <summary>
        /// Gets the package id of the mod that declared this, normalized,
        /// or an empty string for a def made in code.
        /// </summary>
        public string ModPackageId =>
            PackageIds.Normalize(modContentPack?.PackageIdPlayerFacing);

        /// <summary>Gets the clamped map size the game will use.</summary>
        public int MapSizeClamped =>
            Mathf.Clamp(mapSize, MinMapSize, MaxMapSize);

        /// <summary>
        /// This def as the RimWorld-free catalogue sees it.
        /// </summary>
        /// <returns>The entry.</returns>
        public ShowcaseEntry ToEntry() => new ShowcaseEntry(
            defName,
            LabelCap.Resolve(),
            ModPackageId,
            modContentPack?.Name,
            order,
            requiredMods,
            (compatibility ?? new List<ShowcaseCompatibilityEntry>())
                .Where(c => c != null)
                .Select(c => c.ToFlag()));

        /// <summary>
        /// The narration lines as the RimWorld-free script sees them.
        /// </summary>
        /// <returns>The cues, in declared order.</returns>
        public List<NarrationCue> NarrationCues() =>
            (narration ?? new List<ShowcaseNarrationLine>())
                .Where(n => n != null)
                .Select(n => n.ToCue())
                .ToList();

        /// <summary>
        /// Makes a fresh worker for one run of the showcase.
        /// </summary>
        /// <returns>The worker.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="workerClass"/> is not a usable worker.
        /// </exception>
        public ShowcaseWorker MakeWorker()
        {
            if (workerClass == null
                || !typeof(ShowcaseWorker).IsAssignableFrom(workerClass)
                || workerClass.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"{defName}: workerClass {workerClass} is not a "
                    + "concrete ShowcaseWorker.");
            }

            var worker = (ShowcaseWorker)Activator.CreateInstance(workerClass);
            worker.def = this;
            return worker;
        }

        /// <summary>
        /// Checks the def. Runs at startup in dev mode, like every def's.
        /// </summary>
        /// <returns>Each problem found.</returns>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (workerClass == null
                || !typeof(ShowcaseWorker).IsAssignableFrom(workerClass)
                || workerClass.IsAbstract)
            {
                yield return $"workerClass {workerClass} is not a concrete "
                    + "BatshitTechnology.Showcases.ShowcaseWorker";
            }

            if (mapSize < MinMapSize || mapSize > MaxMapSize)
            {
                yield return $"mapSize {mapSize} is outside "
                    + $"{MinMapSize}..{MaxMapSize}";
            }

            if (estimatedMinutes <= 0f)
            {
                yield return "estimatedMinutes must be positive";
            }

            foreach (string id in requiredMods ?? new List<string>())
            {
                if (!PackageIds.IsWellFormed(id))
                {
                    yield return $"requiredMods has a malformed package id '{id}'";
                }
            }

            foreach (ShowcaseCompatibilityEntry entry in
                compatibility ?? new List<ShowcaseCompatibilityEntry>())
            {
                if (entry == null || !PackageIds.IsWellFormed(entry.packageId))
                {
                    yield return "compatibility has an entry without a "
                        + $"well-formed packageId ('{entry?.packageId}')";
                }
            }

            foreach (string error in SceneErrors())
            {
                yield return error;
            }
        }

        /// <summary>
        /// Scene and narration checks: scenes need unique keys, a def with
        /// scenes needs a director, narration must name real scenes when
        /// the def has any, and a "stopped" line must not wait (Stop ends
        /// narration, so a delayed one would never be said).
        /// </summary>
        /// <returns>Each problem found.</returns>
        private IEnumerable<string> SceneErrors()
        {
            List<ShowcaseScene> list = scenes ?? new List<ShowcaseScene>();
            if (list.Count > 0 && workerClass != null
                && !typeof(ShowcaseDirector).IsAssignableFrom(workerClass))
            {
                yield return "has scenes, but its workerClass is not a "
                    + "ShowcaseDirector, so they would never run";
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ShowcaseScene scene in list)
            {
                if (scene == null || scene.key.NullOrEmpty())
                {
                    yield return "has a scene without a key";
                    continue;
                }

                if (!keys.Add(scene.key))
                {
                    yield return $"has two scenes keyed '{scene.key}'";
                }

                foreach (string error in scene.ConfigErrors())
                {
                    yield return $"scene '{scene.key}': {error}";
                }
            }

            if (list.Count > 0)
            {
                foreach (string unknown in NarrationScript.UnknownScenes(
                    NarrationCues(), keys))
                {
                    yield return $"narration names scene '{unknown}', "
                        + "which it does not have";
                }
            }

            foreach (NarrationCue cue in NarrationScript.DroppedAtStop(NarrationCues()))
            {
                yield return $"narration line for scene '{cue.Scene}' at "
                    + $"'stopped' has delayTicks {cue.DelayTicks}, but Stop "
                    + "ends narration, so it would never be said";
            }
        }
    }

    /// <summary>
    /// One entry in a showcase's compatibility list.
    /// </summary>
    public class ShowcaseCompatibilityEntry
    {
        /// <summary>The other mod's package id.</summary>
        [NoTranslate]
        public string packageId;

        /// <summary>How badly the two get on.</summary>
        public ShowcaseSeverity severity = ShowcaseSeverity.Warning;

        /// <summary>What goes wrong, for the player.</summary>
        [MustTranslate]
        public string note;

        /// <summary>This entry as the RimWorld-free policy sees it.</summary>
        /// <returns>The flag.</returns>
        public CompatibilityFlag ToFlag() =>
            new CompatibilityFlag(packageId, severity, note);
    }

    /// <summary>
    /// One narration line: said when its scene reaches its moment, as a
    /// vanilla message pointing at the scene's subject.
    /// </summary>
    public class ShowcaseNarrationLine
    {
        /// <summary>The scene's key (or a plain worker's beat name).</summary>
        [NoTranslate]
        public string scene;

        /// <summary>
        /// The moment: "start" (the default), an outcome ("completed",
        /// "timedOut", "skipped", "stopped", "failed"), or a worker's own
        /// name.
        /// </summary>
        [NoTranslate]
        public string when = NarrationScript.Start;

        /// <summary>
        /// Ticks after the moment before it is said. A "stopped" line is
        /// said only when its delay is 0: Stop ends all narration, so a
        /// delayed one is dropped (the def's configuration check says so).
        /// </summary>
        public int delayTicks;

        /// <summary>The words.</summary>
        [MustTranslate]
        public string text;

        /// <summary>This line as the RimWorld-free script sees it.</summary>
        /// <returns>The cue.</returns>
        public NarrationCue ToCue() =>
            new NarrationCue(scene, when, delayTicks, text);
    }
}
