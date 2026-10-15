// Showcase API - drawing the narration overlay.
// The session (GameComponent_ShowcaseSession) owns a Core NarrationOverlay,
// which decides what shows and how visible it is; this draws it: one line,
// white, about twice the Medium font, centred in the lower third, over a
// soft dark drop shadow (Core's NarrationOverlayLayout). Nothing here
// plays a sound or takes a click.

using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Draws the narration overlay's lines, and supplies the real-time
    /// clock it runs on.
    /// </summary>
    internal static class NarrationOverlayDrawer
    {
        /// <summary>
        /// The Medium font's size if the font does not report one (Unity
        /// reports 0 for a font imported without a size).
        /// </summary>
        private const int FallbackMediumSize = 20;

        /// <summary>The most measured lines remembered.</summary>
        private const int MeasureCacheSize = 16;

        /// <summary>Measured lines: text and width to size and height.</summary>
        private static readonly Dictionary<(string, float), (int Size, float Height)>
            Measured = new Dictionary<(string, float), (int, float)>();

        /// <summary>Reused content, so drawing allocates nothing.</summary>
        private static readonly GUIContent Content = new GUIContent();

        /// <summary>The overlay's style, made on first use.</summary>
        private static GUIStyle style;

        /// <summary>The Medium font's size.</summary>
        private static int mediumSize;

        /// <summary>
        /// Whether the font can be drawn at any size (a dynamic font);
        /// otherwise lines are drawn at the Medium size.
        /// </summary>
        private static bool resizable;

        /// <summary>
        /// Gets the real time, in seconds, the overlay's rules run on:
        /// people read in real time, whatever the game's speed.
        /// </summary>
        internal static double Now => Time.realtimeSinceStartup;

        /// <summary>
        /// Gets the font size the last line was drawn at, for the smoke
        /// runner; 0 before any.
        /// </summary>
        internal static int LastFontSize { get; private set; }

        /// <summary>Gets the Medium font's size, for the smoke runner.</summary>
        internal static int MediumSize => mediumSize;

        /// <summary>
        /// Draws the overlay at a time: the line being replaced, then the
        /// line showing. Repaint events only. The caller restores GUI
        /// state.
        /// </summary>
        /// <param name="overlay">The overlay.</param>
        /// <param name="now">The real time, in seconds.</param>
        /// <param name="topLimit">The highest a line may start.</param>
        /// <returns>True if any text was drawn.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="overlay"/> is null.
        /// </exception>
        internal static bool Draw(NarrationOverlay overlay, double now, float topLimit)
        {
            if (overlay == null)
            {
                throw new ArgumentNullException(nameof(overlay));
            }

            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return false;
            }

            bool drew = DrawLine(
                overlay.OutgoingLine, overlay.OutgoingAlpha(now), topLimit);
            drew |= DrawLine(overlay.CurrentLine, overlay.Alpha(now), topLimit);
            return drew;
        }

        /// <summary>
        /// Draws one line, if visible: the shadow's passes in black, then
        /// the text in white, all at the line's opacity.
        /// </summary>
        /// <param name="text">The line; may be null.</param>
        /// <param name="alpha">How visible it is, 0 to 1.</param>
        /// <param name="topLimit">The highest it may start.</param>
        /// <returns>True if it was drawn.</returns>
        private static bool DrawLine(string text, double alpha, float topLimit)
        {
            if (text.NullOrEmpty() || !(alpha > 0.001))
            {
                return false;
            }

            GUIStyle s = Style();
            float width = NarrationOverlayLayout.TextWidth(UI.screenWidth);
            (int size, float height) = Measure(s, text, width);
            s.fontSize = resizable ? size : 0;
            LastFontSize = resizable ? size : mediumSize;
            UiBox box = NarrationOverlayLayout.Compute(
                UI.screenWidth, UI.screenHeight, height, topLimit);
            var rect = new Rect(box.X, box.Y, box.Width, box.Height);
            float a = Mathf.Clamp01((float)alpha);
            Content.text = text;
            foreach (ShadowPass pass in NarrationOverlayLayout.ShadowPasses)
            {
                GUI.color = new Color(0f, 0f, 0f, a * pass.Alpha);
                GUI.Label(
                    new Rect(rect.x + pass.X, rect.y + pass.Y, rect.width, rect.height),
                    Content,
                    s);
            }

            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(rect, Content, s);
            return true;
        }

        /// <summary>
        /// The size a line is drawn at and its wrapped height, remembered
        /// per line and width.
        /// </summary>
        /// <param name="s">The overlay's style.</param>
        /// <param name="text">The line.</param>
        /// <param name="width">The width it wraps to.</param>
        /// <returns>The font size and the height.</returns>
        private static (int Size, float Height) Measure(
            GUIStyle s, string text, float width)
        {
            if (Measured.TryGetValue((text, width), out (int Size, float Height) known))
            {
                return known;
            }

            Content.text = text;
            int size = resizable
                ? NarrationOverlayLayout.FitFontSize(
                    mediumSize, z => LinesAt(s, z, width))
                : mediumSize;
            s.fontSize = resizable ? size : 0;
            float height = s.CalcHeight(Content, width);
            if (Measured.Count >= MeasureCacheSize)
            {
                Measured.Clear();
            }

            Measured[(text, width)] = (size, height);
            return (size, height);
        }

        /// <summary>
        /// How many lines <see cref="Content"/> wraps to at a font size.
        /// </summary>
        /// <param name="s">The overlay's style.</param>
        /// <param name="size">The font size.</param>
        /// <param name="width">The width it wraps to.</param>
        /// <returns>The number of lines; at least 1.</returns>
        private static int LinesAt(GUIStyle s, int size, float width)
        {
            s.fontSize = size;
            float line = s.lineHeight;
            if (!(line > 0f))
            {
                return 1;
            }

            return Math.Max(1, Mathf.RoundToInt(s.CalcHeight(Content, width) / line));
        }

        /// <summary>
        /// Measures a line as the overlay would draw it on a screen of a
        /// given width in UI units (a 3840-pixel screen at 2x UI scale is
        /// 1,920): the font size it fits at, the lines it wraps to, and
        /// any character the font has no glyph for. Layout is measured in
        /// UI units, so the UI scale changes how sharp the text is, not
        /// where it wraps. Call it from inside OnGUI (the smoke runner).
        /// </summary>
        /// <param name="text">The line.</param>
        /// <param name="screenWidth">The screen's width in UI units.</param>
        /// <returns>
        /// The font size, the number of lines, and the characters with no
        /// glyph (empty when all have one).
        /// </returns>
        internal static (int Size, int Lines, string Missing) Probe(
            string text, float screenWidth)
        {
            GUIStyle s = Style();
            float width = NarrationOverlayLayout.TextWidth(screenWidth);
            Content.text = text ?? string.Empty;
            int size = resizable
                ? NarrationOverlayLayout.FitFontSize(
                    mediumSize, z => LinesAt(s, z, width))
                : mediumSize;
            s.fontSize = resizable ? size : 0;
            float line = s.lineHeight;
            int lines = line > 0f
                ? Math.Max(1, Mathf.RoundToInt(s.CalcHeight(Content, width) / line))
                : 1;
            return (size, lines, MissingGlyphs(s.font, Content.text, size));
        }

        /// <summary>
        /// The characters of a line a font cannot draw, each once. Spaces
        /// and other blanks are not checked (they have no glyph to draw).
        /// </summary>
        /// <param name="font">The font, or null.</param>
        /// <param name="text">The line.</param>
        /// <param name="size">The size it is drawn at.</param>
        /// <returns>The characters with no glyph; empty when all have one.</returns>
        private static string MissingGlyphs(Font font, string text, int size)
        {
            if (font == null)
            {
                return "(no font)";
            }

            if (font.dynamic)
            {
                font.RequestCharactersInTexture(text, size);
            }

            var missing = new List<char>();
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c) || missing.Contains(c))
                {
                    continue;
                }

                bool drawn = font.dynamic
                    ? font.GetCharacterInfo(c, out CharacterInfo info, size)
                        && info.advance > 0
                    : font.HasCharacter(c);
                if (!drawn)
                {
                    missing.Add(c);
                }
            }

            return new string(missing.ToArray());
        }

        /// <summary>
        /// The overlay's style: vanilla's Medium label, centred, wrapping,
        /// white, without rich text (whose colours would tint the shadow).
        /// </summary>
        /// <returns>The style.</returns>
        private static GUIStyle Style()
        {
            if (style != null)
            {
                return style;
            }

            GUIStyle medium = Text.fontStyles[(int)GameFont.Medium];
            var made = new GUIStyle(medium)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                richText = false,
                clipping = TextClipping.Overflow,
            };
            made.normal.textColor = Color.white;
            Font font = medium.font;
            resizable = font != null && font.dynamic;
            mediumSize = font != null && font.fontSize > 0
                ? font.fontSize
                : FallbackMediumSize;
            if (!resizable)
            {
                Log.Warning($"{ShowcaseLog.Prefix} the Medium font cannot be "
                    + "resized; narration is drawn at its own size.");
            }

            style = made;
            return style;
        }
    }
}
