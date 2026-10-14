using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>
/// The Tractor Barn's repair dock (docs/TRACTOR-BOMB.md, "The Tractor
/// Barn"): when a damaged robot comes home, how fast the barn mends it and
/// how much steel that takes. Kept free of RimWorld types so it can be
/// unit tested; the barn and the docking job live in
/// Source/TractorBarn.cs.
/// </summary>
/// <remarks>
/// A robot's health is RimWorld's summary health, from 0 to 1. The barn
/// mends one hit point of the lightest injury every
/// <see cref="TicksPerStep"/> ticks, and once no injury is left, refits
/// one missing part (a wheel, the emitter) per step, as vanilla's mech
/// repair does. Steel comes out of the barn's repair hopper as it goes.
/// </remarks>
public static class TractorBarnRepair
{
    /// <summary>
    /// Below this health a robot with no target goes home for repairs:
    /// "about 50%".
    /// </summary>
    public const double SeekRepairBelow = 0.5;

    /// <summary>Ticks between repair steps: half a second.</summary>
    public const int TicksPerStep = TractorBombTiming.TicksPerSecond / 2;

    /// <summary>Hit points mended per step.</summary>
    public const float HitPointsPerStep = 1f;

    /// <summary>Steel used per hit point mended: one unit per five.</summary>
    public const float SteelPerHitPoint = 0.2f;

    /// <summary>Steel used to refit one missing part.</summary>
    public const float SteelPerPart = 2f;

    /// <summary>
    /// Whether a robot should go home for repairs: its health is below
    /// <see cref="SeekRepairBelow"/>. A value that is not a number does
    /// not send it (it keeps patrolling, the safe mistake).
    /// </summary>
    /// <param name="health">Its summary health, from 0 to 1.</param>
    /// <returns>True if it should.</returns>
    public static bool ShouldSeekRepair(double health) =>
        !double.IsNaN(health) && health < SeekRepairBelow;

    /// <summary>The steel one repair step costs.</summary>
    /// <param name="refitsPart">
    /// Whether the step refits a missing part rather than mending an
    /// injury.
    /// </param>
    /// <returns>The steel, in units.</returns>
    public static float StepCost(bool refitsPart) =>
        refitsPart ? SteelPerPart : SteelPerHitPoint * HitPointsPerStep;

    /// <summary>
    /// Whether the barn's hopper can pay for a step. A hopper reading that
    /// is not a number cannot.
    /// </summary>
    /// <param name="steel">The steel in the hopper.</param>
    /// <param name="cost">The step's cost.</param>
    /// <returns>True if it can.</returns>
    public static bool CanAfford(double steel, double cost) =>
        !double.IsNaN(steel) && !double.IsNaN(cost)
        && steel + 1e-4 >= Math.Max(0d, cost);

    /// <summary>
    /// The steps that mend one injury: one per hit point, the last part
    /// of a hit point taking a whole step, since each step mends one
    /// injury.
    /// </summary>
    /// <param name="severity">
    /// The injury's hit points; negative or not a number is none.
    /// </param>
    /// <returns>The steps.</returns>
    public static long StepsToMend(double severity) =>
        double.IsNaN(severity) || severity <= 0
            ? 0
            : (long)Math.Ceiling((severity / HitPointsPerStep) - 1e-6);

    /// <summary>
    /// The steel a whole repair takes: the given injuries, each mended in
    /// its own steps, and so many missing parts.
    /// </summary>
    /// <param name="injuries">
    /// Each injury's hit points; null counts as none.
    /// </param>
    /// <param name="missingParts">
    /// The missing parts; negative counts as none.
    /// </param>
    /// <returns>The steel, in units.</returns>
    public static double SteelFor(
        IEnumerable<double>? injuries, int missingParts) =>
        (Steps(injuries) * (double)StepCost(false))
        + (Math.Max(0, missingParts) * (double)SteelPerPart);

    /// <summary>
    /// How long a whole repair takes, in ticks: each injury's steps and
    /// one step per missing part.
    /// </summary>
    /// <param name="injuries">
    /// Each injury's hit points; null counts as none.
    /// </param>
    /// <param name="missingParts">
    /// The missing parts; negative counts as none.
    /// </param>
    /// <returns>The ticks.</returns>
    public static long TicksFor(
        IEnumerable<double>? injuries, int missingParts) =>
        (Steps(injuries) + Math.Max(0, missingParts)) * (long)TicksPerStep;

    /// <summary>The steps that mend every injury.</summary>
    /// <param name="injuries">Each injury's hit points; may be null.</param>
    /// <returns>The steps.</returns>
    private static long Steps(IEnumerable<double>? injuries) =>
        injuries?.Sum(StepsToMend) ?? 0;
}
