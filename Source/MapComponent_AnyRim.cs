// Batshit Technology - AnyRim's town record, census and signs.
// One per map (RimWorld makes every MapComponent for every map; this one
// costs nothing until a town is built). It saves where the town stands,
// its seed, its residents and animals by reference, and when a resident
// last died. When the population drops it posts a quiet census message
// (not a letter), unless a showcase is narrating on the map: then the
// narrator reads the census (the tour's raccoon scene), and a vanilla
// message would repeat it in the message stack. There is no sign building
// in RimWorld, so the signs'
// text is drawn over their cells here, at middle zoom or closer, in
// vanilla's neutral label style (magenta is reserved for batshittium).
// The executed plan is not saved: it is rebuilt from the saved seed and
// the saved obstacles it was reduced around, and the label cells (and the
// ground demolition clears) come from it. See docs/SHOWCASE.md, "Signs and
// Labels".

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The map's AnyRim: its record, census and sign text.</summary>
    public class MapComponent_AnyRim : MapComponent
    {
        /// <summary>Ticks between census checks.</summary>
        private const int CensusIntervalTicks = 30;

        /// <summary>Screen-space padding around a sign's text, in UI pixels.</summary>
        private const float LabelPadding = 4f;

        /// <summary>Saved: whether a town stands here.</summary>
        private bool built;

        /// <summary>Saved: the crossroads' map cell.</summary>
        private IntVec3 anchor = IntVec3.Invalid;

        /// <summary>Saved: everything the town touches.</summary>
        private CellRect bounds;

        /// <summary>Saved: the core and firebreak.</summary>
        private CellRect levelled;

        /// <summary>Saved: the plan's seed.</summary>
        private int seed;

        /// <summary>
        /// Saved: the indestructible things the plan was reduced around, as
        /// town-local rectangles (Core boxes cannot be scribed).
        /// </summary>
        private List<CellRect> obstacles = new List<CellRect>();

        /// <summary>
        /// The plan the build executed; rebuilt from the seed and obstacles
        /// after a load, never saved.
        /// </summary>
        private TownPlan plan;

        /// <summary>Set when the plan could not be rebuilt, so the error is logged once.</summary>
        private bool planFailed;

        /// <summary>Saved: the tick a resident last died.</summary>
        private int lastIncidentTick;

        /// <summary>Saved by reference: the residents, in plan order.</summary>
        private List<Pawn> residents = new List<Pawn>();

        /// <summary>Saved by reference: the town's animals.</summary>
        private List<Pawn> animals = new List<Pawn>();

        /// <summary>Label cells and kinds; recomputed after a load, never saved.</summary>
        private List<(IntVec3 Cell, LabelKind Kind)> labels;

        /// <summary>The welcome sign's label cell; recomputed with the labels.</summary>
        private IntVec3 welcomeSign = IntVec3.Invalid;

        /// <summary>The population at the last census, or -1 before the first.</summary>
        private int lastPopulation = -1;

        /// <summary>Set when drawing threw, so the error is logged once.</summary>
        private bool guiFailed;

        /// <summary>Set when the census threw, so the error is logged once.</summary>
        private bool censusFailed;

        /// <summary>Set when the labels could not be recomputed.</summary>
        private bool labelsFailed;

        /// <summary>Created by RimWorld for every map.</summary>
        /// <param name="map">The map.</param>
        public MapComponent_AnyRim(Map map)
            : base(map)
        {
        }

        /// <summary>Whether a town stands on this map.</summary>
        public bool Built => built;

        /// <summary>The map cell of the town's local (0,0), Ground Zero.</summary>
        public IntVec3 Anchor => anchor;

        /// <summary>Everything the town touches, firebreak included.</summary>
        public CellRect Bounds => bounds;

        /// <summary>
        /// The ground the town levelled and built on: its core and
        /// firebreak, without the road tapers. Demolition clears this.
        /// </summary>
        public CellRect LevelledArea => levelled;

        /// <summary>The seed the town was planned with.</summary>
        public int Seed => seed;

        /// <summary>
        /// The plan the build executed: AnyRim at the Medium tier from the
        /// seed, reduced around the saved obstacles exactly as the build
        /// reduced it. Null if no town stands here or the plan could not be
        /// rebuilt (logged once).
        /// </summary>
        internal TownPlan Plan
        {
            get
            {
                if (!built || planFailed)
                {
                    return null;
                }

                if (plan == null)
                {
                    try
                    {
                        TownPlan full = AnyRim.Plan(seed);
                        plan = obstacles.Count > 0
                            ? full.Avoiding(obstacles.Select(AnyRim.ToBox).ToList())
                            : full;
                    }
                    catch (Exception e)
                    {
                        planFailed = true;
                        Log.Error($"[BT] AnyRim could not rebuild its plan: {e}");
                    }
                }

                return plan;
            }
        }

        /// <summary>The tick a resident last died (or the town was built).</summary>
        public int LastIncidentTick => lastIncidentTick;

        /// <summary>The residents, living or dead, in plan order.</summary>
        public IReadOnlyList<Pawn> Residents => residents;

        /// <summary>The town's animals.</summary>
        public IReadOnlyList<Pawn> Animals => animals;

        /// <summary>The welcome sign's label cell, or Invalid.</summary>
        public IntVec3 WelcomeSign
        {
            get
            {
                EnsureLabels();
                return welcomeSign;
            }
        }

        /// <summary>
        /// Living residents on this map. Downed residents, and residents
        /// being carried, still count.
        /// </summary>
        public int Population =>
            residents.Count(p => p != null && !p.Dead && !p.Destroyed && p.MapHeld == map);

        /// <summary>Whole days since a resident last died.</summary>
        public int DaysWithoutIncident =>
            Math.Max(0, (Find.TickManager.TicksGame - lastIncidentTick) / GenDate.TicksPerDay);

        /// <summary>
        /// Records a newly built town. The labels come from the executed
        /// plan directly; after a load the plan is rebuilt from the seed and
        /// the obstacles, and the labels with it.
        /// </summary>
        /// <param name="townAnchor">The crossroads' map cell.</param>
        /// <param name="townBounds">Everything the town touches, map cells.</param>
        /// <param name="townLevelled">The core and firebreak, map cells.</param>
        /// <param name="executed">The plan it was built from, already reduced.</param>
        /// <param name="townObstacles">What the plan was reduced around, town-local.</param>
        /// <param name="townResidents">The residents, in plan order.</param>
        /// <param name="townAnimals">The town's animals.</param>
        internal void Register(
            IntVec3 townAnchor, CellRect townBounds, CellRect townLevelled, TownPlan executed,
            IEnumerable<Box> townObstacles, IEnumerable<Pawn> townResidents,
            IEnumerable<Pawn> townAnimals)
        {
            built = true;
            anchor = townAnchor;
            bounds = townBounds;
            levelled = townLevelled;
            seed = executed.Seed;
            obstacles = (townObstacles ?? Enumerable.Empty<Box>())
                .Select(b => CellRect.FromLimits(b.MinX, b.MinZ, b.MaxX, b.MaxZ))
                .ToList();
            plan = executed;
            planFailed = false;
            lastIncidentTick = Find.TickManager.TicksGame;
            residents = townResidents.Where(p => p != null).ToList();
            animals = townAnimals.Where(p => p != null).ToList();
            labelsFailed = false;
            SetLabels(executed);
            lastPopulation = Population;
            guiFailed = false;
            censusFailed = false;
        }

        /// <summary>Forgets the town (after it has been demolished).</summary>
        internal void Reset()
        {
            built = false;
            anchor = IntVec3.Invalid;
            bounds = default;
            levelled = default;
            residents.Clear();
            animals.Clear();
            obstacles.Clear();
            plan = null;
            planFailed = false;
            labels = null;
            welcomeSign = IntVec3.Invalid;
            lastPopulation = -1;
            labelsFailed = false;
        }

        /// <summary>Recomputes the labels when a saved town is loaded.</summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (built)
            {
                EnsureLabels();
            }
        }

        /// <summary>
        /// Takes the census every half second: when the population drops,
        /// the incident clock restarts and a quiet message says so (left to
        /// the narrator while a showcase narrates on this map).
        /// </summary>
        public override void MapComponentTick()
        {
            if (!built || censusFailed || Find.TickManager.TicksGame % CensusIntervalTicks != 0)
            {
                return;
            }

            try
            {
                int population = Population;
                if (lastPopulation >= 0 && population < lastPopulation)
                {
                    lastIncidentTick = Find.TickManager.TicksGame;
                    string text = AnyRimText.CensusMessage(population);
                    IntVec3 sign = WelcomeSign;
                    // While a showcase narrates here, its narrator reads
                    // the census; a message would repeat it in the stack.
                    if (!ShowcaseNarrating())
                    {
                        if (sign.IsValid)
                        {
                            Messages.Message(
                                text, new LookTargets(sign, map),
                                MessageTypeDefOf.NeutralEvent, false);
                        }
                        else
                        {
                            Messages.Message(text, MessageTypeDefOf.NeutralEvent, false);
                        }
                    }
                }

                lastPopulation = population;
            }
            catch (Exception e)
            {
                censusFailed = true;
                Log.Error($"[BT] AnyRim census stopped: {e}");
            }
        }

        /// <summary>
        /// Whether a showcase is running and narrating on this map: begun,
        /// not ended, and not stopped by the viewer.
        /// </summary>
        /// <returns>True while one is.</returns>
        private bool ShowcaseNarrating()
        {
            GameComponent_ShowcaseSession session = GameComponent_ShowcaseSession.Current;
            ShowcaseWorker worker = session?.Worker;
            return session != null && session.Running && worker != null
                && !worker.Stopped && worker.Map == map;
        }

        /// <summary>
        /// Draws the town's signs: on repaint only, on the map being
        /// viewed, at middle zoom or closer, for labels in view and not
        /// fogged. Text and GUI state are restored whatever happens, and a
        /// failure is logged once and stops further drawing.
        /// </summary>
        public override void MapComponentOnGUI()
        {
            if (!built || guiFailed || Event.current.type != EventType.Repaint || map != Find.CurrentMap)
            {
                return;
            }

            CameraZoomRange zoom = Find.CameraDriver.CurrentZoom;
            if (zoom > CameraZoomRange.Middle)
            {
                return;
            }

            try
            {
                EnsureLabels();
                if (labels == null || labels.Count == 0)
                {
                    return;
                }

                CellRect view = Find.CameraDriver.CurrentViewRect.ExpandedBy(3);
                GameFont font = zoom <= CameraZoomRange.Close ? GameFont.Small : GameFont.Tiny;
                int population = Population;
                int days = DaysWithoutIncident;
                foreach ((IntVec3 cell, LabelKind kind) in labels)
                {
                    if (!view.Contains(cell) || !cell.InBounds(map) || map.fogGrid.IsFogged(cell))
                    {
                        continue;
                    }

                    DrawLabel(cell, AnyRimText.Label(kind, population, days), font);
                }
            }
            catch (Exception e)
            {
                guiFailed = true;
                Log.Error($"[BT] AnyRim signs stopped drawing: {e}");
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Saves the town record. Residents and animals are saved by
        /// reference; destroyed ones are dropped first, and missing ones
        /// after a load. Dead residents stay, so their corpses can be
        /// cleared with the town.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref built, "built");
            Scribe_Values.Look(ref anchor, "anchor", IntVec3.Invalid);
            Scribe_Values.Look(ref bounds, "bounds");
            Scribe_Values.Look(ref levelled, "levelled");
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Collections.Look(ref obstacles, "obstacles", LookMode.Value);
            Scribe_Values.Look(ref lastIncidentTick, "lastIncidentTick");
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                residents?.RemoveAll(p => p == null || p.Destroyed);
                animals?.RemoveAll(p => p == null || p.Destroyed);
            }

            Scribe_Collections.Look(ref residents, "residents", LookMode.Reference);
            Scribe_Collections.Look(ref animals, "animals", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                residents = residents ?? new List<Pawn>();
                animals = animals ?? new List<Pawn>();
                obstacles = obstacles ?? new List<CellRect>(); // older saves have none
                residents.RemoveAll(p => p == null);
                animals.RemoveAll(p => p == null);
                plan = null;
                planFailed = false;
                labels = null;
                welcomeSign = IntVec3.Invalid;
                lastPopulation = -1;
            }
        }

        /// <summary>
        /// Takes the label cells from a plan. A sign the plan dropped (an
        /// obstacle on it, or its building dropped) stays dropped.
        /// </summary>
        /// <param name="executed">The plan the town was built from.</param>
        private void SetLabels(TownPlan executed)
        {
            labels = executed.Labels.Select(l => (AnyRim.ToMap(anchor, l.At), l.Kind)).ToList();
            welcomeSign = AnyRim.ToMap(anchor, executed.WelcomeSign);
        }

        /// <summary>
        /// Recomputes the label cells from the executed plan (rebuilt from
        /// the saved seed and obstacles), once; a failure is logged once and
        /// leaves the signs blank.
        /// </summary>
        private void EnsureLabels()
        {
            if (labels != null || labelsFailed || !built)
            {
                return;
            }

            try
            {
                TownPlan executed = Plan;
                if (executed == null)
                {
                    labelsFailed = true;
                    return;
                }

                SetLabels(executed);
            }
            catch (Exception e)
            {
                labelsFailed = true;
                Log.Error($"[BT] AnyRim could not recompute its signs: {e}");
            }
        }

        /// <summary>
        /// Draws one sign's text just above its cell: vanilla's grey label
        /// background with light, centred text.
        /// </summary>
        /// <param name="cell">The sign's cell.</param>
        /// <param name="text">The text, lines separated by "\n".</param>
        /// <param name="font">The font for the current zoom.</param>
        private static void DrawLabel(IntVec3 cell, string text, GameFont font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Text.Font = font;
            Vector2 size = Text.CalcSize(text);
            Vector2 centre = GenMapUI.LabelDrawPosFor(cell);
            float halfCell = Find.CameraDriver.CellSizePixels * 0.5f;
            var box = new Rect(
                centre.x - (size.x / 2f) - LabelPadding,
                centre.y - halfCell - size.y - (LabelPadding * 2f),
                size.x + (LabelPadding * 2f),
                size.y + LabelPadding);
            GUI.DrawTexture(box, TexUI.GrayTextBG);
            GUI.color = GenMapUI.DefaultThingLabelColor;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(box.x, box.y + (LabelPadding / 2f), box.width, size.y), text);
            GUI.color = Color.white;
        }
    }
}
