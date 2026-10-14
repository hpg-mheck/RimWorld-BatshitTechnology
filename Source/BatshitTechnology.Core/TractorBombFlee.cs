using System;

namespace BatshitTechnology.Core;

/// <summary>
/// What a pawn or animal does about an armed Tractor Bomb it can see
/// (docs/TRACTOR-BOMB.md, "Everyone Else Runs").
/// </summary>
public enum TractorBombReaction
{
    /// <summary>Beyond the bomb's reach plus the margin: carry on.</summary>
    None,

    /// <summary>
    /// Within the pull radius plus the margin, outside the blast radius:
    /// sprint clear.
    /// </summary>
    Flee,

    /// <summary>
    /// Inside the blast radius: too close to run, so freeze and make
    /// peace.
    /// </summary>
    Freeze,

    /// <summary>
    /// Inside the pull radius once the pull has started: the implosion
    /// has it, and nothing else should give it orders.
    /// </summary>
    Caught,
}

/// <summary>
/// The arithmetic of running away from a Tractor Bomb: how far is far
/// enough, who is too close to run, which cells count as clear, and which
/// "making peace" line a frozen pawn shows. Kept free of RimWorld types so
/// it can be unit tested; the job giver and drivers live in
/// Source/TractorBombFlee.cs.
/// </summary>
/// <remarks>
/// Distances are measured as RimWorld's <c>InHorDistOf</c> does, from cell
/// to cell, so "within r" means dx * dx + dz * dz &lt;= r * r. Radii pass
/// through <see cref="TractorBombYield"/>'s clamps, so a value from a save
/// edited by hand cannot produce a nonsense distance.
/// </remarks>
public static class TractorBombFlee
{
    /// <summary>
    /// How far beyond the bomb's pull radius pawns still run, in tiles:
    /// the spec's "pull radius plus 8".
    /// </summary>
    public const int FleeMargin = 8;

    /// <summary>
    /// How far past the flee distance a destination must lie, in tiles, so
    /// that a pawn that arrives is clearly out, not on the edge, and does
    /// not turn round at once if it drifts back a step.
    /// </summary>
    public const int DestinationClearance = 1;

    /// <summary>
    /// How far from a bomb a pawn runs: its pull radius plus
    /// <see cref="FleeMargin"/>. Never less than one tile past its blast
    /// radius, which the shipped ranges (pull at least 3, blast at most
    /// 10) never need, but a widened range might.
    /// </summary>
    /// <param name="pull">The bomb's pull radius, in tiles.</param>
    /// <param name="blast">The bomb's blast radius, in tiles.</param>
    /// <returns>The flee distance, in whole tiles.</returns>
    public static int FleeDistance(double pull, double blast) =>
        Math.Max(
            TractorBombYield.ClampPull(pull) + FleeMargin,
            TractorBombYield.ClampBlast(blast) + 1);

    /// <summary>
    /// What a pawn at an offset from an armed bomb does about it.
    /// </summary>
    /// <param name="dx">The pawn's cell minus the bomb's, along x.</param>
    /// <param name="dz">The pawn's cell minus the bomb's, along z.</param>
    /// <param name="pull">The bomb's pull radius, in tiles.</param>
    /// <param name="blast">The bomb's blast radius, in tiles.</param>
    /// <param name="pulling">Whether the bomb's pull has started.</param>
    /// <returns>
    /// <see cref="TractorBombReaction.None"/> beyond the flee distance;
    /// <see cref="TractorBombReaction.Caught"/> inside a pull that has
    /// started; <see cref="TractorBombReaction.Freeze"/> inside the blast
    /// radius; otherwise <see cref="TractorBombReaction.Flee"/>.
    /// </returns>
    public static TractorBombReaction React(
        int dx, int dz, double pull, double blast, bool pulling)
    {
        long distanceSquared = ((long)dx * dx) + ((long)dz * dz);
        long flee = FleeDistance(pull, blast);
        if (distanceSquared > flee * flee)
        {
            return TractorBombReaction.None;
        }

        long pullRadius = TractorBombYield.ClampPull(pull);
        if (pulling && distanceSquared <= pullRadius * pullRadius)
        {
            return TractorBombReaction.Caught;
        }

        long blastRadius = TractorBombYield.ClampBlast(blast);
        return distanceSquared <= blastRadius * blastRadius
            ? TractorBombReaction.Freeze
            : TractorBombReaction.Flee;
    }

    /// <summary>
    /// Whether a cell at an offset from a danger is a fit place to run
    /// to: more than <see cref="DestinationClearance"/> tiles beyond the
    /// flee distance.
    /// </summary>
    /// <param name="dx">The cell minus the danger's cell, along x.</param>
    /// <param name="dz">The cell minus the danger's cell, along z.</param>
    /// <param name="fleeDistance">
    /// How far to run from this danger, in tiles: a bomb's
    /// <see cref="FleeDistance"/>, or vanilla's 9 for an explosive about
    /// to go off. A value that is not a number makes every cell unfit,
    /// which is the safe mistake; a negative one counts as 0.
    /// </param>
    /// <returns>True if the cell is clear of this danger.</returns>
    public static bool IsClear(int dx, int dz, double fleeDistance)
    {
        if (double.IsNaN(fleeDistance))
        {
            return false;
        }

        double needed = Math.Max(0d, fleeDistance) + DestinationClearance;
        double distanceSquared = ((double)dx * dx) + ((double)dz * dz);
        return distanceSquared > needed * needed;
    }

    /// <summary>
    /// Which "making peace" line a frozen pawn shows for a bomb: chosen
    /// at random per pawn, but from the two ids, so the same pawn keeps
    /// the same line for as long as that bomb holds it (across a save, or
    /// a job restarted), and different pawns mostly differ.
    /// </summary>
    /// <param name="pawnId">The pawn's thing id number.</param>
    /// <param name="bombId">The bomb's thing id number.</param>
    /// <param name="lineCount">How many lines there are.</param>
    /// <returns>An index in [0, <paramref name="lineCount"/>).</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="lineCount"/> is not positive.
    /// </exception>
    public static int LineIndex(int pawnId, int bombId, int lineCount)
    {
        if (lineCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lineCount), lineCount,
                "At least one line is required.");
        }

        unchecked
        {
            uint h = ((uint)pawnId * 0x9E3779B1u)
                ^ Mix((uint)bombId + 0x7F4A7C15u);
            return (int)(Mix(h) % (uint)lineCount);
        }
    }

    /// <summary>
    /// A status line as a job report: trimmed, with a full stop added
    /// unless it already ends in one (or in another closing mark, which a
    /// translation may prefer), as vanilla's reports have.
    /// </summary>
    /// <param name="line">The line; may be null.</param>
    /// <returns>The report, or an empty string for no line.</returns>
    public static string AsReport(string? line)
    {
        string text = (line ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        char last = text[text.Length - 1];
        return last is '.' or '!' or '?' or '…' ? text : text + ".";
    }

    /// <summary>
    /// MurmurHash3's 32-bit finaliser: spreads every input bit over the
    /// whole result, so neighbouring ids pick unrelated lines.
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
