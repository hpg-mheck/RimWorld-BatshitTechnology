// Showcase API - a showcase's map, generated from its seed alone.
// RimWorld 1.6.4871 seeds a map's structures from Map.NextGenSeed, which is
// System.HashCode.Combine(tile, generatedId++, world seed); System.HashCode
// mixes in a random seed chosen once per process, so no seed could pin a
// showcase's map (ancient shrines, ruins, scatter groups, prefabs, layouts,
// unique weapons, and every gen step after them). This is the mod's second
// Harmony patch, approved by Matt "Breakpoint" Heck on 2026-10-14
// (docs/AI-requests-for-humans.txt, item 22; docs/SHOWCASE-API.md,
// Decision 1): a prefix on the NextGenSeed getter that combines the same
// three inputs, in the same order, with Gen.HashCombineInt, which is the
// same in every process. It acts only inside a window that the launcher
// opens for the game it is generating (ShowcaseLauncher.Generate) and that
// closes as that game starts (GameComponent_ShowcaseSession.StartedNewGame)
// or on any way out (a failed generation, the main menu, a loaded game, a
// stopped or finished showcase). The window is bound to that Game object
// (Core's GenerationPin), so one left open by some failure can never pin
// another game's seeds. Outside it the prefix returns true at once and
// vanilla's getter runs untouched.
//
// The patch itself is installed by ShowcaseMenuButton, through the mod's
// one Harmony instance and id, so that HarmonyLib is named in one file
// only. Nothing here uses a Harmony type: the prefix's parameters follow
// Harmony's naming conventions (__instance, __result, and ___generatedId
// for vanilla's private counter) and no more.

using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Pins <c>Map.NextGenSeed</c> to the map's tile, its counter and the
    /// world's seed while a showcase's game generates its map.
    /// </summary>
    public static class ShowcaseMapSeed
    {
        /// <summary>The window, bound to the game being generated.</summary>
        private static readonly GenerationPin Pin = new GenerationPin();

        /// <summary>
        /// Gets a value indicating whether the prefix is installed. Set by
        /// <see cref="ShowcaseMenuButton"/> once Harmony has patched the
        /// getter; false if Harmony is missing or the getter or its
        /// counter has changed (a showcase's map then follows vanilla, and
        /// its <c>MAPSEED</c> cue says so).
        /// </summary>
        public static bool Installed { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the window is open, for any
        /// game. True only while a showcase's game is being generated.
        /// </summary>
        public static bool Armed => Pin.Armed;

        /// <summary>
        /// Gets how many seeds the prefix has pinned since the game
        /// started, in every window.
        /// </summary>
        public static long PinnedTotal => Pin.PinnedTotal;

        /// <summary>
        /// Gets how many calls the prefix has passed through to vanilla
        /// since the game started.
        /// </summary>
        public static long PassedTotal => Pin.PassedTotal;

        /// <summary>
        /// Whether the prefix would pin a seed of this game's maps now:
        /// only while the window is open for that very game.
        /// </summary>
        /// <param name="game">The game, or null.</param>
        /// <returns>True if its seeds would be pinned.</returns>
        public static bool PinsFor(Game game) => Pin.Pins(game);

        /// <summary>
        /// Vanilla's three inputs combined as the prefix combines them:
        /// <c>Gen.HashCombineInt(Gen.HashCombineInt(place, counter),
        /// worldSeed)</c>, the same in every process.
        /// </summary>
        /// <param name="place">The map's place (<see cref="Place"/>).</param>
        /// <param name="counter">The map's generation counter.</param>
        /// <param name="worldSeed">The world's seed (<c>WorldInfo.Seed</c>).</param>
        /// <returns>The seed.</returns>
        public static int Combine(int place, int counter, int worldSeed) =>
            Gen.HashCombineInt(Gen.HashCombineInt(place, counter), worldSeed);

        /// <summary>
        /// The map's place, as vanilla's getter reads it: its tile's hash
        /// (<c>PlanetTile.GetHashCode</c>, made of the tile's and layer's
        /// ids) if the tile is valid, else the map's unique id.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The place.</returns>
        public static int Place(Map map) =>
            map.TileInfo.tile.Valid ? map.TileInfo.tile.GetHashCode() : map.uniqueID;

        /// <summary>
        /// Harmony prefix on the <c>Map.NextGenSeed</c> getter. Outside a
        /// showcase's generation it returns true at once, and vanilla's
        /// getter runs as if unpatched. Inside, it reads vanilla's inputs
        /// in vanilla's order (the place, the counter, which it advances as
        /// vanilla does, then the world's seed), so anything that would
        /// throw in vanilla throws here the same way, and sets the result
        /// to <see cref="Combine"/> of them, skipping vanilla's getter.
        /// </summary>
        /// <param name="__instance">The map.</param>
        /// <param name="__result">The seed, when pinned.</param>
        /// <param name="___generatedId">Vanilla's private counter.</param>
        /// <returns>True to run vanilla's getter; false when pinned.</returns>
        public static bool Prefix(Map __instance, ref int __result, ref int ___generatedId)
        {
            if (__instance == null || !Pin.Take(Current.Game))
            {
                return true;
            }

            __result = Combine(Place(__instance), ___generatedId++, Find.World.info.Seed);
            return false;
        }

        /// <summary>
        /// Opens the window for the game being generated. Called by the
        /// launcher on the long event's thread, after the game exists and
        /// before its map is generated.
        /// </summary>
        /// <param name="game">The showcase's game.</param>
        internal static void Arm(Game game) => Pin.Arm(game);

        /// <summary>
        /// Closes the window. Safe on every way out, any number of times.
        /// </summary>
        /// <returns>
        /// How many seeds were pinned while it was open, or -1 if it was
        /// already closed.
        /// </returns>
        internal static int Disarm() => Pin.Disarm();
    }
}
