using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The noise the clouds are shaped from (GL252): tileable 3D Perlin and Worley noise, packed into the four channels
    /// of a cube of texels that repeats seamlessly in every direction. Red is Perlin-Worley (soft Perlin billows cut
    /// into cells by Worley), the cloud's broad shape; green, blue and alpha are Worley at 2, 4 and 8 times as many
    /// cells, for the billows' edges and the wisps. The <c>Clube/Clouds</c> shader samples it at a few scales.
    /// </summary>
    /// <remarks>
    /// Plain math; *Clube → Sky → Build cloud noise* bakes it into a <c>Texture3D</c> asset, so the game never builds it.
    /// </remarks>
    public static class CloudNoise
    {
        /// <summary>
        /// Perlin noise at <paramref name="point"/> (texel units of a <paramref name="period"/>-cell tile), 0-1, repeating
        /// every <paramref name="period"/> cells along each axis.
        /// </summary>
        public static float Perlin(Vector3 point, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(point.x), y0 = Mathf.FloorToInt(point.y), z0 = Mathf.FloorToInt(point.z);
            float fx = point.x - x0, fy = point.y - y0, fz = point.z - z0;
            float u = Fade(fx), v = Fade(fy), w = Fade(fz);
            float result = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                int dx = corner & 1, dy = (corner >> 1) & 1, dz = (corner >> 2) & 1;
                Vector3 gradient = Gradient(Wrap(x0 + dx, period), Wrap(y0 + dy, period), Wrap(z0 + dz, period), seed);
                float dot = gradient.x * (fx - dx) + gradient.y * (fy - dy) + gradient.z * (fz - dz);
                float weight = (dx == 1 ? u : 1f - u) * (dy == 1 ? v : 1f - v) * (dz == 1 ? w : 1f - w);
                result += dot * weight;
            }
            // Perlin's range is about -0.87..0.87; bring it to 0-1.
            return Mathf.Clamp01(result * 0.6f + 0.5f);
        }

        /// <summary>
        /// Worley (cell) noise: 1 at a cell's feature point, falling to 0 a cell's width away. One feature point per cell
        /// of a <paramref name="cells"/>-cell tile, so it repeats every <paramref name="cells"/> cells.
        /// </summary>
        public static float Worley(Vector3 point, int cells, int seed)
        {
            int x0 = Mathf.FloorToInt(point.x), y0 = Mathf.FloorToInt(point.y), z0 = Mathf.FloorToInt(point.z);
            float nearest = float.MaxValue;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int cx = x0 + dx, cy = y0 + dy, cz = z0 + dz;
                        Vector3 feature = new Vector3(cx, cy, cz) + Jitter(Wrap(cx, cells), Wrap(cy, cells), Wrap(cz, cells), seed);
                        float distance = (feature - point).sqrMagnitude;
                        if (distance < nearest)
                        {
                            nearest = distance;
                        }
                    }
                }
            }
            return 1f - Mathf.Clamp01(Mathf.Sqrt(nearest));
        }

        /// <summary>Worley in three octaves (cells, 2x, 4x), weighted 0.625, 0.25, 0.125.</summary>
        public static float WorleyOctaves(Vector3 unit, int cells, int seed)
        {
            return Worley(unit * cells, cells, seed) * 0.625f
                   + Worley(unit * (cells * 2), cells * 2, seed + 1) * 0.25f
                   + Worley(unit * (cells * 4), cells * 4, seed + 2) * 0.125f;
        }

        /// <summary>
        /// The four channels at a point of the unit tile (each coordinate 0-1 for one repeat): Perlin-Worley, and Worley
        /// octaves at 4, 8 and 16 cells.
        /// </summary>
        public static Color Sample(Vector3 unit, int seed)
        {
            // Perlin in three octaves, then cut into billows by low-frequency Worley.
            float perlin = Perlin(unit * 4f, 4, seed) * 0.5f + Perlin(unit * 8f, 8, seed + 10) * 0.3f + Perlin(unit * 16f, 16, seed + 20) * 0.2f;
            float cellsLow = WorleyOctaves(unit, 4, seed + 30);
            float perlinWorley = Remap(perlin, cellsLow - 1f, 1f, 0f, 1f);
            return new Color(
                Mathf.Clamp01(perlinWorley),
                WorleyOctaves(unit, 4, seed + 40),
                WorleyOctaves(unit, 8, seed + 50),
                WorleyOctaves(unit, 16, seed + 60));
        }

        /// <summary>
        /// A <paramref name="size"/>³ cube of RGBA bytes, x fastest then y then z, as a Texture3D takes them. Each channel
        /// is stretched so its values span 0-1 (from its 1st to its 99th percentile), which keeps the shader's cover and
        /// erosion settings meaning the same thing whatever the noise's own range.
        /// </summary>
        public static byte[] Build(int size, int seed)
        {
            int count = size * size * size;
            var channels = new float[4][];
            for (int c = 0; c < 4; c++)
            {
                channels[c] = new float[count];
            }
            int index = 0;
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Texel centres, so the tile's edges meet its other side half a texel apart, as they repeat.
                        var unit = new Vector3((x + 0.5f) / size, (y + 0.5f) / size, (z + 0.5f) / size);
                        Color sample = Sample(unit, seed);
                        channels[0][index] = sample.r;
                        channels[1][index] = sample.g;
                        channels[2][index] = sample.b;
                        channels[3][index] = sample.a;
                        index++;
                    }
                }
            }

            var bytes = new byte[count * 4];
            for (int c = 0; c < 4; c++)
            {
                float[] sorted = (float[])channels[c].Clone();
                System.Array.Sort(sorted);
                float low = sorted[count / 100];
                float high = sorted[count - 1 - count / 100];
                float span = Mathf.Max(high - low, 1e-5f);
                for (int i = 0; i < count; i++)
                {
                    bytes[i * 4 + c] = ToByte((channels[c][i] - low) / span);
                }
            }
            return bytes;
        }

        /// <summary>Maps <paramref name="value"/> from one range onto another.</summary>
        public static float Remap(float value, float fromLow, float fromHigh, float toLow, float toHigh)
        {
            return toLow + (value - fromLow) / (fromHigh - fromLow) * (toHigh - toLow);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static int Wrap(int value, int period)
        {
            int wrapped = value % period;
            return wrapped < 0 ? wrapped + period : wrapped;
        }

        private static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du;
                h = (h << 13) | (h >> 19);
                h ^= (uint)z * 0x27D4EB2Fu;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        private static float Unit(uint hash)
        {
            return (hash & 0xFFFFFF) / 16777216f;
        }

        private static Vector3 Jitter(int x, int y, int z, int seed)
        {
            return new Vector3(Unit(Hash(x, y, z, seed)), Unit(Hash(x, y, z, seed + 7919)), Unit(Hash(x, y, z, seed + 15887)));
        }

        // One of the 12 edge directions of a cube, as Perlin's improved noise uses.
        private static Vector3 Gradient(int x, int y, int z, int seed)
        {
            switch (Hash(x, y, z, seed) % 12)
            {
                case 0: return new Vector3(1, 1, 0);
                case 1: return new Vector3(-1, 1, 0);
                case 2: return new Vector3(1, -1, 0);
                case 3: return new Vector3(-1, -1, 0);
                case 4: return new Vector3(1, 0, 1);
                case 5: return new Vector3(-1, 0, 1);
                case 6: return new Vector3(1, 0, -1);
                case 7: return new Vector3(-1, 0, -1);
                case 8: return new Vector3(0, 1, 1);
                case 9: return new Vector3(0, -1, 1);
                case 10: return new Vector3(0, 1, -1);
                default: return new Vector3(0, -1, -1);
            }
        }
    }
}
