// Batshit Technology - in-game smoke checks for how hostiles react to an
// armed Tractor Bomb (docs/TRACTOR-BOMB.md, "Decisions" 9). Part of the
// -btsmoketest runner (SmokeTest.cs): in a seeded run, fifteen raiders in
// sight of a bomb split three ways, each as its roll says (flee at once,
// hesitate about two seconds, ignore it), and no roll ever changes; a
// colonist among them bails at once without rolling.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The runner's hostile-reaction scenario.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>How many raiders face the bomb.</summary>
        private const int ReactionHostiles = 15;

        /// <summary>
        /// A seeded run. Fifteen unarmed raiders, awake and free to move,
        /// stand 9 to 11 tiles round a bomb (inside its flee reach,
        /// outside its blast), with a fresh colonist among them. The bomb's
        /// seed is chosen so their rolls split at least four ways each,
        /// worked out here from Core's roll before the bomb is armed. Then
        /// over its five-second countdown each raider must notice it once,
        /// keep exactly the roll expected, and act on it: those that flee
        /// run on the very think they notice it; those that hesitate run
        /// two seconds (to half a second more) after; those that ignore it
        /// never run or freeze. The colonist runs at once and never rolls.
        /// A raider that falls into a mental state or down is left out.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombHostileReactions(Map map, IntVec3 center)
        {
            var mark = ErrorMark();
            ClearTractorStage(map, center);
            Building_TractorBomb bomb = PlantTestBomb(map, center, 5f);
            Faction enemy = Find.FactionManager.RandomEnemyFaction(
                allowNonHumanlike: false);
            var hostiles = new List<Pawn>();
            for (int i = 0; i < ReactionHostiles && enemy != null; i++)
            {
                IntVec3 at = center + (Vector3Utility.FromAngleFlat(
                    i * 360f / ReactionHostiles) * (9f + (i % 3))).ToIntVec3();
                hostiles.Add(FreshHostile(map, at, enemy));
            }

            Pawn bystander = FreshColonist(map, center + new IntVec3(1, 0, -9));
            int seed = SeedForSplit(bomb, hostiles, 4);
            bomb.SeedHostileRolls(seed);
            var expected = hostiles.ToDictionary(
                h => h, h => TractorBombHostiles.Roll(
                    h.thingIDNumber, bomb.thingIDNumber, seed));
            var noticed =
                new Dictionary<Pawn, (TractorBombHostileReaction, int)>();
            var reactedAt = new Dictionary<Pawn, int>();
            var dropped = new HashSet<Pawn>();
            bool changed = false;
            int bystanderAt = -1;
            int armedAt = Find.TickManager.TicksGame;
            bomb.StartCountdown();
            for (int i = 0; i < 600 && bomb.Spawned
                && bomb.CurrentStage != Building_TractorBomb.Stage.Imploding;
                i++)
            {
                Tick(1);
                int now = Find.TickManager.TicksGame;
                foreach (Pawn h in hostiles)
                {
                    if (!h.Spawned || h.Dead || h.Downed || h.InMentalState)
                    {
                        dropped.Add(h);
                        continue;
                    }

                    if (bomb.TryGetNotice(
                        h, out TractorBombHostileReaction r, out int seen))
                    {
                        if (!noticed.TryGetValue(h, out var first))
                        {
                            noticed[h] = (r, seen);
                        }
                        else if (first != (r, seen))
                        {
                            changed = true;
                        }
                    }

                    if (!reactedAt.ContainsKey(h) && RunningOrFrozen(h))
                    {
                        reactedAt[h] = now;
                    }
                }

                if (bystanderAt < 0 && RunningOrFrozen(bystander))
                {
                    bystanderAt = now - armedAt;
                }
            }

            var misfits = new List<string>();
            var split = new Dictionary<TractorBombHostileReaction, int>();
            foreach (Pawn h in hostiles.Where(h => !dropped.Contains(h)))
            {
                TractorBombHostileReaction want = expected[h];
                bool seen = noticed.TryGetValue(h, out var notice);
                bool reacted = reactedAt.TryGetValue(h, out int at);
                int delay = seen && reacted ? at - notice.Item2 : -1;
                bool fits = seen && notice.Item1 == want && (want switch
                {
                    TractorBombHostileReaction.Flee =>
                        reacted && delay >= 0 && delay <= 1,
                    TractorBombHostileReaction.Hesitate =>
                        reacted && delay >= TractorBombHostiles.HesitationTicks
                        && delay <= TractorBombHostiles.HesitationTicks + 31,
                    _ => !reacted,
                });
                if (fits)
                {
                    split[want] = split.TryGetValue(want, out int n) ? n + 1 : 1;
                }
                else
                {
                    misfits.Add($"{h.LabelShort}: rolled {want}, noticed "
                        + $"{(seen ? notice.Item1.ToString() : "never")}, "
                        + $"reacted {(reacted ? delay + " ticks after" : "never")}");
                }
            }

            string counts = string.Join(", ", split.Select(
                kv => $"{kv.Key} {kv.Value}"));
            Log.Message($"[BT-SMOKE] NOTE seed {seed}: {counts}; "
                + $"{dropped.Count} left out; colonist ran "
                + $"{bystanderAt} ticks after arming");
            bool threeWays = split.Count == 3 && split.Values.All(n => n >= 2);
            Check("hostiles split three ways: flee at once, hesitate 2 s, or "
                    + "ignore the bomb",
                enemy != null && seed >= 0 && misfits.Count == 0
                    && threeWays && dropped.Count <= 3,
                $"seed {seed}, split [{counts}], {dropped.Count} left out; "
                    + $"misfits: {string.Join("; ", misfits)}");
            Check("a hostile's reaction is rolled once and never changes; a "
                    + "colonist bails at once without rolling",
                enemy != null && !changed
                    && noticed.Count == hostiles.Count - dropped.Count
                    && bomb.HostileRollCount == noticed.Count + dropped
                        .Count(h => bomb.TryGetNotice(h, out _, out _))
                    && bystanderAt >= 0 && bystanderAt <= 31
                    && !bomb.TryGetNotice(bystander, out _, out _),
                $"changed {changed}, noticed {noticed.Count} of "
                    + $"{hostiles.Count - dropped.Count}, rolls kept "
                    + $"{bomb.HostileRollCount}; colonist ran "
                    + $"{bystanderAt} ticks after arming, rolled "
                    + $"{bomb.TryGetNotice(bystander, out _, out _)}");

            for (int i = 0; i < 1500 && bomb.Spawned; i++)
            {
                Tick(1);
            }

            Tick(30);
            RemoveTestPawns(hostiles.Append(bystander).ToArray());
            Check("hostile reactions log no errors", !NewErrorSince(mark),
                "an error was logged (see the log)");
        }

        /// <summary>
        /// The first seed, from 1, that gives the raiders' rolls for a bomb
        /// at least so many of each reaction: the "seeded run".
        /// </summary>
        /// <param name="bomb">The bomb.</param>
        /// <param name="hostiles">The raiders.</param>
        /// <param name="atLeast">The fewest of each reaction.</param>
        /// <returns>The seed, or -1 if none in 10,000 does.</returns>
        private static int SeedForSplit(
            Building_TractorBomb bomb, List<Pawn> hostiles, int atLeast)
        {
            for (int seed = 1; seed <= 10000 && hostiles.Count > 0; seed++)
            {
                var counts = new int[3];
                foreach (Pawn h in hostiles)
                {
                    counts[(int)TractorBombHostiles.Roll(
                        h.thingIDNumber, bomb.thingIDNumber, seed)]++;
                }

                if (counts.All(n => n >= atLeast))
                {
                    return seed;
                }
            }

            return -1;
        }

        /// <summary>
        /// A brand-new raider of a hostile faction, unarmed, awake, fed,
        /// healthy and free to move, exactly on a spot.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="at">Where to put it.</param>
        /// <param name="enemy">Its faction.</param>
        /// <returns>The raider, spawned.</returns>
        private static Pawn FreshHostile(Map map, IntVec3 at, Faction enemy)
        {
            Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Drifter, enemy, forceGenerateNewPawn: true,
                dontGiveWeapon: true));
            GenSpawn.Spawn(p, at, map);
            TractorStaging.Refresh(p);
            TractorStaging.Heal(p);
            return p;
        }
    }
}
