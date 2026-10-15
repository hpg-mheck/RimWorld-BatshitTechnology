// Showcase API - the main menu's Showcases button, and the home of the
// mod's two Harmony patches, both made here through one Harmony instance
// under one id (HarmonyId), so that HarmonyLib is named in this file only.
//
// 1. The button (approved by Matt "Breakpoint" Heck on 2026-10-13;
// docs/SHOWCASE-API.md, "The Main Menu Button Needs Harmony"). Vanilla
// builds the main menu's options as a local list inside
// MainMenuDrawer.DoMainMenuControls and hands it straight to
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
// 2. The showcase map's seed (approved by Matt "Breakpoint" Heck on
// 2026-10-14; docs/AI-requests-for-humans.txt, item 22): a prefix on the
// Map.NextGenSeed getter, ShowcaseMapSeed.Prefix, which pins the seeds a
// map's structures are generated from, but only while a showcase's game
// generates its map; everywhere else it passes through to vanilla
// untouched (Source/Showcases/ShowcaseMapSeed.cs).
//
// Each patch is installed on its own, so one that fails (a renamed method
// in a later RimWorld) leaves the other working, and the failure is
// logged. Harmony itself comes from the Harmony mod (brrainz.harmony, a
// declared dependency), never from this mod's Assemblies/. Harmony types
// appear only inside the bodies of InstallPatches, its two helpers and
// DescribeOwnPatches, so a missing Harmony fails those calls, which are
// caught and logged, and the class still loads. The postfix counts its
// calls and the buttons it draws, for the smoke runner
// (Source/SmokeTestShowcases.cs), which also checks that these two
// patches are the mod's only ones.

using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Installs the mod's two patches once the game has loaded its
        /// defs: the button's postfix and the map seed's prefix.
        /// </summary>
        static ShowcaseMenuButton()
        {
            try
            {
                InstallPatches();
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} could not install the Showcases "
                    + "button or the showcase map's seed (is the Harmony mod "
                    + "active?): " + e);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the button's patch is installed.
        /// (<see cref="ShowcaseMapSeed.Installed"/> says the same of the
        /// map seed's.)
        /// </summary>
        public static bool Installed => installed;

        /// <summary>
        /// Gets how many times the postfix has run, for any option list.
        /// The smoke runner compares it before and after drawing a menu, to
        /// prove the patch is live on the method vanilla's menus use.
        /// </summary>
        public static int PostfixCalls { get; private set; }

        /// <summary>
        /// Gets how many Showcases buttons the postfix has drawn, so the
        /// smoke runner can count the buttons one menu draw added.
        /// </summary>
        public static int ButtonsDrawn { get; private set; }

        /// <summary>
        /// Gets the button, once the main menu has drawn it; null before.
        /// Its action is the one a click runs.
        /// </summary>
        internal static ListableOption Button => option;

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
            PostfixCalls++;
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
                    ButtonsDrawn++;
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

        /// <summary>
        /// Describes every Harmony patch that belongs to this mod, one line
        /// each, with its owner's id in brackets
        /// ("Verse.OptionListingUtility.DrawOptionListing: postfix
        /// BatshitTechnology.Showcases.ShowcaseMenuButton.Postfix
        /// [breakpoint8088.BatshitTechnology.Showcases]"), so the smoke
        /// runner can check that the button's postfix and the map seed's
        /// prefix are the mod's only patches, both made under
        /// <see cref="HarmonyId"/>. A patch is the mod's if its owner id
        /// starts with the mod's package id, whatever the rest of the id,
        /// or if its patch method is in one of the mod's assemblies,
        /// whatever the owner. Empty if Harmony is missing.
        /// </summary>
        /// <returns>The lines, sorted.</returns>
        public static List<string> OwnPatches()
        {
            try
            {
                return DescribeOwnPatches();
            }
            catch (Exception e)
            {
                Log.Warning($"{ShowcaseLog.Prefix} could not list the "
                    + $"Harmony patches: {e.Message}");
                return new List<string>();
            }
        }

        /// <summary>Opens the first level of the Showcases menu.</summary>
        private static void OpenMenu()
        {
            Find.WindowStack.Add(new Dialog_ShowcaseMods());
        }

        /// <summary>
        /// Lists this mod's patches through Harmony. Kept apart and never
        /// inlined, like <see cref="InstallPatches"/>, and with Harmony
        /// types only in its body, so the class loads without Harmony.
        /// </summary>
        /// <returns>The lines, sorted.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static List<string> DescribeOwnPatches()
        {
            Assembly own = typeof(ShowcaseMenuButton).Assembly;
            ModContentPack pack = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(m => m?.assemblies?.loadedAssemblies?.Contains(own) == true);
            var assemblies = new HashSet<Assembly>(
                pack?.assemblies?.loadedAssemblies ?? new List<Assembly>()) { own };
            string idPrefix = pack?.PackageId;
            if (idPrefix.NullOrEmpty())
            {
                idPrefix = HarmonyId;
            }

            var lines = new List<string>();
            foreach (MethodBase method in Harmony.GetAllPatchedMethods())
            {
                Patches info = Harmony.GetPatchInfo(method);
                if (info == null)
                {
                    continue;
                }

                string target = $"{method.DeclaringType?.FullName}.{method.Name}";
                var kinds = new (string Kind, IEnumerable<Patch> List)[]
                {
                    ("prefix", info.Prefixes),
                    ("postfix", info.Postfixes),
                    ("transpiler", info.Transpilers),
                    ("finalizer", info.Finalizers),
                };
                foreach ((string kind, IEnumerable<Patch> list) in kinds)
                {
                    foreach (Patch patch in list ?? Array.Empty<Patch>())
                    {
                        MethodInfo m = patch?.PatchMethod;
                        bool ours = patch != null
                            && ((patch.owner?.StartsWith(
                                    idPrefix, StringComparison.OrdinalIgnoreCase) ?? false)
                                || (m?.DeclaringType != null
                                    && assemblies.Contains(m.DeclaringType.Assembly)));
                        if (!ours)
                        {
                            continue;
                        }

                        lines.Add($"{target}: {kind} "
                            + $"{m?.DeclaringType?.FullName}.{m?.Name} [{patch.owner}]");
                    }
                }
            }

            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        /// <summary>
        /// Makes the mod's one Harmony instance and installs both patches
        /// through it, each on its own: a patch that fails is logged and
        /// leaves the other in place. Kept apart and never inlined, so
        /// that a missing Harmony assembly fails here, inside the static
        /// constructor's try block.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InstallPatches()
        {
            var harmony = new Harmony(HarmonyId);
            try
            {
                PatchMenu(harmony);
                installed = true;
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} could not add the main menu's "
                    + "Showcases button: " + e);
            }

            try
            {
                PatchMapSeed(harmony);
                ShowcaseMapSeed.Installed = true;
            }
            catch (Exception e)
            {
                Log.Error($"{ShowcaseLog.Prefix} could not pin showcase maps to "
                    + "their seeds; a showcase's map will differ from run to "
                    + "run: " + e);
            }
        }

        /// <summary>
        /// Patches OptionListingUtility.DrawOptionListing with
        /// <see cref="Postfix"/>.
        /// </summary>
        /// <param name="harmony">The mod's Harmony instance.</param>
        /// <exception cref="MissingMethodException">
        /// Thrown when the game no longer has the method.
        /// </exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchMenu(Harmony harmony)
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

            harmony.Patch(
                target,
                postfix: new HarmonyMethod(typeof(ShowcaseMenuButton), nameof(Postfix)));
        }

        /// <summary>
        /// Patches the Map.NextGenSeed getter with
        /// <see cref="ShowcaseMapSeed.Prefix"/>, after checking that the
        /// getter and vanilla's private counter the prefix advances
        /// (<c>Map.generatedId</c>, an int; RimWorld 1.6.4871) are still
        /// there.
        /// </summary>
        /// <param name="harmony">The mod's Harmony instance.</param>
        /// <exception cref="MissingMethodException">
        /// Thrown when the game no longer has the getter.
        /// </exception>
        /// <exception cref="MissingFieldException">
        /// Thrown when the game no longer has the counter as an int.
        /// </exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchMapSeed(Harmony harmony)
        {
            MethodInfo target = AccessTools.PropertyGetter(
                typeof(Map), nameof(Map.NextGenSeed));
            if (target == null)
            {
                throw new MissingMethodException(nameof(Map), nameof(Map.NextGenSeed));
            }

            if (AccessTools.Field(typeof(Map), "generatedId")?.FieldType != typeof(int))
            {
                throw new MissingFieldException(nameof(Map), "generatedId");
            }

            harmony.Patch(
                target,
                prefix: new HarmonyMethod(
                    typeof(ShowcaseMapSeed), nameof(ShowcaseMapSeed.Prefix)));
        }
    }
}
