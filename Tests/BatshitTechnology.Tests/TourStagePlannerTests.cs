using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="TourStagePlanner"/>: the yard and the pads are
/// chosen together, and every keep-away holds.
/// </summary>
public sealed class TourStagePlannerTests
{
    /// <summary>The quick-test map: 250 cells a side.</summary>
    private static readonly Box Map250 = new(0, 0, 249, 249);

    /// <summary>The demo base's centre on that map.</summary>
    private static readonly Cell Base = new(125, 125);

    /// <summary>
    /// The keep-away rule compares the gap with the distance: a gap equal
    /// to it clears, one tile less does not.
    /// </summary>
    [Fact(DisplayName = "Clears holds at exactly the distance and fails inside it")]
    public void Clears_should_compare_the_gap_with_the_distance()
    {
        var keep = new KeepAway(new Box(0, 0, 0, 0), 10);
        Assert.True(TourStagePlanner.Clears(new Box(10, 0, 12, 0), keep));
        Assert.False(TourStagePlanner.Clears(new Box(9, 0, 12, 0), keep));
        Assert.False(TourStagePlanner.Clears(new Box(-3, -3, 3, 3), keep));
    }

    /// <summary>
    /// With nothing in the way, the first candidate yard wins, and two
    /// pads remain.
    /// </summary>
    [Fact(DisplayName = "The first clear candidate yard wins")]
    public void Plan_should_take_the_first_clear_yard()
    {
        TourStagePlan plan = TourStagePlanner.Plan(new TourStageInput(Map250, Base));
        Assert.Equal(TourStagePlanner.YardAt(Base + new Cell(-45, -62)), plan.Yard);
        Assert.Equal(Base + new Cell(-45, -62), plan.YardCentre);
        Assert.Equal(2, plan.Pads.Count);
        Assert.Null(plan.Note);
    }

    /// <summary>
    /// A keep-away over the first candidate moves the yard to the second.
    /// </summary>
    [Fact(DisplayName = "A keep-away over the first yard moves it to the next")]
    public void Plan_should_skip_a_covered_yard()
    {
        Cell first = Base + new Cell(-45, -62);
        TourStagePlan plan = TourStagePlanner.Plan(new TourStageInput(Map250, Base)
        {
            ExtraYardKeepAways = new[] { new KeepAway(new Box(first.X, first.Z, first.X, first.Z), 1) },
        });
        Assert.Equal(TourStagePlanner.YardAt(Base + new Cell(0, -62)), plan.Yard);
    }

    /// <summary>
    /// A yard within the tractor's reach of the base square is rejected
    /// for one beyond it. The reach is how far anyone runs from one of
    /// the tour's Popular tractors (22.5 + 8 = 30.5), rounded up.
    /// </summary>
    [Fact(DisplayName = "A yard within TractorReach of the base is rejected")]
    public void Plan_should_reject_a_yard_near_the_base()
    {
        // (0,-55): the yard's north edge is 25 tiles from the base square,
        // under the 31 required (it passed the old 18); (0,-62) is 32 away.
        TourStagePlan plan = TourStagePlanner.Plan(new TourStageInput(Map250, Base)
        {
            YardOffsets = new[] { new Cell(0, -55), new Cell(0, -62) },
        });
        Assert.Equal(31, TourStagePlanner.TractorReach);
        Assert.Equal(TourStagePlanner.YardAt(Base + new Cell(0, -62)), plan.Yard);
    }

    /// <summary>
    /// A pad within the demo-corner reach of the yard is dropped, and the
    /// plan says why it has only one.
    /// </summary>
    [Fact(DisplayName = "A pad within 87.6 of the yard is dropped, and the plan says why")]
    public void Plan_should_drop_a_pad_near_the_yard()
    {
        TourStagePlan plan = TourStagePlanner.Plan(new TourStageInput(Map250, Base)
        {
            YardOffsets = new[] { new Cell(0, -62) },
            PadOffsets = new[] { new Cell(75, -75), new Cell(75, 75) },
        });
        Assert.Equal(new[] { Base + new Cell(75, 75) }, plan.Pads);
        Assert.NotNull(plan.Note);
        Assert.Contains("only one pad", plan.Note);
        Assert.Contains("the yard", plan.Note);
    }

    /// <summary>
    /// A later yard that leaves two pads beats an earlier one that leaves
    /// one.
    /// </summary>
    [Fact(DisplayName = "The first yard leaving two pads beats one leaving one")]
    public void Plan_should_prefer_a_yard_that_leaves_two_pads()
    {
        TourStagePlan plan = TourStagePlanner.Plan(new TourStageInput(Map250, Base)
        {
            YardOffsets = new[] { new Cell(0, -62), new Cell(-45, -62) },
            PadOffsets = new[] { new Cell(75, -75), new Cell(-75, -75), new Cell(75, 75) },
        });
        Assert.Equal(TourStagePlanner.YardAt(Base + new Cell(-45, -62)), plan.Yard);
        Assert.Equal(
            new[] { Base + new Cell(75, -75), Base + new Cell(75, 75) }, plan.Pads);
        Assert.Null(plan.Note);
    }

    /// <summary>
    /// With no clear yard the plan has none, says so, and still places
    /// pads clear of AnyRim.
    /// </summary>
    [Fact(DisplayName = "No clear yard: no yard, a note, pads anyway")]
    public void Plan_without_a_yard_should_say_so()
    {
        TourStagePlan plan = TourStagePlanner.Plan(new TourStageInput(Map250, Base)
        {
            YardOffsets = new[] { new Cell(0, 0) },
        });
        Assert.Null(plan.Yard);
        Assert.Null(plan.YardCentre);
        Assert.StartsWith("no yard", plan.Note);
        Assert.Equal(2, plan.Pads.Count);
    }

    /// <summary>
    /// AnyRim near a pad drops that pad and only that pad: the town is
    /// measured part by part, as its site search measured it. A control
    /// plan without the town keeps both pads, so AnyRim is the reason.
    /// </summary>
    [Fact(DisplayName = "A pad within reach of AnyRim is dropped")]
    public void Plan_should_drop_a_pad_near_AnyRim()
    {
        TownPlan town = TownPlan.AnyRim(1, TownPalette.For(TownQuality.Medium));
        Cell north = Base + new Cell(-75, 75);
        Cell east = Base + new Cell(75, 75);
        TourStagePlan control = TourStagePlanner.Plan(Input(null));
        TourStagePlan plan = TourStagePlanner.Plan(Input(town));
        Assert.Equal(new[] { north, east }, control.Pads);
        Assert.Equal(new[] { east }, plan.Pads);
        Assert.NotNull(plan.Yard);

        // The input both plans share, with or without the town.
        TourStageInput Input(TownPlan? withTown) =>
            new TourStageInput(Map250, Base)
            {
                Town = withTown,
                TownAnchor = north + new Cell(0, -30),
                YardOffsets = new[] { new Cell(45, -62) },
                PadOffsets = new[] { new Cell(-75, 75), new Cell(75, 75) },
            };
    }

    /// <summary>
    /// Pads and yards that do not fit the map are never chosen.
    /// </summary>
    [Fact(DisplayName = "Nothing is placed off the map")]
    public void Plan_should_keep_everything_on_the_map()
    {
        var small = new Box(0, 0, 119, 119);
        TourStagePlan plan = TourStagePlanner.Plan(
            new TourStageInput(small, new Cell(60, 60)));
        Assert.Null(plan.Yard);
        Assert.Empty(plan.Pads);
        Assert.Contains("no pad", plan.Note);
        Assert.Throws<ArgumentNullException>(() => TourStagePlanner.Plan(null!));
    }
}
