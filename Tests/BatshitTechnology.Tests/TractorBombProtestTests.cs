using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies Core's <see cref="TractorBombProtest"/> (docs/TRACTOR-BOMB.md,
/// "Protest"): the robot objects to a living colonist, as before, and
/// since slice 19000c to a friendly guest, but never to a hostile, a
/// prisoner, an animal or the dead.
/// </summary>
public sealed class TractorBombProtestTests
{
    /// <summary>A living colonist, free or enslaved, is objected to.</summary>
    [Fact(DisplayName = "The robot objects to a colonist in its pull")]
    public void ObjectsTo_should_include_colonists()
    {
        Assert.True(TractorBombProtest.ObjectsTo(
            colonist: true, humanlike: true, ofColony: true, hostile: false,
            prisoner: false, dead: false));
        Assert.False(TractorBombProtest.ObjectsTo(
            colonist: true, humanlike: true, ofColony: true, hostile: false,
            prisoner: false, dead: true));
    }

    /// <summary>
    /// A humanlike of another faction, not hostile and not a prisoner (a
    /// visitor, a trader, a tourist), is objected to.
    /// </summary>
    [Fact(DisplayName = "The robot objects to a friendly guest in its pull")]
    public void ObjectsTo_should_include_friendly_guests()
    {
        Assert.True(TractorBombProtest.ObjectsTo(
            colonist: false, humanlike: true, ofColony: false, hostile: false,
            prisoner: false, dead: false));
        Assert.Equal("a colonist or guest", TractorBombProtest.Whom);
    }

    /// <summary>
    /// Hostiles, prisoners, animals, the colony's own non-colonists
    /// (its animals and mechs) and the dead are not.
    /// </summary>
    [Fact(DisplayName = "The robot ignores hostiles, prisoners, animals and the dead")]
    public void ObjectsTo_should_leave_out_the_rest()
    {
        Assert.False(TractorBombProtest.ObjectsTo(
            colonist: false, humanlike: true, ofColony: false, hostile: true,
            prisoner: false, dead: false));
        Assert.False(TractorBombProtest.ObjectsTo(
            colonist: false, humanlike: true, ofColony: false, hostile: false,
            prisoner: true, dead: false));
        Assert.False(TractorBombProtest.ObjectsTo(
            colonist: false, humanlike: false, ofColony: false, hostile: false,
            prisoner: false, dead: false));
        Assert.False(TractorBombProtest.ObjectsTo(
            colonist: false, humanlike: false, ofColony: true, hostile: false,
            prisoner: false, dead: false));
        Assert.False(TractorBombProtest.ObjectsTo(
            colonist: false, humanlike: true, ofColony: false, hostile: false,
            prisoner: false, dead: true));
    }
}
