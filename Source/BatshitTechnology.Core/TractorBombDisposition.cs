using System;

namespace BatshitTechnology.Core;

/// <summary>
/// How close a Tractor Bomb drives to its quarry before it plants itself
/// (docs/TRACTOR-BOMB.md, roadmap "Decided after the core build"). Set per
/// robot; saved as its number, so the order must not change.
/// </summary>
public enum TractorBombDisposition
{
    /// <summary>Stops 15 tiles out, as first written.</summary>
    Cautious = 0,

    /// <summary>
    /// The default: stops at 15 tiles or one tile inside its tractor beam,
    /// whichever is nearer, so a target standing still is caught.
    /// </summary>
    Moderate = 1,

    /// <summary>Drives right up, to a few tiles.</summary>
    Nosy = 2,
}

/// <summary>
/// The arithmetic of a Tractor Bomb's disposition: where each one stops,
/// and how the setting cycles. Kept free of RimWorld types so it can be
/// unit tested; the robot's comp and its approach live in
/// Source/TractorBomb.cs.
/// </summary>
/// <remarks>
/// Distances are in tiles, measured as RimWorld's <c>InHorDistOf</c> does.
/// The pull radius passes through <see cref="TractorBombYield"/>'s clamp,
/// so a value from a save edited by hand cannot give a nonsense stop.
/// </remarks>
public static class TractorBombApproach
{
    /// <summary>
    /// The cautious stop when nothing else is given, in tiles: the spec's
    /// 15.
    /// </summary>
    public const int DefaultCautiousStop = 15;

    /// <summary>How close a nosy robot drives, in tiles.</summary>
    public const int NosyStop = 3;

    /// <summary>A new robot's disposition.</summary>
    public const TractorBombDisposition Default =
        TractorBombDisposition.Moderate;

    /// <summary>
    /// How far from its quarry a robot stops to plant itself.
    /// </summary>
    /// <param name="disposition">The robot's disposition.</param>
    /// <param name="cautiousStop">
    /// The cautious stop, in tiles: the robot's XML emplacing range. Not a
    /// number, or not positive, counts as
    /// <see cref="DefaultCautiousStop"/>.
    /// </param>
    /// <param name="pull">The robot's pull radius, in tiles.</param>
    /// <returns>
    /// Cautious: <paramref name="cautiousStop"/>. Moderate: the nearer of
    /// that and one tile inside the clamped pull radius. Nosy: the nearer
    /// of <see cref="NosyStop"/> and the moderate stop. An unknown value
    /// counts as moderate.
    /// </returns>
    public static double StopDistance(
        TractorBombDisposition disposition, double cautiousStop,
        double pull)
    {
        double cautious = double.IsNaN(cautiousStop) || cautiousStop <= 0
            ? DefaultCautiousStop
            : cautiousStop;
        double moderate = Math.Min(
            cautious, TractorBombYield.ClampPull(pull) - 1);
        return Sanitize(disposition) switch
        {
            TractorBombDisposition.Cautious => cautious,
            TractorBombDisposition.Nosy => Math.Min(NosyStop, moderate),
            _ => moderate,
        };
    }

    /// <summary>
    /// The next setting in the cycle the robot's button steps through:
    /// Cautious, Moderate, Nosy, and round again.
    /// </summary>
    /// <param name="disposition">The current setting.</param>
    /// <returns>The next one.</returns>
    public static TractorBombDisposition Next(
        TractorBombDisposition disposition) =>
        Sanitize(disposition) switch
        {
            TractorBombDisposition.Cautious => TractorBombDisposition.Moderate,
            TractorBombDisposition.Moderate => TractorBombDisposition.Nosy,
            _ => TractorBombDisposition.Cautious,
        };

    /// <summary>
    /// A disposition brought into range: a value no setting has (from a
    /// save edited by hand, or a later version's) becomes the default.
    /// </summary>
    /// <param name="disposition">The value read.</param>
    /// <returns>A defined disposition.</returns>
    public static TractorBombDisposition Sanitize(
        TractorBombDisposition disposition) =>
        disposition is TractorBombDisposition.Cautious
            or TractorBombDisposition.Moderate
            or TractorBombDisposition.Nosy
            ? disposition
            : Default;
}
