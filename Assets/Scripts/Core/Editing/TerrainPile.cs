using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Puts material back into the ground as a small pile (dropping items, I4/I6): fills
    /// air samples one at a time, each resting on something solid, nearest the drop point
    /// first, so the pile grows outwards and upwards like a mound. One sample per item, the
    /// same amount a tool removed to collect it (GL5). Writes go through the world's edit
    /// paths (A7), so border copies stay equal.
    /// </summary>
    /// <remarks>
    /// Simple stacking only: no slumping to an angle of repose yet (SM1, SM2).
    /// </remarks>
    public static class TerrainPile
    {
        private static readonly Vector3Int[] Faces =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        // Reused between calls; edits only run on the main thread.
        private static readonly List<Vector3Int> Candidates = new List<Vector3Int>();
        private static readonly HashSet<Vector3Int> Seen = new HashSet<Vector3Int>();

        /// <summary>
        /// Fills up to <paramref name="count"/> samples with <paramref name="material"/>, starting
        /// at the air sample nearest <paramref name="point"/> (relative to the world origin).
        /// Returns how many it filled, fewer when there's no supported air nearby or the
        /// samples aren't loaded.
        /// </summary>
        public static int Place(World world, Vector3 point, byte material, int count, float isoLevel)
        {
            Candidates.Clear();
            Seen.Clear();
            Vector3Int start = NearestAir(world, point, isoLevel);
            Consider(world, start, isoLevel);
            // Dropped mid-air, onto a slope or into a hole: settle onto the first support below.
            for (int down = 0; Candidates.Count == 0 && down < 64; down++)
            {
                start += Vector3Int.down;
                Consider(world, start, isoLevel);
            }

            int filled = 0;
            while (filled < count && Candidates.Count > 0)
            {
                int best = Nearest(start);
                Vector3Int sample = Candidates[best];
                Candidates.RemoveAt(best);
                if (!IsSupportedAir(world, sample, isoLevel))
                {
                    continue;
                }

                world.SetMaterial(sample, material);
                world.SetDensity(sample, 1f);
                filled++;
                foreach (Vector3Int face in Faces)
                {
                    Consider(world, sample + face, isoLevel);
                }
            }
            return filled;
        }

        // The air sample nearest the point: the point's own when it's air, else the nearest
        // within two samples (a point on a wall or slope sits just inside it), else straight
        // up out of the ground.
        private static Vector3Int NearestAir(World world, Vector3 point, float isoLevel)
        {
            Vector3Int centre = world.Grid.WorldToNearestSample(point);
            if (IsAir(world, centre, isoLevel))
            {
                return centre;
            }

            const int Radius = 2;
            Vector3Int best = centre;
            float bestDistance = float.PositiveInfinity;
            for (int z = -Radius; z <= Radius; z++)
            {
                for (int y = -Radius; y <= Radius; y++)
                {
                    for (int x = -Radius; x <= Radius; x++)
                    {
                        Vector3Int sample = centre + new Vector3Int(x, y, z);
                        float distance = (world.Grid.SampleToWorld(sample) - point).sqrMagnitude;
                        if (distance < bestDistance && IsAir(world, sample, isoLevel))
                        {
                            bestDistance = distance;
                            best = sample;
                        }
                    }
                }
            }
            if (!float.IsPositiveInfinity(bestDistance))
            {
                return best;
            }

            for (int up = 0; up < 64 && !IsAir(world, best, isoLevel); up++)
            {
                best += Vector3Int.up;
            }
            return best;
        }

        private static bool IsAir(World world, Vector3Int sample, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            return density.HasValue && density.Value < isoLevel;
        }

        // Only samples that are candidates count as seen: an unsupported one can become a
        // candidate later, once the sample under it is filled.
        private static void Consider(World world, Vector3Int sample, float isoLevel)
        {
            if (!Seen.Contains(sample) && IsSupportedAir(world, sample, isoLevel))
            {
                Seen.Add(sample);
                Candidates.Add(sample);
            }
        }

        // Loaded air with solid directly below it.
        private static bool IsSupportedAir(World world, Vector3Int sample, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            float? below = world.GetDensity(sample + Vector3Int.down);
            return density.HasValue && density.Value < isoLevel && below.HasValue && below.Value >= isoLevel;
        }

        // Nearest the start, with height counting double, so the pile spreads before it climbs.
        private static int Nearest(Vector3Int start)
        {
            int best = 0;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < Candidates.Count; i++)
            {
                Vector3Int d = Candidates[i] - start;
                float score = d.x * d.x + d.z * d.z + 2f * d.y * d.y + (d.y > 0 ? d.y : 0);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }
    }
}
