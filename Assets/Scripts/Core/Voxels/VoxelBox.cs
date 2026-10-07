using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A box of voxels by global voxel index, both ends included, e.g. what a tool reaches
    /// (GL1). Voxel v spans samples v to v + 1 on each axis.
    /// </summary>
    public readonly struct VoxelBox
    {
        public VoxelBox(Vector3Int min, Vector3Int max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>A cube of <paramref name="size"/> voxels per side around a voxel (centred for odd sizes).</summary>
        public static VoxelBox Around(Vector3Int voxel, int size)
        {
            size = Mathf.Max(1, size);
            Vector3Int min = voxel - Vector3Int.one * ((size - 1) / 2);
            return new VoxelBox(min, min + Vector3Int.one * (size - 1));
        }

        public Vector3Int Min { get; }

        public Vector3Int Max { get; }

        /// <summary>The box's first sample: corner 0 of its first voxel.</summary>
        public Vector3Int MinSample => Min;

        /// <summary>The box's last sample: the far corner of its last voxel.</summary>
        public Vector3Int MaxSample => Max + Vector3Int.one;

        public bool Contains(Vector3Int voxel)
        {
            return voxel.x >= Min.x && voxel.y >= Min.y && voxel.z >= Min.z
                && voxel.x <= Max.x && voxel.y <= Max.y && voxel.z <= Max.z;
        }
    }
}
