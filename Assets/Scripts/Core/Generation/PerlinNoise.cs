using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Seeded gradient noise (Ken Perlin's "improved noise", 2002) in 2D and 3D.
    /// The same seed always gives the same field (K10, P4), unlike Unity's
    /// <c>Mathf.PerlinNoise</c>, which is 2D only and has no seed.
    /// </summary>
    /// <remarks>
    /// <para>Values lie roughly in -1 to 1 and are exactly 0 on integer lattice points,
    /// so sample at non-integer coordinates (a frequency that isn't a whole
    /// number does that).</para>
    /// <para>A struct whose permutation table is stored inline (256 bytes), so it can be
    /// copied into Burst jobs (K35) and needs no disposing. The classic implementation
    /// doubles the table to avoid wrapping; masking indices with 255 reads the same values.</para>
    /// </remarks>
    public struct PerlinNoise
    {
        private const int Size = 256;
        private const int Mask = Size - 1;

        private FixedList512Bytes<byte> permutation;

        public PerlinNoise(int seed)
        {
            var order = new byte[Size];
            for (int i = 0; i < Size; i++)
            {
                order[i] = (byte)i;
            }

            // Fisher-Yates shuffle with a seeded generator.
            var random = new System.Random(seed);
            for (int i = Size - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            permutation = new FixedList512Bytes<byte>();
            for (int i = 0; i < Size; i++)
            {
                permutation.Add(order[i]);
            }
        }

        /// <summary>2D noise: the 3D field sampled on the plane z = 0.</summary>
        public float Sample(float x, float y)
        {
            return Sample(x, y, 0f);
        }

        public float Sample(float x, float y, float z)
        {
            float floorX = math.floor(x);
            float floorY = math.floor(y);
            float floorZ = math.floor(z);
            int xi = (int)floorX & Mask;
            int yi = (int)floorY & Mask;
            int zi = (int)floorZ & Mask;
            x -= floorX;
            y -= floorY;
            z -= floorZ;

            float u = Fade(x);
            float v = Fade(y);
            float w = Fade(z);

            int a = P(xi) + yi;
            int aa = P(a) + zi;
            int ab = P(a + 1) + zi;
            int b = P(xi + 1) + yi;
            int ba = P(b) + zi;
            int bb = P(b + 1) + zi;

            return Lerp(w,
                Lerp(v,
                    Lerp(u, Gradient(P(aa), x, y, z), Gradient(P(ba), x - 1, y, z)),
                    Lerp(u, Gradient(P(ab), x, y - 1, z), Gradient(P(bb), x - 1, y - 1, z))),
                Lerp(v,
                    Lerp(u, Gradient(P(aa + 1), x, y, z - 1), Gradient(P(ba + 1), x - 1, y, z - 1)),
                    Lerp(u, Gradient(P(ab + 1), x, y - 1, z - 1), Gradient(P(bb + 1), x - 1, y - 1, z - 1))));
        }

        private int P(int index)
        {
            return permutation[index & Mask];
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
