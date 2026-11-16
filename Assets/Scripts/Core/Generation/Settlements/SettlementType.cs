using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>The land paths of the bounded world (GL596), from least to most prepared; a higher one also serves as a lower.</summary>
    public enum RoadClass
    {
        /// <summary>No path at all (camps).</summary>
        None,
        /// <summary>Small, to walk or go by horse.</summary>
        Footpath,
        /// <summary>Wider and flatter, for a cart.</summary>
        DirtCartPath,
        /// <summary>Prepared, for two-way cart traffic.</summary>
        PavedCartPath,
    }

    /// <summary>The numbers of one road class (GL596): how steep it may run, how wide it is cut, what a bridge costs it.</summary>
    [Serializable]
    public sealed class RoadClassDefinition
    {
        public RoadClass Class;
        public string Name;
        /// <summary>Steepest rise over run the class should take; steeper steps are a last resort and are noted on the road.</summary>
        public float MaxSlope;
        /// <summary>Width in metres, for cutting it into the terrain (GL613).</summary>
        public float Width;
        /// <summary>What crossing one cell of river costs the route, in metres of level road: how far it goes round to save a bridge.</summary>
        public float BridgeCost;

        /// <summary>The three classes: a footpath, a dirt cart path, a paved cart path.</summary>
        public static List<RoadClassDefinition> Defaults()
        {
            return new List<RoadClassDefinition>
            {
                new RoadClassDefinition { Class = RoadClass.Footpath, Name = "Footpath", MaxSlope = 0.35f, Width = 1.5f, BridgeCost = 200f },
                new RoadClassDefinition { Class = RoadClass.DirtCartPath, Name = "Dirt cart path", MaxSlope = 0.2f, Width = 3f, BridgeCost = 400f },
                new RoadClassDefinition { Class = RoadClass.PavedCartPath, Name = "Paved cart path", MaxSlope = 0.12f, Width = 5f, BridgeCost = 600f },
            };
        }
    }

    /// <summary>What a settlement type looks for in a site (GL595).</summary>
    public enum SiteRule
    {
        /// <summary>One resource close by: each new one of the type goes for the resource served least so far.</summary>
        OneResource,
        /// <summary>A few resources close by, and fresh water.</summary>
        SeveralResources,
        /// <summary>As many resource types as its wider area can hold.</summary>
        AllResources,
        /// <summary>Out of sight in rough or wooded ground, within reach of settlements to rob.</summary>
        Hidden,
    }

    /// <summary>
    /// One kind of settlement (GL593): a row of <see cref="Defaults"/>. Adding a type is adding a row; placing, roads, names
    /// and the map read these fields and never the type's identity.
    /// </summary>
    [Serializable]
    public sealed class SettlementType
    {
        /// <summary>Stable id for saves and lookups ("village").</summary>
        public string Id;
        /// <summary>Name as shown, and the ending of its settlements' names (GL597).</summary>
        public string Name;
        /// <summary>Id of its world-map icon (GL599): tent, house, houses, town-hall, tower, castle.</summary>
        public string Icon;
        /// <summary>The least road it must be reached by; <see cref="RoadClass.None"/> for no paths.</summary>
        public RoadClass MinimumRoad;
        /// <summary>True when it must stand on an ocean's edge.</summary>
        public bool NeedsOcean;
        /// <summary>Parcel size as the design table writes it ("&lt;1", "~4", "&gt;16").</summary>
        public string ParcelSize;
        /// <summary>Land parcels it is built on, as a number (a camp takes part of one).</summary>
        public float Parcels;
        public int PopulationMin;
        public int PopulationMax;
        /// <summary>How many of the type the world holds.</summary>
        public int Count;
        /// <summary>Shops it holds (GL595: bigger types hold more).</summary>
        public int Shops;
        /// <summary>Highest <see cref="EconomySpecialty.CraftLevel"/> it can house: 0 extraction, 1 processing, 2 fine crafts.</summary>
        public int CraftLevel;
        /// <summary>Metres it keeps from others on a 5 km map (the mean of the two types' spacings; scaled with the map).</summary>
        public float Spacing;
        public SiteRule Site;
        /// <summary>True for thieves: no shops, and no name on the player's map until found.</summary>
        public bool Hostile;

        /// <summary>The seven types and their numbers, exactly as GL593's table has them.</summary>
        public static List<SettlementType> Defaults()
        {
            return new List<SettlementType>
            {
                new SettlementType
                {
                    Id = "camp", Name = "Camp", Icon = "tent", MinimumRoad = RoadClass.None, ParcelSize = "<1", Parcels = 0.5f,
                    PopulationMin = 1, PopulationMax = 1, Count = 12, Shops = 1, CraftLevel = 0, Spacing = 220f, Site = SiteRule.OneResource,
                },
                new SettlementType
                {
                    Id = "thieves-camp", Name = "Thieves' Camp", Icon = "tent", MinimumRoad = RoadClass.None, ParcelSize = "<1", Parcels = 0.5f,
                    PopulationMin = 0, PopulationMax = 0, Count = 4, Shops = 0, CraftLevel = 0, Spacing = 350f, Site = SiteRule.Hidden, Hostile = true,
                },
                new SettlementType
                {
                    Id = "outpost", Name = "Outpost", Icon = "house", MinimumRoad = RoadClass.Footpath, ParcelSize = "~1", Parcels = 1f,
                    PopulationMin = 2, PopulationMax = 4, Count = 12, Shops = 1, CraftLevel = 0, Spacing = 300f, Site = SiteRule.OneResource,
                },
                new SettlementType
                {
                    Id = "village", Name = "Village", Icon = "houses", MinimumRoad = RoadClass.DirtCartPath, ParcelSize = "~4", Parcels = 4f,
                    PopulationMin = 4, PopulationMax = 8, Count = 10, Shops = 2, CraftLevel = 1, Spacing = 450f, Site = SiteRule.SeveralResources,
                },
                new SettlementType
                {
                    Id = "town", Name = "Town", Icon = "town-hall", MinimumRoad = RoadClass.DirtCartPath, ParcelSize = "~9", Parcels = 9f,
                    PopulationMin = 8, PopulationMax = 16, Count = 8, Shops = 3, CraftLevel = 2, Spacing = 600f, Site = SiteRule.SeveralResources,
                },
                new SettlementType
                {
                    Id = "city", Name = "City", Icon = "tower", MinimumRoad = RoadClass.PavedCartPath, ParcelSize = "~16", Parcels = 16f,
                    PopulationMin = 16, PopulationMax = 24, Count = 4, Shops = 5, CraftLevel = 2, Spacing = 800f, Site = SiteRule.AllResources,
                },
                new SettlementType
                {
                    Id = "capital", Name = "Capital", Icon = "castle", MinimumRoad = RoadClass.PavedCartPath, NeedsOcean = true, ParcelSize = ">16",
                    Parcels = 25f, PopulationMin = 24, PopulationMax = 32, Count = 1, Shops = 8, CraftLevel = 2, Spacing = 900f,
                    Site = SiteRule.AllResources,
                },
            };
        }
    }
}
