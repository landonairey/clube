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

        // --- The wall (GL409, GL422) ---

        /// <summary>
        /// The gate's pillars (GL409): big squarish stones stacked a little over 2.5 m either side of the gate, each on the
        /// ground under it, in the world's local metres. The rest of the wall is solid (<see cref="StampWall"/>, GL422).
        /// </summary>
        public static List<WallStone> GatePillars(Func<Vector2, float> groundAt, Func<Vector2, Vector2> toWorld, float worldYaw)
        {
            var stones = new List<WallStone>();
            float middle = (TownLayout.InnerHalf + TownLayout.OuterHalf) * 0.5f;
            float gate = TownLayout.GateWidth * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                var at = new Vector2(side * (gate + PillarWidth * 0.5f), -middle);
                float ground = groundAt(at);
                Vector2 world = toWorld(at);
                for (int k = 0; k < 6; k++)
                {
                    float y = ground + 0.25f + k * 0.48f;
                    var radii = new Vector3(PillarWidth * 0.5f, 0.27f, 0.7f);
                    stones.Add(new WallStone(new Vector3(world.x, y, world.y), radii, worldYaw + (k % 2) * 8f - 4f));
                }
            }
            return stones;
        }

        /// <summary>The width of each gate pillar along the wall, in metres.</summary>
        public const float PillarWidth = 0.9f;

        /// <summary>
        /// Writes the town wall into the world (GL422): a solid band of <paramref name="material"/> (loose stone) round the
        /// square, <see cref="TownLayout.WallHeight"/> over the ground under it, so it follows the land, with flat faces and
        /// a gap for the gate between its pillars. Never lowers ground.
        /// </summary>
        public static void StampWall(World world, Func<Vector2, Vector2> toTown, Func<Vector2, float> groundAt, Vector2 centre, byte material, float iso)
        {
            WorldGrid grid = world.Grid;
            float middle = (TownLayout.InnerHalf + TownLayout.OuterHalf) * 0.5f;
            float half = TownLayout.WallThickness * 0.5f;
            float gap = TownLayout.GateWidth * 0.5f + PillarWidth * 0.5f;
            float reach = TownLayout.OuterHalf + 1f;
            Vector3Int low = grid.WorldToNearestSample(new Vector3(centre.x - reach, 0f, centre.y - reach));
            Vector3Int high = grid.WorldToNearestSample(new Vector3(centre.x + reach, 0f, centre.y + reach));
            for (int z = low.z; z <= high.z; z++)
            {
                for (int x = low.x; x <= high.x; x++)
                {
                    Vector3 column = grid.SampleToWorld(new Vector3Int(x, 0, z));
                    Vector2 town = toTown(new Vector2(column.x, column.z));
                    // How far inside the ring's band the column is, across it.
                    float far = Mathf.Max(Mathf.Abs(town.x), Mathf.Abs(town.y));
                    float inside = half - Mathf.Abs(far - middle);
                    if (inside < -grid.VoxelSize || (town.y < 0f && Mathf.Abs(town.y) > Mathf.Abs(town.x) && Mathf.Abs(town.x) < gap))
                    {
                        continue;
                    }
                    StampColumn(world, x, z, groundAt(town), TownLayout.WallHeight, inside, material, iso);
                }
            }
        }

        /// <summary>
        /// Writes a straight run of the same stone wall (GL545), from <paramref name="from"/> to <paramref name="to"/> (world
        /// x, z), <paramref name="height"/> over the ground under each column and <paramref name="thickness"/> across, with
        /// flat faces. Never lowers ground.
        /// </summary>
        public static void StampWallRun(World world, Vector2 from, Vector2 to, float thickness, float height, Func<Vector2, float> groundAt, byte material, float iso)
        {
            WorldGrid grid = world.Grid;
            float half = thickness * 0.5f;
            float pad = half + grid.VoxelSize;
            Vector3Int low = grid.WorldToNearestSample(new Vector3(Mathf.Min(from.x, to.x) - pad, 0f, Mathf.Min(from.y, to.y) - pad));
            Vector3Int high = grid.WorldToNearestSample(new Vector3(Mathf.Max(from.x, to.x) + pad, 0f, Mathf.Max(from.y, to.y) + pad));
            Vector2 along = to - from;
            float length = along.magnitude;
            Vector2 dir = length > 0.0001f ? along / length : Vector2.right;
            for (int z = low.z; z <= high.z; z++)
            {
                for (int x = low.x; x <= high.x; x++)
                {
                    Vector3 column = grid.SampleToWorld(new Vector3Int(x, 0, z));
                    Vector2 at = new Vector2(column.x, column.z);
                    Vector2 rel = at - from;
                    float t = Mathf.Clamp(Vector2.Dot(rel, dir), 0f, length);
                    float inside = half - (rel - dir * t).magnitude;
                    if (inside < -grid.VoxelSize)
                    {
                        continue;
                    }
                    StampColumn(world, x, z, groundAt(at), height, inside, material, iso);
                }
            }
        }

        // One column of a wall: solid from just under the ground to height over it, the density falling off over its faces.
        private static void StampColumn(World world, int x, int z, float ground, float height, float inside, byte material, float iso)
        {
            WorldGrid grid = world.Grid;
            Vector3 column = grid.SampleToWorld(new Vector3Int(x, 0, z));
            float top = ground + height;
            int y0 = grid.WorldToNearestSample(new Vector3(column.x, ground - 1f, column.z)).y;
            int y1 = grid.WorldToNearestSample(new Vector3(column.x, top + 1.25f, column.z)).y;
            for (int y = y0; y <= y1; y++)
            {
                var sample = new Vector3Int(x, y, z);
                float depth = Mathf.Min(top - grid.SampleToWorld(sample).y, inside);
                float density = TerrainDensity.FromDepth(depth);
                if (!world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local))
                {
                    continue;
                }
                if (density > chunk.GetDensity(local) + 1.5f / 255f)
                {
                    world.SetDensity(sample, density);
                }
                if (density >= iso)
                {
                    world.SetMaterial(sample, material);
                }
            }
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

        // The spiral's centre line runs round a square this far out from the head, and drops this much per metre walked
        // (GL418: with a landing at each corner, each side climbs a metre over steps about 0.7 m deep, deeper than the player's
        // radius; the first stair's steps by the corners came out a quarter metre deep and the player stuck on them).
        public const float SpiralHalf = 2f;
        private const float Slope = 0.25f;

        /// <summary>
        /// A shaft's floor line (GL414), in the world's local metres: a square spiral stair from the surface at the head,
        /// down round a central pillar until it's level with the node, then a level drift straight to the node's face. The
        /// spiral starts at whichever corner makes it end at the corner nearest the node, so the drift leads away from the
        /// stair instead of cutting back under its lowest flight (GL418). <paramref name="groundAt"/> gives the land's height
        /// at (x, z).
        /// </summary>
        public static List<Vector3> ShaftPath(Vector2 head, Vector3 node, Func<float, float, float> groundAt)
        {
            var nodeXZ = new Vector2(node.x, node.z);
            List<Vector3> best = null;
            float bestDistance = float.PositiveInfinity;
            for (int start = 0; start < 4; start++)
            {
                List<Vector3> spiral = Spiral(head, node.y - 1f, start, groundAt);
                Vector3 end = spiral[spiral.Count - 1];
                float distance = (new Vector2(end.x, end.z) - nodeXZ).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = spiral;
                    bestDistance = distance;
                }
            }
            // The drift: level, straight to the node, stopping short of its centre so the ore is the face.
            Vector3 last = best[best.Count - 1];
            Vector2 toNode = nodeXZ - new Vector2(last.x, last.z);
            if (toNode.magnitude > 1.2f)
            {
                Vector2 face = nodeXZ - toNode.normalized * 0.9f;
                best.Add(new Vector3(face.x, last.y, face.y));
            }
            return best;
        }

        // The spiral stair from a corner of the head's square down to a floor level, turning the same way whatever the start.
        private static List<Vector3> Spiral(Vector2 head, float target, int start, Func<float, float, float> groundAt)
        {
            var path = new List<Vector3>();
            Vector2[] corners =
            {
                head + new Vector2(-SpiralHalf, -SpiralHalf), head + new Vector2(SpiralHalf, -SpiralHalf),
                head + new Vector2(SpiralHalf, SpiralHalf), head + new Vector2(-SpiralHalf, SpiralHalf),
            };
            Vector2 at = corners[start];
            float floor = groundAt(at.x, at.y);
            path.Add(new Vector3(at.x, floor, at.y));
            int corner = start + 1;
            // Round and down until level with the node (a few hundred metres at most, for safety).
            for (int i = 0; i < 400 && floor > target + 0.01f; i++)
            {
                Vector2 next = corners[corner % 4];
                float drop = (next - at).magnitude * Slope;
                at = next;
                // The last side is whole too, dropping only what's left, so its steps keep their depth (GL418).
                floor = Mathf.Max(target, floor - drop);
                path.Add(new Vector3(at.x, floor, at.y));
                corner++;
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
                    // Level over the landing at each end (the passage's half width), sloping between (GL418).
                    float climb = Mathf.Max(length - ShaftWidth, 1e-3f);
                    float f = Mathf.Lerp(a.y, b.y, Mathf.Clamp01((t - half) / climb));
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

        /// <summary>Of a set of voxels, those with ground in them (GL475: the cave's room carves only what's there).</summary>
        public static HashSet<Vector3Int> WithGround(World world, IEnumerable<Vector3Int> voxels, float iso)
        {
            var found = new HashSet<Vector3Int>();
            foreach (Vector3Int voxel in voxels)
            {
                if (HasGround(world, voxel, iso))
                {
                    found.Add(voxel);
                }
            }
            return found;
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
