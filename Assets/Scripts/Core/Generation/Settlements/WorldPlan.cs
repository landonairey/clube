using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One shop of a planned settlement and why it is there (GL595, ST7).</summary>
    public sealed class PlannedShop
    {
        public EconomySpecialty Specialty;
        public string Reason;
        /// <summary>True when the pass that makes every specialty exist put it here, rather than the local resources alone.</summary>
        public bool Guaranteed;
    }

    /// <summary>A settlement of the world plan (GL595): plain data for the map to draw and later batches to build.</summary>
    public sealed class PlannedSettlement
    {
        /// <summary>Its place in <see cref="WorldPlan.Settlements"/>; roads and routes refer to it.</summary>
        public int Index;
        public SettlementType Type;
        /// <summary>Its centre (x, z), the middle of its survey cell.</summary>
        public Vector2 Position;
        public Vector2Int Cell;
        /// <summary>Mean ground height at its cell.</summary>
        public float Ground;
        /// <summary>Its name, ending in its type (GL597).</summary>
        public string Name;
        public int Population;
        public List<PlannedShop> Shops = new List<PlannedShop>();
        /// <summary>Why the site was picked, in short lines (ST7).</summary>
        public List<string> Reasons = new List<string>();
        /// <summary>The best road reaching it.</summary>
        public RoadClass BestRoad;
        /// <summary>The sea cell its vessels use, when it stands by the sea.</summary>
        public Vector2Int? Harbour;
        /// <summary>The river cell its boats use, when it stands by a river.</summary>
        public Vector2Int? Landing;

        public bool IsCoastal => Harbour.HasValue;
        public bool IsOnRiver => Landing.HasValue;

        public bool Has(string specialtyId)
        {
            return Shops.Exists(shop => shop.Specialty.Id == specialtyId);
        }
    }

    /// <summary>A river crossing on a road (GL596): where a bridge goes.</summary>
    public readonly struct RoadBridge
    {
        public RoadBridge(Vector2 position, float length)
        {
            Position = position;
            Length = length;
        }

        /// <summary>The middle of the crossing (x, z).</summary>
        public Vector2 Position { get; }
        /// <summary>Its length along the road, to the survey's cell size.</summary>
        public float Length { get; }
    }

    /// <summary>A road of the plan (GL596): from a settlement to another, or to a junction on an earlier road.</summary>
    public sealed class PlannedRoad
    {
        public int Index;
        public RoadClass Class;
        /// <summary>The settlement it was laid from.</summary>
        public int From;
        /// <summary>The settlement it ends in, or -1 when it ends at a junction.</summary>
        public int To = -1;
        /// <summary>The road it joins at its end, or -1.</summary>
        public int Joins = -1;
        /// <summary>The route (x, z), from <see cref="From"/> on, through survey cell centres.</summary>
        public List<Vector2> Points = new List<Vector2>();
        public float Length;
        public List<RoadBridge> Bridges = new List<RoadBridge>();
        /// <summary>The steepest step along it, rise over run between cells.</summary>
        public float SteepestSlope;
        /// <summary>Metres of it steeper than its class should be: where no gentler way exists.</summary>
        public float SteepLength;
    }

    /// <summary>The two kinds of water transit (GL596).</summary>
    public enum WaterRouteKind
    {
        /// <summary>Small boats along a river.</summary>
        River,
        /// <summary>Larger vessels by sea.</summary>
        Sea,
    }

    /// <summary>A water route between two settlements (GL596), or from one out to sea (<see cref="To"/> -1).</summary>
    public sealed class WaterRoute
    {
        public WaterRouteKind Kind;
        public int From;
        public int To = -1;
        public List<Vector2> Points = new List<Vector2>();
        public float Length;
        /// <summary>The name of its vessel, when it has a named one (GL597).</summary>
        public string Vessel;
        /// <summary>What the named vessel carries.</summary>
        public string Cargo;
    }

    /// <summary>A vessel with a name of its own and the route it belongs on (GL597): a row of <see cref="Defaults"/>.</summary>
    [Serializable]
    public sealed class NamedVessel
    {
        public string Name;
        public WaterRouteKind Kind;
        public string Cargo;
        /// <summary>Words that pick the ore its route must serve (any, in the ore's lower-case name).</summary>
        public string[] OreWords = Array.Empty<string>();
        /// <summary>Why it has no route on a map, filled in when it is kept in reserve.</summary>
        public string Note;

        public static List<NamedVessel> Defaults()
        {
            return new List<NamedVessel>
            {
                // "When we get to iron ore, name something Edmund Fitzgerald": the ore freighter out of an iron-mining settlement.
                new NamedVessel
                {
                    Name = "Edmund Fitzgerald", Kind = WaterRouteKind.Sea, Cargo = "iron ore",
                    OreWords = new[] { "iron", "hematite", "magnetite", "taconite" },
                },
            };
        }
    }

    /// <summary>The tables and numbers a world plan is made from; change a list to change the world (GL593–GL597).</summary>
    public sealed class WorldPlanRules
    {
        public List<SettlementType> Types = SettlementType.Defaults();
        public List<EconomySpecialty> Specialties = EconomySpecialty.Defaults();
        public List<RoadClassDefinition> Roads = RoadClassDefinition.Defaults();
        public List<NamedVessel> Vessels = NamedVessel.Defaults();

        /// <summary>The map size the distances below are written for; they scale with a smaller or larger map.</summary>
        public float ReferenceMapSize = 5000f;
        /// <summary>"Nearby", in metres: how far a small settlement reaches for its resources.</summary>
        public float NearbyRadius = 320f;
        /// <summary>A city's or the capital's area, in metres.</summary>
        public float AreaRadius = 900f;
        /// <summary>Side of a land parcel in metres, for the level ground a type needs.</summary>
        public float ParcelSide = 32f;
        /// <summary>Least flatness (0 to 1) of a site and of its footprint's mean.</summary>
        public float MinFlatness = 0.6f;
        /// <summary>Metres kept from the map's edge.</summary>
        public float EdgeMargin = 120f;
        /// <summary>How far from its centre a settlement's harbour may lie, in metres.</summary>
        public float CoastReach = 200f;
        /// <summary>How far from its centre a settlement's river landing may lie, in metres.</summary>
        public float RiverReach = 120f;
        /// <summary>Towns nearest the capital that get a paved road to it, beside the cities.</summary>
        public int PavedTownsFromCapital = 2;
        /// <summary>Thieves keep between these distances of the nearest settlement, in metres.</summary>
        public float ThievesNear = 350f, ThievesFar = 1100f;

        public RoadClassDefinition Road(RoadClass roadClass)
        {
            return Roads.Find(road => road.Class == roadClass) ?? Roads[0];
        }
    }

    /// <summary>
    /// The plan of a bounded world (GL593–GL597): its settlements with type, place, name, shops and reasons, the roads
    /// between them and the water routes. Plain data, made by <see cref="WorldPlanner.Build(ResourceSurvey, int, WorldPlanRules)"/>
    /// and the same for the same seed; the world map draws it (GL599) and later batches build from it (GL613 on).
    /// </summary>
    public sealed class WorldPlan
    {
        public int Seed;
        /// <summary>The map's bounds (x, z).</summary>
        public Rect Area;
        public WorldPlanRules Rules;
        public List<PlannedSettlement> Settlements = new List<PlannedSettlement>();
        public List<PlannedRoad> Roads = new List<PlannedRoad>();
        public List<WaterRoute> WaterRoutes = new List<WaterRoute>();
        /// <summary>Named vessels with no route on this map, each with a <see cref="NamedVessel.Note"/> saying why.</summary>
        public List<NamedVessel> ReservedVessels = new List<NamedVessel>();
        /// <summary>Notes on anything the plan could not do as asked (a type short of sites, a resource the map lacks).</summary>
        public List<string> Notes = new List<string>();
        /// <summary>Milliseconds each step took.</summary>
        public string Timings = "";

        /// <summary>The capital, or null on a plan whose rules have none.</summary>
        public PlannedSettlement Capital => Settlements.Find(settlement => settlement.Type.NeedsOcean);

        public int CountOf(string typeId)
        {
            int count = 0;
            foreach (PlannedSettlement settlement in Settlements)
            {
                count += settlement.Type.Id == typeId ? 1 : 0;
            }
            return count;
        }

        /// <summary>The settlements holding a specialty.</summary>
        public List<PlannedSettlement> With(string specialtyId)
        {
            return Settlements.FindAll(settlement => settlement.Has(specialtyId));
        }

        /// <summary>The roads that start or end at a settlement.</summary>
        public List<PlannedRoad> RoadsOf(int settlement)
        {
            return Roads.FindAll(road => road.From == settlement || road.To == settlement);
        }

        /// <summary>The settlement nearest a position (x, z) within a distance, or null.</summary>
        public PlannedSettlement Nearest(Vector2 position, float within = float.MaxValue)
        {
            PlannedSettlement best = null;
            float bestDistance = within * within;
            foreach (PlannedSettlement settlement in Settlements)
            {
                float distance = (settlement.Position - position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = settlement;
                }
            }
            return best;
        }
    }
}
