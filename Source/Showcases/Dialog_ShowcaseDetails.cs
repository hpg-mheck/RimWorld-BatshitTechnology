// Showcase API - menu level 3: one showcase's details, Start and Back.

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// One showcase: its preview, description, running time and
    /// requirements, any loaded mod it flags, and Start and Back. A fatal
    /// flag refuses Start with the reason; Force start (behind a
    /// confirmation) is offered only in dev mode or when the game was
    /// started with <c>-showcaseforce</c>, since it exists for testing and
    /// for reproducing the crash.
    /// </summary>
    public class Dialog_ShowcaseDetails : ShowcaseWindow
    {
        /// <summary>The showcase.</summary>
        private readonly ShowcaseDef def;

        /// <summary>The mod it belongs to, for Back.</summary>
        private readonly ShowcaseModGroup group;

        /// <summary>Its unforced decision, read when the window opens.</summary>
        private readonly LaunchDecision decision;

        /// <summary>The description's scroll position.</summary>
        private Vector2 scroll;

        /// <summary>Creates the window.</summary>
        /// <param name="def">The showcase.</param>
        /// <param name="group">Its mod's group, for Back; may be null.</param>
        public Dialog_ShowcaseDetails(ShowcaseDef def, ShowcaseModGroup group)
        {
            this.def = def;
            this.group = group;
            decision = ShowcaseLibrary.Evaluate(def, force: false);
        }

        /// <summary>Gets the title: the showcase's name.</summary>
        protected override string Title => def.LabelCap;

        /// <summary>
        /// Draws the preview and facts on the left and the description on
        /// the right.
        /// </summary>
        /// <param name="rect">The body's area.</param>
        protected override void DoBody(Rect rect)
        {
            float leftWidth = Mathf.Min(380f, rect.width * 0.46f);
            var left = new Rect(rect.x, rect.y, leftWidth, rect.height);
            var right = new Rect(
                left.xMax + 14f, rect.y, rect.width - leftWidth - 14f, rect.height);

            var preview = new Rect(left.x, left.y, left.width, left.width * 9f / 16f);
            DrawPreview(preview, def.PreviewTexture);
            float y = preview.yMax + 8f;
            foreach ((string text, Color color) in Facts())
            {
                if (y >= left.yMax)
                {
                    break;
                }

                y += DrawLine(left.x, y, left.width, text, color) + 4f;
            }

            string description = def.description ?? string.Empty;
            float height = Text.CalcHeight(description, right.width - 16f);
            var view = new Rect(0f, 0f, right.width - 16f, height);
            Widgets.BeginScrollView(right, ref scroll, view);
            try
            {
                Widgets.Label(view, description);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>
        /// Draws Back, Start and, when offered, Force start.
        /// </summary>
        /// <param name="rect">The footer's area.</param>
        protected override void DoFooter(Rect rect)
        {
            if (Widgets.ButtonText(LeftButton(rect), "BT_Showcases_Back".Translate()))
            {
                GoTo(group != null
                    ? new Dialog_ModShowcases(group)
                    : (Window)new Dialog_ShowcaseMods());
                return;
            }

            Rect start = RightButton(rect, 0);
            TooltipHandler.TipRegion(start, decision.CanStart
                ? "BT_Showcases_StartTip".Translate()
                : (TaggedString)ShowcaseTexts.WhyNot(decision));
            if (Widgets.ButtonText(start, "BT_Showcases_Start".Translate(),
                active: decision.CanStart))
            {
                if (decision.CanStart)
                {
                    StartShowcase(forced: false);
                }
            }

            if (ShowcaseStartRules.ShowForceButton(
                decision, Prefs.DevMode, ShowcaseLauncher.ForceFlagPassed))
            {
                Rect force = RightButton(rect, 1);
                TooltipHandler.TipRegion(force, "BT_Showcases_ForceTip".Translate());
                if (Widgets.ButtonText(force, "BT_Showcases_ForceStart".Translate()))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "BT_Showcases_ForceConfirm".Translate(
                            ShowcaseTexts.WhyNot(decision)),
                        () => StartShowcase(forced: true),
                        destructive: true));
                }
            }
        }

        /// <summary>
        /// The lines under the preview: running time, requirements, the
        /// flagged mods and, if Start is refused, why.
        /// </summary>
        /// <returns>Each line and its colour.</returns>
        private IEnumerable<(string Text, Color Color)> Facts()
        {
            yield return (ShowcaseTexts.Minutes(def), MutedColor);
            string requires = ShowcaseTexts.Requires(def);
            if (requires != null)
            {
                yield return (requires, decision.Requirements.Met
                    ? MutedColor
                    : RefusalColor);
            }

            foreach (CompatibilityFlag flag in decision.Matches)
            {
                yield return (ShowcaseTexts.Flag(flag),
                    flag.Severity == ShowcaseSeverity.Fatal
                        ? RefusalColor
                        : WarningColor);
            }

            if (!decision.CanStart)
            {
                yield return (ShowcaseTexts.WhyNot(decision), RefusalColor);
            }
        }

        /// <summary>
        /// Starts the showcase and closes the menu, or explains why it
        /// could not start.
        /// </summary>
        /// <param name="forced">Whether Force start was confirmed.</param>
        private void StartShowcase(bool forced)
        {
            if (ShowcaseLauncher.Launch(def, forced, out string refusal))
            {
                Close(doCloseSound: false);
                return;
            }

            Messages.Message(refusal, MessageTypeDefOf.RejectInput, false);
        }
    }
}
