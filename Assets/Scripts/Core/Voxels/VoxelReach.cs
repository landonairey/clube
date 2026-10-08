using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The voxels a tool reaches (GL1, GL24): a <see cref="VoxelBox"/>, either whole or
    /// <see cref="Rounded"/>. A rounded reach leaves out the voxels on the box's edges and
    /// corners, those on two or more of its faces, so a 4x4x4 box becomes a ball of 32 voxels.
    /// Only sides of 3 or more voxels are rounded; thinner ones have no inside to keep.
    /// </summary>
    public readonly struct VoxelReach
    {
        public VoxelReach(VoxelBox box, bool rounded)
        {
            Box = box;
            Rounded = rounded;
        }

        /// <summary>A cube of <paramref name="size"/> voxels per side around a voxel, rounded or not.</summary>
        public static VoxelReach Around(Vector3Int voxel, int size, bool rounded)
        {
            return new VoxelReach(VoxelBox.Around(voxel, size), rounded);
        }

        public VoxelBox Box { get; }

        public bool Rounded { get; }

        public bool Contains(Vector3Int voxel)
        {
            if (!Box.Contains(voxel))
            {
                return false;
            }
            if (!Rounded)
            {
                return true;
            }
            int faces = OnFace(voxel.x, Box.Min.x, Box.Max.x) + OnFace(voxel.y, Box.Min.y, Box.Max.y) + OnFace(voxel.z, Box.Min.z, Box.Max.z);
            return faces <= 1;
        }

        /// <summary>True when any of the 8 voxels sharing a sample (corner) is in the reach.</summary>
        public bool TouchesSample(Vector3Int sample)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                var offset = new Vector3Int(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                if (Contains(sample - offset))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How many voxels it holds.</summary>
        public int Count
        {
            get
            {
                int count = 0;
                for (int z = Box.Min.z; z <= Box.Max.z; z++)
                {
                    for (int y = Box.Min.y; y <= Box.Max.y; y++)
                    {
                        for (int x = Box.Min.x; x <= Box.Max.x; x++)
                        {
                            if (Contains(new Vector3Int(x, y, z)))
                            {
                                count++;
                            }
                        }
                    }
                }
                return count;
            }
        }

        // 1 when the voxel lies on one of the box's faces across this axis, for sides of 3 or more.
        private static int OnFace(int value, int min, int max)
        {
            return max - min >= 2 && (value == min || value == max) ? 1 : 0;
        }

        public static implicit operator VoxelReach(VoxelBox box)
        {
            return new VoxelReach(box, false);
        }
    }
}
