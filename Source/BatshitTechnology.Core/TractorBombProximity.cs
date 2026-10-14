using System;

namespace BatshitTechnology.Core;

/// <summary>
/// How close a Tractor Bomb drives to its quarry before it plants itself
/// (docs/TRACTOR-BOMB.md, "Named Settings"; it was Disposition). Set per
/// robot; saved by name, so the names must not change.
/// </summary>
public enum TractorBombProximity
{
    /// <summary>Stops 15 tiles out (Disposition's Cautious).</summary>
    Timid = 0,

    /// <summary>
    /// The default: stops at 15 tiles or one tile inside its tractor beam,
    /// whichever is nearer, so a target standing still is caught
    /// (Disposition's Moderate).
    /// </summary>
    Prudent = 1,

    /// <summary>Drives right up, to a few tiles.</summary>
    Nosy = 2,
}

/// <summary>
/// The arithmetic of a Tractor Bomb's proximity: where each one stops,
/// and what an older save's Disposition becomes. Kept free of RimWorld
/// types so it can be unit tested; the robot's comp and its approach live
/// in Source/TractorBomb.cs.
/// </summary>
/// <remarks>
/// Distances are in tiles, measured as RimWorld's <c>InHorDistOf</c> does.
/// The pull radius passes through <see cref="TractorBombYield.ClampPull"/>,
/// so a nonsense value cannot give a nonsense stop.
/// </remarks>
public static class TractorBombApproach
{
    /// <summary>
    /// The timid stop when nothing else is given, in tiles: the spec's 15.
    /// </summary>
    public const int DefaultTimidStop = 15;

    /// <summary>How close a nosy robot drives, in tiles.</summary>
    public const int NosyStop = 3;

    /// <summary>A new robot's proximity.</summary>
    public const TractorBombProximity Default = TractorBombProximity.Prudent;

    /// <summary>
    /// How far from its quarry a robot stops to plant itself.
    /// </summary>
    /// <param name="proximity">The robot's proximity.</param>
    /// <param name="timidStop">
    /// The timid stop, in tiles: the robot's XML emplacing range. Not a
    /// number, or not positive, counts as <see cref="DefaultTimidStop"/>.
    /// </param>
    /// <param name="pull">The robot's pull radius, in tiles.</param>
    /// <returns>
    /// Timid: <paramref name="timidStop"/>. Prudent: the nearer of that
    /// and one tile inside the clamped pull radius. Nosy: the nearer of
    /// <see cref="NosyStop"/> and the prudent stop. An undefined value
    /// counts as prudent.
    /// </returns>
    public static double StopDistance(
        TractorBombProximity proximity, double timidStop, double pull)
    {
        double timid = double.IsNaN(timidStop) || timidStop <= 0
            ? DefaultTimidStop
            : timidStop;
        double prudent = Math.Min(
            timid, TractorBombYield.ClampPull(pull) - 1);
        return Sanitize(proximity) switch
        {
            TractorBombProximity.Timid => timid,
            TractorBombProximity.Nosy => Math.Min(NosyStop, prudent),
            _ => prudent,
        };
    }

    /// <summary>
    /// A proximity brought into range: a value no setting has (from a save
    /// edited by hand, or a later version's) becomes the default.
    /// </summary>
    /// <param name="proximity">The value read.</param>
    /// <returns>A defined proximity.</returns>
    public static TractorBombProximity Sanitize(
        TractorBombProximity proximity) =>
        proximity is TractorBombProximity.Timid
            or TractorBombProximity.Prudent
            or TractorBombProximity.Nosy
            ? proximity
            : Default;

    /// <summary>
    /// What a Disposition saved by an older version becomes: Cautious,
    /// Moderate and Nosy load as Timid, Prudent and Nosy. The old names'
    /// numbers (0, 1, 2) are read the same way.
    /// </summary>
    /// <param name="saved">The saved text, or null if there was none.</param>
    /// <returns>
    /// The proximity, or null when nothing (or nothing recognisable) was
    /// saved, so the caller keeps what it has.
    /// </returns>
    public static TractorBombProximity? FromDisposition(string? saved)
    {
        switch ((saved ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "cautious":
            case "0":
                return TractorBombProximity.Timid;
            case "moderate":
            case "1":
                return TractorBombProximity.Prudent;
            case "nosy":
            case "2":
                return TractorBombProximity.Nosy;
            default:
                return null;
        }
    }
}
