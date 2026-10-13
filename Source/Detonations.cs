// Batshit Technology - detonations: what a device does when it blows up.
// Self-destructs (CompSelfDestruct) and death blasts (CompDeathBlast) both
// take a BatshitDetonation, chosen in XML with <... Class="...">:
// - Detonation_Bomb: the Random Replicator's blast, fires and secondaries.
// - Detonation_BoltBarrage: the Reactor's barrage of bolts.
// - Detonation_HopperBurst: the Transmogrifier spews rock and batshittium.
// - Detonation_PsychicSqueeze: the Brain Squeezer squeezes everyone nearby.
// - Detonation_BoltCookOff: a Ballista's or Magazine's bolts cook off.
// - Detonation_MoltenSpray: the Artificer sprays fire and half-made bolts.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BatshitTechnology
{
    /// <summary>
    /// What a device does when it blows up, by self-destruct or by being
    /// destroyed. Subclasses are chosen in XML with a Class attribute.
    /// </summary>
    public abstract class BatshitDetonation
    {
        /// <summary>
        /// Sets off the detonation. The device has just been destroyed: it
        /// is off the map, but its def, position and anything it held are
        /// still readable.
        /// </summary>
        /// <param name="device">The destroyed device.</param>
        /// <param name="map">The map it was on.</param>
        /// <param name="center">Where it stood.</param>
        /// <param name="radius">The blast radius, in tiles.</param>
        public abstract void Detonate(Thing device, Map map, IntVec3 center, float radius);

        /// <summary>
        /// What happens, as a phrase for buttons and letters, e.g.
        /// "a ~75-tile blast".
        /// </summary>
        /// <param name="radius">The blast radius, in tiles.</param>
        /// <returns>The phrase.</returns>
        public abstract string Describe(float radius);

        /// <summary>
        /// Whether this detonation consumes what the device holds, so the
        /// device should not spill it on the ground when destroyed.
        /// </summary>
        public virtual bool ConsumesContents => false;

        /// <summary>Reports mistakes in this detonation's settings.</summary>
        /// <returns>One message per problem.</returns>
        public virtual IEnumerable<string> ConfigErrors()
        {
            yield break;
        }

        /// <summary>This map's aftermath component.</summary>
        /// <param name="map">The map.</param>
        /// <returns>The component.</returns>
        protected static MapComponent_SelfDestructAftermath Aftermaths(Map map) =>
            map.GetComponent<MapComponent_SelfDestructAftermath>();
    }

    /// <summary>
    /// One kind of secondary explosion in a <see cref="Detonation_Bomb"/>
    /// aftermath.
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
                yield return "a secondaries entry has no damageDef";
            }

            if (weight <= 0f)
            {
                yield return "secondaries weights must be positive";
            }

            if (radius.min <= 0f || radius.max > 15f || radius.min > radius.max)
            {
                yield return "secondaries radius must lie within (0, 15] tiles, min first";
            }
        }
    }

    /// <summary>
    /// A Bomb blast across the whole radius, then fires that follow its wave
    /// out and secondary explosions inside the expanding circle.
    /// </summary>
    public class Detonation_Bomb : BatshitDetonation
    {
        /// <summary>Blast damage; -1 uses Bomb's default.</summary>
        public int damage = -1;

        /// <summary>Per-cell chance to start a fire as the wave passes.</summary>
        public float fireChance = 0.8f;

        /// <summary>Size of each fire started.</summary>
        public float fireSize = 0.3f;

        /// <summary>Secondary explosions per second.</summary>
        public float secondariesPerSecond = 15f;

        /// <summary>Seconds the secondaries continue after the wave reaches the edge.</summary>
        public float aftershockSeconds = 4f;

        /// <summary>
        /// Secondaries land no closer to the centre than this fraction of
        /// the wave's current radius (0 = anywhere inside it).
        /// </summary>
        public float secondaryInnerFraction = 0.2f;

        /// <summary>The kinds of secondary explosion; empty means none.</summary>
        public List<SecondaryExplosion> secondaries;

        /// <summary>The blast; the aftermath follows it out.</summary>
        /// <param name="device">The destroyed device.</param>
        /// <param name="map">The map.</param>
        /// <param name="center">Where it stood.</param>
        /// <param name="radius">The blast radius.</param>
        public override void Detonate(Thing device, Map map, IntVec3 center, float radius)
        {
            GenExplosion.DoExplosion(center, map, radius, DamageDefOf.Bomb, device, damage);
            Aftermaths(map)?.Begin(new Aftermath_FireAndSecondaries
            {
                center = center,
                source = device.def,
            });
        }

        /// <summary>Describes the blast.</summary>
        /// <param name="radius">The blast radius.</param>
        /// <returns>The phrase.</returns>
        public override string Describe(float radius) =>
            $"a ~{radius:0}-tile blast, fires racing out behind the shockwave and "
            + $"{aftershockSeconds + (radius * 1.5f / 60f):0} seconds of secondary explosions";

        /// <summary>Reports mistakes in the settings.</summary>
        /// <returns>One message per problem.</returns>
        public override IEnumerable<string> ConfigErrors()
        {
            if (secondariesPerSecond < 0f || aftershockSeconds < 0f)
            {
                yield return "secondariesPerSecond and aftershockSeconds cannot be negative";
            }

            if (secondaryInnerFraction < 0f || secondaryInnerFraction > 1f)
            {
                yield return "secondaryInnerFraction must be between 0 and 1";
            }

            foreach (SecondaryExplosion kind in secondaries ?? Enumerable.Empty<SecondaryExplosion>())
            {
                foreach (string error in kind.ConfigErrors())
                {
                    yield return error;
                }
            }
        }
    }

    /// <summary>
    /// A core blast, then volleys of bolts launched in every direction at
    /// once. Farther targets take longer to reach, so the impacts spread out
    /// as a wave: the Ballista at full volley, from the inside out.
    /// </summary>
    public class Detonation_BoltBarrage : BatshitDetonation
    {
        /// <summary>The projectile each bolt is.</summary>
        public ThingDef projectile;

        /// <summary>How many volleys.</summary>
        public int volleys = 3;

        /// <summary>Bolts in each volley.</summary>
        public int boltsPerVolley = 256;

        /// <summary>Ticks between volleys.</summary>
        public int volleyIntervalTicks = 30;

        /// <summary>Bolts land no closer than this many tiles.</summary>
        public float innerRadius = 4f;

        /// <summary>The core blast's damage type.</summary>
        public DamageDef coreDamageDef;

        /// <summary>The core blast's radius, in tiles.</summary>
        public float coreRadius = 7.9f;

        /// <summary>The core blast's damage; -1 uses the type's default.</summary>
        public int coreDamage = -1;

        /// <summary>The core blast; the aftermath launches the volleys.</summary>
        /// <param name="device">The destroyed device.</param>
        /// <param name="map">The map.</param>
        /// <param name="center">Where it stood.</param>
        /// <param name="radius">How far the bolts reach.</param>
        public override void Detonate(Thing device, Map map, IntVec3 center, float radius)
        {
            if (coreDamageDef != null)
            {
                GenExplosion.DoExplosion(
                    center, map, coreRadius, coreDamageDef, device, coreDamage);
            }

            Aftermaths(map)?.Begin(new Aftermath_BoltBarrage
            {
                center = center,
                source = device.def,
                origin = device.TrueCenter(),
                launcher = BoltOrigin.Place(map, center, device.def),
            });
        }

        /// <summary>Describes the barrage.</summary>
        /// <param name="radius">How far the bolts reach.</param>
        /// <returns>The phrase.</returns>
        public override string Describe(float radius) =>
            $"a core blast, then {volleys * boltsPerVolley:N0} explosive bolts flung in "
            + $"every direction, landing in an expanding wave up to {radius:0} tiles out";

        /// <summary>Reports mistakes in the settings.</summary>
        /// <returns>One message per problem.</returns>
        public override IEnumerable<string> ConfigErrors()
        {
            if (projectile?.projectile == null)
            {
                yield return "the bolt barrage needs a projectile def";
            }

            if (volleys < 1 || boltsPerVolley < 1 || volleyIntervalTicks < 0)
            {
                yield return "the bolt barrage needs at least one bolt in one volley";
            }
        }
    }

    /// <summary>Launches loose bolts for barrages and cook-offs.</summary>
    public static class BoltLauncher
    {
        /// <summary>
        /// Launches one projectile at a random cell in an annulus around a
        /// centre, retrying a few times if the cell is off the map.
        /// </summary>
        /// <param name="launcher">
        /// Who fired it: never null. Vanilla makes animals flee from the
        /// launcher of a projectile that lands near them
        /// (Pawn_MindState.Notify_ClamorImpact), and throws if there is none.
        /// A device that blew itself up passes its <see cref="BoltOrigin"/>.
        /// </param>
        /// <param name="projectileDef">The projectile.</param>
        /// <param name="map">The map.</param>
        /// <param name="center">The annulus's centre.</param>
        /// <param name="origin">Where the projectile leaves from.</param>
        /// <param name="outer">The annulus's outer radius.</param>
        /// <param name="innerFraction">Inner radius as a fraction of the outer.</param>
        /// <param name="distance">How far from the centre it was aimed.</param>
        /// <returns>True if a projectile was launched.</returns>
        /// <exception cref="ArgumentNullException">No launcher.</exception>
        public static bool TryLaunchInAnnulus(
            Thing launcher, ThingDef projectileDef, Map map, IntVec3 center, Vector3 origin,
            float outer, float innerFraction, out float distance)
        {
            if (launcher == null)
            {
                throw new ArgumentNullException(nameof(launcher));
            }

            for (int attempt = 0; attempt < 4; attempt++)
            {
                var (x, z) = BlastSchedule.PointInAnnulus(outer, innerFraction, Rand.Value, Rand.Value);
                IntVec3 target = center + new IntVec3(Mathf.RoundToInt((float)x), 0, Mathf.RoundToInt((float)z));
                if (!target.InBounds(map))
                {
                    continue;
                }

                var projectile = (Projectile)GenSpawn.Spawn(projectileDef, center, map);
                projectile.Launch(launcher, origin, target, target, ProjectileHitFlags.All);
                distance = (target - center).LengthHorizontal;
                return true;
            }

            distance = 0f;
            return false;
        }
    }

    // ===== Death blasts: devices without a self-destruct =====

    /// <summary>
    /// XML-facing properties for <see cref="CompDeathBlast"/>: the
    /// pulverization blast a Batshit device without a self-destruct sets
    /// off when it is destroyed.
    /// </summary>
    public class CompProperties_DeathBlast : CompProperties
    {
        /// <summary>The blast radius, in tiles.</summary>
        public float radius = 4.9f;

        /// <summary>What happens when the device is destroyed.</summary>
        public BatshitDetonation blast;

        /// <summary>Binds these properties to their comp class.</summary>
        public CompProperties_DeathBlast()
        {
            compClass = typeof(CompDeathBlast);
        }

        /// <summary>Reports Def mistakes at load.</summary>
        /// <param name="parentDef">The ThingDef carrying this comp.</param>
        /// <returns>One message per configuration error.</returns>
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (blast == null)
            {
                yield return "a death blast needs a blast";
                yield break;
            }

            foreach (string error in blast.ConfigErrors())
            {
                yield return error;
            }

            if (radius <= 0f || radius > GenRadial.MaxRadialPatternRadius)
            {
                yield return "death blast radius must be positive and within the engine maximum";
            }
        }
    }

    /// <summary>
    /// Sets off the device's pulverization blast when it is destroyed by
    /// damage (not deconstructed, uninstalled or replaced).
    /// </summary>
    public class CompDeathBlast : ThingComp
    {
        /// <summary>Typed access to this comp's XML properties.</summary>
        public CompProperties_DeathBlast Props => (CompProperties_DeathBlast)props;

        /// <summary>Whether the device keeps what it holds for the blast.</summary>
        public bool ConsumesContents => Props.blast?.ConsumesContents ?? false;

        /// <summary>Destroyed by damage: the blast.</summary>
        /// <param name="mode">How it was destroyed.</param>
        /// <param name="previousMap">The map it was on.</param>
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (mode != DestroyMode.KillFinalize || previousMap == null || Props.blast == null)
            {
                return;
            }

            Props.blast.Detonate(parent, previousMap, parent.Position, Props.radius);
            MapComponent_SelfDestructAftermath aftermaths =
                previousMap.GetComponent<MapComponent_SelfDestructAftermath>();
            if (aftermaths != null)
            {
                aftermaths.DeathBlasts++;
            }

            if (parent.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    $"{parent.LabelCap} has been pulverized.",
                    new TargetInfo(parent.Position, previousMap),
                    MessageTypeDefOf.NegativeEvent);
            }
        }
    }

    /// <summary>
    /// The Transmogrifier: a blast that spews its hopper of rock chunks and
    /// a few shards of batshittium across the area.
    /// </summary>
    public class Detonation_HopperBurst : BatshitDetonation
    {
        /// <summary>The blast's damage type.</summary>
        public DamageDef damageDef;

        /// <summary>The blast's damage; -1 uses the type's default.</summary>
        public int damage = -1;

        /// <summary>Rock chunks thrown out.</summary>
        public IntRange chunks = new IntRange(6, 10);

        /// <summary>Batshittium thrown out.</summary>
        public IntRange batshittium = new IntRange(1, 3);

        /// <summary>How far the debris flies, in tiles.</summary>
        public float throwRadius = 8f;

        /// <summary>The blast, then the debris.</summary>
        /// <param name="device">The destroyed device.</param>
        /// <param name="map">The map.</param>
        /// <param name="center">Where it stood.</param>
        /// <param name="radius">The blast radius.</param>
        public override void Detonate(Thing device, Map map, IntVec3 center, float radius)
        {
            GenExplosion.DoExplosion(center, map, radius, damageDef ?? DamageDefOf.Bomb, device, damage);
            List<ThingDef> rock = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.thingCategories != null
                    && d.thingCategories.Contains(ThingCategoryDefOf.StoneChunks))
                .ToList();
            int chunkCount = chunks.RandomInRange;
            for (int i = 0; i < chunkCount && rock.Count > 0; i++)
            {
                Throw(ThingMaker.MakeThing(rock.RandomElement()), map, center);
            }

            ThingDef shard = DefDatabase<ThingDef>.GetNamedSilentFail("BT_Batshittium");
            int shards = batshittium.RandomInRange;
            if (shard != null && shards > 0)
            {
                Thing stack = ThingMaker.MakeThing(shard);
                stack.stackCount = shards;
                Throw(stack, map, center);
            }
        }

        /// <summary>Drops a thing at a random standable cell in the throw radius.</summary>
        /// <param name="thing">The debris.</param>
        /// <param name="map">The map.</param>
        /// <param name="center">Where it flies from.</param>
        private void Throw(Thing thing, Map map, IntVec3 center)
        {
            if (!CellFinder.TryFindRandomCellNear(
                center, map, Mathf.CeilToInt(throwRadius),
                c => c.Standable(map) && c.GetFirstItem(map) == null, out IntVec3 cell))
            {
                cell = center;
            }

            GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
            FleckMaker.ThrowDustPuff(cell, map, 1.5f);
        }

        /// <summary>Describes the burst.</summary>
        /// <param name="radius">The blast radius.</param>
        /// <returns>The phrase.</returns>
        public override string Describe(float radius) =>
            "a blast that spews rock and batshittium across the area";
    }

    /// <summary>
    /// The Brain Squeezer: a psychic squeeze. Everyone nearby is stunned,
    /// blurts a line of the squeezer's monologue and comes away
    /// involuntarily squeezed; a small fireball takes the chair.
    /// </summary>
    public class Detonation_PsychicSqueeze : BatshitDetonation
    {
        /// <summary>How long the squeeze stuns, in ticks.</summary>
        public int stunTicks = 240;

        /// <summary>The fireball's radius, in tiles.</summary>
        public float fireballRadius = 2.9f;

        /// <summary>The squeeze, then the fireball.</summary>
        /// <param name="device">The destroyed device.</param>
        /// <param name="map">The map.</param>
        /// <param name="center">Where it stood.</param>
        /// <param name="radius">How far the squeeze reaches.</param>
        public override void Detonate(Thing device, Map map, IntVec3 center, float radius)
        {
            SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(center, map));
            FleckMaker.Static(center, map, FleckDefOf.PsycastAreaEffect, radius * 2f);
            MonologueExtension monologue =
                BT_BrainSqueezerDefOf.BT_BrainSqueezed.GetModExtension<MonologueExtension>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.Dead || !pawn.Position.InHorDistOf(center, radius)
                    || pawn.RaceProps?.IsFlesh != true)
                {
                    continue;
                }

                pawn.stances?.stunner?.StunFor(stunTicks, device, addBattleLog: false);
                if (monologue == null || monologue.lines.NullOrEmpty())
                {
                    continue;
                }

                string line = monologue.lines[monologue.NextIndex(-1)];
                MoteMaker.ThrowText(
                    pawn.DrawPos + new Vector3(0f, 0f, 0.7f), map, line,
                    new Color(0.95f, 0.65f, 1f), 5f);
                if (pawn.needs?.mood?.thoughts?.memories is MemoryThoughtHandler memories)
                {
                    var thought = (Thought_BrainSqueezed)ThoughtMaker.MakeThought(
                        BT_BrainSqueezerDefOf.BT_BrainSqueezed, null);
                    thought.Line = line;
                    memories.TryGainMemory(thought);
                }
            }

            GenExplosion.DoExplosion(
                center, map, fireballRadius, DamageDefOf.Flame, device, chanceToStartFire: 0.6f);
        }

        /// <summary>Describes the squeeze.</summary>
        /// <param name="radius">How far it reaches.</param>
        /// <returns>The phrase.</returns>
        public override string Describe(float radius) =>
            $"a psychic squeeze that stuns everyone within {radius:0} tiles";
    }

    /// <summary>
    /// A Ballista or Ballista Magazine: a blast, and its stored bolts cook
    /// off, flying in every direction. Bolts beyond the cap are pulverized.
    /// </summary>
    public class Detonation_BoltCookOff : BatshitDetonation
    {
        /// <summary>The blast's damage type.</summary>
        public DamageDef damageDef;

        /// <summary>The blast's damage; -1 uses the type's default.</summary>
        public int damage = -1;

        /// <summary>The most bolts that fly; the rest are destroyed.</summary>
        public int maxBolts = 64;

        /// <summary>How far the bolts fly, in tiles.</summary>
        public float throwRadius = 25f;

        /// <summary>The stored bolts fly instead of spilling.</summary>
        public override bool ConsumesContents => true;

        /// <summary>The blast, then the cook-off.</summary>
        /// <param name="device">The destroyed device.</param>
        /// <param name="map">The map.</param>
        /// <param name="center">Where it stood.</param>
        /// <param name="radius">The blast radius.</param>
        public override void Detonate(Thing device, Map map, IntVec3 center, float radius)
        {
            GenExplosion.DoExplosion(center, map, radius, damageDef ?? DamageDefOf.Bomb, device, damage);
            ThingOwner held = (device as IThingHolder)?.GetDirectlyHeldThings();
            if (held == null)
            {
                return;
            }

            // Fly up to the cap, drawn from the stacks in proportion.
            var bolts = new List<ThingDef>();
            foreach (Thing stack in held)
            {
                for (int i = 0; i < stack.stackCount; i++)
                {
                    bolts.Add(stack.def);
                }
            }

            MapComponent_SelfDestructAftermath aftermaths = Aftermaths(map);
            float inner = Mathf.Clamp01(2f / throwRadius);
            BoltOrigin origin = BoltOrigin.Place(map, center, device.def);
            foreach (ThingDef bolt in bolts.InRandomOrder().Take(maxBolts))
            {
                ThingDef projectile = bolt.GetModExtension<BoltExtension>()?.projectile;
                if (projectile != null && BoltLauncher.TryLaunchInAnnulus(
                    origin, projectile, map, center, device.TrueCenter(), throwRadius, inner,
                    out float distance) && aftermaths != null)
                {
                    aftermaths.BoltsLaunched++;
                    aftermaths.MaxBoltDistance = Mathf.Max(aftermaths.MaxBoltDistance, distance);
                }
            }

            held.ClearAndDestroyContents();
        }

        /// <summary>Describes the cook-off.</summary>
        /// <param name="radius">The blast radius.</param>
        /// <returns>The phrase.</returns>
        public override string Describe(float radius) =>
            $"a blast, and up to {maxBolts} stored bolts cooking off";
    }

    /// <summary>
    /// The Artificer: an incendiary burst of molten batshittium, a sharper
    /// blast, and a few half-made bolts flung out of the jig.
    /// </summary>
    public class Detonation_MoltenSpray : BatshitDetonation
    {
        /// <summary>Chance to start a fire in each cell of the incendiary burst.</summary>
        public float chanceToStartFire = 0.7f;

        /// <summary>The sharper blast's damage type.</summary>
        public DamageDef blastDamageDef;

        /// <summary>The sharper blast's radius, in tiles.</summary>
        public float blastRadius = 2.9f;

        /// <summary>The sharper blast's damage; -1 uses the type's default.</summary>
        public int blastDamage = -1;

        /// <summary>The half-made bolts flung out.</summary>
        public ThingDef boltProjectile;

        /// <summary>How many half-made bolts fly.</summary>
        public IntRange bolts = new IntRange(3, 6);

        /// <summary>How far they fly, in tiles.</summary>
        public float throwRadius = 15f;

        /// <summary>The burst, the blast and the bolts.</summary>
        /// <param name="device">The destroyed device.</param>
        /// <param name="map">The map.</param>
        /// <param name="center">Where it stood.</param>
        /// <param name="radius">The incendiary burst's radius.</param>
        public override void Detonate(Thing device, Map map, IntVec3 center, float radius)
        {
            GenExplosion.DoExplosion(
                center, map, radius, DamageDefOf.Flame, device, chanceToStartFire: chanceToStartFire);
            if (blastDamageDef != null)
            {
                GenExplosion.DoExplosion(center, map, blastRadius, blastDamageDef, device, blastDamage);
            }

            if (boltProjectile == null)
            {
                return;
            }

            MapComponent_SelfDestructAftermath aftermaths = Aftermaths(map);
            int count = bolts.RandomInRange;
            BoltOrigin origin = BoltOrigin.Place(map, center, device.def);
            for (int i = 0; i < count; i++)
            {
                if (BoltLauncher.TryLaunchInAnnulus(
                    origin, boltProjectile, map, center, device.TrueCenter(), throwRadius,
                    Mathf.Clamp01(2f / throwRadius), out float distance) && aftermaths != null)
                {
                    aftermaths.BoltsLaunched++;
                    aftermaths.MaxBoltDistance = Mathf.Max(aftermaths.MaxBoltDistance, distance);
                }
            }
        }

        /// <summary>Describes the spray.</summary>
        /// <param name="radius">The burst radius.</param>
        /// <returns>The phrase.</returns>
        public override string Describe(float radius) =>
            "a spray of molten batshittium and half-made bolts";
    }

    /// <summary>Defs the detonation code refers to directly.</summary>
    [DefOf]
    public static class BT_DetonationDefOf
    {
        /// <summary>The invisible stand-in that launches a dead device's bolts.</summary>
        public static ThingDef BT_BoltOrigin;

        /// <summary>Binds the fields when RimWorld loads its defs.</summary>
        static BT_DetonationDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(BT_DetonationDefOf));
    }

    /// <summary>
    /// An invisible stand-in for a Batshit device that has just blown itself
    /// up: the launcher of the bolts it flings. Vanilla makes an animal flee
    /// from the launcher of a projectile that lands nearby
    /// (Pawn_MindState.Notify_ClamorImpact), and with no launcher that threw
    /// a NullReferenceException, once seen in the showcase when the town
    /// reactor's barrage fell on AnyRim's pens. It stands where the device
    /// stood, goes by the device's name in logs, and removes itself once
    /// every bolt has long since landed.
    /// </summary>
    public class BoltOrigin : Thing
    {
        /// <summary>Game ticks it lasts: a minute, far past the last bolt's flight.</summary>
        public const int LifetimeTicks = 3600;

        private int expiresAtTick;
        private ThingDef source;

        /// <summary>Spawns a stand-in for a device.</summary>
        /// <param name="map">The map.</param>
        /// <param name="cell">Where the device stood.</param>
        /// <param name="source">The device's def, for its name.</param>
        /// <returns>The stand-in, spawned.</returns>
        public static BoltOrigin Place(Map map, IntVec3 cell, ThingDef source)
        {
            var origin = (BoltOrigin)ThingMaker.MakeThing(BT_DetonationDefOf.BT_BoltOrigin);
            origin.source = source;
            origin.expiresAtTick = Find.TickManager.TicksGame + LifetimeTicks;
            return (BoltOrigin)GenSpawn.Spawn(origin, cell, map);
        }

        /// <summary>Named after the device it stands in for.</summary>
        public override string LabelNoCount => source?.label ?? base.LabelNoCount;

        /// <summary>
        /// Removes itself once its time is up. (No base call: Entity's
        /// TickRare throws NotImplementedException by design.)
        /// </summary>
        public override void TickRare()
        {
            if (Spawned && Find.TickManager.TicksGame >= expiresAtTick)
            {
                Destroy();
            }
        }

        /// <summary>Saves its expiry and the device it stands in for.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref expiresAtTick, "expiresAtTick");
            Scribe_Defs.Look(ref source, "source");
        }
    }
}
