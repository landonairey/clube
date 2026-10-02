using System.Collections.Generic;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>Small procedural meshes for lab visuals.</summary>
    public static class LabMeshes
    {
        /// <summary>
        /// Vertices and triangles of a unit-radius sphere: an icosahedron with each
        /// face split into four, 42 vertices and 80 triangles. Round enough for a
        /// marker, cheap enough to repeat tens of thousands of times.
        /// </summary>
        public static void Icosphere(List<Vector3> vertices, List<int> triangles)
        {
            vertices.Clear();
            triangles.Clear();

            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var corners = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            foreach (Vector3 corner in corners)
            {
                vertices.Add(corner.normalized);
            }

            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            // One shared midpoint vertex per icosahedron edge.
            var midpoints = new Dictionary<long, int>();
            int Midpoint(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!midpoints.TryGetValue(key, out int index))
                {
                    index = vertices.Count;
                    vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
                    midpoints.Add(key, index);
                }
                return index;
            }

            for (int i = 0; i < faces.Length; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                triangles.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
        }
    }
}
