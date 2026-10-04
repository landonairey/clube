namespace Clube.Core
{
    /// <summary>
    /// What one <see cref="TerrainBrush"/> application changed: how many samples, and how
    /// much density it added and removed in total. Each sample stands for one voxel's worth
    /// of volume, so density × voxel size³ approximates the solid volume placed or dug.
    /// </summary>
    public readonly struct BrushResult
    {
        public BrushResult(int changedSamples, float densityAdded, float densityRemoved)
        {
            ChangedSamples = changedSamples;
            DensityAdded = densityAdded;
            DensityRemoved = densityRemoved;
        }

        public int ChangedSamples { get; }

        /// <summary>Total density added across the changed samples, 0 or more.</summary>
        public float DensityAdded { get; }

        /// <summary>Total density removed across the changed samples, 0 or more.</summary>
        public float DensityRemoved { get; }

        /// <summary>Approximate solid volume added, in world units³.</summary>
        public float VolumeAdded(float voxelSize)
        {
            return DensityAdded * voxelSize * voxelSize * voxelSize;
        }

        /// <summary>Approximate solid volume removed, in world units³.</summary>
        public float VolumeRemoved(float voxelSize)
        {
            return DensityRemoved * voxelSize * voxelSize * voxelSize;
        }
    }
}
