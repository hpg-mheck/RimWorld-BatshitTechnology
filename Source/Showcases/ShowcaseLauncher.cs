// Showcase API - the launcher.
// Starts a showcase in a fresh, throwaway game the way vanilla's dev Quick
// Test button does (MainMenuDrawer.DoMainMenuControls, RimWorld
// 1.6.4871): inside a "GeneratingMap" long event, Root_Play.
// SetupForQuickTestPlay() makes a new Game, generates a world and picks a
// random starting tile; PageUtility.InitGameStart() then loads the Play
// scene, where Root_Play.Start sees the game exists and calls
// Game.InitNewGame(). Between the two, the launcher sets the map size,
// moves the start to the def's biome if it can, and remembers which
// showcase was asked for, tied to that Game object, so a stale request
// can never turn the player's next real colony into a showcase.
// Quick Test is a dev-mode button, but the calls it makes need no dev
// mode. Every launch has a seed (the command line's, the def's, or a
// fresh random one; a SEED cue names it): the set-up and the biome's tile
// are drawn under a Rand state derived from it, and so is the map's
// preparation, which replaces PageUtility.InitGameStart with the same two
// calls. The map is seeded from the world's seed and the tile: the
// launcher opens ShowcaseMapSeed's window for the new game as soon as it
// exists, so the mod's second Harmony patch pins Map.NextGenSeed while
// that game's map is generated; the session closes it as the game starts,
// and the launcher on any failure and whenever the main menu shows
// (docs/SHOWCASE-API.md, "Seeds").

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// A showcase that was asked for, waiting for its game to start.
    /// </summary>
    public sealed class LaunchRequest
    {
        /// <summary>Creates a request.</summary>
        /// <param name="def">The showcase.</param>
        /// <param name="game">The game generated for it.</param>
        /// <param name="forced">Whether Force start was confirmed.</param>
        /// <param name="seed">
        /// The seed it was generated from, or null (a smoke test's planted
        /// request); the session then sets up without reseeding.
        /// </param>
        /// <param name="quickTest">
        /// Whether it adopts a quick-test map, which vanilla generated
        /// before the showcase was asked for.
        /// </param>
        public LaunchRequest(
            ShowcaseDef def, Game game, bool forced, ResolvedSeed seed = null,
            bool quickTest = false)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Forced = forced;
            Seed = seed;
            QuickTest = quickTest;
        }

        /// <summary>
        /// Gets a value indicating whether the request adopts a quick-test
        /// map (<c>-quicktest -showcase=</c>), whose seeds the map seed's
        /// window never pinned.
        /// </summary>
        public bool QuickTest { get; }

        /// <summary>Gets the seed the game was generated from, or null.</summary>
        public ResolvedSeed Seed { get; }

        /// <summary>Gets the showcase.</summary>
        public ShowcaseDef Def { get; }

        /// <summary>Gets the game generated for it.</summary>
        public Game Game { get; }

        /// <summary>Gets a value indicating whether it was forced.</summary>
        public bool Forced { get; }
    }

    /// <summary>
    /// Starts showcases: from the menu's Start button, and from the
    /// <c>-showcase=&lt;defName&gt;</c> command line.
    /// </summary>
    public static class ShowcaseLauncher
    {
        /// <summary>
        /// How many tiles of the preferred biome are tested for a valid
        /// starting site before giving up on it.
        /// </summary>
        private const int MaxBiomeCandidates = 300;

        /// <summary>Guards <see cref="pending"/>, set on a worker thread.</summary>
        private static readonly object Gate = new object();

        /// <summary>The request waiting for its game, or null.</summary>
        private static LaunchRequest pending;

        /// <summary>Whether the command line has been acted on.</summary>
        private static bool commandLineHandled;

        /// <summary>
        /// Gets a value indicating whether the game was started with
        /// <c>-showcaseforce</c>.
        /// </summary>
        public static bool ForceFlagPassed =>
            GenCommandLine.CommandLineArgPassed(ShowcaseStartRules.ForceArg);

        /// <summary>
        /// Gets the def name passed as <c>-showcase=&lt;defName&gt;</c>, or
        /// null.
        /// </summary>
        public static string CommandLineDefName =>
            GenCommandLine.TryGetCommandLineArg(
                ShowcaseStartRules.ShowcaseArg, out string value)
                ? value?.Trim()
                : null;

        /// <summary>
        /// Gets the seed passed as <c>-showcaseseed=&lt;text&gt;</c>, or
        /// null. It overrides every showcase's own seed, for trials.
        /// </summary>
        public static string CommandLineSeed =>
            GenCommandLine.TryGetCommandLineArg(
                ShowcaseSeed.CommandLineArg, out string value)
                ? value
                : null;

        /// <summary>
        /// Resolves a launch's seed: the command line's, else the def's,
        /// else a fresh random one made from the game's own seed words.
        /// A malformed seed offered is refused with an error (whoever
        /// passed it expected that map) and the next in order is used.
        /// </summary>
        /// <param name="def">The showcase, or null.</param>
        /// <returns>The seed.</returns>
        internal static ResolvedSeed ResolveSeed(ShowcaseDef def)
        {
            ResolvedSeed seed = ShowcaseSeed.Resolve(
                CommandLineSeed, def?.seed, GenText.RandomSeedString);
            foreach (string refusal in seed.Refused)
            {
                Log.Error($"{ShowcaseLog.Prefix} seed refused, {refusal}; using "
                    + seed.CueDetail);
            }

            return seed;
        }

        /// <summary>
        /// Starts a showcase from the main menu: queues the generation of
        /// a fresh game, the way vanilla's Quick Test does.
        /// </summary>
        /// <param name="def">The showcase.</param>
        /// <param name="forced">
        /// Whether the player confirmed Force start (or passed
        /// <c>-showcaseforce</c>).
        /// </param>
        /// <param name="refusal">
        /// Why it could not start, for the player; null on success.
        /// </param>
        /// <returns>True if the game is being generated.</returns>
        public static bool Launch(ShowcaseDef def, bool forced, out string refusal)
        {
            refusal = null;
            if (def == null)
            {
                refusal = "BT_Showcases_RefusedMissing".Translate();
                return false;
            }

            if (Current.ProgramState != ProgramState.Entry)
            {
                refusal = "BT_Showcases_RefusedInGame".Translate();
                return false;
            }

            if (LongEventHandler.AnyEventNowOrWaiting)
            {
                refusal = "BT_Showcases_RefusedBusy".Translate();
                return false;
            }

            LaunchDecision decision = ShowcaseLibrary.Evaluate(def, forced);
            if (!decision.CanStart)
            {
                refusal = ShowcaseTexts.WhyNot(decision);
                return false;
            }

            ShowcaseLog.Cue(
                "LAUNCH",
                decision.Verdict == LaunchVerdict.Forced
                    ? $"{def.defName} forced"
                    : def.defName);
            ResolvedSeed seed = ResolveSeed(def);
            ShowcaseLog.Cue("SEED", seed.CueDetail);
            LoadingTips.Prime();
            ShowcaseDef chosen = def;
            bool wasForced = decision.Verdict == LaunchVerdict.Forced;
            LongEventHandler.QueueLongEvent(
                () => Generate(chosen, wasForced, seed),
                "GeneratingMap",
                doAsynchronously: true,
                GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
            return true;
        }

        /// <summary>
        /// Takes the request for a game, if one was made for exactly that
        /// game. Called once by the game's session component.
        /// </summary>
        /// <param name="game">The game that just started.</param>
        /// <returns>The request, or null.</returns>
        public static LaunchRequest TakeRequestFor(Game game)
        {
            lock (Gate)
            {
                if (pending == null || game == null)
                {
                    return null;
                }

                if (!ReferenceEquals(pending.Game, game))
                {
                    // A generation that failed left this behind; it belongs
                    // to no game that will ever start.
                    Log.Warning($"{ShowcaseLog.Prefix} dropped a stale request "
                        + $"for {pending.Def.defName}");
                    pending = null;
                    return null;
                }

                LaunchRequest taken = pending;
                pending = null;
                return taken;
            }
        }

        /// <summary>
        /// Gets how many times the main menu has reported itself shown, for
        /// the smoke runner.
        /// </summary>
        internal static int MainMenuShownCount { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the main menu acted on the
        /// command line (rather than leaving it to a quick-test game), for
        /// the smoke runner.
        /// </summary>
        internal static bool MenuActedOnCommandLine { get; private set; }

        /// <summary>
        /// Called whenever the main menu is shown, by the main-menu
        /// button's patch, which runs when the menu is drawn in the Entry
        /// scene. Gives back any god mode a showcase still held (the
        /// viewer quit the showcase game to the menu), closes the map
        /// seed's window if a failed generation left it open (no game is
        /// generating while the menu takes input), then, the first time,
        /// acts on <c>-showcase=&lt;defName&gt;</c>: starts the showcase,
        /// or logs why not.
        /// </summary>
        /// <remarks>
        /// Under <c>-quicktest</c> the menu leaves the command line alone.
        /// Vanilla then draws the main menu for one frame (QuickStarter
        /// loads the Play scene directly from UIRoot_Entry.Init, outside a
        /// long event, RimWorld 1.6.4871) while its own quick-test game is
        /// about to generate; launching a second generation there would
        /// collide with it. The session adopts the showcase on the
        /// quick-test map instead (<see cref="TakeQuickTestRequest"/>).
        /// </remarks>
        public static void OnMainMenuShown()
        {
            MainMenuShownCount++;
            ShowcaseGodMode.Restore();
            ReleaseMapSeed("the main menu");
            if (commandLineHandled
                || !ShowcaseStartRules.MenuActsOnCommandLine(
                    GenCommandLine.CommandLineArgPassed(ShowcaseStartRules.QuickTestArg)))
            {
                return;
            }

            commandLineHandled = true;
            MenuActedOnCommandLine = true;
            string name = CommandLineDefName;
            if (name.NullOrEmpty())
            {
                return;
            }

            StartFromCommandLine(name);
        }

        /// <summary>
        /// For a game started with both <c>-quicktest</c> and
        /// <c>-showcase=</c>: the quick test skips the main menu, so the
        /// session adopts the command line's showcase on the quick-test
        /// map, as vanilla generated it (250 cells, a random tile). The
        /// def's <c>mapSize</c> and <c>biome</c> are not applied (that
        /// would take another Harmony patch), and its map is not pinned to
        /// the seed; MAPSIZE, BIOME and MAPSEED cues say so. Returns the
        /// request once, or null.
        /// </summary>
        /// <returns>The showcase and whether it is forced, or null.</returns>
        public static LaunchRequest TakeQuickTestRequest()
        {
            if (commandLineHandled
                || !GenCommandLine.CommandLineArgPassed(ShowcaseStartRules.QuickTestArg))
            {
                return null;
            }

            commandLineHandled = true;
            string name = CommandLineDefName;
            if (name.NullOrEmpty())
            {
                return null;
            }

            ShowcaseDef def = ShowcaseLibrary.Named(name);
            CommandLineVerdict verdict = Judge(name, def);
            if (!ShowcaseStartRules.Starts(verdict))
            {
                ReportCommandLineRefusal(name, def, verdict);
                return null;
            }

            int mapSize = Find.GameInitData?.mapSize ?? Find.CurrentMap?.Size.x ?? 0;
            if (def.MapSizeClamped != mapSize)
            {
                ShowcaseLog.Cue(
                    "MAPSIZE", $"{def.MapSizeClamped} ignored; quicktest map is {mapSize}");
            }

            if (def.biome != null)
            {
                ShowcaseLog.Cue("BIOME", $"{def.biome.defName} ignored; quicktest map");
            }

            ShowcaseLog.Cue("LAUNCH", $"{def.defName} quicktest");

            // Vanilla generated this map unseeded; the seed still pins the
            // worker's random state, so the cue says what it cannot pin.
            ResolvedSeed seed = ResolveSeed(def);
            ShowcaseLog.Cue("SEED", $"{seed.CueDetail}; quicktest map, not pinned");
            return new LaunchRequest(
                def, Current.Game, verdict == CommandLineVerdict.LaunchForced, seed,
                quickTest: true);
        }

        /// <summary>
        /// Gets the seed of the launch request waiting for exactly this
        /// game, without taking it: the session's FinalizeInit draws the
        /// rest of the game's start from it. Null when no request waits
        /// for this game, or the request has no seed. Never drops a stale
        /// request; <see cref="TakeRequestFor"/> does that.
        /// </summary>
        /// <param name="game">The game being started.</param>
        /// <returns>The seed, or null.</returns>
        internal static ResolvedSeed PendingSeedFor(Game game)
        {
            lock (Gate)
            {
                return pending != null && game != null && ReferenceEquals(pending.Game, game)
                    ? pending.Seed
                    : null;
            }
        }

        /// <summary>
        /// Smoke-test seam: plants a launch request, as a generation would,
        /// so the runner can check that it is handed only to its own game.
        /// Players never reach this.
        /// </summary>
        /// <param name="request">The request, or null to clear it.</param>
        internal static void PlantForTest(LaunchRequest request)
        {
            lock (Gate)
            {
                pending = request;
            }
        }

        /// <summary>
        /// Gets a value indicating whether a launch request is waiting for
        /// its game, for the smoke runner.
        /// </summary>
        internal static bool HasPendingForTest
        {
            get
            {
                lock (Gate)
                {
                    return pending != null;
                }
            }
        }

        /// <summary>
        /// Starts the command line's showcase, or logs why it cannot.
        /// </summary>
        /// <param name="name">The requested def name.</param>
        private static void StartFromCommandLine(string name)
        {
            ShowcaseDef def = ShowcaseLibrary.Named(name);
            CommandLineVerdict verdict = Judge(name, def);
            if (!ShowcaseStartRules.Starts(verdict))
            {
                ReportCommandLineRefusal(name, def, verdict);
                return;
            }

            if (!Launch(def, verdict == CommandLineVerdict.LaunchForced,
                out string refusal))
            {
                Log.Error($"{ShowcaseLog.Prefix} -showcase={name} could not "
                    + $"start: {refusal}");
            }
        }

        /// <summary>The command line's verdict for a showcase.</summary>
        /// <param name="name">The requested def name.</param>
        /// <param name="def">The showcase, or null if none has the name.</param>
        /// <returns>The verdict.</returns>
        private static CommandLineVerdict Judge(string name, ShowcaseDef def)
        {
            LaunchDecision decision = def == null
                ? null
                : ShowcaseLibrary.Evaluate(def, ForceFlagPassed);
            return ShowcaseStartRules.ForCommandLine(name, decision);
        }

        /// <summary>
        /// Logs, as an error, why a command-line showcase will not start.
        /// It is an error because whoever passed the flag (a script, a
        /// capture tool) is waiting for a showcase that will not come.
        /// </summary>
        /// <param name="name">The requested def name.</param>
        /// <param name="def">The showcase, or null.</param>
        /// <param name="verdict">The verdict.</param>
        private static void ReportCommandLineRefusal(
            string name, ShowcaseDef def, CommandLineVerdict verdict)
        {
            string why;
            switch (verdict)
            {
                case CommandLineVerdict.UnknownShowcase:
                    why = "no loaded showcase has that name; loaded: "
                        + string.Join(", ", ShowcaseLibrary.All.Select(d => d.defName));
                    break;
                case CommandLineVerdict.Unavailable:
                case CommandLineVerdict.Refused:
                    why = ShowcaseTexts.WhyNot(ShowcaseLibrary.Evaluate(def, false))
                        + (verdict == CommandLineVerdict.Refused
                            ? " (pass -showcaseforce to start it anyway)"
                            : string.Empty);
                    break;
                default:
                    why = verdict.ToString();
                    break;
            }

            ShowcaseLog.Cue("REFUSED", name);
            Log.Error($"{ShowcaseLog.Prefix} -showcase={name} will not start: {why}");
        }

        /// <summary>
        /// Generates the showcase's game. Runs inside the long event, on a
        /// worker thread, as vanilla's Quick Test does. The quick-test
        /// set-up and the biome's tile are drawn under a Rand state
        /// derived from the seed: the world's seed string (vanilla's
        /// GenText.RandomSeedString), so the world, which vanilla seeds
        /// from that string step by step; then the starting tile, the
        /// starting colonists and the biome's tile. The state is pushed
        /// and popped, as vanilla's world generation does on this thread;
        /// the main thread draws nothing from Rand meanwhile (see
        /// <see cref="LoadingTips"/>). Then it opens the map seed's window
        /// for the new game (<see cref="ShowcaseMapSeed"/>), which the
        /// session closes once the map is generated; if anything here
        /// throws after that, it abandons the launch (closes the window and
        /// drops the request) before the long event's handler reports the
        /// error.
        /// </summary>
        /// <param name="def">The showcase.</param>
        /// <param name="forced">Whether it was forced.</param>
        /// <param name="seed">The launch's seed.</param>
        private static void Generate(ShowcaseDef def, bool forced, ResolvedSeed seed)
        {
            Rand.PushState(seed.Derive(ShowcaseSeed.World));
            try
            {
                Root_Play.SetupForQuickTestPlay();
                Find.GameInitData.mapSize = def.MapSizeClamped;
                PreferBiome(def);
            }
            finally
            {
                Rand.PopState();
            }

            try
            {
                ShowcaseMapSeed.Arm(Current.Game);
                lock (Gate)
                {
                    pending = new LaunchRequest(def, Current.Game, forced, seed);
                }

                InitGameStart(seed);
            }
            catch
            {
                AbandonLaunch("a failed generation");
                throw;
            }
        }

        /// <summary>
        /// Abandons a launch whose generation or preparation threw: closes
        /// the map seed's window (<see cref="ReleaseMapSeed"/>) and drops
        /// the launch request, with a warning if one was waiting. Vanilla
        /// only logs a failed preparation and loads the Play scene anyway,
        /// whose Root_Play.Start runs Game.InitNewGame on the half-prepared
        /// game; without its request that game starts as an ordinary game,
        /// rather than as a showcase on a map its seed did not pin. Never
        /// throws.
        /// </summary>
        /// <param name="where">What failed, for the warnings.</param>
        internal static void AbandonLaunch(string where)
        {
            ReleaseMapSeed(where);
            try
            {
                lock (Gate)
                {
                    if (pending != null)
                    {
                        Log.Warning($"{ShowcaseLog.Prefix} {where} dropped the launch "
                            + $"request for {pending.Def.defName}; its game, if it "
                            + "starts, is not a showcase");
                        pending = null;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not drop the launch "
                    + $"request: {e.Message}");
            }
        }

        /// <summary>
        /// Closes the map seed's window, if it is open, and says so with a
        /// warning: it should have been closed as the showcase's game
        /// started. Never throws.
        /// </summary>
        /// <param name="where">What closed it, for the warning.</param>
        internal static void ReleaseMapSeed(string where)
        {
            try
            {
                int pinned = ShowcaseMapSeed.Disarm();
                if (pinned >= 0)
                {
                    Log.Warning($"{ShowcaseLog.Prefix} {where} closed the "
                        + $"showcase map seed's window, left open after {pinned} "
                        + "seeds; that game's map was not finished");
                }
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not close the showcase "
                    + $"map seed's window: {e.Message}");
            }
        }

        /// <summary>
        /// Loads the Play scene to generate the map: vanilla's
        /// PageUtility.InitGameStart (RimWorld 1.6.4871), whose two calls
        /// are made here under a Rand state derived from the seed. The
        /// first gives the colonists their work priorities, breaking ties
        /// at random; the second founds the player's settlement, with a
        /// random name. The map that follows is seeded from the world's
        /// seed and the starting tile, pinned by the map seed's window; if
        /// either call throws, the launch is abandoned (the window closed
        /// and the request dropped) before the error goes on to vanilla,
        /// which logs it and starts the game anyway.
        /// </summary>
        /// <param name="seed">The launch's seed.</param>
        private static void InitGameStart(ResolvedSeed seed)
        {
            LongEventHandler.QueueLongEvent(
                () =>
                {
                    Rand.PushState(seed.Derive(ShowcaseSeed.Prep));
                    try
                    {
                        Find.GameInitData.PrepForMapGen();
                        Find.Scenario.PreMapGenerate();
                    }
                    catch
                    {
                        AbandonLaunch("a failed preparation");
                        throw;
                    }
                    finally
                    {
                        Rand.PopState();
                    }
                },
                "Play",
                "GeneratingMap",
                doAsynchronously: true,
                null);
        }

        /// <summary>
        /// Moves the start to a random valid tile of the def's biome, if it
        /// names one and the world has one; otherwise keeps the quick
        /// test's random tile, and says so in the log.
        /// </summary>
        /// <param name="def">The showcase.</param>
        private static void PreferBiome(ShowcaseDef def)
        {
            BiomeDef biome = def.biome;
            if (biome == null)
            {
                return;
            }

            PlanetTile chosen = FindBiomeTile(biome);
            if (chosen.Valid)
            {
                Find.GameInitData.startingTile = chosen;
                ShowcaseLog.Cue("BIOME", biome.defName);
            }
            else
            {
                ShowcaseLog.Cue("BIOME", $"{biome.defName} not found; random start");
            }
        }

        /// <summary>
        /// A random tile of a biome that a colony may start on, or an
        /// invalid tile.
        /// </summary>
        /// <param name="biome">The biome.</param>
        /// <returns>The tile, or <see cref="PlanetTile.Invalid"/>.</returns>
        private static PlanetTile FindBiomeTile(BiomeDef biome)
        {
            if (!biome.canBuildBase || !biome.implemented)
            {
                return PlanetTile.Invalid;
            }

            PlanetLayer layer = Find.WorldGrid.Surface;
            var candidates = new List<PlanetTile>();
            for (int i = 0; i < layer.TilesCount; i++)
            {
                Tile tile = layer[i];
                if (tile != null && tile.PrimaryBiome == biome
                    && tile.hilliness != Hilliness.Impassable)
                {
                    candidates.Add(tile.tile);
                }
            }

            candidates.Shuffle();
            foreach (PlanetTile tile in candidates.Take(MaxBiomeCandidates))
            {
                if (TileFinder.IsValidTileForNewSettlement(tile))
                {
                    return tile;
                }
            }

            return PlanetTile.Invalid;
        }
    }
}
