using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Debug
{
    /// <summary>
    /// Where <see cref="StorageMesher"/> writes a mesh, for the K11 storage benchmark.
    /// Implemented by structs and used as a generic constraint, so each storage gets
    /// its own compiled copy of the mesher with direct (non-virtual) calls and the
    /// benchmark compares storage alone.
    /// </summary>
    internal interface IMeshStorage
    {
        int VertexCount { get; }

        int IndexCount { get; }

        /// <summary>Memory the storage keeps between builds, in bytes.</summary>
        long RetainedBytes { get; }

        void Clear();

        /// <returns>The new vertex's index.</returns>
        int AddVertex(Vector3 position);

        void AddIndex(int index);

        /// <summary>Hands the vertices and indices to <paramref name="mesh"/> (no normals).</summary>
        void Upload(Mesh mesh);

        /// <summary>Copies the result out, to check it against the core mesher.</summary>
        void CopyTo(List<Vector3> vertices, List<int> indices);
    }

    /// <summary><see cref="List{T}"/> storage, either reused between builds or new for each one.</summary>
    internal struct ListMeshStorage : IMeshStorage
    {
        private readonly bool reuse;
        private List<Vector3> vertices;
        private List<int> indices;

        public ListMeshStorage(bool reuse)
        {
            this.reuse = reuse;
            vertices = new List<Vector3>();
            indices = new List<int>();
        }

        public int VertexCount => vertices.Count;

        public int IndexCount => indices.Count;

        public long RetainedBytes => reuse ? vertices.Capacity * 12L + indices.Capacity * 4L : 0L;

        public void Clear()
        {
            if (reuse)
            {
                vertices.Clear();
                indices.Clear();
            }
            else
            {
                vertices = new List<Vector3>();
                indices = new List<int>();
            }
        }

        public int AddVertex(Vector3 position)
        {
            vertices.Add(position);
            return vertices.Count - 1;
        }

        public void AddIndex(int index)
        {
            indices.Add(index);
        }

        public void Upload(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
        }

        public void CopyTo(List<Vector3> vertexCopy, List<int> indexCopy)
        {
            vertexCopy.Clear();
            vertexCopy.AddRange(vertices);
            indexCopy.Clear();
            indexCopy.AddRange(indices);
        }
    }

    /// <summary>
    /// Plain arrays allocated once, big enough for the worst case (5 triangles, so
    /// 15 vertices and 15 indices, in every voxel), filled with running counts.
    /// </summary>
    internal struct ArrayMeshStorage : IMeshStorage
    {
        /// <summary>Most triangle corners one voxel can produce: 5 triangles of 3.</summary>
        public const int MaxCornersPerVoxel = 15;

        private readonly Vector3[] vertices;
        private readonly int[] indices;
        private int vertexCount;
        private int indexCount;

        public ArrayMeshStorage(int voxelCount)
        {
            vertices = new Vector3[voxelCount * MaxCornersPerVoxel];
            indices = new int[voxelCount * MaxCornersPerVoxel];
            vertexCount = 0;
            indexCount = 0;
        }

        public int VertexCount => vertexCount;

        public int IndexCount => indexCount;

        public long RetainedBytes => vertices.Length * 12L + indices.Length * 4L;

        public void Clear()
        {
            vertexCount = 0;
            indexCount = 0;
        }

        public int AddVertex(Vector3 position)
        {
            vertices[vertexCount] = position;
            return vertexCount++;
        }

        public void AddIndex(int index)
        {
            indices[indexCount++] = index;
        }

        public void Upload(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices, 0, vertexCount);
            mesh.SetTriangles(indices, 0, indexCount, 0);
        }

        public void CopyTo(List<Vector3> vertexCopy, List<int> indexCopy)
        {
            vertexCopy.Clear();
            indexCopy.Clear();
            for (int i = 0; i < vertexCount; i++)
            {
                vertexCopy.Add(vertices[i]);
            }
            for (int i = 0; i < indexCount; i++)
            {
                indexCopy.Add(indices[i]);
            }
        }
    }
}
