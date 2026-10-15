// Batshit Technology - every implosion on a map, superposed.
// ROADMAP.md, "Multi-bomb physics": when several implosions run at once
// (two Tractor Bombs whose fields overlap, a grenade thrown into a bomb's
// pull), every caught pawn and item feels the sum of every pull that
// reaches it, a direction toward each centre scaled by that pull's current
// rate, and steps one cell along the net direction at the net rate. Each
// thing moves once per step, not once per implosion. Every pull
// calculation carries a little random bias (a persistent one per thing,
// with a fresh flicker per step from RimWorld's Rand), so nothing stalls
// cleanly between equal pulls. The maths is Core's PullSuperposition; one
// implosion alone drags exactly as it always did: things only approach its
// centre, at its rate, and are never shoved or lost. A pawn held between
// strong opposing pulls is stretched a little once a second (BT_Stretch,
// a stretch mark), the harder they strain the worse: spaghettification.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The implosions' defs.</summary>
    [DefOf]
    public static class BT_ImplosionDefOf
    {
        /// <summary>
        /// What opposing pulls do to a pawn held between them.
        /// </summary>
        public static DamageDef BT_Stretch;

        /// <summary>The injury stretching leaves.</summary>
        public static HediffDef BT_StretchMark;

        /// <summary>Lets RimWorld bind the fields above at load.</summary>
        static BT_ImplosionDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_ImplosionDefOf));
        }
    }

    /// <summary>
    /// The drag of every implosion on its map. Implosions hand it their
    /// pull each tick (<see cref="Pull"/>); the first to do so in a game
    /// tick drags every caught thing once, under all of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each caught thing keeps its own step clock, running at its net rate,
    /// so a thing between two pulls creeps while one beside a single pull
    /// races. A thing newly caught joins the clock of the strongest pull on
    /// it, so under one implosion every thing steps when the implosion's
    /// own clock does, as before. The clocks and each thing's last try are
    /// saved; which implosions are running is not (they say so again on
    /// their next tick).
    /// </para>
    /// <para>
    /// Items are moved as the implosion always moved them: into a cell with
    /// room for another item, or onto a matching stack there, never into a
    /// full cell (which makes RimWorld shove the cell's item aside, or
    /// destroy it). Otherwise they wait. Pawns share cells and go wherever
    /// they can stand, and are held stunned.
    /// </para>
    /// </remarks>
    public class MapComponent_Implosions : MapComponent
    {
        /// <summary>
        /// How often a pawn held between strong opposing pulls is
        /// stretched, in ticks: once a second.
        /// </summary>
        public const int StretchIntervalTicks = Implosion.TicksPerSecond;

        /// <summary>RimWorld's Rand, for the flicker of noise.</summary>
        private static readonly IUnitRandom Noise = new RandSource();

        private readonly List<Implosion> active = new List<Implosion>();
        private readonly List<Implosion> acting = new List<Implosion>();
        private readonly List<PullSource> pulls = new List<PullSource>();
        private readonly List<Thing> things = new List<Thing>();
        private readonly HashSet<Thing> seen = new HashSet<Thing>();
        private Dictionary<int, ThingPull> clocks =
            new Dictionary<int, ThingPull>();
        private PullJitter[] biases = new PullJitter[4];
        private int lastDragTick = -1;
        private List<int> savedIds;
        private List<double> savedPending;
        private List<int> savedTries;
        private List<bool> savedMoved;

        /// <summary>Created by RimWorld for every map.</summary>
        /// <param name="map">The map this component belongs to.</param>
        public MapComponent_Implosions(Map map)
            : base(map)
        {
        }

        /// <summary>
        /// The implosions its last drag superposed (diagnostics and the
        /// smoke test).
        /// </summary>
        public IReadOnlyList<Implosion> Active => active;

        /// <summary>
        /// The game tick of its last drag, or -1 (diagnostics).
        /// </summary>
        public int LastDragTick => lastDragTick;

        /// <summary>
        /// Things that have felt more than one pull at once this session
        /// (diagnostics).
        /// </summary>
        public static int Superposed { get; private set; }

        /// <summary>
        /// Pawns stretched between opposing pulls this session, a stretch
        /// at a time (diagnostics).
        /// </summary>
        public static int Stretches { get; private set; }

        /// <summary>A map's field.</summary>
        /// <param name="map">The map.</param>
        /// <returns>Its field, or null without a map.</returns>
        public static MapComponent_Implosions For(Map map) =>
            map?.GetComponent<MapComponent_Implosions>();

        /// <summary>
        /// Whether a thing is one an implosion drags: a spawned pawn or
        /// item, not proof against implosions.
        /// </summary>
        /// <param name="t">The thing.</param>
        /// <returns>True if it is.</returns>
        public static bool Pullable(Thing t) =>
            t != null && !t.Destroyed && t.Spawned
            && !(t is IImplosionProof)
            && (t is Pawn || t.def.category == ThingCategory.Item);

        /// <summary>
        /// An implosion's pull for this tick: it joins the drag, and if no
        /// implosion has dragged yet this game tick, everything caught by
        /// any of them is dragged now, once. Implosions whose hosts closed
        /// them, or stopped ticking them, drop out first.
        /// </summary>
        /// <param name="implosion">The implosion, ticking now.</param>
        public void Pull(Implosion implosion)
        {
            if (implosion == null || implosion.Closed)
            {
                return;
            }

            if (!active.Contains(implosion))
            {
                active.Add(implosion);
            }

            int now = Find.TickManager.TicksGame;
            if (lastDragTick == now)
            {
                return;
            }

            lastDragTick = now;
            // The caller ticked this tick; the rest are running if they
            // ticked on the last one (the order things tick in is not
            // fixed).
            active.RemoveAll(i => i == null || i.Closed
                || i.LastTickedAt < now - 1);
            if (active.Count == 0)
            {
                clocks.Clear();
                return;
            }

            Drag(now);
        }

        /// <summary>
        /// Whether a thing could still move, for an implosion's tally: it
        /// is pulled somewhere, and either has not yet tried a step or its
        /// last try moved it. A thing the field has not seen counts as
        /// restless unless it sits on the tallying implosion's centre.
        /// </summary>
        /// <param name="t">The thing.</param>
        /// <param name="atCentre">
        /// Whether it sits on the tallying implosion's centre.
        /// </param>
        /// <returns>True if it could still move.</returns>
        public bool Restless(Thing t, bool atCentre)
        {
            if (t == null || !clocks.TryGetValue(t.thingIDNumber, out var c))
            {
                return !atCentre;
            }

            return c.Rate > 0 && (c.LastTry < 0 || c.LastMoved);
        }

        /// <summary>
        /// Saves and loads each caught thing's step clock and last try.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                savedIds = clocks.Keys.ToList();
                savedPending = savedIds.Select(id => clocks[id].Pending)
                    .ToList();
                savedTries = savedIds.Select(id => clocks[id].LastTry)
                    .ToList();
                savedMoved = savedIds.Select(id => clocks[id].LastMoved)
                    .ToList();
            }

            Scribe_Collections.Look(ref savedIds, "ids", LookMode.Value);
            Scribe_Collections.Look(
                ref savedPending, "pending", LookMode.Value);
            Scribe_Collections.Look(ref savedTries, "tries", LookMode.Value);
            Scribe_Collections.Look(ref savedMoved, "moved", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                clocks = new Dictionary<int, ThingPull>();
                int n = new[]
                {
                    savedIds?.Count ?? 0, savedPending?.Count ?? 0,
                    savedTries?.Count ?? 0, savedMoved?.Count ?? 0,
                }.Min();
                for (int i = 0; i < n; i++)
                {
                    clocks[savedIds[i]] = new ThingPull
                    {
                        Pending = savedPending[i],
                        LastTry = savedTries[i],
                        LastMoved = savedMoved[i],
                    };
                }
            }

            if (Scribe.mode != LoadSaveMode.Saving)
            {
                savedIds = null;
                savedPending = null;
                savedTries = null;
                savedMoved = null;
            }
        }

        /// <summary>
        /// One game tick's drag: every pawn and item within reach of any
        /// running implosion, nearest a centre first (so the inner ones
        /// make room for the outer), steps once if its clock says so.
        /// Clocks of things no pull reaches any more are forgotten.
        /// </summary>
        /// <param name="now">The game tick.</param>
        private void Drag(int now)
        {
            things.Clear();
            seen.Clear();
            foreach (Implosion i in active)
            {
                foreach (Thing t in GenRadial.RadialDistinctThingsAround(
                    i.Center, map, i.PullRadius, true))
                {
                    if (Pullable(t) && !IsHost(t) && seen.Add(t))
                    {
                        things.Add(t);
                    }
                }
            }

            // One implosion's reach already comes nearest first.
            IEnumerable<Thing> order = active.Count == 1
                ? things
                : things.OrderBy(NearestCentre).ToList();
            foreach (Thing t in order)
            {
                if (!t.Destroyed && t.Spawned)
                {
                    DragOne(t, now);
                }
            }

            foreach (int id in clocks.Where(kv => kv.Value.Seen != now)
                .Select(kv => kv.Key).ToList())
            {
                clocks.Remove(id);
            }
        }

        /// <summary>
        /// Drags one thing: sums the pulls on it, advances its clock at
        /// the net rate, and if a step is due, steps it along the best
        /// allowed direction there is room for.
        /// </summary>
        /// <param name="t">The thing.</param>
        /// <param name="now">The game tick.</param>
        private void DragOne(Thing t, int now)
        {
            IntVec3 at = t.Position;
            acting.Clear();
            pulls.Clear();
            Implosion strongest = null;
            foreach (Implosion i in active)
            {
                if (!at.InHorDistOf(i.Center, i.PullRadius))
                {
                    continue;
                }

                acting.Add(i);
                pulls.Add(Source(i));
                if (strongest == null
                    || i.CurrentRate > strongest.CurrentRate)
                {
                    strongest = i;
                }
            }

            if (strongest == null)
            {
                return;
            }

            if (biases.Length < pulls.Count)
            {
                biases = new PullJitter[pulls.Count * 2];
            }

            PullSuperposition.Biases(t.thingIDNumber, pulls, biases);
            if (!clocks.TryGetValue(t.thingIDNumber, out ThingPull clock))
            {
                clock = new ThingPull { Pending = strongest.PendingSteps };
                clocks[t.thingIDNumber] = clock;
            }

            if (acting.Count > 1 && !clock.Superposed)
            {
                clock.Superposed = true;
                Superposed++;
            }

            clock.Seen = now;
            if (t is Pawn held && acting.Count > 1)
            {
                Stretch(held, clock, strongest, now);
                if (!held.Spawned)
                {
                    return; // stretched to death
                }
            }

            clock.Rate = PullSuperposition.NetRate(at.x, at.z, pulls, biases);
            int due = BlastSchedule.DueThisTick(
                ref clock.Pending, clock.Rate, Implosion.TicksPerSecond);
            if (due == 0)
            {
                return;
            }

            // A fresh flicker of noise on every pull calculation.
            var jitter = new PullJitter[pulls.Count];
            for (int k = 0; k < jitter.Length; k++)
            {
                jitter[k] = biases[k].Plus(PullSuperposition.Flicker(Noise));
            }

            IReadOnlyList<(int Dx, int Dz)> steps =
                PullSuperposition.Steps(at.x, at.z, pulls, jitter);
            bool moved;
            if (t is Pawn pawn)
            {
                moved = MovePawn(pawn, steps);
                foreach (Implosion i in acting)
                {
                    i.NoteCaught(pawn);
                }

                pawn.stances?.stunner?.StunFor(
                    Implosion.DragStunTicks, strongest.Instigator,
                    addBattleLog: false, showMote: false);
            }
            else
            {
                moved = MoveItem(t, steps, acting.Count == 1 ? strongest : null);
                if (moved)
                {
                    foreach (Implosion i in acting)
                    {
                        i.NoteItemMoved();
                    }
                }
            }

            clock.LastTry = now;
            clock.LastMoved = moved;
        }

        /// <summary>
        /// Stretches a pawn held between opposing pulls, at most once a
        /// second, by however much they strain against each other
        /// (<see cref="PullSuperposition.StretchDamage"/>): nothing under
        /// pulls that mostly agree. Armour does not help.
        /// </summary>
        /// <param name="pawn">The pawn, under more than one pull.</param>
        /// <param name="clock">Its clock, which times its stretches.</param>
        /// <param name="strongest">The strongest pull on it, blamed.</param>
        /// <param name="now">The game tick.</param>
        private void Stretch(
            Pawn pawn, ThingPull clock, Implosion strongest, int now)
        {
            if (now < clock.NextStretch)
            {
                return;
            }

            IntVec3 at = pawn.Position;
            int damage = PullSuperposition.StretchDamage(
                PullSuperposition.Tension(at.x, at.z, pulls));
            if (damage <= 0)
            {
                return;
            }

            clock.NextStretch = now + StretchIntervalTicks;
            Stretches++;
            pawn.TakeDamage(new DamageInfo(
                BT_ImplosionDefOf.BT_Stretch, damage,
                instigator: strongest.Instigator));
        }

        /// <summary>
        /// Steps a pawn into the first allowed cell it can stand in.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <param name="steps">The allowed steps, best first.</param>
        /// <returns>True if it moved.</returns>
        private bool MovePawn(
            Pawn pawn, IReadOnlyList<(int Dx, int Dz)> steps)
        {
            IntVec3 from = pawn.Position;
            foreach (var (dx, dz) in steps)
            {
                var cell = new IntVec3(from.x + dx, 0, from.z + dz);
                if (cell.InBounds(map) && cell.Walkable(map))
                {
                    pawn.Position = cell;
                    pawn.Notify_Teleported(
                        endCurrentJob: true, resetTweenedPos: false);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Steps an item one allowed cell: into a cell with room for another
        /// item, or onto a matching stack there, which absorbs it. If every
        /// step is blocked it may tumble into the pile (<see cref="Tumble"/>).
        /// Otherwise it waits. (Spawning an item into a full cell makes
        /// RimWorld shove the cell's item aside, or destroy it if there's no
        /// room: that churned items back and forth in a line beside the
        /// singularity.)
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="steps">The allowed steps, best first.</param>
        /// <param name="single">
        /// The one implosion pulling the item, or null under superposed
        /// pulls, where it never tumbles.
        /// </param>
        /// <returns>True if it moved or merged, even in part.</returns>
        private bool MoveItem(
            Thing item, IReadOnlyList<(int Dx, int Dz)> steps, Implosion single)
        {
            IntVec3 from = item.Position;
            foreach (var (dx, dz) in steps)
            {
                var next = new IntVec3(from.x + dx, 0, from.z + dz);
                if (!next.InBounds(map) || !next.Walkable(map))
                {
                    continue;
                }

                Thing stack = next.GetThingList(map).FirstOrDefault(
                    o => o != item && o.def.category == ThingCategory.Item
                        && o.CanStackWith(item));
                if (stack != null && stack.stackCount < stack.def.stackLimit)
                {
                    // Absorbing a spawned item destroys it once it's all
                    // merged; a partial merge leaves the rest where it was.
                    stack.TryAbsorbStack(item, respectStackLimit: true);
                    return true;
                }

                if (next.GetItemCount(map)
                    < next.GetMaxItemsAllowedInCell(map))
                {
                    item.DeSpawn();
                    GenSpawn.Spawn(item, next, map);
                    return true;
                }
            }

            return Tumble(item, single);
        }

        /// <summary>
        /// How far beyond the gather radius a blocked item may be and still
        /// tumble into the pile, in tiles: about one cell.
        /// </summary>
        private const float TumbleReach = 1.5f;

        /// <summary>
        /// Lets an item whose every step is blocked, beside a pile that
        /// still has room, tumble over the pile into the free cell within
        /// the gather radius nearest to it. Only under a single pull, and
        /// only from within <see cref="TumbleReach"/> of the gather radius.
        /// A step goes only strictly nearer the centre, so a stack behind a
        /// full side of the pile waited for good beside free cells on the
        /// far side: the smoke flake of slices 13000 to 19000b, a silver
        /// stack stuck at (0, 2) in about half of all full runs (the pile's
        /// fill order turns on the pull's random flicker).
        /// </summary>
        /// <param name="item">The blocked item.</param>
        /// <param name="single">The one implosion pulling it, or null.</param>
        /// <returns>True if it tumbled.</returns>
        private bool Tumble(Thing item, Implosion single)
        {
            if (single == null)
            {
                return false;
            }

            IntVec3 centre = single.Center;
            IntVec3 from = item.Position;
            float gather = single.GatherRadius;
            float here = from.DistanceTo(centre);
            if (here <= gather || here > gather + TumbleReach)
            {
                return false;
            }

            IntVec3 best = IntVec3.Invalid;
            float bestDistance = float.MaxValue;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(centre, gather, true))
            {
                if (!c.InBounds(map) || !c.Walkable(map)
                    || c.DistanceTo(centre) >= here
                    || c.GetItemCount(map) >= c.GetMaxItemsAllowedInCell(map))
                {
                    continue;
                }

                float d = c.DistanceTo(from);
                if (d < bestDistance)
                {
                    best = c;
                    bestDistance = d;
                }
            }

            if (!best.IsValid)
            {
                return false;
            }

            item.DeSpawn();
            GenSpawn.Spawn(item, best, map);
            return true;
        }

        /// <summary>Whether a thing hosts a running implosion.</summary>
        /// <param name="t">The thing.</param>
        /// <returns>True if it does.</returns>
        private bool IsHost(Thing t)
        {
            foreach (Implosion i in active)
            {
                if (i.Host == t)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A thing's squared distance from the nearest running implosion's
        /// centre: the order things are dragged in.
        /// </summary>
        /// <param name="t">The thing.</param>
        /// <returns>The squared distance.</returns>
        private int NearestCentre(Thing t)
        {
            int best = int.MaxValue;
            foreach (Implosion i in active)
            {
                best = System.Math.Min(
                    best, (t.Position - i.Center).LengthHorizontalSquared);
            }

            return best;
        }

        /// <summary>An implosion as superposition sees it.</summary>
        /// <param name="i">The implosion.</param>
        /// <returns>Its centre, current rate and seed.</returns>
        private static PullSource Source(Implosion i) =>
            new PullSource(i.Center.x, i.Center.z, i.CurrentRate, i.Seed);

        /// <summary>One caught thing's step clock and last try.</summary>
        private sealed class ThingPull
        {
            /// <summary>Steps carried between ticks, a fraction.</summary>
            public double Pending;

            /// <summary>
            /// Its net rate at the last drag, in cells a second.
            /// </summary>
            public double Rate;

            /// <summary>The tick it last tried a step, or -1.</summary>
            public int LastTry = -1;

            /// <summary>Whether that try moved it.</summary>
            public bool LastMoved;

            /// <summary>The tick a drag last reached it.</summary>
            public int Seen = -1;

            /// <summary>Whether it has felt two pulls at once.</summary>
            public bool Superposed;

            /// <summary>
            /// The first tick it may be stretched again, if it is a pawn.
            /// </summary>
            public int NextStretch;
        }

        /// <summary>RimWorld's Rand as a source of uniform numbers.</summary>
        private sealed class RandSource : IUnitRandom
        {
            /// <summary>The next number, in [0, 1].</summary>
            /// <returns>Rand.Value.</returns>
            public double NextUnit() => Rand.Value;
        }
    }
}
