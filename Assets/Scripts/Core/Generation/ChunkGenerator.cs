using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Fills chunks from a terrain generator (K9): one density per sample, a material by its
    /// depth below the surface (<see cref="TerrainLayers"/>, M10), and ore nodes replacing some
    /// of it (<see cref="OreField"/>, 3D). Samples are taken at their world position, so chunks
    /// placed side by side get matching values along shared borders (M2).
    /// </summary>
    /// <remarks>
    /// <para>Every fill runs as Burst jobs (K35), picked once from the generator's kind:</para>
    /// <list type="bullet">
    /// <item>a <see cref="HeightfieldGenerator"/> computes one height per column
    /// (<see cref="ColumnHeightsJob{THeight}"/>), then each sample's depth from it
    /// (<see cref="HeightfieldFillJob"/>);</item>
    /// <item>a <see cref="VolumeGenerator"/> samples its field at every sample
    /// (<see cref="VolumeFillJob{TVolume}"/>), and chunks above its highest possible surface
    /// skip the job;</item>
    /// <item>any other <see cref="ITerrainGenerator"/> is sampled here on the main thread
    /// (<see cref="DepthFillJob"/>).</item>
    /// </list>
    /// <para>All three end in <see cref="ChunkFillKernel"/>. <see cref="Fill"/> runs them to
    /// completion for the labs and tests; the world's <see cref="ChunkPipeline"/> schedules
    /// them and collects the results frames later.</para>
    /// </remarks>
    public static class ChunkGenerator
    {
        // Reused between synchronous fills, which run on the main thread.
        private static readonly List<OreNode> Nodes = new List<OreNode>();

        /// <summary>Overwrites an existing chunk with generated terrain, and waits for it (labs, tests).</summary>
        /// <param name="chunkOrigin">World position of the chunk's sample (0,0,0); a whole number of voxels from the world origin.</param>
        /// <param name="layers">Materials by depth; null or empty leaves every material at id 0.</param>
        /// <param name="ores">Ore nodes to place; null for none.</param>
        /// <param name="trees">Trees to grow (GL30); null for none.</param>
        public static void Fill(
            Chunk chunk, ITerrainGenerator generator, Vector3 chunkOrigin, float voxelSize,
            TerrainLayers layers = null, OreField ores = null, TreeField trees = null)
        {
            Vector3Int first = Vector3Int.RoundToInt(chunkOrigin / voxelSize);
            Vector3Int count = chunk.SampleCount;
            var grid = new ChunkSampleGrid(new int3(first.x, first.y, first.z), new int3(count.x, count.y, count.z), voxelSize);
            using (ChunkFillOutput output = Run(generator, grid, layers, ores, trees, chunk.StoresBytes ? DensityFormat.Byte : DensityFormat.Float))
            {
                chunk.Load(output, output.Summary[0]);
            }
        }

        /// <summary>
        /// Generates one world chunk and waits for it: a uniform chunk (no density storage) when
        /// every sample came out the same, otherwise storage from <paramref name="createStorage"/>.
        /// </summary>
        public static Chunk Generate(
            ITerrainGenerator generator, ChunkSampleGrid grid, Func<Vector3Int, IVoxelStorage> createStorage,
            TerrainLayers layers = null, OreField ores = null, DensityFormat format = DensityFormat.Float, TreeField trees = null)
        {
            using (ChunkFillOutput output = Run(generator, grid, layers, ores, trees, format))
            {
                return FromOutput(grid, output, createStorage);
            }
        }

        /// <summary>A new chunk holding a completed job's output (uniform when the summary says so).</summary>
        public static Chunk FromOutput(ChunkSampleGrid grid, in ChunkFillOutput output, Func<Vector3Int, IVoxelStorage> createStorage)
        {
            ChunkFillSummary summary = output.Summary[0];
            var sampleCount = new Vector3Int(grid.SampleCount.x, grid.SampleCount.y, grid.SampleCount.z);
            Chunk chunk = Chunk.Uniform(sampleCount, summary.Density, summary.Material, createStorage);
            if (!summary.UniformDensity || !summary.UniformMaterial)
            {
                chunk.Load(output, summary);
            }
            return chunk;
        }

        /// <summary>The fill settings for a terrain's layers and ores.</summary>
        public static ChunkFillSettings SettingsFor(TerrainLayers layers, OreField ores, DensityFormat format)
        {
            return new ChunkFillSettings(LayerTable.From(layers), ores?.Seed ?? 0, format);
        }

        /// <summary>
        /// Schedules generating one chunk's samples into <paramref name="output"/>, by the path
        /// the generator's kind picks (see the remarks). Temporary buffers free themselves when
        /// the jobs complete; <paramref name="ores"/>, <paramref name="trees"/> and <paramref name="output"/> stay the caller's.
        /// </summary>
        public static JobHandle Schedule(
            ITerrainGenerator generator, ChunkSampleGrid grid, ChunkFillSettings settings,
            NativeArray<OreNodeData> ores, NativeArray<TreePart> trees, ChunkFillOutput output, JobHandle dependsOn = default)
        {
            switch (generator)
            {
                case HeightfieldGenerator heightfield:
                {
                    var heights = new NativeArray<float>(grid.BorderedColumns.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    var range = new NativeArray<float2>(1, Allocator.Persistent);
                    JobHandle columns = heightfield.ScheduleColumnHeights(grid.BorderedColumns, heights, range, dependsOn);
                    JobHandle fill = ScheduleFromColumns(heights, range, grid, settings, ores, trees, output, columns);
                    return JobHandle.CombineDependencies(heights.Dispose(fill), range.Dispose(fill));
                }
                case VolumeGenerator volume:
                {
                    if (trees.Length == 0 && grid.Bottom >= volume.SurfaceBounds.y + TerrainDensity.RampHalfWidth)
                    {
                        dependsOn.Complete();
                        output.Summary[0] = ChunkFillKernel.AllAir(settings.Layers);
                        return default;
                    }
                    return volume.ScheduleFill(grid, settings, ores, trees, output, dependsOn);
                }
                default:
                {
                    var depths = new NativeArray<float>(grid.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    SampleDepths(generator, grid, depths);
                    JobHandle fill = new DepthFillJob { Depths = depths, Grid = grid, Settings = settings, Ores = ores, Trees = trees, Output = output }
                        .Schedule(dependsOn);
                    return depths.Dispose(fill);
                }
            }
        }

        /// <summary>
        /// Schedules filling one chunk of a heightfield from column heights already computed
        /// (or being computed: pass their job as <paramref name="dependsOn"/>). The world shares
        /// one set of heights between every chunk stacked on a column.
        /// </summary>
        public static JobHandle ScheduleFromColumns(
            NativeArray<float> heights, NativeArray<float2> heightRange, ChunkSampleGrid grid, ChunkFillSettings settings,
            NativeArray<OreNodeData> ores, NativeArray<TreePart> trees, ChunkFillOutput output, JobHandle dependsOn = default)
        {
            return new HeightfieldFillJob
            {
                Heights = heights,
                HeightRange = heightRange,
                Grid = grid,
                Settings = settings,
                Ores = ores,
                Trees = trees,
                Output = output,
            }.Schedule(dependsOn);
        }

        /// <summary>The ore nodes reaching a chunk, as job data (empty, not default, when there are none).</summary>
        public static NativeArray<OreNodeData> CollectOres(OreField ores, ChunkSampleGrid grid, Allocator allocator)
        {
            if (ores == null)
            {
                return new NativeArray<OreNodeData>(0, allocator);
            }
            ores.CollectNodes(grid.Bounds, Nodes);
            return ores.ToJobNodes(Nodes, allocator);
        }

        /// <summary>The tree parts reaching a chunk, as job data (empty, not default, when there are none).</summary>
        public static NativeArray<TreePart> CollectTrees(TreeField trees, ChunkSampleGrid grid, Allocator allocator)
        {
            return trees != null ? trees.CollectJobParts(grid.Bounds, allocator) : new NativeArray<TreePart>(0, allocator);
        }

        // Schedules and completes every job, leaving the output for the caller to read and dispose.
        private static ChunkFillOutput Run(
            ITerrainGenerator generator, ChunkSampleGrid grid, TerrainLayers layers, OreField ores, TreeField trees, DensityFormat format)
        {
            var output = ChunkFillOutput.Allocate(grid.Length, format, Allocator.Persistent);
            NativeArray<OreNodeData> nodes = CollectOres(ores, grid, Allocator.Persistent);
            NativeArray<TreePart> parts = CollectTrees(trees, grid, Allocator.Persistent);
            try
            {
                Schedule(generator, grid, SettingsFor(layers, ores, format), nodes, parts, output).Complete();
            }
            finally
            {
                nodes.Dispose();
                parts.Dispose();
            }
            return output;
        }

        private static void SampleDepths(ITerrainGenerator generator, ChunkSampleGrid grid, NativeArray<float> depths)
        {
            int3 count = grid.SampleCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        depths[grid.Index(x, y, z)] = generator.Depth(grid.Position(x, y, z));
                    }
                }
            }
        }
    }
}
