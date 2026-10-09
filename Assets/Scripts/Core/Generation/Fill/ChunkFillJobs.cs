using Clube.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Generic jobs scheduled from generic code need their concrete types listed, or Burst
// can't find them to compile: one line per terrain field (A6, K35).
[assembly: RegisterGenericJobType(typeof(ColumnHeightsJob<FlatHeight>))]
[assembly: RegisterGenericJobType(typeof(ColumnHeightsJob<SineHeight>))]
[assembly: RegisterGenericJobType(typeof(ColumnHeightsJob<FractalNoiseHeight>))]
[assembly: RegisterGenericJobType(typeof(ColumnHeightsJob<SplineHeight>))]
[assembly: RegisterGenericJobType(typeof(ColumnHeightsJob<HeightmapHeight>))]
[assembly: RegisterGenericJobType(typeof(ColumnHeightsJob<LandscapeHeight>))]
[assembly: RegisterGenericJobType(typeof(VolumeFillJob<Perlin3DVolume>))]

namespace Clube.Core
{
    /// <summary>
    /// A heightfield's height at every column of a <see cref="ColumnBlock"/> (K35), and the
    /// lowest and highest of them. Run once per chunk column; every chunk stacked on it
    /// derives its depths from these (<see cref="HeightfieldFillJob"/>), so the noise is
    /// sampled once per column instead of once per sample.
    /// </summary>
    [BurstCompile]
    public struct ColumnHeightsJob<THeight> : IJob where THeight : struct, IHeightField
    {
        public THeight Field;
        public ColumnBlock Block;

        [WriteOnly] public NativeArray<float> Heights;

        /// <summary>One element: (lowest, highest).</summary>
        [WriteOnly] public NativeArray<float2> Range;

        public void Execute()
        {
            float low = float.MaxValue;
            float high = float.MinValue;
            for (int z = 0; z < Block.Count.y; z++)
            {
                for (int x = 0; x < Block.Count.x; x++)
                {
                    float2 position = Block.Position(x, z);
                    float height = Field.Height(position.x, position.y);
                    Heights[Block.Index(x, z)] = height;
                    low = math.min(low, height);
                    high = math.max(high, height);
                }
            }
            Range[0] = new float2(low, high);
        }
    }

    /// <summary>
    /// Fills one chunk of a heightfield terrain from its column heights (K35): each sample's
    /// depth is its column's height minus its own, and each column's slope (from its
    /// neighbours' heights) says whether it is bare rock (GL21); then
    /// <see cref="ChunkFillKernel"/> does the rest. The heights cover
    /// <see cref="ChunkSampleGrid.BorderedColumns"/>, one column more on every side, so border
    /// columns get the same slope in both chunks. A chunk wholly above its columns' highest
    /// point, with no tree reaching it, is all air and skips the arrays.
    /// </summary>
    [BurstCompile]
    public struct HeightfieldFillJob : IJob
    {
        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<float2> HeightRange;
        public ChunkSampleGrid Grid;
        public ChunkFillSettings Settings;
        [ReadOnly] public NativeArray<OreNodeData> Ores;
        [ReadOnly] public NativeArray<TreePart> Trees;
        public ChunkFillOutput Output;

        public void Execute()
        {
            if (Trees.Length == 0 && Grid.Bottom >= HeightRange[0].y + TerrainDensity.RampHalfWidth)
            {
                Output.Summary[0] = ChunkFillKernel.AllAir(Settings.Layers);
                return;
            }

            var depths = new NativeArray<float>(Grid.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int3 count = Grid.SampleCount;
            int stride = count.x + 2;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    float height = (Grid.FirstSample.y + y) * Grid.VoxelSize;
                    int row = Grid.Index(0, y, z);
                    int column = stride * (z + 1) + 1;
                    for (int x = 0; x < count.x; x++)
                    {
                        depths[row + x] = Heights[column + x] - height;
                    }
                }
            }

            var steep = new NativeArray<byte>(Settings.Layers.SteepGradient > 0f ? count.x * count.z : 0, Allocator.Temp);
            for (int z = 0; z < count.z && steep.Length > 0; z++)
            {
                for (int x = 0; x < count.x; x++)
                {
                    int centre = stride * (z + 1) + x + 1;
                    float dx = (Heights[centre + 1] - Heights[centre - 1]) / (2f * Grid.VoxelSize);
                    float dz = (Heights[centre + stride] - Heights[centre - stride]) / (2f * Grid.VoxelSize);
                    steep[x + count.x * z] = Settings.Layers.IsSteep(math.sqrt(dx * dx + dz * dz), Heights[centre]) ? (byte)1 : (byte)0;
                }
            }
            ChunkFillKernel.Run(depths, steep, Grid, Settings, Ores, Trees, ref Output);
        }
    }

    /// <summary>Fills one chunk of a 3D terrain (K35): the field's depth at every sample, then <see cref="ChunkFillKernel"/>.</summary>
    [BurstCompile]
    public struct VolumeFillJob<TVolume> : IJob where TVolume : struct, IVolumeField
    {
        public TVolume Field;
        public ChunkSampleGrid Grid;
        public ChunkFillSettings Settings;
        [ReadOnly] public NativeArray<OreNodeData> Ores;
        [ReadOnly] public NativeArray<TreePart> Trees;
        public ChunkFillOutput Output;

        public void Execute()
        {
            var depths = new NativeArray<float>(Grid.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int3 count = Grid.SampleCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        depths[Grid.Index(x, y, z)] = Field.Depth(Grid.Position(x, y, z));
                    }
                }
            }
            ChunkFillKernel.Run(depths, default, Grid, Settings, Ores, Trees, ref Output);
        }
    }

    /// <summary>
    /// Fills one chunk from depths already sampled on the main thread: the path for a managed
    /// <see cref="ITerrainGenerator"/> that has no job form (e.g. a test or benchmark wrapper),
    /// so it still shares the kernel with every other path.
    /// </summary>
    [BurstCompile]
    public struct DepthFillJob : IJob
    {
        [ReadOnly] public NativeArray<float> Depths;
        public ChunkSampleGrid Grid;
        public ChunkFillSettings Settings;
        [ReadOnly] public NativeArray<OreNodeData> Ores;
        [ReadOnly] public NativeArray<TreePart> Trees;
        public ChunkFillOutput Output;

        public void Execute()
        {
            ChunkFillKernel.Run(Depths, default, Grid, Settings, Ores, Trees, ref Output);
        }
    }
}
