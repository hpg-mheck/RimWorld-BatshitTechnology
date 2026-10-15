using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies <see cref="MapSeedCue"/>, the MAPSEED cue's detail: why a map
/// was not pinned, and what of a pinned map a seed still cannot pin.
/// </summary>
public sealed class MapSeedCueTests
{
    /// <summary>
    /// Without the prefix nothing is pinned, whatever the window did;
    /// with it, a closed window reads as a quick-test map only when the
    /// showcase was adopted on one, and otherwise says the window was not
    /// open (a failed preparation), never "quicktest map".
    /// </summary>
    [Fact(DisplayName = "MAPSEED says why a map was not pinned")]
    public void Detail_should_say_why_a_map_was_not_pinned()
    {
        Assert.Equal("not pinned (no patch)", MapSeedCue.Detail(false, 12, false));
        Assert.Equal("not pinned (no patch)", MapSeedCue.Detail(false, -1, true));
        Assert.Equal("not pinned (quicktest map)", MapSeedCue.Detail(true, -1, true));
        Assert.Equal("not pinned (window not open)", MapSeedCue.Detail(true, -1, false));
        Assert.Equal(
            "not pinned (window not open)",
            MapSeedCue.Detail(true, -1, false, 2, "AncientGarrison"));
    }

    /// <summary>
    /// A pinned map says how many seeds, in the singular for one, and adds
    /// the layouts whose rooms are not pinned and the tile's landmark only
    /// when there are any; zero seeds still reads as pinned, so the
    /// verifier's "at least one" fact is what fails.
    /// </summary>
    [Fact(DisplayName = "MAPSEED counts the seeds, then the layouts and landmark")]
    public void Detail_should_count_seeds_then_add_layouts_and_landmark()
    {
        Assert.Equal("pinned (653 seeds)", MapSeedCue.Detail(true, 653, false));
        Assert.Equal("pinned (1 seed)", MapSeedCue.Detail(true, 1, false));
        Assert.Equal("pinned (0 seeds)", MapSeedCue.Detail(true, 0, false));
        Assert.Equal("pinned (653 seeds)", MapSeedCue.Detail(true, 653, false, 0, null));
        Assert.Equal("pinned (653 seeds)", MapSeedCue.Detail(true, 653, false, -1, " "));
        Assert.Equal(
            "pinned (653 seeds); rooms of 1 layout not pinned",
            MapSeedCue.Detail(true, 653, false, 1));
        Assert.Equal(
            "pinned (40 seeds); rooms of 2 layouts not pinned; landmark AncientGarrison",
            MapSeedCue.Detail(true, 40, false, 2, "AncientGarrison"));
        Assert.Equal(
            "pinned (40 seeds); landmark Oasis",
            MapSeedCue.Detail(true, 40, false, 0, "Oasis"));
    }
}
