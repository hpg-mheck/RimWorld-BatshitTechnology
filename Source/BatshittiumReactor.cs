// RandomReplicator - Batshittium Reactor
// Burns one unit of batshittium at a time for a random 0.1-36 in-game hours. Vanilla CompPowerPlant supplies
// the power (250,000 W in the Def) whenever a unit is loaded and the reactor is switched on; CompRefuelable
// has haulers load batshittium. This comp times each burn, spends the unit when it ends, and dumps a huge
// amount of waste heat into every room within range, so the reactor has to be sited away from housing.

using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace RandomReplicator
{
    public class CompProperties_BatshittiumReactor : CompProperties
    {
        public FloatRange burnHoursRange = new FloatRange(0.1f, 36f); // in-game hours per unit
        public float heatPerSecond = 1000f;     // total waste heat; a vanilla heater pushes 21
        public float heatRadius = 12f;          // rooms within this many tiles take a share
        public float heatMaxTemperature = 200f; // stop heating a room here; things auto-ignite above 240 °C

        public CompProperties_BatshittiumReactor() { compClass = typeof(CompBatshittiumReactor); }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            var refuelable = parentDef.GetCompProperties<CompProperties_Refuelable>();
            if (refuelable == null || !refuelable.consumeFuelOnlyWhenUsed)
                yield return "needs CompProperties_Refuelable with consumeFuelOnlyWhenUsed: this comp spends the fuel itself";
            if (parentDef.tickerType != TickerType.Normal)
                yield return "needs tickerType Normal";
            if (heatRadius > GenRadial.MaxRadialPatternRadius)
                yield return $"heatRadius must not exceed the engine maximum of {GenRadial.MaxRadialPatternRadius:F1}";
        }
    }

    public class CompBatshittiumReactor : ThingComp
    {
        private const int HeatIntervalTicks = 250;
        public static readonly Color HeatRingColor = new Color(1f, 0.45f, 0.1f);

        private int burnTicksLeft; // > 0 while a unit is burning
        private CompRefuelable refuelable;

        private static readonly Dictionary<Room, float> heatWeightByRoom = new Dictionary<Room, float>();

        public CompProperties_BatshittiumReactor Props => (CompProperties_BatshittiumReactor)this.props;
        private bool Running => burnTicksLeft > 0 && FlickUtility.WantsToBeOn(parent);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            refuelable = parent.GetComp<CompRefuelable>();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref burnTicksLeft, "burnTicksLeft", 0);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!refuelable.HasFuel) return;

            // A loaded unit starts burning; how long it lasts is anyone's guess.
            if (burnTicksLeft <= 0) burnTicksLeft = Mathf.Max(1, Mathf.RoundToInt(Props.burnHoursRange.RandomInRange * GenDate.TicksPerHour));
            if (!FlickUtility.WantsToBeOn(parent)) return; // switched off: the reaction pauses

            burnTicksLeft--;
            if (parent.IsHashIntervalTick(HeatIntervalTicks)) PushWasteHeat();
            // Spent. CompPowerPlant stops producing unless another unit is already loaded; haulers bring the next.
            if (burnTicksLeft <= 0) refuelable.ConsumeFuel(1f);
        }

        // Spreads the heat over every cell in range, weighted toward the reactor, and gives each indoor room
        // its cells' share. Outdoor cells' share dissipates, so nearby rooms cook whether the reactor is in or out.
        private void PushWasteHeat()
        {
            Map map = parent.Map;
            float energy = Props.heatPerSecond * HeatIntervalTicks / 60f;
            float totalWeight = 0f;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(parent.Position, Props.heatRadius, true))
            {
                if (!c.InBounds(map)) continue;
                float weight = 1f - (c - parent.Position).LengthHorizontal / (Props.heatRadius + 1f);
                totalWeight += weight;
                Room room = c.GetRoom(map);
                if (room == null || room.UsesOutdoorTemperature) continue;
                heatWeightByRoom.TryGetValue(room, out float roomWeight);
                heatWeightByRoom[room] = roomWeight + weight;
            }
            foreach (KeyValuePair<Room, float> entry in heatWeightByRoom)
            {
                Room room = entry.Key;
                float headroom = (Props.heatMaxTemperature - room.Temperature) * room.CellCount;
                if (headroom > 0f) room.PushHeat(Mathf.Min(energy * entry.Value / totalWeight, headroom));
            }
            heatWeightByRoom.Clear();
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            GenDraw.DrawRadiusRing(parent.Position, Props.heatRadius, HeatRingColor);
        }

        public override string CompInspectStringExtra()
        {
            string text;
            if (burnTicksLeft <= 0) text = "Reaction: idle (load batshittium to start)";
            else if (!FlickUtility.WantsToBeOn(parent)) text = "Reaction: paused (switched off)";
            else text = $"Reaction: burning (each unit lasts {Props.burnHoursRange.min:0.#} to {Props.burnHoursRange.max:0.#} hours, unpredictably)";
            if (Prefs.DevMode && burnTicksLeft > 0) text += $" [dev: {burnTicksLeft.ToStringTicksToPeriod()} left]";
            if (Running) text += $"\nWaste heat: heating rooms within {Props.heatRadius:0} tiles";
            return text;
        }
    }

    // Shows the reactor's waste-heat radius while placing it, so players can keep it away from housing.
    public class PlaceWorker_ShowReactorHeatRadius : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            var props = def.GetCompProperties<CompProperties_BatshittiumReactor>();
            if (props != null) GenDraw.DrawRadiusRing(center, props.heatRadius, CompBatshittiumReactor.HeatRingColor);
        }
    }
}
