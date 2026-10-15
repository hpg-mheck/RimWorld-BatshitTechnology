// Batshit Technology - in-game smoke checks for shooting a planted Tractor
// Bomb (roadmap, "Decided after the core build": planted bombs are
// damageable). Part of the -btsmoketest runner (SmokeTest.cs): a bullet
// fired at a bomb counting down starts its pull at once; a second bullet,
// fired once it pulls, sets off its blast at once; that blast sets off a
// planted neighbour, which its pull never moved. No hit makes a robot's
// death vortex: that is only for robots wrecked before they plant.

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The runner's planted-bomb damage scenarios.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// A planted bomb, five seconds on its clock, is shot from 28
        /// tiles (beyond its 22.5-tile pull, and the neighbour's) half a
        /// second in: its pull must
        /// start within two ticks of the bullet striking, seconds early.
        /// Steel lies 8 to 9 tiles out
        /// so the pull has work to do; a second bomb stands planted 4
        /// tiles off on a minute's countdown. Once the first one pulls, it
        /// is shot again: it must explode within two ticks, before it has
        /// gathered everything. Its blast then hits the neighbour, whose
        /// pull must start at once, though the first one's pull never
        /// moved it. Neither hit leaves a death vortex.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTractorBombShot(Map map, IntVec3 center)
        {
            var mark = ErrorMark();
            ClearTractorStage(map, center);
            Building_TractorBomb bomb = PlantTestBomb(map, center, 5f);
            IntVec3 neighbourAt = center + new IntVec3(4, 0, 0);
            Building_TractorBomb neighbour =
                PlantTestBomb(map, neighbourAt, 60f);
            var steel = new List<Thing>();
            foreach (IntVec3 offset in new[]
            {
                new IntVec3(-9, 0, 0), new IntVec3(0, 0, 9),
                new IntVec3(-6, 0, -6), new IntVec3(6, 0, 6),
            })
            {
                Thing stack = ThingMaker.MakeThing(ThingDefOf.Steel);
                stack.stackCount = 20;
                steel.Add(GenSpawn.Spawn(stack, center + offset, map));
            }

            Pawn shooter = TestColonist(map, center + new IntVec3(0, 0, -28));
            int wrecked = CompTractorBomb.Wrecked;
            int vortexes = Vortexes(map);
            bomb.StartCountdown();
            neighbour.StartCountdown();
            Tick(30);

            // First shot: during the countdown.
            int hits = Building_TractorBomb.Hits;
            int left = bomb.TicksUntilImplosion;
            Shoot(shooter, bomb);
            for (int i = 0; i < 120 && Building_TractorBomb.Hits == hits; i++)
            {
                Tick(1);
            }

            int hitAt = bomb.LastHitTick;
            Building_TractorBomb.Stage hitStage = bomb.LastHitStage;
            Tick(2);
            int startedAfter = bomb.ImplosionStartedTick - hitAt;
            int leftAtHit = bomb.CountdownTicks
                - (hitAt - bomb.CountdownStartedTick);
            Check("shooting a planted bomb during its countdown starts its "
                    + "pull at once",
                Building_TractorBomb.Hits == hits + 1
                    && hitStage == Building_TractorBomb.Stage.CountingDown
                    && bomb.Spawned
                    && bomb.CurrentStage == Building_TractorBomb.Stage.Imploding
                    && startedAfter >= 0 && startedAfter <= 2
                    && leftAtHit >= 60,
                $"hits +{Building_TractorBomb.Hits - hits}, hit while "
                    + $"{hitStage} with {leftAtHit} ticks left (of {left} "
                    + $"when fired), stage now {bomb.CurrentStage}, pull "
                    + $"started {startedAfter} ticks after the hit");

            // Second shot: during the pull.
            hits = Building_TractorBomb.Hits;
            int explosions = Building_TractorBomb.Explosions;
            bool gatheredBefore = false;
            int elapsedAtHit = -1;
            if (bomb.Spawned)
            {
                Shoot(shooter, bomb);
            }

            for (int i = 0; i < 120 && bomb.Spawned
                && Building_TractorBomb.Hits == hits; i++)
            {
                gatheredBefore = bomb.Implosion != null
                    && (bomb.Implosion.AllGathered || bomb.Implosion.Settled);
                elapsedAtHit = bomb.Implosion?.ElapsedTicks ?? -1;
                Tick(1);
            }

            int secondHitAt = bomb.LastHitTick;
            hitStage = bomb.LastHitStage;
            Tick(2);
            int blastAfter = Building_TractorBomb.LastExplosionTick
                - secondHitAt;
            Check("shooting a planted bomb during its pull sets off its "
                    + "blast at once",
                Building_TractorBomb.Hits == hits + 1
                    && hitStage == Building_TractorBomb.Stage.Imploding
                    && bomb.Destroyed
                    && Building_TractorBomb.Explosions >= explosions + 1
                    && blastAfter >= 0 && blastAfter <= 2
                    && !gatheredBefore,
                $"hits +{Building_TractorBomb.Hits - hits}, hit while "
                    + $"{hitStage} {elapsedAtHit} ticks into the pull "
                    + $"(gathered or settled {gatheredBefore}), destroyed "
                    + $"{bomb.Destroyed}, blast {blastAfter} ticks after "
                    + "the hit");

            // Its blast reaches the neighbour as its wave spreads.
            Tick(20);
            bool chained = neighbour.Spawned
                && neighbour.CurrentStage == Building_TractorBomb.Stage.Imploding
                && neighbour.LastHitTick >= secondHitAt
                && neighbour.LastHitTick - secondHitAt <= 20;
            Check("a planted bomb's blast sets off its neighbour, which its "
                    + "pull never moved, and no hit leaves a death vortex",
                chained && neighbour.Position == neighbourAt
                    && CompTractorBomb.Wrecked == wrecked
                    && Vortexes(map) == vortexes,
                $"neighbour stage {neighbour.CurrentStage}, hit "
                    + $"{neighbour.LastHitTick - secondHitAt} ticks after "
                    + $"the blast, at {neighbour.Position} (planted at "
                    + $"{neighbourAt}); wrecked +{CompTractorBomb.Wrecked - wrecked}, "
                    + $"vortexes {vortexes} -> {Vortexes(map)}");
            for (int i = 0; i < 1500 && neighbour.Spawned; i++)
            {
                Tick(1);
            }

            Tick(30);
            foreach (Thing t in steel.Where(t => t.Spawned))
            {
                t.Destroy(DestroyMode.Vanish);
            }

            Check("shooting planted bombs logs no errors",
                !NewErrorSince(mark), "an error was logged (see the log)");
        }

        /// <summary>
        /// Fires one revolver bullet from a pawn at a planted bomb; it can
        /// hit nothing else on the way.
        /// </summary>
        /// <param name="shooter">Who fires.</param>
        /// <param name="target">The bomb.</param>
        private static void Shoot(Pawn shooter, Thing target)
        {
            ThingDef bulletDef =
                DefDatabase<ThingDef>.GetNamedSilentFail("Bullet_Revolver")
                ?? DefDatabase<ThingDef>.AllDefs.First(
                    d => d.projectile != null && !d.projectile.flyOverhead
                        && d.projectile.damageDef == DamageDefOf.Bullet);
            var bullet = (Projectile)GenSpawn.Spawn(
                bulletDef, shooter.Position, shooter.Map);
            bullet.Launch(
                shooter, shooter.DrawPos, target, target,
                ProjectileHitFlags.IntendedTarget);
        }

        /// <summary>A robot's death vortexes on the map.</summary>
        /// <param name="map">The map.</param>
        /// <returns>How many.</returns>
        private static int Vortexes(Map map) =>
            map.listerThings
                .ThingsOfDef(BT_TractorBombDefOf.BT_TractorBombDeathVortex)
                .Count;
    }
}
