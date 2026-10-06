using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Ground heights read from a <see cref="Heightmap"/> (K9). Black is the surface
    /// level minus the amplitude, white the level plus it, and each pixel covers a set
    /// number of world units, with pixel (0, 0) at the world origin.
    /// </summary>
    /// <remarks>
    /// Managed calls read the heightmap directly. A column job gets its own native copy of
    /// the pixels (<see cref="HeightmapHeight"/>), freed when the job completes, so the
    /// generator holds nothing that needs disposing.
    /// </remarks>
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

        public override JobHandle ScheduleColumnHeights(
            ColumnBlock block, NativeArray<float> heights, NativeArray<float2> heightRange, JobHandle dependsOn = default)
        {
            var pixels = new NativeArray<float>(heightmap.Width * heightmap.Height, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            heightmap.CopyTo(pixels);
            var field = new HeightmapHeight
            {
                Pixels = pixels,
                Width = heightmap.Width,
                Rows = heightmap.Height,
                Lowest = lowest,
                Range = range,
                UnitsPerPixel = unitsPerPixel,
            };
            JobHandle job = new ColumnHeightsJob<HeightmapHeight> { Field = field, Block = block, Heights = heights, Range = heightRange }
                .Schedule(dependsOn);
            return pixels.Dispose(job);
        }
    }
}
