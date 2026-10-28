using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Terrain at the scale of a player (GL26, first pass of P12's multi-noise terrain): wide,
    /// gently rolling plains broken by ranges of foothills and ridged mountains up to
    /// <c>mountainHeight</c> above them. Four seeded noise fields, all in metres:
    /// <list type="bullet">
    /// <item>a warp, which bends every other field so nothing lines up with the axes;</item>
    /// <item>the <b>mask</b>, at the lowest frequency, choosing plains or mountains; the
    /// coverage sets how much of the land is mountainous, and the change between them is wide;</item>
    /// <item><b>ridges</b> (ridged fractal noise: sharp crests, rounded valleys) for the
    /// mountains, and softer <b>hills</b> where the mask is between the two;</item>
    /// <item>low <b>plains</b> noise, a few metres, so flat land isn't a table top.</item>
    /// </list>
    /// Optionally, <see cref="DistantRanges"/> (P7): great ridged ranges, kilometres tall and wide,
    /// that rise only beyond a distance from the origin, so they stand on the horizon of the far
    /// terrain while the land the player works keeps the world's height.
    /// </summary>
    public struct LandscapeHeight : IHeightField
    {
        private PerlinNoise noise;
        private float level;
        private float mountainHeight;
        private float featureSize;
        private float coverage;
        private int octaves;
        private float lacunarity;
        private float persistence;
        private DistantRanges distant;

        /// <param name="level">Height of the plains, in metres.</param>
        /// <param name="mountainHeight">Tallest the mountains rise above the plains, in metres.</param>
        /// <param name="featureSize">Width of a mountain, in metres; ranges and plains are several times wider.</param>
        /// <param name="coverage">Share of the land that is mountains, 0-1.</param>
        /// <param name="octaves">Detail on the ridges.</param>
        /// <param name="distant">Great ranges far from the origin; the default has none.</param>
        public LandscapeHeight(
            int seed, float level, float mountainHeight, float featureSize, float coverage,
            int octaves = 5, float lacunarity = 2f, float persistence = 0.5f, DistantRanges distant = default)
        {
            this.distant = distant;
            noise = new PerlinNoise(seed);
            this.level = level;
            this.mountainHeight = math.max(0f, mountainHeight);
            this.featureSize = math.max(1f, featureSize);
            this.coverage = math.saturate(coverage);
            this.octaves = math.clamp(octaves, 1, 8);
            this.lacunarity = math.max(1f, lacunarity);
            this.persistence = math.saturate(persistence);
        }

        public float Height(float x, float z)
        {
            // Bend space by up to a quarter of a feature, so ridges and coastlines wander.
            float warpScale = 1f / (featureSize * 1.5f);
            float warp = featureSize * 0.25f;
            float wx = x + warp * noise.Sample(x * warpScale + 5.2f, z * warpScale + 1.3f);
            float wz = z + warp * noise.Sample(x * warpScale + 9.7f, z * warpScale + 3.1f);

            // The mask: mostly -0.35 to 0.35, so a threshold from the coverage splits the land.
            float maskScale = 1f / (featureSize * 4f);
            float mask = Fractal(wx * maskScale + 31.7f, wz * maskScale + 11.9f, 2, 2f, 0.5f);
            float threshold = math.lerp(0.3f, -0.3f, coverage);
            // Wide changes: plains rise through foothills into the ranges.
            float mountains = math.smoothstep(threshold, threshold + 0.25f, mask);
            float hills = math.smoothstep(threshold - 0.2f, threshold + 0.05f, mask);

            float ridgeScale = 1f / featureSize;
            float ridges = Ridged(wx * ridgeScale + 71.3f, wz * ridgeScale + 47.9f, octaves);
            // Peaks vary across a range: some summits stand well above their neighbours.
            float massifScale = 1f / (featureSize * 2.5f);
            float massif = 0.45f + 0.55f * math.saturate(Fractal(wx * massifScale + 57.1f, wz * massifScale + 21.4f, 2, 2f, 0.5f) + 0.5f);
            float rolling = Fractal(wx * ridgeScale * 1.7f + 13.1f, wz * ridgeScale * 1.7f + 97.3f, 3, 2f, 0.5f);
            float plainsScale = 1f / (featureSize * 0.6f);
            float plains = Fractal(x * plainsScale + 3.3f, z * plainsScale + 7.7f, 2, 2f, 0.5f);

            return level
                + plains * math.min(3f, mountainHeight * 0.03f)
                + hills * (rolling * 0.5f + 0.5f) * mountainHeight * 0.12f
                + mountains * massif * ridges * mountainHeight
                + DistantHeight(x, z);
        }

        // The great ranges (P7): nothing within Start of the origin, rising to full height by twice
        // that, where a mask at three range widths picks which land carries them.
        private float DistantHeight(float x, float z)
        {
            if (distant.Height <= 0f)
            {
                return 0f;
            }
            float distance = math.sqrt(x * x + z * z);
            float rise = math.smoothstep(distant.Start, distant.Start * 2f, distance);
            if (rise <= 0f)
            {
                return 0f;
            }
            float scale = 1f / distant.Width;
            float mask = math.smoothstep(-0.1f, 0.25f, Fractal(x * scale / 3f + 401.3f, z * scale / 3f + 173.9f, 2, 2f, 0.5f));
            if (mask <= 0f)
            {
                return 0f;
            }
            float ridges = Ridged(x * scale + 211.7f, z * scale + 389.1f, 4);
            return rise * mask * ridges * distant.Height;
        }

        // Plain fractal noise, about -1 to 1.
        private float Fractal(float x, float z, int count, float gap, float fade)
        {
            float sum = 0f;
            float total = 0f;
            float frequency = 1f;
            float weight = 1f;
            for (int octave = 0; octave < count; octave++)
            {
                sum += weight * noise.Sample(x * frequency + octave * 17.17f, z * frequency + octave * 17.17f);
                total += weight;
                frequency *= gap;
                weight *= fade;
            }
            return sum / total;
        }

        // Ridged fractal noise, 0-1: each octave folds the noise into sharp crests (1 - |n|),
        // squared to narrow them, and later octaves are weighted by the ones before, so detail
        // gathers on the ridges and the valleys stay smooth.
        private float Ridged(float x, float z, int count)
        {
            float sum = 0f;
            float total = 0f;
            float frequency = 1f;
            float weight = 1f;
            float previous = 1f;
            for (int octave = 0; octave < count; octave++)
            {
                float ridge = 1f - math.abs(noise.Sample(x * frequency + octave * 23.3f, z * frequency + octave * 23.3f));
                ridge *= ridge;
                sum += ridge * weight * previous;
                total += weight;
                previous = math.saturate(ridge * 1.5f);
                frequency *= lacunarity;
                weight *= persistence;
            }
            // Drop the floor so valleys cut down between the crests.
            return math.saturate((sum / total - 0.15f) / 0.7f);
        }
    }

    /// <summary>
    /// Great ranges on the horizon of a <see cref="LandscapeHeight"/> (P7): ridged mountains
    /// <see cref="Height"/> tall and <see cref="Width"/> wide that start rising <see cref="Start"/>
    /// from the origin and stand at full height from twice that. A height of 0 means none.
    /// </summary>
    public struct DistantRanges
    {
        public float Height;
        public float Width;
        public float Start;

        public DistantRanges(float height, float width, float start)
        {
            Height = math.max(0f, height);
            Width = math.max(100f, width);
            Start = math.max(0f, start);
        }
    }
}
