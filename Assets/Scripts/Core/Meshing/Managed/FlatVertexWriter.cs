using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Writes a new vertex for every triangle corner. No vertex is shared, so
    /// recalculated normals are per face: flat shading.
    /// </summary>
    public sealed class FlatVertexWriter : IVertexWriter
    {
        private readonly List<Vector3> vertices;

        public FlatVertexWriter(List<Vector3> vertices)
        {
            this.vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        }

        public void BeginVoxel(Vector3Int voxel)
        {
        }

        public int Write(int edge, Vector3 position)
        {
            vertices.Add(position);
            return vertices.Count - 1;
        }
    }
}
