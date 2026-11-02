using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Ridges and gullies down slopes, in the terrain data itself (GL293), so marching cubes builds them and they can be
    /// mined and walked on. A ridged noise of the world position, stretched upright (a ridge <c>RidgeWidth</c> across and
    /// <c>RidgeLength</c> long), so on any slope its long axis lies along the fall line: flutes down cliffs, ribs down
    /// hillsides. It moves a sample's depth by up to <c>RidgeDepth</c> metres across the face: the vertical change is that
    /// times the slope's gradient, so a cliff gets ridges as deep as a hillside's. Gentle ground gets none
    /// (<c>RidgeFromGradient</c> up to <c>RidgeFullGradient</c>). It depends only on the global position and the column's
    /// gradient, which neighbouring chunks share (bordered column heights), so chunk borders agree.
    /// </summary>
    public static class SlopeRidges
    {
        // The ridge noise averages about this; taking it off keeps the surface, on the whole, where the heightfield put it.
        private const float Mean = 0.7f;
        private const float MaxGradient = 4f;

        /// <summary>The depth to add at a world position on a column of this gradient (rise over run), in metres.</summary>
        public static float Offset(float3 position, float gradient, in LayerTable layers)
        {
            if (layers.RidgeDepth <= 0f || gradient <= layers.RidgeFromGradient)
            {
                return 0f;
            }
            float steep = math.smoothstep(layers.RidgeFromGradient, layers.RidgeFullGradient, gradient);
            float3 p = position * new float3(1f / layers.RidgeWidth, 1f / layers.RidgeLength, 1f / layers.RidgeWidth);
            float broad = Ridged(p);
            float fine = Ridged(p * new float3(2.1f, 1.4f, 2.1f) + 17.3f);
            float ridge = (broad - Mean) + 0.2f * (fine - Mean);
            return ridge * layers.RidgeDepth * math.min(gradient, MaxGradient) * steep;
        }

        /// <summary>The most a ridge can move the surface up or down, in metres, so chunks that far off it still get sampled.</summary>
        public static float Reach(in LayerTable layers)
        {
            return layers.RidgeDepth > 0f ? layers.RidgeDepth * MaxGradient * (Mean + 0.2f * Mean) : 0f;
        }

        // 0-1, rounded crests where the noise crosses zero (no kink, so the voxels don't make spikes of them) and
        // broad gullies between.
        private static float Ridged(float3 p)
        {
            return 1f - math.pow(math.abs(noise.snoise(p)), 1.6f);
        }
    }
}
