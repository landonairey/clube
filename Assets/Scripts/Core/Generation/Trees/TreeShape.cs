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

        private const int TrunkSegments = 4;

        // How deep the trunk's flared foot reaches into the ground, in metres, so no gap shows on a slope.
        private const float FootDepth = 0.6f;

        // Branches spread around the trunk by the golden angle, so none line up.
        private const float GoldenAngle = 2.39996f;

        // Large branches start between these fractions of the trunk's height.
        private const float LowestBranch = 0.45f;
        private const float HighestBranch = 0.82f;

        // The crown's leaf balls are this much bigger, and spread this much further.
        private const float CrownScale = 1.3f;

        /// <summary>Adds the parts of the tree rooted at <paramref name="root"/> (on the ground) to <paramref name="parts"/>.</summary>
        /// <param name="cell">The tree's cell: with the seed, it picks every number.</param>
        public static void Build(int seed, Vector2Int cell, float3 root, TreeGeneration settings, List<TreePart> parts)
        {
            var dice = new Dice(seed, cell);
            byte wood = settings.Wood.Id;
            byte leaves = settings.Leaves.Id;
            float minRadius = settings.MinBranchRadius;
            float height = dice.Range(settings.Height.x, settings.Height.y);
            float radius = math.max(minRadius, settings.TrunkRadius * dice.Range(0.85f, 1.15f));
            var up = new float3(0f, 1f, 0f);

            // The trunk: leans up to 8% of its height, wobbles in the middle, ends at a third of its radius.
            float3 lean = Direction(dice.Range(0f, 2f * math.PI), 0f) * dice.Range(0f, 0.08f) * height;
            var trunk = new float3[TrunkSegments + 1];
            var trunkRadii = new float[TrunkSegments + 1];
            for (int i = 0; i <= TrunkSegments; i++)
            {
                float t = (float)i / TrunkSegments;
                float3 wobble = i == 0 || i == TrunkSegments
                    ? float3.zero
                    : Direction(dice.Range(0f, 2f * math.PI), 0f) * dice.Range(0f, 0.04f) * height;
                trunk[i] = root + up * (height * t) + lean * (t * t) + wobble;
                trunkRadii[i] = math.max(minRadius, radius * math.lerp(1f, 0.35f, t));
            }
            parts.Add(TreePart.Segment(root - up * FootDepth, root + up * 0.5f, radius * 1.3f, radius, wood));
            for (int i = 0; i < TrunkSegments; i++)
            {
                parts.Add(TreePart.Segment(trunk[i], trunk[i + 1], trunkRadii[i], trunkRadii[i + 1], wood));
            }

            int branches = dice.Range(settings.Branches.x, settings.Branches.y);
            float firstAzimuth = dice.Range(0f, 2f * math.PI);
            for (int b = 0; b < branches; b++)
            {
                // Spaced up the trunk; lower ones longer, as on a real tree.
                float along = math.lerp(LowestBranch, HighestBranch, (b + dice.Range(0.2f, 0.8f)) / branches);
                AlongTrunk(trunk, trunkRadii, along, out float3 start, out float startRadius);
                float azimuth = firstAzimuth + b * GoldenAngle + dice.Range(-0.35f, 0.35f);
                float length = height * dice.Range(0.32f, 0.46f)
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
                    LeafCluster(ref dice, to, settings, leaves, 3, 5, 1f, parts);
                }
                LeafCluster(ref dice, end, settings, leaves, 3, 5, 1f, parts);
            }

            LeafCluster(ref dice, trunk[TrunkSegments] + up * 0.3f, settings, leaves, 4, 6, CrownScale, parts);
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
