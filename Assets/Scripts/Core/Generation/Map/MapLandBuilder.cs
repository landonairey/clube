using System.Collections.Generic;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Lays out a bounded map's land for a seed (GL637-GL639): the round lands, islets and bays of its
    /// <see cref="MapShapeType"/>, and its mountain ranges, written into the <see cref="MapShape"/> the height field
    /// reads. Everything here is a plan of points and sizes; the shape bends and roughens them into coast and crest.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Each side that isn't sea gets a mainland: a broad front of land reaching into the
    /// map as far as the outline says.</item>
    /// <item>The outline's lands are scattered, or strung along arms: a chain of islands across the sea, or
    /// peninsulas reaching out from a mainland.</item>
    /// <item>The main range comes in from a side whose kind has a range (GL639: it cuts across the map and goes on
    /// into the larger system past the edge), or else runs along the chain or across the home land. Its crest has
    /// one highest summit (GL600's tall mountain, <see cref="MapShape.PeakCentre"/>), lower summits and saddles, and
    /// spurs branching off both sides; further ranges fork off it or stand on other islands.</item>
    /// </list>
    /// A range is a chain of overlapping bumps whose sum is the crest; the bumps' heights are fitted so the sum
    /// at each one is the height planned there.
    /// </remarks>
    public static class MapLandBuilder
    {
        // Spacing of a chain's bumps, as a share of their radius: close enough that their sum has no dips between.
        private const float Spacing = 0.55f;

        // A summit is at most this share of its range's half width, so its flanks stay under about 40 degrees.
        private const float Steepest = 0.5f;

        private const int MostBumps = 250;
        private const int MostLands = 31;

        /// <summary>Fills <paramref name="shape"/>'s lands, ranges and summit; its size, sides and edge must be set.</summary>
        /// <param name="sides">The kind of edge on each side, by <see cref="MapSide"/>.</param>
        /// <param name="peakHeight">Height of the main range's highest summit over the plains.</param>
        /// <param name="rangeRadius">Half the width of the main range at its foot.</param>
        public static void Build(ref MapShape shape, MapShapeType type, MapBoundaryType[] sides, float peakHeight, float rangeRadius)
        {
            var random = new Random(math.hash(new int2(shape.Seed, 0x4C414E44)) | 1u);
            float half = shape.Half, edge = shape.EdgeRadius;

            // Mainlands, one per side that isn't sea: a broad front reaching in from that side, centred far out so
            // its rim crosses the whole side as one gentle arc.
            var lands = new List<float4>();
            var landSides = new List<int>();
            var reachOf = new float[4];
            float2 anchor = float2.zero;
            for (int i = 0; i < 4; i++)
            {
                if (sides[i].IsOcean)
                {
                    continue;
                }
                float2 normal = Normal(i), along = Perpendicular(normal);
                reachOf[i] = math.lerp(type.Mainland.x, type.Mainland.y, random.NextFloat());
                lands.Add(new float4(normal * half * 3f + along * half * random.NextFloat(-0.6f, 0.6f), half * (2f + 2f * reachOf[i]), MapShape.Mainland));
                landSides.Add(i);
                anchor += normal;
            }
            int mainlands = lands.Count;

            // The sea's own reach into a map that has a mainland, so the coast is where land and sea meet, well
            // inside the map, and not a line kept just off the edge: from a sea side facing a mainland, as far as
            // that mainland's front; from one facing another sea, a lesser bite.
            for (int i = 0; i < 4 && mainlands > 0; i++)
            {
                if (!sides[i].IsOcean)
                {
                    continue;
                }
                float2 normal = Normal(i), along = Perpendicular(normal);
                int facing = i ^ 1;
                float reach = sides[facing].IsOcean ? random.NextFloat(0.16f, 0.4f) : 1f - reachOf[facing];
                lands.Add(new float4(normal * half * 3f + along * half * random.NextFloat(-0.6f, 0.6f), half * (2f + 2f * reach), MapShape.SeaReach));
            }
            int firstOwn = lands.Count;
            float2 seaward = math.lengthsq(anchor) > 0.01f ? -math.normalize(anchor) : float2.zero;

            // The outline's own lands: strung along arms, or scattered.
            int count = random.NextInt(type.Lands.x, type.Lands.y + 1);
            int arms = random.NextInt(type.Arms.x, type.Arms.y + 1);
            var paths = new List<Path>();
            if (arms > 0)
            {
                float turn = random.NextFloat(0f, 2f * math.PI);
                for (int a = 0; a < arms; a++)
                {
                    Path path = default;
                    if (landSides.Count > 0)
                    {
                        // A peninsula: from a mainland's edge out across the map.
                        float2 normal = Normal(landSides[a % landSides.Count]), along = Perpendicular(normal);
                        float lateral = (arms == 1 ? 0f : math.lerp(-0.6f, 0.6f, (a + 0.5f) / arms)) + random.NextFloat(-0.15f, 0.15f);
                        path.From = normal * edge * 0.9f + along * edge * lateral;
                        path.To = -normal * edge * random.NextFloat(0.2f, 0.65f) + along * edge * (lateral * 0.5f + random.NextFloat(-0.35f, 0.35f));
                    }
                    else
                    {
                        // A chain: from one part of the sea across to the far one.
                        float angle = turn + a * math.PI / arms;
                        path.From = Direction(angle) * edge * type.Spread;
                        path.To = Direction(angle + math.PI + random.NextFloat(-0.5f, 0.5f)) * edge * type.Spread;
                    }
                    path.Bend = (path.From + path.To) * 0.5f + Perpendicular(path.To - path.From) * random.NextFloat(-0.3f, 0.3f);
                    paths.Add(path);
                    int onArm = count / arms + (a < count % arms ? 1 : 0);
                    float length = path.Length(), walked = 0f, lastRadius = 0f;
                    for (int j = 0; j < onArm && lands.Count < MostLands; j++)
                    {
                        float radius = half * math.lerp(type.LandRadius.x, type.LandRadius.y, random.NextFloat());
                        float2 position;
                        if (landSides.Count > 0)
                        {
                            // A peninsula's lands overlap into one finger, narrowing towards its tip.
                            float u = math.lerp(0.15f, 1f, (j + 0.5f) / onArm);
                            radius *= math.lerp(1.15f, 0.75f, u);
                            position = path.At(u);
                        }
                        else
                        {
                            // A chain's islands stand apart, with water between them.
                            walked += j == 0 ? radius * 0.4f : (lastRadius + radius) * 1.12f;
                            if (walked > length)
                            {
                                break;
                            }
                            position = path.AtLength(walked / length);
                            lastRadius = radius;
                        }
                        lands.Add(new float4(position + random.NextFloat2Direction() * edge * 0.04f, radius, MapShape.Land));
                    }
                }
            }
            else
            {
                float2 centre = seaward * edge * 0.4f;
                float scatter = edge * type.Spread;
                bool apart = type.Spread > 0.5f;
                for (int j = 0; j < count && lands.Count < MostLands; j++)
                {
                    float radius = half * math.lerp(type.LandRadius.x, type.LandRadius.y, random.NextFloat());
                    if (j == 0 && landSides.Count == 0)
                    {
                        // The home land: the largest, where the capital and the main range stand.
                        radius = half * type.LandRadius.y * (apart ? 1.3f : 1f);
                    }
                    float2 position = centre;
                    for (int attempt = 0; attempt < 40; attempt++)
                    {
                        position = centre + random.NextFloat2(-1f, 1f) * scatter;
                        if (Norm(position) + radius * 0.6f > edge)
                        {
                            continue;
                        }
                        bool clear = true;
                        for (int other = firstOwn; apart && clear && other < lands.Count; other++)
                        {
                            clear = math.distance(position, lands[other].xy) >= 1.15f * (radius + lands[other].z);
                        }
                        if (clear)
                        {
                            break;
                        }
                    }
                    lands.Add(new float4(position, radius, MapShape.Land));
                }
            }
            int ownLands = lands.Count;

            // Islets, clear of the lands.
            int islets = random.NextInt(type.Islets.x, type.Islets.y + 1);
            for (int j = 0; j < islets && lands.Count < MostLands; j++)
            {
                float radius = half * math.lerp(type.IsletRadius.x, type.IsletRadius.y, random.NextFloat());
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    float2 position = random.NextFloat2(-1f, 1f) * edge * 0.92f;
                    bool clear = Norm(position) + radius <= edge * 0.95f;
                    for (int other = firstOwn; clear && other < ownLands; other++)
                    {
                        clear = math.distance(position, lands[other].xy) >= lands[other].z + radius + half * 0.05f;
                    }
                    if (clear)
                    {
                        lands.Add(new float4(position, radius, MapShape.Land));
                        break;
                    }
                }
            }


            // Bays, cut into an island's coast or opening off the sea's reach into a mainland: two round bites side
            // by side, so none is a circle.
            int bays = random.NextInt(type.Bays.x, type.Bays.y + 1);
            for (int j = 0; j < bays && lands.Count + 1 < MostLands && ownLands > mainlands; j++)
            {
                bool offTheSea = firstOwn > mainlands && (ownLands == firstOwn || random.NextBool());
                int at = offTheSea ? random.NextInt(mainlands, firstOwn) : random.NextInt(firstOwn, ownLands);
                float4 land = lands[at];
                float2 towards = Rotate(
                    math.lengthsq(land.xy) > half * half ? math.normalizesafe(-land.xy) : random.NextFloat2Direction(),
                    random.NextFloat(-0.6f, 0.6f));
                float radius = math.min(land.z * random.NextFloat(0.3f, 0.5f), half * random.NextFloat(0.14f, 0.26f));
                float2 centre = land.xy + towards * land.z * random.NextFloat(0.85f, 1.02f);
                lands.Add(new float4(centre, radius, MapShape.Bay));
                lands.Add(new float4(centre + random.NextFloat2Direction() * radius * 0.9f, radius * random.NextFloat(0.55f, 0.8f), MapShape.Bay));
            }

            shape.Lands.Clear();
            foreach (float4 land in lands)
            {
                shape.Lands.Add(land);
            }

            // The home land: the largest of the outline's own, where a map with no mainland has its main range.
            int home = -1;
            for (int i = firstOwn; i < ownLands; i++)
            {
                home = home < 0 || lands[i].z > lands[home].z ? i : home;
            }

            BuildRanges(ref shape, type, sides, ref random, lands, mainlands, firstOwn, ownLands, home, paths, seaward, peakHeight, rangeRadius);
        }

        private static void BuildRanges(
            ref MapShape shape, MapShapeType type, MapBoundaryType[] sides, ref Random random, List<float4> lands, int mainlands, int firstOwn,
            int ownLands, int home, List<Path> paths, float2 seaward, float peakHeight, float rangeRadius)
        {
            float half = shape.Half, edge = shape.EdgeRadius;
            var bumps = new List<float4>();
            var wanted = new List<float>();
            shape.Ranges.Clear();
            shape.PeakCentre = float2.zero;
            shape.PeakHeight = 0f;
            shape.PeakRadius = rangeRadius;
            if (peakHeight <= 0f || rangeRadius <= 0f)
            {
                return;
            }

            var rangeSides = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                if (sides[i].HasRange)
                {
                    rangeSides.Add(i);
                }
            }

            // The main range's line.
            Path main = default;
            bool fromOpen = true, toOpen = true;
            float radius = rangeRadius;
            if (rangeSides.Count >= 2)
            {
                main.From = Beyond(rangeSides[0], half, ref random);
                main.To = Beyond(rangeSides[1], half, ref random);
                fromOpen = toOpen = false;
            }
            else if (rangeSides.Count == 1)
            {
                float2 normal = Normal(rangeSides[0]);
                main.From = Beyond(rangeSides[0], half, ref random);
                main.To = -normal * edge * random.NextFloat(0.05f, 0.55f) + Perpendicular(normal) * edge * random.NextFloat(-0.45f, 0.45f);
                fromOpen = false;
            }
            else if (mainlands == 0 && paths.Count > 0)
            {
                main.From = paths[0].At(0.06f);
                main.To = paths[0].At(0.94f);
                main.Bend = paths[0].Bend;
                radius = math.min(radius, half * type.LandRadius.y * 0.6f);
            }
            else if (mainlands == 0 && home >= 0)
            {
                float2 along = random.NextFloat2Direction();
                main.From = lands[home].xy - along * lands[home].z * 0.75f;
                main.To = lands[home].xy + along * lands[home].z * 0.75f;
                radius = math.min(radius, lands[home].z * 0.55f);
            }
            else
            {
                float2 centre = -seaward * edge * 0.15f + random.NextFloat2(-1f, 1f) * edge * 0.2f;
                float2 along = random.NextFloat2Direction();
                float reach = edge * random.NextFloat(0.55f, 0.8f);
                main.From = centre - along * reach;
                main.To = centre + along * reach;
            }
            if (!(mainlands == 0 && paths.Count > 0 && rangeSides.Count == 0))
            {
                main.Bend = (main.From + main.To) * 0.5f + Perpendicular(main.To - main.From) * random.NextFloat(-0.38f, 0.38f);
            }
            // The summit is as tall as asked, as its range is wide enough to carry, and as the voxel world has room for.
            float room = shape.Ceiling - shape.PlainsLevel - shape.InlandRise - 30f;
            float peak = math.max(10f, math.min(math.min(peakHeight, radius * Steepest), room));
            bool alongChain = mainlands == 0 && paths.Count > 0 && rangeSides.Count == 0;

            int first = bumps.Count;
            int summit = Chain(bumps, wanted, main, radius, peak, 0.42f, fromOpen, toOpen, edge, alongChain ? lands : null, ref random);
            int last = bumps.Count;
            Spurs(bumps, wanted, first, last, radius, ref random);

            // Further ranges: forks off the main one, or the spines of other islands.
            int more = random.NextInt(type.Ranges.x, type.Ranges.y + 1);
            int island = firstOwn;
            for (int r = 0; r < more && bumps.Count < MostBumps - 30; r++)
            {
                Path path = default;
                float lesser, lesserPeak;
                bool rooted = false;
                if (mainlands == 0 && paths.Count == 0)
                {
                    // The next island big enough to carry one.
                    while (island < ownLands && (island == home || lands[island].z < half * 0.12f))
                    {
                        island++;
                    }
                    if (island >= ownLands)
                    {
                        break;
                    }
                    float2 along = random.NextFloat2Direction();
                    path.From = lands[island].xy - along * lands[island].z * 0.7f;
                    path.To = lands[island].xy + along * lands[island].z * 0.7f;
                    lesser = math.min(rangeRadius * 0.7f, lands[island].z * 0.5f);
                    lesserPeak = math.min(peakHeight * random.NextFloat(0.45f, 0.7f), lesser * Steepest);
                    island++;
                }
                else
                {
                    // A fork: off the main crest a few summits from the highest, out at a wide angle.
                    int span = last - first;
                    if (span < 7)
                    {
                        break;
                    }
                    int root = first + (r % 2 == 0 ? math.max(1, summit - first - random.NextInt(3, 6)) : math.min(span - 2, summit - first + random.NextInt(3, 6)));
                    root = math.clamp(root, first + 1, last - 2);
                    float2 tangent = math.normalizesafe(bumps[root + 1].xy - bumps[root - 1].xy);
                    float2 out2 = Rotate(tangent, (random.NextBool() ? 1f : -1f) * random.NextFloat(0.9f, 1.4f));
                    path.From = bumps[root].xy + out2 * radius * 0.4f;
                    path.To = path.From + out2 * edge * random.NextFloat(0.5f, 0.9f);
                    lesser = radius * 0.75f;
                    lesserPeak = math.min(wanted[root] * 0.85f, lesser * Steepest);
                    rooted = true;
                }
                path.Bend = (path.From + path.To) * 0.5f + Perpendicular(path.To - path.From) * random.NextFloat(-0.25f, 0.25f);
                int start = bumps.Count;
                Chain(bumps, wanted, path, lesser, lesserPeak, 0.5f, !rooted, true, edge, null, ref random);
                Spurs(bumps, wanted, start, bumps.Count, lesser, ref random);
            }

            Fit(bumps, wanted);
            foreach (float4 bump in bumps)
            {
                if (shape.Ranges.Length < MostBumps)
                {
                    shape.Ranges.Add(bump);
                }
            }
            shape.PeakRadius = radius;
            shape.PeakHeight = wanted[summit];
            shape.PeakCentre = Unbend(shape, bumps[summit].xy);
        }

        // Lays a crest's bumps along a line and plans each one's height: one highest summit (returned), lower ones
        // and saddles either side, tapering where an end stands open in the map.
        private static int Chain(
            List<float4> bumps, List<float> wanted, Path path, float radius, float peak, float crest, bool fromOpen, bool toOpen,
            float edge, List<float4> standOn, ref Random random)
        {
            float length = path.Length();
            int count = math.clamp((int)math.ceil(length / (radius * Spacing)) + 1, 3, 44);
            int first = bumps.Count;
            var rolls = new float[count + 2];
            for (int i = 0; i < rolls.Length; i++)
            {
                rolls[i] = random.NextFloat();
            }
            int summit = 0;
            float deepest = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                float2 position = path.AtLength(i / (float)(count - 1));
                bumps.Add(new float4(position, radius * random.NextFloat(0.85f, 1.15f), 0f));
                // The summit stands on the bump deepest inside the map, off the line's very ends.
                float depth = edge - Norm(position) - (i == 0 || i == count - 1 ? edge : 0f);
                if (depth > deepest)
                {
                    deepest = depth;
                    summit = i;
                }
            }
            for (int i = 0; i < count; i++)
            {
                float roll = (rolls[i] + 2f * rolls[i + 1] + rolls[i + 2]) * 0.25f;
                float away = (i - summit) * Spacing / 1.5f;
                float height = math.lerp(crest + 0.34f * (roll - 0.5f) * 2f, 1f, math.exp(-away * away));
                float open = math.min(fromOpen ? i : count, toOpen ? count - 1 - i : count);
                height *= math.lerp(0.3f, 1f, math.smoothstep(0f, 1f, (open + 0.5f) / 3.5f));
                wanted.Add(peak * math.max(0.12f, height) * Standing(standOn, bumps[first + i].xy));
            }
            return first + summit;
        }

        // How much of a crest stands at a point when it keeps to the lands given: all of it deep in one, next to
        // none at their coasts and out at sea, so a chain's islands stay islands. All of it when none are given.
        private static float Standing(List<float4> lands, float2 position)
        {
            if (lands == null)
            {
                return 1f;
            }
            float deepest = -1f;
            foreach (float4 land in lands)
            {
                if (land.w > 0f)
                {
                    deepest = math.max(deepest, 1f - math.distance(position, land.xy) / land.z);
                }
            }
            return math.lerp(0.04f, 1f, math.smoothstep(0.05f, 0.5f, deepest));
        }

        // Spurs off both sides of a crest: shorter, lower chains running down and out from it, swept a little
        // along it, every couple of half widths.
        private static void Spurs(List<float4> bumps, List<float> wanted, int first, int last, float radius, ref Random random)
        {
            float side = random.NextBool() ? 1f : -1f;
            int next = first + random.NextInt(1, 3);
            while (next < last - 1 && bumps.Count < MostBumps - 10)
            {
                float2 tangent = math.normalizesafe(bumps[next + 1].xy - bumps[next - 1].xy);
                float2 out2 = Rotate(Perpendicular(tangent) * side, random.NextFloat(-0.5f, 0.5f));
                float crest = wanted[next];
                float spur = radius * 0.58f;
                float length = radius * random.NextFloat(1.3f, 2.3f);
                int count = math.max(2, (int)(length / (spur * Spacing)));
                float sweep = random.NextFloat(-0.5f, 0.5f) / count;
                float2 position = bumps[next].xy + out2 * radius * 0.45f;
                for (int i = 0; i < count && bumps.Count < MostBumps; i++)
                {
                    float u = (i + 1f) / count;
                    bumps.Add(new float4(position, spur * math.lerp(1f, 0.78f, u), 0f));
                    wanted.Add(crest * math.lerp(0.66f, 0.14f, math.pow(u, 0.8f)));
                    out2 = Rotate(out2, sweep);
                    position += out2 * spur * Spacing;
                }
                side = random.NextFloat() < 0.8f ? -side : side;
                next += random.NextInt(3, 6);
            }
        }

        // Sets each bump's own height so the sum of all of them at its centre is the height wanted there.
        private static void Fit(List<float4> bumps, List<float> wanted)
        {
            for (int i = 0; i < bumps.Count; i++)
            {
                float4 bump = bumps[i];
                bump.w = wanted[i] * 0.5f;
                bumps[i] = bump;
            }
            for (int pass = 0; pass < 12; pass++)
            {
                for (int i = 0; i < bumps.Count; i++)
                {
                    float sum = 0f;
                    for (int j = 0; j < bumps.Count; j++)
                    {
                        float4 other = bumps[j];
                        float u = math.distancesq(bumps[i].xy, other.xy) / (other.z * other.z);
                        if (u < 1f)
                        {
                            float k = 1f - u;
                            sum += other.w * k * k * k;
                        }
                    }
                    float4 bump = bumps[i];
                    bump.w = math.max(0f, bump.w * math.clamp(wanted[i] / math.max(sum, 0.01f), 0.5f, 2f));
                    bumps[i] = bump;
                }
            }
        }

        // The world position the shape's bending carries to a point (the bend is gentle, so a few rounds settle it).
        private static float2 Unbend(in MapShape shape, float2 bent)
        {
            float2 p = bent;
            for (int i = 0; i < 6; i++)
            {
                p = bent - (shape.Warp(p) - p);
            }
            return p;
        }

        // A point well past a side's edge, somewhere along it: where a range comes in from the larger system.
        private static float2 Beyond(int side, float half, ref Random random)
        {
            float2 normal = Normal(side);
            return normal * half * 1.55f + Perpendicular(normal) * half * random.NextFloat(-0.5f, 0.5f);
        }

        private static float2 Normal(int side)
        {
            switch (side)
            {
                case 0:
                    return new float2(-1f, 0f);
                case 1:
                    return new float2(1f, 0f);
                case 2:
                    return new float2(0f, -1f);
                default:
                    return new float2(0f, 1f);
            }
        }

        private static float2 Perpendicular(float2 v)
        {
            return new float2(-v.y, v.x);
        }

        private static float2 Direction(float angle)
        {
            return new float2(math.cos(angle), math.sin(angle));
        }

        private static float2 Rotate(float2 v, float angle)
        {
            math.sincos(angle, out float sin, out float cos);
            return new float2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        // The rounded square's own measure of distance from the middle (as MapShape.EdgeInside has it).
        private static float Norm(float2 p)
        {
            p *= p;
            return math.sqrt(math.sqrt(p.x * p.x + p.y * p.y));
        }

        // A bent line from one point to another: a quadratic curve through a bend point.
        private struct Path
        {
            public float2 From;
            public float2 To;
            public float2 Bend;

            public float2 At(float u)
            {
                return math.lerp(math.lerp(From, Bend, u), math.lerp(Bend, To, u), u);
            }

            public float Length()
            {
                float length = 0f;
                float2 last = From;
                for (int i = 1; i <= 32; i++)
                {
                    float2 point = At(i / 32f);
                    length += math.distance(last, point);
                    last = point;
                }
                return length;
            }

            // The point a share of the way along by length, not by the curve's own uneven parameter.
            public float2 AtLength(float share)
            {
                float target = Length() * math.saturate(share);
                float walked = 0f;
                float2 last = From;
                for (int i = 1; i <= 64; i++)
                {
                    float2 point = At(i / 64f);
                    float step = math.distance(last, point);
                    if (walked + step >= target && step > 0f)
                    {
                        return math.lerp(last, point, (target - walked) / step);
                    }
                    walked += step;
                    last = point;
                }
                return To;
            }
        }
    }
}
