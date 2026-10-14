// Showcase API - menu level 2: one mod's showcases.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Lists one mod's showcases: each one's title, short description,
    /// estimated running time and requirements. A showcase whose
    /// requirements are not met is listed greyed out, with the reason,
    /// and cannot be opened; one that flags a loaded mod says so.
    /// </summary>
    public class Dialog_ModShowcases : ShowcaseWindow
    {
        /// <summary>A row's height.</summary>
        private const float RowHeight = 104f;

        /// <summary>The mod.</summary>
        private readonly ShowcaseModGroup group;

        /// <summary>Its showcases and their decisions, read once.</summary>
        private readonly List<(ShowcaseDef Def, LaunchDecision Decision)> rows;

        /// <summary>The list's scroll position.</summary>
        private Vector2 scroll;

        /// <summary>Creates the window for one mod.</summary>
        /// <param name="group">The mod's group.</param>
        public Dialog_ModShowcases(ShowcaseModGroup group)
        {
            this.group = group;
            rows = ShowcaseLibrary.DefsIn(group)
                .Select(d => (d, ShowcaseLibrary.Evaluate(d, force: false)))
                .ToList();
        }

        /// <summary>Gets the mod whose showcases are listed.</summary>
        public ShowcaseModGroup Group => group;

        /// <summary>Gets how many showcases are listed.</summary>
        public int RowCount => rows.Count;

        /// <summary>Gets the title: the mod's name.</summary>
        protected override string Title =>
            "BT_Showcases_ModTitle".Translate(ShowcaseLibrary.GroupName(group));

        /// <summary>
        /// Whether a showcase can be opened from the list. One whose
        /// required mods are missing is greyed out instead; forcing cannot
        /// help it, because its content is not there.
        /// </summary>
        /// <param name="decision">The showcase's decision.</param>
        /// <returns>True if its row can be opened.</returns>
        public static bool IsAvailable(LaunchDecision decision) =>
            decision != null && decision.Verdict != LaunchVerdict.Unavailable;

        /// <summary>The showcase on a row.</summary>
        /// <param name="index">The row.</param>
        /// <returns>The showcase, or null if there is no such row.</returns>
        public ShowcaseDef DefAt(int index) =>
            index >= 0 && index < rows.Count ? rows[index].Def : null;

        /// <summary>The decision shown on a row.</summary>
        /// <param name="index">The row.</param>
        /// <returns>The decision, or null if there is no such row.</returns>
        public LaunchDecision DecisionAt(int index) =>
            index >= 0 && index < rows.Count ? rows[index].Decision : null;

        /// <summary>
        /// Opens a showcase's details in place of this window, as clicking
        /// its row does. A greyed-out showcase does not open.
        /// </summary>
        /// <param name="index">The showcase's row.</param>
        /// <returns>True if its details opened.</returns>
        public bool OpenShowcase(int index)
        {
            if (index < 0 || index >= rows.Count
                || !IsAvailable(rows[index].Decision))
            {
                return false;
            }

            GoTo(new Dialog_ShowcaseDetails(rows[index].Def, group));
            return true;
        }

        /// <summary>
        /// Goes back one level, to the list of mods, as Back does.
        /// </summary>
        public void Back()
        {
            GoTo(new Dialog_ShowcaseMods());
        }

        /// <summary>
        /// The row's status line: why it is unavailable or refused, or the
        /// first flagged mod, or nothing.
        /// </summary>
        /// <param name="decision">The decision.</param>
        /// <returns>The line, or null.</returns>
        public static string StatusLine(LaunchDecision decision)
        {
            switch (decision?.Verdict)
            {
                case LaunchVerdict.Unavailable:
                case LaunchVerdict.Refused:
                    return ShowcaseTexts.WhyNot(decision)?.Replace('\n', ' ');
                case LaunchVerdict.Warned:
                    return decision.Matches.Count == 1
                        ? ShowcaseTexts.Flag(decision.Matches[0])
                        : "BT_Showcases_FlaggedMany".Translate(decision.Matches.Count)
                            .Resolve();
                default:
                    return null;
            }
        }

        /// <summary>Draws the list of showcases.</summary>
        /// <param name="rect">The body's area.</param>
        protected override void DoBody(Rect rect)
        {
            if (rows.Count == 0)
            {
                DrawLine(rect.x, rect.y, rect.width,
                    "BT_Showcases_NoneLoaded".Translate(), Color.white);
                return;
            }

            var view = new Rect(0f, 0f, rect.width - 16f, rows.Count * RowHeight);
            Widgets.BeginScrollView(rect, ref scroll, view);
            try
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = new Rect(0f, i * RowHeight, view.width, RowHeight - 4f);
                    if (DrawRow(row, rows[i].Def, rows[i].Decision))
                    {
                        OpenShowcase(i);
                        break;
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>Draws Back.</summary>
        /// <param name="rect">The footer's area.</param>
        protected override void DoFooter(Rect rect)
        {
            if (Widgets.ButtonText(LeftButton(rect), "BT_Showcases_Back".Translate()))
            {
                Back();
            }
        }

        /// <summary>
        /// Draws one showcase's row, greyed out if it is unavailable.
        /// </summary>
        /// <param name="row">The row's area.</param>
        /// <param name="def">The showcase.</param>
        /// <param name="decision">Its decision.</param>
        /// <returns>True if an available row was clicked.</returns>
        private static bool DrawRow(Rect row, ShowcaseDef def, LaunchDecision decision)
        {
            bool available = IsAvailable(decision);
            Widgets.DrawOptionBackground(row, false);
            float previewHeight = Mathf.Min(row.height - 12f, 72f);
            var preview = new Rect(
                row.x + 6f, row.y + 6f, previewHeight * 16f / 9f, previewHeight);
            GUI.color = available ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            Widgets.DrawTextureFitted(preview, BaseContent.BlackTex, 1f, 0.35f);
            if (def.PreviewTexture != null)
            {
                Widgets.DrawTextureFitted(preview, def.PreviewTexture, 1f);
            }

            Widgets.DrawBox(preview);
            float x = preview.xMax + 12f;
            float width = row.xMax - x - 8f;
            float y = row.y + 4f;
            Text.Font = GameFont.Medium;
            Widgets.LabelEllipses(new Rect(x, y, width, 30f), def.LabelCap);
            Text.Font = GameFont.Small;
            y += 30f;
            GUI.color = available ? Color.white : MutedColor;
            Widgets.LabelEllipses(new Rect(x, y, width, 22f), def.SummaryText);
            y += 22f;
            string facts = ShowcaseTexts.Minutes(def);
            string requires = ShowcaseTexts.Requires(def);
            if (requires != null)
            {
                facts = "BT_Showcases_FactsPair".Translate(facts, requires);
            }

            GUI.color = available ? MutedColor : new Color(0.6f, 0.6f, 0.6f, 0.6f);
            Widgets.LabelEllipses(new Rect(x, y, width, 22f), facts);
            y += 22f;
            GUI.color = Color.white;
            string status = StatusLine(decision);
            if (status != null)
            {
                GUI.color = decision.Verdict == LaunchVerdict.Warned
                    ? WarningColor
                    : RefusalColor;
                Widgets.LabelEllipses(new Rect(x, y, width, 22f), status);
                TooltipHandler.TipRegion(row, status);
            }

            GUI.color = Color.white;
            if (!available)
            {
                return false;
            }

            Widgets.DrawHighlightIfMouseover(row);
            return Widgets.ButtonInvisible(row);
        }
    }
}
