// Batshit Technology - the directed tour's director.
// BT_Showcase (Defs/Misc/BT_Showcases.xml) is a ShowcaseDirector tour
// whose scenes are listed in Core's TourScript.Scenes (docs/SHOWCASE.md,
// "The Directed Tour"). This director builds the stage first
// (Showcase.Build), brings the tour group (TourGroup.Arrive), keeps both
// for the scenes, and then lets the API's director run them: each scene
// declines a map that lacks what it needs, so the caption counts only the
// scenes that run. Each tick it watches the tour group: a tourist lost
// during a scene that narrates it plays that scene's guestsLost beat
// (after the scene's intro, if it was lost during it).
// When the tour finishes, stops or fails to set up, the group goes home,
// alive or dead (docs/SHOWCASE.md, "Tourists").

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// Runs Batshit Technology's directed tour: builds the demo base,
    /// AnyRim, the pads and the grenade's pile, brings the tour group,
    /// then plays the def's scenes over them.
    /// </summary>
    public class BatshitTourDirector : ShowcaseDirector
    {
        /// <summary>The tourists seen lost so far, so each counts once.</summary>
        private readonly HashSet<Pawn> lostSeen = new HashSet<Pawn>();

        /// <summary>
        /// Gets what the stage holds for this run; empty (never null)
        /// before <see cref="Setup"/> or when the build failed.
        /// </summary>
        public TourStage Stage { get; private set; } = TourStage.Empty;

        /// <summary>
        /// Builds the stage, brings the tour group, then the scenes. The
        /// tour is a film, so it asks for no DEV buttons, even in dev mode
        /// (slice 19000b: the operator's viewing runs show no developer
        /// clutter). A build that throws is logged once and leaves an
        /// empty stage, on which every scene that needs the stage
        /// declines, so the tour still ends rather than stalls. A set-up
        /// that throws after the group arrived sends the group home first.
        /// </summary>
        /// <param name="map">The showcase's map.</param>
        public override void Setup(Map map)
        {
            lostSeen.Clear();
            try
            {
                Stage = Showcase.Build(map, PlannedHerd()) ?? TourStage.Empty;
                if (Stage.BaseCentre.IsValid)
                {
                    Stage.Tourists = TourGroup.Arrive(map, Stage.BaseCentre);
                }
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} the tour's stage could not "
                    + $"be built: {e}");
                Stage?.Tourists?.Dismiss("the stage failed");
                Stage = TourStage.Empty;
            }

            try
            {
                base.Setup(map);
            }
            catch
            {
                DismissTourists("the set-up failed");
                throw;
            }
        }

        /// <summary>
        /// Runs the tour for a tick, notes any tourist lost (the scene
        /// showing plays its guestsLost beat, if it has one), and sends
        /// the group home once the tour is over.
        /// </summary>
        public override void Tick()
        {
            base.Tick();
            WatchTourists();
            if (Finished)
            {
                DismissTourists("the tour finished");
            }
        }

        /// <summary>
        /// Stops the tour (the API ends the running scene as stopped), and
        /// sends the tour group home.
        /// </summary>
        public override void Stop()
        {
            base.Stop();
            DismissTourists("the tour was stopped");
        }

        /// <summary>
        /// Sends the tour group home, if it came and is still here. Never
        /// throws: a failure is logged.
        /// </summary>
        /// <param name="why">Why, for the log.</param>
        internal void DismissTourists(string why)
        {
            TourGroup group = Stage?.Tourists;
            if (group == null || group.Dismissed)
            {
                return;
            }

            try
            {
                group.Dismiss(why);
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} the tour group could not be "
                    + $"sent home: {e}");
            }
        }

        /// <summary>
        /// Notes each tourist newly lost (downed, dead or gone) and lets
        /// the scene showing narrate it, at once or, for a loss in its
        /// intro, once the intro is over.
        /// </summary>
        private void WatchTourists()
        {
            TourGroup group = Stage?.Tourists;
            if (group == null || group.Dismissed)
            {
                return;
            }

            (CurrentScene as TourScene)?.GuestLostAfterIntro();

            foreach (Pawn p in group.Members)
            {
                if (TourGroup.Standing(p) || !lostSeen.Add(p))
                {
                    continue;
                }

                string scene = CurrentScene?.key ?? "between scenes";
                Log.Message($"{ShowcaseLog.Prefix} {scene}: tourist "
                    + $"{p?.LabelShort} ({group.RoleOf(p)}) lost: {TourGroup.Fate(p)}");
                (CurrentScene as TourScene)?.GuestLost();
            }
        }

        /// <summary>
        /// The stampede's herd as the def asks for it, held to Core's
        /// bounds; the target herd when the def has no stampede.
        /// </summary>
        /// <returns>The herd's size.</returns>
        private int PlannedHerd() =>
            def?.scenes?.OfType<StampedeScene>().FirstOrDefault()?.HerdPlanned
                ?? Stampede.TargetHerd;
    }
}
