using System.Linq;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the tour's tractor layouts, <see cref="TwinJunkyardLayout"/>
/// and <see cref="SingleTractorLayout"/>, against the planner's yard and
/// the Tractor Bomb's own rules, at the tour's settings: Popular (33.75
/// tiles since the pulls were raised by half) and Block Party (12).
/// </summary>
public sealed class TractorLayoutTests
{
    /// <summary>The tour's pull radius, in tiles.</summary>
    private static readonly double TourPull =
        TractorBombYield.Radius(TourStagePlanner.TourPull);

    /// <summary>The tour's blast radius, in tiles.</summary>
    private static readonly double TourBlast =
        TractorBombYield.Radius(TourStagePlanner.TourBlast);

    /// <summary>The yard, centred on the origin.</summary>
    private static readonly Box Yard = TourStagePlanner.YardAt(new Cell(0, 0));

    /// <summary>Every offset of both layouts lies inside the yard.</summary>
    [Fact(DisplayName = "Every layout offset is inside the yard")]
    public void Offsets_should_be_inside_the_yard()
    {
        Assert.All(TwinJunkyardLayout.AllOffsets(), c => Assert.True(Yard.Contains(c), $"{c}"));
        Assert.All(SingleTractorLayout.AllOffsets(), c => Assert.True(Yard.Contains(c), $"{c}"));
        Assert.True(Yard.Contains(new Box(-31, -1, -29, 1)), "the barn's 3x3 footprint");
    }

    /// <summary>
    /// Both twin robots see the army and plant close enough that their
    /// pulls overlap, at the tour's Popular and at the smoke test's
    /// default Neighborly alike; neither planted bomb is inside the
    /// other's Block Party blast before the pulls.
    /// </summary>
    [Fact(DisplayName = "The twin pulls overlap")]
    public void Twin_pulls_should_overlap()
    {
        var plants = TwinJunkyardLayout.PredictedPlants();
        Assert.Equal(2, plants.Count);
        Assert.All(plants, p => Assert.NotNull(p));
        double apart = plants[0]!.Value.DistanceTo(plants[1]!.Value);
        Assert.True(apart < 2 * TractorBombYield.DefaultPullRadius, $"apart {apart}");
        Assert.True(apart < 2 * TourPull, $"apart {apart}");
        Assert.True(apart > TourBlast, $"apart {apart}");
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
    /// Every cell of the barn's 3x3 footprint stands more than two tiles
    /// beyond the tour's 33.75-tile pull from where its robot plants (a
    /// tile or two of pathing either way), so the pull never rips it
    /// loose; the 12-tile blast falls far short of it. The robot is Nosy,
    /// so it stops 3 tiles short of the party.
    /// </summary>
    [Fact(DisplayName = "The barn is beyond the pull and the blast")]
    public void Barn_should_be_beyond_pull_and_blast()
    {
        Cell? plant = SingleTractorLayout.PredictedPlant();
        Assert.NotNull(plant);
        double nearest = Enumerable.Range(-1, 3)
            .SelectMany(dx => Enumerable.Range(-1, 3)
                .Select(dz => SingleTractorLayout.Barn + new Cell(dx, dz)))
            .Min(c => plant!.Value.DistanceTo(c));
        Assert.True(nearest > TourPull + 2, $"barn {nearest:F1} tiles from the plant");
        Assert.True(nearest > TourBlast + 2, $"barn {nearest:F1} tiles from the plant");
        Assert.Equal(TractorBombProximity.Nosy, SingleTractorLayout.TourProximity);
        Assert.Equal(3.0, SingleTractorLayout.TourStop);
        Assert.Equal(new Cell(9, 0), plant!.Value);
    }

    /// <summary>
    /// Why the scene's robot is Nosy: from the barn's door, a Prudent
    /// robot (15 tiles short of the party) would plant within the tour's
    /// pull of the barn, wherever in sight of the door the party stood.
    /// </summary>
    [Fact(DisplayName = "A Prudent robot from the door would pull the barn")]
    public void Prudent_robot_would_reach_the_barn()
    {
        double prudent = TractorBombApproach.StopDistance(
            TractorBombProximity.Prudent,
            TractorBombApproach.DefaultTimidStop, TourPull);
        Assert.Equal(15.0, prudent);

        // The farthest a robot can plant from the door: the party at the
        // edge of its sight, less the stop.
        Assert.True(TractorPlanting.RobotSight - prudent < TourPull + 2);
        Assert.True(TractorPlanting.RobotSight - SingleTractorLayout.TourStop
            > TourPull + 2);
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
    /// and outside the party; the party is inside the pull too, and inside
    /// the blast (the doom beat's frozen raiders). The stray stands well
    /// outside the blast, so it never freezes.
    /// </summary>
    [Fact(DisplayName = "The stray is inside the pull, so the robot protests")]
    public void Stray_should_be_inside_the_pull()
    {
        Cell plant = SingleTractorLayout.PredictedPlant()!.Value;
        double stray = plant.DistanceTo(SingleTractorLayout.Stray);
        Assert.True(stray < TourPull - 2, $"stray {stray:F1}");
        Assert.True(stray > TourBlast + 4, $"stray {stray:F1}");
        Assert.All(SingleTractorLayout.Party,
            p => Assert.True(plant.DistanceTo(p) <= TourPull));
        Assert.All(SingleTractorLayout.Party,
            p => Assert.True(plant.DistanceTo(p) <= TourBlast));
        Assert.DoesNotContain(SingleTractorLayout.Stray, SingleTractorLayout.Party);
    }

    /// <summary>
    /// Sent north once the robot protests, the stray leaves the pull
    /// within 20 tiles (the protest's 3 s, the tyres' half second and the
    /// shortest 3 s countdown give a walking colonist about 30), and ends
    /// beyond the flee distance, so no one sends it running back.
    /// </summary>
    [Fact(DisplayName = "The stray's retreat clears the pull in time")]
    public void Stray_retreat_should_clear_the_pull()
    {
        Cell plant = SingleTractorLayout.PredictedPlant()!.Value;
        Cell stray = SingleTractorLayout.Stray;
        int steps = Enumerable.Range(0, SingleTractorLayout.StrayRetreat + 1)
            .First(n => plant.DistanceTo(stray + new Cell(0, n)) > TourPull);
        Assert.True(steps <= 20, $"{steps} tiles to leave the pull");
        double end = plant.DistanceTo(
            stray + new Cell(0, SingleTractorLayout.StrayRetreat));
        Assert.True(end > TractorBombFlee.FleeDistance(TourPull, TourBlast),
            $"ends {end:F1} tiles out");
    }

    /// <summary>
    /// The single yard's junk (slice 19000d) lies inside the yard, inside
    /// the predicted plant's pull and outside its blast, so it is seen to
    /// slide before the blast takes it; on cells of its own, none where
    /// the party, the stray or the barn stand.
    /// </summary>
    [Fact(DisplayName = "The single yard's junk is in the pull, outside the blast")]
    public void Single_junk_should_be_in_the_pull_outside_the_blast()
    {
        Cell plant = SingleTractorLayout.PredictedPlant()!.Value;
        var cells = TourYardJunk.Single.Select(j => j.At).ToList();
        Assert.True(cells.Count >= 20, $"{cells.Count} pieces");
        Assert.Equal(cells.Count, cells.Distinct().Count());
        Assert.All(cells, c =>
        {
            Assert.True(Yard.Contains(c), $"{c} outside the yard");
            double d = plant.DistanceTo(c);
            Assert.True(d > TourBlast && d <= TourPull, $"{c} at {d:F1} tiles");
            Assert.DoesNotContain(c, SingleTractorLayout.Party);
            Assert.False(new Box(-32, -2, -28, 2).Contains(c), $"{c} by the barn");
        });
        Assert.All(TourYardJunk.Single, j => Assert.InRange(j.Count, 1, 75));
    }

    /// <summary>
    /// No piece of the single yard's junk lies within 3 tiles of the
    /// robot's road (the barn's door to where it plants) or of the stray's
    /// way out (its spot to its retreat), so neither has to go round it.
    /// </summary>
    [Fact(DisplayName = "The single yard's junk keeps off the road and the stray's way")]
    public void Single_junk_should_keep_off_the_road_and_the_strays_way()
    {
        Cell plant = SingleTractorLayout.PredictedPlant()!.Value;
        Cell stray = SingleTractorLayout.Stray;
        Cell away = stray + new Cell(0, SingleTractorLayout.StrayRetreat);
        Assert.All(TourYardJunk.Single, j =>
        {
            Assert.True(FromSegment(j.At, SingleTractorLayout.BarnDoor, plant) > 3,
                $"{j.At} on the robot's road");
            Assert.True(FromSegment(j.At, stray, away) > 3,
                $"{j.At} on the stray's way out");
        });
    }

    /// <summary>
    /// The twins' outer ring lies inside the yard, inside a pull and
    /// outside both blasts, off both robots' roads, and on cells the
    /// junkyard itself leaves free.
    /// </summary>
    [Fact(DisplayName = "The twins' outer junk is in a pull, outside both blasts")]
    public void Twins_outer_junk_should_be_in_a_pull_outside_the_blasts()
    {
        var plants = TwinJunkyardLayout.PredictedPlants().Select(p => p!.Value).ToList();
        var used = TwinJunkyardLayout.AllOffsets().ToHashSet();
        var cells = TourYardJunk.TwinsOuter.Select(j => j.At).ToList();
        Assert.True(cells.Count >= 12, $"{cells.Count} pieces");
        Assert.Equal(cells.Count, cells.Distinct().Count());
        Assert.All(cells, c =>
        {
            Assert.True(Yard.Contains(c), $"{c} outside the yard");
            Assert.DoesNotContain(c, used);
            double nearest = plants.Min(p => p.DistanceTo(c));
            Assert.True(nearest > TourBlast && nearest <= TourPull, $"{c} at {nearest:F1}");
            for (int i = 0; i < plants.Count; i++)
            {
                Assert.True(FromSegment(c, TwinJunkyardLayout.DriveIn[i], plants[i]) > 3,
                    $"{c} on robot {i}'s road");
            }
        });
    }

    /// <summary>
    /// The junk is made of things that neither burn nor go off when hit,
    /// so a pull and a blast in the yard start no fire beyond it.
    /// </summary>
    [Fact(DisplayName = "The tour's junk is nothing that burns or explodes")]
    public void Junk_should_be_neither_flammable_nor_explosive()
    {
        string[] allowed =
        {
            TourYardJunk.Slag, TourYardJunk.Rock, TourYardJunk.Crate, "Silver",
            "Gold", "Jade", "Uranium", "Plasteel", "ComponentIndustrial",
        };
        Assert.All(TourYardJunk.Single.Concat(TourYardJunk.TwinsOuter),
            j => Assert.Contains(j.DefName, allowed));
    }

    /// <summary>The distance from a cell to a straight segment.</summary>
    /// <param name="c">The cell.</param>
    /// <param name="a">One end.</param>
    /// <param name="b">The other end.</param>
    /// <returns>The distance, in tiles.</returns>
    private static double FromSegment(Cell c, Cell a, Cell b)
    {
        double dx = b.X - a.X;
        double dz = b.Z - a.Z;
        double length = (dx * dx) + (dz * dz);
        double t = length == 0 ? 0
            : System.Math.Clamp((((c.X - a.X) * dx) + ((c.Z - a.Z) * dz)) / length, 0, 1);
        double px = a.X + (t * dx) - c.X;
        double pz = a.Z + (t * dz) - c.Z;
        return System.Math.Sqrt((px * px) + (pz * pz));
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
