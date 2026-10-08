using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Faces of a chunk whose samples are meshed as air (labs): a curtain of empty corners
    /// around the solid ground there, so marching cubes closes the surface with a wall and the
    /// chunk, or a fixed map's edge, looks like a slice cut out of the world, its underground
    /// on show. Only the mesh changes; the chunk's data, and so edits and raycasts, don't.
    /// </summary>
    [Flags]
    public enum MeshSeal
    {
        None = 0,
        MinX = 1,
        MaxX = 2,
        MinY = 4,
        MaxY = 8,
        MinZ = 16,
        MaxZ = 32,
        All = MinX | MaxX | MinY | MaxY | MinZ | MaxZ,
    }

    /// <summary>Applies a <see cref="MeshSeal"/> to density arrays (X fastest, then Y, then Z).</summary>
    public static class MeshSeals
    {
        /// <summary>True when the sample at (x, y, z) of a chunk with <paramref name="count"/> samples lies on a sealed face.</summary>
        public static bool IsSealed(MeshSeal seal, int x, int y, int z, int3 count)
        {
            return ((seal & MeshSeal.MinX) != 0 && x == 0) || ((seal & MeshSeal.MaxX) != 0 && x == count.x - 1)
                || ((seal & MeshSeal.MinY) != 0 && y == 0) || ((seal & MeshSeal.MaxY) != 0 && y == count.y - 1)
                || ((seal & MeshSeal.MinZ) != 0 && z == 0) || ((seal & MeshSeal.MaxZ) != 0 && z == count.z - 1);
        }

        /// <summary>Sets one Z layer's sealed samples to air (density 0), as the managed mesher reads it.</summary>
        public static void ApplyToLayer(MeshSeal seal, float[] layer, int z, int3 count)
        {
            if (seal == MeshSeal.None)
            {
                return;
            }
            for (int y = 0; y < count.y; y++)
            {
                for (int x = 0; x < count.x; x++)
                {
                    if (IsSealed(seal, x, y, z, count))
                    {
                        layer[x + count.x * y] = 0f;
                    }
                }
            }
        }

        /// <summary>Sets a whole chunk's sealed samples to air, in a copy of its densities (floats or bytes).</summary>
        public static void Apply(MeshSeal seal, NativeArray<float> densities, NativeArray<byte> densityBytes, int3 count)
        {
            if (seal == MeshSeal.None)
            {
                return;
            }
            bool bytes = densityBytes.Length > 0;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        if (!IsSealed(seal, x, y, z, count))
                        {
                            continue;
                        }
                        int i = x + count.x * (y + count.y * z);
                        if (bytes)
                        {
                            densityBytes[i] = 0;
                        }
                        else
                        {
                            densities[i] = 0f;
                        }
                    }
                }
            }
        }
    }
}
