using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Whether a chunk's mesh is sure to fit 16-bit indices (M17). Not every GPU takes 32-bit
    /// index buffers (<see cref="SystemInfo.supports32bitsIndexBuffer"/>: older OpenGL ES 2.0
    /// parts, WebGL 1), so the game's chunk size must keep every mesh at 65,535 vertices or fewer
    /// whatever the terrain. Marching cubes makes at most 5 triangles per voxel, and no shading
    /// or material mode gives a triangle more than 3 vertices of its own, so a chunk needs at
    /// most 15 vertices per voxel: 61,440 for 16³, the plan of record.
    /// </summary>
    public static class ChunkIndexBudget
    {
        /// <summary>Most vertices one voxel can add to a mesh: 5 triangles of 3 unshared vertices.</summary>
        public const int MaxVerticesPerVoxel = 15;

        /// <summary>Most vertices a 16-bit index buffer can address.</summary>
        public const int Max16BitVertices = ushort.MaxValue;

        /// <summary>The most vertices a chunk of <paramref name="voxelCount"/> can need, for any density and materials.</summary>
        public static long MaxVertices(Vector3Int voxelCount)
        {
            return (long)voxelCount.x * voxelCount.y * voxelCount.z * MaxVerticesPerVoxel;
        }

        /// <summary>True when every mesh of such a chunk fits 16-bit indices, so it renders on any GPU.</summary>
        public static bool Fits16Bit(Vector3Int voxelCount)
        {
            return MaxVertices(voxelCount) <= Max16BitVertices;
        }
    }
}
