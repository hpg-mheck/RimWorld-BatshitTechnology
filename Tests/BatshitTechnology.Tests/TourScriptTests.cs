using System;
using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the tour's script in <see cref="TourScript"/>: the running
/// order, and that every scene's narration moments are distinct and never
/// reuse a moment the Showcase API narrates itself.
/// </summary>
public sealed class TourScriptTests
{
    /// <summary>
    /// The tour has ten scenes with unique keys, in the order the plan
    /// gives (the stampede after the defence, the raccoon after the
    /// grenade), ending on the twins.
    /// </summary>
    [Fact(DisplayName = "The tour runs ten unique scenes, ending with the twins")]
    public void Keys_should_be_ten_unique_in_order()
    {
        Assert.Equal(
            new[]
            {
                "home", "defence", "stampede", "retire-replicator",
                "retire-reactor", "anyrim", "grenade", "raccoon", "tractor",
                "twins",
            },
            TourScript.Keys);
        Assert.Equal(
            TourScript.Keys.Count,
            TourScript.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(TourScript.Twins, TourScript.Keys[TourScript.Keys.Count - 1]);
    }

    /// <summary>
    /// Within a scene, its beats and outcomes are distinct (ignoring case,
    /// as narration lookups do) and none is a moment the API narrates.
    /// </summary>
    [Fact(DisplayName = "Each scene's moments are unique and none is the API's")]
    public void Moments_should_be_unique_and_not_the_APIs()
    {
        foreach (TourSceneScript scene in TourScript.Scenes)
        {
            var own = scene.Moments.Concat(scene.Outcomes).ToList();
            Assert.True(
                own.Count == own.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                $"{scene.Key} repeats a moment: {string.Join(", ", own)}");
            Assert.All(own, m => Assert.DoesNotContain(
                m, TourScript.ApiMoments, StringComparer.OrdinalIgnoreCase));
            Assert.All(own, m => Assert.False(string.IsNullOrWhiteSpace(m)));
        }
    }

    /// <summary>
    /// A scene's narrated moments start with "start", include its beats,
    /// "completed" and its own outcomes; only the grenade and the raccoon
    /// have outcomes of their own.
    /// </summary>
    [Fact(DisplayName = "Narrated moments cover start, beats, completion and outcomes")]
    public void NarratedMoments_should_cover_the_scene()
    {
        TourSceneScript grenade = TourScript.For("GRENADE")!;
        Assert.Equal(
            new[]
            {
                "start", "thrown", "thrownAlone", "completed",
                "volunteerSurvived", "volunteerLost",
            },
            grenade.NarratedMoments());
        Assert.Equal(
            new[] { TourScript.Grenade, TourScript.Raccoon },
            TourScript.Scenes.Where(s => s.Outcomes.Count > 0).Select(s => s.Key));
    }

    /// <summary>
    /// The raccoon's census is a computed beat: a moment it plays (and the
    /// verifier requires), which the def gives no line because the scene
    /// says the real count; it is left out of the narrated moments. No
    /// other scene computes its words.
    /// </summary>
    [Fact(DisplayName = "The raccoon's census is its one computed beat")]
    public void Raccoon_census_should_be_computed()
    {
        TourSceneScript raccoon = TourScript.For(TourScript.Raccoon)!;
        Assert.Equal(new[] { "guard", "pest", "thrown", "census" }, raccoon.Moments);
        Assert.Equal(new[] { TourScript.Census }, raccoon.Computed);
        Assert.Equal(
            new[]
            {
                "start", "guard", "pest", "thrown", "completed",
                "raccoonSurvived", "raccoonLost",
            },
            raccoon.NarratedMoments());
        Assert.True(TourScript.Knows("raccoon", "census"));
        Assert.True(TourScript.Knows("raccoon", "raccoonLost"));
        Assert.False(TourScript.Knows("grenade", "census"));
        Assert.Equal(
            new[] { TourScript.Raccoon },
            TourScript.Scenes.Where(s => s.Computed.Count > 0).Select(s => s.Key));
        Assert.All(TourScript.Scenes, s => Assert.All(s.Computed,
            c => Assert.Contains(c, s.Moments)));
    }

    /// <summary>
    /// The raccoon's outcome reads its fate: dead or destroyed is lost;
    /// anything else, downed included, survived.
    /// </summary>
    [Fact(DisplayName = "RaccoonOutcome: dead is lost, anything else survived")]
    public void RaccoonOutcome_should_read_the_fate()
    {
        Assert.Equal(TourScript.RaccoonLost, TourScript.RaccoonOutcome(true, false));
        Assert.Equal(TourScript.RaccoonLost, TourScript.RaccoonOutcome(false, true));
        Assert.Equal(TourScript.RaccoonLost, TourScript.RaccoonOutcome(true, true));
        Assert.Equal(
            TourScript.RaccoonSurvived, TourScript.RaccoonOutcome(false, false));
    }

    /// <summary>
    /// Knows accepts a scene's beats, its outcomes and the API's moments,
    /// and refuses unknown scenes and moments.
    /// </summary>
    [Fact(DisplayName = "Knows tells real moments from bogus ones")]
    public void Knows_should_tell_real_moments_from_bogus()
    {
        Assert.True(TourScript.Knows("twins", "planted"));
        Assert.True(TourScript.Knows("twins", "timedOut"));
        Assert.True(TourScript.Knows("twins", null));
        Assert.True(TourScript.Knows("grenade", "volunteerLost"));
        Assert.False(TourScript.Knows("twins", "volunteerLost"));
        Assert.False(TourScript.Knows("twins", "bogus"));
        Assert.False(TourScript.Knows("bogus", "start"));
        Assert.Null(TourScript.For(null));
    }

    /// <summary>
    /// A completed scene must play every beat but the ones the world
    /// decides: the tractor's doom, the grenade's empty-pile throw, which
    /// stands for the throw, and the stampede's stragglers.
    /// </summary>
    [Fact(DisplayName = "RequiredMoments leaves out only the world's beats")]
    public void RequiredMoments_should_leave_out_only_optional_beats()
    {
        Assert.Equal(
            new[] { "barn", "delivered", "spotted", "protest", "emplaced",
                "pull", "blast" },
            TourScript.RequiredMoments("tractor"));
        Assert.Equal(new[] { "thrown" }, TourScript.RequiredMoments("grenade"));
        Assert.Equal(
            new[] { "planted", "pull", "blast" },
            TourScript.RequiredMoments("twins"));
        Assert.Equal(
            new[] { "herd", "volley", "aftermath" },
            TourScript.RequiredMoments("stampede"));
        Assert.Equal(
            new[] { "guard", "pest", "thrown", "census" },
            TourScript.RequiredMoments("raccoon"));
        Assert.Equal("stragglers", TourScript.StandsFor("stragglers"));
        Assert.Equal(
            TourScript.For("home")!.Moments, TourScript.RequiredMoments("home"));
        Assert.Empty(TourScript.RequiredMoments("bogus"));
        Assert.Equal("thrown", TourScript.StandsFor("thrownAlone"));
        Assert.Equal("pull", TourScript.StandsFor("pull"));
        Assert.Equal(new[] { "blast" }, TourScript.RequiredMoments("retire-replicator"));
        Assert.Equal(new[] { "blast" }, TourScript.RequiredMoments("retire-reactor"));
    }

    /// <summary>
    /// A lost tourist is narrated by the scenes that stand tourists where
    /// they can be reached (the stampede's line, both pads, the tractor's
    /// pull), as an optional beat; the grenade's volunteer has outcomes of
    /// its own, and no other scene has the beat.
    /// </summary>
    [Fact(DisplayName = "Only the stampede, pads and tractor narrate a lost guest")]
    public void GuestsLost_should_be_an_optional_beat_of_four_scenes()
    {
        Assert.Equal(
            new[] { "stampede", "retire-replicator", "retire-reactor", "tractor" },
            TourScript.Scenes.Where(s => TourScript.NarratesGuestsLost(s.Key))
                .Select(s => s.Key));
        Assert.True(TourScript.NarratesGuestsLost("TRACTOR"));
        Assert.False(TourScript.NarratesGuestsLost("grenade"));
        Assert.False(TourScript.NarratesGuestsLost(null));
        Assert.True(TourScript.Knows("stampede", TourScript.GuestsLost));
        Assert.False(TourScript.Knows("twins", TourScript.GuestsLost));
        Assert.All(TourScript.Keys, k => Assert.DoesNotContain(
            TourScript.GuestsLost, TourScript.RequiredMoments(k)));
    }
}
