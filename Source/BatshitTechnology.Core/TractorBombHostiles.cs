using System;

namespace BatshitTechnology.Core;

/// <summary>
/// What a hostile pawn does on first seeing an armed Tractor Bomb
/// (docs/TRACTOR-BOMB.md, "Decisions" 9). Each is rolled once per pawn per
/// bomb, a third each. Saved as its number, so the order must not change.
/// </summary>
public enum TractorBombHostileReaction
{
    /// <summary>Runs at once, as everyone else does.</summary>
    Flee = 0,

    /// <summary>
    /// Notices late: starts to run about two seconds after it first saw
    /// the bomb.
    /// </summary>
    Hesitate = 1,

    /// <summary>Ignores it entirely and carries on with its assault.</summary>
    Ignore = 2,
}

/// <summary>
/// How hostiles react to an armed Tractor Bomb: the roll that sorts them
/// into the three reactions, and when each one starts to run. Everyone who
/// is not hostile (colonists, guests, neutrals, animals) runs at once and
/// never rolls. Kept free of RimWorld types so it can be unit tested; the
/// bomb keeps each pawn's roll (Source/TractorBombEmplaced.cs) and the
/// flee job giver acts on it (Source/TractorBombFlee.cs).
/// </summary>
/// <remarks>
/// The roll hashes the pawn's id, the bomb's id and a seed the bomb draws
/// when it is planted, so it is random per pawn and per bomb, the same
/// every time it is asked, and reproducible from a fixed seed (the smoke
/// test's "seeded run").
/// </remarks>
public static class TractorBombHostiles
{
    /// <summary>
    /// How long a hesitating hostile takes to notice, in ticks: about two
    /// seconds.
    /// </summary>
    public const int HesitationTicks = 2 * TractorBombTiming.TicksPerSecond;

    /// <summary>
    /// The reaction for a uniform number: the lowest third flees, the
    /// middle third hesitates, the top third ignores the bomb.
    /// </summary>
    /// <param name="u">
    /// A number in [0, 1). Values below 0 count as 0, values of 1 or more
    /// as just under 1, and a value that is not a number flees (the safe
    /// mistake: running is what everyone else does).
    /// </param>
    /// <returns>The reaction.</returns>
    public static TractorBombHostileReaction FromUnit(double u)
    {
        if (double.IsNaN(u) || u < 1d / 3d)
        {
            return TractorBombHostileReaction.Flee;
        }

        return u < 2d / 3d
            ? TractorBombHostileReaction.Hesitate
            : TractorBombHostileReaction.Ignore;
    }

    /// <summary>
    /// A hostile's reaction to a bomb: rolled from the two ids and the
    /// bomb's seed, so asking again gives the same answer.
    /// </summary>
    /// <param name="pawnId">The pawn's thing id number.</param>
    /// <param name="bombId">The bomb's thing id number.</param>
    /// <param name="seed">The bomb's seed.</param>
    /// <returns>The reaction.</returns>
    public static TractorBombHostileReaction Roll(
        int pawnId, int bombId, int seed) =>
        FromUnit(Unit(pawnId, bombId, seed));

    /// <summary>
    /// Whether a hostile reacts to a bomb now: one that flees, at once; one
    /// that hesitates, once <see cref="HesitationTicks"/> have passed since
    /// it first saw the bomb; one that ignores it, never.
    /// </summary>
    /// <param name="reaction">Its rolled reaction.</param>
    /// <param name="firstSeenTick">
    /// The game tick it first saw the bomb.
    /// </param>
    /// <param name="nowTick">The game tick now.</param>
    /// <returns>True if it should run (or freeze) now.</returns>
    public static bool ReactsNow(
        TractorBombHostileReaction reaction, int firstSeenTick, int nowTick)
    {
        switch (reaction)
        {
            case TractorBombHostileReaction.Ignore:
                return false;
            case TractorBombHostileReaction.Hesitate:
                return (long)nowTick - firstSeenTick >= HesitationTicks;
            default:
                return true;
        }
    }

    /// <summary>
    /// A reaction brought into range: a value none has (from a save
    /// edited by hand) flees, as everyone else does.
    /// </summary>
    /// <param name="reaction">The value read.</param>
    /// <returns>A defined reaction.</returns>
    public static TractorBombHostileReaction Sanitize(
        TractorBombHostileReaction reaction) =>
        reaction is TractorBombHostileReaction.Hesitate
            or TractorBombHostileReaction.Ignore
            ? reaction
            : TractorBombHostileReaction.Flee;

    /// <summary>
    /// A number in [0, 1) from the two ids and the seed, every input bit
    /// spread over the result.
    /// </summary>
    /// <param name="pawnId">The pawn's thing id number.</param>
    /// <param name="bombId">The bomb's thing id number.</param>
    /// <param name="seed">The bomb's seed.</param>
    /// <returns>The number.</returns>
    private static double Unit(int pawnId, int bombId, int seed)
    {
        unchecked
        {
            uint h = Mix((uint)seed ^ 0x51ED270Bu);
            h = Mix(h ^ ((uint)bombId * 0x9E3779B1u));
            h = Mix(h ^ ((uint)pawnId * 0x85EBCA77u));
            return h / 4294967296d;
        }
    }

    /// <summary>
    /// MurmurHash3's 32-bit finaliser, as <see cref="TractorBombFlee"/>
    /// uses for its line pick.
    /// </summary>
    /// <param name="h">The value to mix.</param>
    /// <returns>The mixed value.</returns>
    private static uint Mix(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }
}
