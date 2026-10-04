using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Adds or removes terrain in a sphere (K13–K16) through the field's single edit path
    /// (A7). Samples within the radius move towards solid (add) or air (remove) by the
    /// brush strength:
    /// <list type="bullet">
    /// <item><b>Hard</b>: every sample within the radius changes.</item>
    /// <item><b>Soft</b>: only the surface layer changes. Adding fills samples that touch
    /// a fully solid sample (density 1); removing empties samples that touch a fully empty
    /// one (density 0). Held down, it piles up or digs down a layer at a time, like
    /// shovelling.</item>
    /// </list>
    /// Pure Core logic: callers decide where the brush goes and how often it applies.
    /// Works on any <see cref="IDensityField"/>, so on a <see cref="World"/> it sees across
    /// chunk borders and writes every copy of a border sample (M5).
    /// </summary>
    public static class TerrainBrush
    {
        // The six face neighbours: what "touching" means for the soft brush.
        private static readonly Vector3Int[] Neighbours =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        // Reused between applications; the brush only runs on the main thread.
        private static readonly List<(Vector3Int Sample, float Before)> Targets = new List<(Vector3Int, float)>();

        /// <param name="field">The densities to edit, addressed by sample coordinate.</param>
        /// <param name="centre">Brush centre in the field's world units (its sample 0 at the origin).</param>
        public static BrushResult Apply(
            IDensityField field, Vector3 centre, float voxelSize, BrushSettings brush, BrushOperation operation)
        {
            if (brush.Radius <= 0f || brush.Strength <= 0f)
            {
                return default;
            }

            bool adding = operation == BrushOperation.Add;
            Vector3Int from = Vector3Int.FloorToInt((centre - Vector3.one * brush.Radius) / voxelSize);
            Vector3Int to = Vector3Int.CeilToInt((centre + Vector3.one * brush.Radius) / voxelSize);

            // Pick every target before writing any, so a soft application only grows the
            // layer that was the surface when it started, not a chain of layers at once.
            Targets.Clear();
            for (int z = from.z; z <= to.z; z++)
            {
                for (int y = from.y; y <= to.y; y++)
                {
                    for (int x = from.x; x <= to.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (Vector3.Distance((Vector3)sample * voxelSize, centre) > brush.Radius
                            || !field.TryGetDensity(sample, out float before))
                        {
                            continue;
                        }

                        // Nothing to add to a full sample, or to remove from an empty one.
                        if (adding ? before >= 1f : before <= 0f)
                        {
                            continue;
                        }

                        if (brush.Falloff == BrushFalloff.Soft && !TouchesSaturated(field, sample, adding))
                        {
                            continue;
                        }

                        Targets.Add((sample, before));
                    }
                }
            }

            float sign = adding ? 1f : -1f;
            float added = 0f;
            float removed = 0f;
            foreach ((Vector3Int sample, float before) in Targets)
            {
                float after = Mathf.Clamp01(before + sign * brush.Strength);
                field.SetDensity(sample, after);
                if (after > before)
                {
                    added += after - before;
                }
                else
                {
                    removed += before - after;
                }
            }
            return new BrushResult(Targets.Count, added, removed);
        }

        // Adding grows from fully solid samples; removing eats in from fully empty ones.
        private static bool TouchesSaturated(IDensityField field, Vector3Int sample, bool adding)
        {
            foreach (Vector3Int offset in Neighbours)
            {
                if (field.TryGetDensity(sample + offset, out float neighbour) && (adding ? neighbour >= 1f : neighbour <= 0f))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
