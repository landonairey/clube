using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>What a building of a settlement is (GL613).</summary>
    public enum SettlementBuildingKind
    {
        /// <summary>A shop with a counter, trading its <see cref="SettlementBuilding.Shop"/>'s goods (GL614).</summary>
        Shop,
        /// <summary>A house of the people who live there.</summary>
        House,
    }

    /// <summary>One building of a settlement (GL613): where it stands, which way it faces, and the level ground under it.</summary>
    public sealed class SettlementBuilding
    {
        public SettlementBuildingKind Kind;
        /// <summary>The planned shop it houses; null for a house.</summary>
        public PlannedShop Shop;
        /// <summary>Its middle (x, z), on the voxel grid.</summary>
        public Vector2 Centre;
        /// <summary>Its turn about y in degrees, a quarter turn: its front (+z in its own frame) faces its street, downhill.</summary>
        public float Yaw;
        /// <summary>The height of its terrace: its floor stands <see cref="TownLayout.FloorHeight"/> over it.</summary>
        public float Pad;
        /// <summary>Its terrace, an index into <see cref="SettlementSite.Terraces"/>.</summary>
        public int Terrace;

        /// <summary>A point of the building's own frame (x across its front, z out of its front), in the world (x, z).</summary>
        public Vector2 ToWorld(Vector2 local)
        {
            return Centre + TownLayout.Turn(local, Yaw);
        }
    }

    /// <summary>
    /// A level of a settlement (GL642): a rectangle of ground cut and filled to one height, holding a run of buildings
    /// and the street in front of them. On a slope the rows of terraces step up the hillside.
    /// </summary>
    public sealed class SettlementTerrace
    {
        /// <summary>The level ground (x, z); past it the ground ramps back to the land over <see cref="SettlementSiteRules.Ramp"/>.</summary>
        public Rect Area;
        public float Height;
        /// <summary>The lowest and highest the land was under it: how far it is filled and cut.</summary>
        public float NaturalLow, NaturalHigh;
        /// <summary>Which row of the settlement it belongs to (0 through the centre, higher uphill).</summary>
        public int Row;

        /// <summary>The deepest it is cut into the hillside, in metres.</summary>
        public float Cut => Mathf.Max(0f, NaturalHigh - Height);

        /// <summary>The most it is built up over the land, in metres.</summary>
        public float Fill => Mathf.Max(0f, Height - NaturalLow);
    }

    /// <summary>What a lane of a settlement is for.</summary>
    public enum SettlementLaneKind
    {
        /// <summary>Runs along a row, in front of its buildings.</summary>
        Street,
        /// <summary>Climbs from one row's street to the next (GL642): straight on an easy slope, doubled back on a steep one.</summary>
        Climb,
    }

    /// <summary>A lane of a settlement (GL613, GL642): a line (x, z) written into the ground as a narrow graded path.</summary>
    public sealed class SettlementLane
    {
        public SettlementLaneKind Kind;
        public List<Vector2> Points = new List<Vector2>();
        /// <summary>True when it doubles back to keep its slope (a switchback).</summary>
        public bool Switchback;
    }

    /// <summary>A road of the world plan near a settlement: the settlement's ground and buildings keep off it.</summary>
    public sealed class SettlementRoad
    {
        public IReadOnlyList<Vector2> Points;
        /// <summary>Metres from its centre line that the road itself shapes (half its width and its shoulder).</summary>
        public float Reach;
    }

    /// <summary>The numbers a settlement is laid out with (GL613, GL642); change one to change every settlement.</summary>
    [Serializable]
    public sealed class SettlementSiteRules
    {
        /// <summary>Metres from one row's street to the next: street, apron, building, back yard and the bank between rows.</summary>
        public float RowDepth = 18f;
        /// <summary>Metres from one building's middle to the next along a row.</summary>
        public float Pitch = 9f;
        /// <summary>A street's width, and a climb's.</summary>
        public float StreetWidth = 2.5f;
        /// <summary>Metres from the street's middle to a building's front.</summary>
        public float FrontSetback = 4.5f;
        /// <summary>Metres of level yard behind a building.</summary>
        public float BackYard = 1f;
        /// <summary>Metres past a terrace over which its bank meets the land.</summary>
        public float Ramp = 3f;
        /// <summary>The most the land may rise across one building's ground, in metres: steeper is left as it is (a cliff).</summary>
        public float MaxRelief = 9f;
        /// <summary>The most the land's height may differ between the buildings of one terrace, in metres; more and they stand on separate levels.</summary>
        public float TerraceRelief = 2f;
        /// <summary>Metres past a planned road's own ground over which the settlement's ground meets the land again.</summary>
        public float RoadBlend = 3f;
        /// <summary>The steepest a climb between rows runs (rise over run); steeper and it doubles back.</summary>
        public float ClimbSlope = 0.3f;
        /// <summary>The most a climb swings out to double back, in metres.</summary>
        public float MaxSwing = 16f;
        /// <summary>People to a house: the houses hold whoever does not keep a shop.</summary>
        public int PeoplePerHouse = 2;
        /// <summary>Metres past the footprint the land's heights are kept for (banks and climbs reach out of it).</summary>
        public float Margin = 24f;
    }

    /// <summary>
    /// A settlement as it is built on its ground (GL613, GL642): its buildings, the terraces they stand on, the lanes
    /// between them, and the height of every point of its ground. Plain data from <see cref="SettlementLayout.Plan"/>;
    /// the writer (<see cref="SettlementWriter"/>), the keepers and the tests all read it, so they agree.
    /// </summary>
    public sealed class SettlementSite
    {
        private readonly Func<float, float, float> natural;
        private readonly List<(Vector2 A, Vector2 B, float Reach)> roadParts = new List<(Vector2, Vector2, float)>();

        internal SettlementSite(PlannedSettlement settlement, SettlementSiteRules rules, Rect footprint, Vector2 along, Vector2 up, float slope,
            Func<float, float, float> natural, IReadOnlyList<SettlementRoad> roads)
        {
            Settlement = settlement;
            Rules = rules;
            Footprint = footprint;
            Along = along;
            Up = up;
            Slope = slope;
            this.natural = natural;
            // Only the parts of roads near the settlement are asked about, for every column of its ground.
            float reach = Mathf.Max(footprint.width, footprint.height) * 0.5f + rules.Margin + 16f;
            if (roads != null)
            {
                foreach (SettlementRoad road in roads)
                {
                    for (int i = 1; i < road.Points.Count; i++)
                    {
                        Vector2 a = road.Points[i - 1], b = road.Points[i];
                        if (Distance(footprint.center, a, b) <= reach)
                        {
                            roadParts.Add((a, b, road.Reach));
                        }
                    }
                }
            }
        }

        public PlannedSettlement Settlement { get; }
        public SettlementSiteRules Rules { get; }
        /// <summary>The square of land parcels it is laid out in (x, z), centred on the settlement.</summary>
        public Rect Footprint { get; }
        /// <summary>The way its rows run (x, z), along the hillside: a unit vector along an axis.</summary>
        public Vector2 Along { get; }
        /// <summary>The way its rows step, uphill (x, z): a unit vector along the other axis.</summary>
        public Vector2 Up { get; }
        /// <summary>The land's mean slope over the footprint, rise over run.</summary>
        public float Slope { get; }
        public List<SettlementBuilding> Buildings { get; } = new List<SettlementBuilding>();
        public List<SettlementTerrace> Terraces { get; } = new List<SettlementTerrace>();
        public List<SettlementLane> Lanes { get; } = new List<SettlementLane>();
        /// <summary>What could not be done as planned (buildings with no ground to stand on).</summary>
        public List<string> Notes { get; } = new List<string>();

        /// <summary>How many different heights its terraces stand at: 1 on level ground, more up a hillside.</summary>
        public int Levels
        {
            get
            {
                var heights = new HashSet<float>();
                foreach (SettlementTerrace terrace in Terraces)
                {
                    heights.Add(terrace.Height);
                }
                return heights.Count;
            }
        }

        /// <summary>Everything the settlement changes (x, z): its terraces with their banks, and its lanes.</summary>
        public Rect Bounds
        {
            get
            {
                Vector2 min = Settlement.Position, max = min;
                foreach (SettlementTerrace terrace in Terraces)
                {
                    min = Vector2.Min(min, terrace.Area.min - Vector2.one * Rules.Ramp);
                    max = Vector2.Max(max, terrace.Area.max + Vector2.one * Rules.Ramp);
                }
                foreach (SettlementLane lane in Lanes)
                {
                    foreach (Vector2 point in lane.Points)
                    {
                        min = Vector2.Min(min, point - Vector2.one * Rules.StreetWidth);
                        max = Vector2.Max(max, point + Vector2.one * Rules.StreetWidth);
                    }
                }
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }

        /// <summary>A point of the settlement's frame (metres along its rows, metres uphill), in the world (x, z).</summary>
        public Vector2 ToWorld(float along, float up)
        {
            return Settlement.Position + Along * along + Up * up;
        }

        /// <summary>The land's height at (x, z) as generated, as the layout read it.</summary>
        public float NaturalAt(float x, float z)
        {
            return natural(x, z);
        }

        /// <summary>The height of the settlement's ground at (x, z): a terrace, its bank, or the land.</summary>
        public float HeightAt(float x, float z)
        {
            return HeightAt(x, z, natural(x, z));
        }

        /// <summary>
        /// The height the ground at (x, z) is set to, given the land's height there: level on a terrace, a smooth bank
        /// back to the land past its edge, and the land itself on and beside a planned road (the road shapes its own ground).
        /// </summary>
        public float HeightAt(float x, float z, float ground)
        {
            float best = 0f;
            float height = ground;
            var point = new Vector2(x, z);
            foreach (SettlementTerrace terrace in Terraces)
            {
                float beyond = Beyond(terrace.Area, point);
                if (beyond >= Rules.Ramp)
                {
                    continue;
                }
                float t = Mathf.Clamp01(beyond / Rules.Ramp);
                float hold = 1f - t * t * (3f - 2f * t);
                if (hold > best)
                {
                    best = hold;
                    height = terrace.Height;
                }
            }
            if (best <= 0f)
            {
                return ground;
            }
            float clear = Mathf.Clamp01(RoadBeyond(x, z) / Mathf.Max(0.01f, Rules.RoadBlend));
            return Mathf.Lerp(ground, height, best * clear * clear * (3f - 2f * clear));
        }

        /// <summary>
        /// Metres from (x, z) to the nearest ground a planned road shapes: 0 or less on a road or its shoulder (the
        /// settlement leaves that to the road), large where no road is near.
        /// </summary>
        public float RoadBeyond(float x, float z)
        {
            float nearest = float.MaxValue;
            var point = new Vector2(x, z);
            foreach ((Vector2 a, Vector2 b, float reach) in roadParts)
            {
                nearest = Mathf.Min(nearest, Distance(point, a, b) - reach);
            }
            return nearest;
        }

        /// <summary>Metres from a point to a rectangle (x, z); 0 inside it.</summary>
        public static float Beyond(Rect area, Vector2 point)
        {
            float dx = Mathf.Max(area.xMin - point.x, 0f, point.x - area.xMax);
            float dz = Mathf.Max(area.yMin - point.y, 0f, point.y - area.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        internal static float Distance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float length = ab.sqrMagnitude;
            float t = length > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / length) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
