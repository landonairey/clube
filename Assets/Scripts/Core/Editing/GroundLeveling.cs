using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Levels ground in the world for something built on it (GL38's merchant table, GL400's town): a flat rectangle at one
    /// height, ramping back to the ground around it, with any trees standing on it cut down first. Every change goes through
    /// <see cref="World.SetDensity"/> (A7), so it is kept with the chunks and borders agree; samples that already hold the
    /// right density are left alone, so a level patch of ground costs little.
    /// </summary>
    public static class GroundLeveling
    {
        // Most plant samples a tree is cleared by: well over the biggest generated tree.
        private const int MaxTreeSamples = 60000;

        // Depth under the surface that new ground is topsoil (grass) rather than dirt, in metres.
        private const float TopsoilDepth = 0.6f;

        // Densities closer than this are the same: under one step of a byte-stored density (M12).
        private const float SameDensity = 1.5f / 255f;

        // Metres between the ground and its target height under which the ground is left as generated.
        private const float KeepShapeWithin = 0.12f;

        private static readonly Vector3Int[] Around =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>What to level and how.</summary>
        public struct Area
        {
            /// <summary>Centre of the flat rectangle (x, z), in the world's local metres.</summary>
            public Vector2 Centre;

            /// <summary>Half the flat rectangle's width and depth (x, z), in metres.</summary>
            public Vector2 Half;

            /// <summary>Height of the flat ground, in metres.</summary>
            public float Height;

            /// <summary>Metres around the rectangle over which the ground ramps back to <see cref="Natural"/>; 0 for none.</summary>
            public float Ramp;

            /// <summary>The ground as it was at (x, z), which the ramp meets; null: flat everywhere.</summary>
            public Func<float, float, float> Natural;

            /// <summary>Metres of air made above the ground, for what stands on it.</summary>
            public float Headroom;

            /// <summary>Metres of ground made solid below the surface, so no gap shows where it was lower.</summary>
            public float Fill;

            /// <summary>The material of the flat rectangle's top layer where it's paved (a cobbled path), or null to keep what's there.</summary>
            public VoxelMaterial Paving;

            /// <summary>Which points (x, z) of the flat rectangle are paved; null: all of it.</summary>
            public Func<float, float, bool> Paved;

            /// <summary>The material of the flat rectangle's top layer where it isn't paved (trodden dirt), or null to keep what's there.</summary>
            public VoxelMaterial Ground;

            /// <summary>
            /// Keep the land's shape (GL413: the town's square): the rectangle follows <see cref="Natural"/> instead of
            /// <see cref="Height"/>, so only what stands on it (trees, rocks) is cleared and its top layer is paved or covered.
            /// </summary>
            public bool FollowGround;
        }

        /// <summary>Levels an area: trees on it are cleared, then each column is set to its flat or ramped height.</summary>
        public static void Level(World world, MaterialRegistry materials, Area area, float iso)
        {
            WorldGrid grid = world.Grid;
            float voxel = grid.VoxelSize;
            VoxelMaterial grass = materials != null ? materials.Find("Grass") : null;
            VoxelMaterial dirt = materials != null ? materials.Find("Dirt") : null;
            Vector2 outer = area.Half + Vector2.one * Mathf.Max(0f, area.Ramp);
            var natural = new NaturalHeights(area, outer);
            Vector3Int low = grid.WorldToNearestSample(new Vector3(area.Centre.x - outer.x, 0f, area.Centre.y - outer.y));
            Vector3Int high = grid.WorldToNearestSample(new Vector3(area.Centre.x + outer.x, 0f, area.Centre.y + outer.y));
            ClearTreesOnGround(world, materials, area, low, high, natural, iso);

            // Past the ground, solid samples (a boulder) are cleared until this many samples of air in a row.
            const int AirToStop = 4;
            int headroom = Mathf.CeilToInt(area.Headroom / voxel);
            for (int z = low.z; z <= high.z; z++)
            {
                for (int x = low.x; x <= high.x; x++)
                {
                    Vector3 column = grid.SampleToWorld(new Vector3Int(x, 0, z));
                    float beyond = Beyond(area, column.x, column.z);
                    float ground = natural.At(column.x, column.z);
                    float target = area.FollowGround ? ground : area.Height;
                    if (beyond > 0f)
                    {
                        if (area.Ramp <= 0f || area.Natural == null)
                        {
                            continue;
                        }
                        float t = Mathf.Clamp01(beyond / area.Ramp);
                        target = Mathf.Lerp(area.Height, ground, t * t * (3f - 2f * t));
                    }
                    bool paved = beyond <= 0f && area.Paving != null && (area.Paved == null || area.Paved(column.x, column.z));
                    bool covered = beyond <= 0f && area.Ground != null && !paved;
                    // The density ramps over a metre each side of the surface (TerrainDensity), so that much either side of
                    // the old and new ground is all that can change; above it only stray solids are cleared.
                    float bottom = Mathf.Min(target, ground) - Mathf.Max(area.Fill, 1.25f);
                    float top = Mathf.Max(target, ground) + 1.25f;
                    int y0 = grid.WorldToNearestSample(new Vector3(column.x, bottom, column.z)).y;
                    int y1 = grid.WorldToNearestSample(new Vector3(column.x, top, column.z)).y;
                    // Ground already within a hair of its height keeps its shape: only its surface material changes.
                    bool keepShape = area.Natural != null && (area.FollowGround || Mathf.Abs(target - ground) < KeepShapeWithin);
                    int air = 0;
                    for (int y = y0; y <= y1 + headroom; y++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        float before = Read(world, sample, out byte material);
                        if (y > y1)
                        {
                            if (before <= 0f)
                            {
                                if (++air >= AirToStop)
                                {
                                    break;
                                }
                                continue;
                            }
                            air = 0;
                            world.SetDensity(sample, 0f);
                            continue;
                        }
                        float depth = target - grid.SampleToWorld(sample).y;
                        float density = TerrainDensity.FromDepth(depth);
                        // Densities may be kept as bytes (M12): within a step of one is already right.
                        // Ground kept as generated is still cleared of what stands on it (a surface rock) and filled where open.
                        bool write = keepShape ? (before >= iso) != (density >= iso) : Mathf.Abs(before - density) > SameDensity;
                        if (write)
                        {
                            world.SetDensity(sample, density);
                        }
                        if (density < iso)
                        {
                            continue;
                        }
                        bool surface = depth < voxel * 1.5f;
                        VoxelMaterial made = paved && surface ? area.Paving
                            : covered && surface ? area.Ground
                            : before < iso ? (depth < TopsoilDepth ? grass : dirt)
                            : null;
                        if (made != null && made.Id != material)
                        {
                            world.SetMaterial(sample, made.Id);
                        }
                    }
                }
            }
        }

        // A sample's density and material by its owning chunk: much quicker than the world's border-aware lookups, for
        // reading many samples. 0 (and material 0) where no chunk is loaded.
        private static float Read(World world, Vector3Int sample, out byte material)
        {
            if (world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local))
            {
                material = chunk.GetMaterial(local);
                return chunk.GetDensity(local);
            }
            material = 0;
            return 0f;
        }

        /// <summary>
        /// The ground as it was (<see cref="Area.Natural"/>) over the area, sampled once on a 1 m grid and read between the
        /// points: a generator's height is costly, and the ramp needs it for every column.
        /// </summary>
        private sealed class NaturalHeights
        {
            private const float Spacing = 1f;
            private readonly float[] heights;
            private readonly int width;
            private readonly int depth;
            private readonly Vector2 origin;
            private readonly float flat;

            public NaturalHeights(Area area, Vector2 outer)
            {
                flat = area.Height;
                origin = area.Centre - outer - Vector2.one * Spacing;
                width = Mathf.CeilToInt(outer.x * 2f / Spacing) + 3;
                depth = Mathf.CeilToInt(outer.y * 2f / Spacing) + 3;
                if (area.Natural == null)
                {
                    return;
                }
                heights = new float[width * depth];
                for (int j = 0; j < depth; j++)
                {
                    for (int i = 0; i < width; i++)
                    {
                        heights[i + j * width] = area.Natural(origin.x + i * Spacing, origin.y + j * Spacing);
                    }
                }
            }

            public float At(float x, float z)
            {
                if (heights == null)
                {
                    return flat;
                }
                float u = Mathf.Clamp((x - origin.x) / Spacing, 0f, width - 1.001f);
                float v = Mathf.Clamp((z - origin.y) / Spacing, 0f, depth - 1.001f);
                int i = (int)u, j = (int)v;
                float fu = u - i, fv = v - j;
                float a = heights[i + j * width], b = heights[i + 1 + j * width];
                float c = heights[i + (j + 1) * width], d = heights[i + 1 + (j + 1) * width];
                return Mathf.Lerp(Mathf.Lerp(a, b, fu), Mathf.Lerp(c, d, fu), fv);
            }
        }

        // How far outside the flat rectangle (x, z) is, in metres (the larger of the two axes); 0 or less inside it.
        private static float Beyond(Area area, float x, float z)
        {
            float dx = Mathf.Abs(x - area.Centre.x) - area.Half.x;
            float dz = Mathf.Abs(z - area.Centre.y) - area.Half.y;
            return Mathf.Max(dx, dz);
        }

        private static bool IsPlant(World world, MaterialRegistry materials, Vector3Int sample)
        {
            Read(world, sample, out byte id);
            return materials != null && materials.Get(id) is VoxelMaterial material && material.Kind == MaterialKind.Plant;
        }

        /// <summary>
        /// Cuts down every tree standing in a rectangle (GL338): trees are terrain data (GL30), so every solid plant sample
        /// joined to one found between <paramref name="floor"/> and <paramref name="ceiling"/> goes, crown and all, and
        /// nothing is left floating.
        /// </summary>
        public static void ClearTrees(World world, MaterialRegistry materials, Vector2 centre, Vector2 half, float floor, float ceiling, float iso)
        {
            if (materials == null)
            {
                return;
            }
            WorldGrid grid = world.Grid;
            Vector3Int low = grid.WorldToNearestSample(new Vector3(centre.x - half.x, floor, centre.y - half.y));
            Vector3Int high = grid.WorldToNearestSample(new Vector3(centre.x + half.x, ceiling, centre.y + half.y));
            var front = new Stack<Vector3Int>();
            var seen = new HashSet<Vector3Int>();
            for (int z = low.z; z <= high.z; z++)
            {
                for (int y = low.y; y <= high.y; y++)
                {
                    for (int x = low.x; x <= high.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if ((world.GetDensity(sample) ?? 0f) >= iso && IsPlant(world, materials, sample) && seen.Add(sample))
                        {
                            front.Push(sample);
                        }
                    }
                }
            }
            FloodClear(world, materials, front, seen, low.y, iso, MaxTreeSamples);
        }

        // Trees standing anywhere on a levelled area: only a band just over the ground of each column is searched for their
        // trunks (the ground as it was and as it will be), then each tree found is cut down whole.
        private static void ClearTreesOnGround(World world, MaterialRegistry materials, Area area, Vector3Int low, Vector3Int high, NaturalHeights natural, float iso)
        {
            if (materials == null)
            {
                return;
            }
            WorldGrid grid = world.Grid;
            const float TrunkBand = 2f;
            var front = new Stack<Vector3Int>();
            var seen = new HashSet<Vector3Int>();
            int floorY = int.MaxValue;
            for (int z = low.z; z <= high.z; z++)
            {
                for (int x = low.x; x <= high.x; x++)
                {
                    Vector3 column = grid.SampleToWorld(new Vector3Int(x, 0, z));
                    float ground = natural.At(column.x, column.z);
                    float level = area.FollowGround ? ground : area.Height;
                    float bottom = Mathf.Min(ground, level) - grid.VoxelSize;
                    float top = Mathf.Max(ground, level) + TrunkBand;
                    int y0 = grid.WorldToNearestSample(new Vector3(column.x, bottom, column.z)).y;
                    int y1 = grid.WorldToNearestSample(new Vector3(column.x, top, column.z)).y;
                    floorY = Mathf.Min(floorY, y0);
                    for (int y = y0; y <= y1; y++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (Read(world, sample, out byte id) >= iso && materials.Get(id) is VoxelMaterial m && m.Kind == MaterialKind.Plant && seen.Add(sample))
                        {
                            front.Push(sample);
                        }
                    }
                }
            }
            // A wide area may hold a small wood: room for a few dozen trees.
            FloodClear(world, materials, front, seen, floorY, iso, MaxTreeSamples * 20);
        }

        // Empties every solid plant sample joined to the ones in front, never below floorY.
        private static void FloodClear(World world, MaterialRegistry materials, Stack<Vector3Int> front, HashSet<Vector3Int> seen, int floorY, float iso, int most)
        {
            while (front.Count > 0 && seen.Count < most)
            {
                Vector3Int sample = front.Pop();
                world.SetDensity(sample, 0f);
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    if (next.y >= floorY && !seen.Contains(next) && Read(world, next, out _) >= iso && IsPlant(world, materials, next))
                    {
                        seen.Add(next);
                        front.Push(next);
                    }
                }
            }
        }

        /// <summary>
        /// The surface height at a point as the world holds it now: the highest solid sample of its column that isn't part of
        /// a tree, and where between it and the air above the density crosses the iso level. 0 when there's no ground.
        /// </summary>
        public static float GroundHeight(World world, MaterialRegistry materials, Vector3 point, float iso, int heightInChunks)
        {
            WorldGrid grid = world.Grid;
            Vector3Int column = grid.WorldToNearestSample(point);
            int top = heightInChunks * grid.ChunkSize.y - 1;
            for (int y = top; y >= 0; y--)
            {
                float? below = world.GetDensity(new Vector3Int(column.x, y, column.z));
                if (!below.HasValue || below.Value < iso || IsPlant(world, materials, new Vector3Int(column.x, y, column.z)))
                {
                    continue;
                }
                float above = world.GetDensity(new Vector3Int(column.x, y + 1, column.z)) ?? 0f;
                float fraction = Mathf.Approximately(below.Value, above) ? 0f : Mathf.Clamp01((below.Value - iso) / (below.Value - above));
                return grid.SampleToWorld(new Vector3Int(column.x, y, column.z)).y + fraction * grid.VoxelSize;
            }
            return 0f;
        }
    }
}
