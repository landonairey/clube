using System;
using System.Collections.Generic;
using Unity.Profiling;
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
                    HalfWidth = definition.Width * 0.5f * (1f - definition.EdgeWander),
                });
            }
            return roads;
        }

        /// <summary>How many buildings a settlement wants: its shops, and houses for the rest of its people (and for a thieves' camp's band, GL617).</summary>
        public static int BuildingsWanted(PlannedSettlement settlement, SettlementSiteRules rules)
        {
            int housed = Mathf.Max(0, settlement.Population - settlement.Shops.Count) + settlement.Type.Band;
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
            // GL734: the quarry settlement's heights reach over its quarry pit and the wall round it too.
            float reach = half + rules.Margin + (settlement.IsQuarry ? QuarryYard.Reach(rules) : 0f);
            HeightGrid grid;
            using (GridMarker.Auto())
            {
                grid = new HeightGrid(new Rect(centre.x - reach, centre.y - reach, reach * 2f, reach * 2f), heightAt, heightsAt);
            }

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
            List<Slot> taken;
            using (TakeMarker.Auto())
            {
                taken = Take(site, rules, wanted, half, closedAt, 0);
                if (taken.Count < wanted)
                {
                    // Short of ground inside its parcels: a row further out each way, and a place further along each row.
                    taken = Take(site, rules, wanted, half, closedAt, 1);
                }
            }
            if (taken.Count < wanted)
            {
                site.Notes.Add($"{settlement.Name}: ground for {taken.Count} of its {wanted} buildings (the rest is water, road or too steep to terrace).");
            }
            using (BuildMarker.Auto())
            {
                Build(site, rules, taken, voxel);
            }
            if (settlement.IsQuarry)
            {
                QuarryYard.Plan(site, heightAt, closedAt, voxel);
            }
            using (WallMarker.Auto())
            {
                PlanWall(site);
                SettlementLights.Plan(site, rules.LightKinds);
            }
            return site;
        }

        private static readonly ProfilerMarker GridMarker = new ProfilerMarker("SettlementLayout.Grid");
        private static readonly ProfilerMarker TakeMarker = new ProfilerMarker("SettlementLayout.Take");
        private static readonly ProfilerMarker BuildMarker = new ProfilerMarker("SettlementLayout.Build");
        private static readonly ProfilerMarker WallMarker = new ProfilerMarker("SettlementLayout.Wall");

        // The places for buildings, nearest the centre first, each on ground that can be terraced with its neighbours.
        private static List<Slot> Take(SettlementSite site, SettlementSiteRules rules, int wanted, float half, Func<float, float, bool> closedAt, int extra)
        {
            float back = rules.FrontSetback + Largest(site).y + rules.BackYard;
            float halfWidth = Largest(site).x * 0.5f;
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
                    var slot = new Slot { Row = row, Column = column, Along = middle, Up = street + rules.FrontSetback + Largest(site).y * 0.5f };
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
                    Vector2 world = site.ToWorld(slot.Along + (i - 1) * halfWidth, street + rules.FrontSetback + j * Largest(site).y * 0.5f);
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
            float back = rules.FrontSetback + Largest(site).y + rules.BackYard;
            float halfWidth = Largest(site).x * 0.5f;
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

            // The buildings: shops nearest the centre, in the plan's order, then the settlement's landmarks (GL715: the
            // capital's castle, a city's guard tower), then houses.
            string[] landmarks = settlement.Type.Landmarks ?? Array.Empty<string>();
            for (int i = 0; i < taken.Count; i++)
            {
                Slot slot = taken[i];
                bool shop = i < settlement.Shops.Count;
                PlannedShop planned = shop ? settlement.Shops[i] : null;
                int landmark = i - settlement.Shops.Count;
                // GL710: its catalog row, by its shop's specialty or the settlement's type; GL711: its own roof pitch.
                BuildingDesign design = !shop && landmark < landmarks.Length ? BuildingDesign.Find(rules.Buildings, landmarks[landmark]) : null;
                design ??= rules.Buildings != null && rules.Buildings.Count > 0
                    ? BuildingDesign.Choose(rules.Buildings, settlement.Type, planned?.Specialty?.Id, shop, Hash01(settlement.Index, slot.Row, slot.Column + 7919))
                    : null;
                float depth = design != null ? design.Depth : TownLayout.ShopDepth;
                // GL712: fronts set back a little more or less, so a row doesn't stand ruled along its street.
                float stagger = shop ? 0f : rules.FrontStagger * Hash01(settlement.Index, slot.Column, slot.Row + 104729);
                Vector2 world = site.ToWorld(slot.Along, slot.Row * rules.RowDepth + rules.FrontSetback + stagger + depth * 0.5f);
                site.Buildings.Add(new SettlementBuilding
                {
                    Kind = shop ? SettlementBuildingKind.Shop : SettlementBuildingKind.House,
                    Shop = planned,
                    Centre = new Vector2(Mathf.Round(world.x / voxel) * voxel, Mathf.Round(world.y / voxel) * voxel),
                    Yaw = yaw,
                    Pad = site.Terraces[terraceOf[slot]].Height,
                    Terrace = terraceOf[slot],
                    Design = design,
                    Pitch = design != null
                        ? Mathf.Lerp(design.PitchMin, design.PitchMax, Hash01(settlement.Index, slot.Row + 31, slot.Column))
                        : 0.5f,
                });
            }
            if (rows.Count == 0)
            {
                return;
            }

            // The climbs between one row and the next, at alternate ends so the way up doubles back row by row (GL642).
            // GL712: each leaves its street a little in from the row's end and bends out and round to the next, a curve
            // rather than a right angle.
            var used = new List<int>(rows.Keys);
            var climbs = new List<SettlementLane>();
            var joins = new Dictionary<int, List<float>>();
            for (int i = 0; i + 1 < used.Count; i++)
            {
                int lower = used[i], upper = used[i + 1];
                float end = i % 2 == 0 ? Mathf.Max(rows[lower].y, rows[upper].y) + 1.5f : Mathf.Min(rows[lower].x, rows[upper].x) - 1.5f;
                float outward = i % 2 == 0 ? 1f : -1f;
                rows[lower] = new Vector2(Mathf.Min(rows[lower].x, end), Mathf.Max(rows[lower].y, end));
                rows[upper] = new Vector2(Mathf.Min(rows[upper].x, end), Mathf.Max(rows[upper].y, end));
                float lead = rules.ClimbLead;
                climbs.Add(Climb(site, rules, end, lower * rules.RowDepth, upper * rules.RowDepth, outward, lead));
                Join(joins, lower, end - outward * lead);
                Join(joins, upper, end - outward * lead);
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
                climbs.Add(Climb(site, rules, 0f, 0f, nearest * rules.RowDepth, 1f, 0f));
                Join(joins, nearest, 0f);
            }
            foreach (KeyValuePair<int, Vector2> row in rows)
            {
                Join(joins, row.Key, 0f);
                site.Lanes.Add(Street(site, rules, row.Key, row.Value, joins[row.Key]));
            }
            site.Lanes.AddRange(climbs);
        }

        private static void Join(Dictionary<int, List<float>> joins, int row, float along)
        {
            if (!joins.TryGetValue(row, out List<float> list))
            {
                joins[row] = list = new List<float>();
            }
            list.Add(along);
        }

        // GL712: a street along its row that wanders a little from side to side between the houses, straight through the
        // places where climbs and the planned roads join it, so they meet it on its line.
        private static SettlementLane Street(SettlementSite site, SettlementSiteRules rules, int row, Vector2 extent, List<float> joins)
        {
            var street = new SettlementLane { Kind = SettlementLaneKind.Street };
            float up = row * rules.RowDepth;
            float length = extent.y - extent.x;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 2f));
            float phase = Hash01(site.Settlement.Index, row, 3571) * Mathf.PI * 2f;
            var places = new List<float>();
            for (int i = 0; i <= steps; i++)
            {
                places.Add(Mathf.Lerp(extent.x, extent.y, i / (float)steps));
            }
            foreach (float join in joins)
            {
                if (join > extent.x + 0.05f && join < extent.y - 0.05f)
                {
                    places.Add(join);
                }
            }
            places.Sort();
            foreach (float along in places)
            {
                float calm = Mathf.Min(along - extent.x, extent.y - along);
                foreach (float join in joins)
                {
                    calm = Mathf.Min(calm, Mathf.Abs(along - join));
                }
                float wander = rules.StreetWander * Mathf.SmoothStep(0f, 1f, calm / 4f)
                    * Mathf.Sin(along * Mathf.PI * 2f / rules.StreetWave + phase);
                street.Points.Add(site.ToWorld(along, up + wander));
            }
            return street;
        }

        // A climb from one street to another near a place along the rows (GL642, GL712): from a point
        // <paramref name="lead"/> metres in from it on the lower street, out round a bend through it (further out where
        // the slope is steep, a switchback, as far as keeps it to the climbing slope) and back in to the upper street.
        private static SettlementLane Climb(SettlementSite site, SettlementSiteRules rules, float along, float fromUp, float toUp, float outward, float lead)
        {
            var lane = new SettlementLane { Kind = SettlementLaneKind.Climb };
            float start = along - outward * lead;
            Vector2 a = site.ToWorld(start, fromUp), b = site.ToWorld(start, toUp);
            float run = Mathf.Abs(toUp - fromUp);
            float rise = Mathf.Abs(site.HeightAt(b.x, b.y) - site.HeightAt(a.x, a.y));
            lane.Switchback = run > 0.01f && rise / run > rules.ClimbSlope;
            float swing = lead;
            if (lane.Switchback)
            {
                float length = rise / rules.ClimbSlope;
                swing = Mathf.Max(lead, Mathf.Sqrt(Mathf.Max(0f, length * length - run * run)) * 0.5f);
            }
            swing = Mathf.Min(rules.MaxSwing, swing);
            List<Vector2> points = Bend(site, start, fromUp, toUp, outward, swing);
            // Rounding the corner shortens the way: swing out further until it keeps to the slope again.
            for (int pass = 0; pass < 8 && lane.Switchback && swing < rules.MaxSwing && rise / Length(points) > rules.ClimbSlope; pass++)
            {
                swing = Mathf.Min(rules.MaxSwing, swing * 1.15f + 0.25f);
                points = Bend(site, start, fromUp, toUp, outward, swing);
            }
            lane.Points.AddRange(points);
            return lane;
        }

        // A smooth bend from (start, fromUp) to (start, toUp) through the point swing metres out between them.
        private static List<Vector2> Bend(SettlementSite site, float start, float fromUp, float toUp, float outward, float swing)
        {
            var points = new List<Vector2>();
            if (swing < 0.01f)
            {
                points.Add(site.ToWorld(start, fromUp));
                points.Add(site.ToWorld(start, toUp));
                return points;
            }
            // A quadratic curve whose middle passes through the apex: its control point is twice as far out.
            float controlAlong = start + outward * swing * 2f;
            float controlUp = (fromUp + toUp) * 0.5f;
            const int Segments = 10;
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments, s = 1f - t;
                float u = s * s * start + 2f * s * t * controlAlong + t * t * start;
                float v = s * s * fromUp + 2f * s * t * controlUp + t * t * toUp;
                points.Add(site.ToWorld(u, v));
            }
            return points;
        }

        private static float Length(List<Vector2> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector2.Distance(points[i - 1], points[i]);
            }
            return length;
        }


        // The widest and deepest building the catalog holds for this settlement: the ground every place keeps.
        private static Vector2 Largest(SettlementSite site)
        {
            SettlementSiteRules rules = site.Rules;
            return rules.Buildings != null ? BuildingDesign.LargestFootprint(rules.Buildings, site.Settlement.Type) : new Vector2(TownLayout.ShopWidth, TownLayout.ShopDepth);
        }

        /// <summary>
        /// GL715: the ring of a walled settlement's town wall (x, z), corner to corner and closed (the last point is the
        /// first): a rectangle <see cref="SettlementSiteRules.WallSetback"/> outside everything it changes. Empty for a
        /// settlement whose type has no walls or that has no buildings.
        /// </summary>
        public static void PlanWall(SettlementSite site)
        {
            site.Wall.Clear();
            // GL734: the quarry settlement is walled round with its quarry pit.
            if ((!site.Settlement.Type.Walls && site.Quarry == null) || site.Buildings.Count == 0)
            {
                return;
            }
            Rect bounds = site.Bounds;
            float voxel = 0.25f;
            float s = site.Rules.WallSetback;
            float x0 = Mathf.Floor((bounds.xMin - s) / voxel) * voxel, x1 = Mathf.Ceil((bounds.xMax + s) / voxel) * voxel;
            float z0 = Mathf.Floor((bounds.yMin - s) / voxel) * voxel, z1 = Mathf.Ceil((bounds.yMax + s) / voxel) * voxel;
            site.Wall.Add(new Vector2(x0, z0));
            site.Wall.Add(new Vector2(x1, z0));
            site.Wall.Add(new Vector2(x1, z1));
            site.Wall.Add(new Vector2(x0, z1));
            site.Wall.Add(new Vector2(x0, z0));
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
