using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// XML regression coverage for the Tractor Bomb's flee step
/// (docs/TRACTOR-BOMB.md, "Everyone Else Runs" and "Making Peace"): the
/// flee and freeze jobs, the twenty "making peace" lines, and the patch
/// that puts the flee job giver into vanilla's constant think trees,
/// checked against RimWorld's own think trees and races where the game is
/// installed.
/// </summary>
public sealed partial class XmlDefinitionTests
{
    /// <summary>The flee job giver's class, as the patch names it.</summary>
    private const string FleeGiverClass =
        "BatshitTechnology.JobGiver_FleeTractorBomb";

    /// <summary>
    /// The constant-tree gate that turns away pawns in a mental state,
    /// drafted, downed without a crawl, burning or asleep.
    /// </summary>
    private const string ConstantGateClass =
        "ThinkNode_ConditionalCanDoConstantThinkTreeJobNow";

    /// <summary>
    /// The default Steam install, used when RIMWORLD_DIR is unset.
    /// </summary>
    private const string DefaultRimWorldPath =
        @"C:\Program Files (x86)\Steam\steamapps\common\RimWorld";

    /// <summary>
    /// The constant trees the patch must reach: humanlikes, animals
    /// (dryads included) and insects.
    /// </summary>
    private static readonly string[] FleeingConstantTrees =
    {
        "HumanlikeConstant", "AnimalConstant", "InsectConstant",
    };

    /// <summary>
    /// The freeze job's status lines: exactly twenty, unique, lowercase,
    /// with no full stop of their own (the report adds it), and the same,
    /// in the same order, as the "Making Peace" list in the spec.
    /// </summary>
    [Fact(DisplayName = "Twenty making-peace lines match the spec")]
    public void TractorBombFreezeLines_should_match_the_spec()
    {
        var freeze = SingleDef("JobDef", "BT_TractorBombFreeze");
        var extension = freeze.Element("modExtensions")?.Elements("li")
            .Single(e => e.Attribute("Class")?.Value
                == "BatshitTechnology.TractorBombFreezeLines");
        string[] lines = ListValues(extension?.Element("lines"));

        Assert.Equal(20, lines.Length);
        Assert.Equal(lines.Length, lines.Distinct().Count());
        Assert.All(lines, line =>
        {
            Assert.False(string.IsNullOrWhiteSpace(line));
            Assert.Equal(line.ToLowerInvariant(), line);
            Assert.False(line.EndsWith(".", StringComparison.Ordinal), line);
        });
        Assert.Equal(MakingPeaceLinesFromSpec(), lines);
    }

    /// <summary>
    /// The flee and freeze jobs use the drivers the code expects; the
    /// freeze shows its line even on another faction's pawns and holds
    /// through damage, but a player may still draft a colonist out of it.
    /// </summary>
    [Fact(DisplayName = "Flee and freeze jobs are wired up")]
    public void TractorBombFleeJobs_should_be_wired_up()
    {
        var flee = SingleDef("JobDef", "BT_TractorBombFlee");
        Assert.Equal("BatshitTechnology.JobDriver_TractorBombFlee",
            flee.Element("driverClass")?.Value);
        Assert.Equal("false", flee.Element("casualInterruptible")?.Value);

        var freeze = SingleDef("JobDef", "BT_TractorBombFreeze");
        Assert.Equal("BatshitTechnology.JobDriver_TractorBombFreeze",
            freeze.Element("driverClass")?.Value);
        Assert.Equal("true", freeze.Element("alwaysShowReport")?.Value);
        Assert.Equal("Never", freeze.Element("checkOverrideOnDamage")?.Value);
        Assert.Null(freeze.Element("playerInterruptible"));
    }

    /// <summary>
    /// The patch prepends the flee job giver inside the constant-tree gate
    /// of the humanlike, animal and insect constant trees, and nowhere
    /// else: below the gate's mental-state test, ahead of vanilla's own
    /// flee givers.
    /// </summary>
    [Fact(DisplayName = "Flee giver is patched into three constant trees")]
    public void FleePatch_should_prepend_the_giver_inside_the_gate()
    {
        var operations = FleePatchOperations();
        Assert.Equal(FleeingConstantTrees.Length, operations.Count);

        var trees = new List<string>();
        foreach (var operation in operations)
        {
            Assert.Equal("PatchOperationAdd",
                operation.Attribute("Class")?.Value);
            Assert.Equal("Prepend", operation.Element("order")?.Value);
            var added = operation.Element("value")?.Elements().ToList()
                ?? new List<XElement>();
            Assert.Single(added);
            Assert.Equal("li", added[0].Name.LocalName);
            Assert.Equal(FleeGiverClass, added[0].Attribute("Class")?.Value);

            string xpath = Compact(operation.Element("xpath")?.Value);
            Match tree = Regex.Match(xpath,
                "^Defs/ThinkTreeDef\\[defName=\"(\\w+)\"\\]/thinkRoot/subNodes"
                + "/li\\[@Class=\"" + ConstantGateClass + "\"\\]/subNodes$");
            Assert.True(tree.Success, xpath);
            trees.Add(tree.Groups[1].Value);
        }

        Assert.Equal(
            FleeingConstantTrees.OrderBy(t => t),
            trees.OrderBy(t => t));
    }

    /// <summary>
    /// Against RimWorld's own think trees (where the game is installed):
    /// each XPath selects exactly one node, the gate's subnodes, and once
    /// applied the flee giver is the gate's first child, ahead of vanilla's
    /// JobGiver_FleePotentialExplosion or JobGiver_AnimalFlee.
    /// </summary>
    [Fact(DisplayName = "Flee patch applies to RimWorld's think trees")]
    public void FleePatch_should_apply_to_vanilla_think_trees()
    {
        string? data = RimWorldData();
        if (data == null)
        {
            return; // RimWorld is not installed here; see the test above.
        }

        string folder = Path.DirectorySeparatorChar + "ThinkTreeDefs"
            + Path.DirectorySeparatorChar;
        var vanilla = new XDocument(new XElement("Defs", Directory
            .EnumerateFiles(data, "*.xml", SearchOption.AllDirectories)
            .Where(f => f.Contains(folder, StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => XDocument.Load(f).Root
                ?.Elements("ThinkTreeDef") ?? Enumerable.Empty<XElement>())));

        foreach (var operation in FleePatchOperations())
        {
            string xpath = operation.Element("xpath")?.Value ?? "";
            var targets = vanilla.XPathSelectElements(xpath).ToList();
            Assert.Single(targets);
            var gate = targets[0].Parent;
            Assert.Equal(ConstantGateClass, gate?.Attribute("Class")?.Value);

            foreach (var added in operation.Element("value")!.Elements()
                .Reverse())
            {
                targets[0].AddFirst(new XElement(added));
            }

            var givers = targets[0].Elements("li")
                .Select(li => li.Attribute("Class")?.Value ?? "")
                .ToList();
            Assert.Equal(FleeGiverClass, givers[0]);
            Assert.Contains(givers.Skip(1), g =>
                g is "JobGiver_FleePotentialExplosion"
                    or "JobGiver_AnimalFlee");
        }
    }

    /// <summary>
    /// Every vanilla race (Core and every DLC installed) whose main think
    /// tree is a pawn's or a critter's (Humanlike, Animal, Insect, Dryad)
    /// runs one of the three patched constant trees, so all of them run;
    /// mechanoids, drones and Anomaly's entities keep their own.
    /// </summary>
    [Fact(DisplayName = "Every pawn and critter race runs a patched tree")]
    public void FleePatch_should_cover_every_pawn_and_critter_race()
    {
        string? data = RimWorldData();
        if (data == null)
        {
            return; // RimWorld is not installed here.
        }

        var races = Directory
            .EnumerateFiles(data, "*.xml", SearchOption.AllDirectories)
            .Where(f => f.Contains("ThingDefs_Races",
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => XDocument.Load(f).Root?.Elements("ThingDef")
                ?? Enumerable.Empty<XElement>())
            .ToList();
        var byName = new Dictionary<string, XElement>();
        foreach (var race in races)
        {
            if (race.Attribute("Name")?.Value is string name)
            {
                byName[name] = race;
            }
        }

        var pawnTrees = new[] { "Humanlike", "Animal", "Insect", "Dryad" };
        int checkedRaces = 0;
        foreach (var race in races.Where(r => !string.Equals(
            r.Attribute("Abstract")?.Value, "true",
            StringComparison.OrdinalIgnoreCase)))
        {
            string? main = InheritedRaceField(race, "thinkTreeMain", byName);
            if (main == null || !pawnTrees.Contains(main))
            {
                continue;
            }

            checkedRaces++;
            string? constant =
                InheritedRaceField(race, "thinkTreeConstant", byName);
            Assert.True(
                constant != null && FleeingConstantTrees.Contains(constant),
                $"{DefName(race)}: {main} with constant tree {constant}");
        }

        Assert.True(checkedRaces > 50, $"{checkedRaces} races checked");
    }

    /// <summary>
    /// A field of a race def's &lt;race&gt;, from the def itself or the
    /// nearest parent (ParentName) that sets it.
    /// </summary>
    /// <param name="def">The ThingDef.</param>
    /// <param name="field">The field under &lt;race&gt;.</param>
    /// <param name="byName">Abstract and named defs, by Name.</param>
    /// <returns>The value, or null if no def in the chain sets it.</returns>
    private static string? InheritedRaceField(
        XElement def, string field, Dictionary<string, XElement> byName)
    {
        var seen = new HashSet<XElement>();
        for (XElement? d = def; d != null && seen.Add(d);)
        {
            string? value = d.Element("race")?.Element(field)?.Value;
            if (value != null)
            {
                return value.Trim();
            }

            string? parent = d.Attribute("ParentName")?.Value;
            d = parent != null && byName.TryGetValue(parent, out var p)
                ? p : null;
        }

        return null;
    }

    /// <summary>
    /// The flee patch's operations, from the Patches fixtures.
    /// </summary>
    /// <returns>The Operation elements.</returns>
    private static List<XElement> FleePatchOperations()
    {
        var patch = LoadFixture(Path.Combine(
            "Patches", "BT_TractorBomb_Flee.xml"));
        Assert.Equal("Patch", patch.Root?.Name.LocalName);
        return patch.Root!.Elements("Operation").ToList();
    }

    /// <summary>
    /// The numbered list under "### Making Peace" in docs/TRACTOR-BOMB.md,
    /// in order.
    /// </summary>
    /// <returns>The lines, without their numbers.</returns>
    private static string[] MakingPeaceLinesFromSpec()
    {
        string path = Path.Combine(
            FixturesDirectory, "docs", "TRACTOR-BOMB.md");
        Assert.True(File.Exists(path), "The spec was not copied.");
        var lines = new List<string>();
        bool inSection = false;
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                if (inSection)
                {
                    break;
                }

                inSection = line == "### Making Peace";
                continue;
            }

            Match item = Regex.Match(line, @"^\d+\.\s+(.+)$");
            if (inSection && item.Success)
            {
                lines.Add(item.Groups[1].Value.Trim());
            }
        }

        Assert.True(lines.Count > 0, "No Making Peace list in the spec.");
        return lines.ToArray();
    }

    /// <summary>An XPath with its whitespace removed, for matching.</summary>
    /// <param name="xpath">The XPath; may be null.</param>
    /// <returns>The compact form.</returns>
    private static string Compact(string? xpath) =>
        Regex.Replace(xpath ?? "", @"\s+", "");

    /// <summary>
    /// RimWorld's Data folder (Core and DLCs), from RIMWORLD_DIR or the
    /// default Steam path, or null if neither holds it.
    /// </summary>
    /// <returns>The folder, or null.</returns>
    private static string? RimWorldData()
    {
        foreach (string? root in new[]
        {
            Environment.GetEnvironmentVariable("RIMWORLD_DIR"),
            DefaultRimWorldPath,
        })
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(
                Path.Combine(root, "Data", "Core", "Defs")))
            {
                return Path.Combine(root, "Data");
            }
        }

        return null;
    }
}
