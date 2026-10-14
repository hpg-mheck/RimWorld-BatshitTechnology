// Batshit Technology - the Tractor Bomb's controls.
// Command_TractorBombDial: one of the three named settings (Proximity,
// Pull, Blast; docs/TRACTOR-BOMB.md, "Named Settings") on a robot, a
// planted bomb or the Tractor Barn. A click opens a menu of the setting's
// three stops with their sizes; with several things selected, the choice
// applies to all of them. Pull and Blast are offered once Tractor Bomb
// Dial-A-Yield is researched. Command_TractorBombArea: picks a robot's
// allowed area, the vanilla per-pawn area that bounds its patrol, from
// vanilla's own area menu, since only colonists, animals and mechs get
// vanilla's area UI.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// Something with Tractor Bomb settings the player may dial: a robot,
    /// the bomb it became, or the Tractor Barn's settings for the robots
    /// it builds.
    /// </summary>
    public interface ITractorBombDials
    {
        /// <summary>
        /// The settings as set (a planted bomb's proximity is the default
        /// and ignored). Setting them sets only the ones
        /// <see cref="CanDial"/> allows.
        /// </summary>
        TractorBombSettings Settings { get; set; }

        /// <summary>
        /// The tractor beam in effect, in tiles: what a proximity's stop
        /// depends on.
        /// </summary>
        float PullRadius { get; }

        /// <summary>The timid stop, in tiles: the robot's emplacing range.</summary>
        float TimidStop { get; }

        /// <summary>Whether the player may change a setting now.</summary>
        /// <param name="dial">The setting.</param>
        /// <returns>True if it may.</returns>
        bool CanDial(TractorBombDial dial);
    }

    /// <summary>
    /// A named setting's button: shows the setting's current stop, and a
    /// click opens a menu of its three stops with their sizes ("Popular,
    /// 22.5 tiles"). Groups with the same setting's button on everything
    /// else selected, and the choice sets them all.
    /// </summary>
    public class Command_TractorBombDial : Command
    {
        private readonly List<ITractorBombDials> targets =
            new List<ITractorBombDials>();

        private readonly TractorBombDial dial;

        private readonly string forWhat;

        /// <summary>Makes the button for one thing.</summary>
        /// <param name="target">The robot, planted bomb or barn.</param>
        /// <param name="dial">Which setting it sets.</param>
        /// <param name="forWhat">
        /// What the setting is for, in the tooltip, in the singular so the
        /// sentence agrees: "this tractor bomb", "each tractor bomb this
        /// barn builds".
        /// </param>
        public Command_TractorBombDial(
            ITractorBombDials target, TractorBombDial dial, string forWhat)
        {
            this.dial = dial;
            this.forWhat = forWhat ?? "this tractor bomb";
            if (target != null)
            {
                targets.Add(target);
            }

            Stop = target?.Settings.Stop(dial) ?? TractorBombDials.DefaultStop;
            icon = BT_TractorBombAssets.DialIcon(dial, Stop);
            Describe();
        }

        /// <summary>Which setting it sets.</summary>
        public TractorBombDial Dial => dial;

        /// <summary>The stop its first thing has, 0 to 2.</summary>
        public int Stop { get; }

        /// <summary>Whether the things it sets differ.</summary>
        public bool Mixed { get; private set; }

        /// <summary>The things it sets (several once merged).</summary>
        public IReadOnlyList<ITractorBombDials> Targets => targets;

        /// <summary>Groups with the same setting's button.</summary>
        /// <param name="other">Another gizmo.</param>
        /// <returns>True if it sets the same setting.</returns>
        public override bool GroupsWith(Gizmo other) =>
            other is Command_TractorBombDial command && command.dial == dial;

        /// <summary>
        /// Takes on another button's things, so one menu sets them all.
        /// </summary>
        /// <param name="other">The button grouped with this one.</param>
        public override void MergeWith(Gizmo other)
        {
            base.MergeWith(other);
            if (!(other is Command_TractorBombDial command))
            {
                return;
            }

            foreach (ITractorBombDials t in command.targets)
            {
                if (t != null && !targets.Contains(t))
                {
                    targets.Add(t);
                    Mixed |= t.Settings.Stop(dial) != Stop;
                }
            }

            Describe();
        }

        /// <summary>
        /// One menu, not one per selected thing: the choice applies to all
        /// of them.
        /// </summary>
        /// <param name="other">Another button in the group.</param>
        /// <returns>False: the others do nothing.</returns>
        public override bool InheritInteractionsFrom(Gizmo other) => false;

        /// <summary>Opens the menu of the three stops.</summary>
        /// <param name="ev">The click.</param>
        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            List<FloatMenuOption> options = Options()
                .Select(o => new FloatMenuOption(o.Label, () => Apply(o.Stop)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// The menu's entries: each stop with its size, the sizes worked
        /// out for the first thing (a proximity's stop depends on its
        /// tractor beam).
        /// </summary>
        /// <returns>The three entries, smallest first.</returns>
        public IReadOnlyList<(int Stop, string Label)> Options()
        {
            ITractorBombDials first = targets.FirstOrDefault();
            double pull = first?.PullRadius
                ?? TractorBombYield.DefaultPullRadius;
            double timid = first?.TimidStop
                ?? TractorBombApproach.DefaultTimidStop;
            var options = new List<(int, string)>();
            for (int s = 0; s < TractorBombDials.StopCount; s++)
            {
                options.Add((s, TractorBombDials.MenuLabel(
                    dial, s, pull, timid)));
            }

            return options;
        }

        /// <summary>
        /// Sets a stop on everything it sets that may still be dialled.
        /// </summary>
        /// <param name="stop">The stop, 0 to 2.</param>
        /// <returns>How many things were set.</returns>
        public int Apply(int stop)
        {
            int set = 0;
            foreach (ITractorBombDials t in targets)
            {
                if (t != null && t.CanDial(dial))
                {
                    t.Settings = t.Settings.With(dial, stop);
                    set++;
                }
            }

            return set;
        }

        /// <summary>Sets its label and tooltip from its things.</summary>
        private void Describe()
        {
            string title = TractorBombDials.Title(dial);
            defaultLabel = Mixed
                ? $"{title}: mixed"
                : $"{title}: {TractorBombDials.Name(dial, Stop)}";
            string what = dial switch
            {
                TractorBombDial.Proximity =>
                    $"How close {forWhat} drives to a hostile before it "
                        + "plants itself.",
                TractorBombDial.Pull =>
                    $"How far the tractor beam of {forWhat} reaches: "
                        + "everything within it is dragged to the bomb "
                        + "before it goes off.",
                _ => $"How far the blast of {forWhat} reaches once "
                    + "everything is gathered.",
            };
            string stops = string.Join(
                "\n", Options().Select(o => o.Label));
            defaultDesc = $"{what}\n\n{stops}\n\nClick to choose.";
            if (targets.Count > 1)
            {
                defaultDesc += " The choice applies to everything selected.";
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
