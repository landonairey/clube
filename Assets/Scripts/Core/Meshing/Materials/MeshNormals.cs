using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Vertex normals as <c>Mesh.RecalculateNormals</c> makes them: each triangle adds its
    /// area-weighted face normal to its vertices. Computed before the material pass splits
    /// vertices (M15), so a material border doesn't show up as a crease in the lighting.
    /// </summary>
    public static class MeshNormals
    {
        /// <param name="normals">Cleared, then one unit normal per vertex.</param>
        public static void Compute(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, List<Vector3> normals)
        {
            normals.Clear();
            for (int i = 0; i < vertices.Count; i++)
            {
                normals.Add(Vector3.zero);
            }

            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                // Unnormalized, so larger triangles count for more.
                Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                normals[a] += face;
                normals[b] += face;
                normals[c] += face;
            }

            for (int i = 0; i < normals.Count; i++)
            {
                normals[i] = normals[i].normalized;
            }
        }
    }
}
