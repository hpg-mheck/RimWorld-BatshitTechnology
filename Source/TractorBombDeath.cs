// Batshit Technology - a Tractor Bomb's death (docs/TRACTOR-BOMB.md).
// docs/ARTISTIC-MOTIF.md: every death is distinct. A robot wrecked before
// it emplaces does not simply explode: its parts burst out of the wreck,
// hang for a moment, and are sucked back into a sputtering miniature
// vortex, its tractor beam's last act, which then pops. It takes its own
// parts and nothing else: nothing in the world is moved, hurt or set
// alight. The motion is Core's DeathVortexMath; this draws and times it.

using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BatshitTechnology
{
    /// <summary>
    /// The sputtering vortex left where a robot was wrecked, drawing its
    /// parts back in before it pops. Purely a spectacle: it touches nothing.
    /// </summary>
    public class TractorBombDeathVortex : Thing
    {
        /// <summary>The vortex's full size, in tiles across.</summary>
        private const float VortexTiles = 1.6f;

        /// <summary>A part's drawn size, in tiles.</summary>
        private const float PartTiles = 0.55f;

        /// <summary>The farthest a part bursts out, in tiles.</summary>
        private const float MaxReach = 2.2f;

        /// <summary>The nearest a part bursts out, in tiles.</summary>
        private const float MinReach = 1.2f;

        /// <summary>
        /// Its sparks and puffs: a grey with batshittium in it.
        /// </summary>
        private static readonly Color DustColor = new Color(0.6f, 0.4f, 0.7f);

        private int ticks;
        private int seed;

        /// <summary>
        /// Vortexes that have popped this session (diagnostics).
        /// </summary>
        public static int Pops { get; private set; }

        /// <summary>Ticks since the death.</summary>
        public int Ticks => ticks;

        /// <summary>Whether it has finished and popped.</summary>
        public bool Popped => ticks >= DeathVortexMath.TotalTicks;

        /// <summary>
        /// Seeds its parts' directions and its sputter, and throws the first
        /// sparks.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="respawningAfterLoad">
        /// True when loading a save.
        /// </param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                seed = Rand.Int;
                FleckMaker.ThrowMicroSparks(DrawPos, map);
                FleckMaker.ThrowDustPuffThick(DrawPos, map, 1.2f, DustColor);
                SoundDefOf.Power_OffSmall.PlayOneShot(
                    new TargetInfo(Position, map));
            }
        }

        /// <summary>Saves and loads its progress.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticks, "ticks");
            Scribe_Values.Look(ref seed, "seed");
        }

        /// <summary>
        /// One tick: it sputters sparks while the parts come in, and pops
        /// at the end.
        /// </summary>
        protected override void Tick()
        {
            if (!Spawned)
            {
                return;
            }

            ticks++;
            if (ticks > DeathVortexMath.FlingTicks && ticks % 7 == 0)
            {
                FleckMaker.ThrowMicroSparks(
                    DrawPos + (Rand.InsideUnitCircleVec3 * 0.4f), Map);
            }

            if (ticks >= DeathVortexMath.TotalTicks)
            {
                Pop();
            }
        }

        /// <summary>
        /// The pop: a flash, a magenta puff and a sound, then nothing.
        /// </summary>
        private void Pop()
        {
            Map map = Map;
            Vector3 at = DrawPos;
            Pops++;
            SoundDefOf.Explosion_FirefoamPopper.PlayOneShot(
                new TargetInfo(Position, map));
            FleckMaker.ThrowLightningGlow(at, map, 1.5f);
            FleckMaker.ThrowDustPuffThick(at, map, 2f, DustColor);
            Destroy(DestroyMode.Vanish);
        }

        /// <summary>
        /// Draws the vortex, sputtering, and the parts on their way in,
        /// spinning as they go.
        /// </summary>
        /// <param name="drawLoc">Where to draw.</param>
        /// <param name="flip">Unused.</param>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float size = (float)DeathVortexMath.VortexSize(ticks, seed)
                * VortexTiles;
            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            if (size > 0.01f)
            {
                float spin = ticks * 14f;
                Graphics.DrawMesh(
                    MeshPool.plane10,
                    Matrix4x4.TRS(
                        pos - new Vector3(0f, 0.002f, 0f),
                        Quaternion.AngleAxis(spin * 0.6f, Vector3.up),
                        new Vector3(size * 1.25f, 1f, size * 1.25f)),
                    BT_SingularityAssets.OuterVortex, 0);
                Graphics.DrawMesh(
                    MeshPool.plane10,
                    Matrix4x4.TRS(
                        pos, Quaternion.AngleAxis(-spin, Vector3.up),
                        new Vector3(size, 1f, size)),
                    BT_SingularityAssets.InnerVortex, 0);
            }

            Material[] parts = BT_TractorBombAssets.Parts;
            var rand = new System.Random(seed);
            for (int i = 0; i < parts.Length; i++)
            {
                double angle = DeathVortexMath.PartAngle(
                    i, parts.Length, rand.NextDouble());
                double reach = MinReach
                    + ((MaxReach - MinReach) * rand.NextDouble());
                float tumble = (float)(rand.NextDouble() * 360.0);
                var (x, z, scale) = DeathVortexMath.PartAt(
                    reach, angle, ticks);
                float s = PartTiles * (float)scale;
                Vector3 at = pos + new Vector3((float)x, 0.001f, (float)z);
                Graphics.DrawMesh(
                    MeshPool.plane10,
                    Matrix4x4.TRS(
                        at,
                        Quaternion.AngleAxis(
                            tumble + (ticks * 9f), Vector3.up),
                        new Vector3(s, 1f, s)),
                    parts[i], 0);
            }
        }
    }
}
