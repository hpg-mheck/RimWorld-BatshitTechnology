using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the tour's tractor layouts, <see cref="TwinJunkyardLayout"/>
/// and <see cref="SingleTractorLayout"/>, against the planner's yard and
/// the Tractor Bomb's own rules.
/// </summary>
public sealed class TractorLayoutTests
{
    /// <summary>The yard, centred on the origin.</summary>
    private static readonly Box Yard = TourStagePlanner.YardAt(new Cell(0, 0));

    /// <summary>Every offset of both layouts lies inside the yard.</summary>
    [Fact(DisplayName = "Every layout offset is inside the yard")]
    public void Offsets_should_be_inside_the_yard()
    {
        Assert.All(TwinJunkyardLayout.AllOffsets(), c => Assert.True(Yard.Contains(c), $"{c}"));
        Assert.All(SingleTractorLayout.AllOffsets(), c => Assert.True(Yard.Contains(c), $"{c}"));
        Assert.True(Yard.Contains(new Box(-25, -1, -23, 1)), "the barn's 3x3 footprint");
    }

    /// <summary>
    /// Both twin robots see the army and plant close enough that their
    /// default pulls overlap.
    /// </summary>
    [Fact(DisplayName = "The twin pulls overlap")]
    public void Twin_pulls_should_overlap()
    {
        var plants = TwinJunkyardLayout.PredictedPlants();
        Assert.Equal(2, plants.Count);
        Assert.All(plants, p => Assert.NotNull(p));
        double apart = plants[0]!.Value.DistanceTo(plants[1]!.Value);
        Assert.True(apart < 2 * TractorBombYield.DefaultPull, $"apart {apart}");
        Assert.True(plants[0]!.Value.X < 0 && plants[1]!.Value.X > 0);
    }

    /// <summary>
    /// The junkyard's army has fourteen raiders on distinct cells, and no
    /// junk lies where a robot plants.
    /// </summary>
    [Fact(DisplayName = "The army has fourteen places and the planting spots are free")]
    public void Army_and_planting_spots_should_be_sound()
    {
        Assert.Equal(14, TwinJunkyardLayout.Army.Distinct().Count());
        var junk = TwinJunkyardLayout.Slag.Concat(TwinJunkyardLayout.Rock)
            .Concat(TwinJunkyardLayout.Stacks.Select(s => s.At))
            .Concat(TwinJunkyardLayout.Shelves).Concat(TwinJunkyardLayout.Crates)
            .ToHashSet();
        Assert.All(TwinJunkyardLayout.PredictedPlants(),
            p => Assert.DoesNotContain(p!.Value, junk));
    }

    /// <summary>
    /// The barn stands beyond the pull plus the blast of where its robot
    /// plants, so the scene never wrecks it.
    /// </summary>
    [Fact(DisplayName = "The barn is beyond pull plus blast")]
    public void Barn_should_be_beyond_pull_and_blast()
    {
        Cell? plant = SingleTractorLayout.PredictedPlant();
        Assert.NotNull(plant);
        double gap = plant!.Value.DistanceTo(SingleTractorLayout.Barn);
        Assert.True(
            gap >= TractorBombYield.DefaultPull + TractorBombYield.DefaultBlast + 1,
            $"barn {gap:F1} tiles from the plant");
    }

    /// <summary>
    /// The party is within a robot's sight of the barn's door, so the robot
    /// finds it from where it starts.
    /// </summary>
    [Fact(DisplayName = "The party is within 45 tiles of the barn's door")]
    public void Party_should_be_within_sight_of_the_door()
    {
        Assert.Equal(45, TractorPlanting.RobotSight);
        Assert.All(SingleTractorLayout.Party, p => Assert.True(
            p.DistanceTo(SingleTractorLayout.BarnDoor) <= TractorPlanting.RobotSight));
    }

    /// <summary>
    /// The stray stands inside the predicted pull, so the robot protests,
    /// and outside the party.
    /// </summary>
    [Fact(DisplayName = "The stray is inside the pull, so the robot protests")]
    public void Stray_should_be_inside_the_pull()
    {
        Cell plant = SingleTractorLayout.PredictedPlant()!.Value;
        Assert.True(
            plant.DistanceTo(SingleTractorLayout.Stray) < TractorBombYield.DefaultPull);
        Assert.DoesNotContain(SingleTractorLayout.Stray, SingleTractorLayout.Party);
    }

    /// <summary>
    /// A robot that sees nothing plants nowhere; one already within its
    /// stop plants where it stands.
    /// </summary>
    [Fact(DisplayName = "PlantFrom needs sight and stops short")]
    public void PlantFrom_should_need_sight_and_stop_short()
    {
        var party = new[] { new Cell(0, 0) };
        Assert.Null(TractorPlanting.PlantFrom(new Cell(46, 0), party, 3));
        Assert.Equal(new Cell(2, 0), TractorPlanting.PlantFrom(new Cell(2, 0), party, 3));
        Assert.Equal(new Cell(9, 0), TractorPlanting.PlantFrom(new Cell(30, 0), party, 9));
        Assert.Null(TractorPlanting.PlantFrom(new Cell(1, 0), null!, 3));
    }
}
