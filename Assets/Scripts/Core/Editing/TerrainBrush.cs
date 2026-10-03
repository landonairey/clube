using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Adds or removes a sphere of terrain (K13–K16) through the chunk's single edit
    /// path (A7). Each sample in reach moves towards solid (add) or air (remove) by
    /// the brush strength times its weight:
    /// <list type="bullet">
    /// <item><b>Hard</b>: the weight is the sphere's own density, ramped across the
    /// radius like terrain (<see cref="TerrainDensity"/>), so at full strength the
    /// new surface sits exactly on the radius.</item>
    /// <item><b>Smooth</b>: the weight fades as (1 - (d/r)²)², reaching zero at the
    /// radius. At full strength on open air it makes a blob about half the radius;
    /// repeated applications grow it.</item>
    /// </list>
    /// Pure Core logic: callers decide where the brush goes and how often it applies.
    /// </summary>
    public static class TerrainBrush
    {
        /// <param name="centre">Brush centre in chunk-local world units (sample 0 at the origin).</param>
        /// <returns>How many samples changed.</returns>
        public static int Apply(
            Chunk chunk, Vector3 centre, float voxelSize, BrushSettings brush, BrushOperation operation)
        {
            if (brush.Radius <= 0f || brush.Strength <= 0f)
            {
                return 0;
            }

            // A hard brush's ramp reaches past the radius.
            float reach = brush.Falloff == BrushFalloff.Hard
                ? brush.Radius + TerrainDensity.RampHalfWidth
                : brush.Radius;
            Vector3Int max = chunk.SampleCount - Vector3Int.one;
            Vector3Int from = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt((centre - Vector3.one * reach) / voxelSize));
            Vector3Int to = Vector3Int.Min(max, Vector3Int.CeilToInt((centre + Vector3.one * reach) / voxelSize));

            float sign = operation == BrushOperation.Add ? 1f : -1f;
            int changed = 0;
            for (int z = from.z; z <= to.z; z++)
            {
                for (int y = from.y; y <= to.y; y++)
                {
                    for (int x = from.x; x <= to.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        float distance = Vector3.Distance((Vector3)sample * voxelSize, centre);
                        float weight = Weight(distance, brush);
                        if (weight <= 0f)
                        {
                            continue;
                        }

                        float before = chunk.GetDensity(sample);
                        float after = Mathf.Clamp01(before + sign * brush.Strength * weight);
                        if (after != before)
                        {
                            chunk.SetDensity(sample, after);
                            changed++;
                        }
                    }
                }
            }
            return changed;
        }

        /// <summary>The brush's effect at a distance from its centre, 0-1.</summary>
        public static float Weight(float distance, BrushSettings brush)
        {
            if (brush.Falloff == BrushFalloff.Hard)
            {
                return TerrainDensity.FromDepth(brush.Radius - distance);
            }

            if (distance >= brush.Radius)
            {
                return 0f;
            }
            float t = distance / brush.Radius;
            float fade = 1f - t * t;
            return fade * fade;
        }
    }
}
