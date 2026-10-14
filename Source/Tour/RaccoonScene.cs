// Batshit Technology - the directed tour's scene 8, "The Raccoon
// Incident" (docs/SHOWCASE.md, "Reel 9: The Raccoon Incident").
// The camera finds Ned "Overkill" Lessly, AnyRim Savings & Loan's guard,
// at the bank. A vanilla raccoon in the manhunter state comes in at Main
// Street's south end and goes for Constance in her lawn chair; Ned steps
// into the street and throws his Ergonomic Singularity Grenade at it as
// soon as it is in range. Once the singularity is over, the narrator reads
// the census with the real count, and the outcome speaks to the raccoon's
// fate. The places are Core's RaccoonIncident; the census line is
// AnyRimText's, said through the Showcase API's Say because its number is
// the world's. However the scene ends, a living raccoon is calmed, Ned is
// stood down, and a grenade he dropped is taken back. If Ned is no longer
// on duty when the scene begins (the tour asked before it started, and the
// town may have lost him since), no raccoon comes: the scene closes after
// its least time on its "completed" line, which thanks him regardless.

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
    /// Scene 8, "The Raccoon Incident": AnyRim's bank guard, a manhunting
    /// raccoon and the grenade. Beats: <c>guard</c> on Ned at the bank,
    /// <c>pest</c> as the raccoon comes up Main Street, <c>thrown</c> when
    /// the grenade leaves his hand, and <c>census</c> (its words computed)
    /// on the welcome sign once the singularity is over. It declines when
    /// AnyRim is not on the map, when the tour did not show AnyRim, or
    /// when Ned is not on duty.
    /// </summary>
    public class RaccoonScene : TourScene
    {
        /// <summary>
        /// When the camera finds Ned, in ticks: after the start line's
        /// reading time (2.5 s plus 50 ms a character at normal speed).
        /// </summary>
        public int guardTick = 330;

        /// <summary>
        /// When the raccoon comes to town, in ticks: after the guard's
        /// line has had its reading time.
        /// </summary>
        public int pestTick = 780;

        /// <summary>
        /// Ticks after the singularity is over before the census is read,
        /// so AnyRim's own census (every 30 ticks) has caught up.
        /// </summary>
        public int censusDelayTicks = 60;

        /// <summary>
        /// How long the census line holds before the scene is done, in
        /// ticks: its reading time at normal speed.
        /// </summary>
        public int holdTicks = 390;

        /// <summary>The camera's zoom on Ned at the bank.</summary>
        public float guardZoom = 14f;

        /// <summary>The zoom that holds Main Street end to end.</summary>
        public float streetZoom = 30f;

        /// <summary>The zoom on the singularity.</summary>
        public float throwZoom = 20f;

        /// <summary>The zoom on the welcome sign for the census.</summary>
        public float signZoom = 16f;

        /// <summary>How often Ned is kept to his orders, in ticks.</summary>
        private const int OrderCheckTicks = 60;

        /// <summary>
        /// Ticks Constance waits in her lawn chair: longer than the scene.
        /// </summary>
        private const int ChairWaitTicks = 6000;

        /// <summary>Ned, the guard, as found at set-up.</summary>
        [Unsaved]
        private Pawn ned;

        /// <summary>Constance, the neighbour, as found at set-up.</summary>
        [Unsaved]
        private Pawn constance;

        /// <summary>Ned's grenade as he held it at set-up.</summary>
        [Unsaved]
        private Thing grenade;

        /// <summary>The raccoon, once it has come to town.</summary>
        [Unsaved]
        private Pawn raccoon;

        /// <summary>The jobs this scene gave Ned, to stand him down.</summary>
        [Unsaved]
        private List<Job> nedOrders;

        /// <summary>Constance's wait in her chair, to end it.</summary>
        [Unsaved]
        private Job chairWait;

        /// <summary>AnyRim's population at set-up.</summary>
        [Unsaved]
        private int populationBefore;

        /// <summary>The scene tick the grenade left Ned's hand, or -1.</summary>
        [Unsaved]
        private int thrownAt = -1;

        /// <summary>
        /// The scene tick the singularity was first seen over, or -1.
        /// </summary>
        [Unsaved]
        private int quietAt = -1;

        /// <summary>The scene tick the census was read, or -1.</summary>
        [Unsaved]
        private int censusAt = -1;

        /// <summary>The census line read, or null.</summary>
        [Unsaved]
        private string censusLine;

        /// <summary>Where the camera looks now.</summary>
        [Unsaved]
        private IntVec3 look = IntVec3.Invalid;

        /// <summary>Whether the raccoon was dead when the scene ended.</summary>
        [Unsaved]
        private bool raccoonLost;

        /// <summary>
        /// Whether the scene stood down at set-up because Ned was no
        /// longer on duty: no raccoon comes, and it closes at once.
        /// </summary>
        [Unsaved]
        private bool stoodDown;

        /// <summary>
        /// Gets the raccoon, once it has come to town (the tour verifier
        /// reads its fate from the world).
        /// </summary>
        internal Pawn Raccoon => raccoon;

        /// <summary>Gets Ned, as found at set-up.</summary>
        internal Pawn Guard => ned;

        /// <summary>Gets AnyRim's population at set-up.</summary>
        internal int PopulationBefore => populationBefore;

        /// <summary>Gets the census line read, or null before it.</summary>
        internal string CensusLine => censusLine;

        /// <summary>Gets Ned while he stands; nothing once he is down.</summary>
        public override Thing Subject =>
            Alive(ned) && !ned.Downed ? ned : null;

        /// <summary>Gets where the camera looks now.</summary>
        public override IntVec3 FocusCell =>
            look.IsValid ? look : base.FocusCell;

        /// <summary>
        /// Gets why the scene cannot run: AnyRim was not shown or not
        /// built, Ned is not on duty with his grenade, the game has no
        /// raccoons, or Main Street's south end is blocked.
        /// </summary>
        protected override string WhyUnavailable
        {
            get
            {
                string town = TownUnavailable(Stage, Map);
                if (town != null)
                {
                    return town;
                }

                Pawn guard = FindResident(Stage, Map, RaccoonIncident.GuardKey);
                if (!OnDuty(guard))
                {
                    return "Ned Lessly is not on duty with his grenade";
                }

                if (RaccoonKind == null)
                {
                    return "this game has no raccoons";
                }

                return MapCell(RaccoonIncident.Entry).Standable(Map)
                    ? null
                    : "Main Street's south end is blocked";
            }
        }

        /// <summary>The raccoon's pawn kind, or null.</summary>
        internal static PawnKindDef RaccoonKind =>
            DefDatabase<PawnKindDef>.GetNamedSilentFail("Raccoon");

        /// <summary>
        /// Why AnyRim cannot host the scene: the tour verifier forced the
        /// AnyRim scene to decline (the tour never shows the town's
        /// incident without the town), or the town is not on the map.
        /// </summary>
        /// <param name="stage">The tour's stage.</param>
        /// <param name="map">The tour's map.</param>
        /// <returns>The reason, or null when the town can host it.</returns>
        internal static string TownUnavailable(TourStage stage, Map map)
        {
            if (SkippedForTest.Contains(TourScript.AnyRim))
            {
                return "AnyRim was not shown (its scene was skipped)";
            }

            return stage != null && stage.HasAnyRim
                && map?.GetComponent<MapComponent_AnyRim>()?.Built == true
                    ? null
                    : "AnyRim was not built on this map";
        }

        /// <summary>
        /// Finds a resident of the stage's AnyRim by plan key, matching the
        /// planned first and last names.
        /// </summary>
        /// <param name="stage">The tour's stage.</param>
        /// <param name="map">The tour's map.</param>
        /// <param name="key">The resident's key ("Ned").</param>
        /// <returns>The pawn, alive or dead, or null.</returns>
        internal static Pawn FindResident(TourStage stage, Map map, string key)
        {
            ResidentPlan plan = stage?.AnyRimPlan?.Residents?
                .FirstOrDefault(r => r.Key == key);
            MapComponent_AnyRim record = map?.GetComponent<MapComponent_AnyRim>();
            if (plan == null || record == null)
            {
                return null;
            }

            return record.Residents.FirstOrDefault(p =>
                p?.Name is NameTriple name
                && name.First == plan.First && name.Last == plan.Last);
        }

        /// <summary>
        /// Whether the guard can play his part: on the map, standing,
        /// sane, and holding his grenade.
        /// </summary>
        /// <param name="guard">The guard, or null.</param>
        /// <returns>True if he is on duty.</returns>
        internal static bool OnDuty(Pawn guard) =>
            guard != null && guard.Spawned && !guard.Dead && !guard.Downed
            && !guard.InMentalState
            && guard.equipment?.Primary?.def.defName == RaccoonIncident.GrenadeDef;

        /// <summary>
        /// Finds the cast, records the population, and sets the stage
        /// before the camera arrives: Ned on the bank's front walk and
        /// Constance in her lawn chair.
        /// </summary>
        protected override void SetupScene()
        {
            ned = FindResident(Stage, Map, RaccoonIncident.GuardKey);
            constance = FindResident(Stage, Map, RaccoonIncident.NeighbourKey);
            grenade = ned?.equipment?.Primary;
            raccoon = null;
            nedOrders = new List<Job>();
            chairWait = null;
            populationBefore =
                Map.GetComponent<MapComponent_AnyRim>()?.Population ?? 0;
            thrownAt = -1;
            quietAt = -1;
            censusAt = -1;
            censusLine = null;
            raccoonLost = false;
            stoodDown = !OnDuty(ned);
            look = MapCell(RaccoonIncident.BankWalk);
            if (stoodDown)
            {
                Log.Message($"{ShowcaseLog.Prefix} raccoon: Ned is not on duty "
                    + $"({Fate(ned)}; population {populationBefore}); no raccoon "
                    + "comes, and the scene closes");
                return;
            }

            if (Alive(ned) && !ned.Downed)
            {
                Place(ned, MapCell(RaccoonIncident.BankWalk));
                ned.Rotation = Rot4.West;
            }

            if (Alive(constance) && !constance.Downed
                && Place(constance, MapCell(RaccoonIncident.LawnChair)))
            {
                chairWait = JobMaker.MakeJob(JobDefOf.Wait, ChairWaitTicks);
                chairWait.overrideFacing = Rot4.West;
                chairWait.playerForced = true;
                constance.jobs.StartJob(chairWait, JobCondition.InterruptForced);
            }
        }

        /// <summary>
        /// Plays the guard's beat, brings the raccoon to town with Ned
        /// sent to his post, plays the throw, and reads the census once
        /// the singularity is over.
        /// </summary>
        public override void Tick()
        {
            if (stoodDown)
            {
                return;
            }

            if (Elapsed >= guardTick)
            {
                BeatOnce(TourScript.Guard, Shot(guardZoom));
            }

            if (Elapsed >= pestTick && raccoon == null)
            {
                BringTheRaccoon();
            }

            if (raccoon == null)
            {
                return;
            }

            if (thrownAt < 0)
            {
                if (GrenadeThrown())
                {
                    thrownAt = Elapsed;
                    look = raccoon.Spawned
                        ? raccoon.Position
                        : MapCell(RaccoonIncident.GuardPost);
                    BeatOnce(TourScript.Thrown, Shot(throwZoom), select: false);
                }
                else if (Elapsed % OrderCheckTicks == 0)
                {
                    KeepNedOnTask();
                }

                return;
            }

            if (censusAt >= 0)
            {
                return;
            }

            bool quiet = Elapsed >= thrownAt + 60
                && !BatshitProjectileInFlight() && !ImplosionActive()
                && !SingularityOpen();
            if (!quiet)
            {
                quietAt = -1;
                return;
            }

            if (quietAt < 0)
            {
                quietAt = Elapsed;
            }

            if (Elapsed >= quietAt + censusDelayTicks)
            {
                ReadTheCensus();
            }
        }

        /// <summary>
        /// Done once the census has been read and has held, or at once
        /// when the scene stood down.
        /// </summary>
        /// <returns>True when the incident is over.</returns>
        public override bool IsDone() =>
            stoodDown || (censusAt >= 0 && Elapsed >= censusAt + holdTicks);

        /// <summary>
        /// Fixes the raccoon's fate for the outcome, then tidies up
        /// however the scene ended: a living raccoon is calmed, Ned is
        /// stood down, a grenade he dropped is taken back, and Constance
        /// leaves her chair.
        /// </summary>
        /// <param name="ending">How it ended.</param>
        public override void End(SceneEnd ending)
        {
            raccoonLost = raccoon != null
                && TourScript.RaccoonOutcome(raccoon.Dead, raccoon.Destroyed)
                    == TourScript.RaccoonLost;
            if (raccoon != null && !raccoon.Dead && !raccoon.Destroyed)
            {
                raccoon.MentalState?.RecoverFromState();
                raccoon.jobs?.StopAll();
            }

            if (ned != null && !ned.Dead && !ned.Destroyed && ned.jobs != null)
            {
                if (nedOrders != null && nedOrders.Contains(ned.CurJob))
                {
                    ned.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }

                ned.jobs.ClearQueuedJobs();
            }

            if (grenade != null && grenade.Spawned && !grenade.Destroyed)
            {
                grenade.Destroy(DestroyMode.Vanish);
            }

            if (constance != null && !constance.Dead && constance.Spawned
                && chairWait != null && constance.CurJob == chairWait)
            {
                constance.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        /// <summary>
        /// The raccoon's fate when the scene completed after the throw;
        /// the API's moment otherwise.
        /// </summary>
        /// <param name="ending">How it ended.</param>
        /// <returns>The narration moment.</returns>
        public override string OutcomeKey(SceneEnd ending)
        {
            if (ending != SceneEnd.Completed || raccoon == null || thrownAt < 0)
            {
                return base.OutcomeKey(ending);
            }

            return raccoonLost
                ? TourScript.RaccoonLost
                : TourScript.RaccoonSurvived;
        }

        /// <summary>
        /// Spawns a raccoon at Main Street's south end in the manhunter
        /// state, sets it on Constance (or lets it choose, if she is not
        /// there), sends Ned to his post with orders to throw as soon as it
        /// is in range, and plays the pest's beat over the whole street.
        /// </summary>
        private void BringTheRaccoon()
        {
            PawnKindDef kind = RaccoonKind;
            IntVec3 entry = MapCell(RaccoonIncident.Entry);
            if (kind == null || !entry.InBounds(Map))
            {
                pestTick = int.MaxValue; // the scene times out
                Log.Warning($"{ShowcaseLog.Prefix} raccoon: no raccoon could "
                    + "come to town; the scene will time out");
                return;
            }

            raccoon = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind, null, PawnGenerationContext.NonPlayer, tile: Map.Tile,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false));
            GenSpawn.Spawn(raccoon, entry, Map, Rot4.North, WipeMode.Vanish);
            raccoon.mindState.mentalStateHandler.TryStartMentalState(
                MentalStateDefOf.ManhunterPermanent, forced: true,
                forceWake: true, transitionSilently: true);
            if (Alive(constance) && !constance.Downed)
            {
                Job bite = JobMaker.MakeJob(JobDefOf.AttackMelee, constance);
                bite.canBashDoors = true;
                bite.canBashFences = true;
                bite.attackDoorIfTargetLost = true;
                bite.playerForced = true;
                raccoon.jobs.StartJob(bite, JobCondition.InterruptForced);
            }

            OrderNed(walk: true);
            Cell entryCell = RaccoonIncident.Entry;
            Cell post = RaccoonIncident.GuardPost;
            look = MapCell(new Cell(
                (entryCell.X + post.X) / 2, (entryCell.Z + post.Z) / 2));
            Log.Message($"{ShowcaseLog.Prefix} raccoon: in at {entry}, "
                + $"after {(Alive(constance) ? "Constance" : "anyone")}; "
                + $"Ned {(OnDuty(ned) ? "to his post" : "not on duty")}; "
                + $"population {populationBefore}");
            BeatOnce(TourScript.Pest, Shot(streetZoom), select: false);
        }

        /// <summary>
        /// Gives Ned his orders: walk to his post (when asked and not
        /// there), then throw at the raccoon once, from where he stands,
        /// as soon as it is in range. Forced, so his household's duty
        /// does not call him back.
        /// </summary>
        /// <param name="walk">Whether to walk to the post first.</param>
        private void OrderNed(bool walk)
        {
            if (!OnDuty(ned) || raccoon == null || ned.jobs == null)
            {
                return;
            }

            Job attack = JobMaker.MakeJob(JobDefOf.AttackStatic, raccoon);
            attack.maxNumStaticAttacks = 1;
            attack.playerForced = true;
            nedOrders.Add(attack);
            IntVec3 post = MapCell(RaccoonIncident.GuardPost);
            ned.jobs.ClearQueuedJobs();
            if (walk && ned.Position != post && post.Standable(Map))
            {
                Job goTo = JobMaker.MakeJob(JobDefOf.Goto, post);
                goTo.locomotionUrgency = LocomotionUrgency.Sprint;
                goTo.playerForced = true;
                nedOrders.Add(goTo);
                ned.jobs.StartJob(goTo, JobCondition.InterruptForced);
                ned.jobs.jobQueue.EnqueueFirst(attack);
            }
            else
            {
                ned.jobs.StartJob(attack, JobCondition.InterruptForced);
            }
        }

        /// <summary>
        /// Puts Ned back to his throw if something else (a raccoon at his
        /// ankles, a stray thought) took his attention before it.
        /// </summary>
        private void KeepNedOnTask()
        {
            if (!OnDuty(ned) || nedOrders == null || nedOrders.Contains(ned.CurJob)
                || ned.jobs.jobQueue.Any(q => nedOrders.Contains(q.job)))
            {
                return;
            }

            OrderNed(walk: false);
        }

        /// <summary>
        /// Whether Ned's grenade has left his hand: it is in the air with
        /// him as its thrower, or a singularity has opened in town.
        /// </summary>
        /// <returns>True once it has.</returns>
        private bool GrenadeThrown()
        {
            bool flying = Map.listerThings
                .ThingsInGroup(ThingRequestGroup.Projectile)
                .OfType<Projectile>()
                .Any(p => p.def.defName == Showcase.GrenadeProjectileDef
                    && ReferenceEquals(p.Launcher, ned));
            if (flying)
            {
                return true;
            }

            CellRect town = Map.GetComponent<MapComponent_AnyRim>()?.Bounds
                ?? CellRect.Empty;
            return RunningImplosions().Any(i => town.Contains(i.Center));
        }

        /// <summary>
        /// Reads the census with the real count, on the welcome sign: the
        /// census beat (which has no declared line) and AnyRim's own words
        /// said through the Showcase API.
        /// </summary>
        private void ReadTheCensus()
        {
            MapComponent_AnyRim record = Map.GetComponent<MapComponent_AnyRim>();
            int now = record?.Population ?? 0;
            censusLine = AnyRimText.CensusReport(populationBefore, now);
            censusAt = Elapsed;
            IntVec3 sign = record != null && record.WelcomeSign.IsValid
                ? record.WelcomeSign
                : MapCell(RaccoonIncident.Entry);
            look = sign;
            BeatOnce(TourScript.Census, Shot(signZoom), select: false);
            Director?.Say(key, censusLine, new LookTargets(sign, Map));
            Log.Message($"{ShowcaseLog.Prefix} raccoon: census {populationBefore}"
                + $" -> {now}; raccoon {Fate(raccoon)}, Ned {Fate(ned)}, "
                + $"Constance {Fate(constance)}");
        }

        /// <summary>A pawn's fate, for the log.</summary>
        /// <param name="pawn">The pawn, or null.</param>
        /// <returns>"missing", "dead", "down" or "standing".</returns>
        private static string Fate(Pawn pawn) =>
            pawn == null ? "missing"
                : pawn.Dead || pawn.Destroyed ? "dead"
                : pawn.Downed ? "down"
                : "standing";

        /// <summary>
        /// Moves a resident to a cell before the camera arrives, if the
        /// cell will take them.
        /// </summary>
        /// <param name="pawn">The resident.</param>
        /// <param name="cell">Where to put them.</param>
        /// <returns>True if they were moved.</returns>
        private bool Place(Pawn pawn, IntVec3 cell)
        {
            if (!cell.InBounds(Map) || !cell.Standable(Map))
            {
                return false;
            }

            pawn.jobs?.StopAll();
            pawn.Position = cell;
            pawn.Notify_Teleported();
            return true;
        }

        /// <summary>A town-local cell on the map.</summary>
        /// <param name="local">The cell, town-local.</param>
        /// <returns>The map cell.</returns>
        private IntVec3 MapCell(Cell local) =>
            AnyRim.ToMap(Stage.AnyRimAnchor, local);
    }
}
