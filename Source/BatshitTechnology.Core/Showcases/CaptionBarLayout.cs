using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Showcases;

/// <summary>
/// A rectangle in UI units, x growing right and y growing down, as
/// RimWorld's IMGUI draws. Kept free of Unity so the layout is unit
/// tested.
/// </summary>
public readonly struct UiBox
{
    /// <summary>Creates a box.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width; negatives become zero.</param>
    /// <param name="height">The height; negatives become zero.</param>
    public UiBox(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = Math.Max(0f, width);
        Height = Math.Max(0f, height);
    }

    /// <summary>Gets the left edge.</summary>
    public float X { get; }

    /// <summary>Gets the top edge.</summary>
    public float Y { get; }

    /// <summary>Gets the width.</summary>
    public float Width { get; }

    /// <summary>Gets the height.</summary>
    public float Height { get; }

    /// <summary>Gets the right edge.</summary>
    public float Right => X + Width;

    /// <summary>Gets the bottom edge.</summary>
    public float Bottom => Y + Height;

    /// <summary>
    /// Whether another box lies wholly inside this one.
    /// </summary>
    /// <param name="other">The other box.</param>
    /// <returns>True if it is inside, edges included.</returns>
    public bool Contains(UiBox other) =>
        other.X >= X - 0.001f && other.Y >= Y - 0.001f
        && other.Right <= Right + 0.001f
        && other.Bottom <= Bottom + 0.001f;

    /// <summary>
    /// Whether this box and another share any area.
    /// </summary>
    /// <param name="other">The other box.</param>
    /// <returns>True if they overlap.</returns>
    public bool Overlaps(UiBox other) =>
        X < other.Right && other.X < Right
        && Y < other.Bottom && other.Y < Bottom;

    /// <summary>A readable form, for test failures.</summary>
    /// <returns>The box's position and size.</returns>
    public override string ToString() =>
        $"({X:0.#},{Y:0.#} {Width:0.#}x{Height:0.#})";
}

/// <summary>
/// Where the parts of the showcase caption bar go: the bar, its caption
/// text, and its Skip and Stop buttons.
/// </summary>
public sealed class CaptionBarRects
{
    /// <summary>Creates the rectangles.</summary>
    /// <param name="bar">The whole bar.</param>
    /// <param name="text">The caption text.</param>
    /// <param name="skip">The Skip button, or null when not offered.</param>
    /// <param name="stop">The Stop button.</param>
    public CaptionBarRects(UiBox bar, UiBox text, UiBox? skip, UiBox stop)
    {
        Bar = bar;
        Text = text;
        Skip = skip;
        Stop = stop;
    }

    /// <summary>Gets the whole bar.</summary>
    public UiBox Bar { get; }

    /// <summary>Gets the caption text's box.</summary>
    public UiBox Text { get; }

    /// <summary>Gets the Skip button's box, or null.</summary>
    public UiBox? Skip { get; }

    /// <summary>Gets the Stop button's box.</summary>
    public UiBox Stop { get; }
}

/// <summary>
/// Lays out the caption bar a running showcase shows: a title card
/// centred at the top of the screen, just below the colonist bar, so it
/// never covers the gizmos, inspect pane or main buttons along the bottom
/// (docs/AI-DECISIONS.txt, 2026-10-14).
/// </summary>
public static class CaptionBarLayout
{
    /// <summary>The bar's height.</summary>
    public const float Height = 34f;

    /// <summary>The gap kept from the screen's edges and other UI.</summary>
    public const float Margin = 6f;

    /// <summary>The padding inside the bar.</summary>
    public const float Padding = 8f;

    /// <summary>Each button's width.</summary>
    public const float ButtonWidth = 64f;

    /// <summary>The gap between the two buttons.</summary>
    public const float ButtonGap = 4f;

    /// <summary>The widest the bar grows.</summary>
    public const float MaxWidth = 760f;

    /// <summary>
    /// The room below each colonist portrait for its name, at scale 1.
    /// </summary>
    public const float ColonistLabelAllowance = 18f;

    /// <summary>
    /// The bar's top edge: below the lowest row of the colonist bar, or at
    /// the margin when no colonist is shown.
    /// </summary>
    /// <param name="rowTops">
    /// The top edge of every colonist portrait; may be null or empty.
    /// </param>
    /// <param name="portraitHeight">A portrait's height.</param>
    /// <param name="scale">The colonist bar's scale.</param>
    /// <returns>The top edge.</returns>
    public static float TopBelowColonists(
        IEnumerable<float>? rowTops, float portraitHeight, float scale)
    {
        List<float> tops = (rowTops ?? Enumerable.Empty<float>())
            .Where(t => !float.IsNaN(t) && !float.IsInfinity(t))
            .ToList();
        if (tops.Count == 0)
        {
            return Margin;
        }

        float label = ColonistLabelAllowance * Math.Max(0f, scale);
        return tops.Max() + Math.Max(0f, portraitHeight) + label + Margin;
    }

    /// <summary>
    /// Lays out the bar: centred, as wide as its caption needs up to
    /// <see cref="MaxWidth"/> and the screen, with Stop at the right end
    /// and Skip to its left. On a narrow screen the caption's box shrinks
    /// first (the text is clipped with an ellipsis by the caller).
    /// </summary>
    /// <param name="screenWidth">The screen's width in UI units.</param>
    /// <param name="top">The bar's top edge.</param>
    /// <param name="textWidth">The caption's natural width.</param>
    /// <param name="withSkip">Whether to make room for Skip.</param>
    /// <returns>The rectangles.</returns>
    public static CaptionBarRects Compute(
        float screenWidth, float top, float textWidth, bool withSkip)
    {
        float screen = Math.Max(0f, screenWidth);
        int buttons = withSkip ? 2 : 1;
        float buttonsWidth = (buttons * ButtonWidth)
            + ((buttons - 1) * ButtonGap);
        float wanted = Padding + Math.Max(0f, textWidth) + Padding
            + buttonsWidth + Padding;
        float room = Math.Max(0f, screen - (2f * Margin));
        float width = Math.Min(Math.Min(wanted, MaxWidth), room);
        float x = (screen - width) / 2f;
        float y = Math.Max(Margin, top);
        var bar = new UiBox(x, y, width, Height);

        float buttonHeight = Height - (2f * 4f);
        float buttonY = y + 4f;
        var stop = new UiBox(
            bar.Right - Padding - ButtonWidth,
            buttonY,
            ButtonWidth,
            buttonHeight);
        UiBox? skip = withSkip
            ? new UiBox(
                stop.X - ButtonGap - ButtonWidth,
                buttonY,
                ButtonWidth,
                buttonHeight)
            : (UiBox?)null;
        float textRight = (skip ?? stop).X - Padding;
        var text = new UiBox(
            x + Padding, y, Math.Max(0f, textRight - (x + Padding)), Height);
        return new CaptionBarRects(bar, text, skip, stop);
    }
}
