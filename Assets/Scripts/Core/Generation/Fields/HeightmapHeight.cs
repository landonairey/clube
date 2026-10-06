using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Ground heights read from a heightmap (K9), for generation jobs: black is the surface
    /// level minus the amplitude, white the level plus it, each pixel covers a set number of
    /// world units, pixel (0, 0) sits at the world origin, and positions past the edges clamp
    /// to the edge. Samples with the same bilinear blend as <see cref="Heightmap.Sample"/>.
    /// </summary>
    /// <remarks>
    /// The pixels are a native array the scheduler copies in for each job and frees with it
    /// (<see cref="HeightmapGenerator"/>), so nothing needs disposing between jobs.
    /// </remarks>
    public struct HeightmapHeight : IHeightField
    {
        [ReadOnly] public NativeArray<float> Pixels;
        public int Width;
        public int Rows;
        public float Lowest;
        public float Range;
        public float UnitsPerPixel;

        public float Height(float x, float z)
        {
            return Lowest + Range * Sample(x / UnitsPerPixel, z / UnitsPerPixel);
        }

        private float Sample(float x, float y)
        {
            int x0 = (int)math.floor(x);
            int y0 = (int)math.floor(y);
            float tx = x - x0;
            float ty = y - y0;
            float bottom = math.lerp(Pixel(x0, y0), Pixel(x0 + 1, y0), tx);
            float top = math.lerp(Pixel(x0, y0 + 1), Pixel(x0 + 1, y0 + 1), tx);
            return math.lerp(bottom, top, ty);
        }

        private float Pixel(int x, int y)
        {
            return Pixels[math.clamp(x, 0, Width - 1) + Width * math.clamp(y, 0, Rows - 1)];
        }
    }
}
