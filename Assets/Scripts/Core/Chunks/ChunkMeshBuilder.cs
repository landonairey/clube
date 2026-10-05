using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Clube.Core
{
    /// <summary>
    /// Builds a chunk's Unity mesh: runs the mesher the settings pick (<see cref="ChunkMesher"/>,
    /// or <see cref="BurstChunkMesher"/>, K12), uploads the result, adds normals and bounds, and
    /// times both halves (K4). Shared by the single-chunk <see cref="ChunkView"/> and the world's
    /// <see cref="ChunkRenderer"/>s, so every chunk is built the same way. Reuses its buffers
    /// between builds.
    /// </summary>
    public sealed class ChunkMeshBuilder : System.IDisposable
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        // The Burst mesher's buffers, made on its first use.
        private NativeList<float3> nativeVertices;
        private NativeList<int> nativeTriangles;

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
            int vertexCount;
            int indexCount;
            var stopwatch = Stopwatch.StartNew();
            if (settings.Backend == MesherBackend.Burst)
            {
                EnsureNativeBuffers();
                BurstChunkMesher.Build(chunk, settings, nativeVertices, nativeTriangles);
                vertexCount = nativeVertices.Length;
                indexCount = nativeTriangles.Length;
            }
            else
            {
                ChunkMesher.Build(chunk, settings, vertices, triangles);
                vertexCount = vertices.Count;
                indexCount = triangles.Count;
            }
            double meshingMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            Mesh.Clear();

            // 16-bit indices top out at 65,535 vertices, which a 32³ chunk can pass.
            Mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            if (settings.Backend == MesherBackend.Burst)
            {
                Mesh.SetVertices(nativeVertices.AsArray());
                Mesh.SetIndices(nativeTriangles.AsArray(), MeshTopology.Triangles, 0);
            }
            else
            {
                Mesh.SetVertices(vertices);
                Mesh.SetTriangles(triangles, 0);
            }
            Mesh.RecalculateNormals();
            LastStats = new ChunkMeshStats(
                vertexCount, indexCount / 3, meshingMilliseconds, stopwatch.Elapsed.TotalMilliseconds);

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
            if (nativeVertices.IsCreated)
            {
                nativeVertices.Dispose();
                nativeTriangles.Dispose();
            }
        }

        private void EnsureNativeBuffers()
        {
            if (!nativeVertices.IsCreated)
            {
                nativeVertices = new NativeList<float3>(Allocator.Persistent);
                nativeTriangles = new NativeList<int>(Allocator.Persistent);
            }
        }
    }
}
