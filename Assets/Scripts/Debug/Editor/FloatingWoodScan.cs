using System.Collections.Generic;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Looks for wood and leaves hanging in the air in a loaded world (GL652): every joined part of plant samples in a
    /// box is followed, and counted as floating when none of it rests on ground. For checking, in Play mode, that
    /// cutting trees for a road or a settlement leaves nothing behind.
    /// </summary>
    public static class FloatingWoodScan
    {
        private static readonly Vector3Int[] Around =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>Scans a box of the world; returns how many parts stand, how many float, and where the first few floating ones are.</summary>
        public static string Scan(World world, MaterialRegistry materials, Vector3 centre, Vector3 half, float iso = 0.5f)
        {
            WorldGrid grid = world.Grid;
            Vector3Int low = grid.WorldToNearestSample(centre - half), high = grid.WorldToNearestSample(centre + half);
            var seen = new HashSet<Vector3Int>();
            var front = new Stack<Vector3Int>();
            var report = new StringBuilder();
            int floating = 0, standing = 0;
            bool Plant(Vector3Int sample)
            {
                if ((world.GetDensity(sample) ?? 0f) < iso)
                {
                    return false;
                }
                byte? id = world.GetMaterial(sample);
                return id.HasValue && materials.Get(id.Value) is VoxelMaterial material && material.Kind == MaterialKind.Plant;
            }
            for (int z = low.z; z <= high.z; z += 2)
            {
                for (int y = low.y; y <= high.y; y += 2)
                {
                    for (int x = low.x; x <= high.x; x += 2)
                    {
                        var start = new Vector3Int(x, y, z);
                        if (seen.Contains(start) || !Plant(start))
                        {
                            continue;
                        }
                        front.Clear();
                        front.Push(start);
                        seen.Add(start);
                        bool stands = false;
                        int count = 0;
                        Vector3Int lowest = start;
                        while (front.Count > 0 && count < 200000)
                        {
                            Vector3Int sample = front.Pop();
                            count++;
                            lowest = sample.y < lowest.y ? sample : lowest;
                            foreach (Vector3Int step in Around)
                            {
                                Vector3Int next = sample + step;
                                if ((world.GetDensity(next) ?? 0f) < iso)
                                {
                                    continue;
                                }
                                if (!Plant(next))
                                {
                                    stands |= step.y < 0;
                                }
                                else if (seen.Add(next))
                                {
                                    front.Push(next);
                                }
                            }
                        }
                        if (stands || count >= 200000)
                        {
                            standing++;
                            continue;
                        }
                        if (++floating <= 5)
                        {
                            report.Append($" [{count} samples, lowest at {grid.SampleToWorld(lowest)}]");
                        }
                    }
                }
            }
            return $"plant parts standing {standing}, floating {floating}{report}";
        }
    }
}
