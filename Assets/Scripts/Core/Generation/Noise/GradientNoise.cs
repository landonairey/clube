using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Seeded 2D gradient noise with no table (GL637): hashed gradients on a unit lattice, blended with a quintic
    /// fade, so the value and its slope are both smooth. Plain static functions, so jobs call it (K35). Used for
    /// the bounded map's broad shapes (its outline, the edge line, the climate), where
    /// <see cref="ValueNoise"/>'s blobs sit visibly on the lattice and <see cref="PerlinNoise"/>'s inline table
    /// would be carried once per field.
    /// </summary>
    public static class GradientNoise
    {
        /// <summary>The noise at (x, z) in lattice units: 0 on lattice points, mostly within -0.7 to 0.7, never past -1.6 to 1.6.</summary>
        public static float At(uint seed, float x, float z)
        {
            float floorX = math.floor(x), floorZ = math.floor(z);
            int ix = (int)floorX, iz = (int)floorZ;
            float tx = x - floorX, tz = z - floorZ;
            float u = tx * tx * tx * (tx * (tx * 6f - 15f) + 10f);
            float v = tz * tz * tz * (tz * (tz * 6f - 15f) + 10f);
            float a = Corner(seed, ix, iz, tx, tz);
            float b = Corner(seed, ix + 1, iz, tx - 1f, tz);
            float c = Corner(seed, ix, iz + 1, tx, tz - 1f);
            float d = Corner(seed, ix + 1, iz + 1, tx - 1f, tz - 1f);
            return math.lerp(math.lerp(a, b, u), math.lerp(c, d, u), v) * 1.6f;
        }

        /// <summary>
        /// Octaves of <see cref="At"/> summed, each twice as fine and <paramref name="gain"/> as strong as the last,
        /// divided by the weights' total so the range stays that of one octave.
        /// </summary>
        public static float Fractal(uint seed, float x, float z, int octaves, float gain = 0.5f)
        {
            float sum = 0f, total = 0f, weight = 1f;
            for (int octave = 0; octave < octaves; octave++)
            {
                sum += weight * At(seed + (uint)octave * 0x9E3779B9u, x + octave * 19.19f, z - octave * 7.31f);
                total += weight;
                weight *= gain;
                x *= 2f;
                z *= 2f;
            }
            return sum / total;
        }

        // A lattice point's gradient (each part -1 to 1) against the offset from it.
        private static float Corner(uint seed, int ix, int iz, float dx, float dz)
        {
            uint h = math.hash(new int3(ix, iz, (int)seed));
            float gx = (h & 0xFFFFu) * (2f / 65535f) - 1f;
            float gz = (h >> 16) * (2f / 65535f) - 1f;
            return gx * dx + gz * dz;
        }
    }
}
