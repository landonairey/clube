using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Counts a chunk's solid samples by material (O8), and optionally lists where the
    /// materials of interest are (the ore view's X-ray, O7). Solid means at or above the
    /// iso level: air samples keep a material id, but it never shows.
    /// </summary>
    public static class MaterialCensus
    {
        /// <param name="counts">Solid samples per material id; cleared first. At least <see cref="MaterialRegistry.MaxMaterials"/> long.</param>
        /// <param name="wanted">Material ids to list in <paramref name="found"/>, by id; null lists none.</param>
        /// <param name="found">Cleared, then the chunk-local samples whose material is wanted.</param>
        public static void Count(Chunk chunk, float isoLevel, int[] counts, bool[] wanted = null, List<Vector3Int> found = null)
        {
            System.Array.Clear(counts, 0, counts.Length);
            found?.Clear();
            Vector3Int samples = chunk.SampleCount;

            // A uniform chunk (most of a world) is all air, or all solid in one material: no need to look at each sample.
            if (chunk.IsUniform && (chunk.UniformDensity < isoLevel || chunk.Materials.IsUniform))
            {
                if (chunk.UniformDensity >= isoLevel)
                {
                    byte id = chunk.Materials.UniformId;
                    counts[id] = samples.x * samples.y * samples.z;
                    if (found != null && wanted != null && wanted[id])
                    {
                        AddAll(samples, found);
                    }
                }
                return;
            }

            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (chunk.GetDensity(sample) < isoLevel)
                        {
                            continue;
                        }
                        byte material = chunk.GetMaterial(sample);
                        counts[material]++;
                        if (found != null && wanted != null && wanted[material])
                        {
                            found.Add(sample);
                        }
                    }
                }
            }
        }

        private static void AddAll(Vector3Int samples, List<Vector3Int> found)
        {
            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        found.Add(new Vector3Int(x, y, z));
                    }
                }
            }
        }
    }
}
