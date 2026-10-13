// Batshit Technology - what keeps happening after a self-destruct.
// MapComponent_SelfDestructAftermath ticks every running aftermath and saves
// them with the map:
// - Aftermath_FireAndSecondaries (the Random Replicator): a vanilla Bomb
//   wave destroys every fire it touches (DamageWorker.ExplosionDamageThing
//   in 1.6), so fires are lit just behind the wave as it travels out, and
//   secondary explosions go off inside the expanding circle and for a few
//   seconds afterwards.
// - Aftermath_BoltBarrage (the Batshittium Reactor): volleys of bolts in
//   every direction; farther targets take longer to reach, so the impacts
//   spread outward as a wave.

using System;
using System.Collections.Generic;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// One running aftermath. Subclasses are saved with the map; they read
    /// their settings from the source device's def each tick.
    /// </summary>
    public abstract class Aftermath : IExposable
    {
        /// <summary>Where the device stood.</summary>
        public IntVec3 center;

        /// <summary>The tick the detonation happened.</summary>
        public int startTick;

        /// <summary>The device's def, whose detonation settings apply.</summary>
        public ThingDef source;

        /// <summary>The source def's self-destruct properties, or null.</summary>
        protected CompProperties_SelfDestruct SelfDestruct =>
            source?.GetCompProperties<CompProperties_SelfDestruct>();

        /// <summary>Advances this aftermath by one tick.</summary>
        /// <param name="map">The map.</param>
        /// <param name="owner">The map component, for its counters.</param>
        /// <returns>False once it has finished.</returns>
        public abstract bool Tick(Map map, MapComponent_SelfDestructAftermath owner);

        /// <summary>Saves and loads the shared state.</summary>
        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Defs.Look(ref source, "source");
        }
    }

    /// <summary>
    /// Fires just behind a Bomb wave, and secondary explosions inside the
    /// expanding circle: the Random Replicator's self-destruct.
    /// </summary>
    public class Aftermath_FireAndSecondaries : Aftermath
    {
        /// <summary>
        /// Fires are lit this many ticks after the wave reaches a cell, so
        /// the wave has already passed and cannot destroy them.
        /// </summary>
        private const int FireLagTicks = 3;

        private const int TicksPerSecond = 60;

        /// <summary>Index into GenRadial's pattern of the next cell to light.</summary>
        public int nextFireCell;

        /// <summary>Fractional secondary explosions carried between ticks.</summary>
        public double pending;

        /// <summary>
        /// Lights the cells the wave has passed, and sets off the secondaries
        /// due this tick.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="owner">The map component, for its counters.</param>
        /// <returns>False once the fires are all lit and the aftershocks over.</returns>
        public override bool Tick(Map map, MapComponent_SelfDestructAftermath owner)
        {
            CompProperties_SelfDestruct props = SelfDestruct;
            if (!(props?.detonation is Detonation_Bomb bomb))
            {
                return false;
            }

            int ticks = Find.TickManager.TicksGame - startTick;
            float radius = props.blastRadius;

            // Fires, cell by cell in order of distance, just behind the wave.
            double fireFront = BlastSchedule.WaveFront(ticks - FireLagTicks, 1d);
            int cellCount = GenRadial.NumCellsInRadius(radius);
            while (nextFireCell < cellCount
                && GenRadial.RadialPatternRadii[nextFireCell] <= fireFront)
            {
                IntVec3 c = center + GenRadial.RadialPattern[nextFireCell++];
                if (c.InBounds(map) && Rand.Chance(bomb.fireChance)
                    && FireUtility.TryStartFireIn(c, map, bomb.fireSize, null))
                {
                    owner.FiresStarted++;
                }
            }

            // Secondary explosions inside the expanding circle, then
            // anywhere in it until the aftershocks end.
            int endTick = BlastSchedule.WaveTicks(radius, 1d)
                + Mathf.RoundToInt(bomb.aftershockSeconds * TicksPerSecond);
            if (ticks <= endTick && !bomb.secondaries.NullOrEmpty())
            {
                int due = BlastSchedule.DueThisTick(
                    ref pending, bomb.secondariesPerSecond, TicksPerSecond);
                double outer = Math.Min(radius, BlastSchedule.WaveFront(ticks, 1d));
                for (int i = 0; i < due; i++)
                {
                    SetOffSecondary(map, owner, bomb, outer);
                }
            }

            return nextFireCell < cellCount || ticks <= endTick;
        }

        /// <summary>One secondary explosion at a random spot.</summary>
        /// <param name="map">The map.</param>
        /// <param name="owner">The map component, for its counters.</param>
        /// <param name="bomb">The detonation's settings.</param>
        /// <param name="outer">How far out it may land, in tiles.</param>
        private void SetOffSecondary(
            Map map, MapComponent_SelfDestructAftermath owner, Detonation_Bomb bomb, double outer)
        {
            SecondaryExplosion kind = bomb.secondaries.RandomElementByWeight(k => k.weight);
            var (x, z) = BlastSchedule.PointInAnnulus(
                outer, bomb.secondaryInnerFraction, Rand.Value, Rand.Value);
            IntVec3 cell = center + new IntVec3(Mathf.RoundToInt((float)x), 0, Mathf.RoundToInt((float)z));
            if (!cell.InBounds(map))
            {
                return;
            }

            GenExplosion.DoExplosion(
                cell, map, kind.radius.RandomInRange, kind.damageDef, null,
                kind.damage, chanceToStartFire: kind.chanceToStartFire,
                screenShakeFactor: 0.25f);
            owner.SecondariesFired++;
            owner.MaxSecondaryDistance = Mathf.Max(
                owner.MaxSecondaryDistance, (cell - center).LengthHorizontal);
        }

        /// <summary>Saves and loads this aftermath.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextFireCell, "nextFireCell");
            Scribe_Values.Look(ref pending, "pending");
        }
    }

    /// <summary>
    /// Volleys of bolts launched in every direction at once: the Batshittium
    /// Reactor's self-destruct.
    /// </summary>
    public class Aftermath_BoltBarrage : Aftermath
    {
        /// <summary>Where the bolts leave from (the device's true centre).</summary>
        public Vector3 origin;

        /// <summary>Volleys launched so far.</summary>
        public int volleysFired;

        /// <summary>Launches each volley when its time comes.</summary>
        /// <param name="map">The map.</param>
        /// <param name="owner">The map component, for its counters.</param>
        /// <returns>False once every volley is away.</returns>
        public override bool Tick(Map map, MapComponent_SelfDestructAftermath owner)
        {
            CompProperties_SelfDestruct props = SelfDestruct;
            if (!(props?.detonation is Detonation_BoltBarrage barrage) || barrage.projectile == null)
            {
                return false;
            }

            int ticks = Find.TickManager.TicksGame - startTick;
            while (volleysFired < barrage.volleys
                && ticks >= volleysFired * barrage.volleyIntervalTicks)
            {
                volleysFired++;
                float inner = Mathf.Clamp01(barrage.innerRadius / props.blastRadius);
                for (int i = 0; i < barrage.boltsPerVolley; i++)
                {
                    if (BoltLauncher.TryLaunchInAnnulus(
                        barrage.projectile, map, center, origin, props.blastRadius, inner,
                        out float distance))
                    {
                        owner.BoltsLaunched++;
                        owner.MaxBoltDistance = Mathf.Max(owner.MaxBoltDistance, distance);
                    }
                }
            }

            return volleysFired < barrage.volleys;
        }

        /// <summary>Saves and loads this aftermath.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref volleysFired, "volleysFired");
        }
    }

    /// <summary>
    /// Runs every aftermath on a map, and keeps session counters the smoke
    /// test reads. Saved with the map, so a save made mid-blast resumes it.
    /// </summary>
    public class MapComponent_SelfDestructAftermath : MapComponent
    {
        private List<Aftermath> running = new List<Aftermath>();

        /// <summary>Created by RimWorld for every map.</summary>
        /// <param name="map">The map this component belongs to.</param>
        public MapComponent_SelfDestructAftermath(Map map)
            : base(map)
        {
        }

        /// <summary>True while any aftermath is still running.</summary>
        public bool Active => running.Count > 0;

        /// <summary>Secondary explosions set off this session.</summary>
        public int SecondariesFired { get; set; }

        /// <summary>Fires started behind a wave this session.</summary>
        public int FiresStarted { get; set; }

        /// <summary>Farthest a secondary has landed from its centre, in tiles.</summary>
        public float MaxSecondaryDistance { get; set; }

        /// <summary>Barrage and cook-off bolts launched this session.</summary>
        public int BoltsLaunched { get; set; }

        /// <summary>Farthest a launched bolt was aimed from its origin, in tiles.</summary>
        public float MaxBoltDistance { get; set; }

        /// <summary>Death blasts set off this session.</summary>
        public int DeathBlasts { get; set; }

        /// <summary>Starts an aftermath that began this tick.</summary>
        /// <param name="aftermath">The aftermath, with its centre and source set.</param>
        public void Begin(Aftermath aftermath)
        {
            aftermath.startTick = Find.TickManager.TicksGame;
            running.Add(aftermath);
        }

        /// <summary>Advances every running aftermath by one tick.</summary>
        public override void MapComponentTick()
        {
            for (int i = running.Count - 1; i >= 0; i--)
            {
                bool keep;
                try
                {
                    keep = running[i].Tick(map, this);
                }
                catch (Exception ex)
                {
                    Log.Error($"[Batshit Technology] aftermath failed: {ex}");
                    keep = false;
                }

                if (!keep)
                {
                    running.RemoveAt(i);
                }
            }
        }

        /// <summary>Saves and loads the running aftermaths.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref running, "aftermaths", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                running ??= new List<Aftermath>();
                running.RemoveAll(a => a?.source == null);
            }
        }
    }
}
