using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the multi-bomb physics in <see cref="PullSuperposition"/>
/// (ROADMAP.md, "Multi-bomb physics"): one jittered pull alone never moves
/// a thing away from its centre and gathers it as fast as ever; equal
/// pulls cancel at their midpoint without jitter, and with it a thing
/// there tips off at once; net directions round to grid steps; and the
/// jitter is bounded and seedable.
/// </summary>
public sealed class PullSuperpositionTests
{
    /// <summary>
    /// A drag rate at the end of a pull, in cells per second.
    /// </summary>
    private const double EndRate = 12;

    /// <summary>
    /// One pull with a thing's persistent bias and a fresh flicker, over
    /// many seeds and every position in a 25 by 25 window round its
    /// centre: every step offered takes the thing strictly closer to the
    /// centre, there is always one, the best shortens the larger of its
    /// offsets (so it is gathered in as many steps as ever), and it drags
    /// at exactly the pull's rate.
    /// </summary>
    [Fact(DisplayName = "One jittered pull never moves a thing away")]
    public void Lone_jittered_pull_should_never_move_a_thing_away()
    {
        var rng = new SeededUnitRandom(20261013);
        for (int seed = 1; seed <= 300; seed++)
        {
            int cx = (int)(rng.NextUnit() * 200) - 100;
            int cz = (int)(rng.NextUnit() * 200) - 100;
            double rate = 0.6 + (rng.NextUnit() * 59.4);
            var pulls = new[] { new PullSource(cx, cz, rate, seed * 7919) };
            for (int ox = -12; ox <= 12; ox++)
            {
                for (int oz = -12; oz <= 12; oz++)
                {
                    if (ox == 0 && oz == 0)
                    {
                        continue;
                    }

                    int thing = (seed * 1000) + ((ox + 12) * 25) + oz + 12;
                    PullJitter[] jitter = Jitter(thing, pulls, rng);
                    var steps = PullSuperposition.Steps(
                        cx + ox, cz + oz, pulls, jitter);
                    Assert.NotEmpty(steps);
                    long before = ((long)ox * ox) + ((long)oz * oz);
                    Assert.All(steps, s => Assert.True(
                        Squared(ox + s.Dx, oz + s.Dz) < before,
                        $"seed {seed}: ({ox}, {oz}) stepped {s} away"));
                    var best = steps[0];
                    Assert.Equal(
                        Chebyshev(ox, oz) - 1,
                        Chebyshev(ox + best.Dx, oz + best.Dz));
                    Assert.Equal(rate, PullSuperposition.NetRate(
                        cx + ox, cz + oz, pulls, jitter));
                }
            }
        }
    }

    /// <summary>
    /// One jittered pull, nothing in the way: a thing anywhere within ten
    /// tiles reaches the centre in exactly as many steps as its larger
    /// offset, which is how many the pull always took. Only the cell path
    /// may differ.
    /// </summary>
    [Fact(DisplayName = "One jittered pull gathers all in the same time")]
    public void Lone_jittered_pull_should_gather_in_the_same_steps()
    {
        var rng = new SeededUnitRandom(7);
        var pulls = new[] { new PullSource(0, 0, EndRate, 4242) };
        for (int x = -10; x <= 10; x++)
        {
            for (int z = -10; z <= 10; z++)
            {
                if ((x * x) + (z * z) > 100 || (x == 0 && z == 0))
                {
                    continue;
                }

                int thing = ((x + 10) * 21) + z + 10;
                PullJitter[] bias = PullSuperposition.Biases(thing, pulls);
                int px = x;
                int pz = z;
                int taken = 0;
                while ((px != 0 || pz != 0) && taken < 50)
                {
                    var flicker = new[]
                    {
                        bias[0].Plus(PullSuperposition.Flicker(rng)),
                    };
                    var step = PullSuperposition.Steps(
                        px, pz, pulls, flicker)[0];
                    px += step.Dx;
                    pz += step.Dz;
                    taken++;
                }

                Assert.Equal(Chebyshev(x, z), taken);
            }
        }
    }

    /// <summary>
    /// Without jitter, two equal pulls cancel at their midpoint: no net
    /// rate and no step. Their tugs cancel all along the line between
    /// their centres, not only at the midpoint, so a balance line it is.
    /// </summary>
    /// <param name="x">Where on the line between the centres.</param>
    [Theory(DisplayName = "Two equal pulls cancel at their midpoint")]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-6)]
    public void Equal_pulls_should_cancel_without_jitter(int x)
    {
        PullSource[] pulls = EqualPulls();
        Assert.Equal(0d, PullSuperposition.NetRate(x, 0, pulls, null));
        Assert.Empty(PullSuperposition.Steps(x, 0, pulls, null));
        var (nx, nz) = PullSuperposition.NetPull(x, 0, pulls);
        Assert.Equal(0d, nx);
        Assert.Equal(0d, nz);
    }

    /// <summary>
    /// With jitter, a thing at the midpoint between two equal pulls tips
    /// off on its first step and keeps going the same way, toward the pull
    /// its persistent bias favours, over many things and seeds: the
    /// flicker on top can never overturn the bias.
    /// </summary>
    [Fact(DisplayName = "With jitter a thing at the midpoint tips off")]
    public void Jitter_should_tip_a_thing_off_the_midpoint()
    {
        PullSource[] pulls = EqualPulls();
        var tipped = new Dictionary<int, int>();
        for (int thing = 1; thing <= 500; thing++)
        {
            var rng = new SeededUnitRandom(thing * 31);
            PullJitter[] bias = PullSuperposition.Biases(thing, pulls);
            int favourite = bias[0].Scale > bias[1].Scale ? -1 : 1;
            Assert.True(PullSuperposition.NetRate(0, 0, pulls, bias) > 0);
            int px = 0;
            int pz = 0;
            for (int step = 1; step <= 5; step++)
            {
                var steps = PullSuperposition.Steps(
                    px, pz, pulls, Jitter(bias, rng));
                Assert.NotEmpty(steps);
                px += steps[0].Dx;
                pz += steps[0].Dz;
                Assert.Equal(favourite * step, px);
                Assert.Equal(0, pz);
            }

            tipped[favourite] = tipped.TryGetValue(favourite, out int n)
                ? n + 1
                : 1;
        }

        // Both ways, roughly evenly: the bias is random per thing.
        Assert.InRange(tipped[-1], 200, 300);
        Assert.InRange(tipped[1], 200, 300);
    }

    /// <summary>
    /// Net directions round to the nearest grid step, ties to the straight
    /// step; no direction, or nonsense, rounds to none.
    /// </summary>
    /// <param name="x">The direction's x.</param>
    /// <param name="z">The direction's z.</param>
    /// <param name="dx">The step's x.</param>
    /// <param name="dz">The step's z.</param>
    [Theory(DisplayName = "Net directions round to grid steps")]
    [InlineData(1, 0, 1, 0)]
    [InlineData(1, 0.3, 1, 0)]
    [InlineData(1, 0.5, 1, 1)]
    [InlineData(-3, -3, -1, -1)]
    [InlineData(0.2, -5, 0, -1)]
    [InlineData(-5, 0.1, -1, 0)]
    [InlineData(-5, -0.1, -1, 0)]
    [InlineData(-2, 4.9, 0, 1)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(double.NaN, 1, 0, 0)]
    [InlineData(double.PositiveInfinity, 1, 0, 0)]
    public void ToGridStep_should_round_to_the_nearest_step(
        double x, double z, int dx, int dz)
    {
        Assert.Equal((dx, dz), PullSuperposition.ToGridStep(x, z));
    }

    /// <summary>
    /// The best step is the net direction rounded to the grid: for one
    /// pull without jitter, everywhere within twenty tiles, and for two
    /// superposed pulls wherever that rounded step is downhill.
    /// </summary>
    [Fact(DisplayName = "The best step is the net direction on the grid")]
    public void Best_step_should_be_the_rounded_net_direction()
    {
        var lone = new[] { new PullSource(0, 0, EndRate, 1) };
        PullSource[] two =
        {
            new PullSource(-7, 2, EndRate, 1), new PullSource(6, -3, 5, 2),
        };
        foreach (PullSource[] pulls in new[] { lone, two })
        {
            for (int x = -20; x <= 20; x++)
            {
                for (int z = -20; z <= 20; z++)
                {
                    var steps = PullSuperposition.Steps(x, z, pulls, null);
                    var (nx, nz) = PullSuperposition.NetPull(x, z, pulls);
                    var rounded = PullSuperposition.ToGridStep(nx, nz);
                    if (steps.Contains(rounded))
                    {
                        Assert.Equal(rounded, steps[0]);
                    }
                    else
                    {
                        Assert.True(pulls.Length > 1 || steps.Count == 0);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Opposed pulls of unequal strength: at the midpoint the thing goes to
    /// the stronger, at the difference of their rates.
    /// </summary>
    [Fact(DisplayName = "Unequal opposed pulls drag toward the stronger")]
    public void Unequal_pulls_should_drag_toward_the_stronger()
    {
        PullSource[] pulls =
        {
            new PullSource(-10, 0, EndRate, 1), new PullSource(10, 0, 6, 2),
        };
        Assert.Equal(6d, PullSuperposition.NetRate(0, 0, pulls, null), 9);
        Assert.Equal((-1, 0), PullSuperposition.Steps(0, 0, pulls, null)[0]);
    }

    /// <summary>
    /// Superposed pulls, over many random layouts of two or three pulls
    /// and many positions within their reach: every step offered is
    /// strictly downhill in the jittered potential; without jitter, every
    /// step moves with the net pull, never against it; and the net rate
    /// never exceeds the fastest drag.
    /// </summary>
    [Fact(DisplayName = "Superposed steps never go against the net pull")]
    public void Superposed_steps_should_go_with_the_net_pull()
    {
        var rng = new SeededUnitRandom(99);
        for (int layout = 0; layout < 300; layout++)
        {
            int count = 2 + (layout % 2);
            var pulls = Enumerable.Range(0, count)
                .Select(i => new PullSource(
                    (int)(rng.NextUnit() * 25) - 12,
                    (int)(rng.NextUnit() * 25) - 12,
                    rng.NextUnit() * 60, (layout * 10) + i))
                .ToArray();
            for (int probe = 0; probe < 40; probe++)
            {
                int x = (int)(rng.NextUnit() * 31) - 15;
                int z = (int)(rng.NextUnit() * 31) - 15;
                PullJitter[] jitter =
                    Jitter((layout * 100) + probe, pulls, rng);
                foreach (var (dx, dz) in PullSuperposition.Steps(
                    x, z, pulls, jitter))
                {
                    Assert.True(
                        JitteredChange(x, z, dx, dz, pulls, jitter) < 0);
                }

                foreach (var (dx, dz) in PullSuperposition.Steps(
                    x, z, pulls, null))
                {
                    Assert.True(PullSuperposition.PotentialChange(
                        x, z, dx, dz, pulls) < 0);
                }

                Assert.InRange(
                    PullSuperposition.NetRate(x, z, pulls, jitter),
                    0, ImplosionSettings.MaxRate);
            }
        }
    }

    /// <summary>
    /// The noise is seedable: the same seed gives the same numbers and
    /// flickers, another seed others, all in [0, 1); and a thing's bias
    /// depends only on its id and the pulls' seeds, not on when it is
    /// asked or the order the pulls are listed in.
    /// </summary>
    [Fact(DisplayName = "Pull jitter is seedable and persistent")]
    public void Jitter_should_be_seedable_and_persistent()
    {
        var a = new SeededUnitRandom(42);
        var b = new SeededUnitRandom(42);
        var c = new SeededUnitRandom(43);
        var first = Enumerable.Range(0, 1000).Select(_ => a.NextUnit())
            .ToList();
        Assert.Equal(first, Enumerable.Range(0, 1000)
            .Select(_ => b.NextUnit()));
        Assert.NotEqual(first, Enumerable.Range(0, 1000)
            .Select(_ => c.NextUnit()));
        Assert.All(first, u => Assert.InRange(u, 0, 0.9999999999));

        var f1 = new SeededUnitRandom(5);
        var f2 = new SeededUnitRandom(5);
        for (int i = 0; i < 100; i++)
        {
            PullJitter x = PullSuperposition.Flicker(f1);
            PullJitter y = PullSuperposition.Flicker(f2);
            Assert.Equal(x.Degrees, y.Degrees);
            Assert.Equal(x.Scale, y.Scale);
        }

        PullSource[] pulls = EqualPulls();
        PullJitter[] ab = PullSuperposition.Biases(17, pulls);
        PullJitter[] ba = PullSuperposition.Biases(
            17, new[] { pulls[1], pulls[0] });
        Assert.Equal(ab, PullSuperposition.Biases(17, pulls));
        Assert.Equal(ab[0], ba[1]);
        Assert.Equal(ab[1], ba[0]);
    }

    /// <summary>
    /// The jitter stays small: a bias of a few degrees and a few per cent,
    /// a flicker smaller still, the two together under half a grid step's
    /// angle, and two flickers never enough to close the gap between two
    /// pulls in a thing's preference order. One pull alone has no
    /// strength bias.
    /// </summary>
    [Fact(DisplayName = "Pull jitter stays within its bounds")]
    public void Jitter_should_stay_within_bounds()
    {
        Assert.True(PullSuperposition.MaxDegrees < 22.5);
        Assert.True(2 * PullSuperposition.MaxFlickerScale
            < PullSuperposition.BiasRankGap);
        var rng = new SeededUnitRandom(3);
        for (int i = 0; i < 2000; i++)
        {
            PullJitter f = PullSuperposition.Flicker(rng);
            Assert.InRange(Math.Abs(f.Degrees), 0,
                PullSuperposition.MaxFlickerDegrees);
            Assert.InRange(Math.Abs(f.Scale), 0,
                PullSuperposition.MaxFlickerScale);
        }

        for (int count = 1; count <= 6; count++)
        {
            var pulls = Enumerable.Range(0, count)
                .Select(i => new PullSource(i, 0, EndRate, i * 13))
                .ToArray();
            for (int thing = 0; thing < 200; thing++)
            {
                PullJitter[] bias = PullSuperposition.Biases(thing, pulls);
                Assert.All(bias, j =>
                {
                    Assert.InRange(Math.Abs(j.Degrees), 0,
                        PullSuperposition.MaxBiasDegrees);
                    Assert.InRange(Math.Abs(j.Scale), 0,
                        PullSuperposition.MaxBiasScale);
                    Assert.InRange(Math.Abs(j.Scale), 0,
                        PullSuperposition.MaxScaleFor(count));
                });
                if (count == 1)
                {
                    Assert.Equal(0d, bias[0].Scale);
                }
                else
                {
                    var scales = bias.Select(j => j.Scale)
                        .OrderBy(s => s).ToList();
                    // Up to five pulls the gap holds everywhere; beyond,
                    // the ends of the order are clamped.
                    for (int k = 1; k < scales.Count; k++)
                    {
                        bool clamped = Math.Abs(scales[k])
                            == PullSuperposition.MaxBiasScale
                            || Math.Abs(scales[k - 1])
                            == PullSuperposition.MaxBiasScale;
                        Assert.True(scales[k] - scales[k - 1]
                            >= PullSuperposition.BiasRankGap - 1e-12
                            || (count > 5 && clamped));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Opposing strength: none under one pull, or two pulling the same
    /// way; twice each rate for two equal pulls head on, anywhere on the
    /// line between them, a pull's own centre included (it holds the thing
    /// against the other); something in between off that line; and none
    /// where the other pull has no strength.
    /// </summary>
    [Fact(DisplayName = "Tension is the pull that cancels out")]
    public void Tension_should_be_the_cancelled_pull()
    {
        var lone = new[] { new PullSource(0, 0, EndRate, 1) };
        Assert.Equal(0d, PullSuperposition.Tension(7, -3, lone), 9);
        PullSource[] sameWay =
        {
            new PullSource(10, 0, EndRate, 1), new PullSource(20, 0, 6, 2),
        };
        Assert.Equal(0d, PullSuperposition.Tension(0, 0, sameWay), 9);
        PullSource[] headOn = EqualPulls();
        Assert.Equal(24d, PullSuperposition.Tension(0, 0, headOn), 9);
        Assert.Equal(24d, PullSuperposition.Tension(-4, 0, headOn), 9);
        double off = PullSuperposition.Tension(0, 6, headOn);
        Assert.InRange(off, 0.1, 23.9);
        Assert.Equal(24d, PullSuperposition.Tension(-10, 0, headOn), 9);
        Assert.Equal(0d, PullSuperposition.Tension(-10, 0,
            new[] { headOn[0], new PullSource(10, 0, 0, 2) }), 9);
    }

    /// <summary>
    /// Stretching damage: none up to the threshold, a point per six cells
    /// a second of opposing pull beyond it, rounded up, and never more
    /// than the cap; two Tractor Bombs head on at full pull do three.
    /// </summary>
    /// <param name="tension">The opposing strength.</param>
    /// <param name="damage">The damage expected.</param>
    [Theory(DisplayName = "Stretching damage scales with the opposing pull")]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(6, 0)]
    [InlineData(6.01, 1)]
    [InlineData(12, 1)]
    [InlineData(12.5, 2)]
    [InlineData(24, 3)]
    [InlineData(120, 4)]
    [InlineData(double.PositiveInfinity, 4)]
    public void StretchDamage_should_scale_with_tension(
        double tension, int damage)
    {
        Assert.Equal(damage, PullSuperposition.StretchDamage(tension));
    }

    /// <summary>
    /// Nonsense in: no pulls give no step and no rate; a rate that is not
    /// a number, or negative, counts as none; jitter that is not a number
    /// counts as none; a random source past 1 is clamped; missing input
    /// and a jitter list too short are programming errors.
    /// </summary>
    [Fact(DisplayName = "Pull superposition survives nonsense")]
    public void Should_survive_nonsense()
    {
        var none = Array.Empty<PullSource>();
        Assert.Empty(PullSuperposition.Steps(0, 0, none, null));
        Assert.Equal(0d, PullSuperposition.NetRate(0, 0, none, null));

        PullSource[] broken =
        {
            new PullSource(-5, 0, double.NaN, 1),
            new PullSource(5, 0, -3, 2),
        };
        Assert.Equal(0d, PullSuperposition.NetRate(0, 0, broken, null));
        Assert.Empty(PullSuperposition.Steps(0, 0, broken, null));

        var lone = new[] { new PullSource(9, 0, EndRate, 1) };
        var nan = new[] { new PullJitter(double.NaN, double.NaN) };
        Assert.Equal((1, 0), PullSuperposition.Steps(0, 0, lone, nan)[0]);

        PullJitter wild = PullSuperposition.Flicker(new Constant(7));
        Assert.Equal(PullSuperposition.MaxFlickerDegrees, wild.Degrees);

        Assert.Throws<ArgumentNullException>(
            () => PullSuperposition.Steps(0, 0, null!, null));
        Assert.Throws<ArgumentNullException>(
            () => PullSuperposition.Biases(0, null!));
        Assert.Throws<ArgumentNullException>(
            () => PullSuperposition.Flicker(null!));
        Assert.Throws<ArgumentException>(() => PullSuperposition.NetRate(
            0, 0, EqualPulls(), new[] { PullJitter.None }));
        Assert.Throws<ArgumentException>(() => PullSuperposition.Biases(
            0, EqualPulls(), new PullJitter[1]));
    }

    /// <summary>
    /// Two equal pulls twenty tiles apart, at the end rate.
    /// </summary>
    /// <returns>The pulls: west, then east.</returns>
    private static PullSource[] EqualPulls() => new[]
    {
        new PullSource(-10, 0, EndRate, 1001),
        new PullSource(10, 0, EndRate, 2002),
    };

    /// <summary>
    /// A thing's persistent bias toward each pull with a fresh flicker on
    /// top, as one drag step computes it.
    /// </summary>
    /// <param name="thing">The thing's id.</param>
    /// <param name="pulls">The pulls it feels.</param>
    /// <param name="rng">The flicker's source.</param>
    /// <returns>One deviation per pull.</returns>
    private static PullJitter[] Jitter(
        int thing, PullSource[] pulls, IUnitRandom rng) =>
        Jitter(PullSuperposition.Biases(thing, pulls), rng);

    /// <summary>Biases with a fresh flicker on top of each.</summary>
    /// <param name="bias">The persistent biases.</param>
    /// <param name="rng">The flicker's source.</param>
    /// <returns>One deviation per bias.</returns>
    private static PullJitter[] Jitter(PullJitter[] bias, IUnitRandom rng) =>
        bias.Select(b => b.Plus(PullSuperposition.Flicker(rng))).ToArray();

    /// <summary>
    /// How much a step changes the jittered potential: each pull's
    /// jittered strength times the change in distance from its centre.
    /// </summary>
    /// <param name="x">Where the thing was: x.</param>
    /// <param name="z">Where the thing was: z.</param>
    /// <param name="dx">The step's x.</param>
    /// <param name="dz">The step's z.</param>
    /// <param name="pulls">The pulls.</param>
    /// <param name="jitter">One deviation per pull.</param>
    /// <returns>The change.</returns>
    private static double JitteredChange(
        int x, int z, int dx, int dz, PullSource[] pulls,
        PullJitter[] jitter)
    {
        double sum = 0;
        for (int i = 0; i < pulls.Length; i++)
        {
            double w = pulls[i].Rate * (1 + jitter[i].Scale);
            sum += w * (Distance(x + dx, z + dz, pulls[i])
                - Distance(x, z, pulls[i]));
        }

        return sum;
    }

    /// <summary>A cell's distance from a pull's centre.</summary>
    /// <param name="x">The cell's x.</param>
    /// <param name="z">The cell's z.</param>
    /// <param name="pull">The pull.</param>
    /// <returns>The distance, in tiles.</returns>
    private static double Distance(int x, int z, PullSource pull) =>
        Math.Sqrt(Squared(x - pull.CenterX, z - pull.CenterZ));

    /// <summary>An offset's squared length.</summary>
    /// <param name="dx">The x offset.</param>
    /// <param name="dz">The z offset.</param>
    /// <returns>dx squared plus dz squared.</returns>
    private static long Squared(int dx, int dz) =>
        ((long)dx * dx) + ((long)dz * dz);

    /// <summary>
    /// An offset's Chebyshev length: the steps a king needs, which is how
    /// many drag steps a pull takes to bring a thing in.
    /// </summary>
    /// <param name="dx">The x offset.</param>
    /// <param name="dz">The z offset.</param>
    /// <returns>The larger of the two offsets' sizes.</returns>
    private static int Chebyshev(int dx, int dz) =>
        Math.Max(Math.Abs(dx), Math.Abs(dz));

    /// <summary>A random source that always says the same thing.</summary>
    private sealed class Constant : IUnitRandom
    {
        private readonly double value;

        /// <summary>Makes the source.</summary>
        /// <param name="value">What it always returns.</param>
        public Constant(double value)
        {
            this.value = value;
        }

        /// <summary>The constant.</summary>
        /// <returns>The value given.</returns>
        public double NextUnit() => value;
    }
}
