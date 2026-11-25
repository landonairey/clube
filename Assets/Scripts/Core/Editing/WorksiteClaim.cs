using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Claiming a stump or a boulder before it can be worked (prototype, backlog 2026-11-24 item 8). A claim is written
    /// into the terrain itself, as making a Flat Rock is (GL88), so it is saved with the chunks: a claimed stump keeps its
    /// wood and gets a row of twine (bark with loops of twine in its texture) round its middle; a claimed boulder gets a few thin cracks down
    /// one side and a gently lighter worked patch in the middle of its top (backlog 2026-11-24b items 6 and 13). Whether a piece is claimed is read back from those materials.
    /// </summary>
    public static class WorksiteClaim
    {
        /// <summary>How far round the aimed sample a boulder's piece is looked at, in samples (2 m at 0.25 m voxels).</summary>
        public const int BoulderRadius = 8;

        // The largest piece looked at, a safety stop for the search.
        private const int MaxSamples = 8192;

        private static readonly Vector3Int[] Sides =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        private static readonly Vector3Int[] Across = { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };

        private static readonly Queue<Vector3Int> Frontier = new Queue<Vector3Int>();
        private static readonly HashSet<Vector3Int> Seen = new HashSet<Vector3Int>();

        /// <summary>True when any sample of the piece has one of the claim materials.</summary>
        public static bool IsClaimed(World world, IEnumerable<Vector3Int> piece, byte mark, byte otherMark)
        {
            foreach (Vector3Int sample in piece)
            {
                byte? id = world.GetMaterial(sample);
                if (id.HasValue && (id.Value == mark || id.Value == otherMark))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Claims a stump, given its samples (backlog 2026-11-24b item 13): the wood is left as it is, and the outside samples
        /// of one row, halfway up from the ground to the lowest column top, take the twine material, whose texture is bark
        /// with thin loops of twine round it. One row only: the terrain blends a sample's material over its neighbours, so
        /// a row is already a band about half a metre tall, most of a stump's height.
        /// </summary>
        public static void ClaimStump(World world, IReadOnlyCollection<Vector3Int> piece, float iso, byte twine)
        {
            // Each column's top, and the lowest sample open to the air (where the stump leaves the ground).
            var tops = new Dictionary<Vector2Int, int>();
            int ground = int.MaxValue;
            foreach (Vector3Int sample in piece)
            {
                var column = new Vector2Int(sample.x, sample.z);
                tops[column] = tops.TryGetValue(column, out int top) ? Mathf.Max(top, sample.y) : sample.y;
                if (sample.y < ground && IsExposed(world, sample, iso))
                {
                    ground = sample.y;
                }
            }
            if (tops.Count == 0 || ground == int.MaxValue)
            {
                return;
            }
            // The loops stay under the lowest column top, so none hangs past a slanted top.
            int lowestTop = int.MaxValue;
            foreach (int top in tops.Values)
            {
                lowestTop = Mathf.Min(lowestTop, top);
            }
            int row = Mathf.Min(lowestTop, ground + Mathf.Max(1, (lowestTop - ground) / 2));
            foreach (Vector3Int sample in piece)
            {
                if (sample.y == row && IsOpenAcross(world, sample, iso))
                {
                    world.SetMaterial(sample, twine);
                }
            }
        }

        /// <summary>
        /// Fills <paramref name="piece"/> with the solid samples joined to <paramref name="start"/> whose material
        /// <paramref name="isRock"/> accepts, no further than <see cref="BoulderRadius"/> from it in any direction.
        /// </summary>
        public static void FindBoulder(World world, Vector3Int start, float iso, Func<byte, bool> isRock, List<Vector3Int> piece)
        {
            piece.Clear();
            Seen.Clear();
            Frontier.Clear();
            if (!IsRock(world, start, iso, isRock))
            {
                return;
            }
            Seen.Add(start);
            Frontier.Enqueue(start);
            while (Frontier.Count > 0 && piece.Count < MaxSamples)
            {
                Vector3Int sample = Frontier.Dequeue();
                piece.Add(sample);
                foreach (Vector3Int step in Sides)
                {
                    Vector3Int next = sample + step;
                    Vector3Int offset = next - start;
                    if (Mathf.Abs(offset.x) > BoulderRadius || Mathf.Abs(offset.y) > BoulderRadius || Mathf.Abs(offset.z) > BoulderRadius)
                    {
                        continue;
                    }
                    if (Seen.Add(next) && IsRock(world, next, iso, isRock))
                    {
                        Frontier.Enqueue(next);
                    }
                }
            }
        }

        /// <summary>
        /// Claims a boulder, given its samples (backlog 2026-11-24b item 6): only the middle of its top becomes worked stone,
        /// a gently lighter working surface, and a few thin cracks run down the side facing <paramref name="toward"/> (x, z),
        /// after the boulder sketch in the hand-axe quest (<c>PlaceSketch.Boulder</c>): one crack from the top edge that
        /// forks halfway down, and two short ledges beside it. The crack material is stone with a hairline in it, so a
        /// crack sample reads as a slight crack, not a dark band.
        /// </summary>
        public static void ClaimBoulder(World world, List<Vector3Int> piece, float iso, byte crack, byte worked, Vector2 toward = default)
        {
            // The outside samples: the top (open to the sky) and the sides.
            var tops = new List<Vector3Int>();
            var sides = new List<Vector3Int>();
            Vector2 centre = Vector2.zero;
            int highest = int.MinValue, lowest = int.MaxValue;
            foreach (Vector3Int sample in piece)
            {
                if (!IsExposed(world, sample, iso))
                {
                    continue;
                }
                (IsOpen(world, sample + Vector3Int.up, iso) ? tops : sides).Add(sample);
                centre += new Vector2(sample.x, sample.z);
                highest = Mathf.Max(highest, sample.y);
                lowest = Mathf.Min(lowest, sample.y);
            }
            int outside = tops.Count + sides.Count;
            if (outside == 0)
            {
                return;
            }
            centre /= outside;
            float reach = 0f;
            foreach (Vector3Int sample in tops)
            {
                reach = Mathf.Max(reach, Vector2.Distance(new Vector2(sample.x, sample.z), centre));
            }
            foreach (Vector3Int sample in sides)
            {
                reach = Mathf.Max(reach, Vector2.Distance(new Vector2(sample.x, sample.z), centre));
            }

            // The worked top: the highest top samples near the middle, never the edges.
            float middle = Mathf.Max(1f, reach * WorkedShare);
            bool anyWorked = false;
            foreach (Vector3Int sample in tops)
            {
                if (sample.y >= highest - 2 && Vector2.Distance(new Vector2(sample.x, sample.z), centre) <= middle)
                {
                    world.SetMaterial(sample, worked);
                    anyWorked = true;
                }
            }
            if (!anyWorked && tops.Count > 0)
            {
                // A boulder with no top sample near its middle: the highest one.
                Vector3Int best = tops[0];
                foreach (Vector3Int sample in tops)
                {
                    if (sample.y > best.y)
                    {
                        best = sample;
                    }
                }
                world.SetMaterial(best, worked);
            }

            // The cracks, on the side facing the claimer: down the sides and over the top's edge, never across the worked
            // middle. On a rounded boulder most of the upper shell is open to the sky, so the top edge carries the crack's start.
            var outer = new List<Vector3Int>(sides);
            foreach (Vector3Int sample in tops)
            {
                if (world.GetMaterial(sample) != worked)
                {
                    outer.Add(sample);
                }
            }
            float facing = toward.sqrMagnitude > 0f ? Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg : 0f;
            float span = Mathf.Max(1f, highest - lowest);
            Vector3Int nearest = outer.Count > 0 ? outer[0] : default;
            float nearestGap = float.PositiveInfinity;
            bool anyCrack = false;
            foreach (Vector3Int sample in outer)
            {
                Vector2 flat = new Vector2(sample.x, sample.z) - centre;
                float radius = flat.magnitude;
                float angle = Mathf.DeltaAngle(facing, Mathf.Atan2(flat.y, flat.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float height = (sample.y - lowest) / span;
                if (IsCrack(angle, height, radius, span))
                {
                    world.SetMaterial(sample, crack);
                    anyCrack = true;
                }
                float gap = Mathf.Abs(angle) + Mathf.Abs(height - 0.6f);
                if (gap < nearestGap)
                {
                    nearestGap = gap;
                    nearest = sample;
                }
            }
            if (!anyCrack && outer.Count > 0)
            {
                // A small, half-buried boulder whose few side samples miss the lines: one crack facing the claimer.
                world.SetMaterial(nearest, crack);
            }
        }

        // How far out from the middle of a boulder's top the worked stone reaches, as a share of the boulder's half-width.
        private const float WorkedShare = 0.35f;

        // Half a crack's width, in samples.
        private const float CrackHalfWidth = 0.75f;

        // The cracks running down, as (height from, angle at it, height to, angle at it): height 0 at the ground and 1 at the
        // top, angle in radians round the boulder from the side facing the claimer. After the sketch: a crack from the top
        // edge to halfway, forking into a longer and a shorter branch.
        private static readonly Vector4[] CrackLines =
        {
            new Vector4(1.0f, -0.05f, 0.62f, 0.05f),
            new Vector4(0.62f, 0.05f, 0.3f, -0.1f),
            new Vector4(0.62f, 0.05f, 0.42f, 0.4f),
        };

        // The ledges running across, as (height, angle from, angle to): two short ones to either side of the crack.
        private static readonly Vector3[] Ledges =
        {
            new Vector3(0.55f, -0.8f, -0.4f),
            new Vector3(0.35f, 0.75f, 1.1f),
        };

        /// <summary>
        /// Whether a side sample lies on one of the crack lines or ledges: within about half a sample of one, measured round
        /// the boulder (<paramref name="angle"/>, radians from the side facing the claimer, times <paramref name="radius"/>)
        /// or up it (<paramref name="height"/>, 0 at the ground and 1 at the top, <paramref name="span"/> samples apart).
        /// </summary>
        public static bool IsCrack(float angle, float height, float radius, float span)
        {
            float round = Mathf.Max(radius, 1f);
            foreach (Vector4 line in CrackLines)
            {
                if (height > line.x + 0.04f || height < line.z - 0.04f)
                {
                    continue;
                }
                float along = Mathf.Clamp01((line.x - height) / (line.x - line.z));
                if (Mathf.Abs(angle - Mathf.Lerp(line.y, line.w, along)) * round <= CrackHalfWidth)
                {
                    return true;
                }
            }
            foreach (Vector3 ledge in Ledges)
            {
                if (angle >= ledge.y && angle <= ledge.z && Mathf.Abs(height - ledge.x) * span <= 0.5f)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsRock(World world, Vector3Int sample, float iso, Func<byte, bool> isRock)
        {
            float? density = world.GetDensity(sample);
            byte? id = world.GetMaterial(sample);
            return density.HasValue && density.Value >= iso && id.HasValue && isRock(id.Value);
        }

        private static bool IsOpen(World world, Vector3Int sample, float iso)
        {
            float? density = world.GetDensity(sample);
            return !density.HasValue || density.Value < iso;
        }

        private static bool IsExposed(World world, Vector3Int sample, float iso)
        {
            foreach (Vector3Int step in Sides)
            {
                if (IsOpen(world, sample + step, iso))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsOpenAcross(World world, Vector3Int sample, float iso)
        {
            foreach (Vector3Int step in Across)
            {
                if (IsOpen(world, sample + step, iso))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
