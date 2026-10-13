// Batshit Technology - Ergonomic Singularity Grenade.
// A thrown weapon (a vanilla-style grenade, made at the Artificer). Its
// projectile opens a singularity where it lands. For five seconds the
// singularity rips fixtures loose (furniture uninstalled, walls torn down to
// their materials, plants uprooted) and drags pawns and items toward its
// centre, ever faster; then it explodes. Every pawn it caught who survives
// comes away with a huge mood boost and a quip about how ergonomic it was.

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

        /// <summary>Reports mistakes in the settings.</summary>
        /// <returns>One message per problem.</returns>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (radius <= 0f || radius > GenRadial.MaxRadialPatternRadius)
            {
                yield return "singularity radius must be positive and within the engine maximum";
            }

            if (pullTicks < 1 || ripTicks < 1 || pullRateStart < 0f || pullRateEnd < pullRateStart)
            {
                yield return "singularity timings must be positive and its pull must not slow down";
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
    /// The singularity: rips fixtures loose, drags pawns and items in ever
    /// faster, explodes, and leaves survivors feeling wonderful.
    /// </summary>
    public class SingularityField : ThingWithComps
    {
        private const int TicksPerSecond = 60;

        /// <summary>How often it re-seizes the pawns in reach, in ticks.</summary>
        private const int GrabIntervalTicks = 15;

        private Thing instigator;
        private int ticks;
        private double pendingSteps;
        private int nextRipCell;
        private bool exploded;
        private List<Pawn> caught = new List<Pawn>();

        /// <summary>Who threw it, for the explosion's blame. May be null.</summary>
        public Thing Instigator
        {
            get => instigator;
            set => instigator = value;
        }

        /// <summary>Its settings.</summary>
        public SingularityExtension Settings =>
            def.GetModExtension<SingularityExtension>() ?? new SingularityExtension();

        /// <summary>Drag steps taken so far (diagnostics).</summary>
        public int StepsTaken { get; private set; }

        /// <summary>Fixtures uninstalled so far (diagnostics).</summary>
        public int Uninstalled { get; private set; }

        /// <summary>Fixtures torn down so far (diagnostics).</summary>
        public int TornDown { get; private set; }

        /// <summary>Item moves made so far (diagnostics).</summary>
        public int ItemsMoved { get; private set; }

        /// <summary>Pawns caught so far (diagnostics).</summary>
        public int CaughtCount => caught.Count;

        /// <summary>Whether it has exploded yet.</summary>
        public bool Exploded => exploded;

        /// <summary>Survivors given the memory this session, across all singularities.</summary>
        public static int SurvivorsComforted { get; private set; }

        /// <summary>A shudder as it opens.</summary>
        /// <param name="map">The map.</param>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(Position, map));
                if (map == Find.CurrentMap)
                {
                    Find.CameraDriver.shaker.DoShake(0.6f);
                }
            }
        }

        /// <summary>Saves and loads its progress.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref instigator, "instigator");
            Scribe_Values.Look(ref ticks, "ticks");
            Scribe_Values.Look(ref pendingSteps, "pendingSteps");
            Scribe_Values.Look(ref nextRipCell, "nextRipCell");
            Scribe_Values.Look(ref exploded, "exploded");
            Scribe_Collections.Look(ref caught, "caught", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                caught ??= new List<Pawn>();
                caught.RemoveAll(p => p == null);
            }
        }

        /// <summary>One tick: rip, drag, and at the end explode and comfort.</summary>
        protected override void Tick()
        {
            base.Tick();
            if (!Spawned)
            {
                return;
            }

            ticks++;
            SingularityExtension s = Settings;
            if (!exploded)
            {
                if (ticks % GrabIntervalTicks == 1)
                {
                    GrabPawns(s);
                }

                RipLoose(s);
                double rate = SingularityPull.Rate(
                    ticks / (double)s.pullTicks, s.pullRateStart, s.pullRateEnd);
                int due = BlastSchedule.DueThisTick(ref pendingSteps, rate, TicksPerSecond);
                for (int i = 0; i < due; i++)
                {
                    PullStep(s);
                }

                if (ticks % 5 == 0)
                {
                    Vector3 dust = Position.ToVector3Shifted()
                        + (Rand.InsideUnitCircleVec3 * s.radius);
                    FleckMaker.ThrowDustPuffThick(dust, Map, Rand.Range(1.2f, 2.4f), new Color(0.6f, 0.4f, 0.7f));
                }

                if (ticks >= s.pullTicks)
                {
                    Explode(s);
                }
            }
            else if (ticks >= s.pullTicks + s.survivorDelayTicks)
            {
                ComfortSurvivors(s);
                Destroy();
            }
        }

        /// <summary>
        /// Rips loose the fixtures its reach has grown to cover: minifiable
        /// buildings are uninstalled, other buildings torn down to their
        /// materials, plants uprooted. Natural rock holds.
        /// </summary>
        /// <param name="s">The settings.</param>
        private void RipLoose(SingularityExtension s)
        {
            float reach = s.radius * Mathf.Min(1f, ticks / (float)s.ripTicks);
            int cellCount = GenRadial.NumCellsInRadius(s.radius);
            Map map = Map;
            while (nextRipCell < cellCount && GenRadial.RadialPatternRadii[nextRipCell] <= reach)
            {
                IntVec3 c = Position + GenRadial.RadialPattern[nextRipCell++];
                if (!c.InBounds(map))
                {
                    continue;
                }

                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (t == this || t.Destroyed || !t.Spawned)
                    {
                        continue;
                    }

                    if (t is Plant)
                    {
                        t.Destroy();
                    }
                    else if (t.def.category == ThingCategory.Building
                        && t.def.building?.isNaturalRock != true && t.def.destroyable)
                    {
                        RipBuilding(t, map);
                    }
                }
            }
        }

        /// <summary>Uninstalls a building, or tears it down if it can't be.</summary>
        /// <param name="building">The building.</param>
        /// <param name="map">The map.</param>
        private void RipBuilding(Thing building, Map map)
        {
            IntVec3 at = building.Position;
            if (building.def.Minifiable)
            {
                MinifiedThing minified = building.MakeMinified();
                if (minified != null)
                {
                    // Placed, not spawned: an item already lying there stays put.
                    GenPlace.TryPlaceThing(minified, at, map, ThingPlaceMode.Near);
                    Uninstalled++;
                }

                return;
            }

            building.Destroy(DestroyMode.Deconstruct); // leaves its materials
            TornDown++;
            FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted(), map, 2f, Color.gray);
        }

        /// <summary>
        /// Seizes every pawn in reach from the moment it opens: they are
        /// caught (for the survivors' memory) and held stunned, so nobody
        /// strolls out while the drag is still slow.
        /// </summary>
        /// <param name="s">The settings.</param>
        private void GrabPawns(SingularityExtension s)
        {
            foreach (Pawn pawn in Map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.Dead || !pawn.Position.InHorDistOf(Position, s.radius))
                {
                    continue;
                }

                if (!caught.Contains(pawn))
                {
                    caught.Add(pawn);
                }

                pawn.stances?.stunner?.StunFor(
                    GrabIntervalTicks + 15, instigator, addBattleLog: false, showMote: false);
            }
        }

        /// <summary>
        /// Drags every pawn and item within reach one cell closer, if the
        /// way is clear. Pawns are stunned so they can't walk back out.
        /// </summary>
        /// <param name="s">The settings.</param>
        private void PullStep(SingularityExtension s)
        {
            Map map = Map;
            IntVec3 center = Position;
            StepsTaken++;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(center, map, s.radius, true).ToList())
            {
                if (t == this || t.Destroyed || !t.Spawned || t.Position == center)
                {
                    continue;
                }

                if (t is Pawn pawn)
                {
                    if (!caught.Contains(pawn))
                    {
                        caught.Add(pawn);
                    }

                    if (TryNextCell(pawn.Position, center, map, out IntVec3 next))
                    {
                        pawn.Position = next;
                        pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: false);
                    }

                    pawn.stances?.stunner?.StunFor(45, instigator, addBattleLog: false, showMote: false);
                }
                else if (t.def.category == ThingCategory.Item)
                {
                    PullItem(t, center, map);
                }
            }
        }

        /// <summary>
        /// Drags an item one cell closer: into a cell with room for another
        /// item, or onto a matching stack there, which absorbs it. Otherwise
        /// it waits. (Spawning an item into a full cell makes RimWorld shove
        /// the cell's item aside, or destroy it if there's no room: that
        /// churned items back and forth in a line beside the singularity.)
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="center">The singularity's centre.</param>
        /// <param name="map">The map.</param>
        private void PullItem(Thing item, IntVec3 center, Map map)
        {
            foreach (var (dx, dz) in SingularityPull.StepsToward(
                item.Position.x, item.Position.z, center.x, center.z))
            {
                var next = new IntVec3(item.Position.x + dx, 0, item.Position.z + dz);
                if (!next.InBounds(map) || !next.Walkable(map))
                {
                    continue;
                }

                Thing stack = next.GetThingList(map).FirstOrDefault(
                    o => o != item && o.def.category == ThingCategory.Item && o.CanStackWith(item));
                if (stack != null && stack.stackCount < stack.def.stackLimit)
                {
                    // Absorbing a spawned item destroys it once it's all
                    // merged; a partial merge leaves the rest where it was.
                    stack.TryAbsorbStack(item, respectStackLimit: true);
                    ItemsMoved++;
                    return;
                }

                if (next.GetItemCount(map) < next.GetMaxItemsAllowedInCell(map))
                {
                    item.DeSpawn();
                    GenSpawn.Spawn(item, next, map);
                    ItemsMoved++;
                    return;
                }
            }
        }

        /// <summary>The next walkable cell toward the centre, trying the fallbacks.</summary>
        /// <param name="from">Where the thing is.</param>
        /// <param name="center">The singularity's centre.</param>
        /// <param name="map">The map.</param>
        /// <param name="next">The cell to move to.</param>
        /// <returns>False if every step is blocked.</returns>
        private static bool TryNextCell(IntVec3 from, IntVec3 center, Map map, out IntVec3 next)
        {
            foreach (var (dx, dz) in SingularityPull.StepsToward(from.x, from.z, center.x, center.z))
            {
                next = new IntVec3(from.x + dx, 0, from.z + dz);
                if (next.InBounds(map) && next.Walkable(map))
                {
                    return true;
                }
            }

            next = from;
            return false;
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
            foreach (Pawn pawn in caught)
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
            if (exploded || def.graphicData == null)
            {
                return;
            }

            SingularityExtension s = Settings;
            float seconds = ticks / (float)TicksPerSecond;
            float total = s.pullTicks / (float)TicksPerSecond;
            float progress = Mathf.Clamp01(seconds / total);
            // Turns: 0.3 per second at the start, accelerating to 3.
            float turns = (0.3f * seconds) + (2.7f * seconds * seconds / (2f * total));
            float pulse = (float)SingularityPull.FinalPulse(total - seconds);
            float size = s.radius * 2f * (0.35f + (0.65f * progress)) * pulse;

            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            float outer = size * 1.25f;
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(pos - new Vector3(0f, 0.002f, 0f),
                    Quaternion.AngleAxis(turns * 0.6f * 360f, Vector3.up), new Vector3(outer, 1f, outer)),
                BT_SingularityAssets.OuterVortex, 0);
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(pos, Quaternion.AngleAxis(-turns * 360f, Vector3.up), new Vector3(size, 1f, size)),
                Graphic.MatSingle, 0);
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
