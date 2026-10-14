// Showcase API - the main menu's Showcases button.
// The mod's only Harmony patch (approved by Matt "Breakpoint" Heck on
// 2026-10-13; docs/SHOWCASE-API.md, "The Main Menu Button Needs
// Harmony"). Vanilla builds the main menu's options as a local list
// inside MainMenuDrawer.DoMainMenuControls and hands it straight to
// OptionListingUtility.DrawOptionListing (RimWorld 1.6.4871), so there is
// no def, virtual method or event to extend. A postfix on
// DrawOptionListing draws one more option, Showcases, under the last one,
// and only for the main menu's own column: in the Entry scene (not the
// in-game menu), never for the column of web links (all
// ListableOption_WebLink), and only for a list that ends with vanilla's
// Quit to OS (Root.Shutdown). The same postfix acts once on the
// -showcase=<defName> command line, because it runs exactly when the main
// menu is ready for input.
//
// Harmony itself comes from the Harmony mod (brrainz.harmony, a declared
// dependency), never from this mod's Assemblies/. Harmony types appear
// only inside InstallPatch, so a missing Harmony fails that one call,
// which is caught and logged, and the class still loads.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace BatshitTechnology.Showcases
{
    /// <summary>
    /// Adds the Showcases button to the bottom of the main menu's column.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ShowcaseMenuButton
    {
        /// <summary>
        /// The Harmony instance id: the mod's package id, then the patch's
        /// purpose.
        /// </summary>
        public const string HarmonyId = "breakpoint8088.BatshitTechnology.Showcases";

        /// <summary>
        /// The gap vanilla leaves between options
        /// (OptionListingUtility.OptionSpacing, private in 1.6.4871).
        /// </summary>
        private const float OptionSpacing = 7f;

        /// <summary>Whether the patch was installed.</summary>
        private static bool installed;

        /// <summary>
        /// Whether the button failed; it is then left off for the session
        /// rather than throwing on every frame of the main menu.
        /// </summary>
        private static bool failed;

        /// <summary>The button, made once and relabelled each draw.</summary>
        private static ListableOption option;

        /// <summary>Installs the patch once the game has loaded its defs.</summary>
        static ShowcaseMenuButton()
        {
            try
            {
                InstallPatch();
                installed = true;
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} could not add the main menu's "
                    + "Showcases button (is the Harmony mod active?): " + e);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the patch is installed.
        /// </summary>
        public static bool Installed => installed;

        /// <summary>
        /// Draws the Showcases button below the main menu's last option and
        /// reports the extra height, as the original does for its own.
        /// </summary>
        /// <param name="rect">The column's area, as the original got it.</param>
        /// <param name="optList">The options the original drew.</param>
        /// <param name="__result">The height the original used.</param>
        public static void Postfix(
            Rect rect, List<ListableOption> optList, ref float __result)
        {
            if (failed || !IsMainMenuColumn(optList))
            {
                return;
            }

            try
            {
                ShowcaseLauncher.OnMainMenuShown();
                if (!ShowcaseLibrary.Any)
                {
                    return;
                }

                option ??= new ListableOption(string.Empty, OpenMenu);
                option.label = "BT_Showcases_MenuButton".Translate();
                Widgets.BeginGroup(rect);
                try
                {
                    __result += option.DrawOption(new Vector2(0f, __result), rect.width)
                        + OptionSpacing;
                }
                finally
                {
                    Widgets.EndGroup();
                }
            }
            catch (Exception e)
            {
                failed = true;
                Log.Error($"{ShowcaseLog.Prefix} the Showcases button failed "
                    + $"and is hidden until restart: {e}");
            }
        }

        /// <summary>
        /// Whether a list is the main menu's own column: drawn in the Entry
        /// scene, holding no web links, and ending with Quit to OS.
        /// </summary>
        /// <param name="optList">The options.</param>
        /// <returns>True for the main menu's column.</returns>
        public static bool IsMainMenuColumn(List<ListableOption> optList)
        {
            if (Current.ProgramState != ProgramState.Entry
                || optList == null || optList.Count == 0)
            {
                return false;
            }

            foreach (ListableOption entry in optList)
            {
                if (entry == null || entry is ListableOption_WebLink)
                {
                    return false;
                }
            }

            MethodInfo last = optList[optList.Count - 1].action?.Method;
            return last != null
                && last.DeclaringType == typeof(Root)
                && last.Name == nameof(Root.Shutdown);
        }

        /// <summary>Opens the first level of the Showcases menu.</summary>
        private static void OpenMenu()
        {
            Find.WindowStack.Add(new Dialog_ShowcaseMods());
        }

        /// <summary>
        /// Patches OptionListingUtility.DrawOptionListing with
        /// <see cref="Postfix"/>. Kept apart and never inlined, so that a
        /// missing Harmony assembly fails here, inside the caller's
        /// try block.
        /// </summary>
        /// <exception cref="MissingMethodException">
        /// Thrown when the game no longer has the method.
        /// </exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InstallPatch()
        {
            MethodInfo target = AccessTools.Method(
                typeof(OptionListingUtility),
                nameof(OptionListingUtility.DrawOptionListing),
                new[] { typeof(Rect), typeof(List<ListableOption>) });
            if (target == null)
            {
                throw new MissingMethodException(
                    nameof(OptionListingUtility),
                    nameof(OptionListingUtility.DrawOptionListing));
            }

            var harmony = new Harmony(HarmonyId);
            harmony.Patch(
                target,
                postfix: new HarmonyMethod(typeof(ShowcaseMenuButton), nameof(Postfix)));
        }
    }
}
