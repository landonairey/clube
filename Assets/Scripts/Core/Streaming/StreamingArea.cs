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

        /// <summary>
        /// Every chunk of a fixed area (labs): the columns in <paramref name="columns"/> (x, z), each
        /// with <paramref name="heightInChunks"/> layers, nearest <paramref name="centre"/> first and,
        /// within a column, bottom layer first.
        /// </summary>
        public static void CollectFixed(RectInt columns, Vector3Int centre, int heightInChunks, List<Vector3Int> coords)
        {
            coords.Clear();
            for (int z = columns.yMin; z < columns.yMax; z++)
            {
                for (int x = columns.xMin; x < columns.xMax; x++)
                {
                    for (int y = 0; y < heightInChunks; y++)
                    {
                        coords.Add(new Vector3Int(x, y, z));
                    }
                }
            }
            coords.Sort((a, b) =>
            {
                int byDistance = HorizontalDistanceSquared(a, centre).CompareTo(HorizontalDistanceSquared(b, centre));
                return byDistance != 0 ? byDistance : a.y.CompareTo(b.y);
            });
        }

        /// <summary>
        /// The faces of a fixed area's chunk that lie on the area's outside (its four sides and the
        /// world's bottom): what a sealed fixed world meshes as air.
        /// </summary>
        public static MeshSeal FixedSeal(RectInt columns, Vector3Int coord)
        {
            MeshSeal seal = coord.y == 0 ? MeshSeal.MinY : MeshSeal.None;
            if (coord.x == columns.xMin)
            {
                seal |= MeshSeal.MinX;
            }
            if (coord.x == columns.xMax - 1)
            {
                seal |= MeshSeal.MaxX;
            }
            if (coord.z == columns.yMin)
            {
                seal |= MeshSeal.MinZ;
            }
            if (coord.z == columns.yMax - 1)
            {
                seal |= MeshSeal.MaxZ;
            }
            return seal;
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
