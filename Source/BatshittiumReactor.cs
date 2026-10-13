// RandomReplicator - Batshittium Reactor
// Burns one unit of batshittium at a time for a random 0.1-36 in-game hours.
// Vanilla CompPowerPlant supplies the power (250,000 W in the Def) whenever
// a unit is loaded and the reactor is switched on, and CompRefuelable has
// haulers load batshittium. This comp times each burn, spends the unit when
// it ends, and dumps a huge amount of waste heat into every room within
// range, so the reactor has to be sited away from housing.

using System.Collections.Generic;
using RandomReplicator.Core;
using Verse;
using RimWorld;
using UnityEngine;

namespace RandomReplicator
{
    /// <summary>
    /// XML-facing properties for <see cref="CompBatshittiumReactor"/>. See
    /// Defs/ThingDefs/Buildings_Batshittium.xml.
    /// </summary>
    public class CompProperties_BatshittiumReactor : CompProperties
    {
        /// <summary>In-game hours one unit of batshittium burns.</summary>
        public FloatRange burnHoursRange = new FloatRange(0.1f, 36f);

        /// <summary>Total waste heat per second (a vanilla heater: 21).</summary>
        public float heatPerSecond = 1000f;

        /// <summary>Rooms within this many tiles take a share.</summary>
        public float heatRadius = 12f;

        /// <summary>
        /// Rooms stop heating at this temperature (°C). Things auto-ignite
        /// above 240 °C.
        /// </summary>
        public float heatMaxTemperature = 200f;

        /// <summary>Binds these properties to their comp class.</summary>
        public CompProperties_BatshittiumReactor()
        {
            compClass = typeof(CompBatshittiumReactor);
        }

        /// <summary>
        /// Reports Def mistakes at load: the comp spends fuel itself, ticks
        /// every tick, and draws its radius with an engine-capped helper.
        /// </summary>
        /// <param name="parentDef">The ThingDef carrying this comp.</param>
        /// <returns>One message per configuration error.</returns>
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            var refuelable =
                parentDef.GetCompProperties<CompProperties_Refuelable>();
            if (refuelable == null || !refuelable.consumeFuelOnlyWhenUsed)
            {
                yield return "needs CompProperties_Refuelable with "
                    + "consumeFuelOnlyWhenUsed: this comp spends the fuel";
            }

            if (parentDef.tickerType != TickerType.Normal)
            {
                yield return "needs tickerType Normal";
            }

            if (heatRadius > GenRadial.MaxRadialPatternRadius)
            {
                yield return "heatRadius must not exceed the engine maximum "
                    + $"of {GenRadial.MaxRadialPatternRadius:F1}";
            }
        }
    }

    /// <summary>
    /// Times each unit's burn, spends it when the burn ends, and spreads
    /// waste heat into nearby rooms while running.
    /// </summary>
    public class CompBatshittiumReactor : ThingComp
    {
        private const int HeatIntervalTicks = 250;

        /// <summary>Color of the heat-radius ring.</summary>
        public static readonly Color HeatRingColor = new Color(1f, 0.45f, 0.1f);

        private static readonly Dictionary<Room, float> heatWeightByRoom =
            new Dictionary<Room, float>();

        private int burnTicksLeft; // > 0 while a unit is burning
        private CompRefuelable refuelable;

        /// <summary>Typed access to this comp's XML properties.</summary>
        public CompProperties_BatshittiumReactor Props =>
            (CompProperties_BatshittiumReactor)this.props;

        /// <summary>
        /// True while a unit is burning and the reactor is switched on.
        /// </summary>
        private bool Running =>
            burnTicksLeft > 0 && FlickUtility.WantsToBeOn(parent);

        /// <summary>Caches the refuelable comp once spawned.</summary>
        /// <param name="respawningAfterLoad">True when loading a save.</param>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            refuelable = parent.GetComp<CompRefuelable>();
        }

        /// <summary>Saves and loads the current burn's remaining time.</summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref burnTicksLeft, "burnTicksLeft", 0);
        }

        /// <summary>
        /// Starts a burn when a unit is loaded, counts it down while
        /// switched on, pushes heat periodically, and spends the unit when
        /// the burn ends.
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            if (!refuelable.HasFuel)
            {
                return;
            }

            // A loaded unit starts burning; how long it lasts is anyone's
            // guess.
            if (burnTicksLeft <= 0)
            {
                float hours = Props.burnHoursRange.RandomInRange;
                burnTicksLeft =
                    Mathf.Max(1, Mathf.RoundToInt(hours * GenDate.TicksPerHour));
            }

            if (!FlickUtility.WantsToBeOn(parent))
            {
                return; // switched off: the reaction pauses
            }

            burnTicksLeft--;
            if (parent.IsHashIntervalTick(HeatIntervalTicks))
            {
                PushWasteHeat();
            }

            // Spent. CompPowerPlant stops producing unless another unit is
            // already loaded; haulers bring the next.
            if (burnTicksLeft <= 0)
            {
                refuelable.ConsumeFuel(1f);
            }
        }

        /// <summary>
        /// Spreads the heat over every cell in range, weighted toward the
        /// reactor, and gives each indoor room its cells' share. Outdoor
        /// cells' share dissipates, so nearby rooms cook whether the reactor
        /// is inside or out.
        /// </summary>
        private void PushWasteHeat()
        {
            Map map = parent.Map;
            float energy = Props.heatPerSecond * HeatIntervalTicks / 60f;
            float totalWeight = 0f;
            IEnumerable<IntVec3> cells =
                GenRadial.RadialCellsAround(parent.Position, Props.heatRadius, true);
            foreach (IntVec3 c in cells)
            {
                if (!c.InBounds(map))
                {
                    continue;
                }

                float distance = (c - parent.Position).LengthHorizontal;
                float weight =
                    (float)ReactorHeat.CellWeight(distance, Props.heatRadius);
                totalWeight += weight;
                Room room = c.GetRoom(map);
                if (room == null || room.UsesOutdoorTemperature)
                {
                    continue;
                }

                heatWeightByRoom.TryGetValue(room, out float roomWeight);
                heatWeightByRoom[room] = roomWeight + weight;
            }

            foreach (KeyValuePair<Room, float> entry in heatWeightByRoom)
            {
                Room room = entry.Key;
                float headroom = (Props.heatMaxTemperature - room.Temperature)
                    * room.CellCount;
                double roomEnergy = ReactorHeat.RoomEnergy(
                    energy, entry.Value, totalWeight, headroom);
                if (roomEnergy > 0)
                {
                    room.PushHeat((float)roomEnergy);
                }
            }

            heatWeightByRoom.Clear();
        }

        /// <summary>Draws the heat radius while the reactor is selected.</summary>
        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            GenDraw.DrawRadiusRing(parent.Position, Props.heatRadius, HeatRingColor);
        }

        /// <summary>
        /// Reports the reaction's state without revealing how long the
        /// current unit will last (dev mode shows it).
        /// </summary>
        /// <returns>Inspect pane lines.</returns>
        public override string CompInspectStringExtra()
        {
            string text;
            if (burnTicksLeft <= 0)
            {
                text = "Reaction: idle (load batshittium to start)";
            }
            else if (!FlickUtility.WantsToBeOn(parent))
            {
                text = "Reaction: paused (switched off)";
            }
            else
            {
                text = "Reaction: burning (each unit lasts "
                    + $"{Props.burnHoursRange.min:0.#} to "
                    + $"{Props.burnHoursRange.max:0.#} hours, unpredictably)";
            }

            if (Prefs.DevMode && burnTicksLeft > 0)
            {
                text += $" [dev: {burnTicksLeft.ToStringTicksToPeriod()} left]";
            }

            if (Running)
            {
                text += "\nWaste heat: heating rooms within "
                    + $"{Props.heatRadius:0} tiles";
            }

            return text;
        }
    }

    /// <summary>
    /// Shows the reactor's waste-heat radius while placing it, so players
    /// can keep it away from housing.
    /// </summary>
    public class PlaceWorker_ShowReactorHeatRadius : PlaceWorker
    {
        /// <summary>Draws the heat radius around the placement ghost.</summary>
        /// <param name="def">The building being placed.</param>
        /// <param name="center">The ghost's position.</param>
        /// <param name="rot">The ghost's rotation.</param>
        /// <param name="ghostCol">The ghost's tint.</param>
        /// <param name="thing">The thing being reinstalled, if any.</param>
        public override void DrawGhost(
            ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol,
            Thing thing = null)
        {
            var props =
                def.GetCompProperties<CompProperties_BatshittiumReactor>();
            if (props != null)
            {
                GenDraw.DrawRadiusRing(
                    center, props.heatRadius, CompBatshittiumReactor.HeatRingColor);
            }
        }
    }
}
