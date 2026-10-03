using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// 3D Perlin noise added to a ground level (K9). Because density varies with
    /// height as well as position, the surface can fold back on itself, making
    /// overhangs, arches and floating pieces that no heightfield can.
    /// </summary>
    public sealed class Perlin3DGenerator : ITerrainGenerator
    {
        // Keeps samples off the integer lattice, where Perlin noise is always 0.
        private const float Offset = 0.31f;

        private readonly PerlinNoise noise;
        private readonly float level;
        private readonly float amplitude;
        private readonly float frequency;

        public Perlin3DGenerator(int seed, float level, float amplitude, float frequency)
        {
            noise = new PerlinNoise(seed);
            this.level = level;
            this.amplitude = amplitude;
            this.frequency = frequency;
        }

        public float Density(Vector3 position)
        {
            float bump = noise.Sample(
                position.x * frequency + Offset, position.y * frequency + Offset, position.z * frequency + Offset);
            return TerrainDensity.FromDepth(level - position.y + amplitude * bump);
        }
    }
}
