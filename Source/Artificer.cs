// Batshit Technology - Batshittium Artificer.
// An automated workbench: haulers keep it loaded with batshittium (vanilla
// CompRefuelable), and while powered it turns one unit at a time into
// Ballista bolts, dropped at its interaction spot for haulers to carry to
// magazines. The player picks which researched bolt type it makes.

using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace BatshitTechnology
{
    /// <summary>One bolt type the Artificer can make.</summary>
    public class ArtificerOutput
    {
        /// <summary>The bolt item.</summary>
        public ThingDef bolt;

        /// <summary>Bolts made from one unit of batshittium.</summary>
        public int countPerUnit = 10;

        /// <summary>Research that unlocks this output.</summary>
        public ResearchProjectDef research;
    }

    /// <summary>XML-facing properties for <see cref="CompBatshittiumArtificer"/>.</summary>
    public class CompProperties_BatshittiumArtificer : CompProperties
    {
        /// <summary>Ticks to work one unit (2500 = one in-game hour).</summary>
        public int ticksPerUnit = 2500;

        /// <summary>What it can make, in gizmo order.</summary>
        public List<ArtificerOutput> outputs = new List<ArtificerOutput>();

        /// <summary>Binds these properties to their comp class.</summary>
        public CompProperties_BatshittiumArtificer()
        {
            compClass = typeof(CompBatshittiumArtificer);
        }

        /// <summary>Reports Def mistakes at load.</summary>
        /// <param name="parentDef">The ThingDef carrying this comp.</param>
        /// <returns>One message per configuration error.</returns>
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (outputs.NullOrEmpty() || outputs.Any(o => o.bolt == null || o.countPerUnit < 1))
            {
                yield return "needs outputs, each with a bolt and a positive countPerUnit";
            }

            var fuel = parentDef.GetCompProperties<CompProperties_Refuelable>();
            if (fuel == null || !fuel.consumeFuelOnlyWhenUsed)
            {
                yield return "needs CompProperties_Refuelable with consumeFuelOnlyWhenUsed";
            }
        }
    }

    /// <summary>Works batshittium into bolts while powered and loaded.</summary>
    public class CompBatshittiumArtificer : ThingComp
    {
        private int progressTicks;
        private ThingDef selectedBolt;
        private CompRefuelable fuel;
        private CompPowerTrader power;

        /// <summary>Typed access to this comp's XML properties.</summary>
        public CompProperties_BatshittiumArtificer Props =>
            (CompProperties_BatshittiumArtificer)this.props;

        /// <summary>Outputs whose research is done.</summary>
        private IEnumerable<ArtificerOutput> Available => Props.outputs.Where(
            o => o.research == null || o.research.IsFinished);

        /// <summary>
        /// What it is making: the player's pick if researched, otherwise the
        /// first researched output, or null if none is.
        /// </summary>
        public ArtificerOutput Current =>
            Available.FirstOrDefault(o => o.bolt == selectedBolt) ?? Available.FirstOrDefault();

        /// <summary>True when powered, loaded and something is researched.</summary>
        private bool CanWork =>
            (power == null || power.PowerOn) && fuel != null && fuel.HasFuel && Current != null;

        /// <summary>Caches the power and fuel comps.</summary>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            fuel = parent.GetComp<CompRefuelable>();
            power = parent.GetComp<CompPowerTrader>();
        }

        /// <summary>Saves and loads progress and the selection.</summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref progressTicks, "progressTicks", 0);
            Scribe_Defs.Look(ref selectedBolt, "selectedBolt");
        }

        /// <summary>Advances the current unit; finishes it when done.</summary>
        public override void CompTick()
        {
            base.CompTick();
            if (!CanWork)
            {
                return;
            }

            progressTicks++;
            if (progressTicks >= Props.ticksPerUnit)
            {
                FinishUnit();
            }
        }

        /// <summary>
        /// Spends one unit of batshittium and drops the bolts at the
        /// interaction spot (smoke test and DEV gizmo call this directly).
        /// </summary>
        internal void FinishUnit()
        {
            ArtificerOutput output = Current;
            if (output == null || fuel == null || !fuel.HasFuel)
            {
                return;
            }

            progressTicks = 0;
            fuel.ConsumeFuel(1f);
            Thing bolts = ThingMaker.MakeThing(output.bolt);
            bolts.stackCount = output.countPerUnit;
            GenPlace.TryPlaceThing(bolts, DropCell(), parent.Map, ThingPlaceMode.Near);
        }

        /// <summary>The interaction spot, or the building's cell.</summary>
        /// <returns>Where bolts land.</returns>
        private IntVec3 DropCell() =>
            parent.def.hasInteractionCell ? parent.InteractionCell : parent.Position;

        /// <summary>What it is making and how far along it is.</summary>
        /// <returns>Inspect pane line.</returns>
        public override string CompInspectStringExtra()
        {
            ArtificerOutput output = Current;
            if (output == null)
            {
                return "Idle: research a bolt type to make";
            }

            string making = $"Making {output.countPerUnit} {output.bolt.label} per unit";
            if (!CanWork)
            {
                return making + " (idle: needs power and batshittium)";
            }

            float pct = (float)progressTicks / Props.ticksPerUnit;
            return making + $": {pct.ToStringPercent()}";
        }

        /// <summary>Output switch, plus a DEV button to finish the unit.</summary>
        /// <returns>The gizmos.</returns>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            List<ArtificerOutput> available = Available.ToList();
            ArtificerOutput current = Current;
            if (parent.Faction == Faction.OfPlayer && current != null)
            {
                var command = new Command_Action
                {
                    defaultLabel = "Making: " + current.bolt.LabelCap,
                    defaultDesc = "Switch the bolt type the Artificer makes.",
                    icon = current.bolt.uiIcon,
                    action = () =>
                    {
                        int next = (available.IndexOf(current) + 1) % available.Count;
                        selectedBolt = available[next].bolt;
                    }
                };
                if (available.Count < 2)
                {
                    command.Disable("Research another bolt type first.");
                }

                yield return command;
            }

            if (DebugSettings.godMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Finish unit",
                    action = FinishUnit
                };
            }
        }

        /// <summary>Selects an output (smoke test).</summary>
        /// <param name="bolt">The bolt item to make.</param>
        internal void Select(ThingDef bolt) => selectedBolt = bolt;
    }
}
