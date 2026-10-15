using System;

namespace BatshitTechnology.Core;

/// <summary>What a tourist of the tour group is for.</summary>
public enum TouristRole
{
    /// <summary>The grenade's volunteer, in the junk pile.</summary>
    Volunteer,

    /// <summary>The tractor scene's stray, standing in the robot's pull.</summary>
    Stray,

    /// <summary>
    /// A sightseer: at the stampede's keep-away line, then beside a pad.
    /// </summary>
    Sightseer,
}

/// <summary>
/// The tour group's plan (docs/SHOWCASE.md, "Tourists"; slice 19000c): a
/// party of three to five visitors, of a hidden faction made for
/// showcases, who arrive with the stage, are walked from scene to scene,
/// and stand where a scene can reach them, so no colonist ever has to.
/// Their roles are fixed at arrival, so the two the tour cannot do
/// without (the grenade's volunteer and the tractor's stray) are never
/// spent in an earlier scene.
/// </summary>
public static class TourGroupPlan
{
    /// <summary>The fewest tourists who arrive.</summary>
    public const int MinSize = 3;

    /// <summary>The most tourists who arrive.</summary>
    public const int MaxSize = 5;

    /// <summary>
    /// Where the group waits between scenes, from the base's centre:
    /// inside the base's square, north-west of the devices.
    /// </summary>
    public static readonly Cell Rest = new(-9, 9);

    /// <summary>How far the waiting group wanders from its spot, in tiles.</summary>
    public const float RestRadius = 3f;

    /// <summary>
    /// How far a tourist placed for a part (the pile, the stray's spot, a
    /// pad) wanders from it, in tiles.
    /// </summary>
    public const float SpotRadius = 1f;

    /// <summary>
    /// How far inside the stampede's keep-away line the sightseers stand,
    /// in tiles: behind the line, on the base's side.
    /// </summary>
    public const int LineInset = 5;

    /// <summary>
    /// How far from a pad's device the sightseers stand to look at it, in
    /// tiles, on the side towards the base.
    /// </summary>
    public const int PadViewTiles = 8;

    /// <summary>
    /// How far from where a scene wants a tourist the director walks it
    /// on screen, in tiles; a tourist farther off is cut to the spot, as
    /// a film would, since a walk across the map takes longer than a
    /// scene's intro and would cross another scene's danger.
    /// </summary>
    public const int WalkTiles = 12;

    /// <summary>
    /// The group's size from a drawn number, held to
    /// <see cref="MinSize"/>..<see cref="MaxSize"/>.
    /// </summary>
    /// <param name="drawn">The number drawn.</param>
    /// <returns>The size.</returns>
    public static int Size(int drawn) => Math.Max(MinSize, Math.Min(MaxSize, drawn));

    /// <summary>
    /// A tourist's role by arrival order: the first volunteers, the second
    /// strays, the rest see the sights.
    /// </summary>
    /// <param name="index">The tourist's index, from 0.</param>
    /// <returns>The role.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is negative.
    /// </exception>
    public static TouristRole RoleOf(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "negative");
        }

        return index switch
        {
            0 => TouristRole.Volunteer,
            1 => TouristRole.Stray,
            _ => TouristRole.Sightseer,
        };
    }

    /// <summary>
    /// Which pad a sightseer visits: they take turns, the first to pad 0
    /// (the replicator), the second to pad 1 (the reactor).
    /// </summary>
    /// <param name="sightseer">The sightseer's index among sightseers, from 0.</param>
    /// <returns>The pad's index.</returns>
    public static int PadFor(int sightseer) => Math.Abs(sightseer) % 2;

    /// <summary>
    /// Where the sightseers stand for the stampede: east of the base's
    /// square, <see cref="LineInset"/> tiles inside the keep-away line
    /// (<see cref="Stampede.KeepAwayTiles"/> from the square), level with
    /// the base.
    /// </summary>
    /// <param name="baseCentre">The base's centre.</param>
    /// <returns>The cell.</returns>
    public static Cell StampedeLine(Cell baseCentre) =>
        new(
            baseCentre.X + TourStagePlanner.BaseHalfSize
                + (int)Stampede.KeepAwayTiles - LineInset,
            baseCentre.Z);

    /// <summary>
    /// Where the sightseers stand at a pad: <see cref="PadViewTiles"/>
    /// from its device, towards the base (straight at it when the two
    /// share a cell).
    /// </summary>
    /// <param name="pad">The pad device's cell.</param>
    /// <param name="baseCentre">The base's centre.</param>
    /// <returns>The cell.</returns>
    public static Cell PadView(Cell pad, Cell baseCentre)
    {
        double dx = baseCentre.X - pad.X;
        double dz = baseCentre.Z - pad.Z;
        double length = Math.Sqrt((dx * dx) + (dz * dz));
        if (length < 1e-9)
        {
            return pad;
        }

        double scale = Math.Min(PadViewTiles, length) / length;
        return new Cell(
            pad.X + (int)Math.Round(dx * scale, MidpointRounding.AwayFromZero),
            pad.Z + (int)Math.Round(dz * scale, MidpointRounding.AwayFromZero));
    }
}

/// <summary>
/// What became of the tour group, for the verifier and the SUMMARY: how
/// many arrived, and how many of them ended alive and standing, downed,
/// dead (a corpse) or gone (destroyed without a corpse, as a tractor's
/// vortex can leave one).
/// </summary>
/// <param name="Arrived">The tourists who arrived.</param>
/// <param name="Standing">Alive and on their feet.</param>
/// <param name="Downed">Alive but downed.</param>
/// <param name="Dead">Dead, with a corpse.</param>
/// <param name="Gone">Destroyed without a corpse.</param>
public readonly record struct TourGroupCount(
    int Arrived, int Standing, int Downed, int Dead, int Gone)
{
    /// <summary>Gets the tourists lost: downed, dead or gone.</summary>
    public int Lost => Downed + Dead + Gone;

    /// <summary>
    /// Gets whether every tourist who arrived is counted once: standing,
    /// downed, dead or gone.
    /// </summary>
    public bool AccountedFor =>
        Arrived >= 0 && Standing >= 0 && Downed >= 0 && Dead >= 0 && Gone >= 0
        && Standing + Downed + Dead + Gone == Arrived;

    /// <summary>The log's line.</summary>
    /// <returns>The text, such as "4 arrived: 2 standing, 0 downed, 2 dead, 0 gone".</returns>
    public override string ToString() =>
        $"{Arrived} arrived: {Standing} standing, {Downed} downed, "
        + $"{Dead} dead, {Gone} gone";
}

/// <summary>
/// When a scene says its guestsLost line: once a scene, and never while
/// the scene's intro is being read. A tourist lost in the intro (one
/// hurt in the scene before, who dies as the next one begins) waits for
/// the intro's end, so the narrator introduces the scene before noting
/// the guests, and the line follows the scene's start lines. One per
/// scene run; the scene resets it as it begins.
/// </summary>
public sealed class GuestLossCue
{
    /// <summary>Whether a loss in the intro waits to be said.</summary>
    private bool waiting;

    /// <summary>Whether the line has been played this run.</summary>
    private bool played;

    /// <summary>Gets whether a loss waits for the intro's end.</summary>
    public bool Waiting => waiting;

    /// <summary>Forgets the last run: a scene begins.</summary>
    public void Reset()
    {
        waiting = false;
        played = false;
    }

    /// <summary>A tourist was lost.</summary>
    /// <param name="inIntro">Whether the scene is still in its intro.</param>
    /// <returns>True when the line plays now; false when it waits or has played.</returns>
    public bool Lost(bool inIntro)
    {
        if (played)
        {
            return false;
        }

        if (inIntro)
        {
            waiting = true;
            return false;
        }

        waiting = false;
        played = true;
        return true;
    }

    /// <summary>Asked every tick: whether a loss held through the intro plays now.</summary>
    /// <param name="inIntro">Whether the scene is still in its intro.</param>
    /// <returns>True once, on the first tick after the intro with a loss waiting.</returns>
    public bool Due(bool inIntro)
    {
        if (!waiting || inIntro || played)
        {
            return false;
        }

        waiting = false;
        played = true;
        return true;
    }
}
