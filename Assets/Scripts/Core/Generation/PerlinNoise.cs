using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Seeded gradient noise (Ken Perlin's "improved noise", 2002) in 2D and 3D.
    /// The same seed always gives the same field (K10, P4), unlike Unity's
    /// <c>Mathf.PerlinNoise</c>, which is 2D only and has no seed.
    /// </summary>
    /// <remarks>
    /// Values lie roughly in -1 to 1 and are exactly 0 on integer lattice points,
    /// so sample at non-integer coordinates (a frequency that isn't a whole
    /// number does that).
    /// </remarks>
    public sealed class PerlinNoise
    {
        private const int Size = 256;

        // The permutation repeated twice, so lookups like p[p[x] + y] never wrap.
        private readonly int[] permutation = new int[Size * 2];

        public PerlinNoise(int seed)
        {
            var order = new int[Size];
            for (int i = 0; i < Size; i++)
            {
                order[i] = i;
            }

            // Fisher-Yates shuffle with a seeded generator.
            var random = new System.Random(seed);
            for (int i = Size - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            for (int i = 0; i < permutation.Length; i++)
            {
                permutation[i] = order[i % Size];
            }
        }

        /// <summary>2D noise: the 3D field sampled on the plane z = 0.</summary>
        public float Sample(float x, float y)
        {
            return Sample(x, y, 0f);
        }

        public float Sample(float x, float y, float z)
        {
            int xi = Mathf.FloorToInt(x) & (Size - 1);
            int yi = Mathf.FloorToInt(y) & (Size - 1);
            int zi = Mathf.FloorToInt(z) & (Size - 1);
            x -= Mathf.Floor(x);
            y -= Mathf.Floor(y);
            z -= Mathf.Floor(z);

            float u = Fade(x);
            float v = Fade(y);
            float w = Fade(z);

            int[] p = permutation;
            int a = p[xi] + yi;
            int aa = p[a] + zi;
            int ab = p[a + 1] + zi;
            int b = p[xi + 1] + yi;
            int ba = p[b] + zi;
            int bb = p[b + 1] + zi;

            return Lerp(w,
                Lerp(v,
                    Lerp(u, Gradient(p[aa], x, y, z), Gradient(p[ba], x - 1, y, z)),
                    Lerp(u, Gradient(p[ab], x, y - 1, z), Gradient(p[bb], x - 1, y - 1, z))),
                Lerp(v,
                    Lerp(u, Gradient(p[aa + 1], x, y, z - 1), Gradient(p[ba + 1], x - 1, y, z - 1)),
                    Lerp(u, Gradient(p[ab + 1], x, y - 1, z - 1), Gradient(p[bb + 1], x - 1, y - 1, z - 1))));
        }

        // 6t^5 - 15t^4 + 10t^3: smooth, with zero first and second derivatives at 0 and 1.
        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float Lerp(float t, float a, float b)
        {
            return a + t * (b - a);
        }

        // Dot product with one of 12 edge-direction gradients, picked by the hash.
        private static float Gradient(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}
