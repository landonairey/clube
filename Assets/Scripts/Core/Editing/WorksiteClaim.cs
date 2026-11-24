using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Claiming a stump or a boulder before it can be worked (prototype, backlog 2026-11-24 item 8). A claim is written
    /// into the terrain itself, as making a Flat Rock is (GL88), so it is saved with the chunks: a claimed stump gets a
    /// flat top and two bands of twine round it, one higher and one lower; a claimed boulder gets cracks down its sides
    /// and a lighter worked stone on its top. Whether a piece is claimed is read back from those materials.
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
        /// Claims a stump, given its samples: everything over its lowest column top is cleared so the top is flat, and the
        /// outside samples of two rows, a third and two thirds of the way up from the ground, take the twine material.
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
            // The flat top: the lowest column top, but never so low that the bands have no room.
            int flat = int.MaxValue;
            foreach (int top in tops.Values)
            {
                flat = Mathf.Min(flat, top);
            }
            int highest = int.MinValue;
            foreach (int top in tops.Values)
            {
                highest = Mathf.Max(highest, top);
            }
            flat = Mathf.Clamp(Mathf.Max(flat, ground + 3), ground, highest);
            foreach (Vector3Int sample in piece)
            {
                if (sample.y > flat)
                {
                    world.SetDensity(sample, 0f);
                }
            }
            int height = flat - ground;
            int low = ground + Mathf.Max(1, height / 3);
            int high = Mathf.Max(low + 1, ground + (height * 2) / 3);
            foreach (Vector3Int sample in piece)
            {
                if ((sample.y == low || sample.y == high) && sample.y < flat && IsOpenAcross(world, sample, iso))
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
        /// Claims a boulder, given its samples: the outside samples open to the sky become worked stone (its working
        /// surface), and outside samples on two crossing sets of slanted lines down its sides become cracks.
        /// </summary>
        public static void ClaimBoulder(World world, List<Vector3Int> piece, float iso, byte crack, byte worked)
        {
            // The sides (outside, not open to the sky), and which of the crack lines' offsets crosses most of them: a
            // small, half-buried boulder has only a few side samples, and they must still show a crack.
            var sides = new List<Vector3Int>();
            var counts = new int[CrackSpacing];
            foreach (Vector3Int sample in piece)
            {
                if (!IsExposed(world, sample, iso))
                {
                    continue;
                }
                if (IsOpen(world, sample + Vector3Int.up, iso))
                {
                    world.SetMaterial(sample, worked);
                    continue;
                }
                sides.Add(sample);
                counts[Mod(sample.x + sample.y + sample.z, CrackSpacing)]++;
            }
            int offset = 0;
            for (int i = 1; i < CrackSpacing; i++)
            {
                if (counts[i] > counts[offset])
                {
                    offset = i;
                }
            }
            foreach (Vector3Int sample in sides)
            {
                if (IsCrack(sample, offset))
                {
                    world.SetMaterial(sample, crack);
                }
            }
        }

        // Samples between crack lines.
        private const int CrackSpacing = 4;

        /// <summary>
        /// Whether a side sample lies on a crack line: slanted planes <see cref="CrackSpacing"/> samples apart (a metre),
        /// shifted by <paramref name="offset"/>, and a sparser crossing set.
        /// </summary>
        public static bool IsCrack(Vector3Int sample, int offset)
        {
            return Mod(sample.x + sample.y + sample.z, CrackSpacing) == offset || Mod(sample.x - sample.z - 2 * sample.y, 13) == 0;
        }

        private static int Mod(int value, int by)
        {
            int left = value % by;
            return left < 0 ? left + by : left;
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
