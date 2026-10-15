// Batshit Technology - the Tractor Bomb (docs/TRACTOR-BOMB.md).
// An expendable robot, a little steel farm tractor with a magenta
// tractor-beam emitter where the exhaust stack should be. It patrols its
// allowed area, spots a hostile it can see, closes to within 15 tiles
// (or nearer, as its Proximity says),
// protests for three seconds if a colonist is inside its pull radius, and
// emplaces itself: the robot leaves the map and an emplaced bomb
// (TractorBombEmplaced.cs) takes its place, counts down and implodes.
// Destroyed before it emplaces, it gets a death of its own
// (TractorBombDeath.cs). Its think tree and jobs are in TractorBombAI.cs,
// its gizmos in TractorBombGizmos.cs.
//
// It is a pawn of its own race, not a mechanoid: its flesh type is its
// own, so RaceProperties.IsMechanoid is false and Biotech's mechanitor,
// bandwidth and overseer rules never apply. It has no needs and no mood.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace BatshitTechnology
{
    /// <summary>The Tractor Bomb's defs, bound by RimWorld at load.</summary>
    [DefOf]
    public static class BT_TractorBombDefOf
    {
        /// <summary>The robot's race.</summary>
        public static ThingDef BT_TractorBomb;

        /// <summary>The bomb a robot becomes when it emplaces.</summary>
        public static ThingDef BT_TractorBombEmplaced;

        /// <summary>A robot's death: a sputtering miniature vortex.</summary>
        public static ThingDef BT_TractorBombDeathVortex;

        /// <summary>
        /// Wandering its allowed area, looking for hostiles.
        /// </summary>
        public static JobDef BT_TractorBombPatrol;

        /// <summary>Closing to within range of a hostile.</summary>
        public static JobDef BT_TractorBombApproach;

        /// <summary>Protesting if need be, then emplacing.</summary>
        public static JobDef BT_TractorBombEmplace;

        /// <summary>
        /// Anyone else: sprinting clear of an armed bomb
        /// (TractorBombFlee.cs).
        /// </summary>
        public static JobDef BT_TractorBombFlee;

        /// <summary>
        /// Anyone else, too close to run: frozen, making peace
        /// (TractorBombFlee.cs).
        /// </summary>
        public static JobDef BT_TractorBombFreeze;

        /// <summary>The project that unlocks the bomb.</summary>
        public static ResearchProjectDef BT_Research_TractorBomb;

        /// <summary>The project that unlocks the yield sliders.</summary>
        public static ResearchProjectDef BT_Research_TractorBombDialAYield;

        /// <summary>Lets RimWorld bind the fields above at load.</summary>
        static BT_TractorBombDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_TractorBombDefOf));
        }
    }

    /// <summary>
    /// The Tractor Bomb's pawn kind, apart from its other defs because it
    /// shares the race's defName and a DefOf binds by field name.
    /// </summary>
    [DefOf]
    public static class BT_TractorBombKindDefOf
    {
        /// <summary>The robot's pawn kind.</summary>
        public static PawnKindDef BT_TractorBomb;

        /// <summary>Lets RimWorld bind the field above at load.</summary>
        static BT_TractorBombKindDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(
                typeof(BT_TractorBombKindDefOf));
        }
    }

    /// <summary>
    /// The Tractor Bomb's extra material and icons, loaded at startup on
    /// the main thread, as RimWorld requires of every static asset.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BT_TractorBombAssets
    {
        /// <summary>The protest lamp's glow, tinted red or yellow.</summary>
        public const string LampPath = "Things/Mote/BT_TractorBombLamp";

        /// <summary>
        /// The flip frames, from tyres upright to almost flat.
        /// </summary>
        public static readonly string[] FlipFramePaths =
        {
            "Things/Building/Security/BT_TractorBombEmplaced_Flip1",
            "Things/Building/Security/BT_TractorBombEmplaced_Flip2",
            "Things/Building/Security/BT_TractorBombEmplaced_Flip3",
        };

        /// <summary>The parts a dying robot's vortex sucks in.</summary>
        public static readonly string[] PartPaths =
        {
            "Things/Mote/BT_TractorBombPart_Wheel",
            "Things/Mote/BT_TractorBombPart_Wheel",
            "Things/Mote/BT_TractorBombPart_Wheel",
            "Things/Mote/BT_TractorBombPart_Wheel",
            "Things/Mote/BT_TractorBombPart_Hood",
            "Things/Mote/BT_TractorBombPart_Emitter",
        };

        /// <summary>The protest lamp, lit red.</summary>
        public static readonly Material LampRed = MaterialPool.MatFrom(
            LampPath, ShaderDatabase.MoteGlow, new Color(1f, 0.15f, 0.1f));

        /// <summary>The protest lamp, lit yellow.</summary>
        public static readonly Material LampYellow = MaterialPool.MatFrom(
            LampPath, ShaderDatabase.MoteGlow, new Color(1f, 0.85f, 0.15f));

        /// <summary>The parts, in <see cref="PartPaths"/> order.</summary>
        public static readonly Material[] Parts = PartPaths
            .Select(p => MaterialPool.MatFrom(p, ShaderDatabase.Transparent))
            .ToArray();

        /// <summary>The allowed-area command's icon.</summary>
        public static readonly Texture2D AreaIcon =
            ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedExpand");

        /// <summary>The DEV commands' icon.</summary>
        public static readonly Texture2D DevIcon =
            ContentFinder<Texture2D>.Get("UI/Commands/Detonate");

        /// <summary>
        /// The named settings' button icons, by setting
        /// (<see cref="TractorBombDial"/> order) and stop (smallest first).
        /// </summary>
        public static readonly string[][] DialIconPaths =
        {
            new[]
            {
                "UI/Commands/BT_ProximityTimid",
                "UI/Commands/BT_ProximityPrudent",
                "UI/Commands/BT_ProximityNosy",
            },
            new[]
            {
                "UI/Commands/BT_PullIntimate",
                "UI/Commands/BT_PullNeighborly",
                "UI/Commands/BT_PullPopular",
            },
            new[]
            {
                "UI/Commands/BT_BlastBirthdayCake",
                "UI/Commands/BT_BlastFirework",
                "UI/Commands/BT_BlastBlockParty",
            },
        };

        /// <summary>The Tractor Barn's "Apply to every tractor" icon.</summary>
        public const string ApplyToEveryTractorPath =
            "UI/Commands/BT_ApplyToEveryTractor";

        /// <summary>The settings' icons, loaded.</summary>
        private static readonly Texture2D[][] DialIcons = DialIconPaths
            .Select(row => row
                .Select(p => ContentFinder<Texture2D>.Get(p))
                .ToArray())
            .ToArray();

        /// <summary>The "Apply to every tractor" icon, loaded.</summary>
        public static readonly Texture2D ApplyToEveryTractorIcon =
            ContentFinder<Texture2D>.Get(ApplyToEveryTractorPath);

        /// <summary>A setting's button icon for a stop.</summary>
        /// <param name="dial">The setting.</param>
        /// <param name="stop">The stop, 0 to 2.</param>
        /// <returns>The icon; vanilla's bad texture if there is none.</returns>
        public static Texture2D DialIcon(TractorBombDial dial, int stop)
        {
            int d = (int)dial;
            int s = TractorBombDials.Sanitize(stop);
            return d >= 0 && d < DialIcons.Length && s < DialIcons[d].Length
                ? DialIcons[d][s] ?? BaseContent.BadTex
                : BaseContent.BadTex;
        }
    }

    /// <summary>
    /// XML-facing properties for <see cref="CompTractorBomb"/>, on the
    /// robot's race.
    /// </summary>
    public class CompProperties_TractorBomb : CompProperties
    {
        /// <summary>How far it can see a hostile, in tiles.</summary>
        public float sightRadius = 45f;

        /// <summary>
        /// How close a timid robot gets to a hostile before it emplaces,
        /// in tiles; its proximity may take it closer
        /// (<see cref="TractorBombApproach.StopDistance"/>).
        /// </summary>
        public float emplaceRange = 15f;

        /// <summary>How far one patrol leg may take it, in tiles.</summary>
        public float patrolLegRadius = 30f;

        /// <summary>The bomb it becomes.</summary>
        public ThingDef emplacedDef;

        /// <summary>Its death.</summary>
        public ThingDef deathVortexDef;

        /// <summary>
        /// The protest beep. A vanilla sound until the mod's sound
        /// pipeline exists.
        /// </summary>
        public SoundDef protestSound;

        /// <summary>Ticks between protest beeps.</summary>
        public int beepIntervalTicks = 30;

        /// <summary>Ticks the protest lamp shows each colour.</summary>
        public int lampHalfPeriodTicks = 10;

        /// <summary>Binds these properties to their comp class.</summary>
        public CompProperties_TractorBomb()
        {
            compClass = typeof(CompTractorBomb);
        }

        /// <summary>Reports Def mistakes at load.</summary>
        /// <param name="parentDef">The race carrying this comp.</param>
        /// <returns>One message per configuration error.</returns>
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (parentDef.race == null)
            {
                yield return "a tractor bomb comp belongs on a race";
            }
            else if (parentDef.race.IsMechanoid)
            {
                yield return "a tractor bomb must not be a mechanoid, or "
                    + "Biotech's mechanitor rules apply to it";
            }

            if (emplacedDef == null
                || !typeof(Building_TractorBomb).IsAssignableFrom(
                    emplacedDef.thingClass))
            {
                yield return "emplacedDef must be a Building_TractorBomb";
            }

            if (deathVortexDef == null
                || !typeof(TractorBombDeathVortex).IsAssignableFrom(
                    deathVortexDef.thingClass))
            {
                yield return "deathVortexDef must be a TractorBombDeathVortex";
            }

            if (!(emplaceRange > 0f) || !(sightRadius >= emplaceRange)
                || !(patrolLegRadius > 0f)
                || sightRadius > GenRadial.MaxRadialPatternRadius)
            {
                yield return "need 0 < emplaceRange <= sightRadius <= the "
                    + "engine's radius limit, and a positive patrol leg";
            }

            if (beepIntervalTicks < 1 || lampHalfPeriodTicks < 1)
            {
                yield return "beep and lamp intervals must be positive";
            }
        }
    }

    /// <summary>
    /// The robot's brain: its named settings, its protest, the act of
    /// emplacing, its controls and its death. Its decisions are made by its
    /// think tree (TractorBombAI.cs), which calls in here.
    /// </summary>
    public class CompTractorBomb : ThingComp, ITractorBombDials
    {
        private TractorBombProximity proximity = TractorBombApproach.Default;
        private TractorBombPull pull = TractorBombYield.DefaultPull;
        private TractorBombBlast blast = TractorBombYield.DefaultBlast;
        private int readyTick = -1;
        private int protestTicksLeft;
        private int protestTicksRun;

        /// <summary>Typed access to this comp's XML properties.</summary>
        public CompProperties_TractorBomb Props =>
            (CompProperties_TractorBomb)props;

        /// <summary>The robot.</summary>
        public Pawn Robot => parent as Pawn;

        /// <summary>
        /// Robots wrecked this session, across all maps (diagnostics).
        /// </summary>
        public static int Wrecked { get; private set; }

        /// <summary>
        /// The tick it became ready to emplace, or -1 if it is not
        /// emplacing (diagnostics, and so a reloaded job resumes).
        /// </summary>
        public int ReadyTick => readyTick;

        /// <summary>Whether it is protesting right now.</summary>
        public bool Protesting => readyTick >= 0 && protestTicksLeft > 0;

        /// <summary>
        /// Whether Tractor Bomb Dial-A-Yield is researched.
        /// </summary>
        public static bool DialAYieldResearched =>
            BT_TractorBombDefOf.BT_Research_TractorBombDialAYield
                ?.IsFinished ?? false;

        /// <summary>
        /// Its settings as set: Proximity, Pull and Blast. Pull and Blast
        /// take effect once Dial-A-Yield is researched (the defaults apply
        /// before). Setting them sets every one the player may change now
        /// (<see cref="CanDial"/>); code that stages a robot (the barn, the
        /// tour, the smoke test) sets them outright with
        /// <see cref="SetSettings"/>.
        /// </summary>
        public TractorBombSettings Settings
        {
            get => new TractorBombSettings(proximity, pull, blast).Sanitized();
            set
            {
                TractorBombSettings v = value.Sanitized();
                if (CanDial(TractorBombDial.Proximity))
                {
                    proximity = v.Proximity;
                }

                if (CanDial(TractorBombDial.Pull))
                {
                    pull = v.Pull;
                }

                if (CanDial(TractorBombDial.Blast))
                {
                    blast = v.Blast;
                }
            }
        }

        /// <summary>
        /// How close it drives to its quarry: Timid, Prudent (the default)
        /// or Nosy. Saved with the robot.
        /// </summary>
        public TractorBombProximity Proximity =>
            TractorBombApproach.Sanitize(proximity);

        /// <summary>
        /// The pull and blast in effect: as set once Dial-A-Yield is
        /// researched, the defaults before.
        /// </summary>
        public (TractorBombPull Pull, TractorBombBlast Blast) Yield =>
            TractorBombYield.Effective(pull, blast, DialAYieldResearched);

        /// <summary>The tractor beam in effect, in tiles.</summary>
        public float PullRadius =>
            (float)TractorBombYield.Radius(Yield.Pull);

        /// <summary>The blast in effect, in tiles.</summary>
        public float BlastRadius =>
            (float)TractorBombYield.Radius(Yield.Blast);

        /// <summary>The timid stop, in tiles: its emplacing range.</summary>
        public float TimidStop => Props.emplaceRange;

        /// <summary>
        /// Whether the player may change its pull and blast: a player
        /// robot, once Dial-A-Yield is researched, until it emplaces.
        /// </summary>
        public bool YieldAdjustable =>
            DialAYieldResearched && parent.Faction == Faction.OfPlayer
            && readyTick < 0;

        /// <summary>
        /// How far from its quarry it stops to plant itself, in tiles: its
        /// proximity's stop, with the tractor beam in effect.
        /// </summary>
        public float StopDistance => (float)TractorBombApproach.StopDistance(
            Proximity, Props.emplaceRange, PullRadius);

        /// <summary>
        /// Whether the player may change its proximity: a player robot
        /// that is not yet emplacing. (Once planted it no longer matters,
        /// so a planted bomb has no such setting.) No research needed.
        /// </summary>
        public bool ProximityAdjustable =>
            parent.Faction == Faction.OfPlayer && readyTick < 0;

        /// <summary>Whether the player may change a setting now.</summary>
        /// <param name="dial">The setting.</param>
        /// <returns>True if it may.</returns>
        public bool CanDial(TractorBombDial dial) =>
            dial == TractorBombDial.Proximity
                ? ProximityAdjustable
                : YieldAdjustable;

        /// <summary>
        /// Sets all three settings outright, whatever the research or
        /// stage: for code that stages a robot, such as the Tractor Barn
        /// fitting a new one with its production defaults.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public void SetSettings(TractorBombSettings settings)
        {
            TractorBombSettings v = settings.Sanitized();
            proximity = v.Proximity;
            pull = v.Pull;
            blast = v.Blast;
        }

        /// <summary>
        /// Whether a colonist stands inside its pull radius: what it
        /// protests about.
        /// </summary>
        /// <returns>True if one does.</returns>
        public bool ColonistInPull() =>
            parent.Spawned && TractorBombUtility.ColonistWithin(
                parent.Map, parent.Position, PullRadius);

        /// <summary>
        /// Ready to emplace: stops, and decides whether to protest. Once
        /// begun it is not begun again, so a reloaded job carries on.
        /// </summary>
        public void BeginEmplacing()
        {
            if (readyTick >= 0 || !parent.Spawned)
            {
                return;
            }

            readyTick = Find.TickManager.TicksGame;
            protestTicksRun = 0;
            protestTicksLeft =
                TractorBombTiming.ProtestDelay(ColonistInPull());
            if (protestTicksLeft > 0 && parent.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    $"{parent.LabelCap} objects: a colonist is within "
                        + "reach of its tractor beam ("
                        + TractorBombDials.TilesText(PullRadius)
                        + "). It will emplace in three seconds regardless.",
                    parent, MessageTypeDefOf.CautionInput, false);
            }
        }

        /// <summary>
        /// One tick of the protest: it beeps on the interval and its lamp
        /// blinks (drawn in <see cref="PostDraw"/>).
        /// </summary>
        /// <returns>
        /// True once the protest is over (or there was none).
        /// </returns>
        public bool TickProtest()
        {
            if (protestTicksLeft <= 0)
            {
                return true;
            }

            if (TractorBombTiming.BeepsAt(
                protestTicksRun, Props.beepIntervalTicks)
                && parent.Spawned)
            {
                Props.protestSound?.PlayOneShot(
                    new TargetInfo(parent.Position, parent.Map));
            }

            protestTicksRun++;
            protestTicksLeft--;
            return protestTicksLeft <= 0;
        }

        /// <summary>
        /// Emplaces: the robot leaves the map for good and an emplaced bomb
        /// takes its place, facing the same way, with its pull and blast.
        /// </summary>
        /// <returns>
        /// The bomb, or null if the robot was not on a map.
        /// </returns>
        public Building_TractorBomb Emplace()
        {
            Pawn robot = Robot;
            if (robot == null || !robot.Spawned || Props.emplacedDef == null)
            {
                return null;
            }

            Map map = robot.Map;
            IntVec3 cell = robot.Position;
            Rot4 facing = robot.Rotation;
            var bomb = (Building_TractorBomb)ThingMaker.MakeThing(
                Props.emplacedDef);
            bomb.SetFaction(robot.Faction);
            bomb.Configure(
                Settings, readyTick, Find.TickManager.TicksGame - readyTick);
            // As a dryad returning to its tree does: off the map, then
            // discarded. Not killed, so it has no death of its own.
            robot.DeSpawn();
            Find.WorldPawns.PassToWorld(robot, PawnDiscardDecideMode.Discard);
            return (Building_TractorBomb)GenSpawn.Spawn(
                bomb, cell, map, facing, WipeMode.VanishOrMoveAside);
        }

        /// <summary>
        /// Wrecked before it emplaced: its own death, a sputtering vortex
        /// that sucks in its parts and nothing else, then pops.
        /// </summary>
        /// <param name="prevMap">The map it died on.</param>
        /// <param name="dinfo">What killed it, if anything.</param>
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            IntVec3 cell = parent.PositionHeld;
            if (prevMap == null || !cell.IsValid || !cell.InBounds(prevMap)
                || Props.deathVortexDef == null)
            {
                return;
            }

            Wrecked++;
            GenSpawn.Spawn(Props.deathVortexDef, cell, prevMap);
            if (parent.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    $"{parent.LabelCap} was wrecked before it could emplace. "
                        + "Its tractor beam has gathered up the pieces.",
                    new TargetInfo(cell, prevMap),
                    MessageTypeDefOf.NegativeEvent);
            }
        }

        /// <summary>The blinking protest lamp, while it protests.</summary>
        public override void PostDraw()
        {
            base.PostDraw();
            if (!Protesting || !parent.Spawned)
            {
                return;
            }

            Material lamp = TractorBombTiming.LampIsRed(
                protestTicksRun, Props.lampHalfPeriodTicks)
                ? BT_TractorBombAssets.LampRed
                : BT_TractorBombAssets.LampYellow;
            // The lamp sits on the control box, a little behind the middle.
            Vector3 pos = parent.DrawPos
                + new Vector3(0f, 0f, -0.17f).RotatedBy(parent.Rotation);
            pos.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(
                    pos, Quaternion.identity, new Vector3(0.7f, 1f, 0.7f)),
                lamp, 0);
        }

        /// <summary>Its reach, while selected.</summary>
        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (parent.Spawned)
            {
                TractorBombUtility.DrawRadii(
                    parent.Position, PullRadius, BlastRadius, false);
            }
        }

        /// <summary>
        /// Its settings by name, their sizes, its allowed area and any
        /// protest.
        /// </summary>
        /// <returns>The inspect pane lines.</returns>
        public override string CompInspectStringExtra()
        {
            var (inPull, inBlast) = Yield;
            string text = TractorBombDials.Summary(
                    new TractorBombSettings(Proximity, inPull, inBlast),
                    withProximity: true)
                + "\nTractor beam "
                + TractorBombDials.TilesText(PullRadius) + ", blast "
                + TractorBombDials.TilesText(BlastRadius) + "; stops "
                + TractorBombDials.TilesText(StopDistance) + " out";
            if (Robot?.playerSettings != null)
            {
                text += "\nPatrols: " + AreaUtility.AreaAllowedLabel(Robot);
            }

            if (Protesting)
            {
                text += "\nProtesting: a colonist is inside its tractor "
                    + $"beam ({protestTicksLeft.ToStringSecondsFromTicks()})";
            }

            return text;
        }

        /// <summary>
        /// Its controls: the allowed area, its Proximity, its Pull and
        /// Blast once Dial-A-Yield is researched, and DEV commands.
        /// </summary>
        /// <returns>The gizmos.</returns>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            Pawn robot = Robot;
            if (robot == null || robot.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (robot.playerSettings != null && robot.Spawned)
            {
                yield return new Command_TractorBombArea(robot);
            }

            foreach (TractorBombDial dial in TractorBombDials.All)
            {
                if (CanDial(dial))
                {
                    yield return new Command_TractorBombDial(
                        this, dial, "this tractor bomb");
                }
            }

            if (DebugSettings.ShowDevGizmos && robot.Spawned && readyTick < 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Emplace now",
                    icon = BT_TractorBombAssets.DevIcon,
                    action = () => robot.jobs.StartJob(
                        JobMaker.MakeJob(
                            BT_TractorBombDefOf.BT_TractorBombEmplace),
                        JobCondition.InterruptForced),
                };
            }
        }

        /// <summary>
        /// Saves and loads the settings and any emplacing. An older save's
        /// Disposition and dialled radii load as the nearest named stops
        /// (<see cref="TractorBombSaves"/>).
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(
                ref proximity, "proximity", TractorBombApproach.Default);
            Scribe_Values.Look(ref pull, "pull", TractorBombYield.DefaultPull);
            Scribe_Values.Look(
                ref blast, "blast", TractorBombYield.DefaultBlast);
            Scribe_Values.Look(ref readyTick, "readyTick", -1);
            Scribe_Values.Look(ref protestTicksLeft, "protestTicksLeft");
            Scribe_Values.Look(ref protestTicksRun, "protestTicksRun");
            TractorBombSaves.LoadLegacy(
                ref proximity, ref pull, ref blast, withProximity: true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SetSettings(new TractorBombSettings(proximity, pull, blast));
                protestTicksLeft = Mathf.Clamp(
                    protestTicksLeft, 0, TractorBombTiming.ProtestTicks);
            }
        }
    }

    /// <summary>
    /// Reads what older versions saved: a robot's Disposition and either
    /// thing's dialled radii, from before the named settings.
    /// </summary>
    public static class TractorBombSaves
    {
        /// <summary>
        /// While loading, replaces the settings with what an older save
        /// wrote instead, if it wrote anything: its Disposition (Cautious,
        /// Moderate, Nosy load as Timid, Prudent, Nosy) and its dialled
        /// pull and blast, snapped to the nearest stop. A radius the old
        /// version left out was its default, never dialled, so that
        /// setting keeps the new default. Saves made since have none of
        /// these, and keep what they read. Does nothing outside loading.
        /// </summary>
        /// <param name="proximity">The proximity read.</param>
        /// <param name="pull">The pull read.</param>
        /// <param name="blast">The blast read.</param>
        /// <param name="withProximity">Whether a Disposition may be there.</param>
        public static void LoadLegacy(
            ref TractorBombProximity proximity, ref TractorBombPull pull,
            ref TractorBombBlast blast, bool withProximity)
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars)
            {
                return;
            }

            if (withProximity)
            {
                string disposition = null;
                Scribe_Values.Look(ref disposition, "disposition");
                TractorBombProximity? old =
                    TractorBombApproach.FromDisposition(disposition);
                if (old.HasValue)
                {
                    proximity = old.Value;
                }
            }

            float dialledPull = float.NaN;
            float dialledBlast = float.NaN;
            Scribe_Values.Look(ref dialledPull, "dialledPull", float.NaN);
            Scribe_Values.Look(ref dialledBlast, "dialledBlast", float.NaN);
            if (!float.IsNaN(dialledPull))
            {
                pull = TractorBombYield.NearestPull(dialledPull);
            }

            if (!float.IsNaN(dialledBlast))
            {
                blast = TractorBombYield.NearestBlast(dialledBlast);
            }
        }
    }

    /// <summary>
    /// Shared Tractor Bomb helpers: making robots, finding their quarry,
    /// and the queries other steps need (the flee job giver finds armed
    /// bombs; the Tractor Barn counts them).
    /// </summary>
    public static class TractorBombUtility
    {
        private static readonly Color PullRingColor =
            new Color(0.85f, 0.35f, 1f);

        private static readonly Color PullRingDimColor =
            new Color(0.85f, 0.35f, 1f, 0.3f);

        private static readonly Color BlastRingColor =
            new Color(1f, 0.25f, 0.15f);

        private static readonly Color BlastRingDimColor =
            new Color(1f, 0.25f, 0.15f, 0.3f);

        /// <summary>
        /// Makes a new robot, not yet spawned: a fresh tractor bomb, built
        /// today.
        /// </summary>
        /// <param name="faction">Its owner, usually the player.</param>
        /// <returns>The robot.</returns>
        public static Pawn MakeRobot(Faction faction) =>
            PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                BT_TractorBombKindDefOf.BT_TractorBomb, faction,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                allowFood: false, allowAddictions: false,
                fixedBiologicalAge: 0f, fixedChronologicalAge: 0f));

        /// <summary>
        /// Makes a robot and drives it out onto the map near a cell: what
        /// the Tractor Barn does when it finishes one.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="near">Where it should appear.</param>
        /// <param name="faction">Its owner, usually the player.</param>
        /// <returns>The robot, spawned.</returns>
        public static Pawn SpawnRobot(Map map, IntVec3 near, Faction faction)
        {
            if (map == null || !near.InBounds(map))
            {
                return null;
            }

            IntVec3 cell = CellFinder.StandableCellNear(near, map, 4f);
            if (!cell.IsValid)
            {
                cell = near;
            }

            return (Pawn)GenSpawn.Spawn(MakeRobot(faction), cell, map);
        }

        /// <summary>
        /// Every tractor bomb a faction has on a map, robots and emplaced
        /// bombs alike: what the Tractor Barn's "Do until you have X"
        /// counts (<see cref="RecipeWorkerCounter_TractorBombs"/>).
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="faction">The owner.</param>
        /// <returns>How many.</returns>
        public static int CountOnMap(Map map, Faction faction)
        {
            if (map == null)
            {
                return 0;
            }

            int robots = map.mapPawns.AllPawnsSpawned.Count(
                p => p.Faction == faction && p.TryGetComp<CompTractorBomb>()
                    != null);
            int planted = map.listerThings
                .ThingsOfDef(BT_TractorBombDefOf.BT_TractorBombEmplaced)
                .Count(t => t.Faction == faction);
            return robots + planted;
        }

        /// <summary>
        /// The emplaced bombs on a map that are armed (counting down or
        /// imploding): what everyone else runs from
        /// (<see cref="JobGiver_FleeTractorBomb"/>), at each one's
        /// <see cref="Building_TractorBomb.PullRadius"/> plus 8 tiles.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The armed bombs.</returns>
        public static IEnumerable<Building_TractorBomb> ArmedBombs(Map map)
        {
            if (map == null)
            {
                yield break;
            }

            foreach (Thing t in map.listerThings.ThingsOfDef(
                BT_TractorBombDefOf.BT_TractorBombEmplaced))
            {
                if (t is Building_TractorBomb bomb && bomb.Spawned
                    && bomb.IsArmed)
                {
                    yield return bomb;
                }
            }
        }

        /// <summary>
        /// Whether a colonist (free or enslaved) stands within a radius.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The centre.</param>
        /// <param name="radius">The radius, in tiles.</param>
        /// <returns>True if one does.</returns>
        public static bool ColonistWithin(
            Map map, IntVec3 center, float radius)
        {
            if (map == null)
            {
                return false;
            }

            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.IsColonist && !p.Dead
                    && p.Position.InHorDistOf(center, radius))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The robot's quarry: the nearest hostile pawn it can see, within
        /// its sight, that is no threat-in-name-only (downed, dormant,
        /// invisible), with a cell within its stopping distance (its
        /// proximity's) that it can reach without leaving its allowed
        /// area.
        /// </summary>
        /// <param name="robot">The robot.</param>
        /// <param name="comp">Its brain: its settings and proximity.</param>
        /// <param name="target">The hostile.</param>
        /// <param name="cell">
        /// Where to go: its own cell when already in range.
        /// </param>
        /// <returns>False if there is none.</returns>
        public static bool TryFindQuarry(
            Pawn robot, CompTractorBomb comp, out Pawn target,
            out IntVec3 cell)
        {
            target = null;
            cell = IntVec3.Invalid;
            CompProperties_TractorBomb props = comp?.Props;
            if (robot == null || !robot.Spawned || props == null
                || robot.Faction == null)
            {
                return false;
            }

            Map map = robot.Map;
            float sight = props.sightRadius;
            float stop = Mathf.Min(comp.StopDistance, sight);
            var seen = new List<Pawn>();
            foreach (IAttackTarget t in map.attackTargetsCache
                .TargetsHostileToFaction(robot.Faction))
            {
                if (t.Thing is Pawn p && CanSee(robot, p, sight))
                {
                    seen.Add(p);
                }
            }

            foreach (Pawn p in seen.OrderBy(
                p => (p.Position - robot.Position).LengthHorizontalSquared))
            {
                if (TryFindApproachCell(robot, p, stop, out cell))
                {
                    target = p;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a robot can see a hostile: spawned, standing, a real
        /// threat, in explored ground, within sight and in line of sight.
        /// </summary>
        /// <param name="robot">The robot.</param>
        /// <param name="p">The hostile.</param>
        /// <param name="sight">How far it sees, in tiles.</param>
        /// <returns>True if it can.</returns>
        private static bool CanSee(Pawn robot, Pawn p, float sight) =>
            p != robot && p.Spawned && !p.Dead && !p.Downed
            && p.Map == robot.Map && !p.ThreatDisabled(robot)
            && !p.Position.Fogged(p.Map)
            && p.Position.InHorDistOf(robot.Position, sight)
            && GenSight.LineOfSight(
                robot.Position, p.Position, robot.Map, skipFirstCell: true);

        /// <summary>
        /// The reachable cell nearest the robot within range of a hostile,
        /// inside the robot's allowed area if it has one.
        /// </summary>
        /// <param name="robot">The robot.</param>
        /// <param name="target">The hostile.</param>
        /// <param name="range">The stopping distance, in tiles.</param>
        /// <param name="cell">The cell; the robot's own if in range.</param>
        /// <returns>False if there is none.</returns>
        public static bool TryFindApproachCell(
            Pawn robot, Pawn target, float range, out IntVec3 cell)
        {
            Map map = robot.Map;
            Area area = robot.playerSettings
                ?.EffectiveAreaRestrictionInPawnCurrentMap;
            IntVec3 here = robot.Position;
            if (here.InHorDistOf(target.Position, range)
                && (area == null || area[here]))
            {
                cell = here;
                return true;
            }

            float r = Mathf.Min(range, GenRadial.MaxRadialPatternRadius - 1f);
            var candidates = GenRadial.RadialCellsAround(
                    target.Position, r, true)
                .Where(c => c.InBounds(map) && c.Standable(map)
                    && !c.Fogged(map) && (area == null || area[c]))
                .OrderBy(c => (c - here).LengthHorizontalSquared)
                .Take(60);
            foreach (IntVec3 c in candidates)
            {
                if (robot.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                {
                    cell = c;
                    return true;
                }
            }

            cell = IntVec3.Invalid;
            return false;
        }

        /// <summary>
        /// A patrol leg: a random reachable cell in explored ground, within
        /// a leg's reach, inside the allowed area if there is one; failing
        /// that, any reachable cell of the area, so a robot outside its
        /// area heads back in.
        /// </summary>
        /// <param name="robot">The robot.</param>
        /// <param name="legRadius">How far one leg may go, in tiles.</param>
        /// <param name="cell">The destination.</param>
        /// <returns>False if there is nowhere to go.</returns>
        public static bool TryFindPatrolCell(
            Pawn robot, float legRadius, out IntVec3 cell)
        {
            Map map = robot.Map;
            Area area = robot.playerSettings
                ?.EffectiveAreaRestrictionInPawnCurrentMap;
            IntVec3 here = robot.Position;
            float minLeg = Mathf.Min(6f, legRadius / 2f);
            bool Fits(IntVec3 c) =>
                c.Standable(map) && !c.Fogged(map)
                && (area == null || area[c])
                && !c.InHorDistOf(here, minLeg);
            if (CellFinder.TryFindRandomReachableNearbyCell(
                here, map, legRadius, TraverseParms.For(robot, Danger.Deadly),
                Fits, null, out cell))
            {
                return true;
            }

            if (area != null && area.ActiveCells
                .Where(c => c.Standable(map)
                    && robot.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                .Take(200).TryRandomElement(out cell))
            {
                return true;
            }

            cell = IntVec3.Invalid;
            return false;
        }

        /// <summary>
        /// Draws a bomb's reach: its tractor beam in magenta and its blast
        /// in red, flashing while it counts down. Radii past the engine's
        /// ring limit are not drawn.
        /// </summary>
        /// <param name="center">The bomb's cell.</param>
        /// <param name="pull">The pull radius, in tiles.</param>
        /// <param name="blast">The blast radius, in tiles.</param>
        /// <param name="flashing">Whether to flash (counting down).</param>
        public static void DrawRadii(
            IntVec3 center, float pull, float blast, bool flashing)
        {
            // Flash at 2 Hz. Two fixed colours each, because DrawRadiusRing
            // pools one material per colour.
            bool bright = !flashing
                || (int)(Time.realtimeSinceStartup * 4f) % 2 == 0;
            float max = GenRadial.MaxRadialPatternRadius;
            if (pull > 0f && pull < max)
            {
                GenDraw.DrawRadiusRing(
                    center, pull, bright ? PullRingColor : PullRingDimColor);
            }

            if (blast > 0f && blast < max)
            {
                GenDraw.DrawRadiusRing(
                    center, blast,
                    bright ? BlastRingColor : BlastRingDimColor);
            }
        }
    }
}
