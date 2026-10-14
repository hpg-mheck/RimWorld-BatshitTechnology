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
            CellRect reach = yard.ExpandedBy(TourStagePlanner.TractorReach);
            Showcase.MoveColonistsOut(Map, c => reach.Contains(c));
            explosionsAtStart = Building_TractorBomb.Explosions;
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
    /// The robot spots them, protests at a colonist standing in its pull
    /// (who is sent away), digs in, and goes off.
    /// </summary>
    public class TractorBombScene : TractorYardScene
    {
        /// <summary>When the barn's beat plays, in ticks.</summary>
        public int barnTick = FirstBeatTick;

        /// <summary>When the robot rolls out of the barn, in ticks.</summary>
        public int deliverTick = 480;

        /// <summary>How far north the stray is sent, in tiles.</summary>
        public int strayRetreat = 30;

        /// <summary>The barn.</summary>
        [Unsaved]
        private Thing barn;

        /// <summary>The robot.</summary>
        [Unsaved]
        private Pawn robot;

        /// <summary>The colonist standing in the robot's way, or null.</summary>
        [Unsaved]
        private Pawn stray;

        /// <summary>Whether the stray has been sent away.</summary>
        [Unsaved]
        private bool straySent;

        /// <summary>The raiders.</summary>
        [Unsaved]
        private List<Pawn> party;

        /// <summary>The bomb, once emplaced.</summary>
        [Unsaved]
        private List<Building_TractorBomb> bombs;

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
            bombs = new List<Building_TractorBomb>();
            barn = TractorStaging.SpawnBarn(
                Map, TractorStaging.At(Centre, SingleTractorLayout.Barn));
            (barn as Building_TractorBarn)?.SetSettings(
                TourSettings(TractorBombApproach.Default));
            party = TractorStaging.SpawnArmy(
                Map, Centre, SingleTractorLayout.Party, armed: false,
                stunned: true);
            stray = PlaceStray();
        }

        /// <summary>Plays the scene's beats as the world reaches them.</summary>
        public override void Tick()
        {
            if (Elapsed >= barnTick)
            {
                BeatOnce("barn", Shot(30f));
            }

            if (robot == null && Elapsed >= deliverTick)
            {
                robot = TractorStaging.SpawnRobot(
                    Map, TractorStaging.At(Centre, SingleTractorLayout.BarnDoor));
                Fit(robot, TractorBombApproach.Default);
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
                BeatOnce("pull", Shot(45f), select: false);
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
        /// Stands the stray down however the scene ended, and says what
        /// the yard held if the bomb never went off.
        /// </summary>
        /// <param name="ending">How it ended.</param>
        public override void End(SceneEnd ending)
        {
            if (Alive(stray) && stray.drafter != null)
            {
                stray.drafter.FireAtWill = true;
                stray.drafter.Drafted = false;
            }

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
        /// Puts a free colonist, drafted so it stays put and holding its
        /// fire, where the robot will find it in its pull (Core's layout
        /// says where). A colonist with nothing to do is chosen first;
        /// failing that, the one in the Brain Squeezer, then the grenadier
        /// (holding fire, it throws nothing): when the grenade's volunteer
        /// is lost, no one else may be left, and the robot then had no one
        /// to protest about. The choice is logged.
        /// </summary>
        /// <returns>The colonist, or null if none is free.</returns>
        private Pawn PlaceStray()
        {
            Pawn pawn = Map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Dead && !p.Downed && !p.InMentalState
                    && p.drafter != null)
                .OrderBy(StrayRank)
                .FirstOrDefault();
            IntVec3 cell = TractorStaging.At(Centre, SingleTractorLayout.Stray);
            if (pawn == null || !cell.InBounds(Map) || !cell.Standable(Map))
            {
                Log.Message($"{ShowcaseLog.Prefix} tractor: no stray ("
                    + (pawn == null ? "no free colonist" : $"{cell} is no "
                        + "place to stand") + "), so no protest");
                return null;
            }

            string why = StrayRank(pawn) switch
            {
                0 => "free",
                1 => "from the Brain Squeezer",
                _ => "the grenadier",
            };
            Log.Message($"{ShowcaseLog.Prefix} tractor: stray "
                + $"{pawn.LabelShort} ({why}) at {cell}");

            pawn.jobs?.StopAll();
            pawn.Position = cell;
            pawn.Notify_Teleported();
            // Drafted to hold the spot, but never firing: a drafted
            // colonist shoots (or throws) at the raiders by itself, and
            // the second showcase run lost its robot before it emplaced.
            pawn.drafter.Drafted = true;
            pawn.drafter.FireAtWill = false;
            return pawn;
        }

        /// <summary>
        /// How fit a colonist is to be the stray, best first: 0 for one
        /// with nothing to do, 1 for the one in the Brain Squeezer, 2 for
        /// the grenadier.
        /// </summary>
        /// <param name="p">The colonist.</param>
        /// <returns>The rank.</returns>
        private static int StrayRank(Pawn p) =>
            p.equipment?.Primary?.def.defName == Showcase.GrenadeDef ? 2
            : p.CurJobDef == BT_BrainSqueezerDefOf.BT_UseBrainSqueezer ? 1
            : 0;

        /// <summary>
        /// Walks the stray out of the robot's reach: north, out of the
        /// yard, or to the base if nowhere there will do.
        /// </summary>
        private void SendStrayAway()
        {
            if (straySent || !Alive(stray) || stray.Dead || stray.Downed)
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

            stray.jobs?.TryTakeOrderedJob(
                JobMaker.MakeJob(JobDefOf.Goto, cell), JobTag.Misc);
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

        /// <summary>The raiders.</summary>
        [Unsaved]
        private List<Pawn> army;

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
                BeatOnce("pull", Shot(34f), select: false);
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
