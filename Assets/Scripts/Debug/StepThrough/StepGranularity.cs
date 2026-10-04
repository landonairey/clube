namespace Clube.Debug
{
    /// <summary>
    /// How much of the recorded build one playback step covers (K19). The density
    /// field and the closing normals step are always a step of their own.
    /// </summary>
    public enum StepGranularity
    {
        /// <summary>One recorded step at a time: sample corners, case index, each vertex, each triangle.</summary>
        SubStep,

        /// <summary>A whole voxel at a time, its sub-steps played quickly inside it.</summary>
        Voxel,

        /// <summary>
        /// A whole Z slice at a time: every voxel with the same z. The mesher's outer
        /// loop runs over z, so a slice is one run of the build in true order (A11);
        /// a Y layer would not be.
        /// </summary>
        Slice,
    }
}
