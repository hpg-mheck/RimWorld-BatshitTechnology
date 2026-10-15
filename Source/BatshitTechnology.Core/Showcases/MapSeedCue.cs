namespace BatshitTechnology.Showcases;

/// <summary>
/// The MAPSEED cue's detail: whether the map seed's window pinned a
/// showcase's map, how many seeds, and what of the map a seed still cannot
/// pin (docs/SHOWCASE-API.md, "Seeds"). Without RimWorld, so unit tested;
/// the session logs it and the tour verifier expects it.
/// </summary>
/// <remarks>
/// RimWorld 1.6.4871 fills the rooms of every layout (Odyssey's ancient
/// garrisons, warehouses, refineries, infested settlements and launch
/// sites, and other ruins) under <c>System.HashCode.Combine</c>, which
/// mixes in a random seed per process (<c>LayoutWorker.FillAllRooms</c>).
/// The map seed's prefix pins only <c>Map.NextGenSeed</c>, so such a map's
/// rooms, and the gen steps after them, differ from process to process;
/// the cue says how many layouts the map holds, and the tile's landmark.
/// </remarks>
public static class MapSeedCue
{
    /// <summary>The detail when the prefix is not installed.</summary>
    public const string NoPatch = "not pinned (no patch)";

    /// <summary>The detail for a quick-test map, which vanilla generated.</summary>
    public const string QuickTestMap = "not pinned (quicktest map)";

    /// <summary>
    /// The detail when the window was not open while the map was
    /// generated, for a game that was not a quick test (its preparation
    /// failed, say).
    /// </summary>
    public const string WindowNotOpen = "not pinned (window not open)";

    /// <summary>
    /// The cue's detail: <c>pinned (n seeds)</c>, with
    /// <c>; rooms of n layouts not pinned</c> when the map holds layouts
    /// and <c>; landmark &lt;defName&gt;</c> when its tile has one; or
    /// <see cref="NoPatch"/>, <see cref="QuickTestMap"/> or
    /// <see cref="WindowNotOpen"/>.
    /// </summary>
    /// <param name="installed">Whether the prefix is installed.</param>
    /// <param name="pinned">
    /// The seeds pinned while the window was open, or -1 if it was not.
    /// </param>
    /// <param name="quickTest">
    /// Whether the showcase was adopted on a quick-test map.
    /// </param>
    /// <param name="unpinnedLayouts">
    /// Layouts on the map whose rooms vanilla filled; zero or less for
    /// none.
    /// </param>
    /// <param name="landmark">
    /// The tile's landmark (its def's name), or null.
    /// </param>
    /// <returns>The detail.</returns>
    public static string Detail(
        bool installed, int pinned, bool quickTest, int unpinnedLayouts = 0,
        string? landmark = null)
    {
        if (!installed)
        {
            return NoPatch;
        }

        if (pinned < 0)
        {
            return quickTest ? QuickTestMap : WindowNotOpen;
        }

        string detail = pinned == 1 ? "pinned (1 seed)" : $"pinned ({pinned} seeds)";
        if (unpinnedLayouts > 0)
        {
            detail += unpinnedLayouts == 1
                ? "; rooms of 1 layout not pinned"
                : $"; rooms of {unpinnedLayouts} layouts not pinned";
        }

        if (!string.IsNullOrWhiteSpace(landmark))
        {
            detail += $"; landmark {landmark!.Trim()}";
        }

        return detail;
    }
}
