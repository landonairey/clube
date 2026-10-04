using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Clube.Core
{
    /// <summary>
    /// Builds a chunk's Unity mesh: runs <see cref="ChunkMesher"/>, uploads the result,
    /// adds normals and bounds, and times both halves (K4). Shared by the single-chunk
    /// <see cref="ChunkView"/> and the world's <see cref="ChunkRenderer"/>s, so every
    /// chunk is built the same way. Reuses its lists between builds.
    /// </summary>
    public sealed class ChunkMeshBuilder : System.IDisposable
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        public ChunkMeshBuilder(string meshName)
        {
            Mesh = new Mesh { name = meshName };
            Mesh.MarkDynamic();
        }

        public Mesh Mesh { get; }

        /// <summary>Counts and timings from the last build.</summary>
        public ChunkMeshStats LastStats { get; private set; }

        /// <summary>Rebuilds the mesh from the chunk and marks the chunk clean.</summary>
        public void Build(Chunk chunk, ChunkMeshSettings settings)
        {
            var stopwatch = Stopwatch.StartNew();
            ChunkMesher.Build(chunk, settings, vertices, triangles);
            double meshingMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            Mesh.Clear();

            // 16-bit indices top out at 65,535 vertices, which a 32³ chunk can pass.
            Mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            Mesh.SetVertices(vertices);
            Mesh.SetTriangles(triangles, 0);
            Mesh.RecalculateNormals();
            LastStats = new ChunkMeshStats(
                vertices.Count, triangles.Count / 3, meshingMilliseconds, stopwatch.Elapsed.TotalMilliseconds);

            // Bounds cover the whole chunk rather than just the current surface, so
            // the renderer (and gizmos Unity culls with it) stays visible whenever
            // any part of the chunk is in view.
            Vector3 chunkSize = (Vector3)chunk.VoxelCount * settings.VoxelSize;
            Mesh.bounds = new Bounds(chunkSize * 0.5f, chunkSize);

            chunk.MarkClean();
        }

        public void Dispose()
        {
            Object.Destroy(Mesh);
        }
    }
}
