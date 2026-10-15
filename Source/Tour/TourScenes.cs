// Batshit Technology - the directed tour's scenes, part one.
// The base class every tour scene shares, and the scenes of the demo
// base, the pads, AnyRim and the grenade (docs/SHOWCASE.md, "The Directed
// Tour"; .local plan section 2). Every wait is a world-state condition
// with the def's timeout behind it; beats (ShowcaseScene.Beat) mark the
// moments the narration speaks to. Per-run state is set in Setup,
// because CloneForRun is a shallow copy of the def's scene.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// A scene of Batshit Technology's tour: it reads the director's
    /// <see cref="TourStage"/>, knows when it started, plays each beat at
    /// most once, and can move colonists clear of what it is about to do.
    /// </summary>
    public abstract class TourScene : ShowcaseScene
    {
        /// <summary>
        /// The earliest tick on the scene's own clock its first beat plays.
        /// The clock starts after the scene's intro (<see cref="IntroTicks"/>),
        /// so the beat comes once the start lines have been read.
        /// </summary>
        public const int FirstBeatTick = 150;

        /// <summary>The game tick this run of the scene began, or -1.</summary>
        [Unsaved]
        private int startTick = -1;

        /// <summary>
        /// Ticks this run holds its own action after it begins, while its
        /// start lines are read (Core's <see cref="TourPacing.IntroTicks"/>
        /// over the def's start lines).
        /// </summary>
        [Unsaved]
        private int introTicks;

        /// <summary>The beats this run has played, by moment.</summary>
        [Unsaved]
        private HashSet<string> played;

        /// <summary>
        /// When this run notes a lost guest: once, and not in the intro
        /// (Core's <see cref="GuestLossCue"/>).
        /// </summary>
        [Unsaved]
        private GuestLossCue guestLoss;

        /// <summary>
        /// Gets the tour's stage; empty (never null) outside a tour.
        /// </summary>
        public TourStage Stage =>
            (Director as BatshitTourDirector)?.Stage ?? TourStage.Empty;

        /// <summary>
        /// Scene keys the tour verifier forces to decline (its
        /// -bttourskip argument), so a verify run exercises the API's
        /// declining (G1) on any map. Empty outside that run.
        /// </summary>
        internal static readonly HashSet<string> SkippedForTest =
            new HashSet<string>();

        /// <summary>Gets the game tick this run began, or -1 before it.</summary>
        public int StartTick => startTick;

        /// <summary>
        /// Gets why the scene cannot run on this map: skipped by the tour
        /// verifier, or the scene's own reason (<see cref="WhyUnavailable"/>).
        /// </summary>
        public sealed override string UnavailableReason =>
            key != null && SkippedForTest.Contains(key)
                ? "skipped for the tour verifier"
                : WhyUnavailable;

        /// <summary>
        /// Gets why this scene cannot run on this map, or null when it can.
        /// </summary>
        protected virtual string WhyUnavailable => null;

        /// <summary>
        /// Gets the hostile pawns this run staged (a herd, a raccoon, a
        /// party of raiders): the only hostiles the tour verifier lets
        /// appear after READY. None by default.
        /// </summary>
        internal virtual IEnumerable<Pawn> StagedHostiles =>
            Enumerable.Empty<Pawn>();

        /// <summary>
        /// Gets the scene's own clock: ticks since its intro ended,
        /// negative during the intro, and 0 before the run begins. Every
        /// beat and action is timed on it, so the intro shifts them all.
        /// </summary>
        protected int Elapsed => startTick < 0
            ? 0
            : Find.TickManager.TicksGame - startTick - introTicks;

        /// <summary>
        /// Gets whether the scene is still in its intro: begun, with its
        /// start lines still being read. Its action waits.
        /// </summary>
        protected bool InIntro => startTick >= 0 && Elapsed < 0;

        /// <summary>
        /// Gets the ticks this run holds its action for its start lines.
        /// </summary>
        public int IntroTicks => introTicks;

        /// <summary>
        /// Starts this run: records the start tick, works out the intro
        /// from the def's start lines, forgets the beats, and lets the
        /// scene build (<see cref="SetupScene"/>).
        /// </summary>
        public sealed override void Setup()
        {
            startTick = Find.TickManager.TicksGame;
            introTicks = TourPacing.IntroTicks(StartLines(), FirstBeatTick);
            played = new HashSet<string>();
            held = new List<Pawn>();
            guestLoss = new GuestLossCue();
            SetupScene();
            TourGroup group = Stage.Tourists;
            if (group != null && !group.Dismissed)
            {
                PlaceTourists(group);
            }
        }

        /// <summary>
        /// Walks the tour group to where this scene wants it, after the
        /// scene is built. By default every tourist waits at the rest spot
        /// in the base; the stampede, the pads, the grenade and the
        /// tractor put theirs where the scene can reach them
        /// (docs/SHOWCASE.md, "Tourists").
        /// </summary>
        /// <param name="group">The tour group, not yet dismissed.</param>
        protected virtual void PlaceTourists(TourGroup group)
        {
            group.RestAll();
        }

        /// <summary>
        /// A tourist was lost while this scene showed: plays its
        /// guestsLost beat once, without moving the camera, if the scene
        /// narrates it (Core's <see cref="TourScript.NarratesGuestsLost"/>);
        /// in the intro, the beat waits for the intro's end
        /// (<see cref="GuestLostAfterIntro"/>).
        /// </summary>
        internal void GuestLost()
        {
            if (played != null && guestLoss != null
                && TourScript.NarratesGuestsLost(key) && guestLoss.Lost(InIntro))
            {
                BeatOnce(TourScript.GuestsLost, null, select: false);
            }
        }

        /// <summary>
        /// Plays a guestsLost beat held back through the intro, on the
        /// first tick after it; the director asks every tick.
        /// </summary>
        internal void GuestLostAfterIntro()
        {
            if (played != null && guestLoss != null && guestLoss.Due(InIntro))
            {
                BeatOnce(TourScript.GuestsLost, null, select: false);
            }
        }

        /// <summary>
        /// This scene's start lines in the def, with their delays.
        /// </summary>
        /// <returns>The lines; empty outside a def.</returns>
        private IEnumerable<(int Delay, string Text)> StartLines() =>
            (Director?.def?.narration ?? new List<ShowcaseNarrationLine>())
            .Where(l => l != null && l.scene == key
                && l.when == NarrationScript.Start)
            .Select(l => (l.delayTicks, l.text));

        /// <summary>Builds what this scene needs; runs once per run.</summary>
        protected virtual void SetupScene()
        {
        }

        /// <summary>Whether a beat has played in this run.</summary>
        /// <param name="moment">The moment.</param>
        /// <returns>True once it has.</returns>
        protected bool Played(string moment) =>
            played != null && played.Contains(moment);

        /// <summary>
        /// Plays a beat once per run: later calls for the same moment do
        /// nothing.
        /// </summary>
        /// <param name="moment">The narration moment.</param>
        /// <param name="shot">The camera move, or null to leave it.</param>
        /// <param name="select">Whether to select the subject.</param>
        /// <returns>True if this call played it.</returns>
        protected bool BeatOnce(
            string moment, ShowcaseShot shot = null, bool select = true)
        {
            if (played == null || !played.Add(moment))
            {
                return false;
            }

            return Beat(moment, shot, select);
        }

        /// <summary>
        /// A camera move for a beat: a glide, kept on the map, so a shot
        /// near an edge never shows the black beyond it (slice 19000d).
        /// </summary>
        /// <param name="zoom">The zoom.</param>
        /// <param name="x">Cells east of the focus.</param>
        /// <param name="z">Cells north of the focus.</param>
        /// <returns>The shot.</returns>
        protected static ShowcaseShot Shot(float zoom, int x = 0, int z = 0) =>
            new ShowcaseShot
            {
                zoom = zoom, offset = new IntVec2(x, z), keepOnMap = true,
            };

        /// <summary>
        /// A camera cut for a beat whose action is quick (a throw): kept on
        /// the map, and there at once, so the moment is not spent gliding.
        /// </summary>
        /// <param name="zoom">The zoom.</param>
        /// <returns>The shot.</returns>
        protected static ShowcaseShot Cut(float zoom)
        {
            ShowcaseShot shot = Shot(zoom);
            shot.pan = false;
            return shot;
        }

        /// <summary>
        /// Moves colonists within a radius of a danger back to the base.
        /// </summary>
        /// <param name="danger">Where the danger is.</param>
        /// <param name="radius">How far out counts as in danger.</param>
        protected void MoveColonistsClear(IntVec3 danger, float radius) =>
            Showcase.MoveColonistsClear(Map, danger, radius);

        /// <summary>
        /// How often a scene that holds its colonists clear checks that
        /// none has come back into the danger, in ticks.
        /// </summary>
        protected const int KeepClearTicks = 30;

        /// <summary>The colonists this run drafted to hold them clear.</summary>
        [Unsaved]
        private List<Pawn> held;

        /// <summary>
        /// Holds the colonists clear of a danger for the scene: any inside
        /// it is moved back to the base, and every free colonist but those
        /// spared is drafted with Fire At Will off, so no one walks back in
        /// to haul, to look or to fight. <see cref="ReleaseHeldColonists"/>
        /// lets them go at the scene's end.
        /// </summary>
        /// <param name="inDanger">Whether a cell is in danger.</param>
        /// <param name="spare">Colonists the scene places itself.</param>
        protected void HoldColonistsClear(
            System.Func<IntVec3, bool> inDanger, params Pawn[] spare)
        {
            held ??= new List<Pawn>();
            Showcase.MoveColonistsOut(Map, inDanger, spare);
            foreach (Pawn p in Map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (spare.Contains(p) || held.Contains(p) || p.drafter == null
                    || p.Downed || p.InMentalState || p.Drafted)
                {
                    continue;
                }

                p.drafter.Drafted = true;
                p.drafter.FireAtWill = false; // the Drafted setter turns it on
                held.Add(p);
            }
        }

        /// <summary>
        /// Every <see cref="KeepClearTicks"/>, moves back to the base any
        /// colonist but those spared that has come into the danger (one
        /// that could not be drafted, say), and logs it.
        /// </summary>
        /// <param name="inDanger">Whether a cell is in danger.</param>
        /// <param name="spare">Colonists the scene places itself.</param>
        protected void KeepColonistsClear(
            System.Func<IntVec3, bool> inDanger, params Pawn[] spare)
        {
            if (Find.TickManager.TicksGame % KeepClearTicks != 0)
            {
                return;
            }

            int moved = Showcase.MoveColonistsOut(Map, inDanger, spare);
            if (moved > 0)
            {
                Log.Message($"{ShowcaseLog.Prefix} {key}: moved {moved} "
                    + "colonist(s) back out of the danger");
            }
        }

        /// <summary>
        /// Undrafts the colonists <see cref="HoldColonistsClear"/> drafted,
        /// with Fire At Will back on.
        /// </summary>
        protected void ReleaseHeldColonists()
        {
            foreach (Pawn p in held ?? new List<Pawn>())
            {
                if (Alive(p) && p.drafter != null && p.Drafted)
                {
                    p.drafter.FireAtWill = true;
                    p.drafter.Drafted = false;
                }
            }

            held?.Clear();
        }

        /// <summary>Whether a thing is on the map and whole.</summary>
        /// <param name="thing">The thing, or null.</param>
        /// <returns>True if it is spawned and not destroyed.</returns>
        protected static bool Alive(Thing thing) =>
            thing != null && thing.Spawned && !thing.Destroyed;

        /// <summary>
        /// Whether one of the mod's projectiles (a bolt, the grenade) is
        /// still in flight on the scene's map.
        /// </summary>
        /// <returns>True while one flies.</returns>
        protected bool BatshitProjectileInFlight() =>
            Map != null && Map.listerThings
                .ThingsInGroup(ThingRequestGroup.Projectile)
                .Any(t => t.def.defName.StartsWith("BT_"));

        /// <summary>
        /// Whether an implosion is still running on the map: one the field
        /// holds that is open and ticked this tick or the last.
        /// </summary>
        /// <remarks>
        /// The field's Active list is pruned only when an implosion pulls,
        /// so after the last one ends it still names it; a count of that
        /// list never falls to zero, and a scene waiting on it times out.
        /// </remarks>
        /// <returns>True while one runs.</returns>
        protected bool ImplosionActive() => RunningImplosions().Any();

        /// <summary>
        /// Whether a singularity grenade's field is on the map: from the
        /// tick the grenade lands until the survivors have been comforted,
        /// after its explosion.
        /// </summary>
        /// <remarks>
        /// The field spawns as the projectile is destroyed, and its
        /// implosion joins the map's field only on its first tick, so on
        /// the landing tick neither a projectile nor a running implosion
        /// shows. A scene that waited on those alone could take that tick
        /// for the end of the singularity, and end before its explosion.
        /// </remarks>
        /// <returns>True while one is on the map.</returns>
        protected bool SingularityOpen() =>
            Map != null && Map.listerThings
                .ThingsOfDef(BT_SingularityDefOf.BT_SingularityField).Count > 0;

        /// <summary>The implosions running on the map now.</summary>
        /// <returns>Each open implosion that ticked this tick or the last.</returns>
        protected IEnumerable<Implosion> RunningImplosions()
        {
            int now = Find.TickManager.TicksGame;
            return (MapComponent_Implosions.For(Map)?.Active
                    ?? (IReadOnlyList<Implosion>)new List<Implosion>())
                .Where(i => i != null && !i.Closed && i.LastTickedAt >= now - 1);
        }
    }

    /// <summary>
    /// Scene 1, "The Home of Tomorrow": a beat per device of the base,
    /// every <see cref="beatInterval"/> ticks, each selected and close
    /// up; the replicator replicates on cue and the squeezer gets a
    /// customer if it has none. Devices that are missing are skipped.
    /// </summary>
    public class DeviceParadeScene : TourScene
    {
        /// <summary>Ticks between the devices' beats.</summary>
        public int beatInterval = 420;

        /// <summary>The zoom on each device.</summary>
        public float deviceZoom = 14f;

        /// <summary>This run's devices, in order, by moment.</summary>
        [Unsaved]
        private List<(string Moment, Thing Device)> parade;

        /// <summary>The next device's index in the parade.</summary>
        [Unsaved]
        private int next;

        /// <summary>The device on screen.</summary>
        [Unsaved]
        private Thing shown;

        /// <summary>Gets the device on screen.</summary>
        public override Thing Subject => Alive(shown) ? shown : null;

        /// <summary>
        /// Gets the beats this run planned at setup, one per device that
        /// stood then (the tour verifier requires each of them).
        /// </summary>
        internal IReadOnlyList<string> PlannedMoments =>
            parade?.Select(p => p.Moment).ToList() ?? new List<string>();

        /// <summary>Gets why the scene cannot run: the base has no device.</summary>
        protected override string WhyUnavailable =>
            Devices().Count > 0 ? null : "the base has no devices";

        /// <summary>Finds the base's devices, in parade order.</summary>
        protected override void SetupScene()
        {
            parade = Devices();
            next = 0;
            shown = parade.Count > 0 ? parade[0].Device : null;
        }

        /// <summary>Plays the next device's beat when its time comes.</summary>
        public override void Tick()
        {
            if (parade == null || next >= parade.Count
                || Elapsed < FirstBeatTick + next * beatInterval)
            {
                return;
            }

            (string moment, Thing device) = parade[next++];
            if (!Alive(device))
            {
                return;
            }

            shown = device;
            Act(moment, device);
            BeatOnce(moment, Shot(deviceZoom));
        }

        /// <summary>
        /// Done once every device has had its beat and the last has had
        /// as long on screen as the others.
        /// </summary>
        /// <returns>True when the parade is over.</returns>
        public override bool IsDone() =>
            parade != null && next >= parade.Count
            && Elapsed >= FirstBeatTick + parade.Count * beatInterval;

        /// <summary>The base's devices that stand, in parade order.</summary>
        /// <returns>The devices, by moment.</returns>
        private List<(string Moment, Thing Device)> Devices()
        {
            TourStage stage = Stage;
            return new List<(string Moment, Thing Device)>
            {
                ("reactor", stage.Reactor),
                ("replicator", stage.Replicator),
                ("transmogrifier", stage.Transmogrifier),
                ("squeezer", stage.Squeezer),
            }.Where(d => Alive(d.Device)).ToList();
        }

        /// <summary>
        /// Shows a device at work: the replicator replicates, and the
        /// squeezer gets a colonist if no one is in it.
        /// </summary>
        /// <param name="moment">The device's moment.</param>
        /// <param name="device">The device.</param>
        private void Act(string moment, Thing device)
        {
            if (moment == "replicator")
            {
                device.TryGetComp<CompRandomReplicator>()?.ReplicateNow();
            }
            else if (moment == "squeezer" && !Map.mapPawns.FreeColonistsSpawned
                .Any(p => p.CurJobDef == BT_BrainSqueezerDefOf.BT_UseBrainSqueezer))
            {
                Showcase.SendColonistToSqueezer(Map, device);
            }
        }
    }

    /// <summary>
    /// Scene 2, "Home Defence": the Artificer, then the Ballista fires one
    /// 16-bolt volley at open ground and holds its fire again. Done once
    /// the burst is over and no bolt is in the air.
    /// </summary>
    public class BallistaVolleyScene : TourScene
    {
        /// <summary>When the Ballista is ordered to fire, in ticks.</summary>
        public int orderTick = 420;

        /// <summary>Whether the attack has been ordered.</summary>
        [Unsaved]
        private bool ordered;

        /// <summary>Whether the burst has been seen.</summary>
        [Unsaved]
        private bool burstSeen;

        /// <summary>Whether the burst has ended.</summary>
        [Unsaved]
        private bool burstOver;

        /// <summary>
        /// Gets the Artificer until the attack is ordered, then the
        /// Ballista.
        /// </summary>
        public override Thing Subject =>
            !ordered && Alive(Stage.Artificer) ? Stage.Artificer : Stage.Ballista;

        /// <summary>Gets why the scene cannot run: no Ballista.</summary>
        protected override string WhyUnavailable =>
            Alive(Stage.Ballista) && Stage.BallistaTarget.IsValid
                ? null
                : "the base has no Ballista";

        /// <summary>Starts the run with nothing fired.</summary>
        protected override void SetupScene()
        {
            ordered = false;
            burstSeen = false;
            burstOver = false;
        }

        /// <summary>
        /// The Artificer's beat; the order to fire; the volley's beat when
        /// the burst begins; and the hold once it ends.
        /// </summary>
        public override void Tick()
        {
            if (Elapsed >= FirstBeatTick && Alive(Stage.Artificer))
            {
                BeatOnce("artificer", Shot(14f));
            }

            Building_BatshittiumBallista ballista = Stage.Ballista;
            if (!Alive(ballista))
            {
                return;
            }

            if (!ordered && Elapsed >= orderTick)
            {
                ordered = true;
                ballista.SetRange(Showcase.VolleyRange);
                ballista.OrderAttack(new LocalTargetInfo(Stage.BallistaTarget));
            }

            if (ordered && !burstSeen && ballista.InBurst)
            {
                burstSeen = true;
                BeatOnce("volley", Shot(40f, 30, 0));
            }
            else if (burstSeen && !burstOver && !ballista.InBurst)
            {
                burstOver = true;
                HoldFire(ballista);
            }
        }

        /// <summary>Done once the burst is over and every bolt has landed.</summary>
        /// <returns>True when the volley has played out.</returns>
        public override bool IsDone() => burstOver && !BatshitProjectileInFlight();

        /// <summary>Holds the Ballista's fire however the scene ended.</summary>
        /// <param name="ending">How it ended.</param>
        public override void End(SceneEnd ending)
        {
            HoldFire(Stage.Ballista);
        }

        /// <summary>
        /// Holds the Ballista's fire: no target of its own, its range at
        /// the least, then its ordered target cleared (in that order, so
        /// the turret's immediate search on clearing finds nothing).
        /// </summary>
        /// <param name="ballista">The Ballista, or null.</param>
        internal static void HoldFire(Building_BatshittiumBallista ballista)
        {
            if (Alive(ballista))
            {
                ballista.AutoTargetHeld = true;
                ballista.SetRange(BallistaPower.MinRange);
                ballista.OrderAttack(LocalTargetInfo.Invalid);
            }
        }
    }

    /// <summary>
    /// Scenes 3 and 4, "When Devices Retire": the lone device on a pad
    /// arms its self-destruct with the camera on it, and the scene holds
    /// until it has gone off and its aftermath (fires, secondaries, the
    /// reactor's barrage) is over. The tour group's sightseers take turns
    /// to look at it from close by, and stay for the look.
    /// </summary>
    public class SelfDestructScene : TourScene
    {
        /// <summary>Which pad: 0 for the replicator, 1 for the reactor.</summary>
        public int pad;

        /// <summary>When the device arms, in ticks.</summary>
        public int armTick = 60;

        /// <summary>The colonists' margin beyond the blast, in tiles.</summary>
        public float margin = 8f;

        /// <summary>The device on the pad.</summary>
        [Unsaved]
        private Thing device;

        /// <summary>Where the device stood.</summary>
        [Unsaved]
        private IntVec3 padCell = IntVec3.Invalid;

        /// <summary>Whether it has been armed.</summary>
        [Unsaved]
        private bool armed;

        /// <summary>Whether it has gone off.</summary>
        [Unsaved]
        private bool blasted;

        /// <summary>The tour group's sightseers looking at this pad.</summary>
        [Unsaved]
        private List<Pawn> visiting = new List<Pawn>();

        /// <summary>Gets the device while it stands.</summary>
        public override Thing Subject => Alive(device) ? device : null;

        /// <summary>Gets the device, or where it stood.</summary>
        public override IntVec3 FocusCell =>
            Alive(device) ? device.Position
                : padCell.IsValid ? padCell : base.FocusCell;

        /// <summary>Gets why the scene cannot run: no pad for it.</summary>
        protected override string WhyUnavailable =>
            PadDevice()?.TryGetComp<CompSelfDestruct>() != null
                ? null
                : "no pad for it clears AnyRim and the yard";

        /// <summary>
        /// Finds the device and holds the colonists clear of everything
        /// it can reach, until its aftermath is over.
        /// </summary>
        protected override void SetupScene()
        {
            device = PadDevice();
            padCell = device?.Position ?? IntVec3.Invalid;
            armed = false;
            blasted = false;
            ClearBlast();
            HoldColonistsClear(InReach);
        }

        /// <summary>
        /// Arms the device on time; plays the blast when it goes; keeps
        /// every colonist out of its reach until its aftermath is over (a
        /// hauler once walked back into the reactor's barrage).
        /// </summary>
        public override void Tick()
        {
            KeepColonistsClear(InReach);
            if (armed && !blasted)
            {
                Stage.Tourists?.StayForTheLook(visiting);
            }

            if (!armed && Elapsed >= armTick && Alive(device))
            {
                ClearBlast();
                CompSelfDestruct comp = device.TryGetComp<CompSelfDestruct>();
                if (comp != null && !comp.IsCountingDown)
                {
                    comp.Arm();
                }

                armed = true;
            }

            if (armed && !blasted && (device == null || device.Destroyed))
            {
                blasted = true;
                BeatOnce("blast", Shot(60f), select: false);
            }
        }

        /// <summary>
        /// Done once the device has gone off and its aftermath is over.
        /// </summary>
        /// <returns>True when the retirement has played out.</returns>
        public override bool IsDone() =>
            blasted
            && Map?.GetComponent<MapComponent_SelfDestructAftermath>()?.Active != true;

        /// <summary>Lets the colonists go however the scene ended.</summary>
        /// <param name="ending">How it ended.</param>
        public override void End(SceneEnd ending)
        {
            ReleaseHeldColonists();
        }

        /// <summary>
        /// Walks this pad's sightseers (they take turns between the two
        /// pads, Core's <see cref="TourGroupPlan.PadFor"/>) to look at the
        /// device from <see cref="TourGroupPlan.PadViewTiles"/> tiles, on
        /// the side towards the base, before it arms; the rest wait in
        /// the base. Once it is armed they stay for the look (the device
        /// sends everyone near it running each second; the scene calls
        /// them back, <see cref="TourGroup.StayForTheLook"/>).
        /// </summary>
        /// <param name="group">The tour group.</param>
        protected override void PlaceTourists(TourGroup group)
        {
            visiting = new List<Pawn>();
            IReadOnlyList<Pawn> sightseers = group.Sightseers;
            for (int i = 0; i < sightseers.Count && padCell.IsValid; i++)
            {
                if (TourGroupPlan.PadFor(i) == pad)
                {
                    visiting.Add(sightseers[i]);
                }
            }

            IntVec3 view = padCell.IsValid && Stage.BaseCentre.IsValid
                ? AnyRim.ToMap(IntVec3.Zero, TourGroupPlan.PadView(
                    AnyRim.ToCell(padCell), AnyRim.ToCell(Stage.BaseCentre)))
                : IntVec3.Invalid;
            foreach (Pawn p in visiting)
            {
                group.Send(p, view, TourGroupPlan.RestRadius);
            }

            group.RestAll(visiting.ToArray());
        }

        /// <summary>
        /// Whether a cell is within the pad's reach: its blast, fires,
        /// secondaries and barrage, with the planner's margin.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <returns>True if the retirement could reach it.</returns>
        private bool InReach(IntVec3 cell) =>
            padCell.IsValid
            && cell.InHorDistOf(padCell, (float)Showcase.DemoCornerReach);

        /// <summary>This scene's pad device, if it stands.</summary>
        /// <returns>The device, or null.</returns>
        private Thing PadDevice()
        {
            IReadOnlyList<Thing> pads = Stage.Pads;
            Thing thing = pad >= 0 && pads != null && pad < pads.Count
                ? pads[pad]
                : null;
            return Alive(thing) ? thing : null;
        }

        /// <summary>Moves colonists clear of the device's blast.</summary>
        private void ClearBlast()
        {
            CompSelfDestruct comp = device?.TryGetComp<CompSelfDestruct>();
            if (comp != null && padCell.IsValid)
            {
                MoveColonistsClear(padCell, comp.Props.blastRadius + margin);
            }
        }
    }

    /// <summary>
    /// Scene 5, "A Typical Rim Community": AnyRim's welcome sign with the
    /// town above it, then its crossroads, then its farthest building.
    /// </summary>
    public class AnyRimWalkScene : TourScene
    {
        /// <summary>When the camera moves to the crossroads, in ticks.</summary>
        public int streetTick = 720;

        /// <summary>When it moves to the farthest building, in ticks.</summary>
        public int homesTick = 1290;

        /// <summary>How long the last view holds, in ticks.</summary>
        public int holdTicks = 300;

        /// <summary>Where the camera looks now.</summary>
        [Unsaved]
        private IntVec3 look = IntVec3.Invalid;

        /// <summary>The crossroads.</summary>
        [Unsaved]
        private IntVec3 street = IntVec3.Invalid;

        /// <summary>The farthest building's centre.</summary>
        [Unsaved]
        private IntVec3 homes = IntVec3.Invalid;

        /// <summary>Gets where the camera looks now.</summary>
        public override IntVec3 FocusCell =>
            look.IsValid ? look : base.FocusCell;

        /// <summary>Gets why the scene cannot run: AnyRim was skipped.</summary>
        protected override string WhyUnavailable =>
            Stage.HasAnyRim
            && Map?.GetComponent<MapComponent_AnyRim>()?.Built == true
                ? null
                : "AnyRim was not built on this map";

        /// <summary>Plans the walk: the sign, the crossroads, the far homes.</summary>
        protected override void SetupScene()
        {
            Map map = Map;
            MapComponent_AnyRim record = map.GetComponent<MapComponent_AnyRim>();
            IntVec3 anchor = Stage.AnyRimAnchor;
            IntVec3 sign = record != null && record.WelcomeSign.IsValid
                ? record.WelcomeSign
                : anchor;
            look = new IntVec3(
                anchor.x, 0,
                System.Math.Min(sign.z + Showcase.TourLookNorth, map.Size.z - 1));
            street = anchor;
            homes = FarthestBuilding(anchor) ?? anchor;
        }

        /// <summary>Plays the sign, street and homes beats on time.</summary>
        public override void Tick()
        {
            if (Elapsed >= FirstBeatTick)
            {
                BeatOnce("sign", null, select: false);
            }

            if (Elapsed >= streetTick && !Played("street"))
            {
                look = street;
                BeatOnce("street", Shot(26f), select: false);
            }

            if (Elapsed >= homesTick && !Played("homes"))
            {
                look = homes;
                BeatOnce("homes", Shot(26f), select: false);
            }
        }

        /// <summary>Done once the last view has held.</summary>
        /// <returns>True when the walk is over.</returns>
        public override bool IsDone() =>
            Played("homes") && Elapsed >= homesTick + holdTicks;

        /// <summary>
        /// The centre of AnyRim's building farthest from its crossroads.
        /// </summary>
        /// <param name="anchor">The crossroads.</param>
        /// <returns>The cell, or null if the plan has no buildings.</returns>
        private IntVec3? FarthestBuilding(IntVec3 anchor)
        {
            TownPlan plan = Stage.AnyRimPlan;
            if (plan?.Buildings == null || plan.Buildings.Count == 0)
            {
                return null;
            }

            IntVec3 best = IntVec3.Invalid;
            float bestDistance = -1f;
            foreach (BuildingPlan building in plan.Buildings)
            {
                Box box = building.Outer;
                IntVec3 cell = AnyRim.ToMap(anchor, new Cell(
                    (box.MinX + box.MaxX) / 2, (box.MinZ + box.MaxZ) / 2));
                float distance = cell.DistanceTo(anchor);
                if (cell.InBounds(Map) && distance > bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }

            return best.IsValid ? best : (IntVec3?)null;
        }
    }

    /// <summary>
    /// Scene 6, "Comfort Is a Weapon": a colonist throws the Ergonomic
    /// Singularity Grenade at the junk pile with a volunteer inside it.
    /// The volunteer is one of the tour group, never a colonist (slice
    /// 19000c): the group's volunteer, else any tourist still standing,
    /// else nobody, and the pile is empty. The cast is checked when the
    /// throw is due (a colonist chosen at the start may be busy or hurt by
    /// then), and another colonist takes the grenadier's part if it needs
    /// it. The outcome speaks to the volunteer's fate.
    /// </summary>
    public class GrenadeScene : TourScene
    {
        /// <summary>When the throw is ordered, in ticks.</summary>
        public int throwTick = 120;

        /// <summary>How far round the pile colonists are moved clear.</summary>
        public float clearRadius = 16f;

        /// <summary>How near the pile an implosion counts as the grenade's.</summary>
        public float landingRadius = 12f;

        /// <summary>Whether the throw was ordered.</summary>
        [Unsaved]
        private bool ordered;

        /// <summary>The tick the grenade was seen thrown, or -1.</summary>
        [Unsaved]
        private int thrownAt = -1;

        /// <summary>The colonist throwing, as cast at the throw.</summary>
        [Unsaved]
        private Pawn grenadier;

        /// <summary>The volunteer, as cast at the throw, or null.</summary>
        [Unsaved]
        private Pawn volunteer;

        /// <summary>Whether the volunteer was dead or downed at the end.</summary>
        [Unsaved]
        private bool volunteerLost;

        /// <summary>
        /// Gets the volunteer cast at the throw, a tourist; null before it
        /// or when no tourist was left to volunteer.
        /// </summary>
        public Pawn Volunteer => volunteer;

        /// <summary>Gets the grenadier.</summary>
        public override Thing Subject =>
            Alive(grenadier) ? grenadier
                : Alive(Stage.Grenadier) ? Stage.Grenadier : null;

        /// <summary>Gets the junk pile.</summary>
        public override IntVec3 FocusCell =>
            Stage.GrenadeTarget.IsValid ? Stage.GrenadeTarget : base.FocusCell;

        /// <summary>
        /// Gets why the scene cannot run: no pile, or no colonist able to
        /// throw.
        /// </summary>
        protected override string WhyUnavailable =>
            Stage.GrenadeTarget.IsValid && Able().Any()
                ? null
                : "no colonist can throw the grenade";

        /// <summary>Starts the run with nothing thrown.</summary>
        protected override void SetupScene()
        {
            ordered = false;
            thrownAt = -1;
            grenadier = null;
            volunteer = null;
            volunteerLost = false;
        }

        /// <summary>Orders the throw; plays the beat once it flies.</summary>
        public override void Tick()
        {
            if (ordered)
            {
                KeepColonistsClear(NearPile, grenadier);
            }

            if (!ordered && Elapsed >= throwTick)
            {
                ordered = true;
                Cast();
                HoldColonistsClear(NearPile, grenadier);
                string why = Showcase.ThrowGrenade(grenadier, Stage.GrenadeTarget);
                if (why != null)
                {
                    Log.Warning($"{ShowcaseLog.Prefix} the grenade could not "
                        + $"be thrown ({why}); the scene will time out");
                }
            }

            if (ordered && thrownAt < 0 && Elapsed > throwTick
                && (Elapsed - throwTick) % WaitNoteTicks == 0)
            {
                NoteTheWait();
            }

            if (ordered && thrownAt < 0 && (GrenadeInFlight() || Landed()))
            {
                thrownAt = Elapsed;
                BeatOnce(
                    volunteer != null ? TourScript.Thrown : TourScript.ThrownAlone,
                    Shot(22f),
                    select: false);
            }
        }

        /// <summary>
        /// Done once the grenade has flown, landed, finished imploding and
        /// exploded: no projectile, no running implosion, and no field left
        /// (see <see cref="TourScene.SingularityOpen"/>).
        /// </summary>
        /// <returns>True when the demonstration is over.</returns>
        public override bool IsDone() =>
            thrownAt >= 0 && Elapsed >= thrownAt + 60
            && !BatshitProjectileInFlight() && !ImplosionActive()
            && !SingularityOpen();

        /// <summary>
        /// Stands the grenadier down and takes the grenade back, so no
        /// colonist throws it at the tractor scenes' raiders; heals a
        /// volunteer who lived.
        /// </summary>
        /// <param name="ending">How it ended.</param>
        public override void End(SceneEnd ending)
        {
            foreach (Pawn pawn in new[] { grenadier, Stage.Grenadier })
            {
                if (pawn?.equipment?.Primary?.def.defName == Showcase.GrenadeDef)
                {
                    pawn.equipment.DestroyAllEquipment();
                }
            }

            if (Alive(grenadier) && grenadier.drafter != null)
            {
                grenadier.drafter.Drafted = false;
            }

            ReleaseHeldColonists();

            // The director asks OutcomeKey after End, so the volunteer's
            // fate is fixed here, before healing. A volunteer who lived is
            // healed, so a later death of theirs is the tour's fault and
            // not the grenade bleeding them out off camera.
            volunteerLost = volunteer != null
                && (volunteer.Dead || volunteer.Destroyed || volunteer.Downed);
            if (volunteer != null && !volunteer.Dead && !volunteer.Destroyed)
            {
                TractorStaging.Heal(volunteer);
            }
        }

        /// <summary>
        /// The volunteer's fate when the scene completed, if there was a
        /// volunteer (lost if dead or downed); the API's moment otherwise.
        /// </summary>
        /// <param name="ending">How it ended.</param>
        /// <returns>The narration moment.</returns>
        public override string OutcomeKey(SceneEnd ending)
        {
            if (ending != SceneEnd.Completed || volunteer == null)
            {
                return base.OutcomeKey(ending);
            }

            // A volunteer knocked flat is "comforted. Completely." too; the
            // survivor's line is for one still standing (fixed in End).
            return volunteerLost
                ? TourScript.VolunteerLost
                : TourScript.VolunteerSurvived;
        }

        /// <summary>
        /// Casts the throw: the staged grenadier if it can still play its
        /// part, else another able colonist (handed a grenade); and the
        /// volunteer from the tour group, placed in the pile.
        /// </summary>
        private void Cast()
        {
            List<Pawn> able = Able().ToList();
            grenadier = able.Contains(Stage.Grenadier)
                ? Stage.Grenadier
                : able.FirstOrDefault();
            Showcase.ArmGrenadier(grenadier);
            volunteer = PlaceVolunteer(Stage.Tourists);
        }

        /// <summary>
        /// Stands the tour group's volunteer in the pile, else the first
        /// tourist still standing; never a colonist (docs/SHOWCASE.md,
        /// "Tourists").
        /// </summary>
        /// <param name="group">The tour group, or null.</param>
        /// <returns>The volunteer placed, or null if none could be.</returns>
        private Pawn PlaceVolunteer(TourGroup group)
        {
            if (group == null || group.Dismissed || !Stage.GrenadeTarget.IsValid)
            {
                return null;
            }

            Pawn chosen = TourGroup.Standing(group.Volunteer)
                ? group.Volunteer
                : group.Members.FirstOrDefault(TourGroup.Standing);
            return chosen != null
                && group.Place(chosen, Showcase.VolunteerCell(Stage.GrenadeTarget))
                ? chosen
                : null;
        }

        /// <summary>
        /// Stands the volunteer in the pile from the scene's start, so the
        /// opening shot shows the guest waiting; the rest wait in the base.
        /// </summary>
        /// <param name="group">The tour group.</param>
        protected override void PlaceTourists(TourGroup group)
        {
            Pawn placed = PlaceVolunteer(group);
            group.RestAll(placed);
        }

        /// <summary>
        /// The free colonists who can stand in the scene: spawned, not
        /// downed, and not in a mental state.
        /// </summary>
        /// <returns>The colonists.</returns>
        private IEnumerable<Pawn> Able() =>
            Map?.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Dead && !p.Downed && !p.InMentalState
                    && p.equipment != null && p.drafter != null)
            ?? Enumerable.Empty<Pawn>();

        /// <summary>
        /// How often the scene logs why the grenade has not yet flown, in
        /// ticks: on maps with dark entities it has timed out unthrown,
        /// with nothing logged to say why.
        /// </summary>
        private const int WaitNoteTicks = 600;

        /// <summary>
        /// Logs the grenadier's state while the throw is overdue: where it
        /// is, what it is doing, and whether it is drafted, downed or in a
        /// mental state, and what it holds.
        /// </summary>
        private void NoteTheWait()
        {
            Pawn p = grenadier;
            string state = p == null ? "no grenadier"
                : $"{p.LabelShort} at {p.PositionHeld} "
                    + $"({p.PositionHeld.DistanceTo(Stage.GrenadeTarget):0} "
                    + $"tiles from the pile), job "
                    + $"{p.CurJobDef?.defName ?? "none"}, drafted {p.Drafted}, "
                    + $"downed {p.Downed}, dead {p.Dead}, mental "
                    + $"{p.MentalStateDef?.defName ?? "none"}, stance "
                    + $"{p.stances?.curStance?.GetType().Name ?? "none"}, "
                    + $"holding {p.equipment?.Primary?.def.defName ?? "nothing"}";
            Log.Message($"{ShowcaseLog.Prefix} grenade: still waiting for the "
                + $"throw at scene tick {Elapsed}: {state}");
        }

        /// <summary>
        /// Whether a cell is near enough the pile for the singularity to
        /// reach it: within <see cref="clearRadius"/>.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <returns>True if it is.</returns>
        private bool NearPile(IntVec3 cell) =>
            Stage.GrenadeTarget.IsValid
            && cell.InHorDistOf(Stage.GrenadeTarget, clearRadius);

        /// <summary>Whether the grenade itself is in the air.</summary>
        /// <returns>True while it flies.</returns>
        private bool GrenadeInFlight() =>
            Map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile)
                .Any(t => t.def.defName == Showcase.GrenadeProjectileDef);

        /// <summary>Whether an implosion has begun at the pile.</summary>
        /// <returns>True once one runs near it.</returns>
        private bool Landed() =>
            RunningImplosions().Any(i => i.Center.InHorDistOf(
                Stage.GrenadeTarget, landingRadius));
    }
}
