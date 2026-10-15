using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// What the tour's stage cleared from its map before READY
/// (docs/SHOWCASE.md, "A Clean Stage"): the map's own hostiles, which the
/// stage's clearing of the fog would otherwise wake, and the trees whose
/// destruction would cost the colonists their mood.
/// </summary>
/// <param name="Hives">Insect hives, dormant or awake.</param>
/// <param name="Nests">
/// Other things that release a hostile when disturbed or killed: insect
/// egg sacs (Odyssey) and stasis cocoons.
/// </param>
/// <param name="HostilePawns">
/// Hostile pawns the map generated, asleep or awake (temple mechanoids and
/// fleshbeasts, cave-hive insects).
/// </param>
/// <param name="CasketOccupants">
/// Pawns sealed in ancient cryptosleep caskets, which a damaged casket
/// would release, with every casket of its group.
/// </param>
/// <param name="AnimaTrees">Anima trees anywhere on the map.</param>
/// <param name="ZoneTrees">Other trees in the volleys' zone.</param>
public readonly record struct StageClearance(
    int Hives, int Nests, int HostilePawns, int CasketOccupants,
    int AnimaTrees, int ZoneTrees)
{
    /// <summary>Gets everything cleared, counted together.</summary>
    public int Total =>
        Hives + Nests + HostilePawns + CasketOccupants + AnimaTrees + ZoneTrees;

    /// <summary>
    /// The log's line: each kind with its count, or "nothing to clear".
    /// </summary>
    /// <returns>The text, such as "2 hives, 0 hostile pawns, ...".</returns>
    public override string ToString() => Total == 0
        ? "nothing to clear"
        : string.Join(", ", Parts());

    /// <summary>Each kind's count, with its name.</summary>
    /// <returns>The six parts, in a fixed order.</returns>
    private IEnumerable<string> Parts()
    {
        yield return Count(Hives, "hive", "hives");
        yield return Count(Nests, "egg sac or cocoon", "egg sacs and cocoons");
        yield return Count(HostilePawns, "hostile pawn", "hostile pawns");
        yield return Count(CasketOccupants, "casket occupant", "casket occupants");
        yield return Count(AnimaTrees, "anima tree", "anima trees");
        yield return Count(ZoneTrees, "tree in the volley zone", "trees in the volley zone");
    }

    /// <summary>A count with its noun, singular for one.</summary>
    /// <param name="n">The count.</param>
    /// <param name="one">The singular noun.</param>
    /// <param name="many">The plural noun.</param>
    /// <returns>The text.</returns>
    private static string Count(int n, string one, string many) =>
        $"{n} {(n == 1 ? one : many)}";
}
