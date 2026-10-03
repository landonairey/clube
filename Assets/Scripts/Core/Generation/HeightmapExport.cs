using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Reads a chunk's surface back out as a heightmap (K29): for each column of
    /// samples, the height of the topmost place the density crosses the iso level.
    /// Overhangs and caves below that are lost, since a heightmap holds one height
    /// per column. Pairs with <see cref="HeightmapGenerator"/> for round trips.
    /// </summary>
    public static class HeightmapExport
    {
        /// <summary>
        /// One height per sample column (x fastest, then z), in world units above the
        /// chunk's base: 0 for a column that is empty all the way down, the chunk's full
        /// height for one that is solid all the way up.
        /// </summary>
        public static float[] ColumnSurfaceHeights(Chunk chunk, float isoLevel, float voxelSize)
        {
            Vector3Int samples = chunk.SampleCount;
            var heights = new float[samples.x * samples.z];
            for (int z = 0; z < samples.z; z++)
            {
                for (int x = 0; x < samples.x; x++)
                {
                    heights[x + samples.x * z] = TopCrossing(chunk, x, z, isoLevel) * voxelSize;
                }
            }
            return heights;
        }

        /// <summary>
        /// Heights scaled to bytes for a grayscale image: 0 at the chunk's base, 255 at
        /// its top. Import with surface level = amplitude = half the chunk height to
        /// get the same surface back.
        /// </summary>
        public static byte[] ToGrayscale(float[] heights, float chunkHeight)
        {
            var bytes = new byte[heights.Length];
            for (int i = 0; i < heights.Length; i++)
            {
                bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(heights[i] / chunkHeight) * 255f);
            }
            return bytes;
        }

        // Walks down from the top until the density first reaches the iso level, then
        // interpolates where between the two samples it crossed, in sample units.
        private static float TopCrossing(Chunk chunk, int x, int z, float isoLevel)
        {
            int top = chunk.SampleCount.y - 1;
            float above = chunk.GetDensity(new Vector3Int(x, top, z));
            if (above >= isoLevel)
            {
                return top;
            }

            for (int y = top - 1; y >= 0; y--)
            {
                float below = chunk.GetDensity(new Vector3Int(x, y, z));
                if (below >= isoLevel)
                {
                    return y + Mathf.InverseLerp(below, above, isoLevel);
                }
                above = below;
            }
            return 0f;
        }
    }
}
