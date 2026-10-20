using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Finds a flat working top chiselled onto rock (GL88, first pass of WS1's large stone): a
    /// level patch of square, solid samples of a host material (the surface rocks), each open to the
    /// air above, holding at least a <see cref="MinSide"/> × <see cref="MinSide"/> square. Plain
    /// data over the world; the game turns what it finds into Flat Rock.
    /// </summary>
    public static class FlatTop
    {
        /// <summary>The smallest square of top samples that counts as a working surface.</summary>
        public const int MinSide = 3;

        private static readonly HashSet<Vector3Int> Candidates = new HashSet<Vector3Int>();
        private static readonly Queue<Vector3Int> Frontier = new Queue<Vector3Int>();

        /// <summary>
        /// Looks around a box of samples (a chisel hit, widened by <paramref name="margin"/> across and
        /// two samples up and down) for a flat top. Fills <paramref name="top"/> with the whole level
        /// patch joined to the first qualifying square found, and returns true; false when there's none.
        /// </summary>
        /// <param name="isHost">Which materials a top can be cut into, by id.</param>
        public static bool Find(World world, Vector3Int min, Vector3Int max, int margin, float isoLevel, Func<byte, bool> isHost, List<Vector3Int> top)
        {
            top.Clear();
            Candidates.Clear();
            for (int z = min.z - margin; z <= max.z + margin; z++)
            {
                for (int y = min.y - 2; y <= max.y + 2; y++)
                {
                    for (int x = min.x - margin; x <= max.x + margin; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (IsTop(world, sample, isoLevel, isHost))
                        {
                            Candidates.Add(sample);
                        }
                    }
                }
            }

            foreach (Vector3Int corner in Candidates)
            {
                if (!HoldsSquare(corner))
                {
                    continue;
                }
                // The whole level patch joined to it, side to side.
                Frontier.Clear();
                Frontier.Enqueue(corner);
                var seen = new HashSet<Vector3Int> { corner };
                while (Frontier.Count > 0)
                {
                    Vector3Int sample = Frontier.Dequeue();
                    top.Add(sample);
                    foreach (Vector3Int step in new[] { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) })
                    {
                        Vector3Int next = sample + step;
                        if (Candidates.Contains(next) && seen.Add(next))
                        {
                            Frontier.Enqueue(next);
                        }
                    }
                }
                return true;
            }
            return false;
        }

        // A square, solid host sample with air straight above it.
        private static bool IsTop(World world, Vector3Int sample, float isoLevel, Func<byte, bool> isHost)
        {
            float? density = world.GetDensity(sample);
            float? above = world.GetDensity(sample + Vector3Int.up);
            byte? material = world.GetMaterial(sample);
            return density.HasValue && density.Value >= isoLevel
                && above.HasValue && above.Value < isoLevel
                && material.HasValue && isHost(material.Value)
                && world.IsSquare(sample);
        }

        // MinSide × MinSide top samples from this corner on, at its level.
        private static bool HoldsSquare(Vector3Int corner)
        {
            for (int z = 0; z < MinSide; z++)
            {
                for (int x = 0; x < MinSide; x++)
                {
                    if (!Candidates.Contains(corner + new Vector3Int(x, 0, z)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
