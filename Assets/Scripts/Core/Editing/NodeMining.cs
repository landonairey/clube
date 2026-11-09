using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A townsperson mining a node by hand (GL415): one sample at a time, the nearest solid sample of the node's ore within
    /// reach of the face, emptied through the world's edit path (A7). Slow on purpose: the caller paces it.
    /// </summary>
    public static class NodeMining
    {
        /// <summary>
        /// Empties the nearest solid sample of one of <paramref name="ores"/> within <paramref name="reach"/> metres of
        /// <paramref name="point"/> (the world's local metres). Returns its material, or null when there's none left in reach.
        /// </summary>
        public static byte? TakeNearest(World world, Vector3 point, float reach, ICollection<byte> ores, float iso)
        {
            WorldGrid grid = world.Grid;
            Vector3Int low = grid.WorldToNearestSample(point - Vector3.one * reach);
            Vector3Int high = grid.WorldToNearestSample(point + Vector3.one * reach);
            float best = reach * reach;
            Vector3Int? found = null;
            byte material = 0;
            for (int z = low.z; z <= high.z; z++)
            {
                for (int y = low.y; y <= high.y; y++)
                {
                    for (int x = low.x; x <= high.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        float distance = (grid.SampleToWorld(sample) - point).sqrMagnitude;
                        if (distance > best || !world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local) || chunk.GetDensity(local) < iso)
                        {
                            continue;
                        }
                        byte id = chunk.GetMaterial(local);
                        if (ores.Contains(id))
                        {
                            best = distance;
                            found = sample;
                            material = id;
                        }
                    }
                }
            }
            if (!found.HasValue)
            {
                return null;
            }
            world.SetDensity(found.Value, 0f);
            return material;
        }
    }
}
