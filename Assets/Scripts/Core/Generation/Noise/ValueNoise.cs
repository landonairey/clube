using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Smoothly blended hashed values on a unit lattice, 0-1: cheap, broad noise for laying out areas
    /// across the land (forests, GL164; wheat fields, GL234). Seeded, so the same seed gives the same land.
    /// </summary>
    public static class ValueNoise
    {
        /// <summary>The noise at (x, z) in lattice units; <paramref name="layer"/> and <paramref name="salt"/> keep separate uses apart.</summary>
        public static float At(int seed, float x, float z, int layer, int salt)
        {
            int ix = Mathf.FloorToInt(x);
            int iz = Mathf.FloorToInt(z);
            float fx = Fade(x - ix);
            float fz = Fade(z - iz);
            float a = VoxelHash.Uniform(seed, ix, layer, iz, salt);
            float b = VoxelHash.Uniform(seed, ix + 1, layer, iz, salt);
            float c = VoxelHash.Uniform(seed, ix, layer, iz + 1, salt);
            float d = VoxelHash.Uniform(seed, ix + 1, layer, iz + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }

        private static float Fade(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
