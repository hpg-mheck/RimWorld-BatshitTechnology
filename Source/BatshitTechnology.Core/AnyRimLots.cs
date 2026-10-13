using System;
using System.Collections.Generic;
using System.Linq;

namespace BatshitTechnology.Core;

/// <summary>
/// AnyRim's buildings: the eight lots along Main Street, their front
/// yards, the outbuildings and the residents (docs/SHOWCASE.md, "Lots",
/// "Buildings" and "Residents"). Coordinates are town-local. The three
/// model homes share Plan A, drawn for the west side and mirrored with
/// <see cref="Footprint.MirrorX"/> for the east.
/// </summary>
internal sealed partial class AnyRimBuilder
{
    /// <summary>The reactor's anchor, in Substation No. 1.</summary>
    internal static readonly Cell ReactorAnchor = new(-32, 0);

    /// <summary>The welcome sign, on the south entrance's east verge.</summary>
    internal static readonly Cell WelcomeSignCell = new(5, -24);

    /// <summary>The exit sign, on the east arm.</summary>
    internal static readonly Cell ExitSignCell = new(31, -4);

    /// <summary>The cemetery gate, where Main Street's road ends.</summary>
    internal static readonly Cell CemeteryGate = new(0, 56);

    /// <summary>The substation gate, where the west arm ends.</summary>
    internal static readonly Cell SubstationGate = new(-27, 0);

    /// <summary>Public Works' front door, under the incident board.</summary>
    internal static readonly Cell PublicWorksDoor = new(7, -11);

    /// <summary>The player stockpile beside the Artificer.</summary>
    internal static readonly Box ArtificerStockpile = new(14, -9, 15, -8);

    /// <summary>
    /// The residents (docs/SHOWCASE.md, "Residents"): key, names, sex, age,
    /// home and the apparel worn over generated clothes.
    /// </summary>
    private static readonly (string Key, string First, string Nick, string Last,
        bool Female, int Age, string Home, string[] Apparel)[] Cast =
    {
        ("Hugh", "Hugh", "Lame Duck", "Moribund", false, 63, "E2",
            new[] { "Apparel_BowlerHat", "Apparel_CollarShirt" }),
        ("Wendy", "Wendy", "Any Minute", "Now", true, 34, "W2",
            Array.Empty<string>()),
        ("Ernest", "Ernest", "Next", "Inline", false, 41, "W2",
            new[] { "Apparel_CowboyHat" }),
        ("Shirley", "Shirley", "Probably Fine", "Knott", true, 52, "W3",
            new[] { "Apparel_PsychicFoilHelmet" }),
        ("Constance", "Constance", "Collateral", "Damage", true, 46, "E3",
            Array.Empty<string>()),
        ("Barry", "Barry", "Six Feet", "Graves", false, 58, "W4",
            new[] { "Apparel_Duster", "Apparel_BowlerHat" }),
        ("Bea", "Bea", "Right Back", "Shortly", true, 29, "W1",
            Array.Empty<string>()),
        ("Morty", "Morty", "Two Weeks", "Tallis", false, 64, "E1",
            new[] { "Apparel_SimpleHelmet" }),
    };

    // ===== Lots =====

    /// <summary>
    /// The eight lots. Each building is 9 tiles along the street, its
    /// facade 7 tiles from the centre line, behind a 3-deep front yard.
    /// </summary>
    private void Lots()
    {
        FiveAndDime();
        PublicWorks();
        var now = new Lot("W2", "The Now residence", BuildingRole.Residence,
            false, 2, 7, 9, Wood, Wood, "WoodPlankFloor");
        PlanA(now, new[] { "Wendy", "Ernest" }, nowHouse: true);
        TownHall();
        var knott = new Lot("W3", "The Knott residence", BuildingRole.Residence,
            false, 3, 21, 9, "BlocksSandstone", Wood, "CarpetMustard");
        PlanA(knott, new[] { "Shirley" }, nowHouse: false);
        var damage = new Lot("E3", "The Damage residence", BuildingRole.Residence,
            true, 3, 21, 9, Steel, Steel, "MetalTile");
        PlanA(damage, new[] { "Constance" }, nowHouse: false);
        FuneralHome();
        SavingsAndLoan();

        // The houses' front-yard furniture.
        HouseYard(now, oakNorth: true);
        Add(Thing("StandingLamp", new Cell(-6, 10))); // the Nows' porch light
        HouseYard(knott, oakNorth: true);
        HouseYard(damage, oakNorth: false);

        // The Damages' lawn chairs face the street, a six-pack beside them.
        foreach (int z in new[] { 27, 28, 29 })
        {
            Add(Thing("Armchair", new Cell(5, z), Facing.West) with { Stuff = Cloth });
        }

        Add(Thing("EndTable", new Cell(5, 26)) with { Stuff = Wood });
        Add(Stack("Beer", new Cell(5, 26), 6));

        // The Knotts' potato patch, between the house and the shelter.
        foreach (Cell c in new Box(-18, 21, -16, 22).Cells())
        {
            Lay(c, Soil);
            Add(Plant("Plant_Potato", c) with { Growth = 0.5f });
        }
    }

    /// <summary>
    /// W1, the Five-and-Dime (limestone): three aisles of shelves, six
    /// singularity grenades on a shelf by the till, a lunch counter, and
    /// Bea's bed in a two-by-two stockroom; a vending machine at the door
    /// and a shopping cart in the gravel lot.
    /// </summary>
    private void FiveAndDime()
    {
        var lot = new Lot("W1", "AnyRim Five-and-Dime & Lunch Counter",
            BuildingRole.Surprise, false, 1, -15, 9,
            "BlocksLimestone", "BlocksLimestone", "TileLimestone");
        const string key = "W1";
        var stockroom = new Cell(-12, -13);
        Shell(lot, false, new[] { lot.Front });
        Walls(lot, false, new Cell(-12, -14), new Cell(-12, -12),
            new Cell(-14, -12), new Cell(-13, -12));
        Door(lot, false, lot.Front);
        Door(lot, false, stockroom);

        // Lunch counter: stove on the north wall, a cook's aisle, a
        // counter of two tables, and four stools facing it.
        Add(Thing("ElectricStove", new Cell(-13, -8)) with { BuildingKey = key });
        Add(Furniture(Over("Table1x2c", new Box(-14, -10, -13, -10), Facing.East), key));
        Add(Furniture(Over("Table1x2c", new Box(-12, -10, -11, -10), Facing.East), key));
        for (int x = -14; x <= -11; x++)
        {
            Add(Furniture(Thing("Stool", new Cell(x, -11)), key));
        }

        // Three aisles, the grenade shelf and the till.
        Box[] shelves =
        {
            new(-10, -14, -9, -14), new(-10, -12, -9, -12),
            new(-10, -8, -9, -8), new(-10, -10, -9, -10),
        };
        foreach (Box shelf in shelves)
        {
            Add(Over("Shelf", shelf, Facing.North) with
            {
                Stuff = Steel, BuildingKey = key,
            });
        }

        Add(Furniture(Over("Table1x2c", new Box(-8, -10, -8, -9), Facing.North), key));
        Add(Thing("StandingLamp", new Cell(-11, -14)) with { BuildingKey = key });

        // Bea's stockroom.
        Add(Furniture(Thing("Bed", new Cell(-14, -14)), key) with { ReservedFor = "Bea" });
        Add(Heater(new Cell(-13, -14), key));

        Stock(key, new Cell(-10, -14), ("Chocolate", 20), ("MealSurvivalPack", 10));
        Stock(key, new Cell(-9, -14), ("Pemmican", 30));
        Stock(key, new Cell(-10, -12), ("Beer", 12), ("Kibble", 40));
        Stock(key, new Cell(-9, -12), ("MedicineHerbal", 10));
        Stock(key, new Cell(-10, -8), ("Cloth", 50));
        Stock(key, new Cell(-9, -8), ("Cloth", 50));
        foreach (Cell c in new[] { new Cell(-10, -10), new Cell(-9, -10) })
        {
            for (int i = 0; i < 3; i++)
            {
                Add(Stack("BT_ErgonomicSingularityGrenade", c, 1) with { BuildingKey = key });
            }
        }

        Register(lot, false,
            new Box(-11, -14, -8, -8), new Box(-14, -11, -12, -8),
            new Box(-14, -14, -13, -13));
        Yard(lot, new[] { lot.Front.Z });
        Add(Thing("AncientVendingMachine", new Cell(-6, -10)));
        LayBox(new Box(-15, -20, -7, -16), "Gravel");
        Add(Thing("AncientShoppingCart", new Cell(-11, -18)));
    }

    /// <summary>
    /// E1, Public Works (steel, concrete, an autodoor): the
    /// Transmogrifier and Artificer with granite chunks and a player
    /// stockpile, the crated Random Replicator, a shelf of batshittium,
    /// Morty's bed and desk, and the "employee wellness" room with the
    /// Brain Squeezer. A back door leads to the Ballista yard.
    /// </summary>
    private void PublicWorks()
    {
        var lot = new Lot("E1", "AnyRim Light, Power & Public Works",
            BuildingRole.Utility, true, 1, -15, 11, Steel, Steel, "Concrete");
        const string key = "E1";
        var back = new Cell(17, -10);
        var wellness = new Cell(13, -13);
        Shell(lot, false, new[] { lot.Front, back });
        Walls(lot, false, new Cell(13, -14), new Cell(13, -12), new Cell(13, -11),
            new Cell(14, -11), new Cell(15, -11), new Cell(16, -11));
        Add(Thing("Autodoor", lot.Front) with { Stuff = Steel, BuildingKey = key });
        Door(lot, false, back);
        Door(lot, false, wellness);

        Add(Over("BT_BatshittiumTransmogrifier", new Box(9, -9, 10, -8), Facing.North) with
        {
            Owner = Owner.Player, SuspendBills = true, BuildingKey = key,
        });
        Add(Over("BT_BatshittiumArtificer", new Box(12, -9, 13, -8), Facing.North) with
        {
            Owner = Owner.Player, Refuel = true, AutoRefuelOff = true,
            BuildingKey = key,
        });
        Add(Thing("StandingLamp", new Cell(8, -8)) with { BuildingKey = key });
        Add(Thing("StandingLamp", new Cell(8, -14)) with { BuildingKey = key });
        Add(Heater(new Cell(16, -8), key));
        Add(Thing("ShelfSmall", new Cell(16, -9)) with { Stuff = Steel, BuildingKey = key });
        Stock(key, new Cell(16, -9), ("BT_Batshittium", 30));

        // Morty's corner.
        Add(Furniture(Over("Bed", new Box(11, -14, 11, -13), Facing.North), key) with
        {
            ReservedFor = "Morty",
        });
        Add(Furniture(Over("Table1x2c", new Box(9, -14, 10, -14), Facing.East), key));
        Add(Furniture(Thing("DiningChair", new Cell(9, -13), Facing.South), key));

        // Employee wellness.
        Add(Thing("BT_BrainSqueezer", new Cell(16, -13), Facing.West) with
        {
            Owner = Owner.Player, SwitchedOff = true, BuildingKey = key,
        });
        Add(Furniture(Thing("PlantPot", new Cell(16, -14)), key));
        Add(Plant("Plant_Daylily", new Cell(16, -14)) with { BuildingKey = key });

        foreach (int z in new[] { -8, -9, -10 })
        {
            Stock(key, new Cell(11, z), ("ChunkGranite", 1));
        }

        Add(Make(PlacementKind.Crate, "BT_RandomReplicator", new Cell(10, -12)) with
        {
            Owner = Owner.Player, BuildingKey = key,
        });

        Register(lot, false,
            new Box(8, -14, 12, -8), new Box(13, -10, 16, -8),
            new Box(14, -14, 16, -12));
        Yard(lot, new[] { lot.Front.Z });
    }

    /// <summary>
    /// E2, Town Hall and the Volunteer Fire Department (wood): a council
    /// chamber seating all eight residents, a fire bay of three firefoam
    /// poppers, and the mayor's office with his bed.
    /// </summary>
    private void TownHall()
    {
        var lot = new Lot("E2", "Town Hall & Volunteer Fire Department",
            BuildingRole.Civic, true, 2, 7, 11, Wood, Wood, "WoodPlankFloor");
        const string key = "E2";
        var bay = new Cell(13, 8);
        var office = new Cell(13, 13);
        Shell(lot, false, new[] { lot.Front });
        Walls(lot, false, new Cell(13, 9), new Cell(13, 10), new Cell(13, 11),
            new Cell(13, 12), new Cell(13, 14), new Cell(14, 11), new Cell(15, 11),
            new Cell(16, 11));
        Door(lot, false, lot.Front);
        Door(lot, false, bay);
        Door(lot, false, office);

        Add(Furniture(Over("Table2x4c", new Box(10, 9, 11, 12), Facing.North), key));
        for (int z = 9; z <= 12; z++)
        {
            Add(Furniture(Thing("DiningChair", new Cell(9, z), Facing.East), key));
            Add(Furniture(Thing("DiningChair", new Cell(12, z), Facing.West), key));
        }

        Add(Thing("StandingLamp", new Cell(8, 14)) with { BuildingKey = key });
        for (int z = 8; z <= 10; z++)
        {
            Add(Thing("FirefoamPopper", new Cell(16, z)) with { BuildingKey = key });
        }

        Add(Heater(new Cell(14, 12), key));
        Add(Thing("StandingLamp", new Cell(15, 12)) with { BuildingKey = key });
        Add(Furniture(Over("Bed", new Box(16, 12, 16, 13), Facing.North), key) with
        {
            ReservedFor = "Hugh",
        });
        Add(Thing("Armchair", new Cell(14, 14), Facing.East) with
        {
            Stuff = Cloth, BuildingKey = key,
        });
        Add(Furniture(Over("Table1x2c", new Box(15, 14, 16, 14), Facing.East), key));

        Register(lot, false,
            new Box(8, 8, 12, 14), new Box(14, 8, 16, 10), new Box(14, 12, 16, 14));
        Yard(lot, new[] { lot.Front.Z });
    }

    /// <summary>
    /// W4, Graves and Sons (marble, burgundy carpet, an ornate front
    /// door): a showroom of three sarcophagi in three materials, a viewing
    /// room of two rows of chairs facing an empty sarcophagus between
    /// potted daylilies, and Barry's back office.
    /// </summary>
    private void FuneralHome()
    {
        var lot = new Lot("W4", "Graves & Sons Funeral Home", BuildingRole.Surprise,
            false, 4, 35, 11, "BlocksMarble", "BlocksMarble", "CarpetBurgundy");
        const string key = "W4";
        var frontPair = new Box(-7, 38, -7, 39);
        var office = new Cell(-11, 37);
        var viewing = new Cell(-11, 40);
        Shell(lot, false, frontPair.Cells().ToArray());
        Walls(lot, false, new Cell(-11, 36), new Cell(-11, 38), new Cell(-11, 39),
            new Cell(-11, 41), new Cell(-11, 42));
        for (int x = -16; x <= -12; x++)
        {
            Walls(lot, false, new Cell(x, 38));
        }

        Add(Over("OrnateDoor", frontPair, Facing.East) with
        {
            Stuff = lot.DoorStuff, BuildingKey = key,
        });
        Door(lot, false, office);
        Door(lot, false, viewing);

        // Showroom: marble, steel and wood.
        string[] materials = { "BlocksMarble", Steel, Wood };
        for (int i = 0; i < 3; i++)
        {
            Add(Thing("Sarcophagus", new Cell(-10 + i, 41)) with
            {
                Stuff = materials[i], BuildingKey = key,
            });
        }

        Add(Thing("StandingLamp", new Cell(-10, 36)) with { BuildingKey = key });

        // Barry's office.
        Add(Furniture(Over("Bed", new Box(-16, 36, -15, 36), Facing.East), key) with
        {
            ReservedFor = "Barry",
        });
        Add(Furniture(Thing("EndTable", new Cell(-16, 37)), key));
        Add(Heater(new Cell(-13, 36), key));

        // The viewing room.
        Add(Thing("Sarcophagus", new Cell(-16, 40)) with
        {
            Stuff = "BlocksMarble", BuildingKey = key,
        });
        foreach (Cell pot in new[] { new Cell(-16, 39), new Cell(-16, 42) })
        {
            Add(Furniture(Thing("PlantPot", pot), key));
            Add(Plant("Plant_Daylily", pot) with { BuildingKey = key });
        }

        for (int x = -14; x <= -13; x++)
        {
            for (int z = 40; z <= 42; z++)
            {
                Add(Furniture(Thing("DiningChair", new Cell(x, z), Facing.West), key));
            }
        }

        Register(lot, false,
            new Box(-10, 36, -8, 42), new Box(-16, 36, -12, 37),
            new Box(-16, 39, -12, 42));
        Yard(lot, new[] { 39, 38 }); // gate on the front door's row
    }

    /// <summary>
    /// E4, the Savings and Loan (granite, marble tile): a teller counter,
    /// and a plasteel vault with a plasteel door holding 2,000 silver and
    /// 50 gold; marble columns and an ATM out front. Nobody lives here.
    /// </summary>
    private void SavingsAndLoan()
    {
        var lot = new Lot("E4", "AnyRim Savings & Loan", BuildingRole.Surprise,
            true, 4, 35, 9, "BlocksGranite", "BlocksGranite", "TileMarble");
        const string key = "E4";
        var vaultDoor = new Cell(11, 39);
        var vault = new Box(11, 37, 15, 41);
        Shell(lot, false, new[] { lot.Front },
            c => vault.Contains(c) ? "Plasteel" : lot.WallStuff);
        foreach (Cell c in vault.EdgeCells())
        {
            if (c.X < 15 && c != vaultDoor)
            {
                Add(Thing("Wall", c) with { Stuff = "Plasteel", BuildingKey = key });
            }
        }

        Door(lot, false, lot.Front);
        Add(Thing("Door", vaultDoor) with { Stuff = "Plasteel", BuildingKey = key });

        Add(Furniture(Over("Table1x2c", new Box(10, 36, 10, 37), Facing.North), key));
        Add(Furniture(Over("Table1x2c", new Box(10, 41, 10, 42), Facing.North), key));
        Add(Furniture(Thing("Stool", new Cell(11, 36), Facing.West), key));
        Add(Furniture(Thing("Stool", new Cell(11, 42), Facing.West), key));
        Add(Thing("StandingLamp", new Cell(8, 36)) with { BuildingKey = key });

        foreach (Cell c in new[]
        {
            new Cell(13, 38), new Cell(14, 38), new Cell(13, 40), new Cell(14, 40),
        })
        {
            Stock(key, c, ("Silver", 500));
        }

        Stock(key, new Cell(14, 39), ("Gold", 50));

        Register(lot, false,
            new Box(8, 36, 10, 42), new Box(11, 36, 14, 36), new Box(11, 42, 14, 42),
            new Box(12, 38, 14, 40));
        Yard(lot, new[] { lot.Front.Z });
        Add(Thing("Column", new Cell(6, 38)) with { Stuff = "BlocksMarble" });
        Add(Thing("Column", new Cell(6, 40)) with { Stuff = "BlocksMarble" });
        Add(Thing("AncientATM", new Cell(5, 38)));
    }

    /// <summary>
    /// Plan A, shared by the three model homes and drawn for the west side
    /// (mirrored for the east): a 3 by 7 living room on the street side,
    /// then a partition with two doors to a 3 by 3 bedroom and a 3 by 3
    /// kitchen.
    /// </summary>
    /// <param name="lot">The lot.</param>
    /// <param name="sleepers">
    /// Resident keys: two for two single beds, one for a double bed.
    /// </param>
    /// <param name="nowHouse">
    /// The Now house's touches: dinner for two on the table, and one
    /// armchair turned to the window beside the evening paper.
    /// </param>
    private void PlanA(Lot lot, string[] sleepers, bool nowHouse)
    {
        bool m = lot.East;
        string key = lot.Key;
        int z0 = lot.MinZ;
        var front = new Cell(-7, z0 + 4);
        var toBedroom = new Cell(-11, z0 + 3);
        var toKitchen = new Cell(-11, z0 + 5);
        Shell(lot, m, new[] { front });
        for (int z = z0 + 1; z <= z0 + 7; z++)
        {
            if (z != toBedroom.Z && z != toKitchen.Z)
            {
                Walls(lot, m, new Cell(-11, z));
            }
        }

        for (int x = -14; x <= -12; x++)
        {
            Walls(lot, m, new Cell(x, z0 + 4));
        }

        Door(lot, m, front);
        Door(lot, m, toBedroom);
        Door(lot, m, toKitchen);

        // Living room. Columns x -10 (back), -9, -8 (facade side).
        Add(m, Furniture(Thing("DiningChair", new Cell(-10, z0 + 7), Facing.East), key));
        Add(m, Furniture(Over("Table2x2c", new Box(-9, z0 + 6, -8, z0 + 7), Facing.North), key));
        Add(m, Furniture(Thing("DiningChair", new Cell(-8, z0 + 5), Facing.North), key));
        Add(m, Thing("Armchair", new Cell(-9, z0 + 3), Facing.South) with
        {
            Stuff = Cloth, BuildingKey = key,
        });
        Add(m, Thing("Armchair", new Cell(-8, z0 + 3), nowHouse ? Facing.East : Facing.South) with
        {
            Stuff = Cloth, BuildingKey = key,
        });
        Add(m, Thing("StandingLamp", new Cell(-10, z0 + 2)) with { BuildingKey = key });
        Add(m, Furniture(Thing("EndTable", new Cell(-8, z0 + 2)), key));
        Add(m, Furniture(Thing("PlantPot", new Cell(-10, z0 + 1)), key));
        Add(m, Plant("Plant_Rose", new Cell(-10, z0 + 1)) with { BuildingKey = key });
        Add(m, Thing("TubeTelevision", new Cell(-9, z0 + 1)) with
        {
            SwitchedOff = true, BuildingKey = key,
        });
        Add(m, Heater(new Cell(-8, z0 + 1), key));

        // Bedroom.
        if (sleepers.Length == 2)
        {
            Add(m, Furniture(Thing("Bed", new Cell(-14, z0 + 1)), key) with
            {
                ReservedFor = sleepers[0],
            });
            Add(m, Furniture(Thing("Bed", new Cell(-13, z0 + 1)), key) with
            {
                ReservedFor = sleepers[1],
            });
        }
        else
        {
            var bed = new Box(-14, z0 + 1, -13, z0 + 2);
            Add(m, Furniture(Over("DoubleBed", bed, Facing.North), key) with
            {
                ReservedFor = sleepers.Single(),
            });
        }

        // Kitchen: Operation Cue's pantry test.
        Add(m, Thing("AncientRefrigerator", new Cell(-14, z0 + 5)) with { BuildingKey = key });
        Add(m, Thing("AncientKitchenSink", new Cell(-14, z0 + 6)) with { BuildingKey = key });
        Add(m, Thing("AncientStove", new Cell(-14, z0 + 7)) with { BuildingKey = key });
        Add(m, Thing("ShelfSmall", new Cell(-13, z0 + 7), Facing.South) with
        {
            Stuff = Wood, BuildingKey = key,
        });
        Add(m, Stack("MealSurvivalPack", new Cell(-13, z0 + 7), 10) with { BuildingKey = key });
        Add(m, Stack("Beer", new Cell(-13, z0 + 7), 6) with { BuildingKey = key });

        if (nowHouse)
        {
            Add(m, Stack("MealFine", new Cell(-9, z0 + 7), 1) with { BuildingKey = key });
            Add(m, Stack("MealFine", new Cell(-8, z0 + 6), 1) with { BuildingKey = key });
            Add(m, Stack("Novel", new Cell(-8, z0 + 2), 1) with { BuildingKey = key });
        }

        Register(lot, m,
            new Box(-10, z0 + 1, -8, z0 + 7), new Box(-14, z0 + 1, -12, z0 + 3),
            new Box(-14, z0 + 5, -12, z0 + 7));
        Yard(lot, new[] { lot.Front.Z });
    }

    /// <summary>
    /// A model home's front-yard furniture: a postbox by the gate, a
    /// potted rose at the door, and one grown oak.
    /// </summary>
    /// <param name="lot">The lot.</param>
    /// <param name="oakNorth">Whether the oak stands at the north end.</param>
    private void HouseYard(Lot lot, bool oakNorth)
    {
        int s = lot.East ? 1 : -1;
        int row = lot.Front.Z;
        Add(Thing("AncientPostbox", new Cell(5 * s, row - 1)));
        var pot = new Cell(6 * s, row + 1);
        Add(Thing("PlantPot", pot) with { Stuff = Wood });
        Add(Plant("Plant_Rose", pot));
        Add(Plant("Plant_TreeOak", new Cell(5 * s, oakNorth ? lot.MinZ + 7 : lot.MinZ + 1)));
    }

    // ===== Outbuildings =====

    /// <summary>
    /// Substation No. 1, the Ballista yard, the Knott shelter, the two
    /// pens, AnyRim Memorial Gardens and the News Nob.
    /// </summary>
    private void Outbuildings()
    {
        Substation();
        BallistaYard();
        KnottShelter();
        Pen("DaisyPen", "Daisy's pen", false, "Boomalope", "Daisy");
        Pen("PriscillaPen", "Priscilla's pen", true, "Pig", "Priscilla");
        Cemetery();
        NewsNob();
    }

    /// <summary>
    /// An unroofed steel fence ring at the end of the west arm with the
    /// reactor (fuelled, auto-refuel off) and a small shelf of batshittium.
    /// No batteries.
    /// </summary>
    private void Substation()
    {
        const string key = "Substation";
        var outer = new Box(-37, -5, -27, 5);
        FenceRing(key, outer, Steel, SubstationGate);
        LayBox(outer.ExpandedBy(-1), "Concrete", null, key);
        Add(Thing(ReactorDef, ReactorAnchor) with
        {
            Owner = Owner.Player, Refuel = true, AutoRefuelOff = true,
            BuildingKey = key,
        });
        Add(Thing("ShelfSmall", new Cell(-35, -3)) with { Stuff = Steel, BuildingKey = key });
        Stock(key, new Cell(-35, -3), ("BT_Batshittium", 10));
        buildings.Add(new BuildingPlan(key, "Substation No. 1", BuildingRole.Outbuilding, outer)
        {
            FrontDoor = SubstationGate, StreetCell = new Cell(-LaneEnd, 0),
        });
    }

    /// <summary>
    /// The fenced, unroofed Ballista yard behind Public Works, ringed with
    /// sandbags: the Ballista (off, 16 painful bolts) and two magazines
    /// chained to it by shared edges (200 painful, 100 really painful).
    /// </summary>
    private void BallistaYard()
    {
        const string key = "BallistaYard";
        var outer = new Box(19, -16, 27, -6);
        var gate = new Cell(19, -10);
        FenceRing(key, outer, Steel, gate);
        LayBox(outer.ExpandedBy(-1), "Concrete", null, key);
        var gap = new Cell(20, -10);
        foreach (Cell c in outer.ExpandedBy(-1).EdgeCells())
        {
            if (c != gap)
            {
                Add(Thing("Sandbags", c) with { Stuff = Cloth, BuildingKey = key });
            }
        }

        Add(Thing("BT_BatshittiumBallista", new Cell(22, -11)) with
        {
            Owner = Owner.Player, SwitchedOff = true, BuildingKey = key,
            Load = new[] { ("BT_BoltPainful", 16) },
        });
        Add(Thing("BT_BallistaMagazine", new Cell(24, -11)) with
        {
            Owner = Owner.Player, BuildingKey = key,
            Load = new[] { ("BT_BoltPainful", 200) },
        });
        Add(Thing("BT_BallistaMagazine", new Cell(25, -11)) with
        {
            Owner = Owner.Player, BuildingKey = key,
            Load = new[] { ("BT_BoltReallyPainful", 100) },
        });
        buildings.Add(new BuildingPlan(key, "Ballista yard", BuildingRole.Outbuilding, outer)
        {
            FrontDoor = gate, StreetCell = new Cell(SidewalkLine, -11),
        });
    }

    /// <summary>
    /// The Knott shelter: granite blocks, a steel door facing west toward
    /// the reactor, a thick rock roof, and inside a bed, a shelf of 20
    /// survival meals and beer, and a lamp.
    /// </summary>
    private void KnottShelter()
    {
        const string key = "KnottShelter";
        var outer = new Box(-23, 23, -19, 27);
        var door = new Cell(-23, 25);
        Box inside = outer.ExpandedBy(-1);
        foreach (Cell c in outer.EdgeCells())
        {
            if (c != door)
            {
                Add(Thing("Wall", c) with { Stuff = "BlocksGranite", BuildingKey = key });
            }
        }

        Add(Thing("Door", door) with { Stuff = Steel, BuildingKey = key });
        LayBox(outer, "Concrete", null, key);
        foreach (Cell c in inside.Cells())
        {
            roofs.Add(new RoofPlacement(c, RoofKind.RockThick, key));
        }

        Add(Furniture(Thing("Bed", new Cell(-20, 24)), key));
        Add(Thing("ShelfSmall", new Cell(-20, 26), Facing.West) with
        {
            Stuff = Steel, BuildingKey = key,
        });
        Stock(key, new Cell(-20, 26),
            ("MealSurvivalPack", 10), ("MealSurvivalPack", 10), ("Beer", 6));
        Add(Thing("StandingLamp", new Cell(-22, 26)) with { BuildingKey = key });
        buildings.Add(new BuildingPlan(key, "The Knott shelter", BuildingRole.Outbuilding, outer)
        {
            FrontDoor = door, StreetCell = new Cell(-SidewalkLine, 25),
            Rooms = new[] { inside }, Enclosed = true,
        });
    }

    /// <summary>
    /// A five-by-five wooden pen behind a model home, with a named roaming
    /// animal (fences hold roamers whatever their faction) and hay.
    /// </summary>
    /// <param name="key">The pen's key.</param>
    /// <param name="name">The pen's name.</param>
    /// <param name="east">Behind the east row rather than the west.</param>
    /// <param name="animal">The animal's PawnKindDef.</param>
    /// <param name="animalName">The animal's name.</param>
    private void Pen(string key, string name, bool east, string animal, string animalName)
    {
        Box outer = As(east, new Box(-23, 29, -19, 33));
        Cell gate = As(east, new Cell(-19, 31));
        FenceRing(key, outer, Wood, gate);
        Add(Make(PlacementKind.Animal, animal, As(east, new Cell(-21, 31))) with
        {
            Name = animalName, BuildingKey = key,
        });
        Stock(key, As(east, new Cell(-20, 32)), ("Hay", 30));
        buildings.Add(new BuildingPlan(key, name, BuildingRole.Outbuilding, outer)
        {
            FrontDoor = gate,
            StreetCell = new Cell(east ? SidewalkLine : -SidewalkLine, 25),
        });
    }

    /// <summary>
    /// AnyRim Memorial Gardens, where Main Street's road ends: a wooden
    /// fence, a lawn, two oaks, and ten graves in two rows, eight reserved
    /// (one per resident, Barry's included) and two not.
    /// </summary>
    private void Cemetery()
    {
        const string key = "Cemetery";
        var outer = new Box(-6, 56, 6, 66);
        FenceRing(key, outer, Wood, CemeteryGate);
        Box inside = outer.ExpandedBy(-1);
        LayBox(inside, Soil, null, key);
        lawn.AddRange(inside.Cells());

        string?[] reserved =
        {
            "Hugh", "Wendy", "Ernest", "Shirley", "Constance",
            "Barry", "Bea", "Morty", null, null,
        };
        int i = 0;
        foreach (int west in new[] { -5, 4 })
        {
            for (int z = 57; z <= 65; z += 2)
            {
                Add(Over("Grave", new Box(west, z, west + 1, z), Facing.East) with
                {
                    ReservedFor = reserved[i++], BuildingKey = key,
                });
            }
        }

        Add(Plant("Plant_TreeOak", new Cell(-2, 63)) with { BuildingKey = key });
        Add(Plant("Plant_TreeOak", new Cell(2, 63)) with { BuildingKey = key });
        buildings.Add(new BuildingPlan(
            key, "AnyRim Memorial Gardens", BuildingRole.Outbuilding, outer)
        {
            FrontDoor = CemeteryGate, StreetCell = new Cell(0, MainNorth),
        });
    }

    /// <summary>
    /// The News Nob, east of town: eight stools behind a row of sandbags,
    /// facing south-west toward Ground Zero.
    /// </summary>
    private void NewsNob()
    {
        const string key = "NewsNob";
        var outer = new Box(45, 11, 52, 12);
        for (int x = 45; x <= 52; x++)
        {
            Add(Thing("Sandbags", new Cell(x, 11)) with { Stuff = Cloth, BuildingKey = key });
            Add(Furniture(Thing("Stool", new Cell(x, 12), Facing.South), key));
        }

        buildings.Add(new BuildingPlan(key, "News Nob", BuildingRole.Outbuilding, outer)
        {
            StreetCell = new Cell(LaneEnd, 0),
        });
    }

    // ===== Residents =====

    /// <summary>
    /// The eight residents, each waiting just inside their own front door.
    /// </summary>
    private void Residents()
    {
        foreach (var r in Cast)
        {
            BuildingPlan home = buildings.Single(b => b.Key == r.Home);
            Cell door = home.FrontDoor
                ?? throw new InvalidOperationException($"{home.Key} has no front door.");
            var inward = new Cell(home.Side == "E" ? 1 : -1, 0);
            residents.Add(new ResidentPlan(
                r.Key, r.First, r.Nick, r.Last, r.Female, r.Age, r.Home, door + inward)
            {
                Apparel = r.Apparel,
            });
        }
    }

    // ===== Helpers =====

    /// <summary>
    /// A building's outer walls, floor (walls included) and constructed
    /// roof over everything inside the outer walls.
    /// </summary>
    /// <param name="lot">The lot.</param>
    /// <param name="mirror">Whether the cells given are for the west side.</param>
    /// <param name="openings">Wall cells left for doors (before mirroring).</param>
    /// <param name="stuffAt">Wall stuff by cell (after mirroring), or null.</param>
    private void Shell(
        Lot lot, bool mirror, Cell[] openings, Func<Cell, string>? stuffAt = null)
    {
        Box outer = lot.Outer;
        var open = new HashSet<Cell>(openings.Select(c => As(mirror, c)));
        foreach (Cell c in outer.EdgeCells())
        {
            if (!open.Contains(c))
            {
                Add(Thing("Wall", c) with
                {
                    Stuff = stuffAt?.Invoke(c) ?? lot.WallStuff, BuildingKey = lot.Key,
                });
            }
        }

        LayBox(outer, lot.Floor, null, lot.Key);
        foreach (Cell c in outer.ExpandedBy(-1).Cells())
        {
            roofs.Add(new RoofPlacement(c, RoofKind.Constructed, lot.Key));
        }
    }

    /// <summary>Inner walls of the lot's wall stuff.</summary>
    /// <param name="lot">The lot.</param>
    /// <param name="mirror">Whether the cells given are for the west side.</param>
    /// <param name="cells">The wall cells.</param>
    private void Walls(Lot lot, bool mirror, params Cell[] cells)
    {
        foreach (Cell c in cells)
        {
            Add(Thing("Wall", As(mirror, c)) with
            {
                Stuff = lot.WallStuff, BuildingKey = lot.Key,
            });
        }
    }

    /// <summary>A plain door of the lot's door stuff.</summary>
    /// <param name="lot">The lot.</param>
    /// <param name="mirror">Whether the cell given is for the west side.</param>
    /// <param name="at">The door's cell.</param>
    private void Door(Lot lot, bool mirror, Cell at) =>
        Add(Thing("Door", As(mirror, at)) with
        {
            Stuff = lot.DoorStuff, BuildingKey = lot.Key,
        });

    /// <summary>A fence ring of one stuff with a gate.</summary>
    /// <param name="key">The outbuilding's key.</param>
    /// <param name="outer">The ring.</param>
    /// <param name="stuff">Fence and gate stuff.</param>
    /// <param name="gate">The gate's cell, on the ring.</param>
    private void FenceRing(string key, Box outer, string stuff, Cell gate)
    {
        foreach (Cell c in outer.EdgeCells())
        {
            Add(Thing(c == gate ? "FenceGate" : "Fence", c) with
            {
                Stuff = stuff, BuildingKey = key,
            });
        }
    }

    /// <summary>
    /// A lot's front yard: soil lawn, a picket fence on the sidewalk edge
    /// with a gate at the walk, and a paved walk to the front door.
    /// </summary>
    /// <param name="lot">The lot.</param>
    /// <param name="walkRows">The rows the walk runs along.</param>
    private void Yard(Lot lot, int[] walkRows)
    {
        int s = lot.East ? 1 : -1;
        int gateRow = walkRows[0];
        for (int z = lot.MinZ; z <= lot.MinZ + 8; z++)
        {
            for (int x = 4; x <= 6; x++)
            {
                var c = new Cell(x * s, z);
                Lay(c, Soil);
                lawn.Add(c);
            }

            Add(Thing(z == gateRow ? "FenceGate" : "Fence", new Cell(4 * s, z)) with
            {
                Stuff = palette.FenceStuff,
            });
        }

        Lay(new Cell(4 * s, gateRow), palette.Sidewalk);
        foreach (int row in walkRows)
        {
            Lay(new Cell(5 * s, row), palette.Sidewalk);
            Lay(new Cell(6 * s, row), palette.Sidewalk);
        }
    }

    /// <summary>Records a lot's building plan.</summary>
    /// <param name="lot">The lot.</param>
    /// <param name="mirror">Whether the rooms given are for the west side.</param>
    /// <param name="rooms">Its rooms' interiors.</param>
    private void Register(Lot lot, bool mirror, params Box[] rooms)
    {
        int s = lot.East ? 1 : -1;
        buildings.Add(new BuildingPlan(lot.Key, lot.Name, lot.Role, lot.Outer)
        {
            FrontDoor = lot.Front,
            StreetCell = new Cell(SidewalkLine * s, lot.Front.Z),
            Rooms = rooms.Select(r => As(mirror, r)).ToList(),
            Enclosed = true,
            Side = lot.East ? "E" : "W",
            Slot = lot.Slot,
        });
    }

    /// <summary>Items on a cell, forbidden, belonging to a building.</summary>
    /// <param name="key">The building.</param>
    /// <param name="at">The cell.</param>
    /// <param name="stacks">Each stack's def and count.</param>
    private void Stock(string key, Cell at, params (string Def, int Count)[] stacks)
    {
        foreach ((string def, int count) in stacks)
        {
            Add(Stack(def, at, count) with { BuildingKey = key });
        }
    }

    /// <summary>Wood furniture belonging to a building.</summary>
    /// <param name="p">The furniture.</param>
    /// <param name="key">The building.</param>
    /// <returns>The furniture, made of wood.</returns>
    private static Placement Furniture(Placement p, string key) =>
        p with { Stuff = Wood, BuildingKey = key };

    /// <summary>A heater, built only if the town is built in the cold.</summary>
    /// <param name="at">Its cell.</param>
    /// <param name="key">The building it heats.</param>
    /// <returns>The placement.</returns>
    private Placement Heater(Cell at, string key) =>
        Thing("Heater", at) with { OnlyIfCold = true, BuildingKey = key };

    /// <summary>
    /// One lot on Main Street: its building's key, name, role, side and
    /// slot, its south edge and depth from the facade, and its materials.
    /// </summary>
    /// <param name="Key">The building's key ("W1" to "E4").</param>
    /// <param name="Name">Its name as players read it.</param>
    /// <param name="Role">What it is for.</param>
    /// <param name="East">On the east side of Main Street.</param>
    /// <param name="Slot">1 for the southernmost lot.</param>
    /// <param name="MinZ">The building's south wall (z).</param>
    /// <param name="Depth">Its depth from the facade, walls included.</param>
    /// <param name="WallStuff">Wall stuff.</param>
    /// <param name="DoorStuff">Door stuff.</param>
    /// <param name="Floor">Floor TerrainDef.</param>
    private sealed record Lot(
        string Key, string Name, BuildingRole Role, bool East, int Slot, int MinZ,
        int Depth, string WallStuff, string DoorStuff, string Floor)
    {
        /// <summary>The building's outer footprint: 9 along the street.</summary>
        public Box Outer => East
            ? new Box(7, MinZ, 7 + Depth - 1, MinZ + 8)
            : new Box(-7 - Depth + 1, MinZ, -7, MinZ + 8);

        /// <summary>The front door: mid-facade, on x = +/-7.</summary>
        public Cell Front => new(East ? 7 : -7, MinZ + 4);
    }
}
