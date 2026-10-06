using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Writes one vertex per grid edge and reuses it for every triangle touching
    /// that edge, including triangles in neighbouring voxels. Recalculated normals
    /// are then averaged across faces: smooth shading.
    /// </summary>
    public sealed class SharedVertexWriter : IVertexWriter
    {
        private readonly List<Vector3> vertices;
        private readonly Vector3Int sampleCount;
        private readonly Dictionary<int, int> vertexByEdgeKey = new Dictionary<int, int>();

        private Vector3Int voxel;

        /// <param name="vertices">Vertex list to append to.</param>
        /// <param name="sampleCount">Sample grid size of the chunk, used to key edges uniquely.</param>
        public SharedVertexWriter(List<Vector3> vertices, Vector3Int sampleCount)
        {
            this.vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            this.sampleCount = sampleCount;
        }

        public void BeginVoxel(Vector3Int voxel)
        {
            this.voxel = voxel;
        }

        public int Write(int edge, Vector3 position)
        {
            int key = EdgeKey(edge);
            if (!vertexByEdgeKey.TryGetValue(key, out int index))
            {
                index = vertices.Count;
                vertices.Add(position);
                vertexByEdgeKey.Add(key, index);
            }

            return index;
        }

        // Every grid edge is axis-aligned, so it is identified by its lower sample and its axis.
        private int EdgeKey(int edge)
        {
            Vector3Int a = voxel + MarchingCubes.CornerOffset(MarchingCubesTables.EdgeCorners[edge, 0]);
            Vector3Int b = voxel + MarchingCubes.CornerOffset(MarchingCubesTables.EdgeCorners[edge, 1]);
            Vector3Int lower = Vector3Int.Min(a, b);
            int axis = a.x != b.x ? 0 : a.y != b.y ? 1 : 2;

            int sample = lower.x + sampleCount.x * (lower.y + sampleCount.y * lower.z);
            return sample * 3 + axis;
        }
    }
}
