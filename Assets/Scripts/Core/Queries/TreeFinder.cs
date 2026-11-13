using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Finds standing wood to chop (GL615): looks at spots round a place for a tree's crown from above, then for wood at
    /// chopping height under it. What it finds is what is in the world now, so a felled tree is not found again, and a
    /// log lying on the ground is (it gets cut up too).
    /// </summary>
    public static class TreeFinder
    {
        /// <summary>Metres over the ground a trunk is chopped at.</summary>
        public const float ChopHeight = 0.75f;

        /// <summary>
        /// Tries <paramref name="tries"/> spots between two distances from <paramref name="around"/> (x, z), nearer ones
        /// first; true with a wood sample at chopping height (<paramref name="trunk"/>) at the first that has a tree.
        /// </summary>
        /// <param name="ground">The ground's height at (x, z).</param>
        /// <param name="allowed">False for a spot nothing may be felled at (inside a settlement, say); may be null.</param>
        public static bool FindTrunk(World world, ChunkMeshSettings mesh, TreeMaterials trees, Vector2 around, float from, float to,
            Func<float, float, float> ground, Func<Vector2, bool> allowed, System.Random random, int tries, float isoLevel, out Vector3Int trunk)
        {
            trunk = default;
            for (int i = 0; i < tries; i++)
            {
                float distance = Mathf.Lerp(from, to, (i + (float)random.NextDouble()) / tries);
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                Vector2 spot = around + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (allowed != null && !allowed(spot))
                {
                    continue;
                }
                float height = ground(spot.x, spot.y);
                var down = new Ray(new Vector3(spot.x, height + 40f, spot.y), Vector3.down);
                if (!world.Raycast(down, mesh, 39f, out WorldHit hit))
                {
                    continue;
                }
                byte? top = world.GetMaterial(world.Grid.WorldToNearestSample(hit.Point + Vector3.down * (world.Grid.VoxelSize * 0.5f)));
                if (!top.HasValue || !trees.IsTree(top.Value))
                {
                    continue;
                }
                if (WoodNear(world, trees, spot, ground, 4f, isoLevel, allowed, out trunk))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The wood sample at chopping height nearest a spot (x, z), within <paramref name="reach"/> metres of it.</summary>
        public static bool WoodNear(World world, TreeMaterials trees, Vector2 spot, Func<float, float, float> ground, float reach, float isoLevel,
            Func<Vector2, bool> allowed, out Vector3Int wood)
        {
            wood = default;
            float voxel = world.Grid.VoxelSize;
            int span = Mathf.CeilToInt(reach / voxel);
            Vector3Int middle = world.Grid.WorldToNearestSample(new Vector3(spot.x, 0f, spot.y));
            int best = int.MaxValue;
            for (int z = -span; z <= span; z++)
            {
                for (int x = -span; x <= span; x++)
                {
                    int far = x * x + z * z;
                    if (far >= best || far > span * span)
                    {
                        continue;
                    }
                    float wx = (middle.x + x) * voxel, wz = (middle.z + z) * voxel;
                    if (allowed != null && !allowed(new Vector2(wx, wz)))
                    {
                        continue;
                    }
                    var sample = new Vector3Int(middle.x + x, Mathf.RoundToInt((ground(wx, wz) + ChopHeight) / voxel), middle.z + z);
                    if (IsWood(world, trees, sample, isoLevel))
                    {
                        best = far;
                        wood = sample;
                    }
                }
            }
            return best != int.MaxValue;
        }

        /// <summary>True when a sample is solid wood.</summary>
        public static bool IsWood(World world, TreeMaterials trees, Vector3Int sample, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            byte? material = world.GetMaterial(sample);
            return density.HasValue && density.Value >= isoLevel && material.HasValue && trees.IsWood(material.Value);
        }
    }
}
