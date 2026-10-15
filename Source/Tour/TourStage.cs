// Batshit Technology - the directed tour's stage.
// What Showcase.Build put on the map for the tour's scenes
// (docs/SHOWCASE.md, "The Directed Tour"): the demo base's devices, the
// Ballista, the self-destruct pads, the grenade's cast, AnyRim and the
// tractor yard, and what the stage cleared from the map. The director
// keeps one per run, so nothing here is static and no pawn outlives its
// game through it.

using System.Collections.Generic;
using BatshitTechnology.Core;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// What the tour has to work with on this map. Any member may be
    /// missing (null, Invalid or empty) when the map would not allow it;
    /// the scene that needs it then declines (its UnavailableReason).
    /// </summary>
    public sealed class TourStage
    {
        /// <summary>A stage with nothing on it, for a failed build.</summary>
        public static TourStage Empty => new TourStage();

        /// <summary>Gets the base's centre.</summary>
        public IntVec3 BaseCentre { get; internal set; } = IntVec3.Invalid;

        /// <summary>Gets the base's Batshittium Reactor.</summary>
        public Thing Reactor { get; internal set; }

        /// <summary>Gets the base's Random Replicator.</summary>
        public Thing Replicator { get; internal set; }

        /// <summary>Gets the base's Transmogrifier.</summary>
        public Thing Transmogrifier { get; internal set; }

        /// <summary>Gets the base's Brain Squeezer.</summary>
        public Thing Squeezer { get; internal set; }

        /// <summary>Gets the base's Artificer.</summary>
        public Thing Artificer { get; internal set; }

        /// <summary>Gets the base's Ballista, loaded and not firing.</summary>
        public Building_BatshittiumBallista Ballista { get; internal set; }

        /// <summary>Gets the open ground the Ballista's volley lands on.</summary>
        public IntVec3 BallistaTarget { get; internal set; } = IntVec3.Invalid;

        /// <summary>
        /// Gets the lone devices on the self-destruct pads, in order: the
        /// Random Replicator, then the Batshittium Reactor. Fewer when
        /// fewer pads clear AnyRim and the yard.
        /// </summary>
        public IReadOnlyList<Thing> Pads { get; internal set; } = new List<Thing>();

        /// <summary>Gets the colonist holding the grenade, or null.</summary>
        public Pawn Grenadier { get; internal set; }

        /// <summary>
        /// Gets the colonist who volunteers inside the junk pile, or null.
        /// </summary>
        public Pawn Volunteer { get; internal set; }

        /// <summary>Gets the junk pile's centre, where the grenade lands.</summary>
        public IntVec3 GrenadeTarget { get; internal set; } = IntVec3.Invalid;

        /// <summary>Gets AnyRim's plan as built, or null if it was skipped.</summary>
        public TownPlan AnyRimPlan { get; internal set; }

        /// <summary>Gets AnyRim's crossroads, or Invalid if it was skipped.</summary>
        public IntVec3 AnyRimAnchor { get; internal set; } = IntVec3.Invalid;

        /// <summary>
        /// Gets the tractor yard, or null when no candidate cleared the
        /// base, the grenade clearing and AnyRim.
        /// </summary>
        public CellRect? Yard { get; internal set; }

        /// <summary>Gets why there is no yard (or only one pad), if so.</summary>
        public string PlanNote { get; internal set; }

        /// <summary>
        /// Gets what the stage cleared from the map before READY: its own
        /// hostiles, its anima trees, and the trees in the volleys' zone
        /// (docs/SHOWCASE.md, "A Clean Stage").
        /// </summary>
        public StageClearance Clearance { get; internal set; }

        /// <summary>
        /// Gets where the defence's and the stampede's volleys can land
        /// (Core's Stampede.VolleyZone), cleared of trees; null when the
        /// map leaves no room east of the base.
        /// </summary>
        public CellRect? VolleyZone { get; internal set; }

        /// <summary>Gets a value indicating whether AnyRim stands.</summary>
        public bool HasAnyRim => AnyRimPlan != null && AnyRimAnchor.IsValid;

        /// <summary>Gets the yard's centre, or Invalid without a yard.</summary>
        public IntVec3 YardCentre =>
            Yard is CellRect yard ? yard.CenterCell : IntVec3.Invalid;
    }
}
