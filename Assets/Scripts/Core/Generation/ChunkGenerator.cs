using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Fills a chunk from a terrain generator (K9), one density per sample, through
    /// <see cref="Chunk.SetDensity"/> (A7). With <see cref="TerrainLayers"/> each sample also
    /// gets a material by its depth below the surface (M10), and with an <see cref="OreField"/>
    /// ore nodes replace some of it (3D). Samples are taken at their world position, so chunks
    /// placed side by side get matching values along shared borders.
    /// </summary>
    public static class ChunkGenerator
    {
        // Reused between fills; generation runs on the main thread.
        private static readonly List<OreNode> Nodes = new List<OreNode>();

        /// <param name="chunkOrigin">World position of the chunk's sample (0,0,0).</param>
        /// <param name="layers">Materials by depth; null or empty leaves every material at id 0.</param>
        /// <param name="ores">Ore nodes to place; null for none.</param>
        public static void Fill(
            Chunk chunk, ITerrainGenerator generator, Vector3 chunkOrigin, float voxelSize,
            TerrainLayers layers = null, OreField ores = null)
        {
            bool withLayers = layers != null && !layers.IsEmpty;
            Vector3Int samples = chunk.SampleCount;
            if (ores != null)
            {
                var size = (Vector3)(samples - Vector3Int.one) * voxelSize;
                ores.CollectNodes(new Bounds(chunkOrigin + size * 0.5f, size), Nodes);
            }
            bool withOres = ores != null && Nodes.Count > 0;

            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        Vector3 position = chunkOrigin + (Vector3)sample * voxelSize;
                        float depth = generator.Depth(position);
                        chunk.SetDensity(sample, TerrainDensity.FromDepth(depth));
                        if (!withLayers && !withOres)
                        {
                            continue;
                        }

                        byte material = withLayers ? layers.MaterialAt(depth) : (byte)0;
                        if (withOres)
                        {
                            // The world-wide sample coordinate seeds the roll, so border copies agree.
                            Vector3Int globalSample = Vector3Int.RoundToInt(position / voxelSize);
                            material = ores.Pick(material, globalSample, position, Nodes);
                        }
                        chunk.SetMaterial(sample, material);
                    }
                }
            }
        }
    }
}
