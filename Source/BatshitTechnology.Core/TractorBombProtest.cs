namespace BatshitTechnology.Core;

/// <summary>
/// Whom the Tractor Bomb protests about (docs/TRACTOR-BOMB.md, "Protest"):
/// a colonist inside its pull, free or enslaved, as before; and, since
/// slice 19000c, a friendly guest: a humanlike of another faction that is
/// not hostile to the colony and not its prisoner (visitors, traders,
/// allies, the showcase's tourists). Either is someone the colony would
/// rather keep, and a guest pulled in costs goodwill in a real game. The
/// protest is a warning, not a refusal: it delays emplacing by three
/// seconds, then the robot plants itself regardless.
/// </summary>
public static class TractorBombProtest
{
    /// <summary>Whom it objects to, for its message and status.</summary>
    public const string Whom = "a colonist or guest";

    /// <summary>Whether the robot objects to a pawn inside its pull.</summary>
    /// <param name="colonist">Whether it is a colonist (free or enslaved).</param>
    /// <param name="humanlike">Whether it is humanlike.</param>
    /// <param name="ofColony">Whether it belongs to the colony's faction.</param>
    /// <param name="hostile">Whether it is hostile to the colony.</param>
    /// <param name="prisoner">Whether it is the colony's prisoner.</param>
    /// <param name="dead">Whether it is dead.</param>
    /// <returns>True for a living colonist or friendly guest.</returns>
    public static bool ObjectsTo(
        bool colonist, bool humanlike, bool ofColony, bool hostile,
        bool prisoner, bool dead) =>
        !dead && (colonist || (humanlike && !ofColony && !hostile && !prisoner));
}
