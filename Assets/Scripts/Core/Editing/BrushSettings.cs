using UnityEngine;

namespace Clube.Core
{
    /// <summary>The shape of one <see cref="TerrainBrush"/> application.</summary>
    public readonly struct BrushSettings
    {
        /// <param name="radius">Sphere radius in world units (K15).</param>
        /// <param name="strength">How much density one application adds or removes at
        /// full effect, 0-1 (K16). 1 fills or empties a sample in one go.</param>
        /// <param name="falloff">How the effect fades towards the radius (K16).</param>
        public BrushSettings(float radius, float strength = 1f, BrushFalloff falloff = BrushFalloff.Hard)
        {
            Radius = Mathf.Max(0f, radius);
            Strength = Mathf.Clamp01(strength);
            Falloff = falloff;
        }

        public float Radius { get; }

        public float Strength { get; }

        public BrushFalloff Falloff { get; }
    }
}
