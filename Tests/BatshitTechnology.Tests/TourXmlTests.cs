using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BatshitTechnology.Core;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the directed tour's def, BT_Showcase in
/// Defs/Misc/BT_Showcases.xml, against Core's <see cref="TourScript"/>:
/// its director, its scenes (keys, classes, titles, timing and zooms) and
/// its narration (every moment covered, nothing unknown, and every line
/// in The Story's voice: one line, at most 96 characters, no exclamation
/// and no word that breaks the fourth wall).
/// </summary>
public sealed class TourXmlTests
{
    /// <summary>The longest narration line, in characters.</summary>
    private const int MaxLineLength = 96;

    /// <summary>The longest scene title the caption bar is laid out for.</summary>
    private const int MaxTitleLength = 32;

    /// <summary>
    /// The closest zoom the API allows (ShowcaseShot.MinZoom, in the game
    /// assembly, which the tests cannot load).
    /// </summary>
    private const float MinZoom = 8f;

    /// <summary>The farthest zoom the API allows (ShowcaseShot.MaxZoom).</summary>
    private const float MaxZoom = 80f;

    /// <summary>Game ticks in a minute at normal speed.</summary>
    private const int TicksPerMinute = 3600;

    /// <summary>The robot's ThingDef, whose sight Core's layouts assume.</summary>
    private const string RobotDef = "BT_TractorBomb";

    /// <summary>
    /// Words that break the fourth wall: the narrator speaks inside the
    /// film's world, never about the software.
    /// </summary>
    private static readonly string[] FourthWallWords =
    {
        "player", "mod", "game", "showcase", "click",
    };

    /// <summary>The tour runs on Batshit Technology's own director.</summary>
    [Fact(DisplayName = "The tour's worker is BatshitTourDirector")]
    public void Worker_should_be_the_tour_director()
    {
        Assert.Equal(
            "BatshitTechnology.BatshitTourDirector",
            Tour().Element("workerClass")?.Value);
    }

    /// <summary>
    /// The def's scene keys are exactly Core's script, in order, so the
    /// scene classes and the narration agree on them.
    /// </summary>
    [Fact(DisplayName = "The tour's scene keys follow TourScript, in order")]
    public void Keys_should_equal_the_script()
    {
        Assert.Equal(TourScript.Keys, Scenes().Select(Key).ToList());
    }

    /// <summary>
    /// Every scene names a class the source declares, in its exact
    /// namespace, so the game can make it.
    /// </summary>
    [Fact(DisplayName = "Every tour scene's class is declared in Source/")]
    public void Scene_classes_should_be_declared()
    {
        foreach (XElement scene in Scenes())
        {
            string cls = scene.Attribute("Class")?.Value ?? string.Empty;
            Assert.True(
                ShowcaseXmlTests.ClassDeclared(cls),
                $"{Key(scene)}: {cls} is not in Source/");
        }
    }

    /// <summary>
    /// Titles fit the caption bar; each scene's timing is consistent;
    /// every shot's zoom is one the API allows.
    /// </summary>
    [Fact(DisplayName = "Tour scenes have short titles, sane timing and zooms")]
    public void Scenes_should_have_titles_timing_and_zooms()
    {
        foreach (XElement scene in Scenes())
        {
            string key = Key(scene);
            string title = scene.Element("title")?.Value ?? string.Empty;
            Assert.InRange(title.Trim().Length, 1, MaxTitleLength);
            Assert.True(
                MinTicks(scene) <= Int(scene, "timeoutTicks", 3600),
                $"{key}: minTicks above timeoutTicks");
            foreach (XElement zoom in scene.Descendants("zoom"))
            {
                float value = float.Parse(zoom.Value, CultureInfo.InvariantCulture);
                Assert.InRange(value, MinZoom, MaxZoom);
            }
        }
    }

    /// <summary>
    /// The menu's running time covers at least the scenes' shortest
    /// runs, so the estimate never undersells the tour.
    /// </summary>
    [Fact(DisplayName = "The tour's estimated minutes cover its scenes")]
    public void Estimate_should_cover_the_minimum_runs()
    {
        float minutes = float.Parse(
            Tour().Element("estimatedMinutes")?.Value ?? "5",
            CultureInfo.InvariantCulture);

        Assert.True(minutes * TicksPerMinute >= Scenes().Sum(MinTicks));
    }

    /// <summary>Every scene opens and closes with a line.</summary>
    [Fact(DisplayName = "Every tour scene has start and completed lines")]
    public void Scenes_should_have_start_and_completed_lines()
    {
        List<(string Scene, string When, int Delay, string Text)> lines = Lines();
        foreach (string key in TourScript.Keys)
        {
            Assert.Contains(lines, l => l.Scene == key && l.When == NarrationScript.Start);
            Assert.Contains(lines, l => l.Scene == key && l.When == TourScript.Completed);
        }
    }

    /// <summary>
    /// Every moment and outcome the script names has a line, so no beat
    /// the scenes play goes unspoken.
    /// </summary>
    [Fact(DisplayName = "Every TourScript moment and outcome has a line")]
    public void Script_moments_should_all_have_lines()
    {
        List<(string Scene, string When, int Delay, string Text)> lines = Lines();
        var missing = TourScript.Scenes
            .SelectMany(s => s.NarratedMoments().Select(m => (s.Key, m)))
            .Where(sm => !lines.Any(l => l.Scene == sm.Key && l.When == sm.m))
            .Select(sm => $"{sm.Key}/{sm.m}")
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// No line names a scene or moment the script does not know, since
    /// such a line would never be said.
    /// </summary>
    [Fact(DisplayName = "No tour line names an unknown scene or moment")]
    public void Lines_should_name_known_moments()
    {
        var unknown = Lines()
            .Where(l => !TourScript.Knows(l.Scene, l.When))
            .Select(l => $"{l.Scene}/{l.When}")
            .ToList();

        Assert.Empty(unknown);
    }

    /// <summary>
    /// Every line is in the narrator's voice: one line, at most 96
    /// characters, no exclamation mark, and no word about the software.
    /// </summary>
    [Fact(DisplayName = "Tour lines are short, calm and stay in the film")]
    public void Lines_should_keep_the_voice()
    {
        foreach ((string scene, string when, _, string text) in Lines())
        {
            string where = $"{scene}/{when}: {text}";
            Assert.False(string.IsNullOrWhiteSpace(text), where);
            Assert.DoesNotContain("\n", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\\n", text, StringComparison.Ordinal);
            Assert.True(text.Length <= MaxLineLength, $"{where} is too long");
            Assert.DoesNotContain("!", text, StringComparison.Ordinal);
            foreach (string word in FourthWallWords)
            {
                Assert.False(
                    Regex.IsMatch(text, @"\b" + word + @"s?\b", RegexOptions.IgnoreCase),
                    $"{where} says '{word}'");
            }
        }
    }

    /// <summary>
    /// The film ends on the twins: their completed line, then the byline
    /// 240 ticks later, word for word.
    /// </summary>
    [Fact(DisplayName = "The tour ends with the twins' line, then the byline")]
    public void Twins_should_close_with_the_byline()
    {
        var closing = Lines()
            .Where(l => l.Scene == TourScript.Twins && l.When == TourScript.Completed)
            .Select(l => (l.Delay, l.Text))
            .ToList();

        Assert.Equal(
            new[]
            {
                (0, "Two tractors are better than one."),
                (240, "The Future: Sooner. Cheaper. Funnier. And ludicrously dangerous."),
            },
            closing);
    }

    /// <summary>
    /// Core's planting prediction assumes the robot's real sight, so the
    /// layouts' tests stay true of the game.
    /// </summary>
    [Fact(DisplayName = "TractorPlanting.RobotSight matches the robot's sight")]
    public void Robot_sight_should_match_the_def()
    {
        XElement sight = Directory
            .EnumerateFiles(Path.Combine(FixturesDirectory, "Defs"), "*.xml",
                SearchOption.AllDirectories)
            .Select(XDocument.Load)
            .SelectMany(d => d.Root?.Elements("ThingDef") ?? Enumerable.Empty<XElement>())
            .Where(d => d.Element("defName")?.Value == RobotDef)
            .SelectMany(d => d.Descendants("sightRadius"))
            .Single();

        Assert.Equal(TractorPlanting.RobotSight, int.Parse(sight.Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Gets the fixtures folder the test project copies.</summary>
    private static string FixturesDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>The tour's def.</summary>
    /// <returns>BT_Showcase's element.</returns>
    private static XElement Tour() =>
        Assert.Single(
            ShowcaseXmlTests.ShowcaseDefs(),
            d => d.Element("defName")?.Value == "BT_Showcase");

    /// <summary>The tour's scenes, in order.</summary>
    /// <returns>The scene elements.</returns>
    private static List<XElement> Scenes() =>
        Tour().Element("scenes")?.Elements("li").ToList() ?? new List<XElement>();

    /// <summary>A scene's key.</summary>
    /// <param name="scene">The scene element.</param>
    /// <returns>Its key, or empty.</returns>
    private static string Key(XElement scene) =>
        scene.Element("key")?.Value.Trim() ?? string.Empty;

    /// <summary>A scene's minTicks, with the API's default.</summary>
    /// <param name="scene">The scene element.</param>
    /// <returns>Its minTicks.</returns>
    private static int MinTicks(XElement scene) => Int(scene, "minTicks", 120);

    /// <summary>An integer field of an element, or a default.</summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The field.</param>
    /// <param name="fallback">The default.</param>
    /// <returns>The value.</returns>
    private static int Int(XElement element, string name, int fallback) =>
        element.Element(name) is XElement e
            ? int.Parse(e.Value, CultureInfo.InvariantCulture)
            : fallback;

    /// <summary>The tour's narration lines, in order.</summary>
    /// <returns>Scene, moment, delay and text of each.</returns>
    private static List<(string Scene, string When, int Delay, string Text)> Lines() =>
        (Tour().Element("narration")?.Elements("li") ?? Enumerable.Empty<XElement>())
            .Select(l => (
                l.Element("scene")?.Value.Trim() ?? string.Empty,
                l.Element("when")?.Value.Trim() ?? NarrationScript.Start,
                Int(l, "delayTicks", 0),
                l.Element("text")?.Value ?? string.Empty))
            .ToList();
}
