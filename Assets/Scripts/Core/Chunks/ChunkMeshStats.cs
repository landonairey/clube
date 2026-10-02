namespace Clube.Core
{
    /// <summary>
    /// Read-only figures from a chunk's last mesh build (K4), exposed for lab
    /// readouts (A4) rather than the core calling into debug code.
    /// </summary>
    public readonly struct ChunkMeshStats
    {
        public ChunkMeshStats(int vertexCount, int triangleCount, double meshingMilliseconds, double uploadMilliseconds)
        {
            VertexCount = vertexCount;
            TriangleCount = triangleCount;
            MeshingMilliseconds = meshingMilliseconds;
            UploadMilliseconds = uploadMilliseconds;
        }

        public int VertexCount { get; }

        public int TriangleCount { get; }

        /// <summary>Time spent in <see cref="ChunkMesher.Build"/>: Marching Cubes itself.</summary>
        public double MeshingMilliseconds { get; }

        /// <summary>Time spent handing the result to Unity: setting the mesh data and recalculating normals.</summary>
        public double UploadMilliseconds { get; }

        public double TotalMilliseconds => MeshingMilliseconds + UploadMilliseconds;
    }
}
