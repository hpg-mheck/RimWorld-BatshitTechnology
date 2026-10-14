using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>
/// Uniform random numbers for the flicker of noise in every pull
/// calculation. In the game it is RimWorld's <c>Rand</c>; in the tests, a
/// <see cref="SeededUnitRandom"/>.
/// </summary>
public interface IUnitRandom
{
    /// <summary>The next number, nominally in [0, 1].</summary>
    /// <returns>
    /// The number. Callers clamp it, so a source that may return 1 (as
    /// RimWorld's does) or stray outside is harmless.
    /// </returns>
    double NextUnit();
}

/// <summary>
/// A seedable <see cref="IUnitRandom"/> (SplitMix64, as
/// <see cref="SeededStream"/>): the same seed gives the same numbers under
/// .NET in the tests and under RimWorld's Mono runtime.
/// </summary>
public sealed class SeededUnitRandom : IUnitRandom
{
    /// <summary>One over two to the 53rd: a 53-bit integer to [0, 1).</summary>
    private const double Inverse53 = 1.0 / 9007199254740992.0;

    private readonly SeededStream stream;

    /// <summary>Starts a stream.</summary>
    /// <param name="seed">The seed; any value, negative included.</param>
    public SeededUnitRandom(int seed)
    {
        stream = new SeededStream(seed);
    }

    /// <summary>The next number, uniform in [0, 1).</summary>
    /// <returns>The number.</returns>
    public double NextUnit() => (stream.NextBits() >> 11) * Inverse53;
}

/// <summary>
/// One pull acting on a thing, as superposition sees it: where its centre
/// is, how hard it drags right now, and the seed that tells it apart for
/// every thing's persistent bias.
/// </summary>
public readonly struct PullSource
{
    /// <summary>Describes a pull.</summary>
    /// <param name="centerX">Its centre's x.</param>
    /// <param name="centerZ">Its centre's z.</param>
    /// <param name="rate">
    /// Its current drag rate, in cells per second; negative or NaN counts
    /// as 0, and more than <see cref="ImplosionSettings.MaxRate"/> as that.
    /// </param>
    /// <param name="seed">
    /// Its seed, drawn once per implosion: what each thing's persistent
    /// bias toward it is hashed from.
    /// </param>
    public PullSource(int centerX, int centerZ, double rate, int seed)
    {
        CenterX = centerX;
        CenterZ = centerZ;
        Rate = rate;
        Seed = seed;
    }

    /// <summary>Its centre's x.</summary>
    public int CenterX { get; }

    /// <summary>Its centre's z.</summary>
    public int CenterZ { get; }

    /// <summary>Its current drag rate, in cells per second, as given.</summary>
    public double Rate { get; }

    /// <summary>Its seed, for each thing's persistent bias.</summary>
    public int Seed { get; }
}

/// <summary>
/// A small deviation in one pull calculation for one thing: its direction
/// turned by a few degrees and its strength changed by a few per cent.
/// </summary>
public readonly struct PullJitter
{
    /// <summary>No deviation.</summary>
    public static readonly PullJitter None = new PullJitter(0, 0);

    /// <summary>Describes a deviation.</summary>
    /// <param name="degrees">
    /// How far the pull's direction is turned, anticlockwise, in degrees.
    /// </param>
    /// <param name="scale">
    /// The fraction its strength changes by: 0.02 is two per cent more.
    /// </param>
    public PullJitter(double degrees, double scale)
    {
        Degrees = degrees;
        Scale = scale;
    }

    /// <summary>How far the direction is turned, in degrees.</summary>
    public double Degrees { get; }

    /// <summary>The fraction the strength changes by.</summary>
    public double Scale { get; }

    /// <summary>Both deviations at once: a bias with a flicker on top.</summary>
    /// <param name="other">The other deviation.</param>
    /// <returns>The sum.</returns>
    public PullJitter Plus(PullJitter other) =>
        new PullJitter(Degrees + other.Degrees, Scale + other.Scale);
}

/// <summary>
/// Several implosions at once (ROADMAP.md, "Multi-bomb physics"): every
/// caught thing feels the sum of every pull that reaches it, a direction
/// toward each centre scaled by that pull's current rate, and steps one
/// cell along the net direction at the net rate. Every pull calculation
/// carries a little random bias, so nothing stalls cleanly between equal
/// pulls. One pull alone behaves exactly as before: things only ever
/// approach its centre, at its own rate. Kept free of RimWorld types so it
/// can be unit tested.
/// </summary>
/// <remarks>
/// <para>
/// The pulls are superposed as a potential: a thing at q has
/// U(q) = sum of w_i |q - c_i|, where w_i is pull i's rate with the thing's
/// jitter applied. Its net pull, the sum of w_i times the unit direction
/// toward c_i, is the downhill slope of U, and its net rate is that
/// slope's size. A step is allowed only if it takes the thing strictly
/// downhill, which for one pull alone means strictly closer to its centre
/// (whatever the jitter), and which no cycle can keep doing for ever. The
/// allowed steps are tried in order of how well they line up with the net
/// direction, each pull's turned by its jitter, so the best is the net
/// direction rounded to the nearest of the eight grid steps.
/// </para>
/// <para>
/// The jitter is a persistent bias per thing per pull, with a flicker of
/// fresh noise for every step. The bias's strength part ranks the pulls a
/// thing feels in a fixed order of its own, <see cref="BiasRankGap"/>
/// apart, so that between two equal pulls one always wins: the flicker,
/// at most <see cref="MaxFlickerScale"/> either way, can never overturn
/// it. Between equal pulls the whole line joining their centres is a
/// balance line (their tugs cancel along it), and a thing on it creeps
/// toward the pull it favours at the few per cent of their rate that the
/// bias gives it.
/// </para>
/// </remarks>
public static class PullSuperposition
{
    /// <summary>
    /// The largest persistent turn of a pull's direction, in degrees.
    /// </summary>
    public const double MaxBiasDegrees = 6;

    /// <summary>
    /// The strength gap between neighbours in a thing's preference order
    /// among the pulls it feels: with two, one is two per cent stronger
    /// and the other two per cent weaker.
    /// </summary>
    public const double BiasRankGap = 0.04;

    /// <summary>
    /// The largest persistent change of a pull's strength, however many
    /// pulls a thing feels. Up to five pulls the order's gaps all hold;
    /// with more, its ends are clamped to this.
    /// </summary>
    public const double MaxBiasScale = 0.08;

    /// <summary>The largest flicker of a direction, in degrees.</summary>
    public const double MaxFlickerDegrees = 3;

    /// <summary>
    /// The largest flicker of a strength: small enough that two flickers
    /// together cannot close <see cref="BiasRankGap"/>.
    /// </summary>
    public const double MaxFlickerScale = 0.01;

    /// <summary>
    /// The largest turn of a direction, bias and flicker together. Under
    /// 22.5 degrees, half the angle between grid steps, so the best step
    /// for one pull always shortens the larger of the thing's offsets from
    /// its centre, and one pull gathers everything in as many steps as it
    /// ever did.
    /// </summary>
    public const double MaxDegrees = MaxBiasDegrees + MaxFlickerDegrees;

    /// <summary>
    /// The largest change of a strength, bias and flicker together.
    /// </summary>
    public const double MaxScale = MaxBiasScale + MaxFlickerScale;

    /// <summary>
    /// The largest turn applied, in degrees, whatever a caller passes.
    /// </summary>
    private const double TurnLimit = 20;

    /// <summary>
    /// The largest strength change applied, whatever a caller passes.
    /// </summary>
    private const double ScaleLimit = 0.5;

    /// <summary>
    /// The eight neighbouring steps, in the order that breaks ties between
    /// equally good steps: clockwise from north.
    /// </summary>
    private static readonly (int Dx, int Dz)[] Neighbours =
    {
        (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1),
    };

    /// <summary>
    /// The largest strength change for a thing that feels so many pulls:
    /// its bias's half-spread of ranks plus a flicker. What a check of
    /// "never against the net pull" must allow for.
    /// </summary>
    /// <param name="pullCount">How many pulls it feels.</param>
    /// <returns>The fraction; 0 for one pull or none.</returns>
    public static double MaxScaleFor(int pullCount)
    {
        if (pullCount < 2)
        {
            return 0;
        }

        double bias = Math.Min(
            MaxBiasScale, BiasRankGap * (pullCount - 1) / 2.0);
        return bias + MaxFlickerScale;
    }

    /// <summary>
    /// A thing's persistent bias toward each pull it feels: a turn hashed
    /// from its id and the pull's seed, and a strength set by where the
    /// pull falls in the thing's own preference order (also hashed), so
    /// the same thing always favours the same pull. One pull alone gets
    /// no strength change.
    /// </summary>
    /// <param name="thingId">The thing's id number.</param>
    /// <param name="pulls">The pulls it feels.</param>
    /// <returns>One bias per pull, in the same order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pulls"/> is null.
    /// </exception>
    public static PullJitter[] Biases(
        int thingId, IReadOnlyList<PullSource> pulls)
    {
        if (pulls == null)
        {
            throw new ArgumentNullException(nameof(pulls));
        }

        var biases = new PullJitter[pulls.Count];
        Biases(thingId, pulls, biases);
        return biases;
    }

    /// <summary>
    /// <see cref="Biases(int, IReadOnlyList{PullSource})"/> into an array
    /// the caller owns, so that a tick of drag allocates nothing.
    /// </summary>
    /// <param name="thingId">The thing's id number.</param>
    /// <param name="pulls">The pulls it feels.</param>
    /// <param name="into">
    /// Receives one bias per pull; at least as long as
    /// <paramref name="pulls"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either list is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="into"/> is too short.
    /// </exception>
    public static void Biases(
        int thingId, IReadOnlyList<PullSource> pulls, PullJitter[] into)
    {
        if (pulls == null)
        {
            throw new ArgumentNullException(nameof(pulls));
        }

        if (into == null)
        {
            throw new ArgumentNullException(nameof(into));
        }

        if (into.Length < pulls.Count)
        {
            throw new ArgumentException(
                "There must be room for a bias per pull.", nameof(into));
        }

        int n = pulls.Count;
        double middle = (n - 1) / 2.0;
        for (int i = 0; i < n; i++)
        {
            uint key = Hash(thingId, pulls[i].Seed, 0x2545F491u);
            int rank = 0;
            for (int j = 0; j < n; j++)
            {
                uint other = Hash(thingId, pulls[j].Seed, 0x2545F491u);
                // Ties (a hash collision) go by seed, never by list order.
                if (other < key
                    || (other == key && pulls[j].Seed < pulls[i].Seed))
                {
                    rank++;
                }
            }

            double scale = Clamp(
                BiasRankGap * (rank - middle), -MaxBiasScale, MaxBiasScale);
            double turn = Hash(thingId, pulls[i].Seed, 0x9E3779B9u)
                / 4294967296.0;
            into[i] = new PullJitter(
                MaxBiasDegrees * ((2 * turn) - 1), scale);
        }
    }

    /// <summary>
    /// A fresh flicker of noise for one pull calculation: up to
    /// <see cref="MaxFlickerDegrees"/> and <see cref="MaxFlickerScale"/>
    /// either way.
    /// </summary>
    /// <param name="random">Where the noise comes from.</param>
    /// <returns>The flicker.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="random"/> is null.
    /// </exception>
    public static PullJitter Flicker(IUnitRandom random)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        double turn = Unit(random.NextUnit());
        double scale = Unit(random.NextUnit());
        return new PullJitter(
            MaxFlickerDegrees * ((2 * turn) - 1),
            MaxFlickerScale * ((2 * scale) - 1));
    }

    /// <summary>
    /// The net pull on a thing with no jitter: the sum of each pull's rate
    /// times the unit direction toward its centre. A pull whose centre the
    /// thing is on adds nothing.
    /// </summary>
    /// <param name="x">The thing's x.</param>
    /// <param name="z">The thing's z.</param>
    /// <param name="pulls">The pulls it feels.</param>
    /// <returns>The net pull, in cells per second along x and z.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pulls"/> is null.
    /// </exception>
    public static (double X, double Z) NetPull(
        int x, int z, IReadOnlyList<PullSource> pulls)
    {
        Check(pulls, null);
        return Direction(x, z, pulls, null, turn: false);
    }

    /// <summary>
    /// How fast a thing is dragged: one pull alone drags at exactly its own
    /// rate, jitter or not (nothing at its centre); several drag at the
    /// size of their jittered net pull, at most
    /// <see cref="ImplosionSettings.MaxRate"/>. Between equal pulls that is
    /// only the few per cent the thing's bias tips the balance by.
    /// </summary>
    /// <param name="x">The thing's x.</param>
    /// <param name="z">The thing's z.</param>
    /// <param name="pulls">The pulls it feels.</param>
    /// <param name="jitter">
    /// One deviation per pull, or null for none.
    /// </param>
    /// <returns>Cells per second.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pulls"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="jitter"/> is given but not one per pull.
    /// </exception>
    public static double NetRate(
        int x, int z, IReadOnlyList<PullSource> pulls,
        IReadOnlyList<PullJitter>? jitter)
    {
        Check(pulls, jitter);
        if (pulls.Count == 1)
        {
            PullSource lone = pulls[0];
            return lone.CenterX == x && lone.CenterZ == z
                ? 0
                : SafeRate(lone.Rate);
        }

        var (nx, nz) = Direction(x, z, pulls, jitter, turn: false);
        return Math.Min(
            ImplosionSettings.MaxRate, Math.Sqrt((nx * nx) + (nz * nz)));
    }

    /// <summary>
    /// The steps a thing may take, best first: every neighbouring step
    /// that takes it strictly downhill in the pulls' jittered potential,
    /// in order of how well it lines up with the jittered net direction
    /// (ties clockwise from north). For one pull alone these are exactly
    /// the steps strictly closer to its centre, whatever the jitter. None
    /// where nothing is downhill: at a lone pull's centre, or exactly
    /// balanced between equal pulls with no jitter.
    /// </summary>
    /// <param name="x">The thing's x.</param>
    /// <param name="z">The thing's z.</param>
    /// <param name="pulls">The pulls it feels.</param>
    /// <param name="jitter">
    /// One deviation per pull (a bias and a flicker), or null for none.
    /// </param>
    /// <returns>(dx, dz) steps, best first.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pulls"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="jitter"/> is given but not one per pull.
    /// </exception>
    public static IReadOnlyList<(int Dx, int Dz)> Steps(
        int x, int z, IReadOnlyList<PullSource> pulls,
        IReadOnlyList<PullJitter>? jitter)
    {
        Check(pulls, jitter);
        var steps = new List<((int Dx, int Dz) Step, double Fit)>(8);
        if (pulls.Count == 0)
        {
            return new List<(int Dx, int Dz)>();
        }

        var (fx, fz) = Direction(x, z, pulls, jitter, turn: true);
        double size = Math.Sqrt((fx * fx) + (fz * fz));
        double here = Potential(x, z, pulls, jitter);
        double slack = 1e-9 * Math.Max(1, here);
        foreach (var (dx, dz) in Neighbours)
        {
            bool downhill = pulls.Count == 1
                ? Closer(x, z, dx, dz, pulls[0])
                : Potential(x + dx, z + dz, pulls, jitter) < here - slack;
            if (!downhill)
            {
                continue;
            }

            double fit = size > 0
                ? ((dx * fx) + (dz * fz))
                    / (Math.Sqrt((dx * dx) + (dz * dz)) * size)
                : 0;
            steps.Add(((dx, dz), fit));
        }

        // OrderByDescending is stable: equal fits stay clockwise.
        return steps.OrderByDescending(s => s.Fit)
            .Select(s => s.Step)
            .ToList();
    }

    /// <summary>
    /// A direction rounded to the nearest of the eight grid steps; a tie
    /// between a straight and a diagonal step goes to the straight one.
    /// </summary>
    /// <param name="x">The direction's x.</param>
    /// <param name="z">The direction's z.</param>
    /// <returns>
    /// The step; (0, 0) for no direction, or one that is not a number.
    /// </returns>
    public static (int Dx, int Dz) ToGridStep(double x, double z)
    {
        if (double.IsNaN(x) || double.IsNaN(z)
            || double.IsInfinity(x) || double.IsInfinity(z)
            || (x == 0 && z == 0))
        {
            return (0, 0);
        }

        // Sectors of 45 degrees, anticlockwise from east; banker's
        // rounding sends an exact boundary to the even (straight) sector.
        int sector = (int)Math.Round(Math.Atan2(z, x) / (Math.PI / 4));
        switch (((sector % 8) + 8) % 8)
        {
            case 0: return (1, 0);
            case 1: return (1, 1);
            case 2: return (0, 1);
            case 3: return (-1, 1);
            case 4: return (-1, 0);
            case 5: return (-1, -1);
            case 6: return (0, -1);
            default: return (1, -1);
        }
    }

    /// <summary>
    /// How much a step changes a thing's potential under the pulls with no
    /// jitter: negative when it moves with the net pull, positive when it
    /// moves against it. For checks of what the game did.
    /// </summary>
    /// <param name="x">Where the thing was: x.</param>
    /// <param name="z">Where the thing was: z.</param>
    /// <param name="dx">The step's x.</param>
    /// <param name="dz">The step's z.</param>
    /// <param name="pulls">The pulls it felt.</param>
    /// <returns>The change, in cells per second times cells.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pulls"/> is null.
    /// </exception>
    public static double PotentialChange(
        int x, int z, int dx, int dz, IReadOnlyList<PullSource> pulls)
    {
        Check(pulls, null);
        return Potential(x + dx, z + dz, pulls, null)
            - Potential(x, z, pulls, null);
    }

    /// <summary>
    /// A thing's potential: each pull's jittered strength times its
    /// distance from that pull's centre, summed.
    /// </summary>
    /// <param name="x">The thing's x.</param>
    /// <param name="z">The thing's z.</param>
    /// <param name="pulls">The pulls.</param>
    /// <param name="jitter">One deviation per pull, or null.</param>
    /// <returns>The potential.</returns>
    private static double Potential(
        int x, int z, IReadOnlyList<PullSource> pulls,
        IReadOnlyList<PullJitter>? jitter)
    {
        double sum = 0;
        for (int i = 0; i < pulls.Count; i++)
        {
            double ox = (double)x - pulls[i].CenterX;
            double oz = (double)z - pulls[i].CenterZ;
            sum += Weight(pulls, jitter, i)
                * Math.Sqrt((ox * ox) + (oz * oz));
        }

        return sum;
    }

    /// <summary>
    /// The net pull: each pull's jittered strength times its unit
    /// direction toward its centre, turned by its jitter if asked, summed.
    /// </summary>
    /// <param name="x">The thing's x.</param>
    /// <param name="z">The thing's z.</param>
    /// <param name="pulls">The pulls.</param>
    /// <param name="jitter">One deviation per pull, or null.</param>
    /// <param name="turn">Whether to turn each direction.</param>
    /// <returns>The net pull's x and z.</returns>
    private static (double X, double Z) Direction(
        int x, int z, IReadOnlyList<PullSource> pulls,
        IReadOnlyList<PullJitter>? jitter, bool turn)
    {
        double sx = 0;
        double sz = 0;
        for (int i = 0; i < pulls.Count; i++)
        {
            double ux = (double)pulls[i].CenterX - x;
            double uz = (double)pulls[i].CenterZ - z;
            double length = Math.Sqrt((ux * ux) + (uz * uz));
            if (length == 0)
            {
                continue; // on its centre: no direction
            }

            double w = Weight(pulls, jitter, i) / length;
            ux *= w;
            uz *= w;
            if (turn && jitter != null)
            {
                double radians = Clamp(
                    Finite(jitter[i].Degrees), -TurnLimit, TurnLimit)
                    * Math.PI / 180;
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);
                (ux, uz) = ((ux * cos) - (uz * sin), (ux * sin) + (uz * cos));
            }

            sx += ux;
            sz += uz;
        }

        return (sx, sz);
    }

    /// <summary>A pull's strength with a thing's jitter applied.</summary>
    /// <param name="pulls">The pulls.</param>
    /// <param name="jitter">One deviation per pull, or null.</param>
    /// <param name="i">Which pull.</param>
    /// <returns>Its strength, never negative.</returns>
    private static double Weight(
        IReadOnlyList<PullSource> pulls, IReadOnlyList<PullJitter>? jitter,
        int i)
    {
        double scale = jitter == null
            ? 0
            : Clamp(Finite(jitter[i].Scale), -ScaleLimit, ScaleLimit);
        return SafeRate(pulls[i].Rate) * (1 + scale);
    }

    /// <summary>
    /// Whether a step takes a thing strictly closer to one pull's centre,
    /// in whole-number arithmetic so that no rounding can say yes wrongly.
    /// </summary>
    /// <param name="x">The thing's x.</param>
    /// <param name="z">The thing's z.</param>
    /// <param name="dx">The step's x.</param>
    /// <param name="dz">The step's z.</param>
    /// <param name="pull">The pull.</param>
    /// <returns>True if it does.</returns>
    private static bool Closer(int x, int z, int dx, int dz, PullSource pull)
    {
        long ox = (long)x - pull.CenterX;
        long oz = (long)z - pull.CenterZ;
        long nx = ox + dx;
        long nz = oz + dz;
        return (nx * nx) + (nz * nz) < (ox * ox) + (oz * oz);
    }

    /// <summary>A rate made safe: within [0, the fastest drag].</summary>
    /// <param name="rate">The rate given.</param>
    /// <returns>0 for negative or NaN; at most the fastest drag.</returns>
    private static double SafeRate(double rate) =>
        double.IsNaN(rate) || rate < 0 ? 0
            : rate > ImplosionSettings.MaxRate ? ImplosionSettings.MaxRate
            : rate;

    /// <summary>A number, or 0 if it is not a finite one.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value, or 0.</returns>
    private static double Finite(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;

    /// <summary>A uniform number brought into [0, 1]; NaN is 0.5.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The clamped value.</returns>
    private static double Unit(double value) =>
        double.IsNaN(value) ? 0.5 : Clamp(value, 0, 1);

    /// <summary>Clamps a value to a range.</summary>
    /// <param name="value">The value, a number.</param>
    /// <param name="min">The minimum.</param>
    /// <param name="max">The maximum, at least the minimum.</param>
    /// <returns>The clamped value.</returns>
    private static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;

    /// <summary>Rejects missing pulls and mismatched jitter.</summary>
    /// <param name="pulls">The pulls.</param>
    /// <param name="jitter">One deviation per pull, or null.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pulls"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="jitter"/> is given but not one per pull.
    /// </exception>
    private static void Check(
        IReadOnlyList<PullSource> pulls, IReadOnlyList<PullJitter>? jitter)
    {
        if (pulls == null)
        {
            throw new ArgumentNullException(nameof(pulls));
        }

        if (jitter != null && jitter.Count < pulls.Count)
        {
            throw new ArgumentException(
                "There must be a deviation per pull.", nameof(jitter));
        }
    }

    /// <summary>
    /// A 32-bit hash of a thing's id, a pull's seed and a salt, every
    /// input bit spread over the result (MurmurHash3's finaliser, as
    /// <see cref="TractorBombHostiles"/> uses).
    /// </summary>
    /// <param name="thingId">The thing's id number.</param>
    /// <param name="seed">The pull's seed.</param>
    /// <param name="salt">Which hash, of several per pair.</param>
    /// <returns>The hash.</returns>
    private static uint Hash(int thingId, int seed, uint salt)
    {
        unchecked
        {
            uint h = Mix((uint)seed ^ salt);
            h = Mix(h ^ ((uint)thingId * 0x85EBCA77u));
            return Mix(h + 0x27D4EB2Fu);
        }
    }

    /// <summary>MurmurHash3's 32-bit finaliser.</summary>
    /// <param name="h">The value.</param>
    /// <returns>The mixed value.</returns>
    private static uint Mix(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }
}
