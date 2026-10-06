using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// 3D Perlin noise added to a ground level (K9). Because density varies with height as
    /// well as position, the surface can fold back on itself, making overhangs, arches and
    /// floating pieces that no heightfield can.
    /// </summary>
    public struct Perlin3DVolume : IVolumeField
    {
        // Keeps samples off the integer lattice, where Perlin noise is always 0.
        private const float Offset = 0.31f;

        // Improved Perlin noise stays within about ±1.04; a little more, to be safe.
        private const float NoiseBound = 1.1f;

        private PerlinNoise noise;
        private float level;
        private float amplitude;
        private float frequency;

        public Perlin3DVolume(int seed, float level, float amplitude, float frequency)
        {
            noise = new PerlinNoise(seed);
            this.level = level;
            this.amplitude = amplitude;
            this.frequency = frequency;
        }

        /// <summary>Lowest and highest the surface can be anywhere (conservative), for skipping all-air chunks.</summary>
        public float2 SurfaceBounds => new float2(level - amplitude * NoiseBound, level + amplitude * NoiseBound);

        public float Depth(float3 position)
        {
            float bump = noise.Sample(
                position.x * frequency + Offset, position.y * frequency + Offset, position.z * frequency + Offset);
            return level - position.y + amplitude * bump;
        }
    }
}
