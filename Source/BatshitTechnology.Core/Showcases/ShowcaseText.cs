using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BatshitTechnology.Showcases;

/// <summary>
/// One narration line, as the def declares it: which scene it belongs
/// to, when it is said (at the scene's start, or on one of its outcomes),
/// how long after that, and the words.
/// </summary>
public sealed class NarrationCue
{
    /// <summary>Creates a cue.</summary>
    /// <param name="scene">The scene's key.</param>
    /// <param name="when">
    /// <see cref="NarrationScript.Start"/>, an outcome key, or a worker's
    /// own beat name; blank means the start.
    /// </param>
    /// <param name="delayTicks">
    /// Ticks after that moment; negatives are 0.
    /// </param>
    /// <param name="text">The words.</param>
    public NarrationCue(
        string? scene, string? when, int delayTicks, string? text)
    {
        Scene = (scene ?? string.Empty).Trim();
        When = string.IsNullOrWhiteSpace(when)
            ? NarrationScript.Start
            : when!.Trim();
        DelayTicks = Math.Max(0, delayTicks);
        Text = text ?? string.Empty;
    }

    /// <summary>Gets the scene's key.</summary>
    public string Scene { get; }

    /// <summary>Gets the moment it belongs to.</summary>
    public string When { get; }

    /// <summary>Gets the delay after that moment, in ticks.</summary>
    public int DelayTicks { get; }

    /// <summary>Gets the words.</summary>
    public string Text { get; }
}

/// <summary>
/// Picks narration lines by scene and moment. Lines live in the def's XML
/// so they can be translated and tested (docs/SHOWCASE-API.md).
/// </summary>
public static class NarrationScript
{
    /// <summary>The moment a scene begins.</summary>
    public const string Start = "start";

    /// <summary>The moment the viewer stops the showcase during a scene.</summary>
    public const string Stopped = "stopped";

    /// <summary>
    /// The lines for one scene and moment (keys compare without regard to
    /// case), by delay, ties in declared order. Blank lines are dropped.
    /// </summary>
    /// <param name="lines">Every line the def declares; may be null.</param>
    /// <param name="scene">The scene's key.</param>
    /// <param name="when">The moment.</param>
    /// <returns>The lines to say.</returns>
    public static IReadOnlyList<NarrationCue> LinesFor(
        IEnumerable<NarrationCue?>? lines, string? scene, string? when)
    {
        string key = (scene ?? string.Empty).Trim();
        string moment =
            string.IsNullOrWhiteSpace(when) ? Start : when!.Trim();
        return (lines ?? Enumerable.Empty<NarrationCue?>())
            .Select((line, index) => (line, index))
            .Where(p => p.line != null
                && !string.IsNullOrWhiteSpace(p.line.Text)
                && Same(p.line.Scene, key)
                && Same(p.line.When, moment))
            .OrderBy(p => p.line!.DelayTicks)
            .ThenBy(p => p.index)
            .Select(p => p.line!)
            .ToList();
    }

    /// <summary>
    /// The scene keys that lines name but no scene has, for a def's
    /// configuration check. Compared without regard to case.
    /// </summary>
    /// <param name="lines">Every line the def declares; may be null.</param>
    /// <param name="sceneKeys">The def's scene keys; may be null.</param>
    /// <returns>The unknown keys, distinct, in first-seen order.</returns>
    public static IReadOnlyList<string> UnknownScenes(
        IEnumerable<NarrationCue?>? lines, IEnumerable<string?>? sceneKeys)
    {
        var known = new HashSet<string>(
            (sceneKeys ?? Enumerable.Empty<string?>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k!.Trim()),
            StringComparer.OrdinalIgnoreCase);
        return (lines ?? Enumerable.Empty<NarrationCue?>())
            .Where(l => l != null && !known.Contains(l.Scene))
            .Select(l => l!.Scene)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The "stopped" lines that would never be said, for a def's
    /// configuration check. Stop ends all narration at once: a scene's
    /// "stopped" lines with no delay are said as it ends, and any with a
    /// delay are dropped with the rest of the queue. Moments compare
    /// without regard to case.
    /// </summary>
    /// <param name="lines">Every line the def declares; may be null.</param>
    /// <returns>The "stopped" lines with a delay, in declared order.</returns>
    public static IReadOnlyList<NarrationCue> DroppedAtStop(
        IEnumerable<NarrationCue?>? lines) =>
        (lines ?? Enumerable.Empty<NarrationCue?>())
            .Where(l => l != null && Same(l.When, Stopped) && l.DelayTicks > 0)
            .Select(l => l!)
            .ToList();

    /// <summary>Whether two keys match, ignoring case.</summary>
    /// <param name="a">One key.</param>
    /// <param name="b">The other.</param>
    /// <returns>True if they match.</returns>
    private static bool Same(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The moment key for an ending, for outcome lines.</summary>
    /// <param name="ending">How the scene ended.</param>
    /// <returns>
    /// "completed", "timedOut", "skipped", "stopped" or "failed".
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown for a value that is not a <see cref="SceneEnd"/>.
    /// </exception>
    public static string OutcomeKey(SceneEnd ending) => ending switch
    {
        SceneEnd.Completed => "completed",
        SceneEnd.TimedOut => "timedOut",
        SceneEnd.Skipped => "skipped",
        SceneEnd.Stopped => Stopped,
        SceneEnd.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(
            nameof(ending), ending, "Unknown scene ending."),
    };
}

/// <summary>
/// Formats the director's caption bar, "2 of 8: The Batshittium Reactor",
/// from a translatable pattern, without ever throwing on a bad one.
/// </summary>
public static class CaptionText
{
    /// <summary>
    /// The English pattern: {0} is the scene's number, {1} the count and
    /// {2} the title.
    /// </summary>
    public const string DefaultPattern = "{0} of {1}: {2}";

    /// <summary>
    /// Formats a caption. A blank or malformed pattern falls back to
    /// <see cref="DefaultPattern"/>; a blank title drops the colon.
    /// </summary>
    /// <param name="pattern">The pattern, usually translated.</param>
    /// <param name="index">The scene's zero-based index.</param>
    /// <param name="count">How many scenes there are.</param>
    /// <param name="title">The scene's title.</param>
    /// <returns>The caption.</returns>
    public static string Format(
        string? pattern, int index, int count, string? title)
    {
        int total = Math.Max(1, count);
        int number = Math.Min(Math.Max(1, index + 1), total);
        string name = title?.Trim() ?? string.Empty;
        string text;
        try
        {
            text = string.Format(
                CultureInfo.InvariantCulture,
                string.IsNullOrWhiteSpace(pattern)
                    ? DefaultPattern
                    : pattern!,
                number, total, name);
        }
        catch (FormatException)
        {
            text = string.Format(
                CultureInfo.InvariantCulture,
                DefaultPattern,
                number,
                total,
                name);
        }

        return name.Length == 0 ? text.TrimEnd().TrimEnd(':') : text;
    }

    /// <summary>
    /// The longest running time the menu will claim, in minutes.
    /// </summary>
    public const int MaxMinutes = 999;

    /// <summary>
    /// Rounds an estimated running time to whole minutes for the menu,
    /// never below one or above <see cref="MaxMinutes"/>.
    /// </summary>
    /// <param name="minutes">The estimate, in minutes.</param>
    /// <returns>Whole minutes, from 1 to <see cref="MaxMinutes"/>.</returns>
    public static int WholeMinutes(float minutes)
    {
        if (float.IsNaN(minutes) || minutes < 1.5f)
        {
            return 1;
        }

        return minutes >= MaxMinutes
            ? MaxMinutes
            : (int)Math.Round(minutes, MidpointRounding.AwayFromZero);
    }
}
