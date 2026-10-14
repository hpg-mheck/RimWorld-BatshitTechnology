namespace BatshitTechnology.Core;

/// <summary>
/// The places and people of the tour's Raccoon Incident (docs/SHOWCASE.md,
/// "Reel 9: The Raccoon Incident"), in AnyRim's town-local cells: where the
/// raccoon comes in, where the guard is found and where he takes his post,
/// and the lawn chair the neighbour watches from. The scene and the tests
/// read them here, so a change to the town that moves one shows up as a
/// failing test rather than a raccoon in a wall.
/// </summary>
public static class RaccoonIncident
{
    /// <summary>The guard's resident key: Ned "Overkill" Lessly.</summary>
    public const string GuardKey = "Ned";

    /// <summary>
    /// The neighbour the raccoon goes for: Constance "Collateral" Damage,
    /// in her lawn chair.
    /// </summary>
    public const string NeighbourKey = "Constance";

    /// <summary>The guard's home: AnyRim Savings &amp; Loan.</summary>
    public const string BankKey = "E4";

    /// <summary>The weapon the guard carries.</summary>
    public const string GrenadeDef = AnyRimBuilder.GrenadeDef;

    /// <summary>
    /// The grenade's throwing range in tiles, as its verb in
    /// Defs/ThingDefs/Items_SingularityGrenade.xml gives it (a test holds
    /// the two together).
    /// </summary>
    public const double ThrowRange = 12.9;

    /// <summary>
    /// How far the singularity reaches, in tiles (the grenade's def), and
    /// so how near the landing the guard may stand and be pulled in too.
    /// </summary>
    public const double PullRadius = 10.0;

    /// <summary>
    /// Main Street's south end, where the raccoon comes into town: the
    /// road's centre on its southernmost row.
    /// </summary>
    public static readonly Cell Entry = new(0, AnyRimBuilder.MainSouth);

    /// <summary>
    /// Where the camera finds the guard: the bank's front walk, between
    /// the ATM and the columns, facing the street.
    /// </summary>
    public static readonly Cell BankWalk = new(5, 39);

    /// <summary>
    /// The guard's post when the raccoon comes: the middle of Main
    /// Street between the Damage house and the bank, in the west lane
    /// beside the car parked at the bank's kerb. A raccoon coming up the
    /// street enters his throwing range while it is still on the road,
    /// before it reaches the Damages' lawn.
    /// </summary>
    public static readonly Cell GuardPost = new(-1, 33);

    /// <summary>
    /// Constance's lawn chair: the middle of the Damages' three armchairs
    /// facing the street.
    /// </summary>
    public static readonly Cell LawnChair = new(5, 28);

    /// <summary>
    /// The row at which a raccoon coming north along Main Street's centre
    /// line first comes within <see cref="ThrowRange"/> of
    /// <see cref="GuardPost"/>.
    /// </summary>
    /// <returns>The row (z), rounded up to a whole cell.</returns>
    public static int FirstRowInRange()
    {
        double dx = GuardPost.X - Entry.X;
        double reach = System.Math.Sqrt((ThrowRange * ThrowRange) - (dx * dx));
        return (int)System.Math.Ceiling(GuardPost.Z - reach);
    }
}
