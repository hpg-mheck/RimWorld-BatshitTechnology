using System;
using System.Linq;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the narration overlay's timing rules (one line at a time,
/// fading in, holding for its reading time or until shortly before the
/// next queued line, fading out, and replacing with a cross-fade) and its
/// layout.
/// </summary>
public sealed class NarrationOverlayTests
{
    /// <summary>A line of 30 characters: 4.0 s of reading time.</summary>
    private const string Thirty = "Thirty characters of narration";

    /// <summary>Another line of 30 characters.</summary>
    private const string Other = "Other thirty chars, one minute";

    /// <summary>A third line.</summary>
    private const string Third = "A third line, said hurriedly.";

    /// <summary>The tolerance for times and alphas.</summary>
    private const int Places = 6;

    /// <summary>
    /// Reading time is 2.5 s plus 50 ms a character, capped at 9 s;
    /// blank and null lines read in the base time.
    /// </summary>
    [Fact(DisplayName = "Reading time grows with length, capped")]
    public void ReadingSeconds_should_grow_with_length_and_cap()
    {
        Assert.Equal(2.5, NarrationTiming.ReadingSeconds(null), Places);
        Assert.Equal(2.5, NarrationTiming.ReadingSeconds("   "), Places);
        Assert.Equal(4.0, NarrationTiming.ReadingSeconds(Thirty), Places);
        Assert.Equal(4.0, NarrationTiming.ReadingSeconds("  " + Thirty + " "), Places);
        Assert.Equal(7.3, NarrationTiming.ReadingSeconds(new string('a', 96)), Places);
        Assert.Equal(9.0, NarrationTiming.ReadingSeconds(new string('a', 500)), Places);
    }

    /// <summary>
    /// Ticks become real seconds at the game's speed; a paused game
    /// never brings the line nearer, and a due line is 0 s away.
    /// </summary>
    [Fact(DisplayName = "Ticks convert to real seconds at the game's speed")]
    public void SecondsUntil_should_follow_the_speed()
    {
        Assert.Equal(2.0, NarrationTiming.SecondsUntil(120, 1f), Places);
        Assert.Equal(120.0 / 180.0, NarrationTiming.SecondsUntil(120, 3f), Places);
        Assert.True(double.IsPositiveInfinity(NarrationTiming.SecondsUntil(120, 0f)));
        Assert.True(double.IsPositiveInfinity(NarrationTiming.SecondsUntil(120, -1f)));
        Assert.True(double.IsPositiveInfinity(
            NarrationTiming.SecondsUntil(120, float.NaN)));
        Assert.Equal(0.0, NarrationTiming.SecondsUntil(0, 1f), Places);
        Assert.Equal(0.0, NarrationTiming.SecondsUntil(-30, 0f), Places);
    }

    /// <summary>
    /// A line said on an empty overlay appears at once and fades in over
    /// half a second.
    /// </summary>
    [Fact(DisplayName = "A line fades in over half a second")]
    public void Say_should_fade_a_line_in()
    {
        var overlay = new NarrationOverlay();

        overlay.Say(Thirty, 10.0);

        Assert.Equal(Thirty, overlay.CurrentLine);
        Assert.Null(overlay.OutgoingLine);
        Assert.Equal(0.0, overlay.Alpha(10.0), Places);
        Assert.Equal(0.5, overlay.Alpha(10.25), Places);
        Assert.Equal(1.0, overlay.Alpha(10.5), Places);
    }

    /// <summary>
    /// With nothing queued, a line holds for its reading time, fades out
    /// over a second, and is then dropped.
    /// </summary>
    [Fact(DisplayName = "With nothing queued, a line holds its reading time")]
    public void Line_should_hold_for_its_reading_time_then_fade()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.SetNextLineIn(null, 0.0);

        overlay.Advance(3.9);
        Assert.Equal(1.0, overlay.Alpha(3.9), Places);
        overlay.Advance(4.5);
        Assert.Equal(0.5, overlay.Alpha(4.5), Places);
        Assert.Equal(Thirty, overlay.CurrentLine);
        overlay.Advance(5.0);

        Assert.Null(overlay.CurrentLine);
        Assert.True(overlay.IsEmpty);
        Assert.Equal(0.0, overlay.Alpha(5.0), Places);
    }

    /// <summary>
    /// With a line queued well ahead, the line showing holds until it can
    /// be gone two seconds before that line is due.
    /// </summary>
    [Fact(DisplayName = "A queued line keeps the current one until shortly before")]
    public void Line_should_be_gone_two_seconds_before_the_next_is_due()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.SetNextLineIn(20.0, 0.0);

        overlay.Advance(16.9);
        Assert.Equal(1.0, overlay.Alpha(16.9), Places);
        overlay.Advance(17.5);
        Assert.Equal(0.5, overlay.Alpha(17.5), Places);
        overlay.Advance(18.0);

        Assert.Equal(0.0, overlay.Alpha(18.0), Places);
        Assert.Null(overlay.CurrentLine);
    }

    /// <summary>
    /// A queued line due soon never cuts the reading time short: the
    /// line showing still holds its reading time.
    /// </summary>
    [Fact(DisplayName = "A queued line never cuts the reading time short")]
    public void Line_should_keep_its_reading_time_when_the_next_is_near()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.SetNextLineIn(1.0, 0.0);

        overlay.Advance(3.9);

        Assert.Equal(1.0, overlay.Alpha(3.9), Places);
        Assert.Equal(0.5, overlay.Alpha(4.5), Places);
    }

    /// <summary>
    /// While the game is paused a queued line comes no nearer, so the line
    /// showing holds.
    /// </summary>
    [Fact(DisplayName = "A paused game holds the line before a queued one")]
    public void Line_should_hold_while_paused_with_a_line_queued()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.SetNextLineIn(NarrationTiming.SecondsUntil(300, 0f), 0.0);

        overlay.Advance(120.0);

        Assert.Equal(Thirty, overlay.CurrentLine);
        Assert.Equal(1.0, overlay.Alpha(120.0), Places);
    }

    /// <summary>
    /// Once a fade-out has begun, the next line moving later (the game
    /// slowed or paused) does not bring the line back.
    /// </summary>
    [Fact(DisplayName = "A fade-out once begun is never reversed")]
    public void Fade_out_should_not_reverse_when_the_next_line_moves_later()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.SetNextLineIn(10.0, 0.0);
        overlay.Advance(7.5);
        Assert.Equal(0.5, overlay.Alpha(7.5), Places);

        overlay.SetNextLineIn(double.PositiveInfinity, 7.5);
        overlay.Advance(7.6);

        Assert.Equal(0.4, overlay.Alpha(7.6), Places);
    }

    /// <summary>
    /// A line said while another shows waits until that one has been up
    /// for the base time, then replaces it with a 0.3 s cross-fade.
    /// </summary>
    [Fact(DisplayName = "A line said over another waits, then cross-fades")]
    public void Say_should_wait_for_the_base_time_then_cross_fade()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);

        overlay.Say(Other, 1.0);
        Assert.Equal(Thirty, overlay.CurrentLine);
        Assert.Equal(new[] { Other }, overlay.WaitingLines);
        overlay.Advance(2.4);
        Assert.Equal(Thirty, overlay.CurrentLine);
        Assert.Equal(1.0, overlay.Alpha(2.4), Places);

        overlay.Advance(2.5);
        Assert.Equal(Other, overlay.CurrentLine);
        Assert.Equal(Thirty, overlay.OutgoingLine);
        Assert.Empty(overlay.WaitingLines);
        Assert.Equal(1.0, overlay.OutgoingAlpha(2.5), Places);
        Assert.Equal(0.5, overlay.OutgoingAlpha(2.65), Places);
        Assert.Equal(0.5, overlay.Alpha(2.65), Places);
        Assert.Equal(1.0, overlay.Alpha(2.8), Places);

        overlay.Advance(2.85);
        Assert.Null(overlay.OutgoingLine);
        Assert.Equal(1.5, overlay.LongestWaitSeconds, Places);
    }

    /// <summary>
    /// A line said after the one showing has had its base time replaces
    /// it at once.
    /// </summary>
    [Fact(DisplayName = "A line said late replaces the current one at once")]
    public void Say_should_replace_at_once_after_the_base_time()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);

        overlay.Say(Other, 3.0);

        Assert.Equal(Other, overlay.CurrentLine);
        Assert.Equal(Thirty, overlay.OutgoingLine);
        Assert.Equal(0.0, overlay.LongestWaitSeconds, Places);
    }

    /// <summary>
    /// A line said once the overlay has emptied fades in from nothing,
    /// with nothing to cross-fade from.
    /// </summary>
    [Fact(DisplayName = "A line on a cleared screen fades in fully")]
    public void Say_should_fade_in_fully_after_a_gap()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.Advance(10.0);

        overlay.Say(Other, 10.0);

        Assert.Equal(Other, overlay.CurrentLine);
        Assert.Null(overlay.OutgoingLine);
        Assert.Equal(0.5, overlay.Alpha(10.25), Places);
    }

    /// <summary>
    /// A line said while the one showing fades out replaces it at once,
    /// cross-fading from how visible it still was.
    /// </summary>
    [Fact(DisplayName = "A line said during a fade-out cross-fades from it")]
    public void Say_should_cross_fade_from_a_fading_line()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.Advance(4.5);

        overlay.Say(Other, 4.5);

        Assert.Equal(Other, overlay.CurrentLine);
        Assert.Equal(Thirty, overlay.OutgoingLine);
        Assert.Equal(0.5, overlay.OutgoingAlpha(4.5), Places);
        Assert.Equal(0.25, overlay.OutgoingAlpha(4.65), Places);
    }

    /// <summary>
    /// Lines said together show in order, each for at least the base
    /// time, even when the clock jumps: one line appears per call.
    /// </summary>
    [Fact(DisplayName = "Lines said together each show for the base time")]
    public void Backlog_should_show_each_line_in_order()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.Say(Other, 0.0);
        overlay.Say(Third, 0.0);
        Assert.Equal(new[] { Other, Third }, overlay.WaitingLines);

        overlay.Advance(60.0);
        Assert.Equal(Other, overlay.CurrentLine);
        overlay.Advance(61.0);
        Assert.Equal(Other, overlay.CurrentLine);
        overlay.Advance(62.5);
        Assert.Equal(Third, overlay.CurrentLine);

        Assert.Equal(3, overlay.LinesSaid);
        Assert.Equal(3, overlay.LinesShown);
        Assert.Equal(62.5, overlay.LongestWaitSeconds, Places);
        Assert.Equal(Third, overlay.LastLine);
    }

    /// <summary>
    /// A patient line (the closing message) waits for the full reading
    /// time of the line showing, not the base time.
    /// </summary>
    [Fact(DisplayName = "A patient line waits for the full reading time")]
    public void Patient_line_should_wait_for_the_full_reading_time()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.Say(Other, 0.5, patient: true);

        overlay.Advance(3.0);
        Assert.Equal(Thirty, overlay.CurrentLine);
        Assert.Equal(1.0, overlay.Alpha(3.0), Places);
        overlay.Advance(4.0);

        Assert.Equal(Other, overlay.CurrentLine);
    }

    /// <summary>
    /// Clear drops everything showing and waiting at once, but keeps the
    /// last line and the counts; a line said afterwards shows at once.
    /// </summary>
    [Fact(DisplayName = "Clear drops the overlay's lines at once")]
    public void Clear_should_drop_everything()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(Thirty, 0.0);
        overlay.Say(Other, 0.1);
        overlay.SetNextLineIn(5.0, 0.1);

        overlay.Clear();

        Assert.True(overlay.IsEmpty);
        Assert.Null(overlay.CurrentLine);
        Assert.Empty(overlay.WaitingLines);
        Assert.Equal(0.0, overlay.Alpha(0.2), Places);
        Assert.Equal(Other, overlay.LastLine);
        Assert.Equal(2, overlay.LinesSaid);

        overlay.Say(Third, 0.2);
        Assert.Equal(Third, overlay.CurrentLine);
        Assert.Equal(0.5, overlay.Alpha(0.45), Places);
    }

    /// <summary>
    /// Blank lines and times that are not numbers are ignored, and time
    /// never runs backwards.
    /// </summary>
    [Fact(DisplayName = "Blank lines, bad times and a clock going back are ignored")]
    public void Overlay_should_ignore_bad_input()
    {
        var overlay = new NarrationOverlay();
        overlay.Say(null, 0.0);
        overlay.Say("  ", 0.0);
        overlay.Say(Thirty, double.NaN);
        Assert.True(overlay.IsEmpty);
        Assert.Equal(0, overlay.LinesSaid);

        overlay.Say(Thirty, 5.0);
        overlay.Advance(double.NaN);
        overlay.Say(Other, 1.0);
        overlay.Advance(6.0);

        Assert.Equal(Thirty, overlay.CurrentLine);
        overlay.Advance(7.5);
        Assert.Equal(Other, overlay.CurrentLine);
        Assert.Equal(0.0, overlay.Alpha(double.NaN), Places);
    }

    /// <summary>
    /// The text is centred, kept above the bottom bar's reserve, inside
    /// the lower third on screens at least 1080 units tall, and never above
    /// the limit below the caption bar.
    /// </summary>
    /// <param name="width">The screen's width in UI units.</param>
    /// <param name="height">The screen's height in UI units.</param>
    [Theory(DisplayName = "Overlay sits centred in the lower third")]
    [InlineData(1920f, 1080f)]
    [InlineData(1280f, 720f)]
    [InlineData(2560f, 1440f)]
    [InlineData(3840f, 2160f)]
    [InlineData(960f, 900f)]
    public void Compute_should_centre_in_the_lower_third(float width, float height)
    {
        foreach (float text in new[] { 0f, 60f, 120f })
        {
            UiBox box = NarrationOverlayLayout.Compute(width, height, text, 60f);

            Assert.Equal(width - box.Right, box.X, 3);
            Assert.True(box.Width <= NarrationOverlayLayout.MaxWidth + 0.001f);
            Assert.True(box.X >= NarrationOverlayLayout.SideMargin - 0.001f);
            Assert.True(
                box.Bottom <= height - NarrationOverlayLayout.BottomReserve + 0.001f,
                box.ToString());
            Assert.True(box.Y >= 60f - 0.001f, box.ToString());
            if (height >= 1080f)
            {
                Assert.True(box.Y >= height * 2f / 3f - 0.001f, box.ToString());
            }
        }
    }

    /// <summary>
    /// On a screen too short for the reserve, the text rises but never
    /// above the limit below the caption bar.
    /// </summary>
    [Fact(DisplayName = "Overlay never rises above the caption bar")]
    public void Compute_should_respect_the_top_limit()
    {
        UiBox box = NarrationOverlayLayout.Compute(800f, 300f, 120f, 80f);

        Assert.Equal(80f, box.Y, 3);
        Assert.Equal(120f, box.Height, 3);
    }

    /// <summary>
    /// A line is drawn at twice the Medium size when it fits two lines,
    /// smaller in even steps when it does not, and never below Medium.
    /// </summary>
    [Fact(DisplayName = "Font size is twice Medium, shrinking to fit two lines")]
    public void FitFontSize_should_shrink_to_two_lines()
    {
        Assert.Equal(40, NarrationOverlayLayout.FitFontSize(20, _ => 1));
        Assert.Equal(40, NarrationOverlayLayout.FitFontSize(20, _ => 2));
        Assert.Equal(34, NarrationOverlayLayout.FitFontSize(20, s => s > 34 ? 3 : 2));
        Assert.Equal(20, NarrationOverlayLayout.FitFontSize(20, _ => 5));
        Assert.Equal(1, NarrationOverlayLayout.FitFontSize(0, _ => 5));
        Assert.Throws<ArgumentNullException>(
            () => NarrationOverlayLayout.FitFontSize(20, null!));
    }

    /// <summary>
    /// The shadow is soft: many light passes, close to the text, heavier
    /// below than above.
    /// </summary>
    [Fact(DisplayName = "Drop shadow is soft and falls below the text")]
    public void ShadowPasses_should_be_soft()
    {
        var passes = NarrationOverlayLayout.ShadowPasses;

        Assert.True(passes.Count >= 8);
        Assert.All(passes, p => Assert.InRange(p.Alpha, 0.05f, 0.4f));
        Assert.All(passes, p => Assert.InRange(Math.Abs(p.X), 0f, 4f));
        Assert.All(passes, p => Assert.InRange(Math.Abs(p.Y), 0f, 4f));
        float below = passes.Where(p => p.Y > 0f).Sum(p => p.Alpha);
        float above = passes.Where(p => p.Y < 0f).Sum(p => p.Alpha);
        Assert.True(below > above * 2f, $"below {below}, above {above}");
    }
}
