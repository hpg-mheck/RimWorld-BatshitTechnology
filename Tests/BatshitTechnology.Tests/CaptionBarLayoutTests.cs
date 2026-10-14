using System;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the showcase caption bar's layout: centred below the colonist
/// bar, sized to its caption within the screen, with its buttons inside
/// it and clear of the text.
/// </summary>
public sealed class CaptionBarLayoutTests
{
    /// <summary>Screen widths in UI units, 2x scale included.</summary>
    public static TheoryData<float> ScreenWidths => new TheoryData<float>
    {
        400f, 512f, 640f, 800f, 960f, 1280f, 1920f, 3840f,
    };

    /// <summary>The bar is centred and kept on the screen.</summary>
    /// <param name="screen">The screen's width.</param>
    [Theory(DisplayName = "Caption bar is centred and fits the screen")]
    [MemberData(nameof(ScreenWidths))]
    public void Compute_should_centre_and_fit(float screen)
    {
        foreach (float text in new[] { 0f, 120f, 400f, 2000f })
        {
            foreach (bool skip in new[] { false, true })
            {
                CaptionBarRects r =
                    CaptionBarLayout.Compute(screen, 50f, text, skip);

                Assert.Equal(screen - r.Bar.Right, r.Bar.X, 3);
                Assert.True(r.Bar.X >= CaptionBarLayout.Margin - 0.001f, r.Bar.ToString());
                Assert.True(r.Bar.Width <= CaptionBarLayout.MaxWidth + 0.001f);
                Assert.Equal(CaptionBarLayout.Height, r.Bar.Height);
                Assert.Equal(50f, r.Bar.Y);
            }
        }
    }

    /// <summary>
    /// Buttons and text sit inside the bar and never overlap one another.
    /// </summary>
    /// <param name="screen">The screen's width.</param>
    [Theory(DisplayName = "Caption bar buttons and text sit inside it, apart")]
    [MemberData(nameof(ScreenWidths))]
    public void Compute_should_keep_parts_inside_and_apart(float screen)
    {
        foreach (float text in new[] { 0f, 200f, 5000f })
        {
            CaptionBarRects r = CaptionBarLayout.Compute(screen, 10f, text, true);
            UiBox skip = r.Skip ?? throw new InvalidOperationException("no skip");

            Assert.True(r.Bar.Contains(r.Stop), $"{r.Stop} in {r.Bar}");
            Assert.True(r.Bar.Contains(skip), $"{skip} in {r.Bar}");
            Assert.True(r.Bar.Contains(r.Text), $"{r.Text} in {r.Bar}");
            Assert.False(skip.Overlaps(r.Stop));
            Assert.False(r.Text.Overlaps(skip));
            Assert.False(r.Text.Overlaps(r.Stop));
            Assert.True(skip.Right <= r.Stop.X);
        }
    }

    /// <summary>A short caption gets a short bar, a long one the most.</summary>
    [Fact(DisplayName = "Caption bar grows with its caption up to its cap")]
    public void Compute_should_size_to_the_caption()
    {
        CaptionBarRects shortBar = CaptionBarLayout.Compute(1920f, 0f, 100f, false);
        CaptionBarRects longBar = CaptionBarLayout.Compute(1920f, 0f, 3000f, false);

        Assert.True(shortBar.Bar.Width < longBar.Bar.Width);
        Assert.True(shortBar.Text.Width >= 100f - 0.001f);
        Assert.Equal(CaptionBarLayout.MaxWidth, longBar.Bar.Width, 3);
    }

    /// <summary>Without Skip there is only the Stop button.</summary>
    [Fact(DisplayName = "Caption bar without Skip has only Stop")]
    public void Compute_should_omit_skip_when_not_offered()
    {
        CaptionBarRects r = CaptionBarLayout.Compute(1280f, 0f, 300f, false);

        Assert.Null(r.Skip);
        Assert.True(r.Text.Right <= r.Stop.X);
    }

    /// <summary>The bar never rises above the top margin.</summary>
    [Fact(DisplayName = "Caption bar stays below the top margin")]
    public void Compute_should_respect_the_top_margin()
    {
        CaptionBarRects r = CaptionBarLayout.Compute(1280f, -40f, 300f, true);

        Assert.Equal(CaptionBarLayout.Margin, r.Bar.Y);
    }

    /// <summary>
    /// Below the colonist bar means below its lowest row, its names and a
    /// margin; with no colonists shown, at the margin.
    /// </summary>
    [Fact(DisplayName = "Caption bar sits below the colonist bar's lowest row")]
    public void TopBelowColonists_should_clear_the_lowest_row()
    {
        float top = CaptionBarLayout.TopBelowColonists(
            new[] { 21f, 21f, 101f }, 48f, 1f);

        Assert.Equal(
            101f + 48f + CaptionBarLayout.ColonistLabelAllowance
                + CaptionBarLayout.Margin,
            top,
            3);
        Assert.Equal(
            CaptionBarLayout.Margin,
            CaptionBarLayout.TopBelowColonists(null, 48f, 1f));
        Assert.Equal(
            CaptionBarLayout.Margin,
            CaptionBarLayout.TopBelowColonists(Array.Empty<float>(), 48f, 1f));
    }

    /// <summary>
    /// The colonist bar's scale shrinks the room left for names, and bad
    /// numbers are ignored rather than thrown.
    /// </summary>
    [Fact(DisplayName = "Caption bar placement scales and ignores bad numbers")]
    public void TopBelowColonists_should_scale_and_ignore_nonsense()
    {
        float half = CaptionBarLayout.TopBelowColonists(new[] { 20f }, 24f, 0.5f);
        float nan = CaptionBarLayout.TopBelowColonists(
            new[] { float.NaN, 20f, float.PositiveInfinity }, 24f, 0.5f);

        Assert.Equal(
            20f + 24f + (CaptionBarLayout.ColonistLabelAllowance / 2f)
                + CaptionBarLayout.Margin,
            half,
            3);
        Assert.Equal(half, nan, 3);
    }

    /// <summary>Box helpers agree with their definitions.</summary>
    [Fact(DisplayName = "UiBox clamps negative sizes and tests overlap")]
    public void UiBox_should_clamp_and_compare()
    {
        var box = new UiBox(0f, 0f, -5f, 10f);
        var a = new UiBox(0f, 0f, 10f, 10f);
        var b = new UiBox(10f, 0f, 10f, 10f);
        var c = new UiBox(2f, 2f, 4f, 4f);

        Assert.Equal(0f, box.Width);
        Assert.False(a.Overlaps(b));
        Assert.True(a.Overlaps(c));
        Assert.True(a.Contains(c));
        Assert.False(c.Contains(a));
    }
}
