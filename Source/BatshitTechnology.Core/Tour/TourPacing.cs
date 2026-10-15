using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Showcases;

namespace BatshitTechnology.Core;

/// <summary>
/// The tour's pacing on the narration overlay, at normal speed: each line
/// is read in full, fades out, and leaves the screen clear for
/// <see cref="NarrationTiming.LeadSeconds"/> before the next is due, as
/// the operator asked ("fading in at the start of a segment, out a few
/// seconds before the next one starts"). The def's delays are checked
/// against these rules, and each scene holds its own action for an intro
/// worked out from its start lines, so its first beat waits for them.
/// </summary>
public static class TourPacing
{
    /// <summary>
    /// Ticks from a line being said until the next may be due on a clear
    /// screen: its reading time, its fade-out and the lead, at 60 ticks a
    /// second.
    /// </summary>
    /// <param name="text">The line; null counts as empty.</param>
    /// <returns>
    /// The ticks, rounded up (less a hair, so floating-point noise on a
    /// whole number of ticks does not add one).
    /// </returns>
    public static int ClearTicks(string? text) =>
        (int)Math.Ceiling((NarrationTiming.TicksPerSecond
            * (NarrationTiming.ReadingSeconds(text)
                + NarrationTiming.FadeOutSeconds
                + NarrationTiming.LeadSeconds)) - 1e-6);

    /// <summary>
    /// The earliest tick, counted from their cue, at which a line after a
    /// group of delayed lines may be due: the latest of each line's delay
    /// plus its <see cref="ClearTicks"/>.
    /// </summary>
    /// <param name="lines">The lines, each with its delay in ticks.</param>
    /// <returns>The tick; 0 for no lines.</returns>
    public static int ClearAfter(IEnumerable<(int Delay, string Text)>? lines) =>
        (lines ?? Enumerable.Empty<(int Delay, string Text)>())
            .Select(l => Math.Max(0, l.Delay) + ClearTicks(l.Text))
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>
    /// How long a scene holds its own action after it begins, so its first
    /// beat (at <paramref name="firstBeatTick"/> on the scene's clock)
    /// comes once its start lines have been read and cleared.
    /// </summary>
    /// <param name="startLines">The scene's start lines and delays.</param>
    /// <param name="firstBeatTick">The scene clock's first beat.</param>
    /// <returns>The intro, in ticks; never negative.</returns>
    public static int IntroTicks(
        IEnumerable<(int Delay, string Text)>? startLines, int firstBeatTick) =>
        Math.Max(0, ClearAfter(startLines) - Math.Max(0, firstBeatTick));

    /// <summary>
    /// Whether the lines of one cue follow each other on a clear screen:
    /// in order of delay, each is due no sooner than the one before has
    /// cleared.
    /// </summary>
    /// <param name="lines">The cue's lines, each with its delay.</param>
    /// <returns>True when they are spaced; true for none or one.</returns>
    public static bool Spaced(IEnumerable<(int Delay, string Text)>? lines)
    {
        List<(int Delay, string Text)> ordered =
            (lines ?? Enumerable.Empty<(int Delay, string Text)>())
                .OrderBy(l => l.Delay).ToList();
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Delay
                < Math.Max(0, ordered[i - 1].Delay) + ClearTicks(ordered[i - 1].Text))
            {
                return false;
            }
        }

        return true;
    }
}
