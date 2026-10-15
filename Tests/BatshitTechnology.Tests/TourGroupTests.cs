using System;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies Core's <see cref="TourGroupPlan"/> and
/// <see cref="TourGroupCount"/> (docs/SHOWCASE.md, "Tourists"; slice
/// 19000c): the group's size, its roles by arrival order, where the
/// sightseers stand for the stampede and the pads, how its fates are
/// counted, and when a scene notes a lost guest (<see cref="GuestLossCue"/>).
/// </summary>
public sealed class TourGroupTests
{
    /// <summary>The group is three to five, whatever is drawn.</summary>
    [Fact(DisplayName = "A tour group is three to five tourists")]
    public void Size_should_be_three_to_five()
    {
        Assert.Equal(3, TourGroupPlan.MinSize);
        Assert.Equal(5, TourGroupPlan.MaxSize);
        Assert.Equal(3, TourGroupPlan.Size(0));
        Assert.Equal(3, TourGroupPlan.Size(-7));
        Assert.Equal(4, TourGroupPlan.Size(4));
        Assert.Equal(5, TourGroupPlan.Size(5));
        Assert.Equal(5, TourGroupPlan.Size(99));
    }

    /// <summary>
    /// The first tourist volunteers, the second strays, the rest see the
    /// sights, so the two parts the tour cannot do without are reserved
    /// from arrival; even the smallest group has one sightseer.
    /// </summary>
    [Fact(DisplayName = "Roles follow arrival: volunteer, stray, sightseers")]
    public void RoleOf_should_reserve_the_volunteer_and_the_stray()
    {
        Assert.Equal(TouristRole.Volunteer, TourGroupPlan.RoleOf(0));
        Assert.Equal(TouristRole.Stray, TourGroupPlan.RoleOf(1));
        Assert.Equal(TouristRole.Sightseer, TourGroupPlan.RoleOf(2));
        Assert.Equal(TouristRole.Sightseer, TourGroupPlan.RoleOf(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => TourGroupPlan.RoleOf(-1));
        Assert.True(TourGroupPlan.MinSize >= 3, "the smallest group needs a sightseer");
    }

    /// <summary>
    /// Sightseers take turns between the pads, the first to the
    /// replicator's (pad 0), so with two or more both pads have visitors.
    /// </summary>
    [Fact(DisplayName = "Sightseers take turns between the two pads")]
    public void PadFor_should_alternate()
    {
        Assert.Equal(0, TourGroupPlan.PadFor(0));
        Assert.Equal(1, TourGroupPlan.PadFor(1));
        Assert.Equal(0, TourGroupPlan.PadFor(2));
        Assert.Equal(1, TourGroupPlan.PadFor(-1));
    }

    /// <summary>
    /// The stampede's sightseers stand east of the base, level with it,
    /// behind the keep-away line (inside it, on the base's side) and
    /// outside the base's square, where the colonists wait.
    /// </summary>
    [Fact(DisplayName = "The stampede's sightseers stand behind the keep-away line")]
    public void StampedeLine_should_be_inside_the_keep_away_and_outside_the_square()
    {
        var baseCentre = new Cell(125, 125);
        Cell line = TourGroupPlan.StampedeLine(baseCentre);
        Assert.Equal(baseCentre.Z, line.Z);
        Assert.True(line.X > baseCentre.X, "east of the base");
        Assert.True(Stampede.InsideKeepAway(baseCentre, line), "behind the line");
        double fromSquare = Stampede.BaseSquare(baseCentre).DistanceTo(line);
        Assert.True(fromSquare > TourGroupPlan.RestRadius + 1,
            $"outside the base's square ({fromSquare:0.0} tiles from it)");
        Assert.Equal(Stampede.KeepAwayTiles - TourGroupPlan.LineInset, fromSquare, 3);
    }

    /// <summary>
    /// The pad's sightseers stand eight tiles from the device on the side
    /// towards the base: inside its blast and its flee ring, which is the
    /// point.
    /// </summary>
    [Fact(DisplayName = "Pad sightseers stand 8 tiles from the device, towards the base")]
    public void PadView_should_be_eight_tiles_towards_the_base()
    {
        var baseCentre = new Cell(125, 125);
        var pad = new Cell(200, 200);
        Cell view = TourGroupPlan.PadView(pad, baseCentre);
        double dx = view.X - pad.X;
        double dz = view.Z - pad.Z;
        Assert.InRange(Math.Sqrt((dx * dx) + (dz * dz)), 7.5, 8.5);
        Assert.True(view.X < pad.X && view.Z < pad.Z, "towards the base");
        Assert.Equal(new Cell(130, 125), TourGroupPlan.PadView(new Cell(138, 125), baseCentre));
        Assert.Equal(new Cell(127, 125), TourGroupPlan.PadView(new Cell(127, 125), new Cell(127, 125)));
        Assert.Equal(new Cell(125, 125), TourGroupPlan.PadView(new Cell(128, 125), baseCentre));
    }

    /// <summary>
    /// A count adds up when every tourist is standing, downed, dead or
    /// gone exactly once; lost is all but the standing.
    /// </summary>
    [Fact(DisplayName = "A tour group's count accounts for every tourist once")]
    public void Count_should_account_for_everyone()
    {
        var count = new TourGroupCount(4, 1, 1, 1, 1);
        Assert.True(count.AccountedFor);
        Assert.Equal(3, count.Lost);
        Assert.Equal("4 arrived: 1 standing, 1 downed, 1 dead, 1 gone", count.ToString());
        Assert.False(new TourGroupCount(4, 1, 1, 1, 0).AccountedFor);
        Assert.False(new TourGroupCount(4, 3, 2, 0, -1).AccountedFor);
        Assert.True(new TourGroupCount(0, 0, 0, 0, 0).AccountedFor);
    }

    /// <summary>
    /// A loss after the intro plays the guestsLost line at once, and only
    /// once a scene; later losses say nothing more.
    /// </summary>
    [Fact(DisplayName = "A guest lost after the intro is noted at once, once")]
    public void Guest_loss_after_the_intro_should_play_once()
    {
        var cue = new GuestLossCue();
        Assert.False(cue.Due(inIntro: false));
        Assert.True(cue.Lost(inIntro: false));
        Assert.False(cue.Lost(inIntro: false));
        Assert.False(cue.Due(inIntro: false));
        Assert.False(cue.Waiting);
    }

    /// <summary>
    /// A loss in the intro (tour-17, slice 19000's review fixes: a
    /// sightseer hurt at the replicator's pad died as the reactor's scene
    /// began) waits through the intro and plays on its first tick after,
    /// once, so the line follows the scene's start lines.
    /// </summary>
    [Fact(DisplayName = "A guest lost in the intro is noted when the intro ends")]
    public void Guest_loss_in_the_intro_should_wait()
    {
        var cue = new GuestLossCue();
        Assert.False(cue.Lost(inIntro: true));
        Assert.True(cue.Waiting);
        Assert.False(cue.Lost(inIntro: true));
        Assert.False(cue.Due(inIntro: true));
        Assert.True(cue.Due(inIntro: false));
        Assert.False(cue.Due(inIntro: false));
        Assert.False(cue.Lost(inIntro: false));
        Assert.False(cue.Waiting);
    }

    /// <summary>
    /// A new run forgets the last: a loss waiting and a line played both
    /// go, so each scene notes its own losses.
    /// </summary>
    [Fact(DisplayName = "Each scene run notes its own guest losses")]
    public void Guest_loss_cue_should_reset_per_scene()
    {
        var cue = new GuestLossCue();
        Assert.True(cue.Lost(inIntro: false));
        cue.Reset();
        Assert.False(cue.Lost(inIntro: true));
        cue.Reset();
        Assert.False(cue.Waiting);
        Assert.False(cue.Due(inIntro: false));
        Assert.True(cue.Lost(inIntro: false));
    }
}
