using System;
using System.Collections.Generic;
using System.Linq;
using BatshitTechnology.Showcases;

namespace BatshitTechnology.Core;

/// <summary>
/// One scene of the directed tour as the script knows it: its key, the
/// beats it plays (each a narration moment), and the outcomes of its own
/// that it may end with instead of the Showcase API's.
/// </summary>
/// <param name="Key">The scene's key, as in the def's XML.</param>
/// <param name="Moments">Its beats, in the order they usually play.</param>
/// <param name="Outcomes">Its own outcome keys; usually none.</param>
public sealed record TourSceneScript(
    string Key, IReadOnlyList<string> Moments, IReadOnlyList<string> Outcomes)
{
    /// <summary>
    /// Gets the beats whose words the scene computes from the world (the
    /// raccoon's census, which reads the real count) rather than the def
    /// declaring them; each is one of <see cref="Moments"/>, and the def
    /// gives it no line. Usually none.
    /// </summary>
    public IReadOnlyList<string> Computed { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Every narration moment the scene can reach that its lines should
    /// cover: the start, its beats (less the computed ones), completion,
    /// and its own outcomes.
    /// </summary>
    /// <returns>The moments, without duplicates, in that order.</returns>
    public IReadOnlyList<string> NarratedMoments() =>
        new[] { NarrationScript.Start }
            .Concat(Moments.Where(m =>
                !Computed.Contains(m, StringComparer.OrdinalIgnoreCase)))
            .Concat(new[] { TourScript.Completed })
            .Concat(Outcomes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

/// <summary>
/// The Batshit Technology tour's running order and its narration moments
/// (docs/SHOWCASE.md). The scene classes, the def's XML and the XML tests
/// all read this one list, so a beat the code plays always has a line, and
/// a line never names a beat that does not exist (the API's configuration
/// check sees scene keys only).
/// </summary>
public static class TourScript
{
    /// <summary>The API's outcome key for a scene that completed.</summary>
    public const string Completed = "completed";

    /// <summary>Scene 1: the demo base's devices.</summary>
    public const string Home = "home";

    /// <summary>Scene 2: the Ballista's volley.</summary>
    public const string Defence = "defence";

    /// <summary>
    /// Scene 3: a 256-bolt volley meets a stampede of manhunting
    /// boomalopes from the east.
    /// </summary>
    public const string Stampede = "stampede";

    /// <summary>Scene 4: the Random Replicator's self-destruct.</summary>
    public const string RetireReplicator = "retire-replicator";

    /// <summary>Scene 5: the Batshittium Reactor's self-destruct.</summary>
    public const string RetireReactor = "retire-reactor";

    /// <summary>Scene 6: AnyRim, the test town.</summary>
    public const string AnyRim = "anyrim";

    /// <summary>Scene 7: the Ergonomic Singularity Grenade.</summary>
    public const string Grenade = "grenade";

    /// <summary>
    /// Scene 8: the Raccoon Incident. AnyRim's bank guard meets a
    /// manhunting raccoon on Main Street with the grenade.
    /// </summary>
    public const string Raccoon = "raccoon";

    /// <summary>Scene 9: one Tractor Bomb, from the barn to the blast.</summary>
    public const string Tractor = "tractor";

    /// <summary>Scene 10: two Tractor Bombs in a junkyard; the finale.</summary>
    public const string Twins = "twins";

    /// <summary>
    /// The stampede's beat when a cleanup volley follows the first; it
    /// plays only when boomalopes outlive the first volley.
    /// </summary>
    public const string Stragglers = "stragglers";

    /// <summary>The grenade scene's outcome when the volunteer lives.</summary>
    public const string VolunteerSurvived = "volunteerSurvived";

    /// <summary>The grenade scene's outcome when the volunteer dies.</summary>
    public const string VolunteerLost = "volunteerLost";

    /// <summary>The grenade's beat: thrown with a volunteer in the pile.</summary>
    public const string Thrown = "thrown";

    /// <summary>
    /// The grenade's beat when no colonist was left to volunteer, so the
    /// pile is empty; it stands for <see cref="Thrown"/>.
    /// </summary>
    public const string ThrownAlone = "thrownAlone";

    /// <summary>The raccoon scene's first beat: the guard at the bank.</summary>
    public const string Guard = "guard";

    /// <summary>The raccoon scene's beat as the raccoon comes to town.</summary>
    public const string Pest = "pest";

    /// <summary>
    /// The raccoon scene's computed beat after the singularity: the
    /// census, read with the real count of surviving residents.
    /// </summary>
    public const string Census = "census";

    /// <summary>The raccoon scene's outcome when the raccoon lives.</summary>
    public const string RaccoonSurvived = "raccoonSurvived";

    /// <summary>The raccoon scene's outcome when the raccoon dies.</summary>
    public const string RaccoonLost = "raccoonLost";

    /// <summary>The scenes, in running order.</summary>
    public static IReadOnlyList<TourSceneScript> Scenes { get; } =
        new[]
        {
            Scene(Home, "reactor", "replicator", "transmogrifier", "squeezer"),
            Scene(Defence, "artificer", "volley"),
            Scene(Stampede, "herd", "volley", Stragglers, "aftermath"),
            Scene(RetireReplicator, "blast"),
            Scene(RetireReactor, "blast"),
            Scene(AnyRim, "sign", "street", "homes"),
            new TourSceneScript(
                Grenade, new[] { Thrown, ThrownAlone },
                new[] { VolunteerSurvived, VolunteerLost }),
            new TourSceneScript(
                Raccoon, new[] { Guard, Pest, Thrown, Census },
                new[] { RaccoonSurvived, RaccoonLost })
            {
                Computed = new[] { Census },
            },
            Scene(Tractor, "barn", "delivered", "spotted", "protest",
                "emplaced", "doom", "pull", "blast"),
            Scene(Twins, "planted", "pull", "blast"),
        };

    /// <summary>The scene keys, in running order.</summary>
    public static IReadOnlyList<string> Keys { get; } =
        Scenes.Select(s => s.Key).ToList();

    /// <summary>
    /// The moments the Showcase API itself narrates, which a scene's own
    /// beats and outcomes must not reuse.
    /// </summary>
    public static IReadOnlyList<string> ApiMoments { get; } =
        new[] { NarrationScript.Start }
            .Concat(Enum.GetValues(typeof(SceneEnd))
                .Cast<SceneEnd>()
                .Select(NarrationScript.OutcomeKey))
            .ToList();

    /// <summary>Finds a scene's script by key, ignoring case.</summary>
    /// <param name="key">The scene's key.</param>
    /// <returns>The script, or null if the tour has no such scene.</returns>
    public static TourSceneScript? For(string? key) =>
        Scenes.FirstOrDefault(s =>
            string.Equals(s.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether a scene can reach a narration moment: one of its beats, its
    /// own outcomes, or one the API narrates.
    /// </summary>
    /// <param name="key">The scene's key.</param>
    /// <param name="moment">The moment.</param>
    /// <returns>False for an unknown scene or moment.</returns>
    public static bool Knows(string? key, string? moment)
    {
        TourSceneScript? scene = For(key);
        string when = string.IsNullOrWhiteSpace(moment)
            ? NarrationScript.Start
            : moment!.Trim();
        return scene != null
            && scene.Moments.Concat(scene.Outcomes).Concat(ApiMoments)
                .Contains(when, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The beats a scene may complete without, because the world decides
    /// them: the tractor's "doom" needs a raider to freeze in place, the
    /// grenade plays one of its two throws, and the stampede's stragglers
    /// need boomalopes to outlive the first volley.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> Optional =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Tractor] = new[] { "doom" },
            [Grenade] = new[] { ThrownAlone },
            [Stampede] = new[] { Stragglers },
        };

    /// <summary>
    /// The beats a scene that completed must have played (the tour
    /// verifier's beat check). The device parade's beats depend on the
    /// devices standing, so the verifier asks that scene instead.
    /// </summary>
    /// <param name="key">The scene's key.</param>
    /// <returns>The required moments; empty for an unknown scene.</returns>
    public static IReadOnlyList<string> RequiredMoments(string? key)
    {
        TourSceneScript? scene = For(key);
        if (scene == null)
        {
            return Array.Empty<string>();
        }

        string[] optional = Optional.TryGetValue(scene.Key, out string[]? o)
            ? o
            : Array.Empty<string>();
        return scene.Moments
            .Where(m => !optional.Contains(m, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// The required moment a played beat stands for: an empty-pile throw
    /// stands for the throw; any other beat for itself.
    /// </summary>
    /// <param name="moment">The beat played.</param>
    /// <returns>The moment it covers.</returns>
    public static string? StandsFor(string? moment) =>
        string.Equals(moment, ThrownAlone, StringComparison.OrdinalIgnoreCase)
            ? Thrown
            : moment;

    /// <summary>
    /// The raccoon scene's outcome when it completes, read from the
    /// world: lost if the raccoon is dead (or gone from the world
    /// altogether), survived otherwise, downed included. The scene and the
    /// tour verifier both read the raccoon's fate this way.
    /// </summary>
    /// <param name="raccoonDead">Whether the raccoon is dead.</param>
    /// <param name="raccoonDestroyed">Whether it was destroyed outright.</param>
    /// <returns><see cref="RaccoonLost"/> or <see cref="RaccoonSurvived"/>.</returns>
    public static string RaccoonOutcome(bool raccoonDead, bool raccoonDestroyed) =>
        raccoonDead || raccoonDestroyed ? RaccoonLost : RaccoonSurvived;

    /// <summary>A scene with beats and no outcomes of its own.</summary>
    /// <param name="key">The key.</param>
    /// <param name="moments">The beats.</param>
    /// <returns>The script.</returns>
    private static TourSceneScript Scene(string key, params string[] moments) =>
        new TourSceneScript(key, moments, Array.Empty<string>());
}
