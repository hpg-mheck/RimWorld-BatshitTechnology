// Batshit Technology - wild birds take off before a volley lands.
// RimWorld 1.6.4871 sends a wild animal that can fly (Odyssey's birds,
// the duck) off the map when an explosion of radius 3.5 or more goes off
// within its radius plus 7 tiles of it (Pawn_MindState.Notify_Explosion),
// or a bullet lands within 12 (Notify_ClamorImpact): it starts the
// ExitMapFlying job with JobCondition.InterruptForced
// (StartFleeingBecauseOfPawnAction), on every such blast, at the bird's
// leaveMapOnFleeChance (1 for Odyssey's small birds). FleeUtility.
// ShouldAnimalFleeDanger keeps a Flee job from being restarted in the
// tick it started, but not ExitMapFlying, so eleven blasts near one bird
// in one tick start eleven jobs, and the job tracker logs "started 10 jobs
// in one tick" as an error and parks the bird on a Wait job, to be sent
// off again by the next blast. A vanilla mortar fires one shell at a
// time; the Ballista lands up to 256 bolts in a few ticks, so a volley
// near a bird logged that error thirteen times in one tour (seed tour-22,
// the stampede's volley). The Ballista therefore sends the birds near its
// target off itself, once, as the volley leaves: vanilla's own call, so
// they take off as they would have, a few ticks before the first bolt
// lands rather than at it. No Harmony; the barrage and cook-offs, whose
// bolts spread over a wide ring and many ticks, are left to vanilla.

using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace BatshitTechnology
{
    /// <summary>
    /// Sends the wild birds near a coming volley off the map, as vanilla
    /// would on the first blast, before the blasts can restart their
    /// flight many times in one tick.
    /// </summary>
    public static class WildFliers
    {
        /// <summary>
        /// How far from a bullet's impact animals hear it
        /// (<c>Projectile.Impact</c>'s clamor radius, RimWorld 1.6.4871).
        /// </summary>
        public const float ClamorReach = 12f;

        /// <summary>
        /// The smallest blast an animal flees from
        /// (<c>Pawn_MindState.Notify_Explosion</c>).
        /// </summary>
        public const float SmallestNoticedBlast = 3.5f;

        /// <summary>
        /// How far beyond its radius a blast sends animals fleeing
        /// (<c>Pawn_MindState.Notify_Explosion</c>).
        /// </summary>
        public const float BlastNoticeMargin = 7f;

        /// <summary>
        /// How far from where a projectile lands a wild animal is sent
        /// fleeing by it, plus a tile: the bullet's clamor, or a big
        /// enough blast's radius and margin, whichever is farther.
        /// </summary>
        /// <param name="projectileDef">The projectile, or null.</param>
        /// <returns>The reach, in tiles.</returns>
        public static float NoticeReach(ThingDef projectileDef)
        {
            float blast = projectileDef?.projectile?.explosionRadius ?? 0f;
            float notice = blast >= SmallestNoticedBlast ? blast + BlastNoticeMargin : 0f;
            return System.Math.Max(ClamorReach, notice) + 1f;
        }

        /// <summary>
        /// Whether a pawn is one vanilla would send off the map when a
        /// blast or an impact scared it: an animal that can fly and leave
        /// the map, not the player's, under the sky, not a quest's lodger,
        /// that would flee danger now (not downed, in a mental state, a
        /// lord's or a master's), and not already flying off.
        /// </summary>
        /// <param name="p">The pawn.</param>
        /// <returns>True if a volley would scare it off the map.</returns>
        public static bool WouldFlyOff(Pawn p)
        {
            return p != null && p.Spawned && !p.Dead && p.RaceProps != null
                && p.RaceProps.canLeaveMapFlying && p.RaceProps.leaveMapOnFleeChance > 0f
                && p.Faction != Faction.OfPlayer
                && !p.Position.Roofed(p.Map)
                && !p.IsQuestLodger()
                && p.flight != null && p.flight.CanEverFly
                && p.jobs != null
                && p.CurJobDef != JobDefOf.ExitMapFlying
                && FleeUtility.ShouldAnimalFleeDanger(p);
        }

        /// <summary>
        /// Sends every wild bird within a radius of a cell off the map,
        /// with vanilla's own call: the ExitMapFlying job, forced, once
        /// each. The bird waits up to nine ticks, then takes off.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="centre">Where the volley is aimed.</param>
        /// <param name="radius">How far out to look, in tiles.</param>
        /// <returns>How many birds were sent off.</returns>
        public static int SendOff(Map map, IntVec3 centre, float radius)
        {
            if (map?.mapPawns == null || !centre.IsValid || radius <= 0f
                || JobDefOf.ExitMapFlying == null)
            {
                return 0;
            }

            int sent = 0;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToArray())
            {
                if (p.Position.InHorDistOf(centre, radius) && WouldFlyOff(p))
                {
                    p.jobs.StartJob(
                        JobMaker.MakeJob(JobDefOf.ExitMapFlying), JobCondition.InterruptForced);
                    sent++;
                }
            }

            return sent;
        }
    }
}
