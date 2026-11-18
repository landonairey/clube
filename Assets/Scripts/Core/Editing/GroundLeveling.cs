using System;
using System.Collections;
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

        // Plant samples looked at in one step of a sliced cut: about a millisecond.
        private const int SamplesPerStep = 64;

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

        /// <summary>See <see cref="Area.PavingOver"/>.</summary>
        public delegate VoxelMaterial PavingChoice(float x, float z, VoxelMaterial was, out float raise);

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

            /// <summary>
            /// GL647: what a paved column is topped with, given what its surface was made of (null: unknown), and how far
            /// its surface is raised over the target there. Null, or a null answer: <see cref="Paving"/>, not raised.
            /// </summary>
            public PavingChoice PavingOver;

            /// <summary>The material of the flat rectangle's top layer where it isn't paved (trodden dirt), or null to keep what's there.</summary>
            public VoxelMaterial Ground;

            /// <summary>
            /// Keep the land's shape (GL413: the town's square): the rectangle follows <see cref="Natural"/> instead of
            /// <see cref="Height"/>, so only what stands on it (trees, rocks) is cleared and its top layer is paved or covered.
            /// </summary>
            public bool FollowGround;

            /// <summary>
            /// The height each column (x, z) is set to, given the ground as it was there (GL613: a road's bed, even
            /// along its length and meeting the ground at its shoulders); null: <see cref="Height"/> or the ground.
            /// Columns it leaves within a hair of the ground keep their shape.
            /// </summary>
            public Func<float, float, float, float> Surface;

            /// <summary>
            /// Which columns (x, z) of the rectangle are worked on at all; null: every one. A road's piece only touches
            /// its own bed, so another road's bed inside the same rectangle is left as that road made it.
            /// </summary>
            public Func<float, float, bool> Inside;

            /// <summary>
            /// Metres round the rectangle past which a tree standing on it is not cut (its far branches are left); 0: the
            /// whole tree goes, and whatever its crown touches. For a narrow strip through a wood (a road), where whole
            /// trees joined crown to crown would be the wood.
            /// </summary>
            public float TreeReach;

            /// <summary>
            /// GL642: ground that is cut down keeps what its surface was made of (a terrace cut into a grassy hillside is
            /// grass again, not the bare stone under it). Ground built up always takes the old surface's material.
            /// </summary>
            public bool KeepTop;

            /// <summary>The height a column inside the rectangle is set to.</summary>
            public readonly float TargetAt(float x, float z, float ground)
            {
                return Surface != null ? Surface(x, z, ground) : FollowGround ? ground : Height;
            }
        }

        /// <summary>Levels an area: trees on it are cleared, then each column is set to its flat or ramped height.</summary>
        public static void Level(World world, MaterialRegistry materials, Area area, float iso)
        {
            SlicedWork.RunToEnd(LevelSteps(world, materials, area, iso));
        }

        /// <summary>
        /// <see cref="Level"/> in small steps for <see cref="SlicedWork"/> (GL771): cutting down a wood for one road piece took
        /// most of a second. The trees still go first and the ground after them, so it ends the same as one call.
        /// </summary>
        public static IEnumerator LevelSteps(World world, MaterialRegistry materials, Area area, float iso)
        {
            WorldGrid grid = world.Grid;
            VoxelMaterial grass = materials != null ? materials.Find("Grass") : null;
            VoxelMaterial dirt = materials != null ? materials.Find("Dirt") : null;
            Vector2 outer = area.Half + Vector2.one * Mathf.Max(0f, area.Ramp);
            var natural = new NaturalHeights(area, outer);
            Vector3Int low = grid.WorldToNearestSample(new Vector3(area.Centre.x - outer.x, 0f, area.Centre.y - outer.y));
            Vector3Int high = grid.WorldToNearestSample(new Vector3(area.Centre.x + outer.x, 0f, area.Centre.y + outer.y));
            yield return null;
            yield return ClearTreesOnGround(world, materials, area, low, high, natural, iso);
            for (int z = low.z; z <= high.z; z++)
            {
                LevelRows(world, materials, area, natural, grass, dirt, low.x, high.x, z, z, iso);
                yield return null;
            }
        }

        // Sets the columns of these rows to their flat or ramped height.
        private static void LevelRows(World world, MaterialRegistry materials, Area area, NaturalHeights natural, VoxelMaterial grass, VoxelMaterial dirt,
            int lowX, int highX, int lowZ, int highZ, float iso)
        {
            WorldGrid grid = world.Grid;
            float voxel = grid.VoxelSize;
            // Past the ground, solid samples (a boulder) are cleared until this many samples of air in a row.
            const int AirToStop = 4;
            int headroom = Mathf.CeilToInt(area.Headroom / voxel);
            for (int z = lowZ; z <= highZ; z++)
            {
                for (int x = lowX; x <= highX; x++)
                {
                    Vector3 column = grid.SampleToWorld(new Vector3Int(x, 0, z));
                    if (area.Inside != null && !area.Inside(column.x, column.z))
                    {
                        continue;
                    }
                    float beyond = Beyond(area, column.x, column.z);
                    float ground = natural.At(column.x, column.z);
                    float target = area.TargetAt(column.x, column.z, ground);
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
                    VoxelMaterial paving = area.Paving;
                    if (paved && area.PavingOver != null)
                    {
                        paving = area.PavingOver(column.x, column.z, TopOf(world, materials, grid, column, ground, iso), out float raise) ?? paving;
                        target += raise;
                    }
                    bool covered = beyond <= 0f && area.Ground != null && !paved;
                    // The density ramps over a metre each side of the surface (TerrainDensity), so that much either side of
                    // the old and new ground is all that can change; above it only stray solids are cleared.
                    float bottom = Mathf.Min(target, ground) - Mathf.Max(area.Fill, 1.25f);
                    float top = Mathf.Max(target, ground) + 1.25f;
                    int y0 = grid.WorldToNearestSample(new Vector3(column.x, bottom, column.z)).y;
                    int y1 = grid.WorldToNearestSample(new Vector3(column.x, top, column.z)).y;
                    // Ground already within a hair of its height keeps its shape: only its surface material changes.
                    bool keepShape = area.Natural != null
                        && ((area.FollowGround && area.Surface == null) || Mathf.Abs(target - ground) < KeepShapeWithin);
                    // What the surface was made of: new ground is topped with it (dirt stays dirt, sand sand), not always grass.
                    VoxelMaterial was = Mathf.Abs(target - ground) >= KeepShapeWithin ? TopOf(world, materials, grid, column, ground, iso) : null;
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
                        VoxelMaterial made = paved && surface ? paving
                            : covered && surface ? area.Ground
                            : before < iso ? (depth < TopsoilDepth ? was ?? grass : dirt)
                            : area.KeepTop && depth < TopsoilDepth ? was
                            : null;
                        if (made != null && made.Id != material)
                        {
                            world.SetMaterial(sample, made.Id);
                        }
                    }
                }
            }
        }

        // The material of the ground's surface at a column, as it lies now: the first solid sample under the height that is
        // bulk ground (not a tree, not ore). Null when there is none within a metre.
        private static VoxelMaterial TopOf(World world, MaterialRegistry materials, WorldGrid grid, Vector3 column, float ground, float iso)
        {
            if (materials == null)
            {
                return null;
            }
            Vector3Int sample = grid.WorldToNearestSample(new Vector3(column.x, ground, column.z));
            int steps = Mathf.CeilToInt(1f / grid.VoxelSize) + 1;
            for (int i = 0; i < steps; i++, sample.y--)
            {
                if (Read(world, sample, out byte id) >= iso)
                {
                    VoxelMaterial material = materials.Get(id);
                    return material != null && material.Category == VoxelMaterialCategory.Aggregate ? material : null;
                }
            }
            return null;
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
        private static IEnumerator ClearTreesOnGround(World world, MaterialRegistry materials, Area area, Vector3Int low, Vector3Int high, NaturalHeights natural, float iso)
        {
            if (materials == null)
            {
                yield break;
            }
            WorldGrid grid = world.Grid;
            var front = new Stack<Vector3Int>();
            var seen = new HashSet<Vector3Int>();
            int floorY = int.MaxValue;
            for (int z = low.z; z <= high.z; z++)
            {
                FindTrunks(world, materials, area, natural, low.x, high.x, z, iso, front, seen, ref floorY);
                yield return null;
            }
            // A wide area may hold a small wood: room for a few dozen trees.
            // A road through a wood (TreeReach): the cut stops a little way out, or touching crowns would take the wood with them.
            int reach = area.TreeReach > 0f ? Mathf.CeilToInt(area.TreeReach / grid.VoxelSize) : int.MaxValue / 4;
            var within = new RectInt(low.x - reach, low.z - reach, high.x - low.x + 2 * reach, high.z - low.z + 2 * reach);
            var cutOff = area.TreeReach > 0f ? new List<Vector3Int>() : null;
            var flood = new PlantFlood(world, materials, front, seen, floorY, iso, MaxTreeSamples * 20, area.TreeReach > 0f ? within : (RectInt?)null, cutOff);
            while (!flood.Step(SamplesPerStep))
            {
                yield return null;
            }
            if (cutOff != null)
            {
                var severed = new SeveredParts(world, materials, cutOff, iso);
                while (!severed.Step(SamplesPerStep))
                {
                    yield return null;
                }
            }
        }

        // The plant samples of one row of columns, in the band just over the ground where trunks stand.
        private static void FindTrunks(World world, MaterialRegistry materials, Area area, NaturalHeights natural, int lowX, int highX, int z, float iso,
            Stack<Vector3Int> front, HashSet<Vector3Int> seen, ref int floorY)
        {
            WorldGrid grid = world.Grid;
            const float TrunkBand = 2f;
            {
                for (int x = lowX; x <= highX; x++)
                {
                    Vector3 column = grid.SampleToWorld(new Vector3Int(x, 0, z));
                    if (area.Inside != null && !area.Inside(column.x, column.z))
                    {
                        continue;
                    }
                    float ground = natural.At(column.x, column.z);
                    float level = area.TargetAt(column.x, column.z, ground);
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
        }

        // GL652: what a cut that stops at a reach leaves behind. The part of a tree past the reach is still there; where
        // its trunk went with the cut, it hangs in the air (a branch, half a crown). Each such remainder is followed, and
        // cleared unless some of it still stands on the ground (a neighbouring tree whose crown touched) or runs down
        // into ground that is not loaded, or it is too large to be the remainder of one tree.
        private sealed class SeveredParts
        {
            private readonly World world;
            private readonly MaterialRegistry materials;
            private readonly List<Vector3Int> cutOff;
            private readonly float iso;
            private readonly HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
            private readonly List<Vector3Int> part = new List<Vector3Int>();
            private readonly Stack<Vector3Int> front = new Stack<Vector3Int>();
            private int nextStart;
            private int nextCleared;
            private bool following;
            private bool clearing;
            private bool stands;

            public SeveredParts(World world, MaterialRegistry materials, List<Vector3Int> cutOff, float iso)
            {
                this.world = world;
                this.materials = materials;
                this.cutOff = cutOff;
                this.iso = iso;
            }

            // Works through this many samples; true once every remainder has been dealt with.
            public bool Step(int samples)
            {
                while (samples > 0)
                {
                    samples--;
                    if (clearing)
                    {
                        world.SetDensity(part[nextCleared++], 0f);
                        clearing = nextCleared < part.Count;
                    }
                    else if (following)
                    {
                        Follow();
                    }
                    else if (nextStart >= cutOff.Count)
                    {
                        return true;
                    }
                    else
                    {
                        Begin(cutOff[nextStart++]);
                    }
                }
                return !clearing && !following && nextStart >= cutOff.Count;
            }

            private void Begin(Vector3Int start)
            {
                if (seen.Contains(start) || Read(world, start, out _) < iso || !IsPlant(world, materials, start))
                {
                    return;
                }
                part.Clear();
                front.Clear();
                front.Push(start);
                seen.Add(start);
                stands = false;
                following = true;
            }

            // One more sample of the remainder being followed.
            private void Follow()
            {
                Vector3Int sample = front.Pop();
                part.Add(sample);
                if (part.Count > MaxTreeSamples)
                {
                    stands = true;
                }
                else
                {
                    foreach (Vector3Int step in Around)
                    {
                        Vector3Int next = sample + step;
                        if (!world.TryGetOwner(next, out _, out _))
                        {
                            // Chunks of air over and beside a crown are often not held at all. Only ground that is not
                            // loaded under it could hold its trunk.
                            if (step.y < 0)
                            {
                                stands = true;
                                break;
                            }
                            continue;
                        }
                        if (Read(world, next, out _) < iso)
                        {
                            continue;
                        }
                        if (!IsPlant(world, materials, next))
                        {
                            // Solid ground under it: a trunk's foot.
                            stands |= step.y < 0;
                            continue;
                        }
                        if (seen.Add(next))
                        {
                            front.Push(next);
                        }
                    }
                }
                if (stands || front.Count == 0)
                {
                    following = false;
                    clearing = !stands && part.Count > 0;
                    nextCleared = 0;
                }
            }
        }

        // Empties every solid plant sample joined to the ones in front, never below floorY.
        private static void FloodClear(World world, MaterialRegistry materials, Stack<Vector3Int> front, HashSet<Vector3Int> seen, int floorY, float iso, int most)
        {
            new PlantFlood(world, materials, front, seen, floorY, iso, most, null, null).Step(int.MaxValue);
        }

        // The flood of a cut, a few samples at a time.
        private sealed class PlantFlood
        {
            private readonly World world;
            private readonly MaterialRegistry materials;
            private readonly Stack<Vector3Int> front;
            private readonly HashSet<Vector3Int> seen;
            private readonly int floorY;
            private readonly float iso;
            private readonly int most;
            private readonly RectInt? within;
            private readonly List<Vector3Int> cutOff;

            public PlantFlood(World world, MaterialRegistry materials, Stack<Vector3Int> front, HashSet<Vector3Int> seen, int floorY, float iso, int most, RectInt? within, List<Vector3Int> cutOff)
            {
                this.world = world;
                this.materials = materials;
                this.front = front;
                this.seen = seen;
                this.floorY = floorY;
                this.iso = iso;
                this.most = most;
                this.within = within;
                this.cutOff = cutOff;
            }

            // Empties up to this many samples; true once the flood has run out.
            public bool Step(int samples)
            {
                while (front.Count > 0 && seen.Count < most)
                {
                    if (samples-- <= 0)
                    {
                        return false;
                    }
                    Vector3Int sample = front.Pop();
                    world.SetDensity(sample, 0f);
                    foreach (Vector3Int step in Around)
                    {
                        Vector3Int next = sample + step;
                        if (within.HasValue && (next.x < within.Value.xMin || next.x > within.Value.xMax || next.z < within.Value.yMin || next.z > within.Value.yMax))
                        {
                            cutOff?.Add(next);
                            continue;
                        }
                        if (next.y >= floorY && !seen.Contains(next) && Read(world, next, out _) >= iso && IsPlant(world, materials, next))
                        {
                            seen.Add(next);
                            front.Push(next);
                        }
                    }
                }
                return true;
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
