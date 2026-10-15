// Batshit Technology - the Showcase API's smoke checks for seeds,
// fingerprints and the quiet storyteller (docs/SHOWCASE-API.md, "Seeds"
// and "The Quiet Storyteller"). They run in the smoke game, which has no
// launch of its own: a seed is resolved as a launch would resolve it, the
// global random state is reseeded and put back, the map is fingerprinted
// around a stack of steel that comes and goes, the map seed's prefix is
// seen passing Map.NextGenSeed through to vanilla in this ordinary game
// and pinning it only while its window is open for this very game, and
// the directed test run is watched holding a harmless storyteller part
// and giving it back. A launch that failed is seen abandoned (its
// request dropped, its window closed), and the settle hold is seen to
// outlast a screen fade, which makes the game refuse to unpause. What
// only a launched game shows (the SEED, MAPSEED and MAPHASH cues, the map
// seed's window opened by the launcher and closed as the game starts, the
// start drawn from the seed, a seed from the command line) the tour
// verifier checks (Source/SmokeTestTour.cs).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BatshitTechnology.Showcases;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The Showcase API's seed, fingerprint and storyteller checks.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// The seed the test showcase is given to check the def's place in
        /// the order.
        /// </summary>
        private const string SmokeSeed = "smoke-seed";

        /// <summary>
        /// A storyteller part that makes no incident at all, so a check can
        /// watch parts being held and given back without a real storyteller
        /// sending anything into the smoke run.
        /// </summary>
        private sealed class SmokeStorytellerComp : StorytellerComp
        {
            /// <summary>Creates the part with default properties.</summary>
            public SmokeStorytellerComp()
            {
                props = new StorytellerCompProperties();
            }
        }

        /// <summary>
        /// A launch's seed, resolved in game: the def's seed when it has
        /// one, else a fresh random one each launch, well formed (made from
        /// the game's own seed words); a smoke run passes no
        /// <c>-showcaseseed=</c>. A malformed def seed is a configuration
        /// error.
        /// </summary>
        private void TestShowcaseSeeds()
        {
            ShowcaseDef def = ShowcaseLibrary.Named(DirectorShowcase);
            string had = def?.seed;
            ResolvedSeed pinned = null, first = null, second = null;
            List<string> errors = new List<string>();
            try
            {
                if (def != null)
                {
                    def.seed = SmokeSeed;
                    pinned = ShowcaseLauncher.ResolveSeed(def);
                    def.seed = "two words";
                    errors = def.ConfigErrors().Where(e => e.Contains("seed")).ToList();
                    def.seed = null;
                    first = ShowcaseLauncher.ResolveSeed(def);
                    second = ShowcaseLauncher.ResolveSeed(def);
                }
            }
            finally
            {
                if (def != null)
                {
                    def.seed = had;
                }
            }

            Check("a launch's seed is the def's, else a fresh random one each "
                    + "launch; no command line seed in a smoke run",
                ShowcaseLauncher.CommandLineSeed == null
                    && pinned?.Text == SmokeSeed && pinned.Source == SeedSource.Def
                    && first?.Source == SeedSource.Random
                    && second?.Source == SeedSource.Random
                    && ShowcaseSeed.IsWellFormed(first.Text)
                    && ShowcaseSeed.IsWellFormed(second.Text)
                    && first.Text != second.Text,
                $"command line '{ShowcaseLauncher.CommandLineSeed}'; pinned "
                    + $"{pinned?.CueDetail ?? "none"}; random {first?.CueDetail ?? "none"}, "
                    + $"{second?.CueDetail ?? "none"}");
            Check("a malformed def seed is a configuration error",
                errors.Count == 1 && errors[0].Contains("'two words' is not usable"),
                $"[{string.Join(" | ", errors)}]");
        }

        /// <summary>
        /// A launch whose generation or preparation threw is abandoned: the
        /// map seed's window is closed and the launch request dropped, each
        /// with a warning. Vanilla logs a failed preparation and loads the
        /// Play scene anyway, so before, the half-prepared game took the
        /// request and ran the showcase on a map its seed did not pin. Here
        /// a request for this very game is planted and the window opened
        /// for it, as the launcher does, and the launch abandoned.
        /// </summary>
        private void TestFailedLaunchDropsRequest()
        {
            const string Where = "a smoke test's failed preparation";
            ShowcaseDef def = ShowcaseLibrary.Named(DirectorShowcase);
            LogMessage mark = Log.Messages.LastOrDefault();
            bool pendingBefore = false, pendingAfter = true, openAfter = true;
            LaunchRequest taken = null;
            string error = null;
            try
            {
                ShowcaseLauncher.PlantForTest(new LaunchRequest(def, Current.Game, false));
                ShowcaseMapSeed.Arm(Current.Game);
                pendingBefore = ShowcaseLauncher.HasPendingForTest;
                ShowcaseLauncher.AbandonLaunch(Where);
                pendingAfter = ShowcaseLauncher.HasPendingForTest;
                openAfter = ShowcaseMapSeed.Armed;
                taken = ShowcaseLauncher.TakeRequestFor(Current.Game);
            }
            catch (Exception e)
            {
                error = e.ToString();
            }
            finally
            {
                ShowcaseLauncher.PlantForTest(null);
                ShowcaseMapSeed.Disarm();
            }

            List<LogMessage> warned = MessagesSince(mark)
                .Where(m => m.type == LogMessageType.Warning && m.text.Contains(Where))
                .ToList();
            ExpectLogs(warned);
            Check("a failed launch drops its request and closes the map seed's "
                    + "window, saying so",
                error == null && pendingBefore && !pendingAfter && !openAfter
                    && taken == null && warned.Count == 2,
                $"pending before {pendingBefore}, after {pendingAfter}; window open "
                    + $"after {openAfter}; handed to the game afterwards {taken != null}; "
                    + $"warnings {warned.Count}"
                    + (error == null ? string.Empty : $"; threw {error}"));
        }

        /// <summary>
        /// The settle hold outlasts a screen fade. While the screen fades
        /// (Root_Play.Start queues a half-second fade as a new game's map
        /// appears) RimWorld silently refuses any change of speed; the
        /// session's release must then keep the hold on, so a later frame
        /// tries again, and take once the fade is over. Before, a refused
        /// release cleared the hold, and the showcase stayed paused at
        /// READY. The smoke game is held, a long fade to clear (invisible)
        /// is started, the release is tried, the fade ended, and the
        /// release tried again; the game's speed is put back.
        /// </summary>
        private void TestSettleHoldOutlastsFade()
        {
            TickManager ticks = Find.TickManager;
            TimeSpeed before = ticks.CurTimeSpeed;
            bool held = false, fading = false, releasedInFade = true;
            bool heldInFade = false, pausedInFade = false;
            bool releasedAfter = false, heldAfter = true, runningAfter = false;
            string error = null;
            try
            {
                ticks.CurTimeSpeed = TimeSpeed.Normal;
                held = showcaseSession.HoldForTest() && ticks.CurTimeSpeed == TimeSpeed.Paused;
                ScreenFader.StartFade(UnityEngine.Color.clear, 30f);
                fading = ScreenFader.IsFading() && !Current.Game.PlayerHasControl;
                releasedInFade = showcaseSession.ReleaseHoldForTest();
                heldInFade = showcaseSession.SettleHeld;
                pausedInFade = ticks.CurTimeSpeed == TimeSpeed.Paused;
                ScreenFader.SetColor(UnityEngine.Color.clear);
                releasedAfter = showcaseSession.ReleaseHoldForTest();
                heldAfter = showcaseSession.SettleHeld;
                runningAfter = ticks.CurTimeSpeed == TimeSpeed.Normal;
            }
            catch (Exception e)
            {
                error = e.ToString();
            }
            finally
            {
                ScreenFader.SetColor(UnityEngine.Color.clear);
                if (!showcaseSession.ReleaseHoldForTest())
                {
                    showcaseSession.ReleaseForTest();
                }

                ticks.CurTimeSpeed = before;
            }

            Check("the settle hold outlasts a screen fade: a refused unpause "
                    + "keeps the hold, and the next try takes",
                error == null && held && fading && !releasedInFade && heldInFade
                    && pausedInFade && releasedAfter && !heldAfter && runningAfter,
                $"held {held}; fading {fading}; in the fade released {releasedInFade}, "
                    + $"still held {heldInFade}, paused {pausedInFade}; after it "
                    + $"released {releasedAfter}, still held {heldAfter}, running "
                    + $"{runningAfter}" + (error == null ? string.Empty : $"; threw {error}"));
        }

        /// <summary>
        /// Reseeding RimWorld's global random state from a seed repeats its
        /// draws, another seed draws differently, and neither logs the
        /// "initial rand seed" error (the final log check would catch it).
        /// The smoke run's own state is put back afterwards.
        /// </summary>
        private void TestGlobalReseed()
        {
            ulong? before = ShowcaseRandom.Snapshot();
            List<int> a = null, b = null, c = null;
            try
            {
                a = DrawAfterReseed(ShowcaseSeed.Derive(SmokeSeed, ShowcaseSeed.Stage));
                b = DrawAfterReseed(ShowcaseSeed.Derive(SmokeSeed, ShowcaseSeed.Stage));
                c = DrawAfterReseed(ShowcaseSeed.Derive("other-seed", ShowcaseSeed.Stage));
            }
            finally
            {
                if (before.HasValue)
                {
                    ShowcaseRandom.Restore(before.Value);
                }
            }

            Check("reseeding the global random state repeats a seed's draws, "
                    + "and another seed draws differently",
                before.HasValue && a != null && a.SequenceEqual(b)
                    && !a.SequenceEqual(c),
                $"state readable {before.HasValue}; draws [{Join(a)}] then "
                    + $"[{Join(b)}]; other seed [{Join(c)}]");
        }

        /// <summary>
        /// The map's fingerprint is the same read twice, draws nothing from
        /// Rand, changes when a stack of steel is put down and comes back
        /// when it is taken away, and covers every cell and pawn.
        /// </summary>
        /// <param name="map">The quick-test map.</param>
        private void TestMapFingerprint(Map map)
        {
            ulong? randBefore = ShowcaseRandom.Snapshot();
            MapFingerprint first = ShowcaseFingerprint.Of(map);
            MapFingerprint again = ShowcaseFingerprint.Of(map);
            ulong? randAfter = ShowcaseRandom.Snapshot();
            string withSteel = null, afterSteel = null;
            if (CellFinder.TryFindRandomCellNear(map.Center, map, 30,
                c => c.Standable(map) && c.GetFirstItem(map) == null
                    && c.GetFirstPawn(map) == null, out IntVec3 cell))
            {
                Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                steel.stackCount = 7;
                GenSpawn.Spawn(steel, cell, map);
                withSteel = ShowcaseFingerprint.Of(map).Hex;
                steel.Destroy(DestroyMode.Vanish);
                afterSteel = ShowcaseFingerprint.Of(map).Hex;
            }

            int pawns = map.mapPawns.AllPawns.Count;
            Check("the map fingerprint is stable, draws nothing from Rand, sees "
                    + "a stack come and go, and covers every cell and pawn",
                first.Hex == again.Hex && randBefore == randAfter
                    && withSteel != null && withSteel != first.Hex
                    && afterSteel == first.Hex
                    && first.Cells == map.cellIndices.NumGridCells
                    && first.Pawns == pawns && first.Things > 0,
                $"{ShowcaseFingerprint.CueDetail(first)} then {again.Hex}; Rand "
                    + $"{randBefore} -> {randAfter}; with steel {withSteel ?? "none placed"}, "
                    + $"after {afterSteel}; cells {map.cellIndices.NumGridCells}, "
                    + $"pawns {pawns}");
        }

        /// <summary>
        /// In the smoke run's ordinary game (a quick test, never a
        /// showcase) the map seed's window is closed and the patched
        /// Map.NextGenSeed is vanilla's own: <c>System.HashCode.Combine</c>
        /// of the map's place, its counter and the world's seed, computed
        /// here through that same method in this process. The counter
        /// advances by one and the prefix counts the call as passed
        /// through, not pinned. With the window open for another game it
        /// stays vanilla. The map's counter is put back afterwards, so the
        /// smoke run's own map is left as it was.
        /// </summary>
        /// <param name="map">The quick-test map.</param>
        private void TestMapSeedPassesThrough(Map map)
        {
            bool closed = !ShowcaseMapSeed.Armed && !ShowcaseMapSeed.PinsFor(Current.Game);
            long pinnedBefore = ShowcaseMapSeed.PinnedTotal;
            long passedBefore = ShowcaseMapSeed.PassedTotal;
            int? counter = MapGenCounter(map);
            int? vanilla = null, got = null, afterOne = null;
            int? otherVanilla = null, otherGot = null;
            int pinnedForOther = -2;
            string error = null;
            try
            {
                int place = ShowcaseMapSeed.Place(map);
                int world = Find.World.info.Seed;
                vanilla = VanillaGenSeed(place, counter.Value, world);
                got = map.NextGenSeed;
                afterOne = MapGenCounter(map);
                var other = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
                ShowcaseMapSeed.Arm(other);
                otherVanilla = VanillaGenSeed(place, afterOne.Value, world);
                otherGot = map.NextGenSeed;
            }
            catch (Exception e)
            {
                error = e.ToString();
            }
            finally
            {
                pinnedForOther = ShowcaseMapSeed.Disarm();
                SetMapGenCounter(map, counter);
            }

            long pinned = ShowcaseMapSeed.PinnedTotal - pinnedBefore;
            long passed = ShowcaseMapSeed.PassedTotal - passedBefore;
            Check("in an ordinary game the map seed's window is closed and "
                    + "Map.NextGenSeed is vanilla's, even with the window open "
                    + "for another game",
                error == null && closed && ShowcaseMapSeed.Installed
                    && vanilla.HasValue && got == vanilla && afterOne == counter + 1
                    && otherVanilla.HasValue && otherGot == otherVanilla
                    && pinnedForOther == 0 && pinned == 0 && passed == 2
                    && MapGenCounter(map) == counter && !ShowcaseMapSeed.Armed,
                $"closed {closed}; installed {ShowcaseMapSeed.Installed}; counter "
                    + $"{counter?.ToString() ?? "unreadable"} -> {afterOne}; got {got}, "
                    + $"vanilla {vanilla}; for another game got {otherGot}, vanilla "
                    + $"{otherVanilla}, its window pinned {pinnedForOther}; pinned "
                    + $"{pinned}, passed through {passed}; counter put back "
                    + $"{MapGenCounter(map) == counter}"
                    + (error == null ? string.Empty : $"; threw {error}"));
        }

        /// <summary>
        /// With the window open for this very game (as the launcher opens
        /// it for a showcase's game), Map.NextGenSeed is pinned: the same
        /// three inputs combined with <c>Gen.HashCombineInt</c>
        /// (ShowcaseMapSeed.Combine), the counter advanced by one, the
        /// call counted as pinned; closing the window says one seed was
        /// pinned, and the next call is vanilla's again. The window is
        /// closed and the map's counter put back whatever happens.
        /// </summary>
        /// <param name="map">The quick-test map.</param>
        private void TestMapSeedPins(Map map)
        {
            long pinnedBefore = ShowcaseMapSeed.PinnedTotal;
            int? counter = MapGenCounter(map);
            int? expected = null, got = null, afterOne = null;
            int? vanillaAfter = null, gotAfter = null;
            bool openWhileArmed = false, pinsWhileArmed = false;
            int closedWith = -2;
            string error = null;
            try
            {
                int place = ShowcaseMapSeed.Place(map);
                int world = Find.World.info.Seed;
                ShowcaseMapSeed.Arm(Current.Game);
                openWhileArmed = ShowcaseMapSeed.Armed;
                pinsWhileArmed = ShowcaseMapSeed.PinsFor(Current.Game);
                expected = ShowcaseMapSeed.Combine(place, counter.Value, world);
                got = map.NextGenSeed;
                afterOne = MapGenCounter(map);
                closedWith = ShowcaseMapSeed.Disarm();
                vanillaAfter = VanillaGenSeed(place, afterOne.Value, world);
                gotAfter = map.NextGenSeed;
            }
            catch (Exception e)
            {
                error = e.ToString();
            }
            finally
            {
                int late = ShowcaseMapSeed.Disarm();
                if (closedWith == -2)
                {
                    closedWith = late;
                }

                SetMapGenCounter(map, counter);
            }

            long pinned = ShowcaseMapSeed.PinnedTotal - pinnedBefore;
            Check("with the window open for its game, Map.NextGenSeed is "
                    + "pinned to Gen.HashCombineInt of vanilla's inputs, and "
                    + "closing the window makes it vanilla's again",
                error == null && openWhileArmed && pinsWhileArmed
                    && expected.HasValue && got == expected && afterOne == counter + 1
                    && closedWith == 1 && pinned == 1
                    && vanillaAfter.HasValue && gotAfter == vanillaAfter
                    && !ShowcaseMapSeed.Armed && MapGenCounter(map) == counter,
                $"open {openWhileArmed}, pins this game {pinsWhileArmed}; counter "
                    + $"{counter?.ToString() ?? "unreadable"} -> {afterOne}; got {got}, "
                    + $"pinned value {expected}; closing said {closedWith}; pinned "
                    + $"{pinned}; then got {gotAfter}, vanilla {vanillaAfter}; counter "
                    + $"put back {MapGenCounter(map) == counter}"
                    + (error == null ? string.Empty : $"; threw {error}"));
        }

        /// <summary>
        /// Vanilla's Map.NextGenSeed for given inputs, computed as vanilla
        /// computes it (<c>System.HashCode.Combine&lt;int, int, int&gt;</c>,
        /// RimWorld 1.6.4871), through reflection, since the mod compiles
        /// against .NET Framework 4.7.2, which has no System.HashCode; the
        /// game's runtime has it. Its per-process seed is this process's,
        /// so the value matches vanilla's here and nowhere else.
        /// </summary>
        /// <param name="place">The map's place.</param>
        /// <param name="counter">The map's counter.</param>
        /// <param name="worldSeed">The world's seed.</param>
        /// <returns>The seed.</returns>
        /// <exception cref="MissingMethodException">
        /// Thrown when the runtime has no System.HashCode.Combine.
        /// </exception>
        private static int VanillaGenSeed(int place, int counter, int worldSeed)
        {
            Type hashCode = typeof(object).Assembly.GetType("System.HashCode")
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("System.HashCode"))
                    .FirstOrDefault(t => t != null);
            MethodInfo combine = hashCode?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Combine" && m.GetGenericArguments().Length == 3);
            if (combine == null)
            {
                throw new MissingMethodException("System.HashCode", "Combine");
            }

            return (int)combine.MakeGenericMethod(typeof(int), typeof(int), typeof(int))
                .Invoke(null, new object[] { place, counter, worldSeed });
        }

        /// <summary>Vanilla's private generation counter of a map.</summary>
        private static readonly FieldInfo MapGenCounterField = typeof(Map).GetField(
            "generatedId", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>A map's generation counter, or null if unreadable.</summary>
        /// <param name="map">The map.</param>
        /// <returns>The counter.</returns>
        private static int? MapGenCounter(Map map) =>
            MapGenCounterField?.FieldType == typeof(int)
                ? (int)MapGenCounterField.GetValue(map)
                : (int?)null;

        /// <summary>Puts a map's generation counter back, if it was read.</summary>
        /// <param name="map">The map.</param>
        /// <param name="value">The counter as read, or null.</param>
        private static void SetMapGenCounter(Map map, int? value)
        {
            if (value.HasValue && MapGenCounterField?.FieldType == typeof(int))
            {
                MapGenCounterField.SetValue(map, value.Value);
            }
        }

        /// <summary>
        /// Gives the storyteller one harmless part before a test run, so
        /// the run can be seen to hold it and give it back.
        /// </summary>
        /// <returns>
        /// The storyteller's parts before (the smoke run's own, silenced),
        /// to put back with <see cref="PutBackStoryteller"/>, and the part.
        /// </returns>
        private static (List<StorytellerComp> Before, StorytellerComp Part) LendStorytellerPart()
        {
            Storyteller teller = Find.Storyteller;
            List<StorytellerComp> before = teller.storytellerComps;
            var part = new SmokeStorytellerComp();
            teller.storytellerComps = new List<StorytellerComp> { part };
            return (before, part);
        }

        /// <summary>
        /// Puts back the smoke run's own (silenced) storyteller parts.
        /// </summary>
        /// <param name="before">The parts to put back.</param>
        private static void PutBackStoryteller(List<StorytellerComp> before)
        {
            Find.Storyteller.storytellerComps = before ?? new List<StorytellerComp>();
            Find.Storyteller.incidentQueue?.Clear();
        }

        /// <summary>
        /// Checks the record cues and the storyteller around a test run
        /// that finished: the storyteller quiet from START to FINISHED and
        /// its part given back at once; STAGEHASH just before READY,
        /// matching the session's; no SEED, MAPSEED or MAPHASH, since
        /// nothing was launched; and no reseed without a seed.
        /// </summary>
        /// <param name="mark">The last message before the run.</param>
        /// <param name="part">The storyteller part lent for the run.</param>
        /// <param name="quietWhileRunning">
        /// Whether the storyteller was seen quiet while the run was going.
        /// </param>
        /// <param name="stageHash">The session's STAGEHASH at READY.</param>
        /// <param name="reseeded">Whether the session reseeded.</param>
        private void CheckRecordCues(
            LogMessage mark, StorytellerComp part, bool quietWhileRunning,
            string stageHash, bool reseeded)
        {
            List<string> cues = ShowcaseCuesSince(mark, withRecords: true);
            int start = cues.FindIndex(c => c.StartsWith("START ", StringComparison.Ordinal));
            int quiet = cues.FindIndex(c => c == "STORYTELLER quiet (1 part held)");
            int stage = cues.FindIndex(c => c.StartsWith("STAGEHASH ", StringComparison.Ordinal));
            int ready = cues.IndexOf("READY");
            int finished = cues.FindIndex(c => c.StartsWith("FINISHED ", StringComparison.Ordinal));
            int restored = cues.IndexOf("STORYTELLER restored (1 part)");
            List<StorytellerComp> parts = Find.Storyteller.storytellerComps;
            Check("a showcase quiets the storyteller from its start until it "
                    + "finishes, then gives its parts back",
                quietWhileRunning && start >= 0 && quiet > start && quiet < ready
                    && finished > ready && restored == finished + 1
                    && !showcaseSession.StorytellerQuiet
                    && parts.Count == 1 && ReferenceEquals(parts[0], part),
                $"quiet while running {quietWhileRunning}; cues "
                    + $"[{string.Join(" | ", cues.Where(c => !c.StartsWith("SCENE")))}]; "
                    + $"parts now {parts.Count}");
            Check("STAGEHASH comes just before READY with the session's "
                    + "fingerprint; an adopted run logs no SEED, MAPSEED or "
                    + "MAPHASH and does not reseed",
                stage >= 0 && stage == ready - 1 && stageHash != null
                    && cues[stage].StartsWith($"STAGEHASH {stageHash} (", StringComparison.Ordinal)
                    && !cues.Any(c => c.StartsWith("SEED ", StringComparison.Ordinal)
                        || c.StartsWith("MAPSEED ", StringComparison.Ordinal)
                        || c.StartsWith("MAPHASH ", StringComparison.Ordinal))
                    && !reseeded,
                $"STAGEHASH at {stage}, READY at {ready}, session {stageHash ?? "none"}; "
                    + $"reseeded {reseeded}; records [{string.Join(" | ", cues.Where(IsRecord))}]");
        }

        /// <summary>
        /// Checks that Stop gives the storyteller its parts back, with the
        /// STORYTELLER cue just after STOPPED.
        /// </summary>
        /// <param name="mark">The last message before the Stop.</param>
        private void CheckStopRestoresStoryteller(LogMessage mark)
        {
            List<string> cues = ShowcaseCuesSince(mark, withRecords: true);
            int stopped = cues.FindIndex(c => c.StartsWith("STOPPED ", StringComparison.Ordinal));
            Check("Stop gives the storyteller its parts back",
                !showcaseSession.StorytellerQuiet && stopped >= 0
                    && stopped + 1 < cues.Count
                    && cues[stopped + 1].StartsWith("STORYTELLER restored", StringComparison.Ordinal),
                $"quiet {showcaseSession.StorytellerQuiet}; cues [{string.Join(" | ", cues)}]");
        }

        /// <summary>Whether a cue is one of the API's record cues.</summary>
        /// <param name="cue">The cue, without the prefix.</param>
        /// <returns>
        /// True for SEED, MAPSEED, MAPHASH, STAGEHASH and STORYTELLER.
        /// </returns>
        private static bool IsRecord(string cue) =>
            ShowcaseLog.RecordCues.Contains(cue.Split(' ')[0]);

        /// <summary>
        /// Reseeds the global state and takes eight draws.
        /// </summary>
        /// <param name="seed">The seed.</param>
        /// <returns>The draws, or null if the state could not be set.</returns>
        private static List<int> DrawAfterReseed(int seed) =>
            ShowcaseRandom.Reseed(seed)
                ? Enumerable.Range(0, 8).Select(_ => Rand.Int).ToList()
                : null;

        /// <summary>Joins numbers for a message.</summary>
        /// <param name="values">The numbers, or null.</param>
        /// <returns>The text.</returns>
        private static string Join(IEnumerable<int> values) =>
            values == null ? "none" : string.Join(", ", values);
    }
}
