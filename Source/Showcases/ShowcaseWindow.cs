// Showcase API - the menus' shared window.
// The Showcases menu has three levels (docs/SHOWCASE-API.md, "The
// Menus"): the mods that offer showcases, one mod's showcases, and one
// showcase's details. Only one level is open at a time: moving between
// them replaces the window, so Back returns one level and Escape, which
// closes the open window, closes the whole menu. The windows use
// vanilla's dialog look so they read as part of the game.

using System;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// The base of the three Showcases menu windows: size, title, body
    /// and footer, navigation, and shared drawing helpers.
    /// </summary>
    public abstract class ShowcaseWindow : Window
    {
        /// <summary>The title row's height.</summary>
        protected const float TitleHeight = 42f;

        /// <summary>The footer row's height, for the buttons.</summary>
        protected const float FooterHeight = 44f;

        /// <summary>A footer button's size.</summary>
        protected static readonly Vector2 ButtonSize = new Vector2(150f, 38f);

        /// <summary>A warning's colour (vanilla's tip-title yellow).</summary>
        protected static readonly Color WarningColor = ColoredText.TipSectionTitleColor;

        /// <summary>A refusal's colour (vanilla's threat red).</summary>
        protected static readonly Color RefusalColor = ColoredText.ThreatColor;

        /// <summary>Muted text (vanilla's subtle grey).</summary>
        protected static readonly Color MutedColor = ColoredText.SubtleGrayColor;

        /// <summary>The backdrop behind a preview picture.</summary>
        private static readonly Color PreviewBackdrop = new Color(0f, 0f, 0f, 0.35f);

        /// <summary>Creates a window with the menus' settings.</summary>
        protected ShowcaseWindow()
        {
            doCloseX = true;
            closeOnCancel = true;
            closeOnAccept = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
            onlyOneOfTypeAllowed = true;
        }

        /// <summary>
        /// Gets the window's size: generous, but never more than the
        /// screen less a margin (2x UI scale on a small window included).
        /// </summary>
        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(840f, UI.screenWidth - 24f),
            Mathf.Min(640f, UI.screenHeight - 24f));

        /// <summary>Gets the window's title.</summary>
        protected abstract string Title { get; }

        /// <summary>
        /// Lays the window out: title, body, footer. A failure is logged
        /// once and the window closes, rather than throwing every frame.
        /// </summary>
        /// <param name="inRect">The window's inner area.</param>
        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.LabelEllipses(
                    new Rect(inRect.x, inRect.y, inRect.width - 32f, TitleHeight),
                    Title);
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                var body = new Rect(
                    inRect.x,
                    inRect.y + TitleHeight,
                    inRect.width,
                    inRect.height - TitleHeight - FooterHeight - 6f);
                var footer = new Rect(
                    inRect.x, inRect.yMax - FooterHeight, inRect.width, FooterHeight);
                DoBody(body);
                DoFooter(footer);
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} the {GetType().Name} window "
                    + $"failed and was closed: {e}");
                Close(doCloseSound: false);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>Draws the window's body.</summary>
        /// <param name="rect">The body's area.</param>
        protected abstract void DoBody(Rect rect);

        /// <summary>Draws the window's footer buttons.</summary>
        /// <param name="rect">The footer's area.</param>
        protected abstract void DoFooter(Rect rect);

        /// <summary>
        /// Replaces this window with another level of the menu.
        /// </summary>
        /// <param name="next">The window to open.</param>
        protected void GoTo(Window next)
        {
            if (next == null)
            {
                return;
            }

            Find.WindowStack.Add(next);
            Close(doCloseSound: false);
        }

        /// <summary>A footer button at the footer's left end.</summary>
        /// <param name="footer">The footer.</param>
        /// <returns>The button's area.</returns>
        protected static Rect LeftButton(Rect footer) => new Rect(
            footer.x,
            footer.y + ((footer.height - ButtonSize.y) / 2f),
            ButtonSize.x,
            ButtonSize.y);

        /// <summary>
        /// A footer button at the footer's right end, or further left.
        /// </summary>
        /// <param name="footer">The footer.</param>
        /// <param name="fromRight">0 for the rightmost button, 1 for the next.</param>
        /// <returns>The button's area.</returns>
        protected static Rect RightButton(Rect footer, int fromRight) => new Rect(
            footer.xMax - ((fromRight + 1) * (ButtonSize.x + 10f)) + 10f,
            footer.y + ((footer.height - ButtonSize.y) / 2f),
            ButtonSize.x,
            ButtonSize.y);

        /// <summary>
        /// Draws a preview picture fitted into a box, or an empty box.
        /// </summary>
        /// <param name="rect">The box.</param>
        /// <param name="texture">The picture; may be null.</param>
        protected static void DrawPreview(Rect rect, Texture2D texture)
        {
            Widgets.DrawBoxSolid(rect, PreviewBackdrop);
            if (texture != null)
            {
                Widgets.DrawTextureFitted(rect, texture, 1f);
            }

            Widgets.DrawBox(rect);
        }

        /// <summary>
        /// Draws one line of text in a colour, and returns the height used.
        /// </summary>
        /// <param name="x">The left edge.</param>
        /// <param name="y">The top edge.</param>
        /// <param name="width">The width.</param>
        /// <param name="text">The text; nothing is drawn if blank.</param>
        /// <param name="color">The colour.</param>
        /// <returns>The height used.</returns>
        protected static float DrawLine(
            float x, float y, float width, string text, Color color)
        {
            if (text.NullOrEmpty())
            {
                return 0f;
            }

            float height = Text.CalcHeight(text, width);
            GUI.color = color;
            Widgets.Label(new Rect(x, y, width, height), text);
            GUI.color = Color.white;
            return height;
        }
    }
}
