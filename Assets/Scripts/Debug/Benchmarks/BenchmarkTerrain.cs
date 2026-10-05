using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>The terrain every mesher benchmark runs on, so results stay comparable (K11, K32).</summary>
    public static class BenchmarkTerrain
    {
        public static readonly int[] DefaultSizes = { 8, 16, 32, 64 };

        /// <summary>
        /// A cubic chunk of rolling hills (fractal 2D Perlin, seed 1) through its middle,
        /// scaled so the shape is the same at every size. Voxel size 1; mesh it at iso 0.5.
        /// </summary>
        public static Chunk Hills(int size)
        {
            var chunk = new Chunk(new Vector3Int(size, size, size));
            var generator = new FractalPerlin2DGenerator(1, size * 0.5f, size * 0.25f, 1.2f / size, octaves: 4);
            ChunkGenerator.Fill(chunk, generator, Vector3.zero, 1f);
            return chunk;
        }
    }
}
