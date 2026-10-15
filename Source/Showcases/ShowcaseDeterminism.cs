// Showcase API - seeds and fingerprints in the game.
// What makes a showcase repeatable (docs/SHOWCASE-API.md, "Seeds"): the
// global random state reseeded from the showcase's seed before its worker
// sets up (ShowcaseRandom), the loading screen's one random act done
// before a seeded generation rather than during it (LoadingTips), and the
// fingerprint of a map that proves it (ShowcaseFingerprint, logged as the
// MAPHASH and STAGEHASH cues). The rules and the hashing are Core's
// (ShowcaseSeed, MapFingerprint); this file only reads and sets the game.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.Steam;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Sets RimWorld's global random state, which every game system draws
    /// on, to a sequence drawn from a showcase's seed.
    /// </summary>
    public static class ShowcaseRandom
    {
        /// <summary>
        /// <c>Rand.StateCompressed</c>, the private seed and iteration
        /// count of the global state (RimWorld 1.6.4871). The public
        /// <c>Rand.Seed</c> setter logs an error when nothing is pushed
        /// ("The initial rand seed should always be based on the startup
        /// time and set only once"), a guard against reseeding by
        /// accident; a showcase reseeds on purpose, so it sets the state
        /// directly, exactly as that setter would.
        /// </summary>
        private static readonly PropertyInfo State = typeof(Rand).GetProperty(
            "StateCompressed", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>Whether the missing state has been reported.</summary>
        private static bool warned;

        /// <summary>
        /// Gets a value indicating whether the global state can be read and
        /// set: false if RimWorld ever renames or retypes it.
        /// </summary>
        public static bool CanReseed =>
            State != null && State.PropertyType == typeof(ulong)
            && State.GetGetMethod(true) != null && State.GetSetMethod(true) != null;

        /// <summary>
        /// Starts the global random state afresh from a seed: the same
        /// seed, then the same draws. Call it only when nothing is pushed
        /// (<c>Rand.PushState</c>), as on the main thread between frames.
        /// </summary>
        /// <param name="seed">The seed (see <see cref="ShowcaseSeed.Derive"/>).</param>
        /// <returns>
        /// True if reseeded; false if the state cannot be set, which is
        /// warned about once (the showcase then runs unpinned).
        /// </returns>
        public static bool Reseed(int seed)
        {
            if (!CanReseed)
            {
                if (!warned)
                {
                    warned = true;
                    Log.Warning($"{ShowcaseLog.Prefix} cannot find Rand.StateCompressed; "
                        + "showcases run without their seed's random state, so they "
                        + "will not repeat.");
                }

                return false;
            }

            // As Rand.Seed's setter: the seed, and no draws taken yet.
            State.SetValue(null, (ulong)(uint)seed);
            return true;
        }

        /// <summary>
        /// The global state now, for a smoke check to put back afterwards,
        /// or null if it cannot be read.
        /// </summary>
        /// <returns>The state.</returns>
        internal static ulong? Snapshot() =>
            CanReseed ? (ulong)State.GetValue(null) : (ulong?)null;

        /// <summary>Puts back a state taken by <see cref="Snapshot"/>.</summary>
        /// <param name="state">The state.</param>
        internal static void Restore(ulong state)
        {
            if (CanReseed)
            {
                State.SetValue(null, state);
            }
        }
    }

    /// <summary>
    /// The loading screen's tips, shuffled on the main thread before a
    /// seeded generation starts.
    /// </summary>
    /// <remarks>
    /// Rand is one global state shared by every thread. A showcase's game
    /// is generated on the long event's worker thread under a state pushed
    /// from its seed; meanwhile the main thread draws the loading screen,
    /// and the first time it draws the tips it shuffles them with Rand
    /// (GameplayTipWindow.DrawWindow, RimWorld 1.6.4871). Done during the
    /// generation, that shuffle would take draws from the seeded sequence
    /// at a moment that depends on thread timing, and the same seed could
    /// give a different world. Nothing else on the loading screen draws on
    /// Rand.
    /// </remarks>
    internal static class LoadingTips
    {
        /// <summary>The tips' private cache (RimWorld 1.6.4871).</summary>
        private static readonly FieldInfo Cache = typeof(GameplayTipWindow).GetField(
            "allTipsCached", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Gets a value indicating whether the tips have been shuffled;
        /// null if the cache cannot be read.
        /// </summary>
        internal static bool? Shuffled =>
            Cache != null && Cache.FieldType == typeof(List<string>)
                ? Cache.GetValue(null) != null
                : (bool?)null;

        /// <summary>
        /// Shuffles the tips now, as vanilla would on first drawing them,
        /// if they have not been yet. Call it on the main thread. Never
        /// throws: if the cache has changed, a warning says the showcase
        /// may not repeat.
        /// </summary>
        internal static void Prime()
        {
            try
            {
                if (Shuffled == null)
                {
                    Log.Warning($"{ShowcaseLog.Prefix} cannot find the loading tips' "
                        + "cache; a seeded showcase may not repeat.");
                    return;
                }

                if (Shuffled == true)
                {
                    return;
                }

                // Vanilla's own expression (GameplayTipWindow.DrawWindow).
                List<string> tips = DefDatabase<TipSetDef>.AllDefsListForReading
                    .SelectMany(set => SteamDeck.IsSteamDeck
                        && set == TipSetDefOf.GameplayTips
                            ? set.tips.Skip(11)
                            : set.tips)
                    .InRandomOrder()
                    .ToList();
                Cache.SetValue(null, tips);
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not shuffle the loading "
                    + $"tips ahead of the showcase: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Fingerprints a map for the MAPHASH and STAGEHASH cues: its size,
    /// every cell's terrain and roof, every building, item, plant and
    /// filth (def, cell, rotation, stuff, stack count), and every pawn on
    /// it, spawned or held (name, kind, faction). Motes, projectiles,
    /// fire and other passing effects are left out, and so are pawns'
    /// cells, since pawns walk. Reading the map draws nothing from Rand.
    /// </summary>
    public static class ShowcaseFingerprint
    {
        /// <summary>Fingerprints a map.</summary>
        /// <param name="map">The map.</param>
        /// <returns>The fingerprint.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="map"/> is null.
        /// </exception>
        public static MapFingerprint Of(Map map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var print = new MapFingerprint();
            print.AddSize(map.Size.x, map.Size.z);
            int cells = map.cellIndices.NumGridCells;
            for (int i = 0; i < cells; i++)
            {
                print.AddCell(
                    map.terrainGrid.TerrainAt(i)?.defName, map.roofGrid.RoofAt(i)?.defName);
            }

            foreach (Thing t in map.listerThings.AllThings)
            {
                if (Counted(t))
                {
                    print.AddThing(t.def.defName, t.Position.x, t.Position.z,
                        t.Rotation.AsInt, t.Stuff?.defName, t.stackCount);
                }
            }

            foreach (Pawn p in map.mapPawns.AllPawns)
            {
                if (p != null)
                {
                    print.AddPawn(p.Name?.ToStringFull, p.kindDef?.defName,
                        p.Faction == null ? null : $"{p.Faction.def?.defName}/{p.Faction.Name}");
                }
            }

            return print;
        }

        /// <summary>
        /// A fingerprint as a cue's detail: the hex, then what it covered,
        /// as in <c>0123456789abcdef (62500 cells, 31022 things, 9 pawns)</c>.
        /// </summary>
        /// <param name="print">The fingerprint.</param>
        /// <returns>The detail.</returns>
        public static string CueDetail(MapFingerprint print) =>
            $"{print.Hex} ({print.Cells} cells, {print.Things} things, "
            + $"{print.Pawns} pawns)";

        /// <summary>
        /// Whether a thing counts: spawned, and a building, item, plant or
        /// filth. Pawns are counted on their own.
        /// </summary>
        /// <param name="t">The thing.</param>
        /// <returns>True if it counts.</returns>
        private static bool Counted(Thing t)
        {
            if (t?.def == null || !t.Spawned)
            {
                return false;
            }

            switch (t.def.category)
            {
                case ThingCategory.Building:
                case ThingCategory.Item:
                case ThingCategory.Plant:
                case ThingCategory.Filth:
                    return true;
                default:
                    return false;
            }
        }
    }
}
