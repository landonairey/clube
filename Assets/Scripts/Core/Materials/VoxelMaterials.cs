using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The material id of every sample in a chunk (M10), one byte each, beside the densities
    /// in its <see cref="IVoxelStorage"/>. Starts uniform (every sample id 0) and only
    /// allocates its array once a sample gets another material, so chunks that never get
    /// materials (the voxel and chunk labs' test shapes) cost nothing.
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

        public VoxelMaterials(Vector3Int sampleCount)
        {
            SampleGrid.Validate(sampleCount);
            SampleCount = sampleCount;
        }

        public Vector3Int SampleCount { get; }

        /// <summary>Bytes held: 0 while every sample is still id 0.</summary>
        public long MemoryBytes => ids?.Length ?? 0;

        public byte Get(int x, int y, int z)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            return ids == null ? (byte)0 : ids[SampleGrid.FlatIndex(SampleCount, x, y, z)];
        }

        public void Set(int x, int y, int z, byte id)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            if (ids == null)
            {
                if (id == 0)
                {
                    return;
                }
                ids = new byte[SampleCount.x * SampleCount.y * SampleCount.z];
            }
            ids[SampleGrid.FlatIndex(SampleCount, x, y, z)] = id;
        }
    }
}
