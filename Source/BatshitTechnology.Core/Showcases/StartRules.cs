using System;

namespace BatshitTechnology.Showcases;

/// <summary>
/// What a <c>-showcase=&lt;defName&gt;</c> command line asks the game to
/// do, once the main menu is up.
/// </summary>
public enum CommandLineVerdict
{
    /// <summary>No showcase was asked for; the menu behaves normally.</summary>
    None,

    /// <summary>Start the showcase.</summary>
    Launch,

    /// <summary>
    /// Start the showcase despite a fatal compatibility flag, because
    /// <c>-showcaseforce</c> was also passed.
    /// </summary>
    LaunchForced,

    /// <summary>No loaded showcase has the requested def name.</summary>
    UnknownShowcase,

    /// <summary>
    /// A mod the showcase requires is not active; forcing cannot help.
    /// </summary>
    Unavailable,

    /// <summary>
    /// A loaded mod is flagged fatal, and <c>-showcaseforce</c> was not
    /// passed.
    /// </summary>
    Refused,
}

/// <summary>
/// The Showcases API's rules for starting a showcase that are not about
/// the mod list itself (that is <see cref="LaunchDecision"/>): when Force
/// start is offered, and what the command line does.
/// </summary>
public static class ShowcaseStartRules
{
    /// <summary>
    /// The command-line key naming a showcase to start at once, as in
    /// <c>-showcase=BT_Showcase</c>.
    /// </summary>
    public const string ShowcaseArg = "showcase";

    /// <summary>
    /// The command-line flag that lets <c>-showcase=</c> start a showcase
    /// flagged fatal, as the details page's Force start does. It affects
    /// only the command line; the menu offers Force start whenever a
    /// fatal flag is all that stands in the way.
    /// </summary>
    public const string ForceArg = "showcaseforce";

    /// <summary>
    /// Vanilla's quick-test flag, which skips the main menu and generates
    /// a game at once.
    /// </summary>
    public const string QuickTestArg = "quicktest";

    /// <summary>
    /// Whether the details page shows a Force start button for a
    /// showcase: only a fatal flag stands in its way (docs/SHOWCASE-API.md,
    /// "Other Mods", and Decision 3). It is offered to every player, dev
    /// mode or not, behind a confirmation; a missing requirement can never
    /// be forced.
    /// </summary>
    /// <param name="decision">The showcase's unforced decision.</param>
    /// <returns>True if the button should be shown.</returns>
    public static bool ShowForceButton(LaunchDecision? decision) =>
        decision != null && decision.CanForce;

    /// <summary>
    /// Whether the main menu acts on <c>-showcase=</c> itself. Not when
    /// <see cref="QuickTestArg"/> was passed: vanilla then draws the main
    /// menu for a single frame while its own quick-test game is about to
    /// generate, and a second generation started there would collide
    /// with it. The quick-test game adopts the showcase instead.
    /// </summary>
    /// <param name="quickTestPassed">
    /// Whether <see cref="QuickTestArg"/> was passed.
    /// </param>
    /// <returns>True if the main menu should start the showcase.</returns>
    public static bool MenuActsOnCommandLine(bool quickTestPassed) =>
        !quickTestPassed;

    /// <summary>
    /// Decides what a <c>-showcase=</c> command line does.
    /// </summary>
    /// <param name="requestedDefName">
    /// The def name given on the command line; blank means none.
    /// </param>
    /// <param name="decision">
    /// The showcase's decision, evaluated with the force flag as given on
    /// the command line, or null when no showcase has that name.
    /// </param>
    /// <returns>The verdict.</returns>
    public static CommandLineVerdict ForCommandLine(
        string? requestedDefName, LaunchDecision? decision)
    {
        if (string.IsNullOrWhiteSpace(requestedDefName))
        {
            return CommandLineVerdict.None;
        }

        if (decision == null)
        {
            return CommandLineVerdict.UnknownShowcase;
        }

        switch (decision.Verdict)
        {
            case LaunchVerdict.Unavailable:
                return CommandLineVerdict.Unavailable;
            case LaunchVerdict.Refused:
                return CommandLineVerdict.Refused;
            case LaunchVerdict.Forced:
                return CommandLineVerdict.LaunchForced;
            case LaunchVerdict.Clear:
            case LaunchVerdict.Warned:
                return CommandLineVerdict.Launch;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(decision),
                    decision.Verdict,
                    "Unknown launch verdict.");
        }
    }

    /// <summary>
    /// Whether a verdict starts a game.
    /// </summary>
    /// <param name="verdict">The verdict.</param>
    /// <returns>True for <see cref="CommandLineVerdict.Launch"/> and
    /// <see cref="CommandLineVerdict.LaunchForced"/>.</returns>
    public static bool Starts(CommandLineVerdict verdict) =>
        verdict == CommandLineVerdict.Launch
        || verdict == CommandLineVerdict.LaunchForced;
}
