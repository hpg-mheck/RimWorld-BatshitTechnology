using System;
using System.Globalization;

namespace BatshitTechnology.Core;

/// <summary>
/// One of a Tractor Bomb's three named settings (docs/TRACTOR-BOMB.md,
/// "Named Settings"). Each has three stops, numbered 0 to 2 from the
/// smallest, with the default in the middle.
/// </summary>
public enum TractorBombDial
{
    /// <summary>
    /// How close it drives: <see cref="TractorBombProximity"/>.
    /// </summary>
    Proximity = 0,

    /// <summary>
    /// How far its tractor beam reaches: <see cref="TractorBombPull"/>.
    /// </summary>
    Pull = 1,

    /// <summary>
    /// How far its blast reaches: <see cref="TractorBombBlast"/>.
    /// </summary>
    Blast = 2,
}

/// <summary>
/// A Tractor Bomb's three settings together: a robot's, or the Tractor
/// Barn's for the robots it builds. A planted bomb ignores its
/// proximity.
/// </summary>
/// <param name="Proximity">How close it drives.</param>
/// <param name="Pull">How far its tractor beam reaches.</param>
/// <param name="Blast">How far its blast reaches.</param>
public readonly record struct TractorBombSettings(
    TractorBombProximity Proximity, TractorBombPull Pull,
    TractorBombBlast Blast)
{
    /// <summary>The factory settings: Prudent, Neighborly, Firework.</summary>
    public static TractorBombSettings Default => new(
        TractorBombApproach.Default, TractorBombYield.DefaultPull,
        TractorBombYield.DefaultBlast);

    /// <summary>
    /// The same settings with every undefined value (from a save edited
    /// by hand) replaced by its default.
    /// </summary>
    /// <returns>Defined settings.</returns>
    public TractorBombSettings Sanitized() => new(
        TractorBombApproach.Sanitize(Proximity),
        TractorBombYield.Sanitize(Pull), TractorBombYield.Sanitize(Blast));

    /// <summary>One setting's stop, numbered 0 to 2.</summary>
    /// <param name="dial">Which setting.</param>
    /// <returns>Its stop; an unknown dial gives the default, 1.</returns>
    public int Stop(TractorBombDial dial) => dial switch
    {
        TractorBombDial.Proximity => (int)TractorBombApproach.Sanitize(Proximity),
        TractorBombDial.Pull => (int)TractorBombYield.Sanitize(Pull),
        TractorBombDial.Blast => (int)TractorBombYield.Sanitize(Blast),
        _ => TractorBombDials.DefaultStop,
    };

    /// <summary>The same settings with one setting changed.</summary>
    /// <param name="dial">Which setting.</param>
    /// <param name="stop">Its new stop, 0 to 2; any other is the default.</param>
    /// <returns>The new settings.</returns>
    public TractorBombSettings With(TractorBombDial dial, int stop)
    {
        int s = TractorBombDials.Sanitize(stop);
        return dial switch
        {
            TractorBombDial.Proximity =>
                this with { Proximity = (TractorBombProximity)s },
            TractorBombDial.Pull => this with { Pull = (TractorBombPull)s },
            TractorBombDial.Blast => this with { Blast = (TractorBombBlast)s },
            _ => this,
        };
    }
}

/// <summary>
/// The player-facing words for a Tractor Bomb's named settings: each
/// setting's title, each stop's name, the menu's entries and the inspect
/// pane's summary. Kept here, free of RimWorld types, so the words and
/// the numbers they quote are unit tested together.
/// </summary>
public static class TractorBombDials
{
    /// <summary>How many stops each setting has.</summary>
    public const int StopCount = 3;

    /// <summary>The default stop of every setting: the middle.</summary>
    public const int DefaultStop = 1;

    /// <summary>The three settings, in the order their buttons show.</summary>
    public static readonly TractorBombDial[] All =
    {
        TractorBombDial.Proximity, TractorBombDial.Pull, TractorBombDial.Blast,
    };

    /// <summary>A stop brought into range; any other number is the default.</summary>
    /// <param name="stop">The stop.</param>
    /// <returns>0, 1 or 2.</returns>
    public static int Sanitize(int stop) =>
        stop >= 0 && stop < StopCount ? stop : DefaultStop;

    /// <summary>A setting's title, as its button and pane show it.</summary>
    /// <param name="dial">The setting.</param>
    /// <returns>"Proximity", "Pull" or "Blast".</returns>
    public static string Title(TractorBombDial dial) => dial switch
    {
        TractorBombDial.Proximity => "Proximity",
        TractorBombDial.Pull => "Pull",
        _ => "Blast",
    };

    /// <summary>A proximity's name.</summary>
    /// <param name="proximity">The proximity.</param>
    /// <returns>"Timid", "Prudent" or "Nosy".</returns>
    public static string Name(TractorBombProximity proximity) =>
        TractorBombApproach.Sanitize(proximity) switch
        {
            TractorBombProximity.Timid => "Timid",
            TractorBombProximity.Nosy => "Nosy",
            _ => "Prudent",
        };

    /// <summary>A pull's name.</summary>
    /// <param name="pull">The pull.</param>
    /// <returns>"Intimate", "Neighborly" or "Popular".</returns>
    public static string Name(TractorBombPull pull) =>
        TractorBombYield.Sanitize(pull) switch
        {
            TractorBombPull.Intimate => "Intimate",
            TractorBombPull.Popular => "Popular",
            _ => "Neighborly",
        };

    /// <summary>A blast's name.</summary>
    /// <param name="blast">The blast.</param>
    /// <returns>"Birthday Cake", "Firework" or "Block Party".</returns>
    public static string Name(TractorBombBlast blast) =>
        TractorBombYield.Sanitize(blast) switch
        {
            TractorBombBlast.BirthdayCake => "Birthday Cake",
            TractorBombBlast.BlockParty => "Block Party",
            _ => "Firework",
        };

    /// <summary>A stop's name, for any setting.</summary>
    /// <param name="dial">The setting.</param>
    /// <param name="stop">The stop, 0 to 2; any other is the default.</param>
    /// <returns>Its name.</returns>
    public static string Name(TractorBombDial dial, int stop)
    {
        int s = Sanitize(stop);
        return dial switch
        {
            TractorBombDial.Proximity => Name((TractorBombProximity)s),
            TractorBombDial.Pull => Name((TractorBombPull)s),
            _ => Name((TractorBombBlast)s),
        };
    }

    /// <summary>
    /// A stop's size in tiles: how far a robot stops from its quarry for a
    /// proximity, the radius for a pull or a blast.
    /// </summary>
    /// <param name="dial">The setting.</param>
    /// <param name="stop">The stop, 0 to 2; any other is the default.</param>
    /// <param name="pull">
    /// The tractor beam in effect, in tiles, which a proximity's stop
    /// depends on; unused for the others.
    /// </param>
    /// <param name="timidStop">The robot's emplacing range, in tiles.</param>
    /// <returns>The size, in tiles.</returns>
    public static double Tiles(
        TractorBombDial dial, int stop, double pull, double timidStop)
    {
        int s = Sanitize(stop);
        return dial switch
        {
            TractorBombDial.Proximity => TractorBombApproach.StopDistance(
                (TractorBombProximity)s, timidStop, pull),
            TractorBombDial.Pull => TractorBombYield.Radius((TractorBombPull)s),
            _ => TractorBombYield.Radius((TractorBombBlast)s),
        };
    }

    /// <summary>
    /// A menu entry: the stop's name and its size, as "Popular, 33.75
    /// tiles" or, for a proximity, "Prudent, stops 8 tiles out".
    /// </summary>
    /// <param name="dial">The setting.</param>
    /// <param name="stop">The stop.</param>
    /// <param name="pull">The tractor beam in effect, in tiles.</param>
    /// <param name="timidStop">The robot's emplacing range, in tiles.</param>
    /// <returns>The entry.</returns>
    public static string MenuLabel(
        TractorBombDial dial, int stop, double pull, double timidStop)
    {
        string tiles = TilesText(Tiles(dial, stop, pull, timidStop));
        return dial == TractorBombDial.Proximity
            ? $"{Name(dial, stop)}, stops {tiles} out"
            : $"{Name(dial, stop)}, {tiles}";
    }

    /// <summary>
    /// The settings' names in one line, for the inspect pane: "Prudent,
    /// Popular, Block Party", or without the proximity for a planted bomb.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="withProximity">Whether to name the proximity.</param>
    /// <returns>The line.</returns>
    public static string Summary(TractorBombSettings settings, bool withProximity)
    {
        string yield = $"{Name(settings.Pull)}, {Name(settings.Blast)}";
        return withProximity ? $"{Name(settings.Proximity)}, {yield}" : yield;
    }

    /// <summary>
    /// A distance for players: decimal places only when it has them, up
    /// to two, so every stop reads exactly ("33.75 tiles", "22.5 tiles",
    /// "15 tiles"), and "tile" for exactly one.
    /// </summary>
    /// <param name="tiles">The distance, in tiles.</param>
    /// <returns>The text.</returns>
    public static string TilesText(double tiles)
    {
        string n = double.IsNaN(tiles)
            ? "?"
            : Math.Round(tiles, 2, MidpointRounding.AwayFromZero)
                .ToString("0.##", CultureInfo.InvariantCulture);
        return n == "1" ? "1 tile" : n + " tiles";
    }
}
