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

        /// <summary>
        /// The closest zoom on the throw: the shot pulls back from it only
        /// as far as Ned, the raccoon and the grenade need.
        /// </summary>
        public float throwZoom = 20f;

        /// <summary>The farthest the throw's shot pulls back.</summary>
        public float throwMaxZoom = 34f;

        /// <summary>Tiles kept round Ned, the raccoon and the grenade.</summary>
        public float throwMargin = 4f;

        /// <summary>The zoom on the welcome sign for the census.</summary>
        public float signZoom = 16f;

        /// <summary>
        /// Ticks after the raccoon comes before Ned, if he has not thrown,
        /// leaves his post and goes after it.
        /// </summary>
        public int pursueTicks = 900;

        /// <summary>
        /// How often the scene logs why the throw is still waiting.
        /// </summary>
        private const int WaitNoteTicks = 600;

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

        /// <summary>
        /// Whether this scene has given Ned orders, so it stands him down
        /// at the end.
        /// </summary>
        [Unsaved]
        private bool ordered;

        /// <summary>The scene tick the raccoon came, or -1.</summary>
        [Unsaved]
        private int pestAt = -1;

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

        /// <summary>
        /// Gets the raccoon, once staged: the hostile this run staged.
        /// </summary>
        internal override IEnumerable<Pawn> StagedHostiles =>
            raccoon == null ? Enumerable.Empty<Pawn>() : new[] { raccoon };

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
            ordered = false;
            pestAt = -1;
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
                    (IntVec3 centre, float zoom) = ThrowFraming();
                    look = centre;
                    BeatOnce(TourScript.Thrown, Cut(zoom), select: false);
                }
                else if (Elapsed % OrderCheckTicks == 0)
                {
                    KeepNedOnTask();
                }

                if (pestAt >= 0 && Elapsed > pestAt
                    && (Elapsed - pestAt) % WaitNoteTicks == 0)
                {
                    NoteTheWait();
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

            if (ordered && ned != null && !ned.Dead && !ned.Destroyed
                && ned.jobs != null)
            {
                ned.jobs.ClearQueuedJobs();
                if (Attacking(ned.CurJob) || Sent(ned.CurJob))
                {
                    ned.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
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
                // Up Main Street to the Damages' gate first: left to path
                // straight to her, it went through the yards, chewing the
                // picket fences, and stalled at Town Hall's.
                Job bite = JobMaker.MakeJob(JobDefOf.AttackMelee, constance);
                bite.canBashDoors = true;
                bite.canBashFences = true;
                bite.attackDoorIfTargetLost = true;
                bite.playerForced = true;
                IntVec3 gate = MapCell(RaccoonIncident.DamageGate);
                if (gate.Standable(Map))
                {
                    Job run = JobMaker.MakeJob(JobDefOf.Goto, gate);
                    run.locomotionUrgency = LocomotionUrgency.Sprint;
                    run.playerForced = true;
                    raccoon.jobs.StartJob(run, JobCondition.InterruptForced);
                    raccoon.jobs.jobQueue.EnqueueFirst(bite);
                }
                else
                {
                    raccoon.jobs.StartJob(bite, JobCondition.InterruptForced);
                }
            }

            pestAt = Elapsed;
            OrderNed(MapCell(RaccoonIncident.GuardPost));
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
        /// Gives Ned his orders: sprint to a cell (when given and he is not
        /// on it), then throw at the raccoon once, from where he stands, as
        /// soon as it is in range and in sight. Forced, so his household's
        /// duty does not call him back.
        /// </summary>
        /// <param name="walkTo">Where to go first, or Invalid to stay.</param>
        private void OrderNed(IntVec3 walkTo)
        {
            if (!OnDuty(ned) || raccoon == null || ned.jobs == null)
            {
                return;
            }

            ordered = true;
            Job attack = JobMaker.MakeJob(JobDefOf.AttackStatic, raccoon);
            attack.maxNumStaticAttacks = 1;
            attack.playerForced = true;
            ned.jobs.ClearQueuedJobs();
            if (walkTo.IsValid && ned.Position != walkTo && walkTo.Standable(Map))
            {
                Job goTo = JobMaker.MakeJob(JobDefOf.Goto, walkTo);
                goTo.locomotionUrgency = LocomotionUrgency.Sprint;
                goTo.playerForced = true;
                ned.jobs.StartJob(goTo, JobCondition.InterruptForced);
                ned.jobs.jobQueue.EnqueueFirst(attack);
            }
            else
            {
                ned.jobs.StartJob(attack, JobCondition.InterruptForced);
            }
        }

        /// <summary>
        /// Keeps Ned to his throw. Before <see cref="pursueTicks"/> have
        /// passed, he is put back to it if something else (a raccoon at his
        /// ankles, a stray thought) took him off it. After that, if the
        /// raccoon is still out of his range or sight, he goes after it:
        /// a sprint to where it is now, the throw queued behind it.
        /// </summary>
        private void KeepNedOnTask()
        {
            if (!OnDuty(ned) || raccoon == null || !raccoon.Spawned)
            {
                return;
            }

            bool onOrders = Attacking(ned.CurJob)
                || (Sent(ned.CurJob) && ned.jobs.jobQueue.Any(q => Attacking(q.job)));
            if (pestAt < 0 || Elapsed - pestAt < pursueTicks)
            {
                if (!onOrders)
                {
                    OrderNed(IntVec3.Invalid);
                }

                return;
            }

            bool inReach = ned.Position.InHorDistOf(
                    raccoon.Position, (float)RaccoonIncident.ThrowRange - 1f)
                && GenSight.LineOfSight(ned.Position, raccoon.Position, Map);
            if (!inReach)
            {
                OrderNed(raccoon.Position);
            }
            else if (!Attacking(ned.CurJob))
            {
                OrderNed(IntVec3.Invalid);
            }
        }

        /// <summary>Whether a job is Ned's throw at the raccoon.</summary>
        /// <param name="job">The job, or null.</param>
        /// <returns>True for a forced AttackStatic at the raccoon.</returns>
        private bool Attacking(Job job) =>
            job != null && job.def == JobDefOf.AttackStatic
            && job.playerForced && ReferenceEquals(job.targetA.Thing, raccoon);

        /// <summary>Whether a job is a sprint this scene sent Ned on.</summary>
        /// <param name="job">The job, or null.</param>
        /// <returns>True for a forced Goto.</returns>
        private static bool Sent(Job job) =>
            job != null && job.def == JobDefOf.Goto && job.playerForced;

        /// <summary>
        /// Logs why the throw is still waiting: where the raccoon and Ned
        /// are and what each is doing, the distance and the line of sight.
        /// </summary>
        private void NoteTheWait()
        {
            string racc = raccoon == null ? "none"
                : $"{Fate(raccoon)} at {raccoon.PositionHeld}, "
                    + $"{raccoon.CurJobDef?.defName ?? "no job"}, "
                    + $"{raccoon.MentalStateDef?.defName ?? "calm"}";
            string guard = ned == null ? "none"
                : $"{Fate(ned)} at {ned.PositionHeld}, "
                    + $"{ned.CurJobDef?.defName ?? "no job"}, holding "
                    + $"{ned.equipment?.Primary?.def.defName ?? "nothing"}";
            string apart = raccoon != null && ned != null && raccoon.Spawned
                && ned.Spawned
                    ? $"{ned.Position.DistanceTo(raccoon.Position):0.0} tiles, sight "
                        + GenSight.LineOfSight(ned.Position, raccoon.Position, Map)
                    : "not both on the map";
            Log.Message($"{ShowcaseLog.Prefix} raccoon: still waiting for the "
                + $"throw at scene tick {Elapsed}: raccoon {racc}; Ned {guard}; "
                + apart);
        }

        /// <summary>
        /// The things the throw's shot must show: Ned, the raccoon, and
        /// Ned's grenade in the air and where it is going, each while it is
        /// on the map. The tour verifier checks the same things.
        /// </summary>
        /// <returns>Their positions, in tiles (a cell's centre).</returns>
        internal List<Spot> ThrowSubjects()
        {
            var spots = new List<Spot>();
            if (Alive(ned))
            {
                spots.Add(new Spot(ned.DrawPos.x, ned.DrawPos.z));
            }

            if (Alive(raccoon))
            {
                spots.Add(new Spot(raccoon.DrawPos.x, raccoon.DrawPos.z));
            }

            foreach (Projectile p in Map.listerThings
                .ThingsInGroup(ThingRequestGroup.Projectile).OfType<Projectile>()
                .Where(p => p.def.defName == Showcase.GrenadeProjectileDef
                    && ReferenceEquals(p.Launcher, ned)))
            {
                spots.Add(new Spot(p.ExactPosition.x, p.ExactPosition.z));
                if (p.usedTarget.IsValid)
                {
                    UnityEngine.Vector3 to = p.usedTarget.CenterVector3;
                    spots.Add(new Spot(to.x, to.z));
                }
            }

            return spots;
        }

        /// <summary>
        /// Where the throw's shot looks and how close: Ned, the raccoon and
        /// the grenade all in the part of the screen the caption bar and
        /// the narration leave clear (Core's <see cref="ShotFraming.Fit"/>;
        /// slice 19000d: the shot had pulled back with all three under the
        /// caption bar). With none of them on the map, the raccoon's last
        /// place or the guard post, at the closest zoom.
        /// </summary>
        /// <returns>The cell to look at and the zoom.</returns>
        private (IntVec3 Centre, float Zoom) ThrowFraming()
        {
            IntVec3 fallback = Alive(raccoon) ? raccoon.Position
                : MapCell(RaccoonIncident.GuardPost);
            List<Spot> spots = ThrowSubjects();
            if (spots.Count == 0 || UI.screenHeight <= 0)
            {
                return (fallback, throwZoom);
            }

            (Spot centre, double zoom) = ShotFraming.Fit(
                spots, (double)UI.screenWidth / UI.screenHeight, throwMargin,
                throwZoom, System.Math.Max(throwZoom, throwMaxZoom));
            var cell = new IntVec3((int)System.Math.Floor(centre.X), 0,
                (int)System.Math.Floor(centre.Z));
            return (cell.ClampInsideMap(Map), (float)zoom);
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
