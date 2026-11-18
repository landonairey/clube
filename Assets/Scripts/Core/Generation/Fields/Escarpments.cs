using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Inland cliffs (GL737) as plain values: lines where a broad noise crosses 0 wander over the land, and along stretches
    /// of them (a second noise, so runs come and go) the ground on one side stands higher by a drop that varies along the
    /// run. Across the line the rise is a few metres wide, a cliff face; on the high side the ground swells up a little
    /// towards the edge, then carries on as the land it was. Pure functions of position and seed.
    /// </summary>
    /// <remarks>
    /// Added to the map's ground after the erosion (<see cref="BoundedMapHeight"/>), so a face stays sharp, and before the
    /// water is worked out, so rivers fall over the cliffs or pool against them. Faded out near the sea and high on the
    /// mountains, which have faces of their own.
    /// </remarks>
    public struct Escarpments
    {
        public const int Salt = 0x45534341;

        public uint Seed;
        public float Spacing;
        public float RunLength;
        public float Coverage;
        public float MinDrop;
        public float MaxDrop;
        public float Face;
        public float SwellHeight;
        public float SwellWidth;
        public float SeaLevel;

        // Over the sea, metres over which the cliffs come in; over the plains, metres over which they fade out up the mountains.
        private const float CoastFadeFrom = 3f;
        private const float CoastFadeTo = 16f;
        private const float MountainFadeFrom = 50f;
        private const float MountainFadeTo = 140f;

        public bool IsCreated => Spacing > 0f && MaxDrop > 0f && Coverage > 0f;

        /// <summary>Metres the ground at (x, z) is raised, where it stood at <paramref name="height"/> with the plains at <paramref name="plains"/>.</summary>
        public float Offset(float x, float z, float height, float plains)
        {
            if (!IsCreated)
            {
                return 0f;
            }
            float fade = math.smoothstep(SeaLevel + CoastFadeFrom, SeaLevel + CoastFadeTo, height)
                         * (1f - math.smoothstep(plains + MountainFadeFrom, plains + MountainFadeTo, height));
            if (fade <= 0f)
            {
                return 0f;
            }
            float on = RunAt(x, z) * fade;
            if (on <= 0f)
            {
                return 0f;
            }
            float distance = LineDistance(x, z);
            float rise = math.smoothstep(-0.5f * Face, 0.5f * Face, distance);
            float drop = math.lerp(MinDrop, MaxDrop, Noise01(Seed + 2u, x / (RunLength * 0.8f), z / (RunLength * 0.8f)));
            float swell = SwellHeight * rise * (1f - math.smoothstep(0.5f * Face, 0.5f * Face + SwellWidth, distance));
            return (drop * rise + swell) * on;
        }

        /// <summary>How far a cliff stands at (x, z) along its run: 0 where the line is open ground, 1 where it is a full cliff.</summary>
        public float RunAt(float x, float z)
        {
            float run = Noise01(Seed + 1u, x / RunLength, z / RunLength);
            float threshold = 1f - Coverage;
            return math.smoothstep(threshold - 0.08f, threshold + 0.08f, run);
        }

        /// <summary>
        /// Metres from the nearest cliff line, above 0 on its high side: the line noise over its own gradient, which is
        /// near enough the true distance within a face and swell of the line, and only its sign matters past them.
        /// </summary>
        public float LineDistance(float x, float z)
        {
            const float step = 1f;
            float n = Line(x, z);
            float nx = Line(x + step, z);
            float nz = Line(x, z + step);
            float slope = math.length(new float2(nx - n, nz - n)) / step;
            return n / math.max(slope, 1e-6f);
        }

        private float Line(float x, float z)
        {
            return GradientNoise.Fractal(Seed, x / Spacing, z / Spacing, 2);
        }

        private static float Noise01(uint seed, float x, float z)
        {
            return math.saturate(0.5f + 0.5f * GradientNoise.Fractal(seed, x, z, 2) * 1.6f);
        }
    }
}
