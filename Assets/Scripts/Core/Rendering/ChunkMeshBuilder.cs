using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Clube.Core
{
    /// <summary>
    /// Builds one chunk's Unity mesh on the main thread and waits for it: the single-chunk
    /// labs' path (<see cref="ChunkView"/>). The settings pick the mesher (K12):
    /// <list type="bullet">
    /// <item><b>Managed</b>: <see cref="ChunkMesher"/>, then, with a <see cref="MaterialDisplay"/>
    /// set, the managed material pass (M13, M15): each vertex takes its edge's solid-end material
    /// and the vertices are split for hard seams or blending, with normals computed first so the
    /// split leaves no creases. The teaching path, and what step-through records (A11).</item>
    /// <item><b>Burst</b>: the whole build as one <see cref="ChunkMeshJob"/>, the same job the
    /// streamed world runs in parallel (<see cref="ChunkPipeline"/>).</item>
    /// </list>
    /// Times both halves (K4) and reuses its buffers between builds.
    /// </summary>
    public sealed class ChunkMeshBuilder : System.IDisposable
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        // The material pass's buffers (M13, M15).
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<byte> vertexMaterials = new List<byte>();
        private readonly MaterialMesh materialMesh = new MaterialMesh();

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
            if (settings.Backend == MesherBackend.Burst)
            {
                BuildWithJob(chunk, settings);
            }
            else
            {
                BuildManaged(chunk, settings);
            }
            chunk.MarkClean();
        }

        public void Dispose()
        {
            // Tests and benchmarks build meshes outside Play mode, where only DestroyImmediate works.
            if (Application.isPlaying)
            {
                Object.Destroy(Mesh);
            }
            else
            {
                Object.DestroyImmediate(Mesh);
            }
        }

        // One job does the meshing, materials and buffers; applying it to the mesh is the upload.
        private void BuildWithJob(Chunk chunk, ChunkMeshSettings settings)
        {
            var stopwatch = Stopwatch.StartNew();
            Mesh.MeshDataArray data = Mesh.AllocateWritableMeshData(1);
            ChunkMeshJobs.Schedule(ChunkMeshInput.Snapshot(chunk, Allocator.TempJob), settings, data).Complete();
            double meshingMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            ChunkMeshJobs.Apply(data, Mesh, chunk.SampleCount, settings.VoxelSize);
            LastStats = new ChunkMeshStats(
                Mesh.vertexCount, (int)Mesh.GetIndexCount(0) / 3, meshingMilliseconds, stopwatch.Elapsed.TotalMilliseconds);
        }

        private void BuildManaged(Chunk chunk, ChunkMeshSettings settings)
        {
            var stopwatch = Stopwatch.StartNew();
            ChunkMesher.Build(chunk, settings, vertices, triangles);
            int vertexCount = vertices.Count;
            int indexCount = triangles.Count;
            bool withMaterials = settings.MaterialDisplay != MaterialDisplay.None;
            if (withMaterials)
            {
                BuildMaterials(chunk, settings);
                vertexCount = materialMesh.Vertices.Count;
                indexCount = materialMesh.Triangles.Count;
            }
            double meshingMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            Mesh.Clear();

            // 16-bit indices top out at 65,535 vertices, which a 32³ chunk can pass.
            Mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            if (withMaterials)
            {
                Mesh.SetVertices(materialMesh.Vertices);
                Mesh.SetNormals(materialMesh.Normals);
                Mesh.SetUVs(2, materialMesh.MaterialIds);
                Mesh.SetUVs(3, materialMesh.MaterialWeights);
                Mesh.SetTriangles(materialMesh.Triangles, 0);
            }
            else
            {
                Mesh.SetVertices(vertices);
                Mesh.SetTriangles(triangles, 0);
                Mesh.RecalculateNormals();
            }

            // Bounds cover the whole chunk rather than just the current surface, so
            // the renderer (and gizmos Unity culls with it) stays visible whenever
            // any part of the chunk is in view.
            Vector3 chunkSize = (Vector3)chunk.VoxelCount * settings.VoxelSize;
            Mesh.bounds = new Bounds(chunkSize * 0.5f, chunkSize);
            LastStats = new ChunkMeshStats(vertexCount, indexCount / 3, meshingMilliseconds, stopwatch.Elapsed.TotalMilliseconds);
        }

        // Material ids per vertex, normals before splitting, then the display's splitter (A6).
        private void BuildMaterials(Chunk chunk, ChunkMeshSettings settings)
        {
            VertexMaterialSampler.Assign(chunk, settings.IsoLevel, settings.VoxelSize, vertices, vertexMaterials);
            MeshNormals.Compute(vertices, triangles, normals);
            MaterialSplitters.For(settings.MaterialDisplay).Split(vertices, normals, triangles, vertexMaterials, materialMesh);
        }
    }
}
