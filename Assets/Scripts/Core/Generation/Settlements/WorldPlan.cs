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
        /// <summary>The piece of land it stands on (GL643): settlements with the same number can be walked between.</summary>
        public int Land;
        /// <summary>
        /// The road network it is on (GL643): 0 the capital's, a higher number a network of its own land (an island's, with no
        /// road to the rest: it is reached by water), -1 for none (camps).
        /// </summary>
        public int RoadNetwork = -1;
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
        /// <summary>
        /// The route (x, z), from <see cref="From"/> on: through survey cell centres as planned, then the smoothed line on
        /// the real ground once <see cref="RoadRefiner"/> has been over it (<see cref="Refined"/>).
        /// </summary>
        public List<Vector2> Points = new List<Vector2>();
        /// <summary>True once the route follows the real ground (GL631).</summary>
        public bool Refined;
        public float Length;
        public List<RoadBridge> Bridges = new List<RoadBridge>();
        /// <summary>Its tunnels (GL642), in the road's own direction: each a straight part of <see cref="Points"/>, bored under a ridge.</summary>
        public List<RoadTunnel> Tunnels = new List<RoadTunnel>();
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

        /// <summary>The slope angle the player can walk, in degrees, where no player says otherwise (the controller's limit in every scene).</summary>
        public const float DefaultWalkableSlope = 50f;

        /// <summary>The slope angle the player can walk, in degrees: the road classes' limits are shares of it (GL634).</summary>
        public float WalkableSlope { get; private set; } = DefaultWalkableSlope;

        /// <summary>Sets the walkable slope angle (the player controller's own limit) and every road class's limit from it.</summary>
        public void SetWalkableSlope(float degrees)
        {
            WalkableSlope = degrees;
            foreach (RoadClassDefinition road in Roads)
            {
                road.SetWalkable(degrees);
            }
        }

        /// <summary>The map size the distances below are written for; they scale with a smaller or larger map.</summary>
        public float ReferenceMapSize = 5000f;
        /// <summary>"Nearby", in metres: how far a small settlement reaches for its resources.</summary>
        public float NearbyRadius = 320f;
        /// <summary>A city's or the capital's area, in metres.</summary>
        public float AreaRadius = 900f;
        /// <summary>Side of a land parcel in metres, for the level ground a type needs.</summary>
        public float ParcelSide = 32f;
        /// <summary>The flatness (0 to 1) of ground that counts as level: a settlement there needs no terraces.</summary>
        public float MinFlatness = 0.6f;
        /// <summary>GL642: least flatness of a site and of its footprint's mean. Below <see cref="MinFlatness"/> the settlement is cut into the slope in terraces (0.2 is a mean slope of 32%).</summary>
        public float TerraceFlatness = 0.2f;
        /// <summary>GL642: least flatness of a site that must be reached by a dirt cart path (0.45 is a mean slope of 22%: about the path's own limit).</summary>
        public float CartFlatness = 0.45f;

        /// <summary>
        /// The least flatness of a settlement type's site (GL642): terraces fit a settlement to a hillside, but its road has
        /// to reach it. One reached on foot may stand on any ground that can be terraced, one that needs a dirt cart path on
        /// a gentler hillside, one that needs a paved road on level ground.
        /// </summary>
        public float FlatnessFor(SettlementType type)
        {
            return type.MinimumRoad == RoadClass.PavedCartPath ? MinFlatness : type.MinimumRoad == RoadClass.DirtCartPath ? CartFlatness : TerraceFlatness;
        }
        /// <summary>GL643: the fewest survey cells a piece of land other than the largest needs to get a settlement of its own.</summary>
        public int IslandCells = 6;
        /// <summary>GL643: land parcels an island must have for each parcel of the settlement type it is given (a village of 4 needs 160: about 400 m square).</summary>
        public float IslandLandPerParcel = 40f;
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
        /// <summary>Signposts at junctions and settlement edges (GL632); empty until the roads are on the real ground (<see cref="WorldRoads.Finish"/>).</summary>
        public List<PlannedSignpost> Signposts = new List<PlannedSignpost>();
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

        /// <summary>True when (x, z) lies on a road of the plan, or within a margin of its edge (a fence leaves a gap there).</summary>
        public bool IsOnRoad(Vector2 point, float margin = 0f)
        {
            foreach (PlannedRoad road in Roads)
            {
                float reach = Rules.Road(road.Class).Width * 0.5f + margin;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 a = road.Points[i - 1], b = road.Points[i];
                    if (Mathf.Min(a.x, b.x) - reach > point.x || Mathf.Max(a.x, b.x) + reach < point.x
                        || Mathf.Min(a.y, b.y) - reach > point.y || Mathf.Max(a.y, b.y) + reach < point.y)
                    {
                        continue;
                    }
                    if (SettlementSite.Distance(point, a, b) <= reach)
                    {
                        return true;
                    }
                }
            }
            return false;
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
