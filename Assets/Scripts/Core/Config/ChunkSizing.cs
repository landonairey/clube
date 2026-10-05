using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Re-picks the chunk shape when the voxel size changes, so the world keeps its size in
    /// metres (M25): terrain is generated in metres, so the same config then makes the same
    /// landscape at any number of marching voxels per metre, only sampled finer. Chunks keep
    /// their width in metres where the side limits allow, and the world keeps at least its
    /// height in metres by adding chunk layers.
    /// </summary>
    public static class ChunkSizing
    {
        /// <summary>A chunk side in voxels and a world height in chunk layers.</summary>
        public readonly struct Shape
        {
            public Shape(int side, int layers)
            {
                Side = side;
                Layers = layers;
            }

            public int Side { get; }
            public int Layers { get; }
        }

        /// <param name="side">Voxels per chunk side now.</param>
        /// <param name="voxelSize">Voxel size now, in metres.</param>
        /// <param name="layers">World height in chunk layers now.</param>
        /// <param name="newVoxelSize">The voxel size to switch to, in metres.</param>
        /// <param name="minSide">Fewest voxels per side to use; keeps coarse chunks from shrinking to a few metres.</param>
        /// <param name="maxSide">Most voxels per side to use; finer voxels make smaller chunks beyond it, so a chunk stays quick to build.</param>
        /// <param name="maxLayers">Most chunk layers allowed.</param>
        public static Shape KeepWorldSize(
            int side, float voxelSize, int layers, float newVoxelSize, int minSide, int maxSide, int maxLayers)
        {
            float chunkMetres = side * voxelSize;
            float worldMetres = chunkMetres * layers;
            int newSide = Mathf.Clamp(Mathf.RoundToInt(chunkMetres / newVoxelSize), minSide, maxSide);
            float newChunkMetres = newSide * newVoxelSize;

            // Never lower than before (the small slack absorbs float error), so tall terrain still fits.
            int newLayers = Mathf.Clamp(Mathf.CeilToInt(worldMetres / newChunkMetres - 1e-3f), 1, maxLayers);
            return new Shape(newSide, newLayers);
        }
    }
}
