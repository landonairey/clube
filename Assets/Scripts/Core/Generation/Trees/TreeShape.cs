using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Builds one tree as <see cref="TreePart"/>s (GL30): a trunk that leans and wobbles a little,
    /// tapering towards the top; several large branches climbing out of its upper half, spread
    /// around it; smaller branches off each of those; and a clump of leaf balls at every branch
    /// end and on the crown. Every number comes from hashing the seed and the tree's cell, so a
    /// tree is the same whichever chunk asks for it, and no two cells give quite the same tree.
    /// </summary>
    public static class TreeShape
    {
        /// <summary>Furthest a branch tip reaches sideways from the root, per metre of tree height.</summary>
        public const float MaxSpreadPerHeight = 0.9f;

        /// <summary>Furthest a leaf ball's centre sits from the branch end it grows on, in metres (before the crown's scale).</summary>
        public const float ClusterScatter = 0.8f;

        /// <summary>
        /// The height the leaf sizes are set for (GL166): a taller tree's leaf balls, and how far they
        /// scatter, grow with it, so every tree keeps the proportions of one this tall.
        /// </summary>
        public const float LeafReferenceHeight = 8.5f;

        /// <summary>
        /// GL720: up to this height a tree's crown (branch lengths, lean, leaf balls) grows in step with it; a taller tree's
        /// crown grows with the square root of the extra, so a tree four times as tall is tall, not a blown-up copy.
        /// </summary>
        public const float CrownReferenceHeight = 20f;

        /// <summary>GL720: full-grown trees taller than <see cref="CrownReferenceHeight"/> have thicker trunks, by this power of the ratio.</summary>
        public const float TrunkThickening = 0.75f;

        /// <summary>The height a tree's crown is sized for (GL720): its own up to <see cref="CrownReferenceHeight"/>, then growing slower.</summary>
        public static float CrownHeight(float height)
        {
            return height <= CrownReferenceHeight ? height : CrownReferenceHeight * math.sqrt(height / CrownReferenceHeight);
        }

        /// <summary>How much thicker than its kind's trunk radius a full-grown tree of <paramref name="fullHeight"/> grows (GL720).</summary>
        public static float TrunkScale(float fullHeight)
        {
            return fullHeight <= CrownReferenceHeight ? 1f : math.pow(fullHeight / CrownReferenceHeight, TrunkThickening);
        }

        // GL720: a kind's full-grown height, skewed towards the low end, so the tallest (up to four times the shortest) are few.
        private static float FullHeight(ref Dice dice, Vector2 range)
        {
            float roll = dice.Next();
            return math.lerp(range.x, range.y, roll * roll);
        }

        private const int TrunkSegments = 4;

        // How deep the trunk's flared foot reaches into the ground, in metres, so no gap shows on a slope.
        private const float FootDepth = 0.6f;

        private const int SpeciesSalt = 0x4D41504C;
        private const int MoreSpeciesSalt = 0x50494E45;

        // Branches spread around the trunk by the golden angle, so none line up.
        private const float GoldenAngle = 2.39996f;

        // Large branches start between these fractions of the trunk's height.
        private const float LowestBranch = 0.45f;
        private const float HighestBranch = 0.82f;

        // The crown's leaf balls are this much bigger, and spread this much further.
        private const float CrownScale = 1.3f;

        /// <summary>True when the cell's tree is a maple (GL150), the same roll <see cref="Build"/> makes.</summary>
        public static bool IsMaple(int seed, Vector2Int cell, TreeGeneration settings)
        {
            return KindOf(seed, cell, settings) == 1;
        }

        /// <summary>
        /// The cell's tree's kind (GL721): 0 birch, 1 maple, 2 on <see cref="TreeGeneration.MoreSpecies"/>. The rows roll first,
        /// on their own hash, each its share at the cell's temperature; the rest are birch or maple by the old roll, so worlds
        /// without rows keep their trees.
        /// </summary>
        public static int KindOf(int seed, Vector2Int cell, TreeGeneration settings)
        {
            float size = settings.CellSize;
            float x = (cell.x + 0.5f) * size, z = (cell.y + 0.5f) * size;
            List<TreeSpecies> rows = settings.MoreSpecies;
            if (rows.Count > 0)
            {
                float temperature = settings.Climate != null ? settings.Climate.TemperatureAt(x, z) : float.NaN;
                float roll = VoxelHash.Uniform(seed, cell.x, MoreSpeciesSalt, cell.y);
                float total = 0f;
                for (int i = 0; i < rows.Count; i++)
                {
                    total += rows[i] != null ? rows[i].ShareAt(temperature) : 0f;
                    if (roll < total)
                    {
                        return 2 + i;
                    }
                }
            }
            float share = settings.MapleShare;
            if (share > 0f && settings.Climate != null)
            {
                // The biome picks the kinds (GL591); never maples where the settings have none to grow.
                share = settings.Climate.MapleShareAt(x, z, share);
            }
            return VoxelHash.Uniform(seed, cell.x, SpeciesSalt, cell.y) < share ? 1 : 0;
        }

        /// <summary>
        /// GL803: the share of trees at (x, z) that are birch (kind 0), by the same rules as <see cref="KindOf"/>: what the
        /// species rows leave at the land's temperature, less the biome's maples.
        /// </summary>
        public static float BirchShareAt(float x, float z, TreeGeneration settings)
        {
            float temperature = settings.Climate != null ? settings.Climate.TemperatureAt(x, z) : float.NaN;
            float rows = 0f;
            foreach (TreeSpecies row in settings.MoreSpecies)
            {
                rows += row != null ? row.ShareAt(temperature) : 0f;
            }
            float maple = settings.MapleShare;
            if (maple > 0f && settings.Climate != null)
            {
                maple = settings.Climate.MapleShareAt(x, z, maple);
            }
            return Mathf.Clamp01(1f - rows) * (1f - maple);
        }

        /// <summary>The cell's tree's height in metres, the same rolls <see cref="Build"/> makes (its first, scaled by its age).</summary>
        public static float HeightOf(int seed, Vector2Int cell, TreeGeneration settings)
        {
            var dice = new Dice(seed, cell);
            Vector2 range = settings.Kind(KindOf(seed, cell, settings)).Height;
            return FullHeight(ref dice, range) * TreeForests.SizeOf(seed, cell, settings);
        }

        /// <summary>Adds the parts of the tree rooted at <paramref name="root"/> (on the ground) to <paramref name="parts"/>.</summary>
        /// <param name="cell">The tree's cell: with the seed, it picks every number.</param>
        public static void Build(int seed, Vector2Int cell, float3 root, TreeGeneration settings, List<TreePart> parts, int kindOverride = -1)
        {
            var dice = new Dice(seed, cell);
            // Its kind (GL150, GL721), by its own roll so the shape's numbers stay as they were.
            TreeKind kind = settings.Kind(kindOverride >= 0 ? kindOverride : KindOf(seed, cell, settings));
            byte wood = kind.Wood.Id;
            byte leaves = kind.Leaves.Id;
            byte stump = kind.Stump.Id;
            float minRadius = settings.MinBranchRadius;
            // Its age scales the whole tree (GL165), by its own roll so the shape's numbers stay as they were.
            float size = TreeForests.SizeOf(seed, cell, settings);
            // Each kind has its own full-grown height and trunk (GL166).
            float full = FullHeight(ref dice, kind.Height);
            float height = full * size;
            float radius = math.max(minRadius, kind.TrunkRadius * TrunkScale(full) * dice.Range(0.85f, 1.15f) * size);
            if (kind.Form == TreeForm.Conifer)
            {
                BuildConifer(ref dice, root, height, radius, settings, wood, leaves, stump, parts);
                return;
            }
            // The crown is sized for a shorter tree once it's very tall (GL720).
            float crown = CrownHeight(height);
            float leafScale = crown / LeafReferenceHeight;
            var up = new float3(0f, 1f, 0f);

            // The trunk: leans up to 8% of its crown's height, wobbles in the middle, ends at a third of its radius.
            float3 lean = Direction(dice.Range(0f, 2f * math.PI), 0f) * dice.Range(0f, 0.08f) * crown;
            var trunk = new float3[TrunkSegments + 1];
            var trunkRadii = new float[TrunkSegments + 1];
            for (int i = 0; i <= TrunkSegments; i++)
            {
                float t = (float)i / TrunkSegments;
                float3 wobble = i == 0 || i == TrunkSegments
                    ? float3.zero
                    : Direction(dice.Range(0f, 2f * math.PI), 0f) * dice.Range(0f, 0.04f) * crown;
                trunk[i] = root + up * (height * t) + lean * (t * t) + wobble;
                trunkRadii[i] = math.max(minRadius, radius * math.lerp(1f, 0.35f, t));
            }
            // The stump: the trunk's flared foot, in its own hard material, a little wider than the trunk above it.
            parts.Add(TreePart.Segment(root - up * FootDepth, root + up * settings.StumpHeight, radius * 1.3f, radius * 1.08f, stump));
            for (int i = 0; i < TrunkSegments; i++)
            {
                parts.Add(TreePart.Segment(trunk[i], trunk[i + 1], trunkRadii[i], trunkRadii[i + 1], wood));
            }

            // A very tall tree (GL720) has a branch more for every 8 m over the crown's reference height, so its crown isn't sparse.
            int branches = dice.Range(settings.Branches.x, settings.Branches.y) + (int)math.max(0f, (height - CrownReferenceHeight) / 8f);
            float firstAzimuth = dice.Range(0f, 2f * math.PI);
            for (int b = 0; b < branches; b++)
            {
                // Spaced up the trunk; lower ones longer, as on a real tree.
                float along = math.lerp(LowestBranch, HighestBranch, (b + dice.Range(0.2f, 0.8f)) / branches);
                AlongTrunk(trunk, trunkRadii, along, out float3 start, out float startRadius);
                float azimuth = firstAzimuth + b * GoldenAngle + dice.Range(-0.35f, 0.35f);
                float length = crown * dice.Range(0.32f, 0.46f)
                               * math.lerp(1.1f, 0.75f, (along - LowestBranch) / (HighestBranch - LowestBranch));

                // Two segments, the outer one bending upwards.
                float elevation = math.radians(dice.Range(25f, 50f));
                float3 mid = start + Direction(azimuth, elevation) * (length * 0.55f);
                float3 end = mid + Direction(azimuth + dice.Range(-0.25f, 0.25f), elevation + math.radians(dice.Range(10f, 25f))) * (length * 0.45f);
                float baseRadius = math.max(minRadius, startRadius * 0.6f);
                float midRadius = math.max(minRadius, baseRadius * 0.7f);
                float endRadius = math.max(minRadius, baseRadius * 0.45f);
                parts.Add(TreePart.Segment(start, mid, baseRadius, midRadius, wood));
                parts.Add(TreePart.Segment(mid, end, midRadius, endRadius, wood));

                // Smaller branches off its outer part, angled to either side, each ending in leaves.
                int twigs = dice.Range(2, 4);
                for (int k = 0; k < twigs; k++)
                {
                    float t = math.lerp(0.35f, 1f, (k + dice.Range(0.1f, 0.9f)) / twigs);
                    float3 from = t < 0.55f ? math.lerp(start, mid, t / 0.55f) : math.lerp(mid, end, (t - 0.55f) / 0.45f);
                    float fromRadius = math.max(minRadius, math.lerp(baseRadius, endRadius, t) * 0.7f);
                    float side = dice.Next() < 0.5f ? -1f : 1f;
                    float3 direction = Direction(azimuth + side * math.radians(dice.Range(30f, 70f)), math.radians(dice.Range(15f, 50f)));
                    float3 to = from + direction * (length * dice.Range(0.28f, 0.42f));
                    parts.Add(TreePart.Segment(from, to, fromRadius, minRadius, wood));
                    LeafCluster(ref dice, to, settings, leaves, 3, 5, leafScale, parts);
                }
                LeafCluster(ref dice, end, settings, leaves, 3, 5, leafScale, parts);
            }

            LeafCluster(ref dice, trunk[TrunkSegments] + up * (0.3f * leafScale), settings, leaves, 4, 6, CrownScale * leafScale, parts);
        }

        /// <summary>
        /// Adds a fallen tree (GL505): the stump left standing at <paramref name="root"/>, and the trunk lying on the ground beside
        /// it, in one of the four cardinal directions, with no branches or leaves.
        /// </summary>
        /// <param name="groundAt">The ground's height at (x, z), so the trunk rests on it.</param>
        public static void BuildFallen(
            int seed, Vector2Int cell, float3 root, TreeGeneration settings, System.Func<float, float, float> groundAt, List<TreePart> parts, int kindOverride = -1)
        {
            var dice = new Dice(seed, cell);
            TreeKind kind = settings.Kind(kindOverride >= 0 ? kindOverride : KindOf(seed, cell, settings));
            byte wood = kind.Wood.Id;
            byte stump = kind.Stump.Id;
            float size = TreeForests.SizeOf(seed, cell, settings);
            float full = FullHeight(ref dice, kind.Height);
            float height = full * size;
            float radius = math.max(settings.MinBranchRadius, kind.TrunkRadius * TrunkScale(full) * dice.Range(0.85f, 1.15f) * size);
            var up = new float3(0f, 1f, 0f);
            parts.Add(TreePart.Segment(root - up * FootDepth, root + up * settings.StumpHeight, radius * 1.3f, radius * 1.08f, stump));

            // Lying off one of the stump's four sides, as far as the trunk reaches without its top.
            int side = dice.Range(0, 3);
            var direction = new float3(side == 0 ? 1f : side == 1 ? -1f : 0f, 0f, side == 2 ? 1f : side == 3 ? -1f : 0f);
            float length = height * dice.Range(0.6f, 0.8f);
            float start = radius * 1.6f;
            int segments = math.max(2, (int)math.ceil(length / 2f));

            // GL823: a trunk lies straight, like the logs over rivers, not draped over every bump. It points down the land's
            // average slope (where the ground has one), along a line fitted to the ground under it, and cuts through any
            // ground that stands higher.
            direction = DownSlope(root, direction, start, length, groundAt);
            FitLine(root, direction, start, length, groundAt, out float lineStart, out float rise);

            float3 previous = float3.zero;
            float previousRadius = 0f;
            for (int i = 0; i <= segments; i++)
            {
                float along = start + length * i / segments;
                float x = root.x + direction.x * along, z = root.z + direction.z * along;
                float thickness = math.max(settings.MinBranchRadius, radius * math.lerp(0.95f, 0.45f, (float)i / segments));
                // Sunk in a little, so it rests on the ground's average with no gap on a rise.
                var point = new float3(x, lineStart + rise * (along - start) + thickness * 0.7f, z);
                if (i > 0)
                {
                    parts.Add(TreePart.Segment(previous, point, previousRadius, thickness, wood));
                }
                previous = point;
                previousRadius = thickness;
            }
        }

        /// <summary>Below this rise per metre of the land's average slope, a fallen trunk keeps its cardinal direction (GL823).</summary>
        public const float FlatSlope = 0.04f;

        /// <summary>
        /// The way a fallen trunk lies (GL823): the horizontal direction the land under it falls, from the average slope of
        /// ground samples over the area it covers; <paramref name="fallback"/> where the land is nearly flat.
        /// </summary>
        public static float3 DownSlope(float3 root, float3 fallback, float start, float length, System.Func<float, float, float> groundAt)
        {
            // The mean of finite differences over a 5 x 5 grid of the square the trunk can lie in.
            float reach = math.max(1f, start + length);
            float gx = 0f, gz = 0f;
            for (int i = -2; i <= 2; i++)
            {
                for (int j = -2; j <= 2; j++)
                {
                    float x = root.x + reach * 0.5f * i, z = root.z + reach * 0.5f * j;
                    gx += (groundAt(x + reach * 0.5f, z) - groundAt(x - reach * 0.5f, z)) / reach;
                    gz += (groundAt(x, z + reach * 0.5f) - groundAt(x, z - reach * 0.5f)) / reach;
                }
            }
            gx /= 25f;
            gz /= 25f;
            if (math.sqrt(gx * gx + gz * gz) < FlatSlope)
            {
                return fallback;
            }
            // Downhill is against the gradient.
            return math.normalize(new float3(-gx, 0f, -gz));
        }

        /// <summary>
        /// The least-squares line through the ground under a trunk lying from <paramref name="start"/> to
        /// <paramref name="start"/> + <paramref name="length"/> metres along <paramref name="direction"/> from the root:
        /// its height at <paramref name="start"/> and its rise per metre.
        /// </summary>
        public static void FitLine(
            float3 root, float3 direction, float start, float length, System.Func<float, float, float> groundAt, out float heightAtStart, out float rise)
        {
            const int Samples = 12;
            float sumT = 0f, sumH = 0f, sumTT = 0f, sumTH = 0f;
            for (int i = 0; i <= Samples; i++)
            {
                float t = length * i / Samples;
                float h = groundAt(root.x + direction.x * (start + t), root.z + direction.z * (start + t));
                sumT += t;
                sumH += h;
                sumTT += t * t;
                sumTH += t * h;
            }
            int count = Samples + 1;
            float denominator = count * sumTT - sumT * sumT;
            rise = denominator > 1e-5f ? (count * sumTH - sumT * sumH) / denominator : 0f;
            heightAtStart = (sumH - rise * sumT) / count;
        }

        // A conifer (GL721): a straight trunk tapering to the top, bare for its lowest quarter, then whorls of short branches
        // drooping a little, longest at the bottom of the crown and shortening to nothing at the tip, each with a few leaf balls
        // along it, so the crown is a narrow cone. Kept well inside the broadleaf's spread.
        private static void BuildConifer(
            ref Dice dice, float3 root, float height, float radius, TreeGeneration settings, byte wood, byte leaves, byte stump, List<TreePart> parts)
        {
            float minRadius = settings.MinBranchRadius;
            float crown = CrownHeight(height);
            var up = new float3(0f, 1f, 0f);
            float3 lean = Direction(dice.Range(0f, 2f * math.PI), 0f) * dice.Range(0f, 0.03f) * crown;
            var trunk = new float3[TrunkSegments + 1];
            var trunkRadii = new float[TrunkSegments + 1];
            for (int i = 0; i <= TrunkSegments; i++)
            {
                float t = (float)i / TrunkSegments;
                trunk[i] = root + up * (height * t) + lean * (t * t);
                trunkRadii[i] = math.max(minRadius, radius * math.lerp(1f, 0.2f, t));
            }
            parts.Add(TreePart.Segment(root - up * FootDepth, root + up * settings.StumpHeight, radius * 1.3f, radius * 1.08f, stump));
            for (int i = 0; i < TrunkSegments; i++)
            {
                parts.Add(TreePart.Segment(trunk[i], trunk[i + 1], trunkRadii[i], trunkRadii[i + 1], wood));
            }

            // The cone: widest a fifth of the crown height across at its foot.
            float baseReach = crown * 0.2f;
            float ball = math.clamp(crown * 0.045f, settings.LeafRadius.x, settings.LeafRadius.y * 2.5f);
            int whorls = math.clamp((int)(height / 2.2f), 5, 18);
            float firstAzimuth = dice.Range(0f, 2f * math.PI);
            for (int w = 0; w < whorls; w++)
            {
                // From a quarter of the way up to just under the tip.
                float along = math.lerp(0.25f, 0.96f, (w + dice.Range(0.2f, 0.8f)) / whorls);
                float taper = 1f - (along - 0.25f) / 0.75f;
                AlongTrunk(trunk, trunkRadii, along, out float3 start, out float startRadius);
                float reach = baseReach * math.max(0.12f, taper) * dice.Range(0.85f, 1.1f);
                int count = dice.Range(4, 5);
                float whorlTurn = firstAzimuth + w * GoldenAngle;
                for (int b = 0; b < count; b++)
                {
                    float azimuth = whorlTurn + b * (2f * math.PI / count) + dice.Range(-0.3f, 0.3f);
                    float3 end = start + Direction(azimuth, math.radians(dice.Range(-18f, 2f))) * reach;
                    if (reach > ball * 1.2f)
                    {
                        parts.Add(TreePart.Segment(start, end, math.max(minRadius, startRadius * 0.45f), minRadius, wood));
                    }
                    // Needles along the branch: at its end, and half way out on the longer ones.
                    float size = ball * math.lerp(0.7f, 1.15f, taper);
                    parts.Add(TreePart.Ball(end + up * (size * 0.2f), size, leaves));
                    if (reach > ball * 2.5f)
                    {
                        parts.Add(TreePart.Ball(math.lerp(start, end, 0.5f) + up * (size * 0.3f), size * 1.1f, leaves));
                    }
                }
            }
            // The tip.
            parts.Add(TreePart.Ball(trunk[TrunkSegments] + up * (ball * 0.4f), ball * 0.8f, leaves));
        }

        // A few leaf balls scattered around a branch end, mostly above and beside it.
        private static void LeafCluster(
            ref Dice dice, float3 centre, TreeGeneration settings, byte leaves, int fewest, int most, float scale, List<TreePart> parts)
        {
            int balls = dice.Range(fewest, most);
            for (int i = 0; i < balls; i++)
            {
                var offset = new float3(dice.Range(-1f, 1f), dice.Range(-0.4f, 1f), dice.Range(-1f, 1f));
                offset = math.normalizesafe(offset) * (dice.Range(0f, ClusterScatter) * scale);
                parts.Add(TreePart.Ball(centre + offset, dice.Range(settings.LeafRadius.x, settings.LeafRadius.y) * scale, leaves));
            }
        }

        // The point and radius a fraction of the way up the trunk.
        private static void AlongTrunk(float3[] trunk, float[] radii, float along, out float3 point, out float radius)
        {
            float scaled = math.saturate(along) * TrunkSegments;
            int segment = math.min((int)scaled, TrunkSegments - 1);
            float t = scaled - segment;
            point = math.lerp(trunk[segment], trunk[segment + 1], t);
            radius = math.lerp(radii[segment], radii[segment + 1], t);
        }

        // A unit vector at an angle around the vertical (azimuth) and above the horizontal (elevation).
        private static float3 Direction(float azimuth, float elevation)
        {
            float flat = math.cos(elevation);
            return new float3(math.cos(azimuth) * flat, math.sin(elevation), math.sin(azimuth) * flat);
        }

        // A stream of numbers from the seed and the cell: the same tree every time it's built.
        private struct Dice
        {
            // Keeps the tree hashes apart from the rocks' and ores'.
            private const int TreeSalt = 0x54524545;

            private readonly int seed;
            private readonly Vector2Int cell;
            private int count;

            public Dice(int seed, Vector2Int cell)
            {
                this.seed = seed;
                this.cell = cell;
                count = 0;
            }

            /// <summary>A number in [0, 1).</summary>
            public float Next()
            {
                return VoxelHash.Uniform(seed, cell.x, TreeSalt, cell.y, count++);
            }

            public float Range(float min, float max)
            {
                return math.lerp(min, max, Next());
            }

            /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
            public int Range(int min, int max)
            {
                return min + math.min((int)(Next() * (max - min + 1)), max - min);
            }
        }
    }
}
