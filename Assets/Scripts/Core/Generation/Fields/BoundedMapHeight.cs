using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// A bounded map's ground (GL591, GL637): <see cref="LandscapeHeight"/>'s plains and smooth massifs, shaped by the map
    /// (<see cref="MapShape.Apply"/>: its coast, its ranges and each side's edge), then eroded like
    /// <see cref="ErodedLandscapeHeight"/> so the ranges and the coast's hills get their gullies, then stepped by the
    /// inland cliffs (<see cref="Escarpments"/>, GL737), raised into tablelands round the dry lands' water
    /// (<see cref="Canyons"/>) and cut by the rivers.
    /// </summary>
    /// <remarks>
    /// The erosion step is <see cref="ErodedLandscapeHeight.Sample"/>'s, over the shaped land instead of the bare one;
    /// with a shape that isn't set up the two fields agree (<c>BoundedMapTests</c> compares them). The water is worked out
    /// on the land with its cliffs and without its canyons, which are cut after it.
    /// </remarks>
    public struct BoundedMapHeight : IHeightField
    {
        // GL804: metres the slope that steers the erosion's gullies is taken over. The map's shaping has creases (where a
        // range or the coast's rise levels off) where the slope's direction turns at once; the gullies follow the slope's
        // direction so closely that over half a metre each crease became a step a metre high: spiky lines across the land
        // and rings round lakes. Over a few metres the turn is spread out and the ground stays smooth.
        public const float SlopeStep = 4f;
        private const float FadeIn = 0.35f;

        private LandscapeHeight land;
        private ErosionFilter erosion;
        private MapShape shape;
        private float level;
        private float peak;
        private WaterMap water;
        private Escarpments escarpments;
        private Canyons canyons;

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
            escarpments = default;
            canyons = Canyons.None;
        }

        /// <summary>The same land with river beds cut into it.</summary>
        public BoundedMapHeight WithWater(WaterMap map)
        {
            BoundedMapHeight cut = this;
            cut.water = map;
            return cut;
        }

        /// <summary>The same land with inland cliffs on it (GL737).</summary>
        public BoundedMapHeight WithEscarpments(Escarpments cliffs)
        {
            BoundedMapHeight stepped = this;
            stepped.escarpments = cliffs;
            return stepped;
        }

        /// <summary>The same land raised into tablelands round the dry lands' water (GL737); needs the water's reach grid.</summary>
        public BoundedMapHeight WithCanyons(Canyons cut)
        {
            BoundedMapHeight raised = this;
            raised.canyons = cut;
            return raised;
        }

        public WaterMap Water => water;

        public MapShape Shape => shape;

        public Escarpments Escarpments => escarpments;

        public Canyons Canyons => canyons;

        public float Height(float x, float z)
        {
            return RawHeight(x, z) - water.CarveAt(x, z);
        }

        /// <summary>The ground before rivers cut into it.</summary>
        public float RawHeight(float x, float z)
        {
            float h = Sample(x, z, out _, out float bottom, out float top);
            if (canyons.IsCreated)
            {
                float lift = canyons.Lift(x, z, BiomeField.MoistureAt(shape, canyons.RegionSize, x, z));
                if (lift > 0f)
                {
                    h = MapShape.Keep(h + lift, bottom, top);
                }
            }
            return h;
        }

        /// <summary>The ridge map from the biggest gullies: below 0 in their valleys, 0 on flat land (rivers start where it's low).</summary>
        public float Valley(float x, float z)
        {
            Sample(x, z, out float valley, out _, out _);
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

        private float Sample(float x, float z, out float valley, out float bottom, out float top)
        {
            // The plains' own level here comes with the height: the map lifts them inland and drops them to the sea's
            // floor, and the gullies fade towards it wherever it stands.
            float h = Shaped(x, z, out float plains, out bottom, out top);
            float amount = math.smoothstep(plains + 0.5f, plains + (peak - level) * FadeIn, h);
            if (erosion.IsNone || amount <= 0f)
            {
                valley = 0f;
            }
            else
            {
                float hx = Shaped(x + SlopeStep, z, out _, out _, out _);
                float hz = Shaped(x, z + SlopeStep, out _, out _, out _);
                var slope = new float2(hx - h, hz - h) / SlopeStep;
                float fade = math.saturate((h - plains) / (peak - level)) * 2f - 1f;
                // The erosion lifts crests a little: reachable ground stays inside the voxel world after it too (GL640).
                h = MapShape.Keep(h + erosion.Offset(new float2(x, z), slope, fade, amount, out _, out valley), bottom, top);
            }
            // The inland cliffs after the erosion, so their faces stay sharp (GL737).
            if (escarpments.IsCreated && shape.IsCreated)
            {
                float step = escarpments.Offset(x, z, h, plains);
                if (step > 0f)
                {
                    h = MapShape.Keep(h + step, bottom, top);
                }
            }
            return h;
        }
    }
}
