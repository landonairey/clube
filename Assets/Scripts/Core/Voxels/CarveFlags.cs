namespace Clube.Core
{
    /// <summary>
    /// A sample's carve flags (GL92): a voxel the chisel has carved out is marked on two of its
    /// corners, its lowest (<see cref="Above"/>) and its highest (<see cref="Below"/>). Border samples
    /// are copied between chunks, so each chunk also knows the carved voxels just outside it on
    /// every side, which its walls need.
    /// </summary>
    public static class CarveFlags
    {
        /// <summary>The voxel this sample is the lowest corner of is carved.</summary>
        public const byte Above = 1;

        /// <summary>The voxel this sample is the highest corner of is carved.</summary>
        public const byte Below = 2;
    }
}
