using System;

namespace RandomReplicator.Core;

/// <summary>
/// Waste-heat arithmetic for the Batshittium Reactor. The reactor spreads
/// its heat over every cell within a radius, weighted toward itself, and
/// each indoor room receives its cells' share. Kept free of RimWorld types
/// so the numbers can be unit tested.
/// </summary>
public static class ReactorHeat
{
    /// <summary>
    /// Weight of a cell when heat is spread over a disc. It is 1 at the
    /// reactor and falls linearly with distance, staying positive out to
    /// the edge of the radius.
    /// </summary>
    /// <param name="distance">Distance from the reactor, in tiles.</param>
    /// <param name="radius">Heat radius, in tiles. Must be positive.</param>
    /// <returns>The cell's weight, between 0 and 1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="radius"/> is not positive or
    /// <paramref name="distance"/> is negative.
    /// </exception>
    public static double CellWeight(double distance, double radius)
    {
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radius), radius, "Heat radius must be positive.");
        }

        if (distance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(distance), distance, "Distance cannot be negative.");
        }

        return Math.Max(0d, 1d - (distance / (radius + 1d)));
    }

    /// <summary>
    /// Heat energy one room receives: its weighted share of the total,
    /// capped so the room never passes the reactor's maximum temperature.
    /// </summary>
    /// <param name="totalEnergy">Heat released this interval.</param>
    /// <param name="roomWeight">Summed weight of the room's cells.</param>
    /// <param name="totalWeight">
    /// Summed weight of every in-bounds cell, indoors or out. Outdoor cells
    /// count here, so their share dissipates.
    /// </param>
    /// <param name="headroom">
    /// Energy that would bring the room to the maximum temperature.
    /// </param>
    /// <returns>
    /// The energy to push into the room; zero when the room is already at
    /// or above the cap, or any weight is non-positive.
    /// </returns>
    public static double RoomEnergy(
        double totalEnergy, double roomWeight, double totalWeight,
        double headroom)
    {
        if (totalEnergy <= 0 || roomWeight <= 0 || totalWeight <= 0
            || headroom <= 0)
        {
            return 0d;
        }

        return Math.Min(totalEnergy * roomWeight / totalWeight, headroom);
    }
}
