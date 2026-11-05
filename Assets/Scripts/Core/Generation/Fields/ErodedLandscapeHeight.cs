using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// <see cref="LandscapeHeight"/>'s plains and ranges, with smooth massifs (<see cref="LandscapeHeight.WithSmoothMassifs"/>)
    /// in place of its ridged peaks, and an <see cref="ErosionFilter"/> on top (P12a): branching
    /// gullies and sharp ridges down the mountains, V-shaped valleys between them, and the plains left flat. The
    /// filter needs the slope, worked out from two more samples of the land a little way off. River beds are cut
    /// after the erosion, like Landscape's.
    /// </summary>
    public struct ErodedLandscapeHeight : IHeightField
    {
        // How far off the slope's extra samples are, in metres: well under the smallest ridge of the land below.
        private const float SlopeStep = 0.5f;

        // Share of the mountains' height over the plains that the gullies take to reach full depth.
        private const float FadeIn = 0.35f;

        private LandscapeHeight land;
        private ErosionFilter erosion;
        private float level;
        private float peak;
        private WaterMap water;

        /// <param name="land">The land before erosion (without water).</param>
        /// <param name="level">Height of the plains, where the fade target is -1 (valley floors).</param>
        /// <param name="mountainHeight">Height of the mountains over the plains; the fade target is 1 at 80% of it.</param>
        public ErodedLandscapeHeight(LandscapeHeight land, ErosionFilter erosion, float level, float mountainHeight)
        {
            this.land = land;
            this.erosion = erosion;
            this.level = level;
            peak = level + math.max(1f, mountainHeight) * 0.8f;
            water = WaterMap.None;
        }

        /// <summary>The same land with river beds cut into it.</summary>
        public ErodedLandscapeHeight WithWater(WaterMap map)
        {
            ErodedLandscapeHeight cut = this;
            cut.water = map;
            return cut;
        }

        public WaterMap Water => water;

        public float Height(float x, float z)
        {
            return RawHeight(x, z) - water.CarveAt(x, z);
        }

        /// <summary>The eroded land before rivers cut into it.</summary>
        public float RawHeight(float x, float z)
        {
            return Sample(x, z, out _, out _);
        }

        /// <summary>The ridge map from the biggest gullies: below 0 in their valleys, 0 on flat land (rivers start where it's low).</summary>
        public float Valley(float x, float z)
        {
            Sample(x, z, out _, out float valley);
            return valley;
        }

        /// <summary>The eroded height, and the ridge map: -1 on gully floors, 1 on ridge crests.</summary>
        public float Sample(float x, float z, out float ridge, out float valley)
        {
            float h = land.RawHeight(x, z);
            // Gullies deepen up the foothills: at full depth from the plains up, the steep foot of every range rose as a
            // cliff (low ground fades towards "valley floor", so its slopes were lifted by the whole erosion depth).
            float amount = math.smoothstep(level + 0.5f, level + (peak - level) * FadeIn, h);
            if (erosion.IsNone || amount <= 0f)
            {
                // The plains: untouched, and two samples cheaper.
                ridge = 0f;
                valley = 0f;
                return h;
            }
            float hx = land.RawHeight(x + SlopeStep, z);
            float hz = land.RawHeight(x, z + SlopeStep);
            var slope = new float2(hx - h, hz - h) / SlopeStep;
            float fade = math.saturate((h - level) / (peak - level)) * 2f - 1f;
            return h + erosion.Offset(new float2(x, z), slope, fade, amount, out ridge, out valley);
        }
    }
}
