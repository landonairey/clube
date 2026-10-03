namespace Clube.Core
{
    /// <summary>
    /// Ground heights read from a <see cref="Heightmap"/> (K9). Black is the surface
    /// level minus the amplitude, white the level plus it, and each pixel covers a set
    /// number of world units, with pixel (0, 0) at the world origin.
    /// </summary>
    public sealed class HeightmapGenerator : HeightfieldGenerator
    {
        private readonly Heightmap heightmap;
        private readonly float lowest;
        private readonly float range;
        private readonly float unitsPerPixel;

        public HeightmapGenerator(Heightmap heightmap, float level, float amplitude, float unitsPerPixel)
        {
            this.heightmap = heightmap;
            lowest = level - amplitude;
            range = 2f * amplitude;
            this.unitsPerPixel = unitsPerPixel;
        }

        public override float Height(float x, float z)
        {
            return lowest + range * heightmap.Sample(x / unitsPerPixel, z / unitsPerPixel);
        }
    }
}
