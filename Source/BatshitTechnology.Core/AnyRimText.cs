using System;

namespace BatshitTechnology.Core;

/// <summary>
/// Everything AnyRim says: its signs, labels and messages, in the voice of
/// docs/ARTISTIC-MOTIF.md. Kept here so it can be unit tested.
/// </summary>
public static class AnyRimText
{
    /// <summary>The town's name.</summary>
    public const string TownName = "AnyRim";

    /// <summary>
    /// The welcome sign: four lines, the third carrying the live
    /// population, which at zero reads "Pop. 0. Thank you for your
    /// participation."
    /// </summary>
    /// <param name="population">Living residents; never negative.</param>
    /// <returns>The sign's text, lines separated by "\n".</returns>
    public static string WelcomeSign(int population) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

    /// <summary>
    /// The message when the census drops: "AnyRim census revised to 7. The
    /// sign has been updated. Please remain calm."
    /// </summary>
    /// <param name="population">The new population.</param>
    /// <returns>The message.</returns>
    public static string CensusMessage(int population) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

    /// <summary>The message when the showcase finishes building the town.</summary>
    public static string OpeningMessage =>
        throw new NotImplementedException("Filled in by the town plan implementation.");

    /// <summary>The text of a map label.</summary>
    /// <param name="kind">Which label.</param>
    /// <param name="population">Living residents, for the welcome sign.</param>
    /// <param name="daysWithoutIncident">Whole days since the last resident died.</param>
    /// <returns>The text, lines separated by "\n".</returns>
    public static string Label(LabelKind kind, int population, int daysWithoutIncident) =>
        throw new NotImplementedException("Filled in by the town plan implementation.");
}
