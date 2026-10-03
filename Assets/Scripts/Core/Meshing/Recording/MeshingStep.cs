namespace Clube.Core
{
    /// <summary>
    /// One logged step of a recorded mesh build (V15). The voxel-wide data
    /// (corners, case index, edge mask, vertex positions) lives once in the
    /// <see cref="RecordedVoxel"/> at <see cref="VoxelIndex"/>; the step only
    /// adds what is specific to it.
    /// </summary>
    public readonly struct MeshingStep
    {
        public MeshingStep(MeshingStepType type, int voxelIndex, int edge = -1, EdgeTriangle triangle = default)
        {
            Type = type;
            VoxelIndex = voxelIndex;
            Edge = edge;
            Triangle = triangle;
        }

        public MeshingStepType Type { get; }

        /// <summary>Index into <see cref="MeshingRecorder.Voxels"/>; -1 for the once-per-build steps (<see cref="MeshingStepType.DensityField"/>, <see cref="MeshingStepType.Normals"/>).</summary>
        public int VoxelIndex { get; }

        /// <summary>The edge (0-11) given a vertex, for <see cref="MeshingStepType.Interpolate"/>; otherwise -1.</summary>
        public int Edge { get; }

        /// <summary>The triangle's three edges, for <see cref="MeshingStepType.Triangle"/>.</summary>
        public EdgeTriangle Triangle { get; }
    }

    /// <summary>A triangle as the three cube edges (0-11) its vertices sit on, in winding order.</summary>
    public readonly struct EdgeTriangle
    {
        public EdgeTriangle(int a, int b, int c)
        {
            A = a;
            B = b;
            C = c;
        }

        public int A { get; }

        public int B { get; }

        public int C { get; }
    }
}
