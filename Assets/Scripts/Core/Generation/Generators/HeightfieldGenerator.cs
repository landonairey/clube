using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A generator defined by a ground height at each (x, z): solid below it, empty
    /// above. Subclasses only say how tall the ground is, and how to compute a block of
    /// column heights in a job (K35); every chunk stacked on a column then shares them.
    /// </summary>
    public abstract class HeightfieldGenerator : ITerrainGenerator
    {
        public float Depth(Vector3 position)
        {
            return Height(position.x, position.z) - position.y;
        }

        /// <summary>Ground height in world units at a horizontal position.</summary>
        public abstract float Height(float x, float z);

        /// <summary>
        /// Schedules a job writing the height of every column in <paramref name="block"/> to
        /// <paramref name="heights"/> (x fastest, then z) and (lowest, highest) to <paramref name="range"/>[0].
        /// </summary>
        public abstract JobHandle ScheduleColumnHeights(
            ColumnBlock block, NativeArray<float> heights, NativeArray<float2> range, JobHandle dependsOn = default);

        /// <summary>
        /// Like <see cref="ScheduleColumnHeights"/>, without the range, with the rows spread over the
        /// workers: for one big block at a time (P7's far terrain), where one job per block would
        /// leave the other workers idle. Falls back to the single job where a generator has no rows job.
        /// </summary>
        public virtual JobHandle ScheduleColumnHeightsParallel(ColumnBlock block, NativeArray<float> heights, JobHandle dependsOn = default)
        {
            var range = new NativeArray<float2>(1, Allocator.TempJob);
            JobHandle job = ScheduleColumnHeights(block, heights, range, dependsOn);
            return range.Dispose(job);
        }

        /// <summary>The ground's height at each of <paramref name="points"/> (x, z), spread over the workers.</summary>
        public abstract JobHandle SchedulePointHeights(NativeArray<float2> points, NativeArray<float> heights, JobHandle dependsOn = default);
    }

    /// <summary>
    /// A heightfield generator whose shape is a Burst-compatible <see cref="IHeightField"/>
    /// struct (K35): managed calls and generation jobs run the same code.
    /// </summary>
    public abstract class HeightfieldGenerator<TField> : HeightfieldGenerator where TField : struct, IHeightField
    {
        // Not readonly: calling a method on a readonly struct field copies the struct first.
        private TField field;

        protected HeightfieldGenerator(TField field)
        {
            this.field = field;
        }

        public override float Height(float x, float z)
        {
            return field.Height(x, z);
        }

        public override JobHandle ScheduleColumnHeights(
            ColumnBlock block, NativeArray<float> heights, NativeArray<float2> range, JobHandle dependsOn = default)
        {
            return new ColumnHeightsJob<TField> { Field = field, Block = block, Heights = heights, Range = range }.Schedule(dependsOn);
        }

        public override JobHandle ScheduleColumnHeightsParallel(ColumnBlock block, NativeArray<float> heights, JobHandle dependsOn = default)
        {
            return new ColumnHeightRowsJob<TField> { Field = field, Block = block, Heights = heights }.ScheduleParallel(block.Count.y, 4, dependsOn);
        }

        public override JobHandle SchedulePointHeights(NativeArray<float2> points, NativeArray<float> heights, JobHandle dependsOn = default)
        {
            return new PointHeightsJob<TField> { Field = field, Points = points, Heights = heights }.ScheduleParallel(points.Length, 64, dependsOn);
        }
    }

    /// <summary>
    /// A fully 3D generator (no single height per column), whose shape is a Burst-compatible
    /// <see cref="IVolumeField"/> struct (K35). Each chunk samples it at every sample.
    /// </summary>
    public abstract class VolumeGenerator : ITerrainGenerator
    {
        public abstract float Depth(Vector3 position);

        /// <summary>Lowest and highest the surface can be anywhere (conservative): chunks above it are all air.</summary>
        public abstract float2 SurfaceBounds { get; }

        /// <summary>Schedules a job filling one chunk's samples (<see cref="VolumeFillJob{TVolume}"/>).</summary>
        public abstract JobHandle ScheduleFill(
            ChunkSampleGrid grid, ChunkFillSettings settings, NativeArray<OreNodeData> ores, NativeArray<TreePart> trees, ChunkFillOutput output,
            JobHandle dependsOn = default);
    }

    /// <summary>A <see cref="VolumeGenerator"/> over a field struct.</summary>
    public abstract class VolumeGenerator<TField> : VolumeGenerator where TField : struct, IVolumeField
    {
        private TField field;

        protected VolumeGenerator(TField field)
        {
            this.field = field;
        }

        protected TField Field => field;

        public override float Depth(Vector3 position)
        {
            return field.Depth(position);
        }

        public override JobHandle ScheduleFill(
            ChunkSampleGrid grid, ChunkFillSettings settings, NativeArray<OreNodeData> ores, NativeArray<TreePart> trees, ChunkFillOutput output,
            JobHandle dependsOn = default)
        {
            return new VolumeFillJob<TField> { Field = field, Grid = grid, Settings = settings, Ores = ores, Trees = trees, Output = output }
                .Schedule(dependsOn);
        }
    }
}
