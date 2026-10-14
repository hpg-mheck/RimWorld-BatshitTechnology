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
// mode.

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
        public LaunchRequest(ShowcaseDef def, Game game, bool forced)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Forced = forced;
        }

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
            ShowcaseDef chosen = def;
            bool wasForced = decision.Verdict == LaunchVerdict.Forced;
            LongEventHandler.QueueLongEvent(
                () => Generate(chosen, wasForced),
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
        /// viewer quit the showcase game to the menu), then, the first
        /// time, acts on <c>-showcase=&lt;defName&gt;</c>: starts the
        /// showcase, or logs why not.
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
        /// would take a second Harmony patch); a MAPSIZE or BIOME cue says
        /// so. Returns the request once, or null.
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
            return new LaunchRequest(
                def, Current.Game, verdict == CommandLineVerdict.LaunchForced);
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
        /// worker thread, as vanilla's Quick Test does.
        /// </summary>
        /// <param name="def">The showcase.</param>
        /// <param name="forced">Whether it was forced.</param>
        private static void Generate(ShowcaseDef def, bool forced)
        {
            Root_Play.SetupForQuickTestPlay();
            Find.GameInitData.mapSize = def.MapSizeClamped;
            PreferBiome(def);
            lock (Gate)
            {
                pending = new LaunchRequest(def, Current.Game, forced);
            }

            PageUtility.InitGameStart();
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
