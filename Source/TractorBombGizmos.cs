// Batshit Technology - the Tractor Bomb's controls.
// Gizmo_TractorBombYield: the Dial-A-Yield sliders, one for the tractor
// beam and one for the blast, on a robot or an emplaced bomb once Tractor
// Bomb Dial-A-Yield is researched; with several bombs selected, one slider
// sets them all. Command_TractorBombArea: picks a robot's allowed area,
// the vanilla per-pawn area that bounds its patrol, from vanilla's own
// area menu, since only colonists, animals and mechs get vanilla's area UI.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>Which radius a yield slider dials.</summary>
    public enum TractorBombYieldKind
    {
        /// <summary>The tractor beam: the implosion's pull radius.</summary>
        Pull,

        /// <summary>The blast radius.</summary>
        Blast,
    }

    /// <summary>
    /// A Dial-A-Yield slider: drag it to set a radius, in whole tiles,
    /// within its range. Groups with the same slider on other selected
    /// bombs and sets them all.
    /// </summary>
    public class Gizmo_TractorBombYield : Gizmo_Slider
    {
        // Whether a slider of each kind is being dragged. Static, as in
        // vanilla's fuel slider: gizmos are rebuilt every frame.
        private static readonly bool[] Dragging = new bool[2];

        private readonly List<ITractorBombYield> bombs =
            new List<ITractorBombYield>();

        private readonly TractorBombYieldKind kind;

        /// <summary>Makes a slider for one bomb.</summary>
        /// <param name="bomb">The robot or emplaced bomb.</param>
        /// <param name="kind">Which radius it dials.</param>
        public Gizmo_TractorBombYield(
            ITractorBombYield bomb, TractorBombYieldKind kind)
        {
            if (bomb != null)
            {
                bombs.Add(bomb);
            }

            this.kind = kind;
        }

        /// <summary>Which radius it dials.</summary>
        public TractorBombYieldKind Kind => kind;

        /// <summary>The bombs it sets (several once merged).</summary>
        public IReadOnlyList<ITractorBombYield> Bombs => bombs;

        /// <summary>The first bomb's radius, in tiles.</summary>
        private float Radius => bombs.Count == 0 ? 0f
            : kind == TractorBombYieldKind.Pull ? bombs[0].PullRadius
            : bombs[0].BlastRadius;

        /// <summary>The slider's position, from the first bomb.</summary>
        protected override float Target
        {
            get => (float)(kind == TractorBombYieldKind.Pull
                ? TractorBombYield.PullFraction(Radius)
                : TractorBombYield.BlastFraction(Radius));
            set => Set(kind == TractorBombYieldKind.Pull
                ? TractorBombYield.PullAt(value)
                : TractorBombYield.BlastAt(value));
        }

        /// <summary>The bar's fill: the same as its position.</summary>
        protected override float ValuePercent => Target;

        /// <summary>What it sets, and to what.</summary>
        protected override string Title => kind == TractorBombYieldKind.Pull
            ? $"Tractor beam: {Radius:0} tiles"
            : $"Blast: {Radius:0} tiles";

        /// <summary>The radius, on the bar.</summary>
        protected override string BarLabel => $"{Radius:0}";

        /// <summary>
        /// Always draggable: it is only offered when dialable.
        /// </summary>
        protected override bool IsDraggable => true;

        /// <summary>One stop per whole tile.</summary>
        protected override int Increments => kind == TractorBombYieldKind.Pull
            ? TractorBombYield.Steps(
                TractorBombYield.MinPull, TractorBombYield.MaxPull)
            : TractorBombYield.Steps(
                TractorBombYield.MinBlast, TractorBombYield.MaxBlast);

        /// <summary>Whether this kind of slider is being dragged.</summary>
        protected override bool DraggingBar
        {
            get => Dragging[(int)kind];
            set => Dragging[(int)kind] = value;
        }

        /// <summary>Groups with the same kind of slider.</summary>
        /// <param name="other">Another gizmo.</param>
        /// <returns>True if it is a slider of the same kind.</returns>
        public override bool GroupsWith(Gizmo other) =>
            other is Gizmo_TractorBombYield slider && slider.kind == kind;

        /// <summary>Takes on another slider's bombs.</summary>
        /// <param name="other">The slider grouped with this one.</param>
        public override void MergeWith(Gizmo other)
        {
            base.MergeWith(other);
            if (other is Gizmo_TractorBombYield slider)
            {
                foreach (ITractorBombYield bomb in slider.bombs)
                {
                    if (!bombs.Contains(bomb))
                    {
                        bombs.Add(bomb);
                    }
                }
            }
        }

        /// <summary>What it does, and the range.</summary>
        /// <returns>The tooltip.</returns>
        protected override string GetTooltip() =>
            kind == TractorBombYieldKind.Pull
                ? "How far the tractor beam reaches: everything within it "
                    + "is dragged to the bomb before it goes off. "
                    + $"{TractorBombYield.MinPull} to "
                    + $"{TractorBombYield.MaxPull} tiles."
                : "How far the blast reaches once everything is gathered. "
                    + $"{TractorBombYield.MinBlast} to "
                    + $"{TractorBombYield.MaxBlast} tiles.";

        /// <summary>
        /// Sets every bomb that may still be dialled, if the radius changed.
        /// </summary>
        /// <param name="radius">The radius, in whole tiles.</param>
        private void Set(int radius)
        {
            foreach (ITractorBombYield bomb in bombs)
            {
                if (bomb == null || !bomb.YieldAdjustable)
                {
                    continue;
                }

                if (kind == TractorBombYieldKind.Pull)
                {
                    if (!Mathf.Approximately(bomb.PullRadius, radius))
                    {
                        bomb.PullRadius = radius;
                    }
                }
                else if (!Mathf.Approximately(bomb.BlastRadius, radius))
                {
                    bomb.BlastRadius = radius;
                }
            }
        }
    }

    /// <summary>
    /// Picks the allowed area of every selected robot, from vanilla's area
    /// menu: "Unrestricted" (the whole map, the default), any area, or
    /// Manage areas. One menu for the whole selection.
    /// </summary>
    public class Command_TractorBombArea : Command_Action
    {
        /// <summary>Makes the command for one robot.</summary>
        /// <param name="robot">The robot.</param>
        public Command_TractorBombArea(Pawn robot)
        {
            defaultLabel = "Patrol: " + AreaUtility.AreaAllowedLabel(robot);
            defaultDesc = "Choose where this tractor bomb patrols. It looks "
                + "for hostiles anywhere in the allowed area, and never "
                + "leaves it to chase them. Unrestricted is the whole map.";
            icon = BT_TractorBombAssets.AreaIcon;
            Map map = robot.MapHeld;
            action = () =>
            {
                if (map == null)
                {
                    return;
                }

                AreaUtility.MakeAllowedAreaListFloatMenu(
                    area => SetArea(map, area), addNullAreaOption: true,
                    addManageOption: true, map);
            };
        }

        /// <summary>
        /// One menu, not one per selected robot: the choice applies to all
        /// of them.
        /// </summary>
        /// <param name="other">Another command in the group.</param>
        /// <returns>False: the others do nothing.</returns>
        public override bool InheritInteractionsFrom(Gizmo other) => false;

        /// <summary>
        /// Gives every selected robot on a map the chosen area.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="area">The area, or null for the whole map.</param>
        private static void SetArea(Map map, Area area)
        {
            foreach (Pawn robot in Find.Selector.SelectedPawns.ToList())
            {
                if (robot.MapHeld == map && robot.playerSettings != null
                    && robot.TryGetComp<CompTractorBomb>() != null)
                {
                    robot.playerSettings.AreaRestrictionInPawnCurrentMap =
                        area;
                }
            }
        }
    }
}
