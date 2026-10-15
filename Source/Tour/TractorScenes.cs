// Batshit Technology - the directed tour's scenes, part two.
// The Tractor Bomb in the yard Core's TourStagePlanner chose: one robot
// from the barn against a small party (scene 7), then two Nosy robots in
// the junkyard against an army (scene 8), staged with the smoke runner's
// own helpers (TractorStaging) and Core's layouts. Raiders are unarmed and
// stunned until the bombs are down, so the robots arrive whole; every
// bomb's hostile rolls are seeded, so a run plays out as rehearsed.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;
using Verse.AI;

namespace BatshitTechnology
{
    /// <summary>
    /// A tour scene set in the tractor yard: it declines without a yard
    /// or a hostile faction, clears the yard, moves colonists out of
    /// reach, and counts the Tractor Bombs that go off. Its robots run the
    /// tour's settings, one up from the defaults: Popular and Block Party
    /// (Core's <see cref="TourStagePlanner.TourPull"/>).
    /// </summary>
    public abstract class TractorYardScene : TourScene
    {
        /// <summary>The seed for every bomb's hostile rolls.</summary>
        public int seed = 20261014;

        /// <summary>The Tractor Bombs gone off before this run.</summary>
        [Unsaved]
        private int explosionsAtStart;

        /// <summary>Gets the yard's centre.</summary>
        protected IntVec3 Centre => Stage.YardCentre;

        /// <summary>Gets the Tractor Bombs gone off in this run.</summary>
        protected int ExplosionsSinceStart =>
            Building_TractorBomb.Explosions - explosionsAtStart;

        /// <summary>
        /// Gets why the scene cannot run: no yard, or no hostile faction
        /// to send.
        /// </summary>
        protected override string WhyUnavailable
        {
            get
            {
                if (!Stage.Yard.HasValue)
                {
                    return "no tractor yard: "
                        + (Stage.PlanNote ?? "no candidate was clear");
                }

                return Find.FactionManager.RandomEnemyFaction(
                    allowNonHumanlike: false) == null
                    ? "no faction is hostile"
                    : null;
            }
        }

        /// <summary>
        /// Clears the yard (level ground, no fires, hostiles or robots),
        /// moves every colonist within the Tractor Bomb's reach of it back
        /// to the base, and notes the explosions so far.
        /// </summary>
        protected void PrepareYard()
        {
            CellRect yard = Stage.Yard.Value;
            TractorStaging.ClearYard(Map, yard);
            Showcase.MoveColonistsOut(Map, InYardReach);
            explosionsAtStart = Building_TractorBomb.Explosions;
        }

        /// <summary>
        /// Whether a cell is within a Tractor Bomb's reach of the yard:
        /// the yard grown by <see cref="TourStagePlanner.TractorReach"/>,
        /// the flee distance at the tour's settings.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <returns>True if a bomb in the yard could reach it.</returns>
        protected bool InYardReach(IntVec3 cell) =>
            Stage.Yard.HasValue
            && Stage.Yard.Value.ExpandedBy(TourStagePlanner.TractorReach)
                .Contains(cell);

        /// <summary>
        /// Lets the colonists the scene held clear of the yard go, however
        /// it ended.
        /// </summary>
        /// <param name="ending">How it ended.</param>
        public override void End(SceneEnd ending)
        {
            ReleaseHeldColonists();
        }

        /// <summary>
        /// The bombs emplaced since this run began, in the order seen.
        /// </summary>
        /// <param name="known">The bombs already known; new ones are added.</param>
        /// <returns>The bombs that are new this tick.</returns>
        protected List<Building_TractorBomb> NewBombs(
            List<Building_TractorBomb> known)
        {
            var found = new List<Building_TractorBomb>();
            foreach (Building_TractorBomb bomb in Map.listerThings
                .ThingsOfDef(BT_TractorBombDefOf.BT_TractorBombEmplaced)
                .OfType<Building_TractorBomb>())
            {
                if (bomb.Spawned && bomb.EmplacedTick >= StartTick
                    && !known.Contains(bomb))
                {
                    bomb.SeedHostileRolls(seed);
                    known.Add(bomb);
                    found.Add(bomb);
                }
            }

            return found;
        }

        /// <summary>
        /// The tour's settings for a robot: the given proximity with the
        /// tour's pull and blast.
        /// </summary>
        /// <param name="proximity">How close it drives.</param>
        /// <returns>The settings.</returns>
        protected static TractorBombSettings TourSettings(
            TractorBombProximity proximity) =>
            new TractorBombSettings(
                proximity, TourStagePlanner.TourPull,
                TourStagePlanner.TourBlast);

        /// <summary>
        /// Fits a robot with the tour's settings, whatever the research.
        /// </summary>
        /// <param name="robot">The robot.</param>
        /// <param name="proximity">How close it drives.</param>
        protected static void Fit(Pawn robot, TractorBombProximity proximity) =>
            robot?.TryGetComp<CompTractorBomb>()?.SetSettings(
                TourSettings(proximity));

        /// <summary>Lets stunned raiders go, to react as they rolled.</summary>
        /// <param name="raiders">The raiders.</param>
        protected static void Release(IEnumerable<Pawn> raiders)
        {
            foreach (Pawn p in raiders ?? Enumerable.Empty<Pawn>())
            {
                if (p.Spawned && !p.Dead)
                {
                    p.stances?.stunner?.StopStun();
                }
            }
        }

        /// <summary>
        /// Done once the scene's bombs have gone off and no implosion is
        /// still running.
        /// </summary>
        /// <param name="bombs">How many bombs the scene sets off.</param>
        /// <returns>True when the yard is quiet.</returns>
        protected bool AllWentOff(int bombs) =>
            ExplosionsSinceStart >= bombs && !ImplosionActive();
    }

    /// <summary>
    /// Scene 7, "The Tractor of Tomorrow": the barn, a robot "made
    /// earlier" at its door, and a party of four raiders across the yard.
    /// The robot spots them, protests at the tour group's stray standing
    /// in its pull (a friendly guest, which it objects to as it does to a
    /// colonist; slice 19000c), who is sent away, digs in, and goes off.
    /// The robot is Nosy (Core's
    /// <see cref="SingleTractorLayout.TourProximity"/>), so its
    /// 33.75-tile pull stops short of the barn.
    /// </summary>
    public class TractorBombScene : TractorYardScene
    {
        /// <summary>When the barn's beat plays, in ticks.</summary>
        public int barnTick = FirstBeatTick;

        /// <summary>When the robot rolls out of the barn, in ticks.</summary>
        public int deliverTick = 480;

        /// <summary>
        /// How far north the stray is sent, in tiles: Core's
        /// <see cref="SingleTractorLayout.StrayRetreat"/>, which its tests
        /// hold beyond the flee distance from where the robot plants.
        /// </summary>
        public int strayRetreat = SingleTractorLayout.StrayRetreat;

        /// <summary>
        /// The zoom of the pull shot: wide enough for the
        /// 33.75-tile pull's ring (67.5 tiles across) with room round it.
        /// </summary>
        public float pullZoom = 52f;

        /// <summary>The barn.</summary>
        [Unsaved]
        private Thing barn;

        /// <summary>The robot.</summary>
        [Unsaved]
        private Pawn robot;

        /// <summary>The tourist standing in the robot's way, or null.</summary>
        [Unsaved]
        private Pawn stray;

        /// <summary>
        /// Gets the tourist this run stood in the robot's pull, or null
        /// (the tour verifier checks it is a tourist, never a colonist).
        /// </summary>
        internal Pawn StrayPlaced => stray;

        /// <summary>Whether the stray has been sent away.</summary>
        [Unsaved]
        private bool straySent;

        /// <summary>The raiders.</summary>
        [Unsaved]
        private List<Pawn> party;

        /// <summary>
        /// Gets the party of raiders: the hostiles this run staged.
        /// </summary>
        internal override IEnumerable<Pawn> StagedHostiles =>
            party ?? Enumerable.Empty<Pawn>();

        /// <summary>The bomb, once emplaced.</summary>
        [Unsaved]
        private List<Building_TractorBomb> bombs;

        /// <summary>
        /// What became of the barn by the time the scene ended: "standing",
        /// "destroyed", "gone" (despawned, as a ripped-loose building
        /// minified is), "none", or "not ended". Taken in
        /// <see cref="End"/>, before the next scene's set-up clears the
        /// yard; the verifier checks it.
        /// </summary>
        [Unsaved]
        private string barnAtEnd = "not ended";

        /// <summary>
        /// Gets what became of the barn by the scene's end: "standing" if
        /// the pull left it alone.
        /// </summary>
        internal string BarnAtEnd => barnAtEnd;

        /// <summary>Gets the bomb, else the robot, else the barn.</summary>
        public override Thing Subject
        {
            get
            {
                Building_TractorBomb bomb = bombs?.FirstOrDefault(b => Alive(b));
                if (bomb != null)
                {
                    return bomb;
                }

                return Alive(robot) ? robot : Alive(barn) ? barn : null;
            }
        }

        /// <summary>Gets the subject, or the yard's centre.</summary>
        public override IntVec3 FocusCell
        {
            get
            {
                Thing subject = Subject;
                return subject != null ? subject.Position
                    : Centre.IsValid ? Centre : base.FocusCell;
            }
        }

        /// <summary>Clears the yard; places the barn, the party and the stray.</summary>
        protected override void SetupScene()
        {
            PrepareYard();
            robot = null;
            straySent = false;
            barnAtEnd = "not ended";
            bombs = new List<Building_TractorBomb>();
            barn = TractorStaging.SpawnBarn(
                Map, TractorStaging.At(Centre, SingleTractorLayout.Barn));
            (barn as Building_TractorBarn)?.SetSettings(
                TourSettings(SingleTractorLayout.TourProximity));
            party = TractorStaging.SpawnArmy(
                Map, Centre, SingleTractorLayout.Party, armed: false,
                stunned: true);
            stray = PlaceStray();
            HoldColonistsClear(InYardReach);
        }

        /// <summary>
        /// Leaves the stray where <see cref="PlaceStray"/> put it; the rest
        /// of the group waits in the base.
        /// </summary>
        /// <param name="group">The tour group.</param>
        protected override void PlaceTourists(TourGroup group)
        {
            group.RestAll(stray);
        }

        /// <summary>Plays the scene's beats as the world reaches them.</summary>
        public override void Tick()
        {
            KeepColonistsClear(InYardReach);
            if (Elapsed >= barnTick)
            {
                BeatOnce("barn", Shot(30f));
            }

            if (robot == null && Elapsed >= deliverTick)
            {
                robot = TractorStaging.SpawnRobot(
                    Map, TractorStaging.At(Centre, SingleTractorLayout.BarnDoor));
                Fit(robot, SingleTractorLayout.TourProximity);
                BeatOnce("delivered", Shot(30f));
            }

            if (Alive(robot))
            {
                if (robot.CurJobDef == BT_TractorBombDefOf.BT_TractorBombApproach)
                {
                    BeatOnce("spotted", Shot(34f));
                }

                if (robot.TryGetComp<CompTractorBomb>()?.Protesting == true)
                {
                    BeatOnce("protest");
                    SendStrayAway();
                }
            }

            if (NewBombs(bombs).Count > 0)
            {
                Release(party);
                SendStrayAway();
                BeatOnce("emplaced", Shot(24f));
            }

            if (bombs.Count > 0 && Map.mapPawns.AllPawnsSpawned.Any(
                p => p.CurJobDef == BT_TractorBombDefOf.BT_TractorBombFreeze))
            {
                BeatOnce("doom");
            }

            if (bombs.Any(b => Alive(b)
                && b.CurrentStage == Building_TractorBomb.Stage.Imploding))
            {
                BeatOnce("pull", Shot(pullZoom), select: false);
            }

            if (ExplosionsSinceStart > 0)
            {
                BeatOnce("blast", Shot(45f), select: false);
            }
        }

        /// <summary>Done once the bomb has gone off and the pull is over.</summary>
        /// <returns>True when the yard is quiet.</returns>
        public override bool IsDone() => AllWentOff(1);

        /// <summary>
        /// Notes the barn's fate, lets the colonists go, and says what the
        /// yard held if the bomb never went off. The stray rejoins the
        /// group with the next scene.
        /// </summary>
        /// <param name="ending">How it ended.</param>
        public override void End(SceneEnd ending)
        {
            barnAtEnd = barn == null ? "none"
                : barn.Destroyed ? "destroyed"
                : barn.Spawned ? "standing" : "gone";
            base.End(ending);
            if (ending == SceneEnd.TimedOut)
            {
                Log.Message($"{ShowcaseLog.Prefix} tractor timed out: "
                    + Diagnosis());
            }
        }

        /// <summary>
        /// What the yard holds, for a scene that timed out: the robot,
        /// the bombs, the raiders and the implosions.
        /// </summary>
        /// <returns>One line.</returns>
        private string Diagnosis()
        {
            string robotState = robot == null
                ? "never delivered"
                : $"spawned {robot.Spawned}, dead {robot.Dead}, "
                    + $"job {robot.CurJobDef?.defName ?? "none"}";
            int standing = party?.Count(
                p => p.Spawned && !p.Dead && !p.Downed) ?? 0;
            return $"robot {robotState}; bombs {bombs?.Count ?? 0}, "
                + $"explosions {ExplosionsSinceStart}; raiders standing "
                + $"{standing}; implosions running "
                + $"{RunningImplosions().Count()}";
        }

        /// <summary>
        /// Stands the tour group's stray where the robot will find it in
        /// its pull (Core's layout says where), else the first tourist
        /// still standing; never a colonist (docs/SHOWCASE.md, "Tourists").
        /// The robot objects to a friendly guest as it does to a colonist
        /// (Core's TractorBombProtest). The choice is logged; with no
        /// tourist left there is no stray and no protest.
        /// </summary>
        /// <returns>The tourist, or null if none could be placed.</returns>
        private Pawn PlaceStray()
        {
            TourGroup group = Stage.Tourists;
            Pawn pawn = group == null || group.Dismissed ? null
                : TourGroup.Standing(group.Stray) ? group.Stray
                : group.Members.FirstOrDefault(TourGroup.Standing);
            IntVec3 cell = TractorStaging.At(Centre, SingleTractorLayout.Stray);
            if (pawn == null || !cell.InBounds(Map) || !group.Place(pawn, cell))
            {
                Log.Message($"{ShowcaseLog.Prefix} tractor: no stray ("
                    + (pawn == null ? "no tourist standing" : $"{cell} is no "
                        + "place to stand") + "), so no protest");
                return null;
            }

            string why = group.RoleOf(pawn) == TouristRole.Stray
                ? "tourist, the group's stray"
                : $"tourist, the group's {group.RoleOf(pawn)?.ToString().ToLowerInvariant()}";
            Log.Message($"{ShowcaseLog.Prefix} tractor: stray "
                + $"{pawn.LabelShort} ({why}) at {pawn.Position}");
            return pawn;
        }

        /// <summary>
        /// Sends the stray out of the robot's reach at a jog: north, out of
        /// the yard, or to the base if nowhere there will do; it waits
        /// there for the rest of the scene.
        /// </summary>
        private void SendStrayAway()
        {
            if (straySent || !TourGroup.Standing(stray))
            {
                return;
            }

            straySent = true;
            IntVec3 away = (stray.Position + new IntVec3(0, 0, strayRetreat))
                .ClampInsideMap(Map);
            if (!CellFinder.TryFindRandomCellNear(
                away, Map, 6, c => c.Standable(Map), out IntVec3 cell))
            {
                cell = Map.Center;
            }

            Stage.Tourists?.Hurry(stray, cell);
        }
    }

    /// <summary>
    /// Scene 8, "Two Tractors": the junkyard and an army of fourteen; two
    /// Nosy robots drive in from either side and plant; the army is let
    /// go, and both pulls start together, so the junk between them is
    /// fought over before both go off.
    /// </summary>
    public class TwinTractorsScene : TractorYardScene
    {
        /// <summary>
        /// Ticks from both bombs counting down to both imploding: time for
        /// the raiders to react as they rolled.
        /// </summary>
        public int implodeDelay = 150;

        /// <summary>
        /// The zoom of the pull shot: wide enough for both 33.75-tile
        /// pulls' rings (about 82 tiles across and 68 tall together).
        /// </summary>
        public float pullZoom = 52f;

        /// <summary>The raiders.</summary>
        [Unsaved]
        private List<Pawn> army;

        /// <summary>
        /// Gets the junkyard's raiders: the hostiles this run staged.
        /// </summary>
        internal override IEnumerable<Pawn> StagedHostiles =>
            army ?? Enumerable.Empty<Pawn>();

        /// <summary>The robots.</summary>
        [Unsaved]
        private List<Pawn> robots;

        /// <summary>The bombs, once emplaced.</summary>
        [Unsaved]
        private List<Building_TractorBomb> bombs;

        /// <summary>The tick both bombs were counting down, or -1.</summary>
        [Unsaved]
        private int plantedAt = -1;

        /// <summary>Whether both have been told to implode.</summary>
        [Unsaved]
        private bool imploded;

        /// <summary>Whether the robots have been sent in.</summary>
        [Unsaved]
        private bool robotsSent;

        /// <summary>Gets the first bomb, else the first robot.</summary>
        public override Thing Subject =>
            bombs?.FirstOrDefault(b => Alive(b))
            ?? (Thing)robots?.FirstOrDefault(r => Alive(r));

        /// <summary>Gets the junkyard's centre.</summary>
        public override IntVec3 FocusCell =>
            Centre.IsValid ? Centre : base.FocusCell;

        /// <summary>Clears the yard; lays out the junk, the army and the robots.</summary>
        protected override void SetupScene()
        {
            PrepareYard();
            plantedAt = -1;
            imploded = false;
            bombs = new List<Building_TractorBomb>();
            TractorStaging.BuildJunkyard(Map, Centre);
            army = TractorStaging.SpawnArmy(
                Map, Centre, TwinJunkyardLayout.Army, armed: false,
                stunned: true);
            robots = new List<Pawn>();
            robotsSent = false;
            HoldColonistsClear(InYardReach);
        }

        /// <summary>
        /// Sends the two Nosy robots in from either side, once the scene's
        /// start line has been read.
        /// </summary>
        private void SendInRobots()
        {
            robotsSent = true;
            foreach (Cell offset in TwinJunkyardLayout.DriveIn)
            {
                IntVec3 cell = TractorStaging.At(Centre, offset);
                if (!cell.InBounds(Map))
                {
                    continue;
                }

                Pawn robot = TractorStaging.SpawnRobot(Map, cell);
                Fit(robot, TractorBombProximity.Nosy);

                robots.Add(robot);
            }
        }

        /// <summary>Plays the scene's beats as the world reaches them.</summary>
        public override void Tick()
        {
            KeepColonistsClear(InYardReach);
            if (InIntro)
            {
                return; // the robots come once the start line has been read
            }

            if (!robotsSent)
            {
                SendInRobots();
            }

            NewBombs(bombs);
            if (plantedAt < 0 && bombs.Count >= 2 && bombs.All(b => Alive(b)
                && b.CurrentStage == Building_TractorBomb.Stage.CountingDown))
            {
                plantedAt = Elapsed;
                Release(army);
                BeatOnce("planted", Shot(30f));
            }

            if (plantedAt >= 0 && !imploded && Elapsed >= plantedAt + implodeDelay)
            {
                imploded = true;
                foreach (Building_TractorBomb bomb in bombs.Where(b => Alive(b)
                    && b.CurrentStage == Building_TractorBomb.Stage.CountingDown))
                {
                    bomb.ImplodeNow();
                }
            }

            if (bombs.Any(b => Alive(b)
                && b.CurrentStage == Building_TractorBomb.Stage.Imploding))
            {
                BeatOnce("pull", Shot(pullZoom), select: false);
            }

            if (ExplosionsSinceStart > 0)
            {
                BeatOnce("blast", Shot(45f), select: false);
            }
        }

        /// <summary>Done once both bombs have gone off and the pulls are over.</summary>
        /// <returns>True when the junkyard is quiet.</returns>
        public override bool IsDone() => AllWentOff(2);
    }
}
