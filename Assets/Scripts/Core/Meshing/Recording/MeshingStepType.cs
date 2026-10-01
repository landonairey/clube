namespace Clube.Core
{
    /// <summary>
    /// The stages Marching Cubes goes through for each voxel, in the order
    /// <see cref="MeshingRecorder"/> logs them (V15).
    /// </summary>
    public enum MeshingStepType
    {
        /// <summary>The voxel's 8 corner densities are read from the chunk.</summary>
        ReadCorners,

        /// <summary>Each corner is compared with the iso level: solid or empty.</summary>
        Classify,

        /// <summary>The solid corners' bits are combined into the 8-bit case index.</summary>
        CaseIndex,

        /// <summary>The case index gives the edges the surface crosses.</summary>
        EdgeTable,

        /// <summary>A vertex is placed on one crossed edge. One step per crossed edge.</summary>
        Interpolate,

        /// <summary>A triangle from the triangle table is emitted. One step per triangle.</summary>
        Triangle,
    }
}
