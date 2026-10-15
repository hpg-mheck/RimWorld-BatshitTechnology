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
/// in The Story's voice: one line, at most 120 characters, no exclamation
/// and no word that breaks the fourth wall).
/// </summary>
public sealed class TourXmlTests
{
    /// <summary>The longest narration line, in characters.</summary>
    private const int MaxLineLength = 120;

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

    /// <summary>
    /// The first beat on a tour scene's own clock (TourScene.FirstBeatTick,
    /// in the game assembly, which the tests cannot load).
    /// </summary>
    private const int FirstBeatTick = 150;

    /// <summary>
    /// The least action time a scene's timeout leaves after its intro:
    /// the shortest timeout before the intros (the defence and grenade).
    /// </summary>
    private const int MinActionTicks = 1800;

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
    /// Every scene opens with a cut, not a glide, so its caption and
    /// opening line never show the last scene's place; and every shot
    /// keeps on the map, so none shows the black beyond its edge (slice
    /// 19000d).
    /// </summary>
    [Fact(DisplayName = "Every tour scene opens with a cut kept on the map")]
    public void Scenes_should_open_with_a_cut_kept_on_the_map()
    {
        foreach (XElement scene in Scenes())
        {
            string key = Key(scene);
            List<XElement> shots = scene.Element("shots")?.Elements("li").ToList()
                ?? new List<XElement>();
            Assert.True(
                shots.Any(s => Int(s, "atTick", 0) == 0),
                $"{key}: no opening shot");
            foreach (XElement shot in shots)
            {
                Assert.True(
                    shot.Element("keepOnMap")?.Value.Trim() == "true",
                    $"{key}: a shot does not keep on the map");
                if (Int(shot, "atTick", 0) == 0)
                {
                    Assert.True(
                        shot.Element("pan")?.Value.Trim() == "false",
                        $"{key}: its opening shot glides");
                }
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
    /// Every line is in the narrator's voice: one line, at most 120
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
    /// The closing message is narration too: since slice 13000 it goes to
    /// the narration overlay in the narrator's voice, so it keeps the same
    /// rules as the def's lines, though it lives in the keyed file. It is
    /// the line docs/SHOWCASE.md, "The End", gives.
    /// </summary>
    [Fact(DisplayName = "The closing message keeps the narrator's voice")]
    public void Closing_message_should_keep_the_voice()
    {
        string path = Path.Combine(
            FixturesDirectory, "Languages", "English", "Keyed", "BT_Showcases.xml");
        string text = Assert.Single(
            XDocument.Load(path).Root?.Elements("BT_Showcases_Finished")
                ?? Enumerable.Empty<XElement>()).Value;
        Assert.Equal(
            "This concludes our presentation. The colony is yours. "
                + "Please remain calm.",
            text);
        Assert.True(text.Length <= MaxLineLength, $"'{text}' is too long");
        Assert.DoesNotContain("!", text, StringComparison.Ordinal);
        foreach (string word in FourthWallWords)
        {
            Assert.False(
                Regex.IsMatch(text, @"\b" + word + @"s?\b", RegexOptions.IgnoreCase),
                $"'{text}' says '{word}'");
        }
    }

    /// <summary>
    /// A computed beat (the raccoon's census) has no line in the def: the
    /// scene says the words, and a declared line would be said as well.
    /// </summary>
    [Fact(DisplayName = "No tour line names a computed beat")]
    public void Computed_beats_should_have_no_lines()
    {
        var declared = Lines()
            .Where(l => TourScript.For(l.Scene)?.Computed
                .Contains(l.When, StringComparer.OrdinalIgnoreCase) == true)
            .Select(l => $"{l.Scene}/{l.When}")
            .ToList();

        Assert.Empty(declared);
    }

    /// <summary>
    /// The Raccoon Incident speaks as docs/SHOWCASE.md's Reel 9 has it:
    /// each fate its own line, then "AnyRim thanks Ned for his service.
    /// Unfortunately." once the fate has been read (460 ticks), either
    /// way, before the next scene's start line, so the lines keep their
    /// cue order.
    /// </summary>
    [Fact(DisplayName = "The raccoon's verdict reads its fate, then thanks Ned")]
    public void Raccoon_verdict_should_follow_the_story()
    {
        const string Thanks = "AnyRim thanks Ned for his service. Unfortunately.";
        const int ThanksDelay = 460;
        var lines = Lines().Where(l => l.Scene == TourScript.Raccoon).ToList();
        (int, string)[] Said(string when) => lines
            .Where(l => l.When == when).Select(l => (l.Delay, l.Text)).ToArray();

        Assert.Equal(
            new[] { (0, "The raccoon has been reassured."), (ThanksDelay, Thanks) },
            Said(TourScript.RaccoonSurvived));
        Assert.Equal(
            new[] { (0, "The raccoon will not be troubling anyone."), (ThanksDelay, Thanks) },
            Said(TourScript.RaccoonLost));
        Assert.Equal(
            new[] { (0, "Ned believes in a proportionate response.") },
            Said(TourScript.Thrown));
        Assert.Contains(
            "Ned Lessly, AnyRim Savings & Loan Security",
            Said(TourScript.Guard).Single().Item2,
            StringComparison.Ordinal);
        int nextStart = Lines()
            .Where(l => l.Scene == TourScript.Tractor && l.When == NarrationScript.Start)
            .Min(l => l.Delay);
        Assert.All(
            lines.Where(l => l.When is TourScript.RaccoonSurvived or TourScript.RaccoonLost),
            l => Assert.True(l.Delay < nextStart, $"{l.When} waits {l.Delay}"));
    }

    /// <summary>
    /// The film ends on the twins: their completed line, then the byline
    /// once that line has been read and cleared (430 ticks), word for word.
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
                (430, "The Future: Sooner. Cheaper. Funnier. And ludicrously dangerous."),
            },
            closing);
    }

    /// <summary>
    /// On the narration overlay, a scene's start lines are due only once
    /// the previous scene's outcome lines (any of its outcomes, in the
    /// script's order) have been read and cleared, so each scene opens on
    /// a clear screen (TourPacing.ClearAfter).
    /// </summary>
    [Fact(DisplayName = "Start lines wait for the previous scene's outcome to clear")]
    public void Start_lines_should_wait_for_the_previous_outcome()
    {
        List<(string Scene, string When, int Delay, string Text)> lines = Lines();
        var late = new List<string>();
        for (int i = 1; i < TourScript.Scenes.Count; i++)
        {
            TourSceneScript before = TourScript.Scenes[i - 1];
            string key = TourScript.Scenes[i].Key;
            int needed = new[] { TourScript.Completed }
                .Concat(before.Outcomes)
                .Select(when => TourPacing.ClearAfter(lines
                    .Where(l => l.Scene == before.Key && l.When == when)
                    .Select(l => (l.Delay, l.Text))))
                .Max();
            int first = lines
                .Where(l => l.Scene == key && l.When == NarrationScript.Start)
                .Min(l => l.Delay);
            if (first < needed)
            {
                late.Add($"{key} starts at {first}, needs {needed}");
            }
        }

        Assert.Empty(late);
    }

    /// <summary>
    /// The lines of one cue (a scene's start, an outcome) follow each
    /// other on a clear screen: each is due once the one before has been
    /// read and cleared (TourPacing.Spaced).
    /// </summary>
    [Fact(DisplayName = "A cue's lines follow each other on a clear screen")]
    public void Cue_lines_should_be_spaced()
    {
        var crowded = Lines()
            .GroupBy(l => (l.Scene, l.When))
            .Where(g => !TourPacing.Spaced(g.Select(l => (l.Delay, l.Text))))
            .Select(g => $"{g.Key.Scene}/{g.Key.When}")
            .ToList();

        Assert.Empty(crowded);
    }

    /// <summary>
    /// Each scene's timeout covers the intro it holds for its start lines
    /// (TourPacing.IntroTicks) and still leaves the 1,800 ticks of action
    /// the shortest scene had before the intros, so no scene times out
    /// for its narration.
    /// </summary>
    [Fact(DisplayName = "Scene timeouts cover the intro and the action")]
    public void Timeouts_should_cover_the_intro()
    {
        List<(string Scene, string When, int Delay, string Text)> lines = Lines();
        var cramped = new List<string>();
        foreach (XElement scene in Scenes())
        {
            string key = Key(scene);
            int intro = TourPacing.IntroTicks(
                lines.Where(l => l.Scene == key && l.When == NarrationScript.Start)
                    .Select(l => (l.Delay, l.Text)),
                FirstBeatTick);
            int timeout = Int(scene, "timeoutTicks", 0);
            if (timeout - intro < MinActionTicks)
            {
                cramped.Add($"{key}: timeout {timeout}, intro {intro}");
            }
        }

        Assert.Empty(cramped);
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
