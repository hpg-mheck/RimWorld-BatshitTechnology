using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="StageClearance"/>, the record of what the tour's
/// stage cleared from its map (docs/SHOWCASE.md, "A Clean Stage").
/// </summary>
public sealed class StageClearanceTests
{
    /// <summary>
    /// Nothing cleared reads as such and totals zero.
    /// </summary>
    [Fact(DisplayName = "An empty clearance says there was nothing to clear")]
    public void ToString_should_say_nothing_to_clear_when_empty()
    {
        var none = default(StageClearance);

        Assert.Equal(0, none.Total);
        Assert.Equal("nothing to clear", none.ToString());
    }

    /// <summary>
    /// Each kind is named with its count, singular for one, in a fixed
    /// order, and the total adds them all.
    /// </summary>
    [Fact(DisplayName = "A clearance names each kind with its count")]
    public void ToString_should_name_each_kind_with_its_count()
    {
        var some = new StageClearance(
            Hives: 2, Nests: 3, HostilePawns: 1, CasketOccupants: 0,
            AnimaTrees: 1, ZoneTrees: 37);

        Assert.Equal(44, some.Total);
        Assert.Equal(
            "2 hives, 3 egg sacs and cocoons, 1 hostile pawn, "
                + "0 casket occupants, 1 anima tree, 37 trees in the volley zone",
            some.ToString());
        Assert.StartsWith(
            "0 hives, 1 egg sac or cocoon, ",
            (default(StageClearance) with { Nests = 1 }).ToString());
    }
}
