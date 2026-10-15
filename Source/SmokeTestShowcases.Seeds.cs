// Batshit Technology - the Showcase API's smoke checks for seeds,
// fingerprints and the quiet storyteller (docs/SHOWCASE-API.md, "Seeds"
// and "The Quiet Storyteller"). They run in the smoke game, which has no
// launch of its own: a seed is resolved as a launch would resolve it, the
// global random state is reseeded and put back, the map is fingerprinted
// around a stack of steel that comes and goes, and the directed test run
// is watched holding a harmless storyteller part and giving it back. What
// only a launched game shows (the SEED and MAPHASH cues, the settle hold,
// a seed from the command line) the tour verifier checks
// (Source/SmokeTestTour.cs).

using System;
using System.Collections.Generic;
using System.Linq;
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
        /// matching the session's; no SEED or MAPHASH, since nothing was
        /// launched; and no reseed without a seed.
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
                    + "fingerprint; an adopted run logs no SEED or MAPHASH and "
                    + "does not reseed",
                stage >= 0 && stage == ready - 1 && stageHash != null
                    && cues[stage].StartsWith($"STAGEHASH {stageHash} (", StringComparison.Ordinal)
                    && !cues.Any(c => c.StartsWith("SEED ", StringComparison.Ordinal)
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
        /// <returns>True for SEED, MAPHASH, STAGEHASH and STORYTELLER.</returns>
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
