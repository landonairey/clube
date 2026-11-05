using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One stone of the town wall (GL409): an ellipsoid turned about y, in the world's local metres.</summary>
    public readonly struct WallStone
    {
        public WallStone(Vector3 centre, Vector3 radii, float yaw)
        {
            Centre = centre;
            Radii = radii;
            Yaw = yaw;
        }

        public Vector3 Centre { get; }

        /// <summary>Half-sizes along its length (x), height (y) and across the wall (z), before the turn.</summary>
        public Vector3 Radii { get; }

        public float Yaw { get; }
    }

    /// <summary>
    /// The town's parts as terrain (GL409, GL412, GL414): the wall's stones, a shop's voxels of stone and boards, and a mine
    /// shaft's stairs, worked out as plain data for <see cref="TownPlan"/> to place and the town's site to write into the
    /// world. Shapes only; nothing here touches the world but the two Stamp/Carve helpers.
    /// </summary>
    public static class TownStructures
    {
        /// <summary>What a shop voxel is made of (GL412).</summary>
        public enum ShopPart
        {
            Stone,
            Boards,
            Trim,
        }

        // --- The wall (GL409) ---

        /// <summary>
        /// The wall's stones round the square, in the town's frame (x, z) with heights from <paramref name="groundAt"/>
        /// (town frame → height): courses of uneven stones two across, each standing on the ground under it, so the wall
        /// follows the land; tall stacks either side of the gate. Seeded, so a town's wall is always the same.
        /// </summary>
        public static List<WallStone> WallStones(int seed, Func<Vector2, float> groundAt, Func<Vector2, Vector2> toWorld, float worldYaw)
        {
            var stones = new List<WallStone>();
            var random = new System.Random(seed);
            float inner = TownLayout.InnerHalf;
            float outer = TownLayout.OuterHalf;
            float middle = (inner + outer) * 0.5f;
            float gate = TownLayout.GateWidth * 0.5f;
            const float Pillar = 0.9f;

            void Run(Vector2 a, Vector2 b)
            {
                Vector2 along = b - a;
                float length = along.magnitude;
                if (length < 0.2f)
                {
                    return;
                }
                Vector2 dir = along / length;
                var across = new Vector2(-dir.y, dir.x);
                float runYaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - 90f;
                float course = 0f;
                int courses = 4;
                for (int k = 0; k < courses; k++)
                {
                    // Each course starts half a stone on from the one below, so the joints don't line up.
                    float t = k % 2 == 0 ? 0f : 0.35f;
                    while (t < length - 0.15f)
                    {
                        float piece = Mathf.Min(length - t, 0.55f + (float)random.NextDouble() * 0.35f);
                        Vector2 mid = a + dir * (t + piece * 0.5f);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 at = mid + across * (side * TownLayout.WallThickness * 0.24f + ((float)random.NextDouble() - 0.5f) * 0.06f);
                            float height = 0.22f + (float)random.NextDouble() * 0.06f;
                            float ground = groundAt(at);
                            course = ground + 0.12f + k * 0.5f + height + ((float)random.NextDouble() - 0.5f) * 0.06f;
                            Vector2 world = toWorld(at);
                            // A little short of the next stone and of uneven depth, so crevices show between them.
                            float depth = TownLayout.WallThickness * (0.22f + (float)random.NextDouble() * 0.12f);
                            var radii = new Vector3(piece * 0.5f - 0.03f, height - 0.01f, depth);
                            float yaw = worldYaw + runYaw + ((float)random.NextDouble() - 0.5f) * 10f;
                            stones.Add(new WallStone(new Vector3(world.x, course, world.y), radii, yaw));
                        }
                        t += piece;
                    }
                }
            }

            Run(new Vector2(-outer, middle), new Vector2(outer, middle));
            Run(new Vector2(-middle, -inner), new Vector2(-middle, inner));
            Run(new Vector2(middle, -inner), new Vector2(middle, inner));
            Run(new Vector2(-outer, -middle), new Vector2(-gate - Pillar, -middle));
            Run(new Vector2(gate + Pillar, -middle), new Vector2(outer, -middle));
            // The gate's pillars: big squarish stones stacked a little higher than the wall.
            foreach (float side in new[] { -1f, 1f })
            {
                var at = new Vector2(side * (gate + Pillar * 0.5f), -middle);
                float ground = groundAt(at);
                Vector2 world = toWorld(at);
                for (int k = 0; k < 6; k++)
                {
                    float y = ground + 0.25f + k * 0.48f;
                    var radii = new Vector3(Pillar * 0.5f, 0.27f, TownLayout.WallThickness * 0.55f);
                    stones.Add(new WallStone(new Vector3(world.x, y, world.y), radii, worldYaw + (k % 2) * 8f - 4f));
                }
            }
            return stones;
        }

        /// <summary>
        /// Writes stones into the world (GL409): each sample inside one becomes solid stone, with the density falling off
        /// over its surface like generated ground, so marching cubes draws each stone as a faceted lump. Never lowers ground.
        /// </summary>
        public static void StampStones(World world, IReadOnlyList<WallStone> stones, byte material, float iso)
        {
            WorldGrid grid = world.Grid;
            foreach (WallStone stone in stones)
            {
                float reach = Mathf.Max(stone.Radii.x, Mathf.Max(stone.Radii.y, stone.Radii.z)) + grid.VoxelSize * 2f;
                Vector3Int low = grid.WorldToNearestSample(stone.Centre - Vector3.one * reach);
                Vector3Int high = grid.WorldToNearestSample(stone.Centre + Vector3.one * reach);
                Quaternion unturn = Quaternion.Euler(0f, -stone.Yaw, 0f);
                float least = Mathf.Min(stone.Radii.x, Mathf.Min(stone.Radii.y, stone.Radii.z));
                for (int z = low.z; z <= high.z; z++)
                {
                    for (int y = low.y; y <= high.y; y++)
                    {
                        for (int x = low.x; x <= high.x; x++)
                        {
                            var sample = new Vector3Int(x, y, z);
                            Vector3 d = unturn * (grid.SampleToWorld(sample) - stone.Centre);
                            float e = Mathf.Sqrt(Sq(d.x / stone.Radii.x) + Sq(d.y / stone.Radii.y) + Sq(d.z / stone.Radii.z));
                            float density = TerrainDensity.FromDepth(least * (1f - e));
                            if (density <= 0f || !world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local))
                            {
                                continue;
                            }
                            float before = chunk.GetDensity(local);
                            if (density > before + 1.5f / 255f)
                            {
                                world.SetDensity(sample, density);
                                if (density >= iso)
                                {
                                    world.SetMaterial(sample, material);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static float Sq(float v)
        {
            return v * v;
        }

        // --- A shop (GL412) ---

        /// <summary>
        /// A shop's voxels in its own frame, by their centres (x, height over the pad, z) in metres: a board floor, a stone
        /// plinth and corner posts, board walls on three sides, a stepped board roof, a counter, a shelf and a lintel in the
        /// trim boards. <paramref name="voxel"/> is the world's voxel size.
        /// </summary>
        public static List<(Vector3 Centre, ShopPart Part)> ShopVoxels(float voxel)
        {
            var cells = new List<(Vector3, ShopPart)>();
            float w = TownLayout.ShopWidth * 0.5f;
            float d = TownLayout.ShopDepth * 0.5f;
            float floor = TownLayout.FloorHeight;
            float top = floor + TownLayout.ShopHeight;
            const float Plinth = 0.75f;
            const float Post = 0.5f;
            float roofRise = 1.25f;
            float h = voxel * 0.5f;

            // Every cell over the roof's reach (a voxel past the walls), bottom to top.
            for (float z = -d - voxel + h; z < d + voxel; z += voxel)
            {
                for (float x = -w - voxel + h; x < w + voxel; x += voxel)
                {
                    bool inside = Mathf.Abs(x) < w && Mathf.Abs(z) < d;
                    bool sideWall = Mathf.Abs(x) > w - voxel && Mathf.Abs(z) < d;
                    bool backWall = z < -d + voxel && Mathf.Abs(x) < w;
                    bool post = Mathf.Abs(x) > w - Post && Mathf.Abs(z) > d - Post && Mathf.Abs(z) < d;
                    bool frontBeam = z > d - voxel && z < d && Mathf.Abs(x) < w;
                    bool counter = Mathf.Abs(x) < TownLayout.CounterLength * 0.5f && z > TownLayout.CounterZ - 0.5f && z < TownLayout.CounterZ;
                    bool shelf = x > -w + 0.5f && x < -0.5f && z > -d + voxel && z < -d + 2f * voxel;
                    // The roof's height over this column: a stepped gable, its ridge along x.
                    float roofY = top + Mathf.Floor((d + voxel - Mathf.Abs(z)) / (2f * voxel)) * voxel;
                    roofY = Mathf.Min(roofY, top + roofRise);
                    for (float y = h; y < top + roofRise + voxel; y += voxel)
                    {
                        ShopPart? part = null;
                        if (y < floor)
                        {
                            part = inside ? ShopPart.Boards : (ShopPart?)null;
                        }
                        else if (y < top)
                        {
                            if (post)
                            {
                                part = ShopPart.Stone;
                            }
                            else if (sideWall || backWall)
                            {
                                part = y < floor + Plinth ? ShopPart.Stone : ShopPart.Boards;
                            }
                            else if (frontBeam && y > top - voxel)
                            {
                                part = ShopPart.Trim;
                            }
                            else if (counter && y < floor + TownLayout.CounterHeight)
                            {
                                part = ShopPart.Trim;
                            }
                            else if (shelf && y > floor + 1.25f && y < floor + 1.25f + voxel)
                            {
                                part = ShopPart.Trim;
                            }
                        }
                        else if (y < roofY + voxel)
                        {
                            // Two layers deep, so neighbouring steps share a face and no slit shows between them.
                            if (y > roofY - voxel)
                            {
                                part = ShopPart.Boards;
                            }
                            else if (Mathf.Abs(x) > w - voxel && Mathf.Abs(x) < w && Mathf.Abs(z) < d)
                            {
                                // The gable ends fill up under the roof.
                                part = ShopPart.Boards;
                            }
                        }
                        if (part.HasValue)
                        {
                            cells.Add((new Vector3(x, y, z), part.Value));
                        }
                    }
                }
            }
            return cells;
        }

        // --- A mine shaft (GL414) ---

        /// <summary>The width of a shaft's passage and the height over its floor, in metres.</summary>
        public const float ShaftWidth = 1.25f;
        public const float ShaftHeight = 2.4f;

        // The spiral's centre line runs round a square this far out from the head, and drops this much per metre walked.
        private const float SpiralHalf = 1.5f;
        private const float Slope = 0.5f;

        /// <summary>
        /// A shaft's floor line (GL414), in the world's local metres: a square spiral stair from the surface at the head,
        /// down round a central pillar until it's level with the node, then a level drift straight to the node's face.
        /// <paramref name="groundAt"/> gives the land's height at (x, z).
        /// </summary>
        public static List<Vector3> ShaftPath(Vector2 head, Vector3 node, Func<float, float, float> groundAt)
        {
            var path = new List<Vector3>();
            float target = node.y - 1f;
            Vector2[] corners =
            {
                head + new Vector2(-SpiralHalf, -SpiralHalf), head + new Vector2(SpiralHalf, -SpiralHalf),
                head + new Vector2(SpiralHalf, SpiralHalf), head + new Vector2(-SpiralHalf, SpiralHalf),
            };
            Vector2 at = corners[0];
            float floor = groundAt(at.x, at.y);
            path.Add(new Vector3(at.x, floor, at.y));
            int corner = 1;
            // Round and down until level with the node (a few hundred metres at most, for safety).
            for (int i = 0; i < 400 && floor > target + 0.01f; i++)
            {
                Vector2 next = corners[corner % 4];
                float length = (next - at).magnitude;
                float drop = length * Slope;
                if (floor - drop <= target)
                {
                    // Level with the node part way along this side.
                    float part = (floor - target) / drop;
                    at = Vector2.Lerp(at, next, part);
                    floor = target;
                    path.Add(new Vector3(at.x, floor, at.y));
                    break;
                }
                at = next;
                floor -= drop;
                path.Add(new Vector3(at.x, floor, at.y));
                corner++;
            }
            // The drift: level, straight to the node, stopping short of its centre so the ore is the face.
            var nodeXZ = new Vector2(node.x, node.z);
            Vector2 toNode = nodeXZ - at;
            if (toNode.magnitude > 1.2f)
            {
                Vector2 end = nodeXZ - toNode.normalized * 0.9f;
                path.Add(new Vector3(end.x, floor, end.y));
            }
            return path;
        }

        /// <summary>
        /// The voxels a shaft's passage takes (GL414): along each stretch of the floor line, a box the shaft's width across
        /// and its height over the floor, on the voxel grid, so the floor steps a voxel at a time. Only voxels with ground in
        /// them are listed; air over the surface stays as it is.
        /// </summary>
        public static HashSet<Vector3Int> ShaftVoxels(World world, IReadOnlyList<Vector3> path, float iso)
        {
            var voxels = new HashSet<Vector3Int>();
            float size = world.Grid.VoxelSize;
            float half = ShaftWidth * 0.5f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1], b = path[i];
                Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float length = flat.magnitude;
                if (length < 1e-4f)
                {
                    continue;
                }
                Vector3 across = new Vector3(-flat.z, 0f, flat.x) / length;
                // A little past each end, so corners are carved out square.
                for (float t = -half; t <= length + half; t += size * 0.5f)
                {
                    float f = Mathf.Lerp(a.y, b.y, Mathf.Clamp01(t / length));
                    Vector3 centre = a + flat / length * t;
                    for (float o = -half; o <= half + 1e-3f; o += size * 0.5f)
                    {
                        for (float y = 0.01f; y <= ShaftHeight; y += size * 0.5f)
                        {
                            Vector3 p = centre + across * o;
                            p.y = f + y;
                            voxels.Add(Vector3Int.FloorToInt(p / size));
                        }
                    }
                }
            }
            voxels.RemoveWhere(v => !HasGround(world, v, iso));
            return voxels;
        }

        private static bool HasGround(World world, Vector3Int voxel, float iso)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                var sample = voxel + new Vector3Int(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                if (world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local) && chunk.GetDensity(local) >= iso)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
