using System;
using System.Collections.Generic;

namespace BatshitTechnology.Core;

/// <summary>
/// The data behind <see cref="TownDefs.All"/>: every ThingDef and
/// PawnKindDef the AnyRim plan places, wears or loads, with the facts its
/// geometry depends on. Values were read from RimWorld 1.6's Core Defs and
/// the mod's Defs with ParentName inheritance resolved; TownDefsTests
/// re-checks them against the XML whenever the game is installed.
/// </summary>
internal static class TownDefCatalogue
{
    private static readonly string[] AnyStuff = { "Metallic", "Woody", "Stony" };
    private static readonly string[] Woody = { "Woody" };
    private static readonly string[] Stony = { "Stony" };
    private static readonly string[] Metallic = { "Metallic" };
    private static readonly string[] Fabric = { "Fabric" };
    private static readonly string[] Cloths = { "Fabric", "Leathery" };

    /// <summary>The catalogue, by defName.</summary>
    internal static IReadOnlyDictionary<string, DefInfo> Entries { get; } = Build();

    /// <summary>Builds the catalogue.</summary>
    /// <returns>Every entry, by defName (ordinal comparison).</returns>
    private static Dictionary<string, DefInfo> Build()
    {
        var d = new Dictionary<string, DefInfo>(StringComparer.Ordinal);

        // Structure. Doors and gates are Standable; fences PassThroughOnly.
        d["Wall"] = Solid(1, 1) with
        {
            IsWall = true, Rotatable = false, AcceptsStuff = AnyStuff,
        };
        d["Door"] = Edifice(1, 1) with
        {
            IsDoor = true, Rotatable = false, AcceptsStuff = AnyStuff,
        };
        d["Autodoor"] = Edifice(1, 1) with
        {
            IsDoor = true, Rotatable = false, AcceptsStuff = AnyStuff,
            Powered = true,
        };
        d["OrnateDoor"] = Edifice(2, 1) with
        {
            IsDoor = true, AcceptsStuff = AnyStuff,
        };
        d["Fence"] = Edifice(1, 1) with
        {
            IsFence = true, Rotatable = false, AcceptsStuff = AnyStuff,
        };
        d["FenceGate"] = Edifice(1, 1) with
        {
            IsDoor = true, Rotatable = false, AcceptsStuff = AnyStuff,
        };
        d["Column"] = Edifice(1, 1) with
        {
            Rotatable = false, AcceptsStuff = AnyStuff,
        };
        d["Sandbags"] = Edifice(1, 1) with
        {
            Rotatable = false, AcceptsStuff = Cloths,
        };
        d["PowerConduit"] = new DefInfo(1, 1, ThingLayer.Conduit, 0, 1)
        {
            TransmitsPower = true, Rotatable = false,
        };

        // Furniture.
        d["Bed"] = Furniture(1, 2, AnyStuff);
        d["DoubleBed"] = Furniture(2, 2, AnyStuff);
        d["Table1x2c"] = Furniture(1, 2, AnyStuff) with { ItemCapacity = 1 };
        d["Table2x2c"] = Furniture(2, 2, AnyStuff) with
        {
            ItemCapacity = 1, Rotatable = false,
        };
        d["Table2x4c"] = Furniture(2, 4, AnyStuff) with { ItemCapacity = 1 };
        d["EndTable"] = Furniture(1, 1, AnyStuff) with { ItemCapacity = 1 };
        d["DiningChair"] = Furniture(1, 1, new[] { "Metallic", "Woody" });
        d["Armchair"] = Furniture(1, 1, Cloths);
        d["Stool"] = Furniture(1, 1, AnyStuff);
        d["PlantPot"] = Furniture(1, 1, AnyStuff) with { Rotatable = false };
        d["Sarcophagus"] = Edifice(1, 2) with
        {
            AcceptsStuff = new[] { "Woody", "Metallic", "Stony" },
            HasQuality = true,
        };
        d["Grave"] = Edifice(1, 2);
        d["Shelf"] = Edifice(2, 1) with
        {
            ItemCapacity = 3, AcceptsStuff = AnyStuff, Minifiable = true,
        };
        d["ShelfSmall"] = Edifice(1, 1) with
        {
            ItemCapacity = 3, AcceptsStuff = AnyStuff, Minifiable = true,
        };

        // Powered fixtures and appliances.
        d["StandingLamp"] = Edifice(1, 1) with
        {
            Rotatable = false, Powered = true, Flickable = true,
            Minifiable = true,
        };
        d["TubeTelevision"] = Edifice(1, 1) with
        {
            Powered = true, Flickable = true, Minifiable = true,
        };
        d["Heater"] = Edifice(1, 1) with
        {
            Rotatable = false, Powered = true, Flickable = true,
            Minifiable = true,
        };
        d["ElectricStove"] = Edifice(3, 1) with
        {
            ItemCapacity = 1, InteractionCell = new Cell(0, -1),
            Powered = true, Flickable = true, HasBills = true,
            Minifiable = true,
        };
        d["FirefoamPopper"] = Edifice(1, 1) with
        {
            Rotatable = false, Minifiable = true,
        };

        // Ancient props: inert, non-claimable, PassThroughOnly.
        foreach (string prop in new[]
        {
            "AncientLamppost", "AncientPostbox", "AncientHydrant",
            "AncientVendingMachine", "AncientATM", "AncientShoppingCart",
            "AncientRefrigerator", "AncientStove", "AncientKitchenSink",
        })
        {
            d[prop] = Edifice(1, 1) with { Rotatable = false };
        }

        d["AncientRustedCar"] = Edifice(2, 4);
        d["AncientRustedTruck"] = Edifice(2, 4);
        d["AncientRustedCarFrame"] = Edifice(2, 3);

        // Batshit devices (the mod's Defs).
        d["BT_BatshittiumReactor"] = Solid(2, 2) with
        {
            Rotatable = false, TransmitsPower = true, Flickable = true,
            Refuelable = true, Minifiable = true,
        };
        d["BT_BatshittiumTransmogrifier"] = Solid(2, 2) with
        {
            InteractionCell = new Cell(0, -1), Powered = true,
            Flickable = true, HasBills = true, Minifiable = true,
        };
        d["BT_BatshittiumArtificer"] = Solid(2, 2) with
        {
            InteractionCell = new Cell(0, -1), Powered = true,
            Flickable = true, Refuelable = true, Minifiable = true,
        };
        d["BT_RandomReplicator"] = Solid(2, 2) with
        {
            InteractionCell = new Cell(0, -1), Powered = true,
            Flickable = true, Minifiable = true,
        };
        d["BT_BrainSqueezer"] = Edifice(1, 1) with
        {
            Powered = true, Flickable = true, Minifiable = true,
        };
        d["BT_BatshittiumBallista"] = Edifice(2, 2) with
        {
            Rotatable = false, Powered = true, Flickable = true,
            Minifiable = true, LoadCapacity = 16,
        };
        d["BT_BallistaMagazine"] = Edifice(1, 1) with
        {
            Powered = true, Flickable = true, Minifiable = true,
            LoadCapacity = 2560,
        };

        // Items.
        d["MealSurvivalPack"] = Item(10);
        d["MealFine"] = Item(10);
        d["Beer"] = Item(25);
        d["Chocolate"] = Item(75);
        d["Pemmican"] = Item(75);
        d["Kibble"] = Item(75);
        d["MedicineHerbal"] = Item(25);
        d["Hay"] = Item(200);
        d["ChunkGranite"] = Item(1);
        d["Silver"] = Item(500) with { StuffCategories = Metallic };
        d["Gold"] = Item(500) with { StuffCategories = Metallic };
        d["Novel"] = Item(1);
        d["BT_Batshittium"] = Item(75);
        d["BT_BoltPainful"] = Item(256);
        d["BT_BoltReallyPainful"] = Item(256);
        d["BT_ErgonomicSingularityGrenade"] = Item(1);

        // Stuff.
        d["WoodLog"] = Item(75) with { StuffCategories = Woody };
        d["Cloth"] = Item(75) with { StuffCategories = Fabric };
        d["Steel"] = Item(75) with { StuffCategories = Metallic };
        d["Plasteel"] = Item(75) with { StuffCategories = Metallic };
        foreach (string blocks in new[]
        {
            "BlocksLimestone", "BlocksSandstone", "BlocksGranite",
            "BlocksMarble",
        })
        {
            d[blocks] = Item(75) with { StuffCategories = Stony };
        }

        // Apparel the residents wear over their generated clothes.
        d["Apparel_BowlerHat"] = Item(1) with { AcceptsStuff = Cloths };
        d["Apparel_CollarShirt"] = Item(1) with { AcceptsStuff = Cloths };
        d["Apparel_CowboyHat"] = Item(1) with { AcceptsStuff = Cloths };
        d["Apparel_Duster"] = Item(1) with { AcceptsStuff = Cloths };
        d["Apparel_FlakVest"] = Item(1);
        d["Apparel_PsychicFoilHelmet"] = Item(1);
        d["Apparel_SimpleHelmet"] = Item(1) with { AcceptsStuff = Metallic };

        // Plants. Trees are PassThroughOnly; the rest Standable.
        d["Plant_Grass"] = Plant();
        d["Plant_Rose"] = Plant();
        d["Plant_Daylily"] = Plant();
        d["Plant_Potato"] = Plant();
        d["Plant_TreeOak"] = Plant() with { Rotatable = false };

        // Animals (PawnKindDefs).
        d["Boomalope"] = new DefInfo(1, 1, ThingLayer.Pawn, 0, 1);
        d["Pig"] = new DefInfo(1, 1, ThingLayer.Pawn, 0, 1);
        return d;
    }

    /// <summary>A walkable building (Standable or PassThroughOnly).</summary>
    /// <param name="sizeX">size.x.</param>
    /// <param name="sizeZ">size.z.</param>
    /// <returns>The entry, holding no items.</returns>
    private static DefInfo Edifice(int sizeX, int sizeZ) =>
        new(sizeX, sizeZ, ThingLayer.Building, 0, 1);

    /// <summary>An Impassable building.</summary>
    /// <param name="sizeX">size.x.</param>
    /// <param name="sizeZ">size.z.</param>
    /// <returns>The entry, holding no items.</returns>
    private static DefInfo Solid(int sizeX, int sizeZ) =>
        Edifice(sizeX, sizeZ) with { Passable = false };

    /// <summary>Minifiable, stuff-made furniture with a quality.</summary>
    /// <param name="sizeX">size.x.</param>
    /// <param name="sizeZ">size.z.</param>
    /// <param name="stuff">The stuff categories it accepts.</param>
    /// <returns>The entry.</returns>
    private static DefInfo Furniture(int sizeX, int sizeZ, string[] stuff) =>
        Edifice(sizeX, sizeZ) with
        {
            AcceptsStuff = stuff, HasQuality = true, Minifiable = true,
        };

    /// <summary>An item.</summary>
    /// <param name="stackLimit">Its stack limit.</param>
    /// <returns>The entry.</returns>
    private static DefInfo Item(int stackLimit) =>
        new(1, 1, ThingLayer.Item, 0, stackLimit);

    /// <summary>A plant.</summary>
    /// <returns>The entry.</returns>
    private static DefInfo Plant() => new(1, 1, ThingLayer.Plant, 0, 1);
}
