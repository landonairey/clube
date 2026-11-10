using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Rocks and downed trunks in rivers (GL504), stamped into the terrain like trees and boulders. In a cell of
    /// <see cref="CellSize"/> metres, now and then a log lies across a river from bank to bank (a little over the water, resting on
    /// both banks) and a few rocks stand in the stream with their tops above the surface. Placed from the seed, the cell and the
    /// water map, so every chunk that reaches one builds the same. Main thread only (cached).
    /// </summary>
    public sealed class RiverDebris : IStampField
    {
        /// <summary>Width of the cell that holds at most one log and a few rocks, in metres.</summary>
        public const float CellSize = 32f;

        /// <summary>Chance a cell with a river in it gets a log.</summary>
        public const float LogChance = 0.3f;

        /// <summary>Widest river a log is laid across, in metres.</summary>
        public const float MaxSpan = 9f;

        private const int Salt = 0x52495645;
        private const int MaxCachedCells = 16384;
        private const int Attempts = 10;

        private readonly int seed;
        private readonly HeightfieldGenerator ground;
        private readonly byte wood;
        private readonly byte rock;
        private readonly Rect keepClear;
        private readonly Dictionary<Vector2Int, TreePart[]> cells = new Dictionary<Vector2Int, TreePart[]>();
        private readonly List<TreePart> building = new List<TreePart>();

        public RiverDebris(int seed, HeightfieldGenerator ground, byte wood, byte rock, Rect keepClear = default)
        {
            this.seed = seed;
            this.ground = ground;
            this.wood = wood;
            this.rock = rock;
            this.keepClear = keepClear;
        }

        /// <summary>The debris for a terrain, or null when it has no rivers, rock or wood.</summary>
        public static RiverDebris Create(TerrainSettings terrain, ITerrainGenerator generator)
        {
            if (!(generator is HeightfieldGenerator heightfield) || terrain.Layers == null || terrain.Layers.Rock == null
                || terrain.Trees == null || terrain.Trees.Wood == null || terrain.Water == null || !terrain.Water.Enabled)
            {
                return null;
            }
            Rect clear = terrain.StartArea != null && terrain.StartArea.Enabled ? terrain.StartArea.Area : default;
            return new RiverDebris(terrain.Seed, heightfield, terrain.Trees.Wood.Id, terrain.Layers.Rock.Id, clear);
        }

        public void CollectParts(Bounds box, List<TreePart> results, float margin)
        {
            int fromX = Mathf.FloorToInt((box.min.x - margin) / CellSize) - 1;
            int fromZ = Mathf.FloorToInt((box.min.z - margin) / CellSize) - 1;
            int toX = Mathf.FloorToInt((box.max.x + margin) / CellSize) + 1;
            int toZ = Mathf.FloorToInt((box.max.z + margin) / CellSize) + 1;
            for (int z = fromZ; z <= toZ; z++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    foreach (TreePart part in PartsIn(new Vector2Int(x, z)))
                    {
                        if (part.Bounds(margin).Intersects(box))
                        {
                            results.Add(part);
                        }
                    }
                }
            }
        }

        private TreePart[] PartsIn(Vector2Int cell)
        {
            if (cells.TryGetValue(cell, out TreePart[] parts))
            {
                return parts;
            }
            if (cells.Count >= MaxCachedCells)
            {
                cells.Clear();
            }
            parts = Build(cell);
            cells[cell] = parts;
            return parts;
        }

        private float Roll(Vector2Int cell, int what)
        {
            return VoxelHash.Uniform(seed, cell.x, what, cell.y, Salt);
        }

        private TreePart[] Build(Vector2Int cell)
        {
            WaterMap water = ground.Water;
            if (!water.IsCreated)
            {
                return System.Array.Empty<TreePart>();
            }
            building.Clear();
            bool wantLog = Roll(cell, 0) < LogChance;
            int rocks = (int)(Roll(cell, 1) * 3.99f);
            for (int attempt = 0; attempt < Attempts && (wantLog || rocks > 0); attempt++)
            {
                var spot = new float2(
                    (cell.x + Roll(cell, 10 + attempt * 2)) * CellSize,
                    (cell.y + Roll(cell, 11 + attempt * 2)) * CellSize);
                if (keepClear.width > 0f && keepClear.Contains(new Vector2(spot.x, spot.y)) || !IsRiver(water, spot, out int index))
                {
                    continue;
                }
                float2 flow = water.Flow[index];
                float2 along = math.lengthsq(flow) > 1e-6f ? math.normalize(flow) : new float2(1f, 0f);
                float2 across = new float2(-along.y, along.x);
                if (wantLog && TryLog(water, cell, spot, across, attempt))
                {
                    wantLog = false;
                    continue;
                }
                if (rocks > 0)
                {
                    AddRock(water, index, spot, attempt);
                    rocks--;
                }
            }
            return building.Count == 0 ? System.Array.Empty<TreePart>() : building.ToArray();
        }

        private static bool IsRiver(WaterMap water, float2 at, out int index)
        {
            return water.TryCell(at, out index) && water.Kind[index] == (byte)WaterKind.River && water.Depth[index] > 0.2f;
        }

        // Metres from a point to where the river ends along a direction, or -1 when it doesn't within MaxSpan.
        private static float Edge(WaterMap water, float2 from, float2 direction)
        {
            for (float d = 0.5f; d <= MaxSpan; d += 0.5f)
            {
                if (!IsRiver(water, from + direction * d, out _))
                {
                    return d;
                }
            }
            return -1f;
        }

        // A log across the river through the spot, resting on both banks and clear of the water.
        private bool TryLog(WaterMap water, Vector2Int cell, float2 spot, float2 across, int attempt)
        {
            float left = Edge(water, spot, -across), right = Edge(water, spot, across);
            if (left < 0f || right < 0f)
            {
                return false;
            }
            float2 a = spot - across * (left + 1.5f), b = spot + across * (right + 1.5f);
            float radius = math.lerp(0.3f, 0.5f, Roll(cell, 40 + attempt));
            water.TryCell(spot, out int mid);
            float level = water.Surface[mid];
            float ya = math.max(ground.Height(a.x, a.y), level) + radius * 0.6f;
            float yb = math.max(ground.Height(b.x, b.y), level) + radius * 0.6f;
            // Never lower than the water plus a hand's breadth at the middle.
            float lowest = math.min(ya, yb);
            if (lowest < level + radius + 0.15f)
            {
                float lift = level + radius + 0.15f - lowest;
                ya += lift;
                yb += lift;
            }
            var from = new float3(a.x, ya, a.y);
            var to = new float3(b.x, yb, b.y);
            // Two segments with a slight sag, so it doesn't read as a ruler.
            float3 middle = (from + to) * 0.5f;
            middle.y -= 0.1f;
            building.Add(TreePart.Segment(from, middle, radius * 1.1f, radius, wood));
            building.Add(TreePart.Segment(middle, to, radius, radius * 0.8f, wood));
            return true;
        }

        // A rock in the stream whose top shows above the surface.
        private void AddRock(WaterMap water, int index, float2 spot, int attempt)
        {
            var cell = new Vector2Int((int)math.floor(spot.x / CellSize), (int)math.floor(spot.y / CellSize));
            float size = math.lerp(0.5f, 1.2f, Roll(cell, 60 + attempt));
            float3 radii = new float3(size, size * math.lerp(0.5f, 0.8f, Roll(cell, 61 + attempt)), size * math.lerp(0.7f, 1f, Roll(cell, 62 + attempt)));
            float bed = water.Surface[index] - water.Depth[index];
            // Sunk into the bed so its top clears the surface by a little.
            float y = math.max(bed + radii.y * 0.4f, water.Surface[index] - radii.y * 0.5f);
            building.Add(TreePart.Boulder(
                new float3(spot.x, y, spot.y), radii, Roll(cell, 63 + attempt) * 2f * math.PI,
                float4.zero, float4.zero, float4.zero, float4.zero, rock));
        }
    }
}
