using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Ground in whole, square voxels (GL411, GL412, GL414), on the carving the chisel uses (GL92). A block is a voxel
    /// whose eight corners are solid in one material, with every voxel round it that isn't ground carved out: marching
    /// cubes then draws nothing round it, and the carved voxels show flat walls towards it, so it stands as a cube.
    /// Carving a run of voxels out of the ground the same way leaves a tunnel with flat walls and square steps. Every
    /// change goes through the world's edit paths (A7), so it's saved and borders agree.
    /// </summary>
    public static class SquareVoxels
    {
        private static readonly List<Vector3Int> Around = BuildAround();

        private static readonly Vector3Int[] Faces =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        private static List<Vector3Int> BuildAround()
        {
            var around = new List<Vector3Int>(26);
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x != 0 || y != 0 || z != 0)
                        {
                            around.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }
            return around;
        }

        private static Vector3Int Corner(int corner)
        {
            return new Vector3Int(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
        }

        /// <summary>
        /// Builds blocks: each voxel (by its lowest corner, a global sample) becomes a solid cube of its material. Corners
        /// two blocks share take the material of the last one listed. Then every voxel round them that isn't a block and
        /// isn't wholly inside the ground is carved, so their faces are flat.
        /// </summary>
        public static void Build(World world, IReadOnlyDictionary<Vector3Int, byte> blocks, float iso)
        {
            // Solid first: raising a sample's density fills in carved voxels round it (World.UncarveAround), so the
            // carving has to come after.
            foreach (KeyValuePair<Vector3Int, byte> block in blocks)
            {
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3Int sample = block.Key + Corner(corner);
                    world.SetDensity(sample, 1f);
                    world.SetMaterial(sample, block.Value);
                }
            }
            var carve = new HashSet<Vector3Int>();
            foreach (Vector3Int voxel in blocks.Keys)
            {
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = voxel + step;
                    if (!blocks.ContainsKey(next) && !IsWhollySolid(world, next, iso))
                    {
                        carve.Add(next);
                    }
                }
            }
            foreach (Vector3Int voxel in carve)
            {
                if (!world.IsCarved(voxel))
                {
                    world.SetCarved(voxel, true);
                }
            }
        }

        /// <summary>Builds one block of a material (a board placed by the player). False when the voxel is already solid.</summary>
        public static bool Place(World world, Vector3Int voxel, byte material, float iso)
        {
            if (IsWhollySolid(world, voxel, iso) && !world.IsCarved(voxel))
            {
                return false;
            }
            if (world.IsCarved(voxel))
            {
                world.SetCarved(voxel, false);
            }
            Build(world, new Dictionary<Vector3Int, byte> { { voxel, material } }, iso);
            return true;
        }

        /// <summary>
        /// Carves voxels out of the ground (a tunnel, GL414): each becomes air with flat walls where it meets the ground.
        /// The ground round them that would fall (dirt, sand) is turned to <paramref name="lining"/>, so ceilings hold; null
        /// leaves it as it is.
        /// </summary>
        public static void Carve(World world, IEnumerable<Vector3Int> voxels, MaterialRegistry materials, VoxelMaterial lining, float iso)
        {
            var set = new HashSet<Vector3Int>(voxels);
            foreach (Vector3Int voxel in set)
            {
                if (!world.IsCarved(voxel))
                {
                    world.SetCarved(voxel, true);
                }
            }
            if (lining == null || materials == null)
            {
                return;
            }
            // The ground just outside the carved voxels: the corners of each uncarved voxel beside one, a sample deep.
            var lined = new HashSet<Vector3Int>();
            foreach (Vector3Int voxel in set)
            {
                foreach (Vector3Int face in Faces)
                {
                    Vector3Int beside = voxel + face;
                    if (set.Contains(beside))
                    {
                        continue;
                    }
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3Int sample = beside + Corner(corner);
                        if (!lined.Add(sample)
                            || !world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local) || chunk.GetDensity(local) < iso)
                        {
                            continue;
                        }
                        // Ground that falls once undercut (dirt, sand); rock holds while it's joined to the rest (GL327).
                        if (materials.Get(chunk.GetMaterial(local)) is VoxelMaterial material
                            && material.Hold.Fall != GroundFall.Stays && material.Hold.StabilityLoss > 0)
                        {
                            world.SetMaterial(sample, lining.Id);
                        }
                    }
                }
            }
        }

        // All eight corners solid and not carved: ground a block can't be carved into.
        private static bool IsWhollySolid(World world, Vector3Int voxel, float iso)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3Int sample = voxel + Corner(corner);
                if (!world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local) || chunk.GetDensity(local) < iso)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
