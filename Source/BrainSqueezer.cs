// Batshit Technology - Brain Squeezer
// A chair under a squeezing headpiece. A colonist sits in it for moderate
// recreation, muttering disturbing inner-monologue lines while it works.
// Afterwards they get a mood boost labelled with the last thing they said
// (less anxiety) and, for a day, a lower mental break threshold (fewer
// breaks, riots and arguments). The lines live in the BT_BrainSqueezed
// ThoughtDef's MonologueExtension, so they can be edited in XML.

using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace BatshitTechnology
{
    /// <summary>The Brain Squeezer's defs.</summary>
    [DefOf]
    public static class BT_BrainSqueezerDefOf
    {
        /// <summary>The mood memory left by a squeezing.</summary>
        public static ThoughtDef BT_BrainSqueezed;

        /// <summary>The day-long lower mental break threshold.</summary>
        public static HediffDef BT_BrainSqueezedCalm;

        /// <summary>Sitting in the squeezer.</summary>
        public static JobDef BT_UseBrainSqueezer;

        /// <summary>Lets RimWorld bind the fields above at load.</summary>
        static BT_BrainSqueezerDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BT_BrainSqueezerDefOf));
        }
    }

    /// <summary>
    /// Monologue lines attached to a Def in XML:
    /// <c>&lt;li Class="BatshitTechnology.MonologueExtension"&gt;&lt;lines&gt;...</c>
    /// </summary>
    public class MonologueExtension : DefModExtension
    {
        /// <summary>The lines, said one at a time at random.</summary>
        public List<string> lines = new List<string>();

        /// <summary>
        /// Picks the next line, never the one just said. Rand.Value can
        /// return exactly 1.0, which the picker rejects, so it is clamped.
        /// </summary>
        /// <param name="previousIndex">The last line said, or -1.</param>
        /// <returns>The next line's index.</returns>
        public int NextIndex(int previousIndex) =>
            MonologuePicker.NextIndex(
                lines.Count, previousIndex, () => Math.Min(Rand.Value, 0.99999994d));

        /// <summary>Reports an empty line list at load.</summary>
        /// <returns>One message per configuration error.</returns>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (lines.NullOrEmpty())
            {
                yield return "MonologueExtension needs at least one line";
            }
        }
    }

    /// <summary>
    /// Sends a colonist to sit in a Brain Squeezer. The seat is the
    /// squeezer itself, so the job's sitting spot is its own cell.
    /// </summary>
    public class JoyGiver_BrainSqueezer : JoyGiver_InteractBuilding
    {
        /// <summary>Creates the job; the base class already checked power,
        /// reservations and reachability.</summary>
        /// <param name="pawn">The colonist.</param>
        /// <param name="t">The squeezer.</param>
        /// <returns>The job.</returns>
        protected override Job TryGivePlayJob(Pawn pawn, Thing t)
        {
            return JobMaker.MakeJob(def.jobDef, t, t.Position);
        }
    }

    /// <summary>
    /// Sits in the squeezer for the joy duration, muttering, then applies
    /// the mood memory and the calm hediff.
    /// </summary>
    public class JobDriver_UseBrainSqueezer : JobDriver_WatchBuilding
    {
        private const int MonologueIntervalTicks = 420; // about 7 seconds
        private const int MinTicksForEffects = 600;     // a brief sit does nothing
        private static readonly Color MonologueColor = new Color(0.85f, 0.6f, 1f);

        private int ticksSqueezed;
        private int lastLineIndex = -1;

        /// <summary>Lines shown so far this session (smoke test).</summary>
        internal static int MonologuesShown;

        /// <summary>The monologue lines, from the thought's extension.</summary>
        private static MonologueExtension Monologue =>
            BT_BrainSqueezerDefOf.BT_BrainSqueezed.GetModExtension<MonologueExtension>();

        /// <summary>
        /// Reserves the squeezer. Its seat is its own cell, so one
        /// reservation covers both the building and the spot.
        /// </summary>
        /// <param name="errorOnFailed">Log an error if it fails.</param>
        /// <returns>True if reserved.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(
                job.targetA, job, job.def.joyMaxParticipants, 0, null, errorOnFailed);
        }

        /// <summary>
        /// The vanilla watch toils, with the after-effects added to the
        /// sitting toil.
        /// </summary>
        /// <returns>The job's toils.</returns>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            List<Toil> toils = base.MakeNewToils().ToList();
            toils[toils.Count - 1].AddFinishAction(ApplyAftereffects);
            return toils;
        }

        /// <summary>
        /// Each tick interval: face the way the chair faces, take its
        /// comfort, mutter now and then, and gain joy.
        /// </summary>
        /// <param name="delta">Ticks since the last call.</param>
        protected override void WatchTickAction(int delta)
        {
            pawn.Rotation = TargetThingA.Rotation;
            pawn.GainComfortFromCellIfPossible(delta);
            int before = ticksSqueezed;
            ticksSqueezed += delta;
            if (ticksSqueezed / MonologueIntervalTicks != before / MonologueIntervalTicks)
            {
                Mutter();
            }

            JoyUtility.JoyTickCheckEnd(
                pawn, delta, JoyTickFullJoyAction.EndJob, 1f, (Building)TargetThingA);
        }

        /// <summary>Shows the next monologue line above the colonist.</summary>
        private void Mutter()
        {
            MonologueExtension monologue = Monologue;
            if (monologue == null || monologue.lines.NullOrEmpty())
            {
                return;
            }

            lastLineIndex = monologue.NextIndex(lastLineIndex);
            MonologuesShown++;
            MoteMaker.ThrowText(
                pawn.DrawPos + new Vector3(0f, 0f, 0.7f), pawn.Map,
                monologue.lines[lastLineIndex], MonologueColor, 3.5f);
        }

        /// <summary>
        /// After a real sit: replaces any earlier squeezing memory with a
        /// fresh one labelled with the last line said, and restarts the
        /// day of calm.
        /// </summary>
        private void ApplyAftereffects()
        {
            if (ticksSqueezed < MinTicksForEffects || pawn.Dead)
            {
                return;
            }

            MemoryThoughtHandler memories = pawn.needs?.mood?.thoughts?.memories;
            if (memories != null)
            {
                memories.RemoveMemoriesOfDef(BT_BrainSqueezerDefOf.BT_BrainSqueezed);
                var thought = (Thought_BrainSqueezed)ThoughtMaker.MakeThought(
                    BT_BrainSqueezerDefOf.BT_BrainSqueezed, null);
                MonologueExtension monologue = Monologue;
                if (monologue != null && !monologue.lines.NullOrEmpty())
                {
                    int index = lastLineIndex >= 0 ? lastLineIndex : monologue.NextIndex(-1);
                    thought.Line = monologue.lines[index];
                }

                memories.TryGainMemory(thought);
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(
                BT_BrainSqueezerDefOf.BT_BrainSqueezedCalm);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing); // restart the day
            }

            pawn.health.AddHediff(BT_BrainSqueezerDefOf.BT_BrainSqueezedCalm);
        }

        /// <summary>Saves and loads the session's progress.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksSqueezed, "ticksSqueezed", 0);
            Scribe_Values.Look(ref lastLineIndex, "lastLineIndex", -1);
        }
    }

    /// <summary>
    /// The squeezing memory. Its label is the last thing the colonist said
    /// in the squeezer.
    /// </summary>
    public class Thought_BrainSqueezed : Thought_Memory
    {
        private string line;

        /// <summary>The monologue line shown as the label.</summary>
        public string Line
        {
            get => line;
            set => line = value;
        }

        /// <summary>The quoted line, or the Def's label if none was set.</summary>
        public override string LabelCap =>
            line.NullOrEmpty() ? base.LabelCap : $"“{line}”";

        /// <summary>Saves and loads the line.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref line, "line");
        }
    }
}
