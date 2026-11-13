using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Finds standing trees to chop (GL615). Where trees were planted comes from the <see cref="TreeField"/> (the same
    /// every time, and cheap to ask); whether one still stands is read from the world, so a felled tree is not found
    /// again.
    /// </summary>
    public static class TreeFinder
    {
        /// <summary>Metres over the ground a trunk is chopped at.</summary>
        public const float ChopHeight = 0.75f;

        /// <summary>
        /// Every spot the field plants a tree at between two distances of <paramref name="around"/> (x, z), nearest first.
        /// Some have no tree after all (ground too steep), and some trees are gone: <see cref="FindTrunk"/> looks.
        /// </summary>
        public static void SpotsNear(TreeField field, Vector2 around, float from, float to, List<Vector2> spots)
        {
            spots.Clear();
            float size = field.CellSize;
            int span = Mathf.CeilToInt(to / size) + 1;
            var middle = new Vector2Int(Mathf.FloorToInt(around.x / size), Mathf.FloorToInt(around.y / size));
            for (int z = -span; z <= span; z++)
            {
                for (int x = -span; x <= span; x++)
                {
                    if (field.TryGetSpot(new Vector2Int(middle.x + x, middle.y + z), out Vector2 spot))
                    {
                        float distance = (spot - around).sqrMagnitude;
                        if (distance >= from * from && distance <= to * to)
                        {
                            spots.Add(spot);
                        }
                    }
                }
            }
            spots.Sort((a, b) => (a - around).sqrMagnitude.CompareTo((b - around).sqrMagnitude));
        }

        /// <summary>
        /// Looks at up to <paramref name="budget"/> spots from <paramref name="cursor"/> on for a standing trunk; true with
        /// a wood sample at chopping height. The cursor stays on a tree that is found (it is asked about again until it
        /// is felled) and on a spot whose ground is not loaded; it passes spots with no tree.
        /// </summary>
        /// <param name="ground">The ground's height at (x, z).</param>
        /// <param name="allowed">False for a spot nothing may be felled at (on a road, say); may be null.</param>
        public static bool FindTrunk(World world, TreeMaterials trees, IReadOnlyList<Vector2> spots, ref int cursor, int budget,
            Func<float, float, float> ground, Func<Vector2, bool> allowed, float isoLevel, out Vector3Int trunk)
        {
            trunk = default;
            for (int looked = 0; looked < budget && cursor < spots.Count; looked++)
            {
                Vector2 spot = spots[cursor];
                if (allowed == null || allowed(spot))
                {
                    Vector3Int at = world.Grid.WorldToNearestSample(new Vector3(spot.x, ground(spot.x, spot.y) + ChopHeight, spot.y));
                    if (!world.GetDensity(at).HasValue)
                    {
                        return false;
                    }
                    if (WoodNear(world, trees, spot, ground, 1.5f, isoLevel, out trunk))
                    {
                        return true;
                    }
                }
                cursor++;
            }
            return false;
        }

        /// <summary>The wood sample at chopping height nearest a spot (x, z), within <paramref name="reach"/> metres of it.</summary>
        public static bool WoodNear(World world, TreeMaterials trees, Vector2 spot, Func<float, float, float> ground, float reach, float isoLevel,
            out Vector3Int wood)
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
