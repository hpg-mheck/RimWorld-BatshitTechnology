// Batshit Technology - in-game smoke checks for twin Tractor Bombs
// (ROADMAP.md, step 11's "twin bombs" and "Multi-bomb physics"). Part of
// the -btsmoketest runner (SmokeTest.cs). In a junkyard of slag, rock
// chunks, loose stacks and crates of items, fourteen armed raiders hold
// position while two robots drive in from opposite sides and plant
// themselves a few tiles off the army, so their fields overlap; both pull
// at once. Every caught thing feels both pulls: nothing may be lost or
// shoved, nothing may move against the net pull, a pawn any pull holds
// stays held, things in the overlap must end in a pile or at a balance
// point, both bombs must go off, and neither pull may run past its eight
// seconds; a pawn held between the two strong pulls must be stretched
// (and none that never was). Then two bombs planted four tiles apart, both
// pulling: one is
// hit, goes off, and its blast sets off the other, with no pull outliving
// its bomb. The hostiles roll how they react, as always (a third flee, a
// third hesitate, a third ignore), so the army partly scatters; the rolls
// and the jitter are seeded.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace BatshitTechnology
{
    /// <summary>The runner's twin-bomb scenarios.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// The seed for the twin scenarios: RimWorld's random state while
        /// they run, and both bombs' hostile rolls.
        /// </summary>
        private const int TwinSeed = 20261013;

        /// <summary>
        /// How long the twin pulls may take, in ticks, at most.
        /// </summary>
        private const int TwinWatchTicks = 900;

        /// <summary>
        /// How near a bomb's centre a thing counts as in its pile: its
        /// 1.5-tile gather radius and the ring where overflow waits.
        /// </summary>
        private const float TwinPileRadius = 4f;

        /// <summary>
        /// How small a net pull counts as a balance point, as a fraction of
        /// the pulls it sums: well beyond what the bias jitter tips it by.
        /// </summary>
        private const double TwinBalance = 0.1;

        /// <summary>
        /// How recently, in ticks, a thing in the overlap must have been at
        /// rest when the first bomb goes off (a thing stepping into a cell
        /// just freed is moving for a moment).
        /// </summary>
        private const int TwinRestWindow = 15;

        /// <summary>
        /// The twin bombs, then the superposed chain reaction, all seeded,
        /// with no error logged.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombTwins(Map map, IntVec3 center)
        {
            var mark = ErrorMark();
            Rand.PushState(TwinSeed);
            try
            {
                TestTractorBombTwinJunkyard(map, center);
                TestTractorBombTwinChain(map, center);
            }
            finally
            {
                Rand.PopState();
            }

            Check("twin Tractor Bombs log no errors", !NewErrorSince(mark),
                "an error was logged (see the log)");
        }

        /// <summary>
        /// The junkyard: two robots, Nosy so they plant three tiles off the
        /// army, drive in from 28 tiles either side of it (the raiders are
        /// stunned until both have planted, so the robots arrive whole).
        /// The countdowns are held long while they come; once both are
        /// armed, the raiders are let go for two and a half seconds to
        /// react as they rolled, and then both pulls start on the same
        /// tick, at the same strength, so the overlap is a true tug-of-war.
        /// Every tick of the pulls is watched; see the checks.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombTwinJunkyard(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            HashSet<ThingDef> junkDefs = TractorStaging.BuildJunkyard(map, center);
            List<Pawn> army = TractorStaging.SpawnArmy(
                map, center, TwinJunkyardLayout.Army, armed: true,
                stunned: true);
            int explosions = Building_TractorBomb.Explosions;
            List<Building_TractorBomb> bombs = DriveInTwinBombs(map, center);
            if (bombs.Count < 2 || army.Count < 12)
            {
                Check("twin Tractor Bombs both go off", false,
                    $"{bombs.Count} of 2 robots planted; army {army.Count}");
                bombs.ForEach(b => b.Destroy(DestroyMode.Vanish));
                RemoveTestPawns(army.ToArray());
                return;
            }

            for (int i = 0; i < 120 && bombs.Any(b => b.Spawned
                && b.CurrentStage != Building_TractorBomb.Stage.CountingDown);
                i++)
            {
                Tick(1);
            }

            foreach (Pawn p in army.Where(p => p.Spawned))
            {
                p.stances.stunner.StopStun();
            }

            Tick(150);
            bombs.ForEach(b => b.ImplodeNow());
            var watch = new TwinWatch(map, bombs, junkDefs);
            for (int i = 0; i < TwinWatchTicks && bombs.Any(b => b.Spawned);
                i++)
            {
                watch.Step();
            }

            Tick(30);
            ReportTwins(watch, bombs, army, explosions);
            RemoveTestPawns(army.ToArray());
        }

        /// <summary>
        /// Logs the twin bombs' story and checks it, from what the watch
        /// saw.
        /// </summary>
        /// <param name="watch">What was seen.</param>
        /// <param name="bombs">The two bombs.</param>
        /// <param name="army">The raiders.</param>
        /// <param name="explosions">Explosions before the scenario.</param>
        private void ReportTwins(
            TwinWatch watch, List<Building_TractorBomb> bombs,
            List<Pawn> army, int explosions)
        {
            Building_TractorBomb west = bombs[0];
            Building_TractorBomb east = bombs[1];
            var rolls = new Dictionary<TractorBombHostileReaction, int>();
            foreach (Pawn p in army)
            {
                foreach (Building_TractorBomb b in bombs)
                {
                    if (b.TryGetNotice(p, out var r, out _))
                    {
                        rolls[r] =
                            rolls.TryGetValue(r, out int n) ? n + 1 : 1;
                    }
                }
            }

            Log.Message("[BT-SMOKE] NOTE twin bombs planted at "
                + $"{watch.WestAt} and {watch.EastAt}, "
                + $"{watch.WestAt.DistanceTo(watch.EastAt):F1} apart; "
                + "hostile rolls "
                + string.Join(
                    ", ", rolls.Select(kv => $"{kv.Key} {kv.Value}"))
                + $"; pulls ran {watch.RunOf(west)} and {watch.RunOf(east)} "
                + $"ticks, caught {watch.CaughtOf(west)} and "
                + $"{watch.CaughtOf(east)}; judged {watch.Judged} moves "
                + $"({watch.SuperposedJudged} under both pulls); overlap "
                + $"{watch.Overlap} things: {watch.EndPiled} piled, "
                + $"{watch.EndBalanced} balanced, "
                + $"{watch.EndBlocked} blocked, "
                + $"{watch.EndTipped} tipped out, {watch.EndGone} merged or "
                + $"gone; superposed things this session "
                + $"{MapComponent_Implosions.Superposed}");

            bool bothOff = west.Destroyed && east.Destroyed
                && Building_TractorBomb.Explosions >= explosions + 2;
            Check("twin Tractor Bombs both go off (chain reaction allowed)",
                bothOff,
                $"west destroyed {west.Destroyed}, east destroyed "
                    + $"{east.Destroyed}, explosions "
                    + $"+{Building_TractorBomb.Explosions - explosions}");
            Check("twin implosions keep to their 2 s minimum and 8 s maximum",
                bothOff && watch.Timings.Count == 0,
                $"runs {watch.RunOf(west)} and {watch.RunOf(east)} ticks: "
                    + string.Join("; ", watch.Timings));
            Check("nothing is lost in the twin pulls",
                watch.Losses.Count == 0 && watch.Watched > 0,
                $"watched {watch.Watched} ticks: "
                    + string.Join("; ", watch.Losses.Take(6)));
            Check("nothing moves against the net pull of twin bombs",
                watch.Against.Count == 0 && watch.Judged >= 50
                    && watch.SuperposedJudged >= 20,
                $"{watch.Against.Count} of {watch.Judged} moves against it "
                    + $"({watch.SuperposedJudged} under both pulls): "
                    + string.Join("; ", watch.Against.Take(6)));
            Check("a pawn any twin pull holds stays held",
                watch.Unheld.Count == 0 && watch.HeldChecks >= 100,
                $"{watch.Unheld.Count} lapses in {watch.HeldChecks} checks: "
                    + string.Join("; ", watch.Unheld.Take(6)));
            var unstretched = new List<string>();
            int stretched = 0;
            foreach (Pawn p in army)
            {
                int marks = p.health?.hediffSet?.hediffs.Count(h =>
                    h.def == BT_ImplosionDefOf.BT_StretchMark) ?? 0;
                int strained = watch.StrainedTicks(p);
                stretched += marks > 0 ? 1 : 0;
                if (strained >= 120 && marks == 0)
                {
                    unstretched.Add($"{p.LabelShort} strained {strained} "
                        + "ticks, no stretch mark");
                }
                else if (marks > 0 && !watch.EverNearStrain(p))
                {
                    unstretched.Add($"{p.LabelShort} has {marks} stretch "
                        + "marks, never strained");
                }
            }

            Log.Message($"[BT-SMOKE] NOTE twin bombs stretched {stretched} "
                + $"of {army.Count} raiders; stretches this session "
                + $"{MapComponent_Implosions.Stretches}");
            Check("a pawn held between strong opposing pulls is stretched, "
                    + "and only then",
                unstretched.Count == 0 && stretched >= 1,
                $"{stretched} stretched: "
                    + string.Join("; ", unstretched.Take(6)));
            Check("things in the twin overlap end in a pile or at a balance "
                    + "point",
                watch.Overlap >= 5 && watch.Stranded.Count == 0
                    && watch.EndBalanced >= 1,
                $"{watch.Overlap} in the overlap, {watch.EndBalanced} at a "
                    + $"balance point; stranded: "
                    + string.Join("; ", watch.Stranded.Take(6)));
        }

        /// <summary>
        /// The superposed chain reaction: two bombs four tiles apart (each
        /// inside the other's blast) both pulling steel off the ground.
        /// One is hit and must go off within two ticks; its pull must be
        /// closed and gone from the map's drag at the next tick; its blast
        /// must set the other off while that one pulls; and once both are
        /// gone no pull may still run.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombTwinChain(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            Building_TractorBomb first =
                PlantTestBomb(map, center + new IntVec3(-2, 0, 0), 30f);
            Building_TractorBomb second =
                PlantTestBomb(map, center + new IntVec3(2, 0, 0), 30f);
            foreach (IntVec3 offset in new[]
            {
                new IntVec3(0, 0, 8), new IntVec3(0, 0, -8),
                new IntVec3(-9, 0, 3), new IntVec3(9, 0, -3),
            })
            {
                Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                steel.stackCount = 25;
                GenSpawn.Spawn(steel, center + offset, map);
            }

            first.StartCountdown();
            second.StartCountdown();
            first.ImplodeNow();
            second.ImplodeNow();
            Tick(60);
            MapComponent_Implosions field = MapComponent_Implosions.For(map);
            Implosion pullA = first.Implosion;
            Implosion pullB = second.Implosion;
            bool bothPulling = pullA != null && pullB != null
                && field.Active.Contains(pullA)
                && field.Active.Contains(pullB);
            int hitAt = Find.TickManager.TicksGame;
            first.Hit();
            for (int i = 0; i < 5 && first.Spawned; i++)
            {
                Tick(1);
            }

            int firstOff = first.Spawned ? -1 : Building_TractorBomb
                .LastExplosionTick - hitAt;
            bool closed = pullA?.Closed == true;
            Tick(1);
            bool dropped = second.Spawned && pullB != null
                && !pullB.Closed && field.Active.Contains(pullB)
                && !field.Active.Contains(pullA);
            for (int i = 0; i < 60 && second.Spawned; i++)
            {
                Tick(1);
            }

            bool chained = second.Destroyed
                && second.LastHitStage == Building_TractorBomb.Stage.Imploding
                && second.LastHitTick >= hitAt;
            Tick(2);
            bool noneLeft = field.Active.All(i => i.Closed)
                && pullB?.Closed == true;
            Check("a superposed bomb's blast sets off its neighbour and no "
                    + "pull outlives its bomb",
                bothPulling && firstOff >= 0 && firstOff <= 2 && closed
                    && dropped && chained && noneLeft,
                $"both pulling {bothPulling}; first off {firstOff} ticks "
                    + $"after its hit, its pull closed {closed}, dropped "
                    + $"from the drag {dropped}; second hit while pulling "
                    + $"and gone {chained} ({second.LastHitStage}); no pull "
                    + $"left {noneLeft}");
            Tick(30);
        }

        /// <summary>
        /// Two Nosy robots drive in from either side of the army and plant
        /// themselves; their countdowns are held at half a minute meanwhile
        /// (the def's range is restored at once), and each bomb's hostile
        /// rolls are seeded the moment it appears, before any raider can
        /// see it.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the army.</param>
        /// <returns>
        /// The planted bombs, west first; fewer if one failed.
        /// </returns>
        private static List<Building_TractorBomb> DriveInTwinBombs(
            Map map, IntVec3 center)
        {
            TractorBombExtension settings = BT_TractorBombDefOf
                .BT_TractorBombEmplaced
                .GetModExtension<TractorBombExtension>();
            FloatRange rolled = settings?.countdownSeconds ?? default;
            var robots = new List<Pawn>();
            var bombs = new List<Building_TractorBomb>();
            int started = Find.TickManager.TicksGame;
            try
            {
                if (settings != null)
                {
                    settings.countdownSeconds = new FloatRange(30f, 30f);
                }

                foreach (Cell offset in TwinJunkyardLayout.DriveIn)
                {
                    Pawn robot = TractorStaging.SpawnRobot(
                        map, TractorStaging.At(center, offset));
                    robot.TryGetComp<CompTractorBomb>().Disposition =
                        TractorBombDisposition.Nosy;
                    robots.Add(robot);
                }

                for (int i = 0; i < 1500 && bombs.Count < 2; i++)
                {
                    Tick(1);
                    foreach (Building_TractorBomb b in map.listerThings
                        .ThingsOfDef(
                            BT_TractorBombDefOf.BT_TractorBombEmplaced)
                        .OfType<Building_TractorBomb>())
                    {
                        if (b.Spawned && b.EmplacedTick >= started
                            && !bombs.Contains(b))
                        {
                            b.SeedHostileRolls(TwinSeed);
                            bombs.Add(b);
                        }
                    }
                }
            }
            finally
            {
                if (settings != null)
                {
                    settings.countdownSeconds = rolled;
                }

                RemoveTestPawns(robots.ToArray());
            }

            return bombs.OrderBy(b => b.Position.x).ToList();
        }

        /// <summary>
        /// Watches twin pulls tick by tick: every drag move judged against
        /// the net pull, every held pawn checked, the junk's totals
        /// counted, each bomb's run timed, and what was in the overlap
        /// classified when the first bomb goes off.
        /// </summary>
        private sealed class TwinWatch
        {
            private readonly Map map;
            private readonly List<Building_TractorBomb> bombs;
            private readonly HashSet<ThingDef> junkDefs;
            private readonly Dictionary<Building_TractorBomb, int> started =
                new Dictionary<Building_TractorBomb, int>();
            private readonly Dictionary<Building_TractorBomb, int> ended =
                new Dictionary<Building_TractorBomb, int>();
            private readonly Dictionary<Building_TractorBomb, int> caught =
                new Dictionary<Building_TractorBomb, int>();
            private readonly Dictionary<Thing, (int Tick, string Why)>
                restedAt = new Dictionary<Thing, (int Tick, string Why)>();
            private readonly HashSet<Thing> overlap = new HashSet<Thing>();
            private readonly Dictionary<Pawn, int> strained =
                new Dictionary<Pawn, int>();
            private readonly HashSet<Pawn> nearStrain = new HashSet<Pawn>();
            private Dictionary<ThingDef, int> totals;
            private bool endJudged;

            /// <summary>
            /// Starts watching, as both pulls are about to start.
            /// </summary>
            /// <param name="map">The map.</param>
            /// <param name="bombs">The two bombs, west first.</param>
            /// <param name="junkDefs">The junk's item defs.</param>
            public TwinWatch(
                Map map, List<Building_TractorBomb> bombs,
                HashSet<ThingDef> junkDefs)
            {
                this.map = map;
                this.bombs = bombs;
                this.junkDefs = junkDefs;
                WestAt = bombs[0].Position;
                EastAt = bombs[1].Position;
                totals = Totals();
            }

            /// <summary>Where the west bomb planted.</summary>
            public IntVec3 WestAt { get; }

            /// <summary>Where the east bomb planted.</summary>
            public IntVec3 EastAt { get; }

            /// <summary>Ticks watched.</summary>
            public int Watched { get; private set; }

            /// <summary>Drag moves judged against the net pull.</summary>
            public int Judged { get; private set; }

            /// <summary>Of those, moves made under both pulls.</summary>
            public int SuperposedJudged { get; private set; }

            /// <summary>Moves against the net pull, described.</summary>
            public List<string> Against { get; } = new List<string>();

            /// <summary>Held pawns checked, a pawn a tick.</summary>
            public int HeldChecks { get; private set; }

            /// <summary>Held pawns found free, described.</summary>
            public List<string> Unheld { get; } = new List<string>();

            /// <summary>Ticks the junk's totals fell with no blast.</summary>
            public List<string> Losses { get; } = new List<string>();

            /// <summary>
            /// Bombs whose pull broke its limits, described.
            /// </summary>
            public List<string> Timings { get; } = new List<string>();

            /// <summary>Overlap things not at rest at the end.</summary>
            public List<string> Stranded { get; } = new List<string>();

            /// <summary>Things that were ever under both pulls.</summary>
            public int Overlap => overlap.Count;

            /// <summary>Overlap things that ended in a pile.</summary>
            public int EndPiled { get; private set; }

            /// <summary>
            /// Overlap things that ended at a balance point.
            /// </summary>
            public int EndBalanced { get; private set; }

            /// <summary>Overlap things that ended blocked.</summary>
            public int EndBlocked { get; private set; }

            /// <summary>
            /// Overlap things that tipped off the balance to one bomb.
            /// </summary>
            public int EndTipped { get; private set; }

            /// <summary>Overlap things merged into stacks or gone.</summary>
            public int EndGone { get; private set; }

            /// <summary>
            /// How long a bomb's pull ran, in ticks, or -1.
            /// </summary>
            /// <param name="b">The bomb.</param>
            /// <returns>The ticks.</returns>
            public int RunOf(Building_TractorBomb b) =>
                started.TryGetValue(b, out int s)
                    && ended.TryGetValue(b, out int e)
                    ? e - s
                    : -1;

            /// <summary>
            /// Ticks a pawn spent under both pulls straining hard enough
            /// to stretch it.
            /// </summary>
            /// <param name="p">The pawn.</param>
            /// <returns>The ticks.</returns>
            public int StrainedTicks(Pawn p) =>
                strained.TryGetValue(p, out int n) ? n : 0;

            /// <summary>
            /// Whether a pawn was ever under pulls straining nearly hard
            /// enough to stretch it.
            /// </summary>
            /// <param name="p">The pawn.</param>
            /// <returns>True if it was.</returns>
            public bool EverNearStrain(Pawn p) => nearStrain.Contains(p);

            /// <summary>How many pawns a bomb's pull caught.</summary>
            /// <param name="b">The bomb.</param>
            /// <returns>The count.</returns>
            public int CaughtOf(Building_TractorBomb b) =>
                caught.TryGetValue(b, out int n) ? n : 0;

            /// <summary>
            /// One tick, watched: what each running pull reached before it,
            /// and what moved, merged, vanished or broke loose during it.
            /// </summary>
            public void Step()
            {
                int now = Find.TickManager.TicksGame;
                List<Implosion> before = Running();
                var rateBefore =
                    before.ToDictionary(i => i, i => i.CurrentRate);
                var elapsedBefore =
                    before.ToDictionary(i => i, i => i.ElapsedTicks);
                var where = new Dictionary<Thing, IntVec3>();
                var held = new HashSet<Pawn>();
                foreach (Thing t in Reached(before))
                {
                    where[t] = t.Position;
                    if (t is Pawn p && p.stances?.stunner?.Stunned == true)
                    {
                        held.Add(p);
                    }
                }

                CheckHeld(before, where.Keys.OfType<Pawn>(), elapsedBefore);
                bool blastBefore = Blasting();
                Tick(1);
                Watched++;
                List<Implosion> after = Running();
                NoteStartsAndEnds();
                bool stable = before.Count > 0
                    && before.SequenceEqual(after)
                    && after.All(i => elapsedBefore[i] >= 1
                        && i.ElapsedTicks == elapsedBefore[i] + 1);
                if (stable)
                {
                    JudgeMoves(before, where, held, rateBefore);
                }

                CheckTotals(blastBefore || Blasting());
                if (before.Count >= 2 && stable)
                {
                    TrackOverlap(after, where.Keys, now + 1);
                }

                if (!endJudged && before.Count >= 2 && after.Count < 2)
                {
                    JudgeEnd(before, now);
                }
            }

            /// <summary>
            /// The bombs' pulls running now, in the bombs' order.
            /// </summary>
            /// <returns>The implosions.</returns>
            private List<Implosion> Running() =>
                bombs.Where(b => b.Spawned
                        && b.CurrentStage
                            == Building_TractorBomb.Stage.Imploding
                        && b.Implosion != null && !b.Implosion.Closed)
                    .Select(b => b.Implosion)
                    .ToList();

            /// <summary>
            /// Every pawn and item within reach of any of these pulls.
            /// </summary>
            /// <param name="pulls">The pulls.</param>
            /// <returns>The things, each once.</returns>
            private List<Thing> Reached(List<Implosion> pulls)
            {
                var found = new HashSet<Thing>();
                foreach (Implosion i in pulls)
                {
                    foreach (Thing t in GenRadial.RadialDistinctThingsAround(
                        i.Center, map, i.PullRadius, true))
                    {
                        if (MapComponent_Implosions.Pullable(t))
                        {
                            found.Add(t);
                        }
                    }
                }

                return found.ToList();
            }

            /// <summary>Whether a blast is spreading on the map.</summary>
            /// <returns>True if one is.</returns>
            private bool Blasting() =>
                map.listerThings.ThingsOfDef(ThingDefOf.Explosion).Count > 0;

            /// <summary>
            /// Notes when each bomb's pull started and when it went off,
            /// and how many pawns it had caught, and checks the run against
            /// the pull's minimum and maximum.
            /// </summary>
            private void NoteStartsAndEnds()
            {
                int now = Find.TickManager.TicksGame;
                foreach (Building_TractorBomb b in bombs)
                {
                    if (!started.ContainsKey(b)
                        && b.ImplosionStartedTick >= 0)
                    {
                        started[b] = b.ImplosionStartedTick;
                    }

                    if (b.Implosion != null)
                    {
                        caught[b] = b.Implosion.CaughtCount;
                    }

                    if (!ended.ContainsKey(b) && !b.Spawned)
                    {
                        ended[b] = now;
                        int run = RunOf(b);
                        bool hit = b.LastHitStage
                                == Building_TractorBomb.Stage.Imploding
                            && started.TryGetValue(b, out int s)
                            && b.LastHitTick >= s;
                        int min = b.Implosion?.MinDurationTicks ?? 0;
                        int max = b.Implosion?.MaxDurationTicks ?? 0;
                        if (run < 0
                            || run > TractorBombTiming.MaxImplosionTicks
                            || max != TractorBombTiming.MaxImplosionTicks
                            || (!hit && run < min))
                        {
                            Timings.Add($"{b.Position}: ran {run} ticks "
                                + $"(limits {min} to {max}, hit {hit})");
                        }
                    }
                }
            }

            /// <summary>
            /// Every caught pawn within reach of a pull that caught it must
            /// be stunned, once that pull has run long enough to seize.
            /// </summary>
            /// <param name="pulls">The pulls running.</param>
            /// <param name="pawns">The pawns within reach.</param>
            /// <param name="elapsed">Each pull's ticks run.</param>
            private void CheckHeld(
                List<Implosion> pulls, IEnumerable<Pawn> pawns,
                Dictionary<Implosion, int> elapsed)
            {
                foreach (Pawn p in pawns)
                {
                    bool holding = pulls.Any(i => elapsed[i] >= 2
                        && p.Position.InHorDistOf(i.Center, i.PullRadius)
                        && i.Caught.Contains(p));
                    if (!holding || p.Dead || !p.Spawned)
                    {
                        continue;
                    }

                    HeldChecks++;
                    if (p.stances?.stunner?.Stunned != true)
                    {
                        Unheld.Add($"{p.LabelShort} at {p.Position}");
                    }
                }
            }

            /// <summary>
            /// Judges every thing that moved during a tick of steady pulls:
            /// a step of more than one cell is a shove; under one pull a
            /// step must take it strictly closer; under several it may not
            /// raise their potential by more than its jitter allows. Pawns
            /// that were not held (walking in) are not the pull's doing.
            /// </summary>
            /// <param name="pulls">The pulls running.</param>
            /// <param name="where">
            /// Where each thing was before the tick.
            /// </param>
            /// <param name="held">The pawns stunned before the tick.</param>
            /// <param name="rateBefore">Each pull's rate before it.</param>
            private void JudgeMoves(
                List<Implosion> pulls, Dictionary<Thing, IntVec3> where,
                HashSet<Pawn> held, Dictionary<Implosion, double> rateBefore)
            {
                foreach (var kv in where)
                {
                    Thing t = kv.Key;
                    IntVec3 from = kv.Value;
                    if (!t.Spawned || t.Position == from
                        || (t is Pawn p && !held.Contains(p)))
                    {
                        continue;
                    }

                    var reaching = pulls.Where(i =>
                        from.InHorDistOf(i.Center, i.PullRadius)).ToList();
                    if (reaching.Count == 0)
                    {
                        continue;
                    }

                    IntVec3 to = t.Position;
                    IntVec3 step = to - from;
                    Judged++;
                    SuperposedJudged += reaching.Count > 1 ? 1 : 0;
                    string what = $"{t.LabelShort} {from}->{to} under "
                        + $"{reaching.Count}";
                    if (Math.Max(Math.Abs(step.x), Math.Abs(step.z)) > 1)
                    {
                        Against.Add(what + " (shoved)");
                    }
                    else if (reaching.Count == 1)
                    {
                        IntVec3 c = reaching[0].Center;
                        if ((to - c).LengthHorizontalSquared
                            >= (from - c).LengthHorizontalSquared)
                        {
                            Against.Add(what + " (not closer)");
                        }
                    }
                    else
                    {
                        // The drag used each pull's rate from before or
                        // after the tick: give it the kinder of the two.
                        double change = 0;
                        double sum = 0;
                        foreach (Implosion i in reaching)
                        {
                            double d =
                                Dist(to, i.Center) - Dist(from, i.Center);
                            double r0 = rateBefore[i];
                            double r1 = i.CurrentRate;
                            change += d * (d > 0 ? Math.Min(r0, r1)
                                : Math.Max(r0, r1));
                            sum += Math.Max(r0, r1);
                        }

                        double allowed = (PullSuperposition.MaxScaleFor(
                                reaching.Count) + 0.01)
                            * sum * Dist(to, from);
                        if (change > allowed)
                        {
                            Against.Add(what + $" (potential +{change:F2}, "
                                + $"jitter allows {allowed:F2})");
                        }
                    }
                }
            }

            /// <summary>
            /// The junk's totals, by def, against the last tick's: any fall
            /// without a blast spreading is a loss.
            /// </summary>
            /// <param name="blasting">
            /// Whether a blast spread before or during the tick.
            /// </param>
            private void CheckTotals(bool blasting)
            {
                Dictionary<ThingDef, int> now = Totals();
                foreach (var kv in totals)
                {
                    int n = now.TryGetValue(kv.Key, out int v) ? v : 0;
                    if (n < kv.Value && !blasting)
                    {
                        Losses.Add($"{kv.Key.defName} {kv.Value}->{n} at "
                            + $"tick {Watched}");
                    }
                }

                totals = now;
            }

            /// <summary>The junk's item totals, map-wide, by def.</summary>
            /// <returns>Each def's total stack count.</returns>
            private Dictionary<ThingDef, int> Totals() =>
                junkDefs.ToDictionary(
                    d => d,
                    d => map.listerThings.ThingsOfDef(d)
                        .Where(t => t.Spawned).Sum(t => t.stackCount));

            /// <summary>
            /// Notes what is under both pulls now, and which of those are
            /// at rest: in a pile, at a balance point, or blocked.
            /// </summary>
            /// <param name="pulls">The pulls running.</param>
            /// <param name="seen">
            /// The things within reach before the tick.
            /// </param>
            /// <param name="now">The tick it is now.</param>
            private void TrackOverlap(
                List<Implosion> pulls, IEnumerable<Thing> seen, int now)
            {
                foreach (Thing t in seen)
                {
                    if (!t.Spawned)
                    {
                        continue;
                    }

                    if (pulls.Count(i => t.Position.InHorDistOf(
                        i.Center, i.PullRadius)) >= 2)
                    {
                        overlap.Add(t);
                    }

                    if (t is Pawn p)
                    {
                        NoteStrain(p, pulls);
                    }

                    string why = overlap.Contains(t) ? Rest(t, pulls) : null;
                    if (why != null)
                    {
                        restedAt[t] = (now, why);
                    }
                }
            }

            /// <summary>
            /// Notes how hard the pulls on a pawn strain against each
            /// other now (Core's PullSuperposition.Tension).
            /// </summary>
            /// <param name="p">The pawn.</param>
            /// <param name="pulls">The pulls running.</param>
            private void NoteStrain(Pawn p, List<Implosion> pulls)
            {
                IntVec3 at = p.Position;
                double tension = PullSuperposition.Tension(at.x, at.z, pulls
                    .Where(i => at.InHorDistOf(i.Center, i.PullRadius))
                    .Select(i => new PullSource(
                        i.Center.x, i.Center.z, i.CurrentRate, i.Seed))
                    .ToList());
                if (tension > PullSuperposition.StretchTension - 1)
                {
                    nearStrain.Add(p);
                }

                if (PullSuperposition.StretchDamage(tension) >= 1)
                {
                    strained[p] = StrainedTicks(p) + 1;
                }
            }

            /// <summary>
            /// When the first bomb goes off: each thing that was ever under
            /// both pulls must be gone (merged), have tipped off to one
            /// bomb's lone pull, or have been at rest within the last few
            /// ticks.
            /// </summary>
            /// <param name="pulls">The pulls that ran until now.</param>
            /// <param name="lastBoth">The last tick both ran.</param>
            private void JudgeEnd(List<Implosion> pulls, int lastBoth)
            {
                endJudged = true;
                foreach (Thing t in overlap)
                {
                    if (!t.Spawned)
                    {
                        EndGone++;
                        continue;
                    }

                    if (pulls.Count(i => t.Position.InHorDistOf(
                        i.Center, i.PullRadius)) < 2)
                    {
                        EndTipped++;
                        continue;
                    }

                    string rest = Rest(t, pulls);
                    if (rest == null && restedAt.TryGetValue(t, out var was)
                        && was.Tick >= lastBoth - TwinRestWindow)
                    {
                        rest = was.Why;
                    }

                    switch (rest)
                    {
                        case "piled":
                            EndPiled++;
                            break;
                        case "balanced":
                            EndBalanced++;
                            break;
                        case "blocked":
                            EndBlocked++;
                            break;
                        default:
                            var (fx, fz) =
                                NetPull(t.Position, pulls, out double sum);
                            Stranded.Add($"{t.LabelShort} at {t.Position}, "
                                + $"net pull ({fx:F1}, {fz:F1}) of {sum:F1}");
                            break;
                    }
                }
            }

            /// <summary>
            /// Why a thing under both pulls is at rest, or null: in a pile
            /// (near a centre), at a balance point (its net pull small), or
            /// blocked (every step clearly downhill is full or walled off).
            /// </summary>
            /// <param name="t">The thing.</param>
            /// <param name="pulls">The pulls running.</param>
            /// <returns>"piled", "balanced", "blocked" or null.</returns>
            private string Rest(Thing t, List<Implosion> pulls)
            {
                IntVec3 at = t.Position;
                if (pulls.Any(i => at.InHorDistOf(i.Center, TwinPileRadius)))
                {
                    return "piled";
                }

                var (fx, fz) = NetPull(at, pulls, out double sum);
                if (Math.Sqrt((fx * fx) + (fz * fz)) <= TwinBalance * sum)
                {
                    return "balanced";
                }

                var reaching = pulls.Where(i =>
                    at.InHorDistOf(i.Center, i.PullRadius)).ToList();
                double slack = PullSuperposition.MaxScaleFor(reaching.Count)
                    + 0.01;
                foreach (IntVec3 d in GenAdj.AdjacentCells)
                {
                    IntVec3 next = at + d;
                    double change = reaching.Sum(i => i.CurrentRate
                        * (Dist(next, i.Center) - Dist(at, i.Center)));
                    if (change < -slack * sum * Dist(next, at)
                        && Takes(next, t))
                    {
                        return null; // could still go downhill
                    }
                }

                return "blocked";
            }

            /// <summary>
            /// Whether a cell could take a thing: one it can stand in, for a
            /// pawn; for an item, also room for it or a stack to merge with.
            /// </summary>
            /// <param name="cell">The cell.</param>
            /// <param name="t">The thing.</param>
            /// <returns>True if it could.</returns>
            private bool Takes(IntVec3 cell, Thing t)
            {
                if (!cell.InBounds(map) || !cell.Walkable(map))
                {
                    return false;
                }

                if (t is Pawn)
                {
                    return true;
                }

                return cell.GetItemCount(map)
                        < cell.GetMaxItemsAllowedInCell(map)
                    || cell.GetThingList(map).Any(o => o != t
                        && o.def.category == ThingCategory.Item
                        && o.CanStackWith(t)
                        && o.stackCount < o.def.stackLimit);
            }

            /// <summary>
            /// The net pull on a cell from the pulls that reach it, with no
            /// jitter, and the sum of their rates.
            /// </summary>
            /// <param name="cell">The cell.</param>
            /// <param name="pulls">The pulls running.</param>
            /// <param name="sum">The reaching pulls' rates, summed.</param>
            /// <returns>The net pull's x and z.</returns>
            private static (double X, double Z) NetPull(
                IntVec3 cell, List<Implosion> pulls, out double sum)
            {
                var reaching = pulls.Where(i =>
                    cell.InHorDistOf(i.Center, i.PullRadius)).ToList();
                sum = reaching.Sum(i => i.CurrentRate);
                return PullSuperposition.NetPull(cell.x, cell.z, reaching
                    .Select(i => new PullSource(
                        i.Center.x, i.Center.z, i.CurrentRate, i.Seed))
                    .ToList());
            }

            /// <summary>The distance between two cells, in tiles.</summary>
            /// <param name="a">One cell.</param>
            /// <param name="b">The other.</param>
            /// <returns>The distance.</returns>
            private static double Dist(IntVec3 a, IntVec3 b) =>
                Math.Sqrt((a - b).LengthHorizontalSquared);
        }
    }
}
