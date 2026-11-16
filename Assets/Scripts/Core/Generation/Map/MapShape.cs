using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// A bounded map's outline as plain values (GL589-GL591, K35): the square's half size, the sea level, each side's
    /// <see cref="BoundaryProfile"/> and the great peak. <see cref="Apply"/> turns the endless land's height into the
    /// map's: the peak added, then each side's change blended in as the edge nears, and held past it forever.
    /// </summary>
    /// <remarks>
    /// Corners (GL590): each side has a strength (0 inland, 1 at its edge and beyond) and a weight growing with how far
    /// into its zone a point is. Where two sides reach a point their full changes are averaged by strength times weight,
    /// and the land takes that average by the two strengths together, so the height is continuous everywhere and a
    /// corner turns from one side's shape to the other's across the diagonal.
    /// </remarks>
    public struct MapShape
    {
        public int Seed;
        public float Half;
        public float SeaLevel;
        public BoundaryProfile West;
        public BoundaryProfile East;
        public BoundaryProfile South;
        public BoundaryProfile North;
        public float2 PeakCentre;
        public float PeakHeight;
        public float PeakRadius;

        /// <summary>Metres the land rises from an ocean edge to the far side of the map, so it drains to the sea.</summary>
        public float InlandRise;

        /// <summary>Furthest inside an edge that any side changes the land (its blend width and wobble).</summary>
        public float Reach;

        private const int WobbleSalt = 0x574F4242;
        private const int RoughSalt = 0x524F5547;
        private const float WobbleSize = 900f;
        private const float RoughSize = 160f;

        /// <summary>True when the shape was set up (a default one leaves the land alone).</summary>
        public bool IsCreated => Half > 0f;

        /// <summary>The map's height where the endless land's is <paramref name="h"/>.</summary>
        public float Apply(float x, float z, float h)
        {
            if (!IsCreated)
            {
                return h;
            }
            h += PeakAt(x, z) + InlandAt(x, z);
            if (Half - math.max(math.abs(x), math.abs(z)) > Reach)
            {
                return h;
            }
            float wobble = WobbleAt(x, z);
            float rough = 0f;
            bool haveRough = false;
            float sum = 0f, total = 0f, keep = 1f;
            for (int i = 0; i < 4; i++)
            {
                BoundaryProfile p = Side(i, x, z, out float d);
                Weigh(p, d, wobble, out float t, out float w);
                if (t <= 0f)
                {
                    continue;
                }
                if (!haveRough)
                {
                    rough = ValueNoise.At(Seed, x / RoughSize, z / RoughSize, 1, RoughSalt) * 2f - 1f;
                    haveRough = true;
                }
                float edge = p.EdgeHeight + rough * p.Roughness;
                float full = math.lerp(h + edge, SeaLevel + edge, p.Flatten);
                sum += t * w * full;
                total += t * w;
                keep *= 1f - t;
            }
            return total > 0f ? math.lerp(h, sum / total, 1f - keep) : h;
        }

        /// <summary>How much warmer and wetter the edges make the climate at a point (GL591).</summary>
        public void EdgeClimate(float x, float z, out float temperature, out float moisture)
        {
            temperature = 0f;
            moisture = 0f;
            if (!IsCreated || Half - math.max(math.abs(x), math.abs(z)) > Reach)
            {
                return;
            }
            float wobble = WobbleAt(x, z);
            for (int i = 0; i < 4; i++)
            {
                BoundaryProfile p = Side(i, x, z, out float d);
                Weigh(p, d, wobble, out float t, out _);
                temperature += t * p.TemperatureShift;
                moisture += t * p.MoistureShift;
            }
        }

        /// <summary>
        /// True where low ground joined to the edge is sea: everywhere but in the reach of a side that isn't an ocean
        /// (the void past a cliff stays empty).
        /// </summary>
        public bool IsSeaSide(float x, float z)
        {
            if (!IsCreated || Half - math.max(math.abs(x), math.abs(z)) > Reach)
            {
                return true;
            }
            float wobble = WobbleAt(x, z);
            float best = 0f;
            bool ocean = true;
            for (int i = 0; i < 4; i++)
            {
                BoundaryProfile p = Side(i, x, z, out float d);
                Weigh(p, d, wobble, out float t, out float w);
                if (t * w > best)
                {
                    best = t * w;
                    ocean = p.Ocean;
                }
            }
            return ocean;
        }

        /// <summary>
        /// The land's rise at a point from the tilt towards the sea: 0 at the nearest ocean edge, <see cref="InlandRise"/> a
        /// whole map away from it. Without it the plains have no way down and pond into wide lakes.
        /// </summary>
        public float InlandAt(float x, float z)
        {
            if (InlandRise <= 0f || !IsCreated)
            {
                return 0f;
            }
            float nearest = float.MaxValue;
            nearest = West.Ocean ? math.min(nearest, x + Half) : nearest;
            nearest = East.Ocean ? math.min(nearest, Half - x) : nearest;
            nearest = South.Ocean ? math.min(nearest, z + Half) : nearest;
            nearest = North.Ocean ? math.min(nearest, Half - z) : nearest;
            return InlandRise * math.saturate(nearest / (2f * Half));
        }

        /// <summary>The great peak's height over the land at a point: a cone with a rounded top and a smooth foot.</summary>
        public float PeakAt(float x, float z)
        {
            if (PeakHeight <= 0f || PeakRadius <= 0f)
            {
                return 0f;
            }
            float u = math.distance(new float2(x, z), PeakCentre) / PeakRadius;
            if (u >= 1f)
            {
                return 0f;
            }
            float fall = 1f - math.smoothstep(0f, 1f, u);
            return PeakHeight * fall * math.sqrt(fall);
        }

        // A side's profile and how far inside its edge a point is (negative past it).
        private BoundaryProfile Side(int index, float x, float z, out float inside)
        {
            switch (index)
            {
                case 0:
                    inside = x + Half;
                    return West;
                case 1:
                    inside = Half - x;
                    return East;
                case 2:
                    inside = z + Half;
                    return South;
                default:
                    inside = Half - z;
                    return North;
            }
        }

        // A side's strength at a point (0 inland, 1 at the edge and past it) and its weight against the other sides.
        private static void Weigh(in BoundaryProfile p, float inside, float wobble, out float strength, out float weight)
        {
            inside += wobble * p.Wobble;
            strength = 1f - math.smoothstep(0f, p.BlendWidth, inside);
            weight = 0f;
            if (strength <= 0f)
            {
                return;
            }
            strength = math.pow(strength, p.Sharpness);
            weight = math.max(p.BlendWidth - inside, 1e-3f) / p.BlendWidth;
        }

        // -1 to 1, smooth over about a kilometre: the edge lines wander by it.
        private float WobbleAt(float x, float z)
        {
            float noise = 0.7f * ValueNoise.At(Seed, x / WobbleSize, z / WobbleSize, 0, WobbleSalt)
                          + 0.3f * ValueNoise.At(Seed, x / (WobbleSize * 0.35f), z / (WobbleSize * 0.35f), 2, WobbleSalt);
            return math.clamp((noise - 0.5f) * 3f, -1f, 1f);
        }
    }
}
