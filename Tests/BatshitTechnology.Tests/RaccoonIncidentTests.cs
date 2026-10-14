using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the Raccoon Incident's places and people in
/// <see cref="RaccoonIncident"/> against AnyRim's plan and the grenade's
/// def: the raccoon comes in on Main Street, the guard's walk and post are
/// open ground, Constance's lawn chair is a chair, and the guard can reach
/// the street from his post before the raccoon reaches her.
/// </summary>
public sealed class RaccoonIncidentTests
{
    /// <summary>The plan the tour builds (seed 1, medium quality).</summary>
    private static readonly TownPlan Plan =
        TownPlan.AnyRim(1, TownPalette.For(TownQuality.Medium));

    /// <summary>
    /// The guard and the neighbour are residents, the guard lives at the
    /// bank, and he alone carries the grenade.
    /// </summary>
    [Fact(DisplayName = "The guard lives at the bank with the grenade")]
    public void Cast_should_be_in_the_plan()
    {
        ResidentPlan guard = Plan.Residents.Single(r => r.Key == RaccoonIncident.GuardKey);
        Assert.Equal(RaccoonIncident.BankKey, guard.HomeKey);
        Assert.Equal(RaccoonIncident.GrenadeDef, guard.Weapon);
        Assert.Equal(("Ned", "Overkill", "Lessly"), (guard.First, guard.Nick, guard.Last));
        Assert.Contains(Plan.Residents, r => r.Key == RaccoonIncident.NeighbourKey);
        Assert.Equal(
            "AnyRim Savings & Loan",
            Plan.Buildings.Single(b => b.Key == RaccoonIncident.BankKey).Name);
    }

    /// <summary>
    /// The raccoon comes in on Main Street's road, at its south end, and
    /// the guard's post is on the same road; neither cell is built on.
    /// </summary>
    [Fact(DisplayName = "Entry and post are open road on Main Street")]
    public void Entry_and_post_should_be_open_road()
    {
        string? road = TerrainAt(new Cell(0, 10));
        Assert.Equal(road, TerrainAt(RaccoonIncident.Entry));
        Assert.Equal(road, TerrainAt(RaccoonIncident.GuardPost));
        Assert.DoesNotContain(RaccoonIncident.Entry, Built());
        Assert.DoesNotContain(RaccoonIncident.GuardPost, Built());
        Assert.InRange(Math.Abs(RaccoonIncident.GuardPost.X), 0, 2);
    }

    /// <summary>
    /// The guard is found on the bank's front walk, in its yard, on open
    /// ground; Constance's chair is one of the Damages' lawn armchairs.
    /// </summary>
    [Fact(DisplayName = "The bank walk is open; the lawn chair is a chair")]
    public void Walk_and_chair_should_be_where_the_story_puts_them()
    {
        BuildingPlan bank = Plan.Buildings.Single(b => b.Key == RaccoonIncident.BankKey);
        Assert.Equal(bank.FrontDoor!.Value.Z, RaccoonIncident.BankWalk.Z);
        Assert.InRange(RaccoonIncident.BankWalk.X, 4, bank.FrontDoor.Value.X - 1);
        Assert.DoesNotContain(RaccoonIncident.BankWalk, Built());

        Placement chair = Assert.Single(
            Plan.Placements, p => p.At == RaccoonIncident.LawnChair && p.Def == "Armchair");
        Assert.Equal(Facing.West, chair.Rot);
        BuildingPlan home = Plan.Buildings.Single(b => b.Key == "E3");
        Assert.InRange(chair.At.Z, home.Outer.MinZ, home.Outer.MaxZ);
    }

    /// <summary>
    /// A raccoon coming north along Main Street's centre line comes within
    /// the guard's throw while still on the road, north of where it came
    /// in and south of Constance's chair, so he can throw before it
    /// reaches her; and Constance sits within the singularity's pull of
    /// that first cell, so it is she who is "collateral".
    /// </summary>
    [Fact(DisplayName = "The raccoon comes within the throw before it reaches her")]
    public void Raccoon_should_come_into_range_on_the_street()
    {
        int row = RaccoonIncident.FirstRowInRange();
        var first = new Cell(0, row);
        Assert.InRange(row, RaccoonIncident.Entry.Z + 1, RaccoonIncident.LawnChair.Z - 1);
        Assert.Equal(TerrainAt(new Cell(0, 10)), TerrainAt(first));
        Assert.True(Distance(RaccoonIncident.GuardPost, first) <= RaccoonIncident.ThrowRange);
        Assert.True(
            Distance(RaccoonIncident.GuardPost, first + new Cell(0, -1))
                > RaccoonIncident.ThrowRange);
        Assert.True(
            Distance(RaccoonIncident.LawnChair, first) <= RaccoonIncident.PullRadius);
    }

    /// <summary>
    /// The throw's range and the singularity's pull are the grenade's
    /// own, as Defs/ThingDefs/Items_SingularityGrenade.xml gives them.
    /// </summary>
    [Fact(DisplayName = "ThrowRange and PullRadius match the grenade's def")]
    public void Ranges_should_match_the_def()
    {
        XDocument doc = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "Defs", "ThingDefs",
            "Items_SingularityGrenade.xml"));
        List<XElement> defs = doc.Root!.Elements("ThingDef").ToList();
        XElement grenade = defs.Single(d =>
            d.Element("defName")?.Value == RaccoonIncident.GrenadeDef);
        XElement field = defs.Single(d =>
            d.Element("defName")?.Value == "BT_SingularityField");

        Assert.Equal(
            RaccoonIncident.ThrowRange,
            double.Parse(grenade.Descendants("range").Single().Value,
                CultureInfo.InvariantCulture));
        Assert.Equal(
            RaccoonIncident.PullRadius,
            double.Parse(field.Descendants("radius").Single().Value,
                CultureInfo.InvariantCulture));
    }

    /// <summary>The terrain the plan lays last on a cell.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>Its TerrainDef, or null if the plan lays none.</returns>
    private static string? TerrainAt(Cell cell) =>
        Plan.Terrain.LastOrDefault(t => t.At == cell)?.Def;

    /// <summary>
    /// The cells the plan's buildings cover, on the building layer
    /// (conduits run under the street and are walked over).
    /// </summary>
    /// <returns>The cells.</returns>
    private static HashSet<Cell> Built() =>
        new(Plan.Placements
            .Where(p => p.Kind is PlacementKind.Building or PlacementKind.Crate
                && TownDefs.All[p.Def].Layer == ThingLayer.Building)
            .SelectMany(p => p.Occupied.Cells()));

    /// <summary>The straight-line distance between two cells.</summary>
    /// <param name="a">One cell.</param>
    /// <param name="b">The other.</param>
    /// <returns>The distance, in tiles.</returns>
    private static double Distance(Cell a, Cell b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));
}
