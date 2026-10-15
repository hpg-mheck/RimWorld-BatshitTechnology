// Batshit Technology - smoke checks for the tour group and the Tractor
// Bomb's protest about a friendly guest (slice 19000c; docs/SHOWCASE.md,
// "Tourists", and docs/TRACTOR-BOMB.md, "Protest"). The tour verifier
// watches the group through a whole tour; these checks run the same
// TourGroup on the smoke map, where a death and a dismissal can be staged
// on purpose: a group arrives (hidden faction, no goodwill, unarmed); a
// Timid robot protests about a tourist in its pull with no colonist near;
// a tourist killed and another hurt by a colonist change no goodwill and
// send no letter; dismissal leaves nothing behind and counts each; and a
// loaded game sends home a group it still holds (a game saved mid-tour).

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The smoke runner's tour group checks.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// The tour group on the smoke map, and the robot's protest about
        /// a guest: the group arrives, a robot protests about a tourist in
        /// its pull, a tourist's death and injury by a colonist cost no
        /// goodwill and send no letter, dismissal counts and removes every
        /// tourist, corpse and stitch of clothing, and a loaded game sends
        /// home a group the tour never dismissed.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTourists(Map map, IntVec3 center)
        {
            var mark = ErrorMark();
            ClearTractorStage(map, center);
            TourGroup group = TourGroup.Arrive(map, center + new IntVec3(0, 0, -20));
            CheckTouristsArrived(group);
            if (group == null || group.Members.Count == 0)
            {
                return;
            }

            TestTractorBombProtestsAboutGuest(map, center, group);
            CheckTouristLossesCostNothing(map, group);
            CheckTouristsDismissed(group);
            TestTouristsSentHomeOnLoad(map, center);
            Check("the tour group logged no errors",
                !NewErrorSince(mark), "an error was logged (see the log)");
        }

        /// <summary>
        /// A tour group arrived: three to five tourists, standing, of the
        /// hidden tourist faction (no goodwill, not hostile, never made with
        /// a world), none a colonist, unarmed, with empty pockets and tour
        /// composure; a volunteer and a stray among them.
        /// </summary>
        /// <param name="group">The group, or null.</param>
        private void CheckTouristsArrived(TourGroup group)
        {
            IReadOnlyList<Pawn> members = group?.Members ?? new List<Pawn>();
            Faction faction = group?.Faction;
            bool sized = members.Count >= TourGroupPlan.MinSize
                && members.Count <= TourGroupPlan.MaxSize;
            bool hidden = faction != null && faction.Hidden && !faction.HasGoodwill
                && faction.def == BT_TouristDefOf.BT_ShowcaseTourists
                && faction.def.requiredCountAtGameStart == 0
                && !faction.HostileTo(Faction.OfPlayer);
            bool tourists = members.All(p => TourGroup.Standing(p)
                && p.Faction == faction && !p.IsColonist
                && p.equipment?.Primary == null
                && (p.inventory?.innerContainer.Count ?? 0) == 0
                && p.health.hediffSet.HasHediff(BT_TouristDefOf.BT_TourComposure));
            Check("a tour group arrives: 3 to 5 unarmed tourists of a hidden "
                    + "faction with no goodwill, none a colonist",
                group != null && sized && hidden && tourists
                    && group.Volunteer != null && group.Stray != null,
                $"group {group != null}; {members.Count} tourists; faction "
                    + $"{faction?.Name ?? "none"} hidden {faction?.Hidden} goodwill "
                    + $"{faction?.HasGoodwill} hostile {faction?.HostileTo(Faction.OfPlayer)}; "
                    + "tourists "
                    + string.Join(", ", members.Select(p => $"{p.LabelShort} "
                        + $"(colonist {p.IsColonist}, holds "
                        + $"{p.equipment?.Primary?.def.defName ?? "nothing"}, pockets "
                        + $"{p.inventory?.innerContainer.Count ?? -1})")));
        }

        /// <summary>
        /// A Timid robot ready to emplace, with a tourist 4 tiles off and
        /// no colonist within its pull, protests about the guest for about
        /// three seconds, then emplaces anyway (Core's TractorBombProtest).
        /// The tourist is then sent clear and the bomb let go off.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        /// <param name="group">The tour group.</param>
        private void TestTractorBombProtestsAboutGuest(
            Map map, IntVec3 center, TourGroup group)
        {
            ClearTractorStage(map, center);
            Pawn robot = TractorStaging.SpawnRobot(map, center + new IntVec3(-6, 0, 0));
            SetProximity(robot, TractorBombProximity.Timid);
            Pawn guest = group.Stray;
            bool placed = group.Place(guest, robot.Position + new IntVec3(0, 0, 4));
            SpawnStunnedHostile(map, center + new IntVec3(6, 0, 0));
            var comp = robot.TryGetComp<CompTractorBomb>();
            float pull = comp.PullRadius;
            bool colonistNear = map.mapPawns.FreeColonistsSpawned.Any(
                p => p.Position.InHorDistOf(robot.Position, pull + 2f));
            bool protested = false;
            for (int i = 0; i < 600 && robot.Spawned; i++)
            {
                Tick(1);
                protested |= robot.Spawned && comp.Protesting;
            }

            Building_TractorBomb bomb = map.listerThings
                .ThingsOfDef(BT_TractorBombDefOf.BT_TractorBombEmplaced)
                .OfType<Building_TractorBomb>().FirstOrDefault(b => b.Spawned);
            int delay = bomb == null ? -1 : bomb.EmplacedTick - bomb.ReadyTick;
            Check("a tourist (a friendly guest) in the pull radius, with no "
                    + "colonist near, earns the robot's 3 s protest",
                placed && !colonistNear && protested && bomb != null
                    && delay >= 170 && delay <= 190,
                $"placed {placed}, colonist near {colonistNear}, protested "
                    + $"{protested}, bomb {bomb != null}, delay {delay}, guest "
                    + $"{guest?.Position.DistanceTo(robot.Position):F1} tiles off");

            // Get the guest clear before the pull, and let it go off.
            group.Place(guest, Clamp(map, center + new IntVec3(0, 0, -60)));
            for (int i = 0; i < 1200 && bomb != null && bomb.Spawned; i++)
            {
                Tick(1);
            }

            Tick(60);
        }

        /// <summary>
        /// A tourist killed by a colonist's blast and another hurt by a
        /// colonist's blow change nothing between the colony and the
        /// tourists' faction (no goodwill, no hostility) and send no
        /// letter.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="group">The tour group.</param>
        private void CheckTouristLossesCostNothing(Map map, TourGroup group)
        {
            Pawn colonist = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            Faction faction = group.Faction;
            int letters = Find.LetterStack.LettersListForReading.Count;
            FactionRelationKind before = faction.RelationKindWith(Faction.OfPlayer);
            int goodwill = faction.GoodwillWith(Faction.OfPlayer);
            Pawn killed = group.Sightseers.FirstOrDefault(TourGroup.Standing)
                ?? group.Volunteer;
            Pawn hurt = group.Members.FirstOrDefault(
                p => p != killed && TourGroup.Standing(p));
            hurt?.TakeDamage(new DamageInfo(
                DamageDefOf.Blunt, 8f, instigator: colonist));
            killed?.Kill(new DamageInfo(DamageDefOf.Bomb, 99f, instigator: colonist));
            Tick(30);
            int newLetters = Find.LetterStack.LettersListForReading.Count - letters;
            Check("a tourist killed and one hurt by a colonist cost no goodwill "
                    + "and send no letter",
                killed != null && killed.Dead && hurt != null
                    && faction.RelationKindWith(Faction.OfPlayer) == before
                    && before != FactionRelationKind.Hostile
                    && faction.GoodwillWith(Faction.OfPlayer) == goodwill
                    && newLetters == 0,
                $"killed {killed?.LabelShort} dead {killed?.Dead}; hurt "
                    + $"{hurt?.LabelShort}; relation {before} to "
                    + $"{faction.RelationKindWith(Faction.OfPlayer)}; goodwill "
                    + $"{goodwill} to {faction.GoodwillWith(Faction.OfPlayer)}; "
                    + $"new letters {newLetters}");
        }

        /// <summary>
        /// Dismissal counts every tourist once (one dead, the rest
        /// standing), and leaves no tourist, corpse or clothing on the map,
        /// and no lord; a second dismissal changes nothing.
        /// </summary>
        /// <param name="group">The tour group.</param>
        private void CheckTouristsDismissed(TourGroup group)
        {
            Map map = group.Map;
            int lords = map.lordManager.lords.Count(l => l.faction == group.Faction);
            TourGroupCount count = group.Dismiss("the smoke test");
            TourGroupCount again = group.Dismiss("the smoke test, again");
            List<string> left = group.LeftOnMap();
            int lordsAfter = map.lordManager.lords.Count(l => l.faction == group.Faction);
            Check("dismissal counts every tourist and leaves no tourist, corpse "
                    + "or clothes behind",
                count.AccountedFor && count.Arrived == group.Members.Count
                    && count.Dead == 1 && count.Standing == count.Arrived - 1
                    && again == count && left.Count == 0 && lords == 1
                    && lordsAfter == 0 && group.Fates.Count == count.Arrived,
                $"{count}; again {again}; left [{string.Join(", ", left)}]; "
                    + $"lords {lords} then {lordsAfter}; fates "
                    + $"[{string.Join(", ", group.Fates)}]");
        }

        /// <summary>
        /// A game saved during a tour and loaded again holds a group the
        /// tour never dismissed: the tourists, their lord (it saves their
        /// spots) and any corpse, while the session and the tour saved
        /// nothing. A second group arrives on the smoke map and is left
        /// as such a game finds it, one tourist dead and one downed; the
        /// game's own GameComponent_TourGroupSweep then runs its
        /// LoadedGame, as RimWorld does after a load, and nothing of the
        /// tourists' faction may be left: no pawn, no corpse, no lord. A
        /// second load finds nothing. A whole save and reload is not run
        /// (the runner never reloads its game; docs/AI-TODO.txt).
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="center">The middle of the test area.</param>
        private void TestTouristsSentHomeOnLoad(Map map, IntVec3 center)
        {
            ClearTractorStage(map, center);
            TourGroup group = TourGroup.Arrive(map, center + new IntVec3(0, 0, -20));
            var sweep = Current.Game.GetComponent<GameComponent_TourGroupSweep>();
            Pawn dead = group?.Members.FirstOrDefault();
            Pawn downed = group?.Members.Skip(1).FirstOrDefault();
            dead?.Kill(new DamageInfo(DamageDefOf.Bomb, 99f));
            if (downed != null)
            {
                HealthUtility.DamageUntilDowned(downed, allowBleedingWounds: false);
            }

            Tick(10);
            var before = TouristsLeft(map);
            int left = group?.LeftOnMap().Count ?? 0;
            sweep?.LoadedGame();
            var after = TouristsLeft(map);
            int again = GameComponent_TourGroupSweep.SweepAllMaps("the smoke test, again");
            Check("a loaded game sends home a tour group it still holds: no "
                    + "tourist, corpse or lord of theirs is left",
                group != null && sweep != null && dead != null && dead.Dead
                    && downed != null && downed.Downed
                    && left == group.Members.Count
                    && before == (group.Members.Count - 1, 1, 1)
                    && after == (0, 0, 0) && group.LeftOnMap().Count == 0
                    && again == 0,
                $"group {group?.Members.Count}; sweep {sweep != null}; dead "
                    + $"{dead?.Dead}; downed {downed?.Downed}; on the map "
                    + $"{left}; before (pawns, corpses, lords) {before}; after "
                    + $"{after}; again {again}");
        }

        /// <summary>
        /// What of the tourists' faction a map holds: living pawns, spawned
        /// or held, corpses anywhere, and lords of the tourists' lord job.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The counts.</returns>
        private static (int Pawns, int Corpses, int Lords) TouristsLeft(Map map)
        {
            var corpses = new List<Corpse>();
            ThingOwnerUtility.GetAllThingsRecursively(
                map, ThingRequest.ForGroup(ThingRequestGroup.Corpse), corpses);
            return (
                map.mapPawns.AllPawns.Count(TourGroup.IsTourist),
                corpses.Count(c => TourGroup.IsTourist(c.InnerPawn)),
                map.lordManager.lords.Count(l => l.LordJob is LordJob_ShowcaseTourists));
        }
    }
}
