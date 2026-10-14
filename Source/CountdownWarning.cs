// Batshit Technology - the shared countdown warning.
// docs/ARTISTIC-MOTIF.md: "A device counting down to its own destruction
// wears a one-cell warning band that fades in and out in yellow-orange,
// then turns red and pulses four times as fast in the final seconds. Any
// new countdown should reuse it." CountdownWarningBand draws that band
// around any footprint; MapComponent_SelfDestructOverlay (SelfDestruct.cs)
// draws every ICountdownWarning on the visible map each frame, because
// buildings draw from the static map mesh and get no frame of their own.
// Self-destructs (CompSelfDestruct) and the Tractor Bomb share both.

using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>
    /// Something counting down that draws its warnings every frame while
    /// registered with the map's <see cref="MapComponent_SelfDestructOverlay"/>.
    /// </summary>
    public interface ICountdownWarning
    {
        /// <summary>
        /// Draws its warnings for this frame. Called on the visible map
        /// only, selected or not.
        /// </summary>
        void DrawCountdownOverlay();
    }

    /// <summary>
    /// The pulsing one-cell band around a device counting down: a
    /// translucent fill edged with a brighter line, yellow-orange and slow
    /// until the final seconds, then red and four times as fast. One per
    /// device; it caches the cells it covers.
    /// </summary>
    public sealed class CountdownWarningBand
    {
        /// <summary>
        /// How many opacity levels the band uses; each colour and level is
        /// one pooled material.
        /// </summary>
        private const int PulseLevels = 12;

        /// <summary>The band's colour before the final seconds.</summary>
        private static readonly Color WarningColor =
            new Color(1f, 0.62f, 0.1f); // yellow-orange

        /// <summary>The band's colour in the final seconds.</summary>
        private static readonly Color FinalWarningColor =
            new Color(1f, 0.1f, 0.06f); // red

        private CellRect footprint;
        private List<IntVec3> outline;
        private List<IntVec3> band;

        /// <summary>
        /// Forgets the cells it covered, so the next draw recomputes them
        /// (after the device moves, or respawns).
        /// </summary>
        public void Reset()
        {
            outline = null;
            band = null;
        }

        /// <summary>Draws the band for this frame.</summary>
        /// <param name="around">The device's footprint.</param>
        /// <param name="secondsLeft">Countdown seconds remaining.</param>
        /// <param name="finalWarningSeconds">
        /// When this many seconds or fewer remain, it turns red and pulses
        /// faster.
        /// </param>
        /// <param name="pulsePeriodSeconds">
        /// The pulse period before the final seconds; must be positive.
        /// </param>
        /// <param name="finalPulsePeriodSeconds">
        /// The pulse period in the final seconds; must be positive.
        /// </param>
        public void Draw(
            CellRect around, float secondsLeft, float finalWarningSeconds,
            float pulsePeriodSeconds, float finalPulsePeriodSeconds)
        {
            var (final, alpha) = CountdownPulse.At(
                secondsLeft, finalWarningSeconds, pulsePeriodSeconds,
                finalPulsePeriodSeconds, Time.realtimeSinceStartup);
            float a = (float)CountdownPulse.Quantize(alpha, PulseLevels);
            Color color = final ? FinalWarningColor : WarningColor;
            EnsureCells(around);

            // A translucent band one cell wide around the device, edged
            // with a brighter line at its outside.
            Material fill = SolidColorMaterials.SimpleSolidColorMaterial(
                new Color(color.r, color.g, color.b, a * 0.45f));
            for (int i = 0; i < band.Count; i++)
            {
                CellRenderer.RenderCell(band[i], fill);
            }

            GenDraw.DrawFieldEdges(
                outline, new Color(color.r, color.g, color.b, a));
        }

        /// <summary>
        /// Caches the cells the band covers: the footprint grown by one,
        /// and that ring without the footprint.
        /// </summary>
        /// <param name="around">The device's footprint.</param>
        private void EnsureCells(CellRect around)
        {
            if (outline != null && around == footprint)
            {
                return;
            }

            footprint = around;
            outline = around.ExpandedBy(1).Cells.ToList();
            band = outline.Where(c => !around.Contains(c)).ToList();
        }
    }
}
