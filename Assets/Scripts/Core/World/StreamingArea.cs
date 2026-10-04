using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The chunks wanted around a centre chunk (M3): every column within the render
    /// distance horizontally (a circle), across all of the world's chunk layers, nearest first.
    /// </summary>
    public static class StreamingArea
    {
        /// <summary>Replaces the contents of <paramref name="coords"/> with the wanted chunks, nearest first.</summary>
        public static void Collect(Vector3Int centre, int renderDistance, int heightInChunks, List<Vector3Int> coords)
        {
            coords.Clear();
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                for (int dx = -renderDistance; dx <= renderDistance; dx++)
                {
                    if (dx * dx + dz * dz > renderDistance * renderDistance)
                    {
                        continue;
                    }
                    for (int y = 0; y < heightInChunks; y++)
                    {
                        coords.Add(new Vector3Int(centre.x + dx, y, centre.z + dz));
                    }
                }
            }
            coords.Sort((a, b) => HorizontalDistanceSquared(a, centre).CompareTo(HorizontalDistanceSquared(b, centre)));
        }

        public static int HorizontalDistanceSquared(Vector3Int coord, Vector3Int centre)
        {
            int dx = coord.x - centre.x;
            int dz = coord.z - centre.z;
            return dx * dx + dz * dz;
        }
    }
}
