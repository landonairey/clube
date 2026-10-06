namespace Clube.Core
{
    /// <summary>
    /// Deterministic random numbers from integer coordinates (O2, O4): the same seed and
    /// coordinates always give the same value, on any machine and in any load order, which
    /// <c>System.Random</c> can't promise once chunks load in a different order.
    /// </summary>
    public static class VoxelHash
    {
        /// <summary>A well-mixed 32-bit hash of the inputs.</summary>
        public static uint Hash(int seed, int x, int y, int z, int salt = 0)
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h = Mix(h ^ (uint)x * 0x85EBCA77u);
            h = Mix(h ^ (uint)y * 0xC2B2AE3Du);
            h = Mix(h ^ (uint)z * 0x27D4EB2Fu);
            h = Mix(h ^ (uint)salt * 0x165667B1u);
            return h;
        }

        /// <summary>A value in [0, 1) from the inputs.</summary>
        public static float Uniform(int seed, int x, int y, int z, int salt = 0)
        {
            // The top 24 bits fit a float's mantissa exactly.
            return (Hash(seed, x, y, z, salt) >> 8) * (1f / 16777216f);
        }

        // The murmur3 finalizer: every input bit affects every output bit.
        private static uint Mix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }
}
