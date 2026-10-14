// Showcase API - player-facing text built in code.
// Every string comes from Languages/English/Keyed/BT_Showcases.xml, so it
// can be translated, and the XML tests check that every key used here
// exists there.

using System.Collections.Generic;
using System.Linq;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Builds the menus' and the session's text from translated keys.
    /// </summary>
    public static class ShowcaseTexts
    {
        /// <summary>"About 3 min", from a showcase's estimate.</summary>
        /// <param name="def">The showcase.</param>
        /// <returns>The label.</returns>
        public static string Minutes(ShowcaseDef def) =>
            "BT_Showcases_Minutes".Translate(
                CaptionText.WholeMinutes(def?.estimatedMinutes ?? 1f));

        /// <summary>"1 showcase" or "3 showcases".</summary>
        /// <param name="count">How many.</param>
        /// <returns>The label.</returns>
        public static string Count(int count) =>
            count == 1
                ? "BT_Showcases_CountOne".Translate().Resolve()
                : "BT_Showcases_CountMany".Translate(count).Resolve();

        /// <summary>A severity's name, for the player.</summary>
        /// <param name="severity">The severity.</param>
        /// <returns>The name.</returns>
        public static string Severity(ShowcaseSeverity severity) =>
            ("BT_Showcases_Severity_" + severity).Translate();

        /// <summary>
        /// The names of the mods behind some package ids, joined for a
        /// sentence.
        /// </summary>
        /// <param name="ids">The package ids.</param>
        /// <returns>The joined names.</returns>
        public static string ModNames(IEnumerable<string> ids) =>
            (ids ?? Enumerable.Empty<string>())
                .Select(ShowcaseLibrary.ModName)
                .ToCommaList(useAnd: true);

        /// <summary>
        /// "Requires Biotech." for a showcase with requirements, or null.
        /// </summary>
        /// <param name="def">The showcase.</param>
        /// <returns>The line, or null.</returns>
        public static string Requires(ShowcaseDef def)
        {
            List<string> ids = PackageIds.Distinct(def?.requiredMods).ToList();
            return ids.Count == 0
                ? null
                : "BT_Showcases_Requires".Translate(ModNames(ids)).Resolve();
        }

        /// <summary>
        /// One flagged mod, for the details page: its name, the severity
        /// and the showcase's note.
        /// </summary>
        /// <param name="flag">The flag.</param>
        /// <returns>The line.</returns>
        public static string Flag(CompatibilityFlag flag) =>
            "BT_Showcases_FlagLine".Translate(
                ShowcaseLibrary.ModName(flag.PackageId),
                Severity(flag.Severity),
                flag.Note).Resolve();

        /// <summary>
        /// Why a showcase cannot start, for the player: the missing mods,
        /// or the fatal flags. Null when it can start.
        /// </summary>
        /// <param name="decision">The decision.</param>
        /// <returns>The reason, or null.</returns>
        public static string WhyNot(LaunchDecision decision)
        {
            if (decision == null || decision.CanStart)
            {
                return null;
            }

            if (decision.Verdict == LaunchVerdict.Unavailable)
            {
                return "BT_Showcases_NeedsMods".Translate(
                    ModNames(decision.Requirements.Missing)).Resolve();
            }

            string fatal = decision.FatalMatches
                .Select(f => "BT_Showcases_FatalLine".Translate(
                    ShowcaseLibrary.ModName(f.PackageId), f.Note).Resolve())
                .ToLineList();
            return "BT_Showcases_Refused".Translate(fatal).Resolve();
        }
    }
}
