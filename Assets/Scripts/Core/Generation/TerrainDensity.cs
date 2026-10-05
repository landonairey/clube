using UnityEngine;

namespace Clube.Core
{
    /// <summary>Turns "how far below the surface" into a density, the same way for every generator.</summary>
    public static class TerrainDensity
    {
        /// <summary>
        /// Density ramps linearly from 1 at this depth below the surface to 0 at this
        /// height above it, in world units. Marching Cubes interpolates between the two
        /// samples either side of the surface, so it places the surface exactly where
        /// the generator put it as long as neither is clamped: true for voxel sizes up
        /// to this value. A narrower ramp clamps one of them and biases the surface.
        /// </summary>
        public const float RampHalfWidth = 1f;

        /// <summary>0.5 on the surface, 1 at <see cref="RampHalfWidth"/> below it and deeper, 0 as far above.</summary>
        public static float FromDepth(float depth)
        {
            return Mathf.Clamp01(0.5f + depth / (2f * RampHalfWidth));
        }

        /// <summary>The depth a density stands for: the inverse of <see cref="FromDepth"/> within the ramp.</summary>
        public static float ToDepth(float density)
        {
            return (Mathf.Clamp01(density) - 0.5f) * 2f * RampHalfWidth;
        }
    }
}
