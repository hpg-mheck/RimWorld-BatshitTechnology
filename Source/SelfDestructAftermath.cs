// Batshit Technology - Random Replicator self-destruct aftermath.
// The main blast is a vanilla Bomb explosion, whose wave destroys every fire
// it touches (DamageWorker.ExplosionDamageThing in 1.6). So the fires are
// lit just behind the wave as it travels out, and secondary explosions go
// off inside the expanding circle and for a few seconds afterwards.

using System;
using System.Collections.Generic;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// One kind of secondary explosion in the self-destruct aftermath, from
    /// CompProperties_RandomReplicator.selfDestructSecondaries.
    /// </summary>
    public class SecondaryExplosion
    {
        /// <summary>
        /// Damage type. Avoid Bomb itself: its explosions put out every fire
        /// they touch. BombSuper looks the same and leaves fires burning.
        /// </summary>
        public DamageDef damageDef;

        /// <summary>Damage per hit; -1 uses the damage type's default.</summary>
        public int damage = -1;

        /// <summary>Explosion radius, in tiles, picked at random.</summary>
        public FloatRange radius = new FloatRange(1.9f, 3.9f);

        /// <summary>Relative likelihood among the kinds.</summary>
        public float weight = 1f;

        /// <summary>Chance to start a fire in each cell it hits.</summary>
        public float chanceToStartFire;

        /// <summary>Reports mistakes in one entry.</summary>
        /// <returns>One message per problem.</returns>
        public IEnumerable<string> ConfigErrors()
        {
            if (damageDef == null)
            {
                yield return "a selfDestructSecondaries entry has no damageDef";
            }

            if (weight <= 0f)
            {
                yield return "selfDestructSecondaries weights must be positive";
            }

            if (radius.min <= 0f || radius.max > 15f || radius.min > radius.max)
            {
                yield return "selfDestructSecondaries radius must lie within "
                    + "(0, 15] tiles, min first";
            }
        }
    }

    /// <summary>
    /// Runs every self-destruct aftermath on a map: fires that follow the
    /// main blast wave out, and the secondary explosions. Saved with the
    /// map, so a save made mid-blast resumes it.
    /// </summary>
    public class MapComponent_SelfDestructAftermath : MapComponent
    {
        /// <summary>
        /// Fires are lit this many ticks after the wave reaches a cell, so the
        /// wave has already passed and cannot destroy them.
        /// </summary>
        private const int FireLagTicks = 3;

        private const int TicksPerSecond = 60;

        private List<Blast> blasts = new List<Blast>();

        /// <summary>Created by RimWorld for every map.</summary>
        /// <param name="map">The map this component belongs to.</param>
        public MapComponent_SelfDestructAftermath(Map map)
            : base(map)
        {
        }

        /// <summary>True while any aftermath is still running.</summary>
        public bool Active => blasts.Count > 0;

        /// <summary>Secondary explosions set off this session (diagnostics).</summary>
        public int SecondariesFired { get; private set; }

        /// <summary>Fires started behind the wave this session (diagnostics).</summary>
        public int FiresStarted { get; private set; }

        /// <summary>
        /// Farthest a secondary explosion has landed from its blast's centre
        /// this session, in tiles (diagnostics).
        /// </summary>
        public float MaxSecondaryDistance { get; private set; }

        /// <summary>
        /// Starts the aftermath of a blast that began this tick.
        /// </summary>
        /// <param name="center">The blast's centre.</param>
        /// <param name="source">
        /// The replicator's ThingDef, whose comp properties set the radius,
        /// fires and secondaries.
        /// </param>
        public void Begin(IntVec3 center, ThingDef source)
        {
            if (source?.GetCompProperties<CompProperties_RandomReplicator>() == null)
            {
                Log.Error("[Batshit Technology] self-destruct aftermath needs a "
                    + $"replicator def, got {source?.defName ?? "null"}");
                return;
            }

            blasts.Add(new Blast
            {
                center = center,
                startTick = Find.TickManager.TicksGame,
                source = source,
            });
        }

        /// <summary>Advances every running aftermath by one tick.</summary>
        public override void MapComponentTick()
        {
            for (int i = blasts.Count - 1; i >= 0; i--)
            {
                bool running;
                try
                {
                    running = TickBlast(blasts[i]);
                }
                catch (Exception ex)
                {
                    Log.Error($"[Batshit Technology] self-destruct aftermath failed: {ex}");
                    running = false;
                }

                if (!running)
                {
                    blasts.RemoveAt(i);
                }
            }
        }

        /// <summary>Saves and loads the running aftermaths.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref blasts, "blasts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                blasts ??= new List<Blast>();
                blasts.RemoveAll(b => b?.source == null);
            }
        }

        /// <summary>One tick of one aftermath.</summary>
        /// <param name="blast">The aftermath.</param>
        /// <returns>False once it has finished.</returns>
        private bool TickBlast(Blast blast)
        {
            var props = blast.source.GetCompProperties<CompProperties_RandomReplicator>();
            int ticks = Find.TickManager.TicksGame - blast.startTick;
            float radius = props.selfDestructRadius;

            // Fires, cell by cell in order of distance, just behind the wave.
            double fireFront = BlastSchedule.WaveFront(ticks - FireLagTicks, 1d);
            int cellCount = GenRadial.NumCellsInRadius(radius);
            while (blast.nextFireCell < cellCount
                && GenRadial.RadialPatternRadii[blast.nextFireCell] <= fireFront)
            {
                IntVec3 c = blast.center + GenRadial.RadialPattern[blast.nextFireCell++];
                if (c.InBounds(map) && Rand.Chance(props.selfDestructFireChance)
                    && FireUtility.TryStartFireIn(c, map, props.selfDestructFireSize, null))
                {
                    FiresStarted++;
                }
            }

            // Secondary explosions inside the expanding circle, then
            // anywhere in it until the aftershocks end.
            int endTick = BlastSchedule.WaveTicks(radius, 1d)
                + Mathf.RoundToInt(props.selfDestructAftershockSeconds * TicksPerSecond);
            if (ticks <= endTick && !props.selfDestructSecondaries.NullOrEmpty())
            {
                int due = BlastSchedule.DueThisTick(
                    ref blast.pending, props.selfDestructSecondariesPerSecond, TicksPerSecond);
                double outer = Math.Min(radius, BlastSchedule.WaveFront(ticks, 1d));
                for (int i = 0; i < due; i++)
                {
                    SetOffSecondary(blast.center, outer, props);
                }
            }

            return blast.nextFireCell < cellCount || ticks <= endTick;
        }

        /// <summary>One secondary explosion at a random spot.</summary>
        /// <param name="center">The blast's centre.</param>
        /// <param name="outer">How far out it may land, in tiles.</param>
        /// <param name="props">The replicator's properties.</param>
        private void SetOffSecondary(
            IntVec3 center, double outer, CompProperties_RandomReplicator props)
        {
            SecondaryExplosion kind = props.selfDestructSecondaries
                .RandomElementByWeight(k => k.weight);
            var (x, z) = BlastSchedule.PointInAnnulus(
                outer, props.selfDestructSecondaryInnerFraction, Rand.Value, Rand.Value);
            IntVec3 cell = center + new IntVec3(
                Mathf.RoundToInt((float)x), 0, Mathf.RoundToInt((float)z));
            if (!cell.InBounds(map))
            {
                return;
            }

            GenExplosion.DoExplosion(
                cell, map, kind.radius.RandomInRange, kind.damageDef, null,
                kind.damage, chanceToStartFire: kind.chanceToStartFire,
                screenShakeFactor: 0.25f);
            SecondariesFired++;
            MaxSecondaryDistance = Mathf.Max(
                MaxSecondaryDistance, (cell - center).LengthHorizontal);
        }

        /// <summary>The saved state of one running aftermath.</summary>
        private class Blast : IExposable
        {
            /// <summary>The blast's centre.</summary>
            public IntVec3 center;

            /// <summary>The tick the main explosion started.</summary>
            public int startTick;

            /// <summary>The replicator def whose properties apply.</summary>
            public ThingDef source;

            /// <summary>Index into GenRadial's pattern of the next cell to light.</summary>
            public int nextFireCell;

            /// <summary>Fractional secondary explosions carried between ticks.</summary>
            public double pending;

            /// <summary>Saves and loads this aftermath.</summary>
            public void ExposeData()
            {
                Scribe_Values.Look(ref center, "center");
                Scribe_Values.Look(ref startTick, "startTick");
                Scribe_Defs.Look(ref source, "source");
                Scribe_Values.Look(ref nextFireCell, "nextFireCell");
                Scribe_Values.Look(ref pending, "pending");
            }
        }
    }
}
