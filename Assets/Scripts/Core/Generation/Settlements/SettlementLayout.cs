using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Lays a planned settlement out on its ground (GL613, GL642): rows of buildings along the hillside, each run of
    /// neighbours on a terrace cut and filled to one height, the rows stepping uphill; a street in front of each row,
    /// and climbs between the rows at alternate ends, doubled back where the hillside is steep. On level ground the
    /// same rules give one level. Shops take the places nearest the centre, houses the rest. Buildings keep off the
    /// planned roads, water and ground too steep to terrace; the same settlement on the same land is always laid out
    /// the same.
    /// </summary>
    public static class SettlementLayout
    {
        // Metres between the land's heights kept over a settlement.
        private const float GridSpacing = 2f;

        private sealed class Slot
        {
            public int Row, Column;
            public float Along, Up;
            public float Height, Low, High;
            public float Key;
            public bool Taken;
        }

        // The land's heights over the settlement, read once on a grid.
        private sealed class HeightGrid
        {
            private readonly float[] heights;
            private readonly Vector2 origin;
            private readonly int side;

            public HeightGrid(Rect area, Func<float, float, float> heightAt, Func<Vector2[], float[]> heightsAt)
            {
                origin = area.min;
                side = Mathf.CeilToInt(Mathf.Max(area.width, area.height) / GridSpacing) + 1;
                var points = new Vector2[side * side];
                for (int z = 0; z < side; z++)
                {
                    for (int x = 0; x < side; x++)
                    {
                        points[x + z * side] = origin + new Vector2(x, z) * GridSpacing;
                    }
                }
                if (heightsAt != null)
                {
                    heights = heightsAt(points);
                }
                else
                {
                    heights = new float[points.Length];
                    for (int i = 0; i < points.Length; i++)
                    {
                        heights[i] = heightAt(points[i].x, points[i].y);
                    }
                }
            }

            public float At(float x, float z)
            {
                float fx = Mathf.Clamp((x - origin.x) / GridSpacing, 0f, side - 1.001f);
                float fz = Mathf.Clamp((z - origin.y) / GridSpacing, 0f, side - 1.001f);
                int ix = (int)fx, iz = (int)fz;
                float tx = fx - ix, tz = fz - iz;
                return Mathf.Lerp(Mathf.Lerp(heights[ix + iz * side], heights[ix + 1 + iz * side], tx),
                    Mathf.Lerp(heights[ix + (iz + 1) * side], heights[ix + 1 + (iz + 1) * side], tx), tz);
            }

            // The land's slope over the grid (rise over run along x and z), by least squares.
            public Vector2 Gradient()
            {
                double sumH = 0.0, sumXH = 0.0, sumZH = 0.0, sumXX = 0.0;
                float middle = (side - 1) * 0.5f;
                for (int z = 0; z < side; z++)
                {
                    for (int x = 0; x < side; x++)
                    {
                        float h = heights[x + z * side];
                        sumH += h;
                        sumXH += (x - middle) * h;
                        sumZH += (z - middle) * h;
                        sumXX += (x - middle) * (x - middle);
                    }
                }
                double scale = sumXX * GridSpacing;
                return scale > 0.0 ? new Vector2((float)(sumXH / scale), (float)(sumZH / scale)) : Vector2.zero;
            }
        }

        /// <summary>The planned roads that come near a settlement, with the ground each shapes.</summary>
        public static List<SettlementRoad> RoadsNear(WorldPlan plan, PlannedSettlement settlement, float within)
        {
            var roads = new List<SettlementRoad>();
            foreach (PlannedRoad road in plan.Roads)
            {
                bool near = false;
                for (int i = 0; i < road.Points.Count && !near; i++)
                {
                    near = (road.Points[i] - settlement.Position).sqrMagnitude <= within * within;
                }
                if (!near)
                {
                    continue;
                }
                RoadClassDefinition definition = plan.Rules.Road(road.Class);
                bool graded = definition.GradeLength > 0f && definition.MaxCut > 0f;
                roads.Add(new SettlementRoad
                {
                    Points = road.Points,
                    Reach = definition.Width * 0.5f * (1f + definition.EdgeWander) + (graded ? definition.Shoulder : 0f),
                });
            }
            return roads;
        }

        /// <summary>How many buildings a settlement wants: its shops, and houses for the rest of its people.</summary>
        public static int BuildingsWanted(PlannedSettlement settlement, SettlementSiteRules rules)
        {
            int housed = Mathf.Max(0, settlement.Population - settlement.Shops.Count);
            return settlement.Shops.Count + Mathf.CeilToInt(housed / (float)Mathf.Max(1, rules.PeoplePerHouse));
        }

        /// <summary>The side of the square a settlement is laid out in, in metres: its type's land parcels.</summary>
        public static float SideOf(SettlementType type, float parcelSide)
        {
            return Mathf.Sqrt(Mathf.Max(0.25f, type.Parcels)) * parcelSide;
        }

        /// <summary>Lays a settlement out on its ground.</summary>
        /// <param name="parcelSide">The side of a land parcel in metres (<see cref="WorldPlanRules.ParcelSide"/>).</param>
        /// <param name="heightAt">The land's height at (x, z), as generated.</param>
        /// <param name="heightsAt">The land's heights at many points at once, where that is faster; may be null.</param>
        /// <param name="closedAt">True where nothing may be built (water, ground the player is held back from); may be null.</param>
        /// <param name="roads">The planned roads near it (<see cref="RoadsNear"/>); may be null.</param>
        /// <param name="voxel">The world's voxel size: buildings and terraces sit on its grid.</param>
        public static SettlementSite Plan(PlannedSettlement settlement, float parcelSide, SettlementSiteRules rules, Func<float, float, float> heightAt,
            Func<Vector2[], float[]> heightsAt = null, Func<float, float, bool> closedAt = null, IReadOnlyList<SettlementRoad> roads = null, float voxel = 0.25f)
        {
            rules ??= new SettlementSiteRules();
            voxel = Mathf.Max(0.01f, voxel);
            float side = SideOf(settlement.Type, parcelSide);
            float half = side * 0.5f;
            Vector2 centre = settlement.Position;
            var footprint = new Rect(centre.x - half, centre.y - half, side, side);
            float reach = half + rules.Margin;
            var grid = new HeightGrid(new Rect(centre.x - reach, centre.y - reach, reach * 2f, reach * 2f), heightAt, heightsAt);

            // Rows run along the hillside: across whichever axis the land climbs most along.
            Vector2 gradient = grid.Gradient();
            Vector2 up = new Vector2(0f, 1f);
            if (gradient.magnitude >= 0.02f)
            {
                up = Mathf.Abs(gradient.x) >= Mathf.Abs(gradient.y) ? new Vector2(Mathf.Sign(gradient.x), 0f) : new Vector2(0f, Mathf.Sign(gradient.y));
            }
            var along = new Vector2(up.y, up.x);
            var site = new SettlementSite(settlement, rules, footprint, along, up, gradient.magnitude, grid.At, roads);

            int wanted = BuildingsWanted(settlement, rules);
            List<Slot> taken = Take(site, rules, wanted, half, closedAt, 0);
            if (taken.Count < wanted)
            {
                // Short of ground inside its parcels: a row further out each way, and a place further along each row.
                taken = Take(site, rules, wanted, half, closedAt, 1);
            }
            if (taken.Count < wanted)
            {
                site.Notes.Add($"{settlement.Name}: ground for {taken.Count} of its {wanted} buildings (the rest is water, road or too steep to terrace).");
            }
            Build(site, rules, taken, voxel);
            return site;
        }

        // The places for buildings, nearest the centre first, each on ground that can be terraced with its neighbours.
        private static List<Slot> Take(SettlementSite site, SettlementSiteRules rules, int wanted, float half, Func<float, float, bool> closedAt, int extra)
        {
            float back = rules.FrontSetback + TownLayout.ShopDepth + rules.BackYard;
            float halfWidth = TownLayout.ShopWidth * 0.5f;
            var slots = new Dictionary<(int, int), Slot>();
            var order = new List<Slot>();
            int rows = Mathf.CeilToInt(half / rules.RowDepth) + 1 + extra;
            int columns = Mathf.CeilToInt(half / rules.Pitch) + 1 + extra;
            float room = half + extra * rules.RowDepth;
            for (int row = -rows; row <= rows; row++)
            {
                float street = row * rules.RowDepth;
                if (street - rules.StreetWidth * 0.5f < -room || street + back > room)
                {
                    continue;
                }
                for (int column = -columns; column < columns; column++)
                {
                    float middle = (column + 0.5f) * rules.Pitch;
                    if (Mathf.Abs(middle) + halfWidth + 0.5f > half + extra * rules.Pitch)
                    {
                        continue;
                    }
                    var slot = new Slot { Row = row, Column = column, Along = middle, Up = street + rules.FrontSetback + TownLayout.ShopDepth * 0.5f };
                    if (!Measure(site, rules, slot, street, back, halfWidth, closedAt))
                    {
                        continue;
                    }
                    // Nearest the centre first; the hash only orders places equally far.
                    slot.Key = new Vector2(slot.Along, slot.Up).magnitude + 0.01f * Hash01(site.Settlement.Index, row, column);
                    slots[(row, column)] = slot;
                    order.Add(slot);
                }
            }
            order.Sort((a, b) => a.Key.CompareTo(b.Key));
            var taken = new List<Slot>();
            foreach (Slot slot in order)
            {
                if (taken.Count >= wanted)
                {
                    break;
                }
                // With its neighbours along the row it makes one terrace: the land under them must be near one height.
                float low = slot.Height, high = slot.Height;
                for (int step = -1; step <= 1; step += 2)
                {
                    for (int column = slot.Column + step; slots.TryGetValue((slot.Row, column), out Slot neighbour) && neighbour.Taken; column += step)
                    {
                        low = Mathf.Min(low, neighbour.Height);
                        high = Mathf.Max(high, neighbour.Height);
                    }
                }
                if (high - low > rules.TerraceRelief)
                {
                    continue;
                }
                slot.Taken = true;
                taken.Add(slot);
            }
            return taken;
        }

        // Reads the land under a place: false when it is closed, on a road, or too steep.
        private static bool Measure(SettlementSite site, SettlementSiteRules rules, Slot slot, float street, float back, float halfWidth, Func<float, float, bool> closedAt)
        {
            var heights = new List<float>(20);
            slot.Low = float.MaxValue;
            slot.High = float.MinValue;
            for (int j = 0; j <= 4; j++)
            {
                float v = Mathf.Lerp(street - rules.StreetWidth * 0.5f, street + back, j / 4f);
                for (int i = 0; i <= 3; i++)
                {
                    float u = slot.Along + Mathf.Lerp(-halfWidth, halfWidth, i / 3f);
                    Vector2 world = site.ToWorld(u, v);
                    if (closedAt != null && closedAt(world.x, world.y))
                    {
                        return false;
                    }
                    float height = site.NaturalAt(world.x, world.y);
                    heights.Add(height);
                    slot.Low = Mathf.Min(slot.Low, height);
                    slot.High = Mathf.Max(slot.High, height);
                }
            }
            if (slot.High - slot.Low > rules.MaxRelief)
            {
                return false;
            }
            // The building itself stands wholly clear of a road's ground and of where its own ground meets the road's.
            for (int j = 0; j <= 2; j++)
            {
                for (int i = 0; i <= 2; i++)
                {
                    Vector2 world = site.ToWorld(slot.Along + (i - 1) * halfWidth, street + rules.FrontSetback + j * TownLayout.ShopDepth * 0.5f);
                    if (site.RoadBeyond(world.x, world.y) < rules.RoadBlend)
                    {
                        return false;
                    }
                }
            }
            heights.Sort();
            slot.Height = heights[heights.Count / 2];
            return true;
        }

        private static void Build(SettlementSite site, SettlementSiteRules rules, List<Slot> taken, float voxel)
        {
            float back = rules.FrontSetback + TownLayout.ShopDepth + rules.BackYard;
            float halfWidth = TownLayout.ShopWidth * 0.5f;
            Vector2 front = -site.Up;
            float yaw = Mathf.Round(Mathf.Atan2(front.x, front.y) * Mathf.Rad2Deg / 90f) * 90f;
            PlannedSettlement settlement = site.Settlement;

            // The terraces: each run of neighbours along a row.
            var byPlace = new Dictionary<(int, int), Slot>();
            foreach (Slot slot in taken)
            {
                byPlace[(slot.Row, slot.Column)] = slot;
            }
            var terraceOf = new Dictionary<Slot, int>();
            var rows = new SortedDictionary<int, Vector2>();
            foreach (Slot slot in taken)
            {
                if (byPlace.ContainsKey((slot.Row, slot.Column - 1)))
                {
                    continue;
                }
                var run = new List<Slot>();
                for (int column = slot.Column; byPlace.TryGetValue((slot.Row, column), out Slot next); column++)
                {
                    run.Add(next);
                }
                float sum = 0f, low = float.MaxValue, high = float.MinValue;
                foreach (Slot member in run)
                {
                    sum += member.Height;
                    low = Mathf.Min(low, member.Low);
                    high = Mathf.Max(high, member.High);
                    terraceOf[member] = site.Terraces.Count;
                }
                float street = slot.Row * rules.RowDepth;
                float from = run[0].Along - halfWidth - 1f, to = run[run.Count - 1].Along + halfWidth + 1f;
                Vector2 a = site.ToWorld(from, street - rules.StreetWidth * 0.5f), b = site.ToWorld(to, street + back);
                site.Terraces.Add(new SettlementTerrace
                {
                    Area = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)),
                    Height = Mathf.Round(sum / run.Count / voxel) * voxel,
                    NaturalLow = low,
                    NaturalHigh = high,
                    Row = slot.Row,
                });
                rows[slot.Row] = rows.TryGetValue(slot.Row, out Vector2 extent)
                    ? new Vector2(Mathf.Min(extent.x, from), Mathf.Max(extent.y, to))
                    : new Vector2(from, to);
            }

            // The buildings: shops nearest the centre, in the plan's order, then houses.
            for (int i = 0; i < taken.Count; i++)
            {
                Slot slot = taken[i];
                Vector2 world = site.ToWorld(slot.Along, slot.Up);
                bool shop = i < settlement.Shops.Count;
                site.Buildings.Add(new SettlementBuilding
                {
                    Kind = shop ? SettlementBuildingKind.Shop : SettlementBuildingKind.House,
                    Shop = shop ? settlement.Shops[i] : null,
                    Centre = new Vector2(Mathf.Round(world.x / voxel) * voxel, Mathf.Round(world.y / voxel) * voxel),
                    Yaw = yaw,
                    Pad = site.Terraces[terraceOf[slot]].Height,
                    Terrace = terraceOf[slot],
                });
            }
            if (rows.Count == 0)
            {
                return;
            }

            // The climbs between one row and the next, at alternate ends so the way up doubles back row by row (GL642).
            var used = new List<int>(rows.Keys);
            var climbs = new List<SettlementLane>();
            for (int i = 0; i + 1 < used.Count; i++)
            {
                int lower = used[i], upper = used[i + 1];
                float end = i % 2 == 0 ? Mathf.Max(rows[lower].y, rows[upper].y) + 1.5f : Mathf.Min(rows[lower].x, rows[upper].x) - 1.5f;
                float outward = i % 2 == 0 ? 1f : -1f;
                rows[lower] = new Vector2(Mathf.Min(rows[lower].x, end), Mathf.Max(rows[lower].y, end));
                rows[upper] = new Vector2(Mathf.Min(rows[upper].x, end), Mathf.Max(rows[upper].y, end));
                climbs.Add(Climb(site, rules, end, lower * rules.RowDepth, upper * rules.RowDepth, outward));
            }
            // The planned roads meet at the centre, on row 0's street line: the streets are joined to it.
            if (rows.TryGetValue(0, out Vector2 through))
            {
                rows[0] = new Vector2(Mathf.Min(through.x, 0f), Mathf.Max(through.y, 0f));
            }
            else
            {
                int nearest = used[0];
                foreach (int row in used)
                {
                    nearest = Mathf.Abs(row) < Mathf.Abs(nearest) ? row : nearest;
                }
                rows[nearest] = new Vector2(Mathf.Min(rows[nearest].x, 0f), Mathf.Max(rows[nearest].y, 0f));
                climbs.Add(Climb(site, rules, 0f, 0f, nearest * rules.RowDepth, 1f));
            }
            foreach (KeyValuePair<int, Vector2> row in rows)
            {
                var street = new SettlementLane { Kind = SettlementLaneKind.Street };
                street.Points.Add(site.ToWorld(row.Value.x, row.Key * rules.RowDepth));
                street.Points.Add(site.ToWorld(row.Value.y, row.Key * rules.RowDepth));
                site.Lanes.Add(street);
            }
            site.Lanes.AddRange(climbs);
        }

        // A climb from one street to another at a place along the rows: straight where that is gentle enough, else out
        // to a corner and back (a switchback), as far as keeps it to the climbing slope.
        private static SettlementLane Climb(SettlementSite site, SettlementSiteRules rules, float along, float fromUp, float toUp, float outward)
        {
            var lane = new SettlementLane { Kind = SettlementLaneKind.Climb };
            Vector2 a = site.ToWorld(along, fromUp), b = site.ToWorld(along, toUp);
            float run = Mathf.Abs(toUp - fromUp);
            float rise = Mathf.Abs(site.HeightAt(b.x, b.y) - site.HeightAt(a.x, a.y));
            lane.Points.Add(a);
            if (run > 0.01f && rise / run > rules.ClimbSlope)
            {
                float length = rise / rules.ClimbSlope;
                float swing = Mathf.Min(rules.MaxSwing, Mathf.Sqrt(Mathf.Max(0f, length * length - run * run)) * 0.5f);
                lane.Points.Add(site.ToWorld(along + outward * swing, (fromUp + toUp) * 0.5f));
                lane.Switchback = true;
            }
            lane.Points.Add(b);
            return lane;
        }

        private static float Hash01(int a, int b, int c)
        {
            uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
