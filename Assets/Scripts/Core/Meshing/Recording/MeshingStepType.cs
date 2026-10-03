namespace Clube.Core
{
    /// <summary>
    /// The stages of a Marching Cubes build, in the order <see cref="MeshingRecorder"/>
    /// logs them (V15): the density field once, then the rest per voxel, then the
    /// normals once (K18).
    /// </summary>
    public enum MeshingStepType
    {
        /// <summary>The terrain data: the chunk's 3D grid of density samples. Once per build.</summary>
        DensityField,

        /// <summary>The voxel's 8 corners are sampled and compared with the iso level: solid or empty.</summary>
        SampleCorners,

        /// <summary>The 8 solid/empty bits are combined into the case index, the row to look up in the tables.</summary>
        CaseIndex,

        /// <summary>The edge table's row for the case gives the edges the surface crosses.</summary>
        EdgeTable,

        /// <summary>A vertex is placed on one crossed edge. One step per crossed edge.</summary>
        Interpolate,

        /// <summary>A triangle from the triangle table's row is emitted. One step per triangle.</summary>
        Triangle,

        /// <summary>
        /// The finished mesh gets its vertex normals, for lighting. Once per build, after
        /// the last voxel. The mesher doesn't compute them: <see cref="ChunkView"/> does,
        /// with Unity's Mesh.RecalculateNormals. The step marks where that happens.
        /// </summary>
        Normals,
    }
}
