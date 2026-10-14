// Batshit Technology - the tour verifier's watch on the boomalope
// stampede (scene "stampede"), and its clock on every scene. The facts
// come from Core's Stampede rules, measured from the world each tick
// rather than taken from the scene: the bolts in each volley from the
// Ballista's counter, the herd's dead when the first live boomalope comes
// inside the base's keep-away, and each colonist's injuries against those
// it had when the scene began. The clock reports how fast the game ran.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The tour verifier's stampede watch and scene clock.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// What the verifier saw of one run of the stampede: its volleys,
        /// the herd at the keep-away, the colonists' injuries and the stage.
        /// </summary>
        private sealed class StampedeWatch
        {
            /// <summary>
            /// Each colonist's injuries as the scene began.
            /// </summary>
            private readonly Dictionary<Pawn, HashSet<Hediff>> injuries =
                new Dictionary<Pawn, HashSet<Hediff>>();

            /// <summary>What stood of the stage as the scene began.</summary>
            private readonly List<Thing> stage = new List<Thing>();

            /// <summary>
            /// Colonists seen downed or dead during the scene.
            /// </summary>
            private readonly HashSet<string> fallen = new HashSet<string>();

            /// <summary>The base's centre.</summary>
            private readonly Cell baseCentre;

            /// <summary>The tour's map.</summary>
            private readonly Map map;

            /// <summary>
            /// Fires already inside the base's keep-away as the scene began
            /// (left by an earlier scene), by thing ID, so they are not
            /// blamed on the stampede.
            /// </summary>
            private readonly HashSet<int> firesBefore = new HashSet<int>();

            /// <summary>
            /// Colonists seen drafted with Fire At Will on during the scene.
            /// </summary>
            private readonly HashSet<string> firingAtWill = new HashSet<string>();

            /// <summary>
            /// The Ballista's bolts fired, as of the last tick.
            /// </summary>
            private int boltsSeen;

            /// <summary>Ticks sampled so far: the scene's own clock.</summary>
            private int sampled;

            /// <summary>
            /// Starts watching a stampede that has just begun.
            /// </summary>
            /// <param name="scene">The scene.</param>
            /// <param name="director">The tour's director.</param>
            public StampedeWatch(StampedeScene scene, BatshitTourDirector director)
            {
                Scene = scene;
                map = director.Map;
                TourStage s = director.Stage ?? TourStage.Empty;
                baseCentre = s.BaseCentre.IsValid
                    ? AnyRim.ToCell(s.BaseCentre)
                    : AnyRim.ToCell(director.Map.Center);
                boltsSeen = Building_BatshittiumBallista.BoltsFired;
                foreach (Pawn p in director.Map.mapPawns.FreeColonistsSpawned)
                {
                    injuries[p] = Injuries(p);
                }

                foreach (Thing f in FiresNearBase())
                {
                    firesBefore.Add(f.thingIDNumber);
                }

                stage.AddRange(new Thing[]
                    {
                        s.Reactor, s.Replicator, s.Transmogrifier, s.Squeezer,
                        s.Artificer, s.Ballista,
                    }
                    .Concat(s.Pads ?? new List<Thing>())
                    .Where(Standing));
            }

            /// <summary>Gets the scene watched.</summary>
            public StampedeScene Scene { get; }

            /// <summary>
            /// Gets the bolts in each volley seen, in order.
            /// </summary>
            public List<int> Volleys { get; } = new List<int>();

            /// <summary>
            /// Gets the herd's dead when the first live boomalope came
            /// inside the keep-away, or null while none has.
            /// </summary>
            public int? DeadAtFirstEntry { get; private set; }

            /// <summary>
            /// Gets where the first one came inside, if one did.
            /// </summary>
            public IntVec3 FirstEntryCell { get; private set; } = IntVec3.Invalid;

            /// <summary>
            /// Gets the most new fires seen at once inside the base's
            /// keep-away during the scene.
            /// </summary>
            public int MostFiresNearBase { get; private set; }

            /// <summary>
            /// Gets where and at which scene tick the first new fire inside
            /// the keep-away was seen, or null.
            /// </summary>
            public string FirstFireNearBase { get; private set; }

            /// <summary>Gets the most fires seen at once anywhere on the map.</summary>
            public int MostFires { get; private set; }

            /// <summary>
            /// Gets what of a singularity grenade was seen during the scene
            /// (its projectile or its field), or null.
            /// </summary>
            public string SingularitySeen { get; private set; }

            /// <summary>Samples the scene once.</summary>
            public void Sample()
            {
                sampled++;
                SampleFires();
                SampleSingularity();
                foreach (Pawn p in injuries.Keys)
                {
                    if (p.Spawned && p.Drafted && p.drafter?.FireAtWill == true)
                    {
                        firingAtWill.Add(p.LabelShort);
                    }
                }

                int fired = Building_BatshittiumBallista.BoltsFired - boltsSeen;
                boltsSeen = Building_BatshittiumBallista.BoltsFired;
                if (fired > 0)
                {
                    Volleys.Add(fired); // a volley leaves in a single tick
                }

                IReadOnlyList<Pawn> herd = Scene.Herd;
                if (DeadAtFirstEntry == null)
                {
                    Pawn inside = herd.FirstOrDefault(p => p != null
                        && !p.Dead && p.Spawned
                        && Stampede.InsideKeepAway(baseCentre, AnyRim.ToCell(p.Position)));
                    if (inside != null)
                    {
                        DeadAtFirstEntry = herd.Count(p => p != null && p.Dead);
                        FirstEntryCell = inside.Position;
                    }
                }

                foreach (Pawn p in injuries.Keys)
                {
                    if (p.Dead || p.Destroyed || p.Downed)
                    {
                        fallen.Add(p.LabelShort + (p.Dead ? " (dead)" : " (downed)"));
                    }
                }
            }

            /// <summary>
            /// Records the scene's facts at its end, through the verifier's
            /// fact recorder, and logs a note of what happened.
            /// </summary>
            /// <param name="fact">Records one fact: what, and whether it held.</param>
            /// <param name="map">The tour's map.</param>
            public void Check(System.Action<string, bool> fact, Map map)
            {
                IReadOnlyList<Pawn> herd = Scene.Herd;
                int size = herd.Count;
                int dead = herd.Count(p => p != null && p.Dead);
                int largest = Volleys.Count == 0 ? 0 : Volleys.Max();
                fact($"a herd of at least {Stampede.MinHerd} ({size})",
                    size >= Stampede.MinHerd);
                fact($"a {Stampede.MoneyShotBolts}-bolt volley (largest "
                        + $"{largest}; volleys [{string.Join(", ", Volleys)}])",
                    largest >= Stampede.MoneyShotBolts);
                fact($"at most {Stampede.MaxVolleys} volleys ({Volleys.Count})",
                    Volleys.Count <= Stampede.MaxVolleys);
                string entry = DeadAtFirstEntry is int d
                    ? $"{d} dead when one reached {FirstEntryCell}"
                    : "none reached the keep-away";
                fact($"{Stampede.DeadShare:P0} of the herd dead before any "
                        + $"reached the base ({entry}; {dead} of {size} dead "
                        + $"at the end, {Stampede.DeadNeeded(size)} needed)",
                    Stampede.HeldTheLine(size, DeadAtFirstEntry, dead));

                List<string> harmed = fallen.ToList();
                foreach (KeyValuePair<Pawn, HashSet<Hediff>> was in injuries)
                {
                    Pawn p = was.Key;
                    int fresh = Injuries(p).Count(h => !was.Value.Contains(h));
                    if (fresh > 0)
                    {
                        harmed.Add($"{p.LabelShort} ({fresh} new injuries)");
                    }
                }

                fact($"no colonist harmed ([{string.Join(", ", harmed.Distinct())}])",
                    harmed.Count == 0);
                List<string> lost = stage.Where(t => !Standing(t))
                    .Select(t => t.LabelShort).ToList();
                fact($"the stage still stands (lost [{string.Join(", ", lost)}])",
                    lost.Count == 0);

                // Measured while the scene ran: End puts every fire out, so
                // a count after it shows only that the clean-up ran.
                fact($"no fire reached the base (most {MostFiresNearBase} "
                        + "inside the keep-away"
                        + (FirstFireNearBase != null
                            ? $", first {FirstFireNearBase}" : string.Empty)
                        + ")",
                    MostFiresNearBase == 0);
                int fires = map?.listerThings.ThingsOfDef(ThingDefOf.Fire).Count ?? 0;
                fact($"no fire left after the scene's clean-up ({fires})", fires == 0);
                fact("no singularity grenade during the stampede ("
                        + (SingularitySeen ?? "none seen") + ")",
                    SingularitySeen == null);
                fact("the drafted colonists held their fire ("
                        + $"[{string.Join(", ", firingAtWill)}] on Fire At Will)",
                    firingAtWill.Count == 0);
                Log.Message($"[BT-SMOKE] NOTE stampede fires: most {MostFires} "
                    + $"on the map at once, {MostFiresNearBase} near the base; "
                    + $"{Scene.VolleysRefused} volleys refused for safety");
            }

            /// <summary>
            /// The spawned fires inside the base's keep-away now.
            /// </summary>
            /// <returns>The fires.</returns>
            private IEnumerable<Thing> FiresNearBase() =>
                (map?.listerThings.ThingsOfDef(ThingDefOf.Fire)
                    ?? new List<Thing>())
                .Where(f => f != null && f.Spawned
                    && Stampede.InsideKeepAway(baseCentre, AnyRim.ToCell(f.Position)));

            /// <summary>
            /// Counts the fires this tick: on the map, and new ones inside
            /// the base's keep-away, keeping the most of each and where the
            /// first near the base was.
            /// </summary>
            private void SampleFires()
            {
                if (map == null)
                {
                    return;
                }

                MostFires = Mathf.Max(MostFires,
                    map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count);
                List<Thing> near = FiresNearBase()
                    .Where(f => !firesBefore.Contains(f.thingIDNumber)).ToList();
                if (near.Count > MostFiresNearBase)
                {
                    MostFiresNearBase = near.Count;
                }

                if (near.Count > 0 && FirstFireNearBase == null)
                {
                    FirstFireNearBase = $"at {near[0].Position}, scene tick {sampled}";
                }
            }

            /// <summary>
            /// Notes a singularity grenade in the air or its field on the
            /// map: the grenadier stands drafted in the base during the
            /// stampede and must not throw at the herd.
            /// </summary>
            private void SampleSingularity()
            {
                if (map == null || SingularitySeen != null)
                {
                    return;
                }

                if (map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile)
                    .Any(t => t.def.defName == Showcase.GrenadeProjectileDef))
                {
                    SingularitySeen = $"a grenade in the air at scene tick {sampled}";
                }
                else if (map.listerThings
                    .ThingsOfDef(BT_SingularityDefOf.BT_SingularityField).Count > 0)
                {
                    SingularitySeen = $"a singularity field at scene tick {sampled}";
                }
            }

            /// <summary>A pawn's injuries and lost parts now.</summary>
            /// <param name="p">The pawn.</param>
            /// <returns>The hediffs.</returns>
            private static HashSet<Hediff> Injuries(Pawn p) =>
                new HashSet<Hediff>(p?.health?.hediffSet?.hediffs?
                    .Where(h => h is Hediff_Injury || h is Hediff_MissingPart)
                    ?? Enumerable.Empty<Hediff>());
        }

        /// <summary>
        /// How fast one scene ran in a verify run: game ticks against real
        /// time and frames, and the longest real gap between two ticks
        /// (one tick's work, plus a frame's when one fell between them).
        /// </summary>
        private sealed class SceneClock
        {
            /// <summary>Real time since the scene began.</summary>
            private readonly Stopwatch watch = Stopwatch.StartNew();

            /// <summary>The game tick the scene began.</summary>
            private readonly int startTick;

            /// <summary>The frame count as the scene began.</summary>
            private readonly int startFrame;

            /// <summary>Real milliseconds at the last tick seen.</summary>
            private double lastMs;

            /// <summary>
            /// The longest gap between ticks, in milliseconds.
            /// </summary>
            private double worstMs;

            /// <summary>The scene tick the longest gap ended on.</summary>
            private int worstAt;

            /// <summary>Ticks noted so far.</summary>
            private int ticked;

            /// <summary>Starts timing a scene.</summary>
            /// <param name="key">The scene's key.</param>
            /// <param name="tick">The current game tick.</param>
            public SceneClock(string key, int tick)
            {
                Key = key;
                startTick = tick;
                startFrame = Time.frameCount;
            }

            /// <summary>Gets the scene's key.</summary>
            public string Key { get; }

            /// <summary>Notes one more tick.</summary>
            public void Tick()
            {
                double ms = watch.Elapsed.TotalMilliseconds;
                ticked++;
                if (ms - lastMs > worstMs)
                {
                    worstMs = ms - lastMs;
                    worstAt = ticked;
                }

                lastMs = ms;
            }

            /// <summary>The clock's note for the log.</summary>
            /// <param name="tick">The current game tick.</param>
            /// <returns>The note's text.</returns>
            public string Note(int tick)
            {
                double seconds =
                    System.Math.Max(1e-3, watch.Elapsed.TotalSeconds);
                int ticks = tick - startTick;
                int frames = Time.frameCount - startFrame;
                double msATick = 1000.0 * seconds / System.Math.Max(1, ticks);
                return $"scene clock {Key}: {ticks} ticks in {seconds:0.0} s "
                    + $"({ticks / seconds:0} ticks/s, {msATick:0.00} ms a "
                    + $"tick), {frames} frames ({frames / seconds:0} fps), "
                    + $"longest tick gap {worstMs:0} ms at scene tick {worstAt}";
            }
        }
    }
}
