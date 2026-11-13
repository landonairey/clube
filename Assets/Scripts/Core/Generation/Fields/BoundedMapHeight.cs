using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// A bounded map's ground (GL591, GL637): <see cref="LandscapeHeight"/>'s plains and smooth massifs, shaped by the map
    /// (<see cref="MapShape.Apply"/>: its coast, its ranges and each side's edge), then eroded like
    /// <see cref="ErodedLandscapeHeight"/> so the ranges and the coast's hills get their gullies, then cut by the rivers.
    /// </summary>
    /// <remarks>
    /// The erosion step is <see cref="ErodedLandscapeHeight.Sample"/>'s, over the shaped land instead of the bare one;
    /// with a shape that isn't set up the two fields agree (<c>BoundedMapTests</c> compares them).
    /// </remarks>
    public struct BoundedMapHeight : IHeightField
    {
        private const float SlopeStep = 0.5f;
        private const float FadeIn = 0.35f;

        private LandscapeHeight land;
        private ErosionFilter erosion;
        private MapShape shape;
        private float level;
        private float peak;
        private WaterMap water;

        /// <param name="land">The endless land before the map shapes it (without water).</param>
        /// <param name="level">Height of the plains.</param>
        /// <param name="mountainHeight">Height of the mountains over the plains, as the erosion's fade takes it.</param>
        public BoundedMapHeight(LandscapeHeight land, ErosionFilter erosion, MapShape shape, float level, float mountainHeight)
        {
            this.land = land;
            this.erosion = erosion;
            this.shape = shape;
            this.level = level;
            peak = level + math.max(1f, mountainHeight) * 0.8f;
            water = WaterMap.None;
        }

        /// <summary>The same land with river beds cut into it.</summary>
        public BoundedMapHeight WithWater(WaterMap map)
        {
            BoundedMapHeight cut = this;
            cut.water = map;
            return cut;
        }

        public WaterMap Water => water;

        public MapShape Shape => shape;

        public float Height(float x, float z)
        {
            return RawHeight(x, z) - water.CarveAt(x, z);
        }

        /// <summary>The ground before rivers cut into it.</summary>
        public float RawHeight(float x, float z)
        {
            return Sample(x, z, out _);
        }

        /// <summary>The ridge map from the biggest gullies: below 0 in their valleys, 0 on flat land (rivers start where it's low).</summary>
        public float Valley(float x, float z)
        {
            Sample(x, z, out float valley);
            return valley;
        }

        private float Shaped(float x, float z, out float plains, out float bottom, out float top)
        {
            float h = land.RawHeight(x, z);
            if (!shape.IsCreated)
            {
                plains = level;
                bottom = float.NegativeInfinity;
                top = float.PositiveInfinity;
                return h;
            }
            return shape.Apply(x, z, h, out plains, out bottom, out top);
        }

        private float Sample(float x, float z, out float valley)
        {
            // The plains' own level here comes with the height: the map lifts them inland and drops them to the sea's
            // floor, and the gullies fade towards it wherever it stands.
            float h = Shaped(x, z, out float plains, out float bottom, out float top);
            float amount = math.smoothstep(plains + 0.5f, plains + (peak - level) * FadeIn, h);
            if (erosion.IsNone || amount <= 0f)
            {
                valley = 0f;
                return h;
            }
            float hx = Shaped(x + SlopeStep, z, out _, out _, out _);
            float hz = Shaped(x, z + SlopeStep, out _, out _, out _);
            var slope = new float2(hx - h, hz - h) / SlopeStep;
            float fade = math.saturate((h - plains) / (peak - level)) * 2f - 1f;
            // The erosion lifts crests a little: reachable ground stays inside the voxel world after it too (GL640).
            return MapShape.Keep(h + erosion.Offset(new float2(x, z), slope, fade, amount, out _, out valley), bottom, top);
        }
    }
}
