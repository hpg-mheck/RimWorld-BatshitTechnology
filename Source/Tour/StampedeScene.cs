// Batshit Technology - the directed tour's scene 3, "Uninvited Guests".
// A herd of manhunting boomalopes enters from the east edge and makes for
// the colonists; the Ballista meets it with one 256-bolt volley, and at
// most two more for stragglers (Core's Stampede holds the rules, which
// the tour verifier also reads). Rain falls for the scene, so the
// boomalopes' fires do not take the east; what still burns is put out
// during the aftermath, and the weather is given back at the end. The
// colonists wait drafted inside the base. Whatever the ending, the
// Ballista holds its fire again and no live boomalope outlasts the scene.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BatshitTechnology
{
    /// <summary>
    /// Scene 3, "Uninvited Guests": the boomalope stampede and the
    /// Ballista's 256-bolt volley. Beats: <c>herd</c> as the herd comes
    /// on, <c>volley</c> as the first volley leaves, <c>stragglers</c> if a
    /// cleanup volley follows, and <c>aftermath</c> once it is over.
    /// </summary>
    public class StampedeScene : TourScene
    {
        /// <summary>The herd's size; Core holds it to its bounds.</summary>
        public int herdSize = Stampede.TargetHerd;

        /// <summary>
        /// Boomalopes spawned a tick while the herd enters.
        /// </summary>
        public int spawnPerTick = 25;

        /// <summary>The earliest tick the first volley is ordered.</summary>
        public int volleyTick = 360;

        /// <summary>Bolts in each cleanup volley.</summary>
        public int cleanupBolts = 128;

        /// <summary>
        /// Ticks after the last bolt lands before judging a volley.
        /// </summary>
        public int chainSettleTicks = 120;

        /// <summary>How long the aftermath holds, in ticks.</summary>
        public int holdTicks = 420;

        /// <summary>
        /// The least time from a volley to the next line, in ticks: the
        /// volley's line (115 characters) reads for 8.25 s on the narration
        /// overlay, and the stragglers' line (after the first volley) or
        /// the aftermath's (after the last) would replace it.
        /// </summary>
        public int lineHoldTicks = 510;

        /// <summary>
        /// How long after <see cref="volleyTick"/> the scene waits for the
        /// first volley before it gives up and sends the herd home.
        /// </summary>
        public int giveUpTicks = 1800;

        /// <summary>
        /// How long an order may go unfired (a grid that cannot power it)
        /// before the scene aims again.
        /// </summary>
        private const int OrderPatienceTicks = 240;

        /// <summary>
        /// Ticks over which the herd's velocity is measured.
        /// </summary>
        private const int VelocityWindow = 60;

        /// <summary>
        /// How often the scene checks that it is still raining.
        /// </summary>
        private const int RainCheckTicks = 250;

        /// <summary>
        /// How often the scene logs why the first volley is still waiting.
        /// </summary>
        private const int WaitNoteTicks = 600;

        /// <summary>The weather's full transition, in ticks.</summary>
        private const int WeatherTransitionTicks = 4000;

        /// <summary>The boomalopes, in the order they entered.</summary>
        [Unsaved]
        private List<Pawn> herd;

        /// <summary>Boomalopes still to enter.</summary>
        [Unsaved]
        private int toSpawn;

        /// <summary>The cells the herd enters on, in spawn order.</summary>
        [Unsaved]
        private List<IntVec3> entry;

        /// <summary>
        /// The herd's centres over the last ticks, for its velocity.
        /// </summary>
        [Unsaved]
        private Queue<(int Tick, Spot Centre)> track;

        /// <summary>The colonists this scene drafted.</summary>
        [Unsaved]
        private List<Pawn> drafted;

        /// <summary>
        /// The weather before the rain, given back at the end.
        /// </summary>
        [Unsaved]
        private WeatherDef previousWeather;

        /// <summary>
        /// The Ballista's projectile count before the scene.
        /// </summary>
        [Unsaved]
        private int previousCount;

        /// <summary>
        /// The Ballista's bolts fired so far, seen last tick.
        /// </summary>
        [Unsaved]
        private int boltsSeen;

        /// <summary>
        /// Volleys fired in this run, and the bolts in each.
        /// </summary>
        [Unsaved]
        private List<int> volleys;

        /// <summary>The tick the last volley left, or -1.</summary>
        [Unsaved]
        private int lastVolleyAt;

        /// <summary>
        /// Whether a volley has been ordered and not yet seen.
        /// </summary>
        [Unsaved]
        private bool volleyOrdered;

        /// <summary>The tick the last volley was ordered.</summary>
        [Unsaved]
        private int orderedAt;

        /// <summary>
        /// The last tick one of the mod's bolts was in the air.
        /// </summary>
        [Unsaved]
        private int lastInFlightAt;

        /// <summary>Where the first volley was aimed.</summary>
        [Unsaved]
        private IntVec3 impact = IntVec3.Invalid;

        /// <summary>Where the camera looks now.</summary>
        [Unsaved]
        private IntVec3 look = IntVec3.Invalid;

        /// <summary>The tick the aftermath began, or -1.</summary>
        [Unsaved]
        private int aftermathAt = -1;

        /// <summary>The tick the first volley left, or -1.</summary>
        [Unsaved]
        private int firstVolleyAt = -1;

        /// <summary>
        /// The tick the stragglers' line is due, or -1 when none waits: it
        /// is held until the volley's line has had its reading time.
        /// </summary>
        [Unsaved]
        private int stragglersDueAt = -1;

        /// <summary>
        /// Whether a cleanup volley was refused because its footprint
        /// reached the base or a colonist; the scene then goes to its
        /// aftermath instead of firing again.
        /// </summary>
        [Unsaved]
        private bool cleanupRefused;

        /// <summary>
        /// Volleys refused in this run because their footprint reached the
        /// base's square or a colonist.
        /// </summary>
        [Unsaved]
        private int refusedForSafety;

        /// <summary>How an order to fire went.</summary>
        private enum OrderResult
        {
            /// <summary>The Ballista was ordered to fire.</summary>
            Ordered,

            /// <summary>The aim was inside the Ballista's least range.</summary>
            TooNear,

            /// <summary>
            /// The volley's footprint would have reached the base's square
            /// or a colonist.
            /// </summary>
            Unsafe,
        }

        /// <summary>
        /// Gets how many volleys this run refused for the colonists'
        /// safety, for the tour verifier's note.
        /// </summary>
        internal int VolleysRefused => refusedForSafety;

        /// <summary>
        /// Gets the herd as it entered (dead and alive), for the tour
        /// verifier; empty before the herd enters.
        /// </summary>
        internal IReadOnlyList<Pawn> Herd => herd ?? new List<Pawn>();

        /// <summary>Gets the herd size this run sends.</summary>
        internal int HerdPlanned => Stampede.HerdSize(herdSize);

        /// <summary>
        /// Gets the bolts in each volley fired in this run.
        /// </summary>
        internal IReadOnlyList<int> Volleys => volleys ?? new List<int>();

        /// <summary>
        /// Gets the Ballista: the subject the narration points at.
        /// </summary>
        public override Thing Subject =>
            Alive(Stage.Ballista) ? Stage.Ballista : null;

        /// <summary>
        /// Gets where the camera looks: the herd, the volley, or after.
        /// </summary>
        public override IntVec3 FocusCell =>
            look.IsValid ? look : base.FocusCell;

        /// <summary>
        /// Gets why the scene cannot run: no Ballista, too few explosive
        /// bolts for the volley, no boomalopes in this game, or no open
        /// ground on the east edge that reaches the base.
        /// </summary>
        protected override string WhyUnavailable
        {
            get
            {
                Building_BatshittiumBallista ballista = Stage.Ballista;
                if (!Alive(ballista) || !Stage.BaseCentre.IsValid)
                {
                    return "the base has no Ballista";
                }

                if (ballista.BoltsAvailable(
                        BT_BallistaDefOf.BT_BoltReallyPainful, unpoweredToo: true)
                    < Stampede.MoneyShotBolts)
                {
                    return "the Ballista has too few explosive bolts";
                }

                if (Boomalope == null)
                {
                    return "this game has no boomalopes";
                }

                return EntryCells(Map, Stage.BaseCentre, HerdPlanned,
                        out string note).Count > 0
                    ? null
                    : "no open ground on the east edge reaches the base "
                        + $"({note})";
            }
        }

        /// <summary>The boomalope's pawn kind, or null.</summary>
        private static PawnKindDef Boomalope =>
            DefDatabase<PawnKindDef>.GetNamedSilentFail("Boomalope");

        /// <summary>
        /// The open cells on the east edge the herd can enter on: the first
        /// of Core's bands with enough standable cells that reach the base,
        /// in a shuffled order. Empty when no band has enough.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="baseCentre">The demo base's centre.</param>
        /// <param name="herd">The herd's size.</param>
        /// <returns>The cells.</returns>
        internal static List<IntVec3> EntryCells(
            Map map, IntVec3 baseCentre, int herd) =>
            EntryCells(map, baseCentre, herd, out _);

        /// <summary>
        /// The open cells on the east edge the herd can enter on, with a
        /// note of the best band's counts when none has enough.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="baseCentre">The demo base's centre.</param>
        /// <param name="herd">The herd's size.</param>
        /// <param name="note">
        /// The band used, or the best band's standable cells and those of
        /// them that reach the base, against the cells needed.
        /// </param>
        /// <returns>The cells; empty when no band has enough.</returns>
        internal static List<IntVec3> EntryCells(
            Map map, IntVec3 baseCentre, int herd, out string note)
        {
            var cells = new List<IntVec3>();
            note = "no map";
            if (map == null || !baseCentre.IsValid)
            {
                return cells;
            }

            int bestStandable = 0;
            int bestReaching = -1;
            Box best = default;

            var bounds = new Box(0, 0, map.Size.x - 1, map.Size.z - 1);
            TraverseParms parms = TraverseParms.For(
                TraverseMode.PassDoors, Danger.Deadly);
            foreach (Box band in Stampede.Bands(bounds, AnyRim.ToCell(baseCentre)))
            {
                List<IntVec3> standable = CellRect
                    .FromLimits(band.MinX, band.MinZ, band.MaxX, band.MaxZ)
                    .Cells
                    .Where(c => c.InBounds(map) && c.Standable(map))
                    .ToList();
                cells = standable
                    .Where(c => map.reachability.CanReach(
                        c, baseCentre, PathEndMode.Touch, parms))
                    .ToList();
                if (cells.Count >= Stampede.CellsNeeded(herd))
                {
                    note = $"band {band}";
                    cells.Shuffle();
                    return cells;
                }

                if (cells.Count > bestReaching)
                {
                    bestReaching = cells.Count;
                    bestStandable = standable.Count;
                    best = band;
                }
            }

            note = $"best band {best}: {bestStandable} of {best.Area} cells "
                + $"standable, {bestReaching} reach the base, "
                + $"{Stampede.CellsNeeded(herd)} needed";
            return new List<IntVec3>();
        }

        /// <summary>
        /// Starts the run: rain, the colonists drafted inside the base,
        /// the Ballista loaded with explosive bolts and holding its fire,
        /// and the herd's entry planned (it enters over the first ticks).
        /// </summary>
        protected override void SetupScene()
        {
            herd = new List<Pawn>();
            track = new Queue<(int Tick, Spot Centre)>();
            drafted = new List<Pawn>();
            volleys = new List<int>();
            lastVolleyAt = -1;
            volleyOrdered = false;
            lastInFlightAt = -1;
            impact = IntVec3.Invalid;
            aftermathAt = -1;
            firstVolleyAt = -1;
            stragglersDueAt = -1;
            cleanupRefused = false;
            refusedForSafety = 0;
            boltsSeen = Building_BatshittiumBallista.BoltsFired;
            toSpawn = HerdPlanned;
            entry = EntryCells(Map, Stage.BaseCentre, toSpawn, out string band);
            look = entry.Count > 0
                ? new IntVec3(
                    (int)entry.Average(c => c.x) - 12, 0,
                    (int)entry.Average(c => c.z))
                : base.FocusCell;

            Building_BatshittiumBallista ballista = Stage.Ballista;
            previousCount = Alive(ballista) ? ballista.Count : 16;
            if (Alive(ballista))
            {
                ballista.SetAmmo(BT_BallistaDefOf.BT_BoltReallyPainful);
                BallistaVolleyScene.HoldFire(ballista);
            }

            StartRain();
            GatherColonists();
            Log.Message($"{ShowcaseLog.Prefix} stampede: herd {toSpawn} on "
                + $"{entry.Count} cells of {band}; grid {GridNote(ballista)}");
        }

        /// <summary>
        /// Lets the herd in, plays its beats, aims and orders the volleys,
        /// sees stragglers off, and puts out the fires in the aftermath.
        /// </summary>
        public override void Tick()
        {
            if (herd == null || InIntro)
            {
                return; // the herd comes once the start line has been read
            }

            int now = Find.TickManager.TicksGame;
            SpawnSome();
            KeepRaining();
            Track(now);
            if (Elapsed >= FirstBeatTick && herd.Count > 0 && !Played("herd"))
            {
                look = HerdLook();
                BeatOnce("herd", Shot(40f), select: false);
            }

            Building_BatshittiumBallista ballista = Stage.Ballista;
            if (BatshitProjectileInFlight())
            {
                lastInFlightAt = now;
            }

            SeeVolleys(ballista, now);
            if (stragglersDueAt >= 0 && now >= stragglersDueAt)
            {
                PlayStragglers();
            }

            if (aftermathAt < 0)
            {
                Direct(ballista, now);
            }
            else
            {
                PutOutFires(now);
            }
        }

        /// <summary>
        /// Done once the aftermath has held, no bolt is in the air and the
        /// herd has fully entered.
        /// </summary>
        /// <returns>True when the stampede has played out.</returns>
        public override bool IsDone() =>
            aftermathAt >= 0 && toSpawn <= 0
            && Elapsed >= aftermathAt + holdTicks
            && !BatshitProjectileInFlight();

        /// <summary>
        /// Cleans up however the scene ended: the Ballista holds its fire
        /// at its old count, no live boomalope stays, the fires are out,
        /// the weather is given back and the colonists are undrafted.
        /// </summary>
        /// <param name="ending">How it ended.</param>
        public override void End(SceneEnd ending)
        {
            Building_BatshittiumBallista ballista = Stage.Ballista;
            if (Alive(ballista))
            {
                BallistaVolleyScene.HoldFire(ballista);
                ballista.SetCount(previousCount);
            }

            toSpawn = 0;
            int removed = 0;
            foreach (Pawn p in herd ?? new List<Pawn>())
            {
                // Those that walked off the edge have already left.
                if (p != null && !p.Dead && !p.Destroyed && p.Spawned)
                {
                    p.Destroy(DestroyMode.Vanish);
                    removed++;
                }
            }

            int fires = ExtinguishAll();
            StopRain();
            foreach (Pawn p in drafted ?? new List<Pawn>())
            {
                if (Alive(p) && p.drafter != null)
                {
                    p.drafter.Drafted = false;
                }
            }

            Log.Message($"{ShowcaseLog.Prefix} stampede ended {ending}: "
                + $"{Dead()} of {herd?.Count ?? 0} dead, volleys "
                + $"[{string.Join(", ", Volleys)}], {removed} removed alive, "
                + $"{fires} fires put out");
        }

        /// <summary>
        /// Spawns the next boomalopes of the herd, manhunting and silent,
        /// facing west.
        /// </summary>
        private void SpawnSome()
        {
            PawnKindDef kind = Boomalope;
            if (toSpawn <= 0 || kind == null || entry == null || entry.Count == 0)
            {
                toSpawn = 0;
                return;
            }

            int batch = Mathf.Min(Mathf.Max(1, spawnPerTick), toSpawn);
            for (int i = 0; i < batch; i++)
            {
                IntVec3 cell = entry[herd.Count % entry.Count];
                Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    kind, null, PawnGenerationContext.NonPlayer, tile: Map.Tile,
                    forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    fixedBiologicalAge: Rand.Range(1f, 6f)));
                GenSpawn.Spawn(pawn, cell, Map, Rot4.West, WipeMode.Vanish);
                pawn.mindState.mentalStateHandler.TryStartMentalState(
                    MentalStateDefOf.ManhunterPermanent, forced: true,
                    forceWake: true, transitionSilently: true);
                herd.Add(pawn);
            }

            toSpawn -= batch;
        }

        /// <summary>
        /// Orders the first volley once the herd is near enough for the
        /// grid to power it, and cleanup volleys at stragglers once each
        /// volley has landed; plays the aftermath when it is over.
        /// </summary>
        /// <param name="ballista">The Ballista.</param>
        /// <param name="now">The current tick.</param>
        private void Direct(Building_BatshittiumBallista ballista, int now)
        {
            if (volleyOrdered && now >= orderedAt + OrderPatienceTicks)
            {
                volleyOrdered = false; // never left: aim again
                if (Alive(ballista))
                {
                    ballista.OrderAttack(LocalTargetInfo.Invalid);
                }
            }

            bool settled = !volleyOrdered && !BatshitProjectileInFlight()
                && (lastInFlightAt < 0 || now >= lastInFlightAt + chainSettleTicks)
                && (lastVolleyAt < 0 || now >= lastVolleyAt + chainSettleTicks);
            if (toSpawn > 0 || !settled || !Played("herd"))
            {
                return;
            }

            List<Pawn> alive = Living();
            if (volleys.Count == 0)
            {
                bool givingUp = Elapsed >= volleyTick + giveUpTicks;
                if (Elapsed >= volleyTick
                    && ((Elapsed - volleyTick) % WaitNoteTicks == 0 || givingUp))
                {
                    NoteWait(ballista, alive, givingUp);
                }

                if (alive.Count == 0 || !Alive(ballista) || givingUp)
                {
                    // Nothing left to shoot, nothing to shoot with, or a
                    // grid that never powered the volley: the herd goes.
                    Aftermath(now);
                }
                else if (Elapsed >= volleyTick
                    && BoltsFor(ballista) >= Stampede.MoneyShotBolts)
                {
                    // Powered magazines only: a fresh grid turns its
                    // consumers on one at a time, so the volley waits.
                    TryFirst(ballista);
                }

                return;
            }

            // A cleanup the grid never powers gives up as the first does,
            // and one refused for the colonists' safety is not tried again.
            bool canFire = Alive(ballista) && !cleanupRefused
                && BoltsFor(ballista) >= cleanupBolts
                && now < lastVolleyAt + giveUpTicks;
            if (alive.Count == 0 || volleys.Count >= Stampede.MaxVolleys || !canFire)
            {
                // Let the last volley's line be read before the next.
                if (now >= lastVolleyAt + lineHoldTicks)
                {
                    Aftermath(now);
                }
            }
            else if (ballista.CooldownTicksLeft <= 0)
            {
                TryCleanup(ballista, alive);
            }
        }

        /// <summary>
        /// Logs why the first volley is still waiting (or has given up):
        /// where the herd is and how fast it comes, and what the grid and
        /// the magazines can give.
        /// </summary>
        /// <param name="ballista">The Ballista.</param>
        /// <param name="alive">The boomalopes standing.</param>
        /// <param name="givingUp">Whether the scene is giving up.</param>
        private void NoteWait(
            Building_BatshittiumBallista ballista, List<Pawn> alive, bool givingUp)
        {
            Spot? centre = Centre(alive);
            string herdNote = centre.HasValue && Alive(ballista)
                ? $"herd centre ({centre.Value.X:0},{centre.Value.Z:0}), "
                    + $"{Spot.Of(AnyRim.ToCell(ballista.Position)).DistanceTo(centre.Value):0} "
                    + $"tiles off, moving {Velocity(centre.Value).Length * GenTicks.TicksPerRealSecond:0.0} "
                    + "tiles a second"
                : "no herd standing";
            int hunting = alive.Count(p => p.MentalStateDef != null
                && p.MentalStateDef.IsAggro);
            string jobs = string.Join(", ", alive
                .GroupBy(p => p.CurJobDef?.defName ?? "none")
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key} {g.Count()}"));
            Log.Message($"{ShowcaseLog.Prefix} stampede: "
                + (givingUp ? "gave up on" : "still waiting for")
                + $" the volley at scene tick {Elapsed}: {alive.Count} "
                + $"standing ({hunting} manhunting; jobs {jobs}), "
                + $"{herdNote}; grid {GridNote(ballista)}");
        }

        /// <summary>
        /// Orders the 256-bolt volley at where the herd will be when it
        /// lands, if that is within the range the grid can power.
        /// </summary>
        /// <param name="ballista">The Ballista.</param>
        private void TryFirst(Building_BatshittiumBallista ballista)
        {
            Spot? centre = Centre(Living());
            if (centre == null)
            {
                return;
            }

            Spot from = Spot.Of(AnyRim.ToCell(ballista.Position));
            Spot velocity = Velocity(centre.Value);
            Spot aim = Stampede.Lead(
                from, centre.Value, velocity, ballista.BoltTilesPerTick);
            int reach = Reach(ballista, Stampede.MoneyShotBolts);
            if (reach > 0 && from.DistanceTo(aim) <= reach)
            {
                Log.Message($"{ShowcaseLog.Prefix} stampede: herd centre "
                    + $"({centre.Value.X:0},{centre.Value.Z:0}), "
                    + $"{from.DistanceTo(centre.Value):0} tiles off, moving "
                    + $"{velocity.Length * GenTicks.TicksPerRealSecond:0.0} "
                    + "tiles a second");
                if (Order(ballista, aim, Stampede.MoneyShotBolts)
                    == OrderResult.Unsafe)
                {
                    // The herd is already too near to shell: it goes home
                    // rather than the colonists being shelled with it.
                    Aftermath(Find.TickManager.TicksGame);
                }
            }
        }

        /// <summary>
        /// Orders a cleanup volley at the thickest group of stragglers,
        /// led by their pace toward the base, within the powered range.
        /// </summary>
        /// <param name="ballista">The Ballista.</param>
        /// <param name="alive">The living boomalopes.</param>
        private void TryCleanup(Building_BatshittiumBallista ballista, List<Pawn> alive)
        {
            Spot from = Spot.Of(AnyRim.ToCell(ballista.Position));
            List<Spot> spots = alive.Select(p => Spot.Of(AnyRim.ToCell(p.Position))).ToList();
            Spot? centre = Stampede.Centroid(spots);
            float scatter = ballista.ScatterRadius(
                centre.HasValue ? (float)from.DistanceTo(centre.Value) : 30f);
            int best = Stampede.BestCover(spots, scatter);
            int reach = Reach(ballista, cleanupBolts);
            if (best < 0 || reach <= 0)
            {
                Aftermath(Find.TickManager.TicksGame);
                return;
            }

            Spot aim = Stampede.Lead(
                from, spots[best], TowardBase(spots[best], alive), ballista.BoltTilesPerTick);
            if (Order(ballista, Stampede.Within(from, aim, reach), cleanupBolts)
                == OrderResult.Unsafe)
            {
                cleanupRefused = true; // Direct goes to the aftermath
            }
        }

        /// <summary>
        /// Sets the Ballista's count and range for a volley and orders it at
        /// a cell, kept on the map and beyond the Ballista's least range,
        /// unless the volley's footprint (its scatter, a bolt's blast and a
        /// boomalope's burst) would reach the base's square or a colonist.
        /// </summary>
        /// <param name="ballista">The Ballista.</param>
        /// <param name="aim">Where to aim.</param>
        /// <param name="bolts">Bolts in the volley.</param>
        /// <returns>How the order went.</returns>
        private OrderResult Order(
            Building_BatshittiumBallista ballista, Spot aim, int bolts)
        {
            Cell c = aim.ToCell();
            IntVec3 cell = new IntVec3(c.X, 0, c.Z).ClampInsideMap(Map);
            double distance = cell.DistanceTo(ballista.Position);
            if (distance < BallistaPower.MinRange)
            {
                return OrderResult.TooNear;
            }

            double footprint = Stampede.Footprint(
                ballista.ScatterRadius((float)distance), BoltBlastTiles(ballista));
            bool baseClear = Stampede.SafeAim(
                AnyRim.ToCell(Stage.BaseCentre), AnyRim.ToCell(cell), footprint);
            Pawn near = Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                p => p.Position.InHorDistOf(cell, (float)footprint));
            if (!baseClear || near != null)
            {
                refusedForSafety++;
                Log.Message($"{ShowcaseLog.Prefix} stampede: volley "
                    + $"{volleys.Count + 1} of {bolts} at ({cell.x},{cell.z}) "
                    + $"refused: its {footprint:0.0}-tile footprint reaches "
                    + (baseClear ? $"{near.LabelShort}" : "the base")
                    + $" at scene tick {Elapsed}; {Living().Count} standing");
                return OrderResult.Unsafe;
            }

            ballista.SetCount(bolts);
            ballista.SetRange(Stampede.RangeFor(distance));
            ballista.OrderAttack(new LocalTargetInfo(cell));
            volleyOrdered = true;
            orderedAt = Find.TickManager.TicksGame;
            Log.Message($"{ShowcaseLog.Prefix} stampede: volley "
                + $"{volleys.Count + 1} of {bolts} ordered at scene tick "
                + $"{Elapsed}, {distance:0} tiles to ({cell.x},{cell.z}); "
                + $"{Living().Count} standing; grid {GridNote(ballista)}");
            if (!impact.IsValid)
            {
                impact = cell;
            }

            return OrderResult.Ordered;
        }

        /// <summary>
        /// Notices each volley as it leaves: records its bolts, plays its
        /// beat, and clears the order so the Ballista does not fire it
        /// again on its next cooldown.
        /// </summary>
        /// <param name="ballista">The Ballista.</param>
        /// <param name="now">The current tick.</param>
        private void SeeVolleys(Building_BatshittiumBallista ballista, int now)
        {
            int fired = Building_BatshittiumBallista.BoltsFired - boltsSeen;
            boltsSeen = Building_BatshittiumBallista.BoltsFired;
            if (fired <= 0)
            {
                return;
            }

            volleys.Add(fired);
            lastVolleyAt = now;
            volleyOrdered = false;
            if (Alive(ballista))
            {
                ballista.OrderAttack(LocalTargetInfo.Invalid);
            }

            if (volleys.Count == 1)
            {
                firstVolleyAt = now;
                look = Midway(ballista, impact);
                BeatOnce("volley", Shot(56f), select: true);
            }
            else if (!Played(TourScript.Stragglers) && stragglersDueAt < 0)
            {
                // The cleanup fires at once, to keep the herd off the base,
                // but its line waits until the volley's line has been read.
                stragglersDueAt = System.Math.Max(now, firstVolleyAt + lineHoldTicks);
            }
        }

        /// <summary>
        /// Plays the stragglers' beat that was held back for the volley's
        /// line, if one waits; its line has no camera move.
        /// </summary>
        private void PlayStragglers()
        {
            if (stragglersDueAt < 0)
            {
                return;
            }

            stragglersDueAt = -1;
            BeatOnce(TourScript.Stragglers, null, select: false);
        }

        /// <summary>
        /// The explosion radius of the bolt the Ballista fires, from its
        /// projectile's def, or Core's default for the explosive bolt.
        /// </summary>
        /// <param name="ballista">The Ballista.</param>
        /// <returns>Tiles.</returns>
        private static double BoltBlastTiles(Building_BatshittiumBallista ballista)
        {
            float? radius = ballista?.AmmoDef?.GetModExtension<BoltExtension>()?
                .projectile?.projectile?.explosionRadius;
            return radius is float r && r > 0f ? r : Stampede.DefaultBoltBlastTiles;
        }

        /// <summary>
        /// Plays the aftermath once: the Ballista stands down, and any
        /// boomalope still alive stops manhunting and heads back east.
        /// </summary>
        /// <param name="now">The current tick.</param>
        private void Aftermath(int now)
        {
            if (aftermathAt >= 0)
            {
                return;
            }

            // A held stragglers' line always comes before the aftermath's.
            PlayStragglers();
            aftermathAt = Elapsed;
            Log.Message($"{ShowcaseLog.Prefix} stampede: aftermath at scene "
                + $"tick {Elapsed}; {Living().Count} standing, "
                + $"{Map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count} fires");
            if (Alive(Stage.Ballista))
            {
                BallistaVolleyScene.HoldFire(Stage.Ballista);
            }

            foreach (Pawn p in Living())
            {
                SendHome(p);
            }

            look = impact.IsValid ? impact : HerdLook();
            BeatOnce("aftermath", Shot(36f), select: false);
        }

        /// <summary>
        /// Calms a boomalope (silently) and walks it off the east edge.
        /// </summary>
        /// <param name="p">The boomalope.</param>
        private void SendHome(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.Downed)
            {
                return;
            }

            p.mindState?.mentalStateHandler?.Reset();
            IntVec3 exit = new IntVec3(Map.Size.x - 1, 0, p.Position.z);
            if (p.jobs != null && CellFinder.TryFindRandomCellNear(
                exit, Map, 6, c => c.Standable(Map) && c.OnEdge(Map), out IntVec3 edge))
            {
                Job job = JobMaker.MakeJob(JobDefOf.Goto, edge);
                job.exitMapOnArrival = true;
                p.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }

        /// <summary>
        /// Puts out a share of the fires each tick of the hold, so the last
        /// is out as it ends: the rain finishing what it started.
        /// </summary>
        /// <param name="now">The current tick.</param>
        private void PutOutFires(int now)
        {
            int left = aftermathAt + holdTicks - Elapsed;
            List<Thing> fires = Map.listerThings.ThingsOfDef(ThingDefOf.Fire).ToList();
            if (fires.Count == 0)
            {
                return;
            }

            int n = left <= 1 ? fires.Count : Mathf.CeilToInt(fires.Count / (float)left);
            foreach (Thing fire in fires.InRandomOrder().Take(n))
            {
                if (!fire.Destroyed)
                {
                    fire.Destroy(DestroyMode.Vanish);
                }
            }
        }

        /// <summary>Puts out every fire on the map.</summary>
        /// <returns>How many were burning.</returns>
        private int ExtinguishAll()
        {
            if (Map == null)
            {
                return 0;
            }

            List<Thing> fires = Map.listerThings.ThingsOfDef(ThingDefOf.Fire).ToList();
            foreach (Thing fire in fires)
            {
                if (!fire.Destroyed)
                {
                    fire.Destroy(DestroyMode.Vanish);
                }
            }

            return fires.Count;
        }

        /// <summary>Records the herd's centre for its velocity.</summary>
        /// <param name="now">The current tick.</param>
        private void Track(int now)
        {
            Spot? centre = Centre(Living());
            if (centre == null || toSpawn > 0)
            {
                track.Clear();
                return;
            }

            track.Enqueue((now, centre.Value));
            while (track.Count > 0 && track.Peek().Tick < now - VelocityWindow)
            {
                track.Dequeue();
            }
        }

        /// <summary>
        /// The herd's velocity: its centre's drift over the window, or its
        /// pace toward the base before there is a window.
        /// </summary>
        /// <param name="centre">The herd's centre now.</param>
        /// <returns>Tiles a tick.</returns>
        private Spot Velocity(Spot centre)
        {
            if (track.Count >= 2)
            {
                (int tick, Spot then) = track.Peek();
                int ticks = Find.TickManager.TicksGame - tick;
                if (ticks >= VelocityWindow / 2)
                {
                    return (centre - then) * (1.0 / ticks);
                }
            }

            return TowardBase(centre, Living());
        }

        /// <summary>
        /// A velocity toward the base at the boomalopes' mean walking pace:
        /// where a manhunter makes for, at the speed it walks.
        /// </summary>
        /// <param name="from">Where it is.</param>
        /// <param name="alive">The boomalopes, for their pace.</param>
        /// <returns>Tiles a tick.</returns>
        private Spot TowardBase(Spot from, List<Pawn> alive)
        {
            Spot to = Spot.Of(AnyRim.ToCell(Stage.BaseCentre)) - from;
            double length = to.Length;
            if (length < 1 || alive.Count == 0)
            {
                return new Spot(0, 0);
            }

            double pace = alive.Average(p => p.GetStatValue(StatDefOf.MoveSpeed))
                / GenTicks.TicksPerRealSecond;
            return to * (pace / length);
        }

        /// <summary>
        /// The longest range the grid powers for a volley of this many
        /// bolts, less a tile for the range's rounding, keeping the
        /// magazines' feeding and Core's margin spare.
        /// </summary>
        /// <param name="ballista">The Ballista.</param>
        /// <param name="bolts">Bolts in the volley.</param>
        /// <returns>Tiles; zero when it cannot fire at all.</returns>
        private static int Reach(Building_BatshittiumBallista ballista, int bolts)
        {
            double watts = ballista.GridWattsForBurst()
                - ballista.MagazineFeedingWatts() - Stampede.PowerMarginWatts;
            return System.Math.Max(0, BallistaPower.LongestRangeWithin(bolts, watts) - 1);
        }

        /// <summary>The explosive bolts a Ballista can draw.</summary>
        /// <param name="ballista">The Ballista, or null.</param>
        /// <returns>The bolts; zero without one.</returns>
        private static int BoltsFor(Building_BatshittiumBallista ballista)
        {
            if (!Alive(ballista))
            {
                return 0;
            }

            return ballista.BoltsAvailable(BT_BallistaDefOf.BT_BoltReallyPainful);
        }

        /// <summary>
        /// The grid's numbers for the log: watts, feeding, reach.
        /// </summary>
        /// <param name="ballista">The Ballista, or null.</param>
        /// <returns>The note.</returns>
        private static string GridNote(Building_BatshittiumBallista ballista) =>
            Alive(ballista)
                ? $"{ballista.GridWattsForBurst():N0} W for a burst, "
                    + $"{ballista.MagazineFeedingWatts():N0} W feeding, "
                    + $"{BoltsFor(ballista)} explosive bolts, 256-bolt reach "
                    + $"{Reach(ballista, Stampede.MoneyShotBolts)} tiles"
                : "no Ballista";

        /// <summary>
        /// Starts the rain at once, remembering the weather.
        /// </summary>
        private void StartRain()
        {
            WeatherManager weather = Map?.weatherManager;
            WeatherDef rain = DefDatabase<WeatherDef>.GetNamedSilentFail("Rain");
            previousWeather = weather?.curWeather;
            if (weather != null && rain != null)
            {
                weather.TransitionTo(rain);
                weather.curWeatherAge = WeatherTransitionTicks;
            }
        }

        /// <summary>
        /// Starts the rain again if the weather changed on its own: a fire
        /// on the map shortens the weather's turn.
        /// </summary>
        private void KeepRaining()
        {
            WeatherManager weather = Map?.weatherManager;
            if (aftermathAt < 0 && weather != null && Elapsed % RainCheckTicks == 0
                && weather.curWeather?.defName != "Rain")
            {
                WeatherDef rain = DefDatabase<WeatherDef>.GetNamedSilentFail("Rain");
                if (rain != null)
                {
                    weather.TransitionTo(rain);
                    weather.curWeatherAge = WeatherTransitionTicks;
                }
            }
        }

        /// <summary>
        /// Gives back the weather from before the rain, at once.
        /// </summary>
        private void StopRain()
        {
            WeatherManager weather = Map?.weatherManager;
            if (weather != null && previousWeather != null
                && weather.curWeather != previousWeather)
            {
                weather.TransitionTo(previousWeather);
                weather.curWeatherAge = WeatherTransitionTicks;
            }
        }

        /// <summary>
        /// Moves every colonist outside the base's square into it, and
        /// drafts the able with Fire At Will off, so no one wanders east
        /// during the stampede and no one fires at a boomalope near the
        /// base (its burst would set fires among them).
        /// </summary>
        /// <remarks>
        /// The tour's grenadier holds the Ergonomic Singularity Grenade,
        /// and a drafted pawn waiting with Fire At Will on throws at any
        /// threat in range (JobDriver_Wait.CheckForAutoAttack), which here
        /// would open a singularity among the colonists. The Drafted
        /// setter turns Fire At Will back on, so it is turned off after.
        /// Undrafting at the end turns it on again.
        /// </remarks>
        private void GatherColonists()
        {
            Box square = Stampede.BaseSquare(AnyRim.ToCell(Stage.BaseCentre));
            Showcase.MoveColonistsOut(
                Map, c => !square.Contains(AnyRim.ToCell(c)));
            foreach (Pawn p in Map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p.drafter != null && !p.Drafted && !p.Downed
                    && !p.InMentalState)
                {
                    p.drafter.Drafted = true;
                    p.drafter.FireAtWill = false; // as TractorScenes.PlaceStray
                    drafted.Add(p);
                }
            }
        }

        /// <summary>
        /// The herd's members still on their feet: alive, on the map and
        /// not downed. A downed boomalope is no threat to aim at, and it
        /// cannot walk home (pathing while downed is an error); the end
        /// removes it with the rest.
        /// </summary>
        /// <returns>The boomalopes standing.</returns>
        private List<Pawn> Living() =>
            herd?.Where(p => p != null && !p.Dead && p.Spawned && !p.Downed)
                .ToList()
            ?? new List<Pawn>();

        /// <summary>How many of the herd are dead.</summary>
        /// <returns>The dead.</returns>
        private int Dead() => herd?.Count(p => p != null && p.Dead) ?? 0;

        /// <summary>The centre of some boomalopes.</summary>
        /// <param name="pawns">The boomalopes.</param>
        /// <returns>The centre, or null for none.</returns>
        private static Spot? Centre(IEnumerable<Pawn> pawns) =>
            Stampede.Centroid(pawns.Select(p => Spot.Of(AnyRim.ToCell(p.Position))));

        /// <summary>
        /// Where the camera looks at the herd: its centre, a little west,
        /// so the open ground it crosses is in the frame.
        /// </summary>
        /// <returns>The cell.</returns>
        private IntVec3 HerdLook()
        {
            Spot? centre = Centre(Living());
            return centre.HasValue
                ? new IntVec3((int)centre.Value.X - 12, 0, (int)centre.Value.Z)
                    .ClampInsideMap(Map)
                : look;
        }

        /// <summary>
        /// The cell halfway between the Ballista and the impact.
        /// </summary>
        /// <param name="ballista">The Ballista.</param>
        /// <param name="target">Where the volley lands.</param>
        /// <returns>The cell, or the impact if the Ballista is gone.</returns>
        private static IntVec3 Midway(Thing ballista, IntVec3 target) =>
            Alive(ballista) && target.IsValid
                ? new IntVec3(
                    (ballista.Position.x + target.x) / 2, 0,
                    (ballista.Position.z + target.z) / 2)
                : target;
    }
}
