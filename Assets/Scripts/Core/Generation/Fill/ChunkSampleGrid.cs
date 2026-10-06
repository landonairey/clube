using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The block of samples one chunk holds, as generation jobs see it (K35): where its first
    /// sample sits in the world's global sample grid, how many samples it has per axis, and
    /// the voxel size. Sample positions are computed from the global sample index, so two
    /// chunks sharing a border sample compute exactly the same position for it (M2).
    /// </summary>
    /// <remarks>Arrays over the block run X fastest, then Y, then Z, like the voxel storages.</remarks>
    public struct ChunkSampleGrid
    {
        /// <summary>Global sample of the block's local sample (0, 0, 0).</summary>
        public int3 FirstSample;

        /// <summary>Samples per axis (a chunk of N voxels has N + 1).</summary>
        public int3 SampleCount;

        /// <summary>Edge length of one voxel in world units.</summary>
        public float VoxelSize;

        public ChunkSampleGrid(int3 firstSample, int3 sampleCount, float voxelSize)
        {
            FirstSample = firstSample;
            SampleCount = sampleCount;
            VoxelSize = voxelSize;
        }

        /// <summary>The block of samples a world chunk holds.</summary>
        public static ChunkSampleGrid ForChunk(WorldGrid grid, Vector3Int coord)
        {
            Vector3Int first = grid.ChunkFirstSample(coord);
            Vector3Int count = grid.ChunkSize + Vector3Int.one;
            return new ChunkSampleGrid(new int3(first.x, first.y, first.z), new int3(count.x, count.y, count.z), grid.VoxelSize);
        }

        /// <summary>Samples in the block.</summary>
        public int Length => SampleCount.x * SampleCount.y * SampleCount.z;

        /// <summary>Sample columns in the block: one per (x, z).</summary>
        public int ColumnCount => SampleCount.x * SampleCount.z;

        /// <summary>World height of the lowest sample row.</summary>
        public float Bottom => FirstSample.y * VoxelSize;

        /// <summary>World height of the highest sample row.</summary>
        public float Top => (FirstSample.y + SampleCount.y - 1) * VoxelSize;

        /// <summary>The block's bounds in world units, relative to the world origin.</summary>
        public Bounds Bounds
        {
            get
            {
                float3 min = (float3)FirstSample * VoxelSize;
                float3 size = (float3)(SampleCount - 1) * VoxelSize;
                return new Bounds(min + size * 0.5f, size);
            }
        }

        /// <summary>Index of a local sample in arrays over the block.</summary>
        public int Index(int x, int y, int z)
        {
            return x + SampleCount.x * (y + SampleCount.y * z);
        }

        /// <summary>World position of a local sample, relative to the world origin.</summary>
        public float3 Position(int x, int y, int z)
        {
            return (float3)(FirstSample + new int3(x, y, z)) * VoxelSize;
        }

        /// <summary>The same columns, as a <see cref="ColumnBlock"/>.</summary>
        public ColumnBlock Columns => new ColumnBlock(FirstSample.xz, SampleCount.xz, VoxelSize);
    }

    /// <summary>
    /// The sample columns under a chunk (K35): every chunk stacked in the same column of the
    /// world shares them, so a heightfield computes them once for the whole stack.
    /// </summary>
    public struct ColumnBlock
    {
        /// <summary>Global sample (x, z) of the first column.</summary>
        public int2 FirstColumn;

        /// <summary>Columns along x and z.</summary>
        public int2 Count;

        public float VoxelSize;

        public ColumnBlock(int2 firstColumn, int2 count, float voxelSize)
        {
            FirstColumn = firstColumn;
            Count = count;
            VoxelSize = voxelSize;
        }

        public int Length => Count.x * Count.y;

        /// <summary>Index of a column in arrays over the block: x fastest, then z.</summary>
        public int Index(int x, int z)
        {
            return x + Count.x * z;
        }

        /// <summary>World (x, z) of a column, relative to the world origin.</summary>
        public float2 Position(int x, int z)
        {
            return (float2)(FirstColumn + new int2(x, z)) * VoxelSize;
        }
    }
}
