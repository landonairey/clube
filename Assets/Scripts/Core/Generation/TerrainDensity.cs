using UnityEngine;

namespace Clube.Core
{
    /// <summary>Turns "how far below the surface" into a density, the same way for every generator.</summary>
    public static class TerrainDensity
    {
        /// <summary>
        /// World units over which density fades from 1 to 0 across the surface. Wide
        /// enough to span at least a voxel at sensible voxel sizes, so the iso level
        /// can interpolate a smooth surface.
        /// </summary>
        public const float SurfaceThickness = 1f;

        /// <summary>0.5 on the surface, 1 at half a thickness below it and deeper, 0 at half above.</summary>
        public static float FromDepth(float depth)
        {
            return Mathf.Clamp01(0.5f + depth / SurfaceThickness);
        }
    }
}
