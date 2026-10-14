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
    /// The command-line flag that offers Force start outside dev mode, and
    /// lets <c>-showcase=</c> start a showcase flagged fatal.
    /// </summary>
    public const string ForceArg = "showcaseforce";

    /// <summary>
    /// Whether the details page may offer Force start at all. It is a
    /// testing tool (docs/SHOWCASE-API.md, "Other Mods"), so it appears
    /// only in dev mode or when the game was started with
    /// <see cref="ForceArg"/>.
    /// </summary>
    /// <param name="devMode">Whether dev mode is on.</param>
    /// <param name="forceFlag">Whether <see cref="ForceArg"/> was passed.</param>
    /// <returns>True if Force start may be offered.</returns>
    public static bool MayOfferForce(bool devMode, bool forceFlag) =>
        devMode || forceFlag;

    /// <summary>
    /// Whether the details page shows a Force start button for a
    /// showcase: only a fatal flag stands in its way, and forcing may be
    /// offered.
    /// </summary>
    /// <param name="decision">The showcase's unforced decision.</param>
    /// <param name="devMode">Whether dev mode is on.</param>
    /// <param name="forceFlag">Whether <see cref="ForceArg"/> was passed.</param>
    /// <returns>True if the button should be shown.</returns>
    public static bool ShowForceButton(
        LaunchDecision? decision, bool devMode, bool forceFlag) =>
        decision != null
        && decision.CanForce
        && MayOfferForce(devMode, forceFlag);

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
