using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Showcases;

/// <summary>
/// The narration overlay's timing rules, in real seconds: people read in
/// real time, whatever the game's speed (docs/SHOWCASE-API.md, "The
/// Narration Overlay").
/// </summary>
public static class NarrationTiming
{
    /// <summary>How long a line takes to fade in from nothing.</summary>
    public const double FadeInSeconds = 0.5;

    /// <summary>How long a line takes to fade out.</summary>
    public const double FadeOutSeconds = 1.0;

    /// <summary>How long a line takes to replace the one showing.</summary>
    public const double CrossFadeSeconds = 0.3;

    /// <summary>
    /// How long before the next queued line is due the one showing has
    /// gone, so each line appears on a clear screen.
    /// </summary>
    public const double LeadSeconds = 2.0;

    /// <summary>
    /// The reading time of an empty line, and the least time any line
    /// shows before another may replace it.
    /// </summary>
    public const double BaseReadingSeconds = 2.5;

    /// <summary>The reading time each character adds.</summary>
    public const double SecondsPerCharacter = 0.05;

    /// <summary>The longest reading time, however long the line.</summary>
    public const double MaxReadingSeconds = 9.0;

    /// <summary>Game ticks in a real second at normal speed.</summary>
    public const double TicksPerSecond = 60.0;

    /// <summary>
    /// The least time a line shows when nothing replaces it: the base
    /// time plus a little per character, capped.
    /// </summary>
    /// <param name="text">The line; null counts as empty.</param>
    /// <returns>The reading time, in seconds.</returns>
    public static double ReadingSeconds(string? text)
    {
        int length = text?.Trim().Length ?? 0;
        return Math.Min(
            MaxReadingSeconds,
            BaseReadingSeconds + (SecondsPerCharacter * length));
    }

    /// <summary>
    /// Converts game ticks still to run into real seconds at the game's
    /// speed. The nominal speed is used, so a game running slower than
    /// nominal brings the line later than this says, and the line before
    /// it fades early rather than late.
    /// </summary>
    /// <param name="ticksLeft">Ticks until the line is due.</param>
    /// <param name="tickRateMultiplier">
    /// The game's speed: 1 at normal speed, 0 when paused.
    /// </param>
    /// <returns>
    /// The seconds; 0 when the line is already due, and infinity while
    /// the game is paused (the line comes no nearer).
    /// </returns>
    public static double SecondsUntil(int ticksLeft, float tickRateMultiplier)
    {
        if (ticksLeft <= 0)
        {
            return 0.0;
        }

        if (float.IsNaN(tickRateMultiplier) || tickRateMultiplier <= 0f)
        {
            return double.PositiveInfinity;
        }

        return ticksLeft / (TicksPerSecond * tickRateMultiplier);
    }
}

/// <summary>
/// What the narration overlay shows, one line at a time, as a function of
/// real time: the line said fades in; holds for its reading time, or
/// until a couple of seconds before the next queued line is due; and
/// fades out. A line said while another shows waits until that one has
/// been up for <see cref="NarrationTiming.BaseReadingSeconds"/>, then
/// replaces it with a quick cross-fade, so no line flashes past unread.
/// A patient line (the closing message) waits for the full reading time
/// instead. Kept free of Unity so it is unit tested; the game supplies
/// the clock.
/// </summary>
public sealed class NarrationOverlay
{
    /// <summary>Lines said but not yet shown, oldest first.</summary>
    private readonly List<WaitingLine> waiting = new List<WaitingLine>();

    /// <summary>The line showing, or null.</summary>
    private string? current;

    /// <summary>When the line showing appeared.</summary>
    private double shownAt;

    /// <summary>How long the line showing takes to fade in.</summary>
    private double fadeIn = NarrationTiming.FadeInSeconds;

    /// <summary>When the line showing has had its reading time.</summary>
    private double readUntil;

    /// <summary>When the line showing began to fade out, once it has.</summary>
    private double? fadeOutAt;

    /// <summary>The line being replaced, during a cross-fade, or null.</summary>
    private string? outgoing;

    /// <summary>When the cross-fade began.</summary>
    private double outgoingAt;

    /// <summary>How visible the replaced line was when it began.</summary>
    private double outgoingFrom;

    /// <summary>When the next queued line is due, or null for none.</summary>
    private double? nextDueAt;

    /// <summary>The latest time seen, so time never runs backwards.</summary>
    private double clock = double.NegativeInfinity;

    /// <summary>
    /// Gets the line showing (fading in, holding or fading out), or null.
    /// </summary>
    public string? CurrentLine => current;

    /// <summary>Gets the line being replaced during a cross-fade, or null.</summary>
    public string? OutgoingLine => outgoing;

    /// <summary>Gets the lines said but not yet shown, oldest first.</summary>
    public IReadOnlyList<string> WaitingLines =>
        waiting.Select(w => w.Text).ToList();

    /// <summary>
    /// Gets the last line handed to the overlay, shown or not; kept after
    /// <see cref="Clear"/>.
    /// </summary>
    public string? LastLine { get; private set; }

    /// <summary>Gets how many lines have been handed to the overlay.</summary>
    public int LinesSaid { get; private set; }

    /// <summary>Gets how many lines have appeared.</summary>
    public int LinesShown { get; private set; }

    /// <summary>
    /// Gets the longest any line waited between being said and appearing,
    /// in seconds.
    /// </summary>
    public double LongestWaitSeconds { get; private set; }

    /// <summary>Gets a value indicating whether nothing shows or waits.</summary>
    public bool IsEmpty =>
        current == null && outgoing == null && waiting.Count == 0;

    /// <summary>
    /// Hands the overlay a line, which appears at once if nothing shows,
    /// or replaces the line showing once that has been up long enough.
    /// Blank lines are ignored.
    /// </summary>
    /// <param name="text">The words.</param>
    /// <param name="now">The real time, in seconds.</param>
    /// <param name="patient">
    /// Whether the line waits for the full reading time of the one
    /// showing, rather than the base time (for a closing message).
    /// </param>
    public void Say(string? text, double now, bool patient = false)
    {
        if (string.IsNullOrWhiteSpace(text) || double.IsNaN(now))
        {
            return;
        }

        now = Observe(now);
        LastLine = text;
        LinesSaid++;
        waiting.Add(new WaitingLine(text!, now, patient));
        Advance(now);
    }

    /// <summary>
    /// Tells the overlay when the next queued line is due, so the line
    /// showing can be gone a little before it.
    /// </summary>
    /// <param name="seconds">
    /// Real seconds until it is due; infinity while the game is paused;
    /// null (or NaN) when nothing is queued.
    /// </param>
    /// <param name="now">The real time, in seconds.</param>
    public void SetNextLineIn(double? seconds, double now)
    {
        if (seconds is not double s || double.IsNaN(s) || double.IsNaN(now))
        {
            nextDueAt = null;
            return;
        }

        nextDueAt = Observe(now) + Math.Max(0.0, s);
    }

    /// <summary>
    /// Moves the overlay on to a time: a waiting line replaces the one
    /// showing when that has been up long enough, a line that has held
    /// long enough begins to fade, and a faded line is dropped. At most
    /// one waiting line appears per call, so every line shows for at
    /// least a frame and then its base time, however long the gap.
    /// </summary>
    /// <param name="now">The real time, in seconds.</param>
    public void Advance(double now)
    {
        if (double.IsNaN(now))
        {
            return;
        }

        now = Observe(now);
        if (outgoing != null
            && now >= outgoingAt + NarrationTiming.CrossFadeSeconds)
        {
            outgoing = null;
        }

        if (waiting.Count > 0 && HeadMayShow(now))
        {
            WaitingLine head = waiting[0];
            waiting.RemoveAt(0);
            Show(head, now);
        }

        if (current != null && fadeOutAt == null)
        {
            double start = FadeStart();
            if (now >= start)
            {
                fadeOutAt = start;
            }
        }

        if (current != null && fadeOutAt is double began
            && now >= began + NarrationTiming.FadeOutSeconds)
        {
            current = null;
            fadeOutAt = null;
        }
    }

    /// <summary>How visible the line showing is at a time.</summary>
    /// <param name="now">The real time, in seconds.</param>
    /// <returns>0 (unseen) to 1 (fully shown).</returns>
    public double Alpha(double now)
    {
        if (current == null || double.IsNaN(now))
        {
            return 0.0;
        }

        double rising = fadeIn <= 0.0 ? 1.0 : Clamp01((now - shownAt) / fadeIn);
        double start = fadeOutAt ?? FadeStart();
        double falling = now <= start
            ? 1.0
            : Clamp01(1.0 - ((now - start) / NarrationTiming.FadeOutSeconds));
        return Math.Min(rising, falling);
    }

    /// <summary>How visible the line being replaced is at a time.</summary>
    /// <param name="now">The real time, in seconds.</param>
    /// <returns>0 (gone) to how visible it was when replaced.</returns>
    public double OutgoingAlpha(double now)
    {
        if (outgoing == null || double.IsNaN(now))
        {
            return 0.0;
        }

        double left = 1.0
            - ((now - outgoingAt) / NarrationTiming.CrossFadeSeconds);
        return outgoingFrom * Clamp01(left);
    }

    /// <summary>
    /// Drops everything showing and waiting at once (Stop, a new run).
    /// <see cref="LastLine"/> and the counts are kept.
    /// </summary>
    public void Clear()
    {
        waiting.Clear();
        current = null;
        fadeOutAt = null;
        outgoing = null;
        nextDueAt = null;
    }

    /// <summary>Clamps a value to 0 to 1.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The clamped value; NaN becomes 0.</returns>
    private static double Clamp01(double value) =>
        double.IsNaN(value) ? 0.0 : Math.Max(0.0, Math.Min(1.0, value));

    /// <summary>
    /// Records a time and returns it, never earlier than one seen before.
    /// </summary>
    /// <param name="now">The real time, in seconds.</param>
    /// <returns>The time to use.</returns>
    private double Observe(double now)
    {
        clock = Math.Max(clock, now);
        return clock;
    }

    /// <summary>
    /// Whether the oldest waiting line may appear now: nothing shows, the
    /// line showing is already fading, or it has been up for long enough
    /// (the base time, or its whole reading time for a patient line).
    /// </summary>
    /// <param name="now">The real time, in seconds.</param>
    /// <returns>True if it may.</returns>
    private bool HeadMayShow(double now)
    {
        if (current == null || fadeOutAt != null)
        {
            return true;
        }

        double hold = waiting[0].Patient
            ? readUntil - shownAt
            : NarrationTiming.BaseReadingSeconds;
        return now >= shownAt + hold;
    }

    /// <summary>
    /// When the line showing begins to fade out, if nothing replaces it:
    /// after its reading time, or later so that it is gone
    /// <see cref="NarrationTiming.LeadSeconds"/> before the next queued
    /// line is due. Never while a line waits (that one replaces it).
    /// </summary>
    /// <returns>The time; infinity to hold.</returns>
    private double FadeStart()
    {
        if (waiting.Count > 0)
        {
            return double.PositiveInfinity;
        }

        if (nextDueAt is double due)
        {
            double gone = due - NarrationTiming.LeadSeconds
                - NarrationTiming.FadeOutSeconds;
            return Math.Max(readUntil, gone);
        }

        return readUntil;
    }

    /// <summary>
    /// Shows a line: a cross-fade from the line showing if any of it is
    /// still visible, otherwise a fade-in from nothing.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="now">The real time, in seconds.</param>
    private void Show(WaitingLine line, double now)
    {
        double visible = Alpha(now);
        if (current != null && visible > 0.0)
        {
            outgoing = current;
            outgoingAt = now;
            outgoingFrom = visible;
            fadeIn = NarrationTiming.CrossFadeSeconds;
        }
        else
        {
            outgoing = null;
            fadeIn = NarrationTiming.FadeInSeconds;
        }

        current = line.Text;
        shownAt = now;
        readUntil = now + NarrationTiming.ReadingSeconds(line.Text);
        fadeOutAt = null;
        LinesShown++;
        LongestWaitSeconds = Math.Max(LongestWaitSeconds, now - line.SaidAt);
    }

    /// <summary>A line said but not yet shown.</summary>
    private readonly struct WaitingLine
    {
        /// <summary>Creates a waiting line.</summary>
        /// <param name="text">The words.</param>
        /// <param name="saidAt">When it was said.</param>
        /// <param name="patient">Whether it waits for a full reading.</param>
        public WaitingLine(string text, double saidAt, bool patient)
        {
            Text = text;
            SaidAt = saidAt;
            Patient = patient;
        }

        /// <summary>Gets the words.</summary>
        public string Text { get; }

        /// <summary>Gets when it was said.</summary>
        public double SaidAt { get; }

        /// <summary>
        /// Gets a value indicating whether it waits for a full reading.
        /// </summary>
        public bool Patient { get; }
    }
}

/// <summary>One pass of the overlay's drop shadow.</summary>
public readonly struct ShadowPass
{
    /// <summary>Creates a pass.</summary>
    /// <param name="x">Offset right, in UI units.</param>
    /// <param name="y">Offset down, in UI units.</param>
    /// <param name="alpha">Its opacity, 0 to 1.</param>
    public ShadowPass(float x, float y, float alpha)
    {
        X = x;
        Y = y;
        Alpha = alpha;
    }

    /// <summary>Gets the offset right.</summary>
    public float X { get; }

    /// <summary>Gets the offset down.</summary>
    public float Y { get; }

    /// <summary>Gets the opacity.</summary>
    public float Alpha { get; }
}

/// <summary>
/// Where the narration overlay goes and how large it is drawn: centred,
/// in the lower third, above the gizmos, inspect pane and main buttons
/// along the bottom, and never above the caption bar; about twice the
/// Medium font, in UI units so it follows the UI scale.
/// </summary>
public static class NarrationOverlayLayout
{
    /// <summary>How much larger than the Medium font the text is.</summary>
    public const float FontScale = 2f;

    /// <summary>The most lines a narration line wraps to.</summary>
    public const int MaxLines = 2;

    /// <summary>The widest the text runs, in UI units.</summary>
    public const float MaxWidth = 1100f;

    /// <summary>The gap kept from the screen's sides.</summary>
    public const float SideMargin = 24f;

    /// <summary>
    /// The height kept clear along the bottom: the main buttons (35),
    /// the inspect pane (165) or two rows of gizmos, and a gap.
    /// </summary>
    public const float BottomReserve = 230f;

    /// <summary>The step by which a line too long for two lines shrinks.</summary>
    public const int FontStep = 2;

    /// <summary>
    /// The drop shadow's passes: a soft dark halo, heavier below, drawn
    /// before the text so white lettering reads over snow, sand and
    /// explosions alike.
    /// </summary>
    public static IReadOnlyList<ShadowPass> ShadowPasses { get; } =
        new[]
        {
            new ShadowPass(0f, 3f, 0.35f),
            new ShadowPass(2f, 4f, 0.25f),
            new ShadowPass(-2f, 4f, 0.25f),
            new ShadowPass(2f, 0f, 0.18f),
            new ShadowPass(-2f, 0f, 0.18f),
            new ShadowPass(0f, -2f, 0.18f),
            new ShadowPass(0f, 2f, 0.18f),
            new ShadowPass(1.5f, 1.5f, 0.18f),
            new ShadowPass(-1.5f, 1.5f, 0.18f),
            new ShadowPass(1.5f, -1.5f, 0.18f),
            new ShadowPass(-1.5f, -1.5f, 0.18f),
        };

    /// <summary>The text's width on a screen.</summary>
    /// <param name="screenWidth">The screen's width in UI units.</param>
    /// <returns>The width, at most <see cref="MaxWidth"/>.</returns>
    public static float TextWidth(float screenWidth) =>
        Math.Max(0f, Math.Min(MaxWidth, screenWidth - (2f * SideMargin)));

    /// <summary>
    /// Places the text: centred, its bottom <see cref="BottomReserve"/>
    /// above the screen's bottom, rising on a short screen but never
    /// above <paramref name="topLimit"/>.
    /// </summary>
    /// <param name="screenWidth">The screen's width in UI units.</param>
    /// <param name="screenHeight">The screen's height in UI units.</param>
    /// <param name="textHeight">The wrapped text's height.</param>
    /// <param name="topLimit">
    /// The highest the text may start: below the caption bar.
    /// </param>
    /// <returns>The text's box.</returns>
    public static UiBox Compute(
        float screenWidth, float screenHeight, float textHeight, float topLimit)
    {
        float screen = Math.Max(0f, screenWidth);
        float width = TextWidth(screen);
        float height = Math.Max(0f, textHeight);
        float x = (screen - width) / 2f;
        float y = Math.Max(0f, screenHeight) - BottomReserve - height;
        return new UiBox(x, Math.Max(y, topLimit), width, height);
    }

    /// <summary>
    /// The font size to draw a line at: <see cref="FontScale"/> times the
    /// Medium size, stepped down until the line wraps to
    /// <see cref="MaxLines"/> or fewer, but never below the Medium size.
    /// </summary>
    /// <param name="mediumSize">The Medium font's size.</param>
    /// <param name="linesAt">How many lines the text wraps to at a size.</param>
    /// <returns>The size.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="linesAt"/> is null.
    /// </exception>
    public static int FitFontSize(int mediumSize, Func<int, int> linesAt)
    {
        if (linesAt == null)
        {
            throw new ArgumentNullException(nameof(linesAt));
        }

        int floor = Math.Max(1, mediumSize);
        int largest = (int)Math.Round(floor * FontScale);
        for (int size = largest; size > floor; size -= FontStep)
        {
            if (linesAt(size) <= MaxLines)
            {
                return size;
            }
        }

        return floor;
    }
}
