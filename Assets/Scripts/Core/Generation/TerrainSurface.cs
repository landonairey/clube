using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where a generator's ground is (GL56): the height of its top surface above a point, as
    /// generated, before any edits. Exact for heightfields; other generators are searched down
    /// from the top for the first solid point, so an overhang counts as the surface.
    /// </summary>
    public static class TerrainSurface
    {
        // Metres between samples while searching down, then halved until this close.
        private const float SearchStep = 1f;
        private const float Precision = 0.02f;

        /// <summary>The ground height at (x, z) in world units; <paramref name="bottom"/> when there's none up to <paramref name="top"/>.</summary>
        public static float HeightAt(ITerrainGenerator generator, float x, float z, float bottom, float top)
        {
            if (generator is HeightfieldGenerator heightfield)
            {
                return Mathf.Clamp(heightfield.Height(x, z), bottom, top);
            }
            float above = top;
            for (float y = top - SearchStep; y >= bottom; y -= SearchStep)
            {
                if (generator.Depth(new Vector3(x, y, z)) > 0f)
                {
                    // Solid at y, air at above: halve the gap until it's small.
                    float below = y;
                    while (above - below > Precision)
                    {
                        float middle = (above + below) * 0.5f;
                        if (generator.Depth(new Vector3(x, middle, z)) > 0f)
                        {
                            below = middle;
                        }
                        else
                        {
                            above = middle;
                        }
                    }
                    return (above + below) * 0.5f;
                }
                above = y;
            }
            return bottom;
        }
    }
}
