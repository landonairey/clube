using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Finds the top of the highest ground within a radius (GL476: the peak the lone adventurer camps on): a coarse grid
    /// over the circle, then finer grids around the best point found. Works on any height function, usually the
    /// generated surface (<see cref="TerrainSurface.HeightAt"/>), so it needs no loaded chunks.
    /// </summary>
    public static class PeakSearch
    {
        // How many times the grid is made finer round the best point, and by how much each time.
        private const int Refinements = 2;
        private const float RefineFactor = 0.25f;

        /// <summary>
        /// The highest point (x, z) within <paramref name="radius"/> of <paramref name="centre"/>, checked every
        /// <paramref name="step"/> metres and then more finely; inside <paramref name="area"/> when one is given.
        /// <paramref name="height"/> receives its ground height.
        /// </summary>
        public static Vector2 Highest(Func<float, float, float> heightAt, Vector2 centre, float radius, float step, Rect? area, out float height)
        {
            if (heightAt == null)
            {
                throw new ArgumentNullException(nameof(heightAt));
            }
            step = Mathf.Max(0.1f, step);
            Vector2 best = Clamp(centre, area);
            height = heightAt(best.x, best.y);
            int cells = Mathf.CeilToInt(radius / step);
            for (int z = -cells; z <= cells; z++)
            {
                for (int x = -cells; x <= cells; x++)
                {
                    var offset = new Vector2(x * step, z * step);
                    if (offset.sqrMagnitude > radius * radius)
                    {
                        continue;
                    }
                    Try(heightAt, centre + offset, area, ref best, ref height);
                }
            }
            // Finer grids within one coarse step of the best point, still inside the circle.
            for (int round = 0; round < Refinements; round++)
            {
                float fine = step * RefineFactor;
                Vector2 around = best;
                for (int z = -4; z <= 4; z++)
                {
                    for (int x = -4; x <= 4; x++)
                    {
                        Vector2 point = around + new Vector2(x * fine, z * fine);
                        if ((point - centre).sqrMagnitude <= radius * radius)
                        {
                            Try(heightAt, point, area, ref best, ref height);
                        }
                    }
                }
                step = fine;
            }
            return best;
        }

        /// <summary>
        /// Climbs from <paramref name="start"/> to the top of the hill it's on: steps to the highest of the eight points
        /// <paramref name="step"/> metres round it while one is higher, then halves the step, down to half a metre.
        /// Stays inside <paramref name="area"/> when one is given; <paramref name="height"/> receives the top's height.
        /// </summary>
        public static Vector2 Summit(Func<float, float, float> heightAt, Vector2 start, float step, Rect? area, out float height)
        {
            if (heightAt == null)
            {
                throw new ArgumentNullException(nameof(heightAt));
            }
            const int MaxSteps = 400;
            const float FinestStep = 0.5f;
            Vector2 best = Clamp(start, area);
            height = heightAt(best.x, best.y);
            step = Mathf.Max(FinestStep, step);
            for (int i = 0; i < MaxSteps && step >= FinestStep; i++)
            {
                Vector2 from = best;
                for (int z = -1; z <= 1; z++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x != 0 || z != 0)
                        {
                            Try(heightAt, from + new Vector2(x, z) * step, area, ref best, ref height);
                        }
                    }
                }
                if (best == from)
                {
                    step *= 0.5f;
                }
            }
            return best;
        }

        private static void Try(Func<float, float, float> heightAt, Vector2 point, Rect? area, ref Vector2 best, ref float height)
        {
            if (area.HasValue && !area.Value.Contains(point))
            {
                return;
            }
            float h = heightAt(point.x, point.y);
            if (h > height)
            {
                height = h;
                best = point;
            }
        }

        private static Vector2 Clamp(Vector2 point, Rect? area)
        {
            if (!area.HasValue)
            {
                return point;
            }
            Rect r = area.Value;
            return new Vector2(Mathf.Clamp(point.x, r.xMin, r.xMax), Mathf.Clamp(point.y, r.yMin, r.yMax));
        }
    }
}
