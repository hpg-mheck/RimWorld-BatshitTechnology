// Batshit Technology - the directed tour's director.
// BT_Showcase (Defs/Misc/BT_Showcases.xml) is a ShowcaseDirector tour
// whose scenes are listed in Core's TourScript.Scenes (docs/SHOWCASE.md,
// "The Directed Tour"). This director builds the stage first
// (Showcase.Build), keeps it for the scenes, and then lets the API's
// director run them: each scene declines a map that lacks what it needs,
// so the caption counts only the scenes that run.

using System;
using BatshitTechnology.Showcases;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// Runs Batshit Technology's directed tour: builds the demo base,
    /// AnyRim, the pads and the grenade's pile, then plays the def's
    /// scenes over them.
    /// </summary>
    public class BatshitTourDirector : ShowcaseDirector
    {
        /// <summary>
        /// Gets what the stage holds for this run; empty (never null)
        /// before <see cref="Setup"/> or when the build failed.
        /// </summary>
        public TourStage Stage { get; private set; } = TourStage.Empty;

        /// <summary>
        /// Builds the stage, then the scenes. In dev mode the devices'
        /// DEV buttons show too (lent by the API and given back when the
        /// showcase ends). A build that throws is logged once and leaves an
        /// empty stage, on which every scene that needs the stage
        /// declines, so the tour still ends rather than stalls.
        /// </summary>
        /// <param name="map">The showcase's map.</param>
        public override void Setup(Map map)
        {
            EnableDevButtons();
            try
            {
                Stage = Showcase.Build(map) ?? TourStage.Empty;
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} the tour's stage could not "
                    + $"be built: {e}");
                Stage = TourStage.Empty;
            }

            base.Setup(map);
        }
    }
}
