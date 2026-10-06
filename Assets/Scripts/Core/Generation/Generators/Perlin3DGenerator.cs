using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// 3D Perlin noise added to a ground level (K9). Because density varies with
    /// height as well as position, the surface can fold back on itself, making
    /// overhangs, arches and floating pieces that no heightfield can. The shape is
    /// <see cref="Perlin3DVolume"/>.
    /// </summary>
    public sealed class Perlin3DGenerator : VolumeGenerator<Perlin3DVolume>
    {
        public Perlin3DGenerator(int seed, float level, float amplitude, float frequency)
            : base(new Perlin3DVolume(seed, level, amplitude, frequency))
        {
        }

        public override float2 SurfaceBounds => Field.SurfaceBounds;
    }
}
