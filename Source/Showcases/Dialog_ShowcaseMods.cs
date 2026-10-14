// Showcase API - menu level 1: the mods that offer showcases.

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Lists every mod that offers at least one showcase, with its preview
    /// picture, its name and how many showcases it offers. Choosing one
    /// opens that mod's showcases.
    /// </summary>
    public class Dialog_ShowcaseMods : ShowcaseWindow
    {
        /// <summary>A row's height.</summary>
        private const float RowHeight = 84f;

        /// <summary>The mods, grouped once when the window opens.</summary>
        private readonly IReadOnlyList<ShowcaseModGroup> groups;

        /// <summary>The list's scroll position.</summary>
        private Vector2 scroll;

        /// <summary>Creates the window, reading the loaded showcases.</summary>
        public Dialog_ShowcaseMods()
        {
            groups = ShowcaseLibrary.Groups();
        }

        /// <summary>Gets the mods listed, in menu order.</summary>
        public IReadOnlyList<ShowcaseModGroup> Groups => groups;

        /// <summary>Gets the title.</summary>
        protected override string Title => "BT_Showcases_ModsTitle".Translate();

        /// <summary>
        /// Opens one mod's showcases in place of this window, as clicking
        /// its row does.
        /// </summary>
        /// <param name="index">The mod's row.</param>
        /// <returns>False if there is no such row.</returns>
        public bool OpenGroup(int index)
        {
            if (index < 0 || index >= groups.Count)
            {
                return false;
            }

            GoTo(new Dialog_ModShowcases(groups[index]));
            return true;
        }

        /// <summary>Draws the introduction and the list of mods.</summary>
        /// <param name="rect">The body's area.</param>
        protected override void DoBody(Rect rect)
        {
            float y = rect.y;
            y += DrawLine(rect.x, y, rect.width,
                "BT_Showcases_ModsIntro".Translate(), MutedColor);
            y += DrawLine(rect.x, y, rect.width,
                "BT_Showcases_ModsSaves".Translate(), MutedColor) + 8f;
            var listRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            if (groups.Count == 0)
            {
                DrawLine(listRect.x, listRect.y, listRect.width,
                    "BT_Showcases_NoneLoaded".Translate(), Color.white);
                return;
            }

            var view = new Rect(0f, 0f, listRect.width - 16f, groups.Count * RowHeight);
            Widgets.BeginScrollView(listRect, ref scroll, view);
            try
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    var row = new Rect(0f, i * RowHeight, view.width, RowHeight - 4f);
                    if (DrawRow(row, groups[i]))
                    {
                        OpenGroup(i);
                        break;
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>Draws Close.</summary>
        /// <param name="rect">The footer's area.</param>
        protected override void DoFooter(Rect rect)
        {
            if (Widgets.ButtonText(LeftButton(rect), "CloseButton".Translate()))
            {
                Close();
            }
        }

        /// <summary>Draws one mod's row.</summary>
        /// <param name="row">The row's area.</param>
        /// <param name="group">The mod.</param>
        /// <returns>True if the row was clicked.</returns>
        private static bool DrawRow(Rect row, ShowcaseModGroup group)
        {
            Widgets.DrawOptionBackground(row, false);
            float previewHeight = row.height - 12f;
            var preview = new Rect(
                row.x + 6f, row.y + 6f, previewHeight * 16f / 9f, previewHeight);
            DrawPreview(preview, ShowcaseLibrary.ModPreview(group));
            float textX = preview.xMax + 12f;
            float textWidth = row.xMax - textX - 8f;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.LowerLeft;
            Widgets.LabelEllipses(
                new Rect(textX, row.y, textWidth, row.height / 2f),
                ShowcaseLibrary.GroupName(group));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            DrawLine(textX, row.y + (row.height / 2f) + 2f, textWidth,
                ShowcaseTexts.Count(group.Count), MutedColor);
            return Widgets.ButtonInvisible(row);
        }
    }
}
