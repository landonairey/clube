using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A chunk mesh with materials (M13, M15): positions, normals and triangles as usual, plus
    /// for every vertex the three material ids of its triangle (<see cref="MaterialIds"/>,
    /// UV channel 2) and how much of each it shows (<see cref="MaterialWeights"/>, UV channel
    /// 3). The terrain shader samples the three materials and mixes them by the weights, which
    /// the GPU interpolates across the triangle. Filled by an <see cref="IMaterialSplitter"/>.
    /// </summary>
    public sealed class MaterialMesh
    {
        public List<Vector3> Vertices { get; } = new List<Vector3>();

        public List<Vector3> Normals { get; } = new List<Vector3>();

        public List<int> Triangles { get; } = new List<int>();

        public List<Vector3> MaterialIds { get; } = new List<Vector3>();

        public List<Vector3> MaterialWeights { get; } = new List<Vector3>();

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Triangles.Clear();
            MaterialIds.Clear();
            MaterialWeights.Clear();
        }

        /// <summary>Adds a vertex and returns its index.</summary>
        public int AddVertex(Vector3 position, Vector3 normal, Vector3 ids, Vector3 weights)
        {
            Vertices.Add(position);
            Normals.Add(normal);
            MaterialIds.Add(ids);
            MaterialWeights.Add(weights);
            return Vertices.Count - 1;
        }
    }
}
