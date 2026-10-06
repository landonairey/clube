using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The chunks wanted around a centre chunk (M3): every column within the render
    /// distance horizontally (a circle), across all of the world's chunk layers, nearest first.
    /// </summary>
    /// <remarks>
    /// The offsets from the centre only depend on the render distance and the layer count, so
    /// they're sorted once and kept: moving the centre is then one pass, with no sort, which
    /// matters at large render distances (thousands of chunks, every time the focus crosses a
    /// chunk border). Main thread only.
    /// </remarks>
    public static class StreamingArea
    {
        private static readonly Dictionary<(int Distance, int Layers), Vector3Int[]> Offsets =
            new Dictionary<(int, int), Vector3Int[]>();

        /// <summary>Replaces the contents of <paramref name="coords"/> with the wanted chunks, nearest first.</summary>
        public static void Collect(Vector3Int centre, int renderDistance, int heightInChunks, List<Vector3Int> coords)
        {
            Vector3Int[] offsets = OffsetsFor(renderDistance, heightInChunks);
            coords.Clear();
            if (coords.Capacity < offsets.Length)
            {
                coords.Capacity = offsets.Length;
            }
            var shift = new Vector3Int(centre.x, 0, centre.z);
            foreach (Vector3Int offset in offsets)
            {
                coords.Add(offset + shift);
            }
        }

        public static int HorizontalDistanceSquared(Vector3Int coord, Vector3Int centre)
        {
            int dx = coord.x - centre.x;
            int dz = coord.z - centre.z;
            return dx * dx + dz * dz;
        }

        // Every (dx, layer, dz) in the circle, nearest column first; within a column, bottom layer first.
        private static Vector3Int[] OffsetsFor(int renderDistance, int heightInChunks)
        {
            if (Offsets.TryGetValue((renderDistance, heightInChunks), out Vector3Int[] offsets))
            {
                return offsets;
            }

            var list = new List<Vector3Int>();
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
                        list.Add(new Vector3Int(dx, y, dz));
                    }
                }
            }

            // A stable order (distance, then position), so runs are repeatable.
            list.Sort((a, b) =>
            {
                int byDistance = (a.x * a.x + a.z * a.z).CompareTo(b.x * b.x + b.z * b.z);
                if (byDistance != 0)
                {
                    return byDistance;
                }
                int byZ = a.z.CompareTo(b.z);
                int byX = a.x.CompareTo(b.x);
                return byZ != 0 ? byZ : byX != 0 ? byX : a.y.CompareTo(b.y);
            });
            offsets = list.ToArray();
            Offsets.Add((renderDistance, heightInChunks), offsets);
            return offsets;
        }
    }
}
