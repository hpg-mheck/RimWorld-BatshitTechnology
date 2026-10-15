using System;
using System.Globalization;

namespace BatshitTechnology.Core;

/// <summary>
/// Everything AnyRim says: its signs, labels and messages, in the voice of
/// docs/ARTISTIC-MOTIF.md. Kept here so it can be unit tested.
/// </summary>
public static class AnyRimText
{
    /// <summary>The town's name.</summary>
    public const string TownName = "AnyRim";

    /// <summary>The exit sign, on the east arm.</summary>
    public const string ExitSign =
        "NOW LEAVING ANYRIM\nCome back soon. We will try to be here.";

    /// <summary>The cemetery gate.</summary>
    public const string CemeteryGate =
        "ANYRIM MEMORIAL GARDENS\nReservations only.";

    /// <summary>The substation gate.</summary>
    public const string SubstationGate =
        "SUBSTATION No. 1\nCapacity 250 kW. Demand: a few.";

    /// <summary>The sign on Main Street, just north of the crossing.</summary>
    public const string EvacuationRoute = "EVACUATION ROUTE: NORTH";

    /// <summary>The roundel at the crossroads.</summary>
    public const string GroundZero = "GROUND ZERO";

    /// <summary>
    /// The welcome sign: four lines, the third carrying the live
    /// population, which at zero reads "Pop. 0. Thank you for your
    /// participation."
    /// </summary>
    /// <param name="population">Living residents; never negative.</param>
    /// <returns>The sign's text, lines separated by "\n".</returns>
    /// <exception cref="ArgumentOutOfRangeException">A negative population.</exception>
    public static string WelcomeSign(int population)
    {
        RequireCount(population, nameof(population));
        string census = population == 0
            ? "Pop. 0. Thank you for your participation."
            : $"Pop. {Number(population)}, subject to change";
        return "WELCOME TO ANYRIM\nA Typical Rim Community\n" + census
            + "\nEst. this morning. Re-est. as needed.";
    }

    /// <summary>
    /// The message when the census drops: "AnyRim census revised to 7. The
    /// sign has been updated. Please remain calm."
    /// </summary>
    /// <param name="population">The new population.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A negative population.</exception>
    public static string CensusMessage(int population)
    {
        RequireCount(population, nameof(population));
        return $"AnyRim census revised to {Number(population)}. "
            + "The sign has been updated. Please remain calm.";
    }

    /// <summary>
    /// The census as the narrator reads it after an incident: the census
    /// message (<see cref="CensusMessage"/>) when the population fell, or
    /// "AnyRim census holds at 9. Please remain calm." when no one was
    /// lost. A rise is read as a hold: residents do not come back.
    /// </summary>
    /// <param name="before">The population before the incident.</param>
    /// <param name="after">The population now.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A negative count.</exception>
    public static string CensusReport(int before, int after)
    {
        RequireCount(before, nameof(before));
        RequireCount(after, nameof(after));
        return after < before
            ? CensusMessage(after)
            : $"AnyRim census holds at {Number(after)}. Please remain calm.";
    }

    /// <summary>The message when the showcase finishes building the town.</summary>
    public static string OpeningMessage =>
        "AnyRim, a small town in the north-west, is available for testing. "
        + "Its residents have been informed.";

    /// <summary>The text of a map label.</summary>
    /// <param name="kind">Which label.</param>
    /// <param name="population">Living residents, for the welcome sign.</param>
    /// <param name="daysWithoutIncident">Whole days since the last resident died.</param>
    /// <returns>The text, lines separated by "\n".</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An unknown kind; a negative population for the welcome sign; or a
    /// negative day count for the incident board.
    /// </exception>
    public static string Label(LabelKind kind, int population, int daysWithoutIncident)
    {
        switch (kind)
        {
            case LabelKind.Welcome:
                return WelcomeSign(population);
            case LabelKind.Exit:
                return ExitSign;
            case LabelKind.Cemetery:
                return CemeteryGate;
            case LabelKind.Substation:
                return SubstationGate;
            case LabelKind.Evacuation:
                return EvacuationRoute;
            case LabelKind.Incident:
                RequireCount(daysWithoutIncident, nameof(daysWithoutIncident));
                return $"DAYS WITHOUT INCIDENT: {Number(daysWithoutIncident)}";
            case LabelKind.GroundZero:
                return GroundZero;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind), kind, "Not an AnyRim label.");
        }
    }

    /// <summary>Rejects a negative count.</summary>
    /// <param name="count">The count.</param>
    /// <param name="name">The argument's name, for the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    private static void RequireCount(int count, string name)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(name, count, "Counts are never negative.");
        }
    }

    /// <summary>
    /// A count as plain digits, whatever the player's locale (RimWorld runs
    /// under the system culture).
    /// </summary>
    /// <param name="count">The count.</param>
    /// <returns>Its digits.</returns>
    private static string Number(int count) =>
        count.ToString(CultureInfo.InvariantCulture);
}
