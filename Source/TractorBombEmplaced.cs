// Batshit Technology - the emplaced Tractor Bomb (docs/TRACTOR-BOMB.md).
// What a robot becomes where it stops: its tyres flip out flat like
// stabiliser pads over about half a second, then it counts down a rolled
// 3 to 5 seconds under the shared warning band (CountdownWarning.cs), then
// hosts the shared implosion (Implosion.cs): everything within its pull
// radius, 10 tiles unless dialled, is dragged to within 1.5 tiles. It
// explodes over its blast radius, 5 tiles unless dialled, as soon as
// everything is gathered or nothing more can move, or when half again its
// drag time has passed.
//
// The pile holds one item stack per cell: nine cells lie within 1.5 tiles
// (the bomb passes pawns and items over it, like sandbags). Stacks beyond
// nine that cannot merge wait just outside the pile, at about 2 tiles,
// where the blast still catches them. Pawns share cells and all fit.
//
// It cannot be damaged and no other implosion rips it loose: once planted,
// it goes off. The flee step (roadmap step 6) finds armed bombs through
// TractorBombUtility.ArmedBombs and reads each one's PullRadius.

using System.Collections.Generic;
using System.Text;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BatshitTechnology
{
    /// <summary>The emplaced bomb's settings, on its ThingDef.</summary>
    public class TractorBombExtension : DefModExtension
    {
        /// <summary>
        /// The implosion it hosts. Its pull radius is replaced by the
        /// bomb's own (the default, or as dialled).
        /// </summary>
        public ImplosionSettings implosion = new ImplosionSettings
        {
            gatherRadius = 1.5f,
        };

        /// <summary>
        /// The countdown's range, in seconds, rolled per bomb.
        /// </summary>
        public FloatRange countdownSeconds = new FloatRange(
            (float)TractorBombTiming.MinCountdownSeconds,
            (float)TractorBombTiming.MaxCountdownSeconds);

        /// <summary>Ticks its tyres take to flip out flat.</summary>
        public int deployTicks = TractorBombTiming.DeployTicks;

        /// <summary>The explosion's damage type; Bomb if unset.</summary>
        public DamageDef explosionDamageDef;

        /// <summary>
        /// The explosion's damage; -1 uses the type's default.
        /// </summary>
        public int explosionDamage = -1;

        /// <summary>
        /// When this many seconds or fewer remain, the warning band turns
        /// red and pulses faster.
        /// </summary>
        public float finalWarningSeconds = 1.5f;

        /// <summary>
        /// The band's pulse period before the final seconds.
        /// </summary>
        public float pulsePeriodSeconds = 1f;

        /// <summary>The band's pulse period in the final seconds.</summary>
        public float finalPulsePeriodSeconds = 0.25f;

        /// <summary>Reports mistakes in the settings.</summary>
        /// <returns>One message per problem.</returns>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (implosion == null)
            {
                yield return "tractor bomb needs implosion settings";
            }
            else
            {
                foreach (string problem in implosion.Problems(
                    GenRadial.MaxRadialPatternRadius))
                {
                    yield return "tractor bomb implosion " + problem;
                }
            }

            if (!(countdownSeconds.min > 0f)
                || countdownSeconds.max < countdownSeconds.min)
            {
                yield return "countdownSeconds must be positive, min first";
            }

            if (deployTicks < 0 || finalWarningSeconds < 0f
                || !(pulsePeriodSeconds > 0f)
                || !(finalPulsePeriodSeconds > 0f))
            {
                yield return "deploy and warning timings must be sensible";
            }
        }
    }

    /// <summary>
    /// An emplaced Tractor Bomb: flips its tyres flat, counts down under
    /// the shared warning band, implodes, and explodes.
    /// </summary>
    public class Building_TractorBomb : Building, ICountdownWarning,
        IImplosionProof, ITractorBombYield
    {
        /// <summary>Where it is in its short life.</summary>
        public enum Stage
        {
            /// <summary>Tyres flipping out flat.</summary>
            Deploying,

            /// <summary>Counting down under the warning band.</summary>
            CountingDown,

            /// <summary>Pulling everything in.</summary>
            Imploding,
        }

        private Stage stage;
        private int stageTicks;
        private int countdownTicks;
        private float dialledPull = TractorBombYield.DefaultPull;
        private float dialledBlast = TractorBombYield.DefaultBlast;
        private Implosion implosion;
        private int emplacedTick = -1;
        private int readyTick = -1;
        private int protestTicks;
        private int countdownStartedTick = -1;
        private int implosionStartedTick = -1;
        private Sustainer alarm;
        private readonly CountdownWarningBand band =
            new CountdownWarningBand();

        /// <summary>
        /// Bombs that have exploded this session (diagnostics).
        /// </summary>
        public static int Explosions { get; private set; }

        /// <summary>
        /// The blast radius of the last bomb to explode this session
        /// (diagnostics).
        /// </summary>
        public static float LastBlastRadius { get; private set; }

        /// <summary>Its settings.</summary>
        public TractorBombExtension Settings =>
            def.GetModExtension<TractorBombExtension>()
            ?? new TractorBombExtension();

        /// <summary>Where it is in its short life.</summary>
        public Stage CurrentStage => stage;

        /// <summary>
        /// Whether it is armed: counting down or imploding. What the flee
        /// step will run from.
        /// </summary>
        public bool IsArmed => Spawned && stage != Stage.Deploying;

        /// <summary>Its rolled countdown, in ticks.</summary>
        public int CountdownTicks => countdownTicks;

        /// <summary>
        /// Ticks until the implosion starts; 0 once it has.
        /// </summary>
        public int TicksUntilImplosion => stage switch
        {
            Stage.Deploying => (Settings.deployTicks - stageTicks)
                + countdownTicks,
            Stage.CountingDown => countdownTicks - stageTicks,
            _ => 0,
        };

        /// <summary>The implosion, once it has started; else null.</summary>
        public Implosion Implosion => implosion;

        /// <summary>The tick it was emplaced (diagnostics).</summary>
        public int EmplacedTick => emplacedTick;

        /// <summary>
        /// The tick its robot was ready to emplace, or -1 (diagnostics).
        /// </summary>
        public int ReadyTick => readyTick;

        /// <summary>
        /// How long its robot protested, in ticks (diagnostics).
        /// </summary>
        public int ProtestTicks => protestTicks;

        /// <summary>
        /// The tick its countdown started, or -1 (diagnostics).
        /// </summary>
        public int CountdownStartedTick => countdownStartedTick;

        /// <summary>
        /// The tick its implosion started, or -1 (diagnostics).
        /// </summary>
        public int ImplosionStartedTick => implosionStartedTick;

        /// <summary>
        /// Its tractor beam: as dialled once Dial-A-Yield is researched, the
        /// default before. Setting it dials it.
        /// </summary>
        public float PullRadius
        {
            get => TractorBombYield.Effective(
                dialledPull, dialledBlast,
                CompTractorBomb.DialAYieldResearched).Pull;
            set => dialledPull = TractorBombYield.ClampPull(value);
        }

        /// <summary>
        /// Its blast: as dialled once Dial-A-Yield is researched, the
        /// default before. Setting it dials it.
        /// </summary>
        public float BlastRadius
        {
            get => TractorBombYield.Effective(
                dialledPull, dialledBlast,
                CompTractorBomb.DialAYieldResearched).Blast;
            set => dialledBlast = TractorBombYield.ClampBlast(value);
        }

        /// <summary>
        /// Whether the player may still dial it: a player bomb, once
        /// Dial-A-Yield is researched, until the implosion starts.
        /// </summary>
        public bool YieldAdjustable =>
            CompTractorBomb.DialAYieldResearched
            && Faction == Faction.OfPlayer && stage != Stage.Imploding;

        /// <summary>
        /// Sets it up before it spawns, from the robot it replaces.
        /// </summary>
        /// <param name="pull">The robot's pull radius, in tiles.</param>
        /// <param name="blast">The robot's blast radius, in tiles.</param>
        /// <param name="robotReadyTick">
        /// When the robot was ready to emplace, or -1.
        /// </param>
        /// <param name="robotProtestTicks">
        /// How long the robot protested first, in ticks.
        /// </param>
        public void Configure(
            float pull, float blast, int robotReadyTick,
            int robotProtestTicks)
        {
            dialledPull = TractorBombYield.ClampPull(pull);
            dialledBlast = TractorBombYield.ClampBlast(blast);
            readyTick = robotReadyTick;
            protestTicks = Mathf.Max(0, robotProtestTicks);
        }

        /// <summary>
        /// Rolls its countdown and begins flipping its tyres; on a load,
        /// resumes its warnings.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="respawningAfterLoad">
        /// True when loading a save.
        /// </param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            band.Reset();
            if (!respawningAfterLoad && emplacedTick < 0)
            {
                emplacedTick = Find.TickManager.TicksGame;
                FloatRange range = Settings.countdownSeconds;
                countdownTicks = TractorBombTiming.RollCountdownTicks(
                    Rand.Value, range.min, range.max);
                SoundDefOf.Building_Complete.PlayOneShot(
                    new TargetInfo(Position, map));
                FleckMaker.ThrowDustPuffThick(
                    DrawPos, map, 1.6f, new Color(0.6f, 0.58f, 0.55f));
            }

            if (stage == Stage.CountingDown)
            {
                Overlay(map)?.Register(this);
            }
        }

        /// <summary>
        /// Leaving the map ends its alarm and warnings. (It leaves only by
        /// exploding, or by a developer's hand.)
        /// </summary>
        /// <param name="mode">How it is leaving.</param>
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = Map;
            StopAlarm();
            if (map != null)
            {
                Overlay(map)?.Deregister(this);
            }

            base.DeSpawn(mode);
        }

        /// <summary>
        /// Saves and loads its progress, implosion included.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref stageTicks, "stageTicks");
            Scribe_Values.Look(ref countdownTicks, "countdownTicks");
            Scribe_Values.Look(
                ref dialledPull, "dialledPull", TractorBombYield.DefaultPull);
            Scribe_Values.Look(
                ref dialledBlast, "dialledBlast",
                TractorBombYield.DefaultBlast);
            Scribe_Values.Look(ref emplacedTick, "emplacedTick", -1);
            Scribe_Values.Look(ref readyTick, "readyTick", -1);
            Scribe_Values.Look(ref protestTicks, "protestTicks");
            Scribe_Values.Look(
                ref countdownStartedTick, "countdownStartedTick", -1);
            Scribe_Values.Look(
                ref implosionStartedTick, "implosionStartedTick", -1);
            Scribe_Deep.Look(ref implosion, "implosion");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                dialledPull = TractorBombYield.ClampPull(dialledPull);
                dialledBlast = TractorBombYield.ClampBlast(dialledBlast);
                countdownTicks = Mathf.Max(1, countdownTicks);
                if (stage == Stage.Imploding && implosion == null)
                {
                    // A hand-edited save: start the pull afresh.
                    stage = Stage.CountingDown;
                    stageTicks = countdownTicks;
                }
            }
        }

        /// <summary>
        /// One tick: flip, count down, or pull, and explode when done.
        /// </summary>
        protected override void Tick()
        {
            base.Tick();
            if (!Spawned)
            {
                return;
            }

            switch (stage)
            {
                case Stage.Deploying:
                    if (++stageTicks >= Settings.deployTicks)
                    {
                        StartCountdown();
                    }

                    break;
                case Stage.CountingDown:
                    TickAlarm();
                    if (++stageTicks >= countdownTicks)
                    {
                        StartImplosion();
                    }

                    break;
                case Stage.Imploding:
                    implosion.Tick(Map);
                    if (TractorBombTiming.ShouldExplode(
                        implosion.AllGathered, implosion.Settled,
                        implosion.ElapsedTicks,
                        TractorBombTiming.ImplosionCapTicks(
                            implosion.PullTicks)))
                    {
                        Explode();
                    }

                    break;
            }
        }

        /// <summary>
        /// Starts the countdown now (DEV and smoke-test hook, too): the
        /// tyres are down and the warning band goes up.
        /// </summary>
        public void StartCountdown()
        {
            if (!Spawned || stage != Stage.Deploying)
            {
                return;
            }

            stage = Stage.CountingDown;
            stageTicks = 0;
            countdownStartedTick = Find.TickManager.TicksGame;
            Overlay(Map)?.Register(this);
            SoundDefOf.TrapArm.PlayOneShot(new TargetInfo(Position, Map));
            if (Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    $"{LabelCap} armed: it implodes in "
                        + $"{countdownTicks.ToStringSecondsFromTicks()}, "
                        + $"pulling in everything within {PullRadius:0} "
                        + $"tiles.",
                    this, MessageTypeDefOf.ThreatSmall, false);
            }
        }

        /// <summary>
        /// Opens the implosion: the countdown's alarm and warnings end and
        /// the pull begins.
        /// </summary>
        private void StartImplosion()
        {
            StopAlarm();
            Overlay(Map)?.Deregister(this);
            ImplosionSettings pull = Settings.implosion?.Copy()
                ?? new ImplosionSettings { gatherRadius = 1.5f };
            pull.pullRadius = PullRadius;
            implosion = new Implosion(Position, pull, this, this);
            stage = Stage.Imploding;
            stageTicks = 0;
            implosionStartedTick = Find.TickManager.TicksGame;
            SoundDefOf.PsychicPulseGlobal.PlayOneShot(
                new TargetInfo(Position, Map));
            if (Map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(0.5f);
            }
        }

        /// <summary>
        /// The explosion, on whatever has piled up around it. The bomb is
        /// gone first, as vanilla explosives are.
        /// </summary>
        private void Explode()
        {
            Map map = Map;
            IntVec3 center = Position;
            float blast = BlastRadius;
            TractorBombExtension s = Settings;
            Explosions++;
            LastBlastRadius = blast;
            Destroy(DestroyMode.Vanish);
            GenExplosion.DoExplosion(
                center, map, blast, s.explosionDamageDef ?? DamageDefOf.Bomb,
                this, s.explosionDamage);
        }

        /// <summary>Keeps the countdown's hiss going.</summary>
        private void TickAlarm()
        {
            if (alarm == null || alarm.Ended)
            {
                alarm = SoundDefOf.HissSmall.TrySpawnSustainer(
                    SoundInfo.InMap(this, MaintenanceType.PerTick));
            }
            else
            {
                alarm.Maintain();
            }
        }

        /// <summary>Ends the countdown's hiss.</summary>
        private void StopAlarm()
        {
            alarm?.End();
            alarm = null;
        }

        /// <summary>This map's countdown overlay component.</summary>
        /// <param name="map">The map.</param>
        /// <returns>The component, or null.</returns>
        private static MapComponent_SelfDestructOverlay Overlay(Map map) =>
            map?.GetComponent<MapComponent_SelfDestructOverlay>();

        /// <summary>
        /// Draws the countdown's warnings every frame: the flashing rings
        /// and the shared pulsing band.
        /// </summary>
        public void DrawCountdownOverlay()
        {
            if (!Spawned || stage != Stage.CountingDown)
            {
                return;
            }

            TractorBombUtility.DrawRadii(
                Position, PullRadius, BlastRadius, true);
            TractorBombExtension s = Settings;
            band.Draw(
                this.OccupiedRect(),
                (countdownTicks - stageTicks)
                    / (float)TractorBombTiming.TicksPerSecond,
                s.finalWarningSeconds, s.pulsePeriodSeconds,
                s.finalPulsePeriodSeconds);
        }

        /// <summary>
        /// Draws the bomb: its tyres mid-flip while it deploys, flat after;
        /// the vortex on top while it implodes.
        /// </summary>
        /// <param name="drawLoc">Where to draw.</param>
        /// <param name="flip">Unused.</param>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic graphic = Graphic;
            if (stage == Stage.Deploying)
            {
                graphic = BT_TractorBombFlipGraphics.Frame(
                    stageTicks, Settings.deployTicks) ?? graphic;
            }

            graphic?.Draw(drawLoc, Rotation, this);
            Comps_PostDraw();
            if (stage == Stage.Imploding && implosion != null)
            {
                SingularityVortex.Draw(
                    drawLoc,
                    implosion.ElapsedTicks
                        / (float)TractorBombTiming.TicksPerSecond,
                    implosion.PullTicks
                        / (float)TractorBombTiming.TicksPerSecond,
                    implosion.PullRadius, BT_SingularityAssets.InnerVortex);
            }
        }

        /// <summary>
        /// Its reach, while selected and not counting down.
        /// </summary>
        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            if (stage != Stage.CountingDown)
            {
                TractorBombUtility.DrawRadii(
                    Position, PullRadius, BlastRadius, false);
            }
        }

        /// <summary>Its stage, timer and yield.</summary>
        /// <returns>The inspect pane text.</returns>
        public override string GetInspectString()
        {
            var text = new StringBuilder(base.GetInspectString());
            if (text.Length > 0)
            {
                text.AppendLine();
            }

            switch (stage)
            {
                case Stage.Deploying:
                    text.Append("Deploying stabiliser tyres");
                    break;
                case Stage.CountingDown:
                    text.Append("IMPLOSION IN: "
                        + (countdownTicks - stageTicks)
                            .ToStringSecondsFromTicks());
                    break;
                default:
                    text.Append(
                        $"Imploding: {implosion?.CaughtCount ?? 0} caught");
                    break;
            }

            text.AppendLine();
            text.Append($"Tractor beam {PullRadius:0} tiles, "
                + $"blast {BlastRadius:0} tiles");
            return text.ToString();
        }

        /// <summary>
        /// Its controls: the yield sliders until it implodes, once
        /// Dial-A-Yield is researched; and DEV commands.
        /// </summary>
        /// <returns>The gizmos.</returns>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            if (YieldAdjustable)
            {
                yield return new Gizmo_TractorBombYield(
                    this, TractorBombYieldKind.Pull);
                yield return new Gizmo_TractorBombYield(
                    this, TractorBombYieldKind.Blast);
            }

            if (DebugSettings.godMode && stage == Stage.CountingDown)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Implode now",
                    icon = BT_TractorBombAssets.DevIcon,
                    action = () => stageTicks = countdownTicks - 1,
                };
            }
        }
    }

    /// <summary>
    /// The emplaced bomb's flip frames, loaded at startup on the main
    /// thread: tyres upright, then a third, two thirds and almost all the
    /// way flat.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BT_TractorBombFlipGraphics
    {
        /// <summary>The frames, in order.</summary>
        private static readonly Graphic[] Frames = LoadFrames();

        /// <summary>
        /// The frame to show part-way through the flip.
        /// </summary>
        /// <param name="ticks">Ticks the flip has run.</param>
        /// <param name="deployTicks">The flip's length, in ticks.</param>
        /// <returns>The frame, or null when it is done.</returns>
        public static Graphic Frame(int ticks, int deployTicks)
        {
            if (Frames.Length == 0 || deployTicks <= 0 || ticks >= deployTicks)
            {
                return null;
            }

            int i = Mathf.Clamp(
                ticks * Frames.Length / deployTicks, 0, Frames.Length - 1);
            return Frames[i];
        }

        /// <summary>
        /// Loads each frame at the emplaced bomb's draw size.
        /// </summary>
        /// <returns>The frames.</returns>
        private static Graphic[] LoadFrames()
        {
            Vector2 size = BT_TractorBombDefOf.BT_TractorBombEmplaced
                ?.graphicData?.drawSize ?? new Vector2(2f, 2f);
            var frames = new List<Graphic>();
            foreach (string path in BT_TractorBombAssets.FlipFramePaths)
            {
                frames.Add(GraphicDatabase.Get<Graphic_Single>(
                    path, ShaderDatabase.Cutout, size, Color.white));
            }

            return frames.ToArray();
        }
    }
}
