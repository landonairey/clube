using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// A bounded map's land as plain values (GL589-GL591, GL637-GL640, K35): where the coast runs, where the ranges
    /// stand, where the playable edge wanders and what each side turns into past it. <see cref="Apply"/> turns the
    /// endless land's height (<see cref="LandscapeHeight"/>) into the map's; the erosion filter then cuts its gullies
    /// into the result (<see cref="BoundedMapHeight"/>). Laid out per seed by <see cref="MapLandBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Nothing here is a straight line or a circle (GL637):
    /// <list type="bullet">
    /// <item><b>The coast</b> is the zero line of a land field, in metres inland: a smooth union of round
    /// <see cref="Lands"/> (and bays cut out of them), read at a position bent by <see cref="Warp"/>, with a
    /// fractal wander on top. The land rises from it to the plains and on inland, so rivers run to the sea; the sea
    /// floor falls away from it.</item>
    /// <item><b>The ranges</b> are chains of overlapping <see cref="Ranges"/> bumps, one sum with no creases, read
    /// at the same bent position: a crest with summits and saddles, spurs branching off it. A range lifts the land
    /// field too, so one that runs out to sea makes its own peninsula or islands. The endless land's own hills ride
    /// on top, at full size by a range and lower out on the plains.</item>
    /// <item><b>The playable edge</b> is the zero line of <see cref="EdgeInside"/>: a rounded square that wanders
    /// by <see cref="EdgeWobble"/>, always inside the map's square.</item>
    /// <item><b>The sides</b> share the land by <see cref="SideWeights"/>, whose borders wander too. An ocean side
    /// keeps the coast inside the edge; any other side keeps land at it and blends its own change in
    /// (<see cref="BoundaryProfile"/>): the desert's plain and dunes, the larger range's wall, the void's cliff.</item>
    /// </list>
    /// Ground the player can reach (<see cref="LimitAt"/> below 1) is kept between <see cref="Floor"/> and
    /// <see cref="Ceiling"/>, the voxel world's own bottom and top (GL640).
    /// </remarks>
    public struct MapShape
    {
        public int Seed;
        public float Half;
        public float SeaLevel;

        /// <summary>Height of the plains by the coast (the terrain's surface level).</summary>
        public float PlainsLevel;

        public BoundaryProfile West;
        public BoundaryProfile East;
        public BoundaryProfile South;
        public BoundaryProfile North;

        /// <summary>Where the range's highest summit stands, its height over the plains and the range's half width.</summary>
        public float2 PeakCentre;
        public float PeakHeight;
        public float PeakRadius;

        /// <summary>Metres the land rises from the coast to deep inland, so it drains to the sea.</summary>
        public float InlandRise;

        /// <summary>Furthest inside the edge that any side changes the land (its blend width and wobble).</summary>
        public float Reach;

        /// <summary>The playable edge's distance from the centre along the axes, before it wanders.</summary>
        public float EdgeRadius;

        /// <summary>Metres the playable edge wanders either way.</summary>
        public float EdgeWobble;

        /// <summary>Metres the coast wanders either way from the round lands' own line.</summary>
        public float CoastWander;

        /// <summary>Depth of the open sea's floor under the sea level, in metres.</summary>
        public float OceanDepth;

        /// <summary>Lowest and highest ground the player can reach, as world heights (the voxel world's own).</summary>
        public float Floor;
        public float Ceiling;

        /// <summary>
        /// Round lands and round bites out of them (x, z, radius, kind), joined and cut in order: mainlands, the sea's
        /// reaches into them, lands and islets, bays. Above 0 a kind adds land; below 0 it takes it away.
        /// </summary>
        public FixedList512Bytes<float4> Lands;

        /// <summary>The ranges' bumps: x, z, radius, height.</summary>
        public FixedList4096Bytes<float4> Ranges;

        /// <summary>The kinds in <see cref="Lands"/>.</summary>
        public const float Mainland = 2f;
        public const float SeaReach = -2f;
        public const float Land = 1f;
        public const float Bay = -1f;

        private const uint WarpSalt = 0x57415250u;
        private const uint EdgeSalt = 0x45444745u;
        private const uint SideSalt = 0x53454354u;
        private const uint CoastSalt = 0x434F4153u;
        private const uint FloorSalt = 0x464C4F52u;
        private const uint WobbleSalt = 0x574F4242u;
        private const uint RoughSalt = 0x524F5547u;
        private const uint WallSalt = 0x57414C4Cu;

        // Metres of land field a metre of range adds: a range out at sea stands on land of its own.
        private const float RangeLand = 2.5f;

        // Share of the endless land's own hills kept out on the plains; by a range they stand a little over full size.
        private const float LowlandRelief = 0.65f;
        private const float RangeRelief = 1.15f;

        // Metres over which two lands, or a land and a side's hold on it, melt into each other.
        private const float LandBlend = 110f;
        private const float SideBlend = 80f;

        // Metres from the coast over which the beach rises to the plains.
        private const float BeachWidth = 70f;

        /// <summary>True when the shape was set up (a default one leaves the land alone).</summary>
        public bool IsCreated => Half > 0f;

        /// <summary>The map's height where the endless land's is <paramref name="landHeight"/>.</summary>
        public float Apply(float x, float z, float landHeight)
        {
            return Apply(x, z, landHeight, out _);
        }

        /// <summary>
        /// The map's height where the endless land's is <paramref name="landHeight"/>, and the level of the plains
        /// there (what the ground would be with no hills or ranges on it), which the erosion fades its gullies by.
        /// </summary>
        public float Apply(float x, float z, float landHeight, out float plains)
        {
            return Apply(x, z, landHeight, out plains, out _, out _);
        }

        /// <summary>
        /// The same, with the lowest and highest ground allowed at the point: the voxel world's own bottom and top
        /// wherever the player can reach (GL640), and free past the line they are stopped at.
        /// </summary>
        public float Apply(float x, float z, float landHeight, out float plains, out float bottom, out float top)
        {
            if (!IsCreated)
            {
                plains = PlainsLevel;
                bottom = float.NegativeInfinity;
                top = float.PositiveInfinity;
                return landHeight;
            }
            var p = new float2(x, z);
            float2 bent = Warp(p);
            float inside = EdgeInside(x, z);
            float4 weights = SideWeights(x, z);
            float range = RangeAt(bent);
            float land = HeldLand(LandAt(p, bent, range), inside, weights);

            // The coast: the beach up to the plains and the slow rise inland; the shelf down to the sea's floor.
            float ground;
            if (land >= 0f)
            {
                ground = SeaLevel + (PlainsLevel - SeaLevel) * (1f - math.exp(-land / BeachWidth))
                         + InlandRise * (1f - math.exp(-land / (Half * 0.5f)));
            }
            else
            {
                float shelf = OceanDepth * BeachWidth / math.max(0.5f, PlainsLevel - SeaLevel);
                float bed = Half * 0.1f;
                float swell = GradientNoise.Fractal(Salted(FloorSalt), x / bed, z / bed, 2);
                ground = SeaLevel - OceanDepth * (1f - math.exp(land / shelf))
                         + swell * 0.12f * OceanDepth * math.smoothstep(0f, -shelf, land);
            }
            plains = ground;

            // Hills and ranges stand on dry land only; they fade out across the shore.
            float gain = math.lerp(LowlandRelief, RangeRelief, math.saturate(range / 90f));
            // High on a crest the hills give way, so a summit stands as tall as it was planned and no taller.
            float relief = (landHeight - PlainsLevel) * gain * (1f - 0.6f * math.saturate((range - 140f) / 140f));
            float h = ground + relief * math.smoothstep(-30f, 150f, land) + range * math.smoothstep(-60f, 200f, land);

            float wobble = 0f;
            if (inside < Reach)
            {
                wobble = WobbleAt(x, z);
                float change = 0f, level = 0f;
                float rough = 0f, wall = 0f;
                bool haveNoise = false;
                for (int i = 0; i < 4; i++)
                {
                    BoundaryProfile side = Side(i);
                    if (side.Ocean || weights[i] <= 1e-4f)
                    {
                        continue;
                    }
                    float t = Strength(side, inside + wobble * side.Wobble) * weights[i];
                    if (t <= 0f)
                    {
                        continue;
                    }
                    if (!haveNoise)
                    {
                        float roughSize = Half * 0.1f, wallSize = Half * 0.8f;
                        rough = math.clamp(GradientNoise.Fractal(Salted(RoughSalt), x / roughSize, z / roughSize, 2) * 1.5f, -1f, 1f);
                        // The larger system: long winding crests where a broad noise crosses 0, valleys between them, and a
                        // slow swell so some stand taller than others.
                        float lines = GradientNoise.Fractal(Salted(WallSalt), bent.x / wallSize, bent.y / wallSize, 2);
                        float swell = GradientNoise.At(Salted(WallSalt) + 7u, x / (wallSize * 1.7f), z / (wallSize * 1.7f));
                        wall = (0.3f + 0.8f / (1f + lines * lines / 0.07f)) * (0.85f + 0.5f * swell);
                        haveNoise = true;
                    }
                    // A flattening side lays its own plain over the plains here; an adding one stands its wall on the land.
                    float added = side.EdgeHeight * math.lerp(wall, 1f, side.Flatten) + rough * side.Roughness;
                    float full = math.lerp(h + added, ground + added, side.Flatten);
                    change += t * (full - h);
                    level += t * side.Flatten * side.EdgeHeight;
                }
                h += change;
                plains += level;
            }

            // Reachable ground stays inside the voxel world; past the stop line it is free to leave it.
            float past = math.max(0f, PastLimit(inside, wobble, weights) - 10f);
            top = Ceiling + past * 2f;
            bottom = Floor - past * 4f;
            return Keep(h, bottom, top);
        }

        /// <summary>
        /// A height kept between the lowest and highest ground allowed at a point (from <see cref="Apply"/>), rounded
        /// where it meets them so no summit is cut flat. The erosion's own rise and cut are kept by it again.
        /// </summary>
        public static float Keep(float h, float bottom, float top)
        {
            if (h > top - 100f)
            {
                h = SoftMin(h, top, 12f);
            }
            if (h < bottom + 30f)
            {
                h = SoftMax(h, bottom, 3f);
            }
            return h;
        }

        /// <summary>
        /// Metres inside the playable edge (below 0 past it): a rounded square that wanders by
        /// <see cref="EdgeWobble"/>, so no side of it is a straight line (GL639). It never leaves the map's square.
        /// </summary>
        public float EdgeInside(float x, float z)
        {
            float size = Half * 0.5f;
            uint seed = Salted(EdgeSalt);
            float2 wander = EdgeWobble * math.clamp(
                1.6f * new float2(
                    GradientNoise.Fractal(seed, x / size, z / size, 2),
                    GradientNoise.Fractal(seed + 1u, x / size + 40.3f, z / size - 17.9f, 2)),
                -1f, 1f);
            float2 q = math.abs(new float2(x, z) + wander);
            q *= q;
            return EdgeRadius - math.sqrt(math.sqrt(q.x * q.x + q.y * q.y));
        }

        /// <summary>
        /// How much each side (west, east, south, north) holds a point, summing to 1: the side it lies towards holds
        /// nearly all of it, and where two meet the border between them wanders across the diagonal (GL590, GL639).
        /// </summary>
        public float4 SideWeights(float x, float z)
        {
            float size = Half * 1.1f;
            uint seed = Salted(SideSalt);
            float qx = x + Half * 0.3f * GradientNoise.Fractal(seed, x / size, z / size, 2);
            float qz = z + Half * 0.3f * GradientNoise.Fractal(seed + 1u, x / size - 23.7f, z / size + 9.1f, 2);
            float4 score = new float4(-qx, qx, -qz, qz) / (Half * 0.09f);
            float4 weights = math.exp(score - math.cmax(score));
            return weights / math.csum(weights);
        }

        /// <summary>The side (as a <see cref="MapSide"/> index) that holds most of a point.</summary>
        public int SideAt(float x, float z)
        {
            float4 weights = SideWeights(x, z);
            int best = 0;
            for (int i = 1; i < 4; i++)
            {
                best = weights[i] > weights[best] ? i : best;
            }
            return best;
        }

        /// <summary>
        /// How far the player has come into an edge's limit at a point (GL640): 0 where they walk freely, rising over
        /// the side's softness to 1 at its stop line, where they can go no further out.
        /// </summary>
        public float LimitAt(float x, float z)
        {
            if (!IsCreated)
            {
                return 0f;
            }
            float inside = EdgeInside(x, z);
            if (inside > Reach + 400f)
            {
                return 0f;
            }
            float4 weights = SideWeights(x, z);
            float softness = math.dot(weights, new float4(West.LimitSoftness, East.LimitSoftness, South.LimitSoftness, North.LimitSoftness));
            return math.saturate(1f + PastLimit(inside, WobbleAt(x, z), weights) / math.max(1f, softness));
        }

        /// <summary>
        /// Metres past the line the player is stopped at (below 0 short of it): the sides' own lines, each wandering
        /// its own way off the playable edge, weighed together as the sides share the point.
        /// </summary>
        public float PastStopAt(float x, float z)
        {
            return PastLimit(EdgeInside(x, z), WobbleAt(x, z), SideWeights(x, z));
        }

        /// <summary>
        /// The direction out of the map at a point (x, z; length 1): straight across the stop line, the way the
        /// limit holds the player back from, so a walker held at the line slides along it.
        /// </summary>
        public float2 OutwardAt(float x, float z)
        {
            const float step = 3f;
            var slope = new float2(
                PastStopAt(x + step, z) - PastStopAt(x - step, z),
                PastStopAt(x, z + step) - PastStopAt(x, z - step));
            return math.lengthsq(slope) > 1e-8f ? math.normalize(slope) : math.normalizesafe(new float2(x, z));
        }

        /// <summary>
        /// Metres inland of the coast at a point (below 0 out at sea), as the map lays its land: what
        /// <see cref="Apply"/> shapes the shore from.
        /// </summary>
        public float CoastAt(float x, float z)
        {
            var p = new float2(x, z);
            float2 bent = Warp(p);
            return HeldLand(LandAt(p, bent, RangeAt(bent)), EdgeInside(x, z), SideWeights(x, z));
        }

        /// <summary>Height of the ranges over the land at a point, before the shore fades them and erosion cuts them.</summary>
        public float RangeAt(float x, float z)
        {
            return RangeAt(Warp(new float2(x, z)));
        }

        /// <summary>How much warmer and wetter the edges make the climate at a point (GL591).</summary>
        public void EdgeClimate(float x, float z, out float temperature, out float moisture)
        {
            temperature = 0f;
            moisture = 0f;
            if (!IsCreated)
            {
                return;
            }
            float inside = EdgeInside(x, z);
            if (inside > Reach + 200f)
            {
                return;
            }
            float wobble = WobbleAt(x, z);
            float4 weights = SideWeights(x, z);
            for (int i = 0; i < 4; i++)
            {
                BoundaryProfile side = Side(i);
                // The climate turns over a wider band than the land, and gently at first, so a desert's line is the
                // climate's own wandering one and not the edge's.
                float t = 1f - math.smoothstep(0f, side.BlendWidth + 200f, inside + wobble * side.Wobble);
                t *= math.sqrt(t) * weights[i];
                temperature += t * side.TemperatureShift;
                moisture += t * side.MoistureShift;
            }
        }

        /// <summary>
        /// True where low ground joined to the edge is sea: everywhere but at and past an edge with nothing beyond it
        /// (the void past a cliff stays empty).
        /// </summary>
        public bool IsSeaSide(float x, float z)
        {
            if (!IsCreated)
            {
                return true;
            }
            float inside = EdgeInside(x, z);
            if (inside > Reach)
            {
                return true;
            }
            BoundaryProfile side = Side(SideAt(x, z));
            return !(side.Empty && inside + WobbleAt(x, z) * side.Wobble < side.BlendWidth + 20f);
        }

        /// <summary>A position bent by up to a few hundred metres, so the round lands and the ranges' lines wander.</summary>
        public float2 Warp(float2 p)
        {
            float size = Half * 0.55f;
            uint seed = Salted(WarpSalt);
            return p + Half * 0.14f * new float2(
                GradientNoise.Fractal(seed, p.x / size, p.y / size, 3),
                GradientNoise.Fractal(seed + 1u, p.x / size + 31.7f, p.y / size - 12.4f, 3));
        }

        /// <summary>A side's profile, by <see cref="MapSide"/> index.</summary>
        public BoundaryProfile Side(int index)
        {
            switch (index)
            {
                case 0:
                    return West;
                case 1:
                    return East;
                case 2:
                    return South;
                default:
                    return North;
            }
        }

        // The ranges' height at a bent position: every bump within reach, summed.
        private float RangeAt(float2 bent)
        {
            float sum = 0f;
            for (int i = 0; i < Ranges.Length; i++)
            {
                float4 bump = Ranges[i];
                float2 d = bent - bump.xy;
                float u = math.dot(d, d) / (bump.z * bump.z);
                if (u < 1f)
                {
                    float k = 1f - u;
                    sum += bump.w * k * k * k;
                }
            }
            return sum;
        }

        // Metres inland of the coast before the sides have their say: the lands joined and cut in the order they
        // were laid (mainlands, the sea's reaches into them, lands and islets, bays), the coast's own wander, and
        // the ranges' lift.
        private float LandAt(float2 p, float2 bent, float range)
        {
            float land = -Half;
            for (int i = 0; i < Lands.Length; i++)
            {
                float4 lobe = Lands[i];
                float inland = lobe.z - math.distance(bent, lobe.xy);
                land = lobe.w > 0f ? SoftMax(land, inland, LandBlend) : SoftMin(land, -inland, LandBlend);
            }
            float size = Half * 0.42f;
            return land + CoastWander * 1.8f * GradientNoise.Fractal(Salted(CoastSalt), p.x / size, p.y / size, 5, 0.62f)
                   + RangeLand * range;
        }

        // The land field once each side has held it: an ocean side lets no land nearer the edge than its blend
        // width, any other side keeps land at the edge.
        private float HeldLand(float land, float inside, float4 weights)
        {
            float held = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (weights[i] <= 1e-5f)
                {
                    continue;
                }
                BoundaryProfile side = Side(i);
                held += weights[i] * (side.Ocean
                    ? SoftMin(land, inside - side.BlendWidth, SideBlend)
                    : SoftMax(land, math.max(side.BlendWidth * 0.5f, 150f) - inside, SideBlend));
            }
            return held;
        }

        // How far a side's change has come at a distance inside the edge: 0 inland, 1 once it is complete.
        private static float Strength(in BoundaryProfile side, float inside)
        {
            float strength = 1f - math.smoothstep(-side.BlendPast, side.BlendWidth, inside);
            return strength <= 0f ? 0f : math.pow(strength, side.Sharpness);
        }

        // Metres past the stop line (below 0 short of it), the sides' own lines weighed together.
        private float PastLimit(float inside, float wobble, float4 weights)
        {
            float wander = math.dot(weights, new float4(West.Wobble, East.Wobble, South.Wobble, North.Wobble));
            float distance = math.dot(weights, new float4(West.LimitDistance, East.LimitDistance, South.LimitDistance, North.LimitDistance));
            return -(inside + wobble * wander + distance);
        }

        // -1 to 1, smooth over about a kilometre: each side's own line wanders by it.
        private float WobbleAt(float x, float z)
        {
            float size = Half * 0.36f;
            return math.clamp(GradientNoise.Fractal(Salted(WobbleSalt), x / size, z / size, 2) * 1.6f, -1f, 1f);
        }

        private uint Salted(uint salt)
        {
            return math.hash(new uint2((uint)Seed, salt));
        }

        // The larger of two values, rounded where they cross (log-sum-exp); exact once they are far apart.
        private static float SoftMax(float a, float b, float k)
        {
            float gap = math.abs(a - b);
            float larger = math.max(a, b);
            return gap > 12f * k ? larger : larger + k * math.log(1f + math.exp(-gap / k));
        }

        private static float SoftMin(float a, float b, float k)
        {
            return -SoftMax(-a, -b, k);
        }
    }
}
