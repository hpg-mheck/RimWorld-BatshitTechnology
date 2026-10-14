// Batshit Technology - the implosion.
// The singularity pull, extracted from the Ergonomic Singularity Grenade so
// that the Tractor Bomb and the Colony Bomb can reuse it. From its first
// tick it seizes every pawn in reach and holds them stunned; it rips
// fixtures loose as its reach grows (furniture uninstalled, walls torn down
// to their materials, plants uprooted, natural rock and indestructible
// things left alone); and it drags pawns and items toward its centre a cell
// at a time, ever faster, never shoving an item aside or losing one. Its
// host owns it, ticks it, saves it and ends it when it reports itself over.
//
// The drag itself belongs to the map (ImplosionField.cs): when several
// implosions run at once, every caught thing feels the sum of their pulls
// and moves once per step, not once per implosion.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// A thing no implosion rips loose or drags, though it could be
    /// destroyed: an armed bomb (the Tractor Bomb's), which must go off
    /// where it stands, not be torn down to nothing by its neighbour's
    /// pull.
    /// </summary>
    public interface IImplosionProof
    {
    }

    /// <summary>
    /// One implosion: a singularity pull around a fixed centre, owned and
    /// ticked by a host thing and saved with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A host (the grenade's <see cref="SingularityField"/>, the Tractor
    /// Bomb, later the Colony Bomb) makes one with
    /// <c>new Implosion(center, settings, host, instigator)</c>, calls
    /// <see cref="Tick"/> once per game tick for as long as it wants the
    /// pull to run, calls <see cref="Close"/> when it ends it, and saves it
    /// with <c>Scribe_Deep.Look(ref implosion, "implosion")</c>.
    /// </para>
    /// <para>
    /// Its settings say how long it runs: never less than their minimum,
    /// never more than their maximum, and in between until
    /// <see cref="AllGathered"/> or <see cref="Settled"/>. It reports that
    /// as <see cref="Over"/>, and the host ends it (the grenade's minimum
    /// and maximum are both five seconds). The drag keeps its end rate
    /// after its ramp (<see cref="PullTicks"/>). It draws nothing but the
    /// dust it sucks in: the vortex, the explosion and any aftermath are
    /// the host's.
    /// </para>
    /// <para>
    /// It seizes pawns and rips fixtures loose itself, but hands the drag
    /// to its map's <see cref="MapComponent_Implosions"/>, which superposes
    /// every implosion running there. Its own step clock, at its own rate,
    /// still sets when it tallies what is gathered and what can still move,
    /// so one implosion alone reports exactly as it always did.
    /// </para>
    /// </remarks>
    public sealed class Implosion : IExposable
    {
        /// <summary>Game ticks per second.</summary>
        public const int TicksPerSecond = 60;

        /// <summary>
        /// How long a dragged pawn stays stunned, in ticks.
        /// </summary>
        internal const int DragStunTicks = 45;

        /// <summary>
        /// How often it re-seizes the pawns in reach, in ticks.
        /// </summary>
        private const int GrabIntervalTicks = 15;

        /// <summary>
        /// How long a seized pawn stays stunned, in ticks: until after the
        /// next seizure, so nobody strolls out between them.
        /// </summary>
        private const int GrabStunTicks = GrabIntervalTicks + 15;

        /// <summary>
        /// How often a puff of dust is sucked in, in ticks.
        /// </summary>
        private const int DustIntervalTicks = 5;

        /// <summary>
        /// The dust's colour: a grey with batshittium in it.
        /// </summary>
        private static readonly Color DustColor = new Color(0.6f, 0.4f, 0.7f);

        private IntVec3 center;
        private ImplosionSettings settings = new ImplosionSettings();
        private Thing host;
        private Thing instigator;
        private int seed;
        private int elapsed;
        private double pendingSteps;
        private int nextRipCell;
        private List<Pawn> caught = new List<Pawn>();
        private int stepsTaken;
        private int uninstalled;
        private int tornDown;
        private int itemsMoved;
        private int lastStepAt;
        private int lastRestless = 1;
        private int lastOutstanding;
        private bool closed;
        private int lastTickedAt = -1;

        /// <summary>
        /// For loading a saved implosion only: Scribe_Deep needs a
        /// parameterless constructor, and fills in the rest.
        /// </summary>
        public Implosion()
        {
        }

        /// <summary>
        /// Opens an implosion. It does nothing until ticked.
        /// </summary>
        /// <param name="center">Its centre, on the host's map.</param>
        /// <param name="settings">
        /// Its settings. They are copied and normalized
        /// (<see cref="ImplosionSettings.Normalized"/>), so later changes to
        /// this instance have no effect.
        /// </param>
        /// <param name="host">
        /// The thing that owns it, which it never rips loose or drags; may
        /// be null.
        /// </param>
        /// <param name="instigator">
        /// Who set it off, blamed for the stuns; may be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="settings"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="center"/> is not a valid cell.
        /// </exception>
        public Implosion(
            IntVec3 center, ImplosionSettings settings, Thing host = null,
            Thing instigator = null)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!center.IsValid)
            {
                throw new ArgumentException(
                    "An implosion needs a valid centre.", nameof(center));
            }

            this.center = center;
            this.settings =
                settings.Normalized(GenRadial.MaxRadialPatternRadius);
            this.host = host;
            this.instigator = instigator;
            seed = Rand.Int;
        }

        /// <summary>Its centre.</summary>
        public IntVec3 Center => center;

        /// <summary>How far it reaches, in tiles.</summary>
        public float PullRadius => settings.pullRadius;

        /// <summary>
        /// How near the centre counts as gathered, in tiles.
        /// </summary>
        public float GatherRadius => settings.gatherRadius;

        /// <summary>Ticks its drag takes to reach full speed.</summary>
        public int PullTicks => settings.pullTicks;

        /// <summary>The thing that owns it; may be null.</summary>
        public Thing Host => host;

        /// <summary>
        /// Who set it off, blamed for the stuns; may be null.
        /// </summary>
        public Thing Instigator
        {
            get => instigator;
            set => instigator = value;
        }

        /// <summary>
        /// Its seed, drawn when it opens: what every caught thing's
        /// persistent bias toward it is hashed from
        /// (<see cref="PullSuperposition"/>'s Biases).
        /// </summary>
        public int Seed => seed;

        /// <summary>Ticks it has run.</summary>
        public int ElapsedTicks => elapsed;

        /// <summary>
        /// How hard it drags now, in cells per second: its start rate,
        /// accelerating to its end rate over its ramp.
        /// </summary>
        public double CurrentRate => SingularityPull.Rate(
            ImplosionMath.Progress(elapsed, settings.pullTicks),
            settings.pullRateStart, settings.pullRateEnd);

        /// <summary>
        /// The shortest it runs, in ticks, however soon everything is
        /// gathered.
        /// </summary>
        public int MinDurationTicks => settings.minDurationTicks;

        /// <summary>
        /// The longest it runs, in ticks, however things stand.
        /// </summary>
        public int MaxDurationTicks => settings.maxDurationTicks;

        /// <summary>
        /// Whether it is over and its host should end it: its maximum has
        /// passed, or its minimum has and everything is gathered or nothing
        /// more can move (<see cref="ImplosionMath.IsOver"/>). It goes on
        /// pulling if the host keeps ticking it.
        /// </summary>
        public bool Over => ImplosionMath.IsOver(
            elapsed, settings.minDurationTicks, settings.maxDurationTicks,
            AllGathered, Settled);

        /// <summary>
        /// Whether its host has ended it: it pulls no more, even if ticked.
        /// </summary>
        public bool Closed => closed;

        /// <summary>
        /// Whether everything in reach was gathered at its last tally:
        /// every pawn and item within the pull radius lay within the gather
        /// radius, and its reach to rip fixtures loose was already complete,
        /// so nothing more will be torn loose. False before the first
        /// tally. Things may still be settling closer to the centre, and in
        /// a tug-of-war with another implosion they may never all come.
        /// </summary>
        public bool AllGathered => SawEverything && lastOutstanding == 0;

        /// <summary>
        /// Whether nothing in reach could still move at its last tally,
        /// with its reach to rip fixtures loose already complete: every
        /// pawn and item was gathered at the centre or stuck at its last
        /// try (behind rock, or waiting for room in a full pile), and only
        /// a newcomer can change that. A thing at a balance point between
        /// two implosions, creeping however slowly, keeps it unsettled.
        /// False before the first tally.
        /// </summary>
        public bool Settled => SawEverything && lastRestless == 0;

        /// <summary>
        /// The pawns it has caught, for a host's aftermath.
        /// </summary>
        public IReadOnlyList<Pawn> Caught => caught;

        /// <summary>How many pawns it has caught.</summary>
        public int CaughtCount => caught.Count;

        /// <summary>
        /// Tallies taken so far, one per drag step on its own clock
        /// (diagnostics).
        /// </summary>
        public int StepsTaken => stepsTaken;

        /// <summary>Fixtures uninstalled so far (diagnostics).</summary>
        public int Uninstalled => uninstalled;

        /// <summary>Fixtures torn down so far (diagnostics).</summary>
        public int TornDown => tornDown;

        /// <summary>Item moves made so far (diagnostics).</summary>
        public int ItemsMoved => itemsMoved;

        /// <summary>
        /// The game tick it was last ticked, or -1: how its map's field
        /// tells a running implosion from one its host stopped ticking.
        /// </summary>
        internal int LastTickedAt => lastTickedAt;

        /// <summary>
        /// Its own step clock's carried fraction, which a newly caught
        /// thing joins so that it steps in time with the pull.
        /// </summary>
        internal double PendingSteps => pendingSteps;

        /// <summary>
        /// Whether a tally has run since the reach to rip fixtures loose
        /// completed, and so has seen everything there will be.
        /// </summary>
        private bool SawEverything =>
            stepsTaken > 0
            && (!settings.ripFixtures || lastStepAt >= settings.ripTicks);

        /// <summary>
        /// One tick: seize the pawns in reach, rip fixtures loose, have the
        /// map drag everything in by however many cells are due, tally, and
        /// suck in dust.
        /// </summary>
        /// <param name="map">
        /// The host's map. Nothing happens, and no time passes, without one,
        /// or once it is closed.
        /// </param>
        public void Tick(Map map)
        {
            if (closed || map == null || !center.InBounds(map))
            {
                return;
            }

            elapsed++;
            lastTickedAt = Find.TickManager.TicksGame;
            if (elapsed % GrabIntervalTicks == 1)
            {
                GrabPawns(map);
            }

            if (settings.ripFixtures)
            {
                RipLoose(map);
            }

            MapComponent_Implosions field = MapComponent_Implosions.For(map);
            field?.Pull(this);
            int due = BlastSchedule.DueThisTick(
                ref pendingSteps, CurrentRate, TicksPerSecond);
            if (due > 0)
            {
                Tally(map, field, due);
            }

            if (elapsed % DustIntervalTicks == 0)
            {
                Vector3 dust = center.ToVector3Shifted()
                    + (Rand.InsideUnitCircleVec3 * settings.pullRadius);
                FleckMaker.ThrowDustPuffThick(
                    dust, map, Rand.Range(1.2f, 2.4f), DustColor);
            }
        }

        /// <summary>
        /// Ends it for good: it pulls no more, and its map's field drops it
        /// before its next drag. Hosts call this when they explode, or
        /// leave the map.
        /// </summary>
        public void Close() => closed = true;

        /// <summary>Saves and loads its settings and progress.</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref settings.pullRadius, "pullRadius");
            Scribe_Values.Look(ref settings.gatherRadius, "gatherRadius");
            Scribe_Values.Look(ref settings.pullTicks, "pullTicks");
            Scribe_Values.Look(ref settings.pullRateStart, "pullRateStart");
            Scribe_Values.Look(ref settings.pullRateEnd, "pullRateEnd");
            Scribe_Values.Look(ref settings.ripFixtures, "ripFixtures");
            Scribe_Values.Look(ref settings.ripTicks, "ripTicks");
            // -1 marks a save from before the durations existed.
            Scribe_Values.Look(
                ref settings.minDurationTicks, "minDurationTicks", -1);
            Scribe_Values.Look(
                ref settings.maxDurationTicks, "maxDurationTicks", -1);
            Scribe_References.Look(ref host, "host");
            Scribe_References.Look(ref instigator, "instigator");
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref elapsed, "elapsed");
            Scribe_Values.Look(ref pendingSteps, "pendingSteps");
            Scribe_Values.Look(ref nextRipCell, "nextRipCell");
            Scribe_Collections.Look(ref caught, "caught", LookMode.Reference);
            Scribe_Values.Look(ref stepsTaken, "stepsTaken");
            Scribe_Values.Look(ref uninstalled, "uninstalled");
            Scribe_Values.Look(ref tornDown, "tornDown");
            Scribe_Values.Look(ref itemsMoved, "itemsMoved");
            Scribe_Values.Look(ref lastStepAt, "lastStepAt");
            // Restless until its next tally, if the save predates it.
            Scribe_Values.Look(ref lastRestless, "lastRestless", 1);
            Scribe_Values.Look(ref lastOutstanding, "lastOutstanding");
            Scribe_Values.Look(ref closed, "closed");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // A save from before the durations existed: the released
                // grenade ran exactly its ramp, so that is what it gets.
                // (An unreleased Tractor Bomb's cap was half again more.)
                if (settings.maxDurationTicks < 0)
                {
                    settings.maxDurationTicks = settings.pullTicks;
                    settings.minDurationTicks = settings.pullTicks;
                }

                // A save edited by hand, or from a later version, must not
                // stall or crash the pull.
                settings =
                    settings.Normalized(GenRadial.MaxRadialPatternRadius);
                caught ??= new List<Pawn>();
                caught.RemoveAll(p => p == null);
                lastTickedAt = -1;
            }
        }

        /// <summary>
        /// Records a pawn its map's drag moved, or tried to, as caught.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        internal void NoteCaught(Pawn pawn)
        {
            if (pawn != null && !caught.Contains(pawn))
            {
                caught.Add(pawn);
            }
        }

        /// <summary>
        /// Counts an item its map's drag moved under its pull
        /// (diagnostics).
        /// </summary>
        internal void NoteItemMoved() => itemsMoved++;

        /// <summary>
        /// Seizes every pawn in reach: caught (for a host's aftermath) and
        /// held stunned, so nobody strolls out while the drag is still slow.
        /// While any implosion's reach holds a pawn, its seizures keep it
        /// held.
        /// </summary>
        /// <param name="map">The map.</param>
        private void GrabPawns(Map map)
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn == host || pawn.Dead || !pawn.Position.InHorDistOf(
                    center, settings.pullRadius))
                {
                    continue;
                }

                NoteCaught(pawn);
                pawn.stances?.stunner?.StunFor(
                    GrabStunTicks, instigator, addBattleLog: false,
                    showMote: false);
            }
        }

        /// <summary>
        /// Rips loose the fixtures its reach has grown to cover: minifiable
        /// buildings are uninstalled, other buildings torn down to their
        /// materials, plants uprooted. Natural rock holds, and nothing that
        /// cannot be destroyed (a steam geyser) or is
        /// <see cref="IImplosionProof"/> (an armed bomb) is touched.
        /// </summary>
        /// <param name="map">The map.</param>
        private void RipLoose(Map map)
        {
            float reach = (float)ImplosionMath.RipReach(
                elapsed, settings.ripTicks, settings.pullRadius);
            int cellCount = GenRadial.NumCellsInRadius(settings.pullRadius);
            while (nextRipCell < cellCount
                && GenRadial.RadialPatternRadii[nextRipCell] <= reach)
            {
                IntVec3 c = center + GenRadial.RadialPattern[nextRipCell++];
                if (!c.InBounds(map))
                {
                    continue;
                }

                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (t == host || t.Destroyed || !t.Spawned
                        || !t.def.destroyable || t is IImplosionProof)
                    {
                        continue;
                    }

                    if (t is Plant)
                    {
                        t.Destroy();
                    }
                    else if (t.def.category == ThingCategory.Building
                        && t.def.building?.isNaturalRock != true)
                    {
                        RipBuilding(t, map);
                    }
                }
            }
        }

        /// <summary>
        /// Uninstalls a building, or tears it down if it can't be.
        /// </summary>
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
                    // Placed, not spawned: an item already lying there
                    // stays put.
                    GenPlace.TryPlaceThing(
                        minified, at, map, ThingPlaceMode.Near);
                    uninstalled++;
                }

                return;
            }

            building.Destroy(DestroyMode.Deconstruct); // leaves its materials
            tornDown++;
            FleckMaker.ThrowDustPuffThick(
                at.ToVector3Shifted(), map, 2f, Color.gray);
        }

        /// <summary>
        /// One of its own drag steps: tallies, after the map's drag, what in
        /// its reach is still outside the gather radius and what could
        /// still move. A thing could still move unless it sits on this
        /// centre, or its last try to step failed; see
        /// <see cref="MapComponent_Implosions.Restless"/>.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="field">Its map's field, or null.</param>
        /// <param name="due">How many steps fell due this tick.</param>
        private void Tally(Map map, MapComponent_Implosions field, int due)
        {
            stepsTaken += due;
            lastStepAt = elapsed;
            int restless = 0;
            int outstanding = 0;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(
                center, map, settings.pullRadius, true))
            {
                if (!MapComponent_Implosions.Pullable(t) || t == host)
                {
                    continue;
                }

                bool atCentre = t.Position == center;
                if (!atCentre && !Gathered(t.Position))
                {
                    outstanding++;
                }

                if (field == null
                    ? !atCentre
                    : field.Restless(t, atCentre))
                {
                    restless++;
                }
            }

            lastRestless = restless;
            lastOutstanding = outstanding;
        }

        /// <summary>Whether a cell lies within the gather radius.</summary>
        /// <param name="cell">The cell.</param>
        /// <returns>True if it does.</returns>
        private bool Gathered(IntVec3 cell) =>
            ImplosionMath.IsGathered(
                cell.x - center.x, cell.z - center.z, settings.gatherRadius);
    }
}
