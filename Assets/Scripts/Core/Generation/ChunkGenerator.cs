using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Fills a chunk from a terrain generator (K9), one density per sample, through
    /// <see cref="Chunk.SetDensity"/> (A7). Samples are taken at their world position,
    /// so chunks placed side by side get matching densities along shared borders.
    /// </summary>
    public static class ChunkGenerator
    {
        /// <param name="chunkOrigin">World position of the chunk's sample (0,0,0).</param>
        public static void Fill(Chunk chunk, ITerrainGenerator generator, Vector3 chunkOrigin, float voxelSize)
        {
            Vector3Int samples = chunk.SampleCount;
            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        chunk.SetDensity(sample, generator.Density(chunkOrigin + (Vector3)sample * voxelSize));
                    }
                }
            }
        }
    }
}
