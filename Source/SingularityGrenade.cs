// Batshit Technology - Ergonomic Singularity Grenade.
// A thrown weapon (a vanilla-style grenade, made at the Artificer). Its
// projectile opens a singularity where it lands. For five seconds the
// singularity rips fixtures loose (furniture uninstalled, walls torn down to
// their materials, plants uprooted) and drags pawns and items toward its
// centre, ever faster; then it explodes. Every pawn it caught who survives
// comes away with a huge mood boost and a quip about how ergonomic it was.
// The pull itself is the shared Implosion (Implosion.cs), which the Tractor
// Bomb and the Colony Bomb reuse.

using System.Collections.Generic;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BatshitTechnology
{
    /// <summary>The singularity's defs.</summary>
    [DefOf]
    public static class BT_SingularityDefOf
    {
        /// <summary>The singularity a grenade opens.</summary>
        public static ThingDef BT_SingularityField;

        /// <summary>Lets RimWorld bind the fields above at load.</summary>
        static BT_SingularityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_SingularityDefOf));
        }
    }

    /// <summary>
    /// The singularity's extra material, loaded at startup on the main
    /// thread: RimWorld requires that of every static asset.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BT_SingularityAssets
    {
        /// <summary>The texture of the vortex's outer, counter-rotating layer.</summary>
        public const string OuterVortexPath = "Things/Mote/BT_SingularityVortexOuter";

        /// <summary>The vortex's outer, counter-rotating layer.</summary>
        public static readonly Material OuterVortex =
            MaterialPool.MatFrom(OuterVortexPath, ShaderDatabase.Transparent);

        /// <summary>
        /// The texture of the vortex's main layer, for hosts whose own
        /// graphic is something else (the Tractor Bomb).
        /// </summary>
        public const string InnerVortexPath = "Things/Mote/BT_SingularityVortex";

        /// <summary>The vortex's main layer.</summary>
        public static readonly Material InnerVortex =
            MaterialPool.MatFrom(InnerVortexPath, ShaderDatabase.Transparent);
    }

    /// <summary>
    /// Draws a singularity's vortex, for every implosion host: the
    /// grenade's field and the Tractor Bomb.
    /// </summary>
    public static class SingularityVortex
    {
        /// <summary>
        /// Draws the vortex in two layers: the main one spinning faster and
        /// growing as the pull goes on, and a fainter outer one, larger,
        /// beneath it and spinning the other way, for depth. Both throb in
        /// the final second.
        /// </summary>
        /// <param name="drawLoc">Where to draw; its height is replaced.</param>
        /// <param name="seconds">Seconds the pull has run.</param>
        /// <param name="totalSeconds">
        /// The pull's intended length, in seconds; must be positive.
        /// </param>
        /// <param name="radius">The pull radius, in tiles.</param>
        /// <param name="inner">The main layer's material.</param>
        public static void Draw(
            Vector3 drawLoc, float seconds, float totalSeconds, float radius,
            Material inner)
        {
            if (inner == null || !(totalSeconds > 0f))
            {
                return;
            }

            float progress = Mathf.Clamp01(seconds / totalSeconds);
            // Turns: 0.3 per second at the start, accelerating to 3.
            float turns = (0.3f * seconds)
                + (2.7f * seconds * seconds / (2f * totalSeconds));
            float pulse = (float)SingularityPull.FinalPulse(
                totalSeconds - seconds);
            float size = radius * 2f * (0.35f + (0.65f * progress)) * pulse;

            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            float outer = size * 1.25f;
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(
                    pos - new Vector3(0f, 0.002f, 0f),
                    Quaternion.AngleAxis(turns * 0.6f * 360f, Vector3.up),
                    new Vector3(outer, 1f, outer)),
                BT_SingularityAssets.OuterVortex, 0);
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(
                    pos, Quaternion.AngleAxis(-turns * 360f, Vector3.up),
                    new Vector3(size, 1f, size)),
                inner, 0);
        }
    }

    /// <summary>The singularity's settings, on its ThingDef.</summary>
    public class SingularityExtension : DefModExtension
    {
        /// <summary>How far it reaches, in tiles.</summary>
        public float radius = 10f;

        /// <summary>How long it pulls before it explodes, in ticks.</summary>
        public int pullTicks = 300;

        /// <summary>Drag speed at the start, in cells per second.</summary>
        public float pullRateStart = 0.6f;

        /// <summary>Drag speed at the end, in cells per second.</summary>
        public float pullRateEnd = 12f;

        /// <summary>
        /// Ticks for its reach to rip fixtures loose to grow from the centre
        /// to the full radius.
        /// </summary>
        public int ripTicks = 120;

        /// <summary>The explosion's damage type.</summary>
        public DamageDef explosionDamageDef;

        /// <summary>The explosion's radius, in tiles.</summary>
        public float explosionRadius = 5.9f;

        /// <summary>The explosion's damage; -1 uses the type's default.</summary>
        public int explosionDamage = 65;

        /// <summary>Ticks after the explosion before survivors are counted.</summary>
        public int survivorDelayTicks = 30;

        /// <summary>The memory survivors get, labelled with one of its quips.</summary>
        public ThoughtDef survivorThought;

        /// <summary>
        /// The settings of the singularity's pull: this extension's radius
        /// and timings, gathering as close to the centre as it can, ripping
        /// fixtures loose, and lasting exactly its ramp (its minimum and
        /// maximum durations are both <see cref="pullTicks"/>).
        /// </summary>
        /// <returns>New settings for an <see cref="Implosion"/>.</returns>
        public ImplosionSettings MakeImplosionSettings() =>
            new ImplosionSettings
            {
                pullRadius = radius,
                gatherRadius = 0f,
                pullTicks = pullTicks,
                pullRateStart = pullRateStart,
                pullRateEnd = pullRateEnd,
                ripFixtures = true,
                ripTicks = ripTicks,
                minDurationTicks = pullTicks,
                maxDurationTicks = pullTicks,
            };

        /// <summary>Reports mistakes in the settings.</summary>
        /// <returns>One message per problem.</returns>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            ImplosionSettings pull = MakeImplosionSettings();
            foreach (string problem in
                pull.Problems(GenRadial.MaxRadialPatternRadius))
            {
                yield return "singularity " + problem;
            }

            if (survivorThought == null)
            {
                yield return "singularity needs a survivorThought";
            }
        }
    }

    /// <summary>
    /// The thrown grenade: opens a singularity where it goes off. An
    /// explosive projectile as far as RimWorld is concerned (thrown verbs
    /// with a forced-miss radius require one), but its "explosion" is the
    /// singularity.
    /// </summary>
    public class Projectile_SingularityGrenade : Projectile_Explosive
    {
        /// <summary>Goes off: the grenade becomes a singularity.</summary>
        protected override void Explode()
        {
            Map map = Map;
            IntVec3 cell = Position;
            Thing thrower = launcher;
            Destroy();
            var field = (SingularityField)ThingMaker.MakeThing(BT_SingularityDefOf.BT_SingularityField);
            field.Instigator = thrower;
            GenSpawn.Spawn(field, cell, map);
        }
    }

    /// <summary>
    /// The singularity: an <see cref="Implosion"/> that rips fixtures loose
    /// and drags pawns and items in ever faster, drawn as a vortex; then it
    /// explodes and leaves survivors feeling wonderful.
    /// </summary>
    public class SingularityField : ThingWithComps
    {
        private const int TicksPerSecond = Implosion.TicksPerSecond;

        private Thing instigator;
        private Implosion implosion;
        private bool exploded;
        private int ticksSinceExplosion;

        /// <summary>
        /// Who threw it, for the explosion's and the stuns' blame. May be
        /// null.
        /// </summary>
        public Thing Instigator
        {
            get => instigator;
            set
            {
                instigator = value;
                if (implosion != null)
                {
                    implosion.Instigator = value;
                }
            }
        }

        /// <summary>Its settings.</summary>
        public SingularityExtension Settings =>
            def.GetModExtension<SingularityExtension>() ?? new SingularityExtension();

        /// <summary>Drag steps taken so far (diagnostics).</summary>
        public int StepsTaken => implosion?.StepsTaken ?? 0;

        /// <summary>Fixtures uninstalled so far (diagnostics).</summary>
        public int Uninstalled => implosion?.Uninstalled ?? 0;

        /// <summary>Fixtures torn down so far (diagnostics).</summary>
        public int TornDown => implosion?.TornDown ?? 0;

        /// <summary>Item moves made so far (diagnostics).</summary>
        public int ItemsMoved => implosion?.ItemsMoved ?? 0;

        /// <summary>Pawns caught so far (diagnostics).</summary>
        public int CaughtCount => implosion?.CaughtCount ?? 0;

        /// <summary>Whether it has exploded yet.</summary>
        public bool Exploded => exploded;

        /// <summary>Survivors given the memory this session, across all singularities.</summary>
        public static int SurvivorsComforted { get; private set; }

        /// <summary>
        /// Opens its implosion where it stands, with a shudder. A field
        /// loaded from a save made before the implosion was extracted has
        /// none, and starts its pull afresh.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            implosion ??= new Implosion(
                Position, Settings.MakeImplosionSettings(), this, instigator);
            if (!respawningAfterLoad)
            {
                SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(Position, map));
                if (map == Find.CurrentMap)
                {
                    Find.CameraDriver.shaker.DoShake(0.6f);
                }
            }
        }

        /// <summary>
        /// Saves and loads its progress, its implosion's included.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref instigator, "instigator");
            Scribe_Values.Look(ref exploded, "exploded");
            Scribe_Values.Look(
                ref ticksSinceExplosion, "ticksSinceExplosion");
            Scribe_Deep.Look(ref implosion, "implosion");
        }

        /// <summary>
        /// One tick: the implosion pulls until it is over (exactly its
        /// five seconds); then the field explodes, and comforts the
        /// survivors a moment later.
        /// </summary>
        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || implosion == null)
            {
                return;
            }

            SingularityExtension s = Settings;
            if (!exploded)
            {
                implosion.Tick(Map);
                if (implosion.Over)
                {
                    Explode(s);
                }
            }
            else if (++ticksSinceExplosion >= s.survivorDelayTicks)
            {
                ComfortSurvivors(s);
                Destroy();
            }
        }

        /// <summary>The explosion, on whatever has piled up at the centre.</summary>
        /// <param name="s">The settings.</param>
        private void Explode(SingularityExtension s)
        {
            exploded = true;
            GenExplosion.DoExplosion(
                Position, Map, s.explosionRadius, s.explosionDamageDef ?? DamageDefOf.Bomb,
                instigator, s.explosionDamage);
        }

        /// <summary>
        /// Every caught pawn still alive gets the memory, labelled with a
        /// quip, which they also say out loud.
        /// </summary>
        /// <param name="s">The settings.</param>
        private void ComfortSurvivors(SingularityExtension s)
        {
            MonologueExtension quips = s.survivorThought?.GetModExtension<MonologueExtension>();
            foreach (Pawn pawn in implosion.Caught)
            {
                if (pawn == null || pawn.Dead || !(pawn.needs?.mood?.thoughts?.memories is MemoryThoughtHandler memories))
                {
                    continue;
                }

                var thought = (Thought_Quip)ThoughtMaker.MakeThought(s.survivorThought, null);
                if (quips != null && !quips.lines.NullOrEmpty())
                {
                    thought.Line = quips.lines[quips.NextIndex(-1)];
                    if (pawn.Spawned)
                    {
                        MoteMaker.ThrowText(
                            pawn.DrawPos + new Vector3(0f, 0f, 0.7f), pawn.Map, thought.Line,
                            new Color(0.95f, 0.65f, 1f), 6f);
                    }
                }

                memories.RemoveMemoriesOfDef(s.survivorThought);
                memories.TryGainMemory(thought);
                SurvivorsComforted++;
            }
        }

        /// <summary>
        /// Draws the vortex in two layers: the main one spinning faster and
        /// growing as the pull goes on, and a fainter outer one, larger,
        /// beneath it and spinning the other way, for depth. Both throb in
        /// the final second. Gone once it explodes.
        /// </summary>
        /// <param name="drawLoc">Where to draw.</param>
        /// <param name="flip">Unused.</param>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (exploded || def.graphicData == null || implosion == null)
            {
                return;
            }

            SingularityVortex.Draw(
                drawLoc, implosion.ElapsedTicks / (float)TicksPerSecond,
                implosion.PullTicks / (float)TicksPerSecond,
                implosion.PullRadius, Graphic.MatSingle);
        }
    }

    /// <summary>
    /// A memory labelled with a quip instead of its stage label: what the
    /// pawn said when it happened.
    /// </summary>
    public class Thought_Quip : Thought_Memory
    {
        private string line;

        /// <summary>The quip shown as the label.</summary>
        public string Line
        {
            get => line;
            set => line = value;
        }

        /// <summary>The quoted quip, or the Def's label if none was set.</summary>
        public override string LabelCap =>
            line.NullOrEmpty() ? base.LabelCap : $"“{line}”";

        /// <summary>Saves and loads the quip.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref line, "line");
        }
    }
}
