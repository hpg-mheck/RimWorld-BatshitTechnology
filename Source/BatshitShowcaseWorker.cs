// Batshit Technology - the first client of the Showcase API.
// BT_Showcase (Defs/Misc/BT_Showcases.xml) runs today's demo base through
// the API: the Showcases menu or -showcase=BT_Showcase generates a fresh
// game, and the API's session calls this worker, which hands the work to
// Source/Showcase.cs unchanged. Its log cues stay as they were (ANYRIM or
// "AnyRim skipped", THROW, TOUR), so capture tooling keeps working; READY
// now comes from the API, once the base is built. The directed tour
// (docs/SHOWCASE.md, "The Directed Tour") will replace this worker with
// ShowcaseDirector scenes.

using BatshitTechnology.Showcases;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// Runs Batshit Technology's demo base as a showcase: builds it on the
    /// fresh map, then plays its timeline of self-destructs, the grenade
    /// and the AnyRim tour.
    /// </summary>
    public class BatshitShowcaseWorker : ShowcaseWorker
    {
        /// <summary>
        /// Gets a value indicating whether the timeline has played out:
        /// nothing left to arm, throw or tour, no device counting down
        /// under the camera, and ten seconds' grace for the last scene.
        /// </summary>
        public override bool Finished =>
            Showcase.TimelineFinished(Find.TickManager.TicksGame);

        /// <summary>
        /// Builds the demo base, AnyRim and the demos. In dev mode the
        /// devices' DEV buttons show too (god mode, lent by the API and
        /// given back when the showcase ends or the viewer leaves it).
        /// </summary>
        /// <param name="map">The showcase's map.</param>
        public override void Setup(Map map)
        {
            EnableDevButtons();
            Showcase.Build(map);
        }

        /// <summary>Runs the timeline's due steps.</summary>
        public override void Tick()
        {
            Showcase.Tick();
        }

        /// <summary>
        /// Stops the timeline: no more arming, throwing or touring, and no
        /// more camera moves. A device already counting down still goes
        /// off; that is the game, not the showcase.
        /// </summary>
        public override void Stop()
        {
            Showcase.StopTimeline();
            base.Stop();
        }
    }
}
