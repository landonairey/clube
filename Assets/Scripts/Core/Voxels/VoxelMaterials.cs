using Unity.Collections;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The material id of every sample in a chunk (M10), one byte each, beside the densities
    /// in its <see cref="IVoxelStorage"/>. Starts uniform (every sample the same id, 0 unless
    /// given) and only allocates its array once a sample gets another material, so chunks of
    /// one material (the labs' test shapes, a world's air above the ground) cost nothing.
    /// </summary>
    /// <remarks>
    /// Kept apart from the density schemes (2G) on purpose: they compress densities in
    /// different ways, and materials need no comparison yet. A sample's material only
    /// matters while it is solid; the mesher's material pass reads the solid end of each
    /// surface edge (<see cref="VertexMaterialSampler"/>).
    /// </remarks>
    public sealed class VoxelMaterials
    {
        private byte[] ids;
        private byte uniformId;

        public VoxelMaterials(Vector3Int sampleCount, byte uniformId = 0)
        {
            SampleGrid.Validate(sampleCount);
            SampleCount = sampleCount;
            this.uniformId = uniformId;
        }

        public Vector3Int SampleCount { get; }

        /// <summary>Bytes held: 0 while every sample has the same id.</summary>
        public long MemoryBytes => ids?.Length ?? 0;

        /// <summary>True while every sample has <see cref="UniformId"/> and no array is held.</summary>
        public bool IsUniform => ids == null;

        /// <summary>The id every sample has while <see cref="IsUniform"/>.</summary>
        public byte UniformId => uniformId;

        public byte Get(int x, int y, int z)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            return ids == null ? uniformId : ids[SampleGrid.FlatIndex(SampleCount, x, y, z)];
        }

        public void Set(int x, int y, int z, byte id)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            if (ids == null)
            {
                if (id == uniformId)
                {
                    return;
                }
                ids = VoxelArrayPool.Rent(SampleCount.x * SampleCount.y * SampleCount.z);
                if (uniformId != 0)
                {
                    System.Array.Fill(ids, uniformId);
                }
            }
            ids[SampleGrid.FlatIndex(SampleCount, x, y, z)] = id;
        }

        /// <summary>Makes every sample <paramref name="id"/>, giving the array back to <see cref="VoxelArrayPool"/>.</summary>
        public void SetUniform(byte id)
        {
            VoxelArrayPool.Return(ids);
            ids = null;
            uniformId = id;
        }

        /// <summary>Overwrites every sample's id from a native array of the same length (a generation job's output).</summary>
        public void CopyFrom(NativeArray<byte> source)
        {
            ids ??= VoxelArrayPool.Rent(SampleCount.x * SampleCount.y * SampleCount.z);
            source.CopyTo(ids);
        }

        /// <summary>
        /// Copies every sample's id into a native array of the same length (a mesh job's input).
        /// Returns false, copying nothing, while uniform: <see cref="UniformId"/> says it all.
        /// </summary>
        public bool TryCopyTo(NativeArray<byte> destination)
        {
            if (ids == null)
            {
                return false;
            }
            destination.CopyFrom(ids);
            return true;
        }
    }
}
