using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The low-poly trees drawn far off (GL162), one unit tall (scaled to each tree), standing on
    /// their origin: a tapering trunk that runs on below the ground (the coarse terrain sits a
    /// little low, so none floats) and a crown of leaf blobs over the upper half, sized like the
    /// voxel trees'. Flat shaded, coloured by vertex (bark, leaves; pass linear colours in a linear
    /// project). The near one has a six-sided trunk and five blobs (about 110 triangles); the far
    /// one a three-sided trunk and one (26).
    /// </summary>
    public static class DistantTreeMeshes
    {
        /// <summary>The trunk's radius at the ground against the tree's height when none is given.</summary>
        public const float DefaultTrunkRadius = 0.045f;
        private const float TrunkBottom = -0.3f;
        private const float TrunkTop = 0.75f;

        public static Mesh Build(bool near, Color bark, Color leaves, float trunkRadius = DefaultTrunkRadius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            Trunk(near ? 6 : 3, trunkRadius, bark, vertices, normals, colors, triangles);
            // The voxel trees' crowns spread about 0.4 of the height either side and reach a little over the
            // top (branches from 0.45 to 0.82 up, angled upwards, with leaf clusters at their ends).
            if (near)
            {
                Blob(new Vector3(0f, 0.97f, 0f), 0.25f, leaves, vertices, normals, colors, triangles);
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * 0.5f + 0.4f;
                    var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.24f;
                    Blob(new Vector3(0f, 0.74f + (i % 2) * 0.06f, 0f) + offset, 0.2f, leaves * 0.9f, vertices, normals, colors, triangles);
                }
            }
            else
            {
                Blob(new Vector3(0f, 0.84f, 0f), 0.42f, leaves, vertices, normals, colors, triangles);
            }

            var mesh = new Mesh { name = near ? "Distant tree (near)" : "Distant tree (far)" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Trunk(int sides, float trunkRadius, Color bark, List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 b0 = d0 * trunkRadius + Vector3.up * TrunkBottom;
                Vector3 b1 = d1 * trunkRadius + Vector3.up * TrunkBottom;
                Vector3 t0 = d0 * (trunkRadius * 0.4f) + Vector3.up * TrunkTop;
                Vector3 t1 = d1 * (trunkRadius * 0.4f) + Vector3.up * TrunkTop;
                Quad(b0, t0, t1, b1, new Vector3(0f, (b0.y + t0.y) * 0.5f, 0f), bark, vertices, normals, colors, triangles);
            }
        }

        // An icosahedron: twenty flat faces, a blob of leaves at this distance.
        private static void Blob(Vector3 centre, float radius, Color leaves, List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int i = 0; i < faces.Length; i += 3)
            {
                // Flattened a little: crowns are wider than tall.
                Vector3 a = centre + Vector3.Scale(corners[faces[i]].normalized, new Vector3(1f, 0.8f, 1f)) * radius;
                Vector3 b = centre + Vector3.Scale(corners[faces[i + 1]].normalized, new Vector3(1f, 0.8f, 1f)) * radius;
                Vector3 c = centre + Vector3.Scale(corners[faces[i + 2]].normalized, new Vector3(1f, 0.8f, 1f)) * radius;
                Triangle(a, b, c, centre, leaves, vertices, normals, colors, triangles);
            }
        }

        private static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 middle, Color color, List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles)
        {
            Triangle(a, b, c, middle, color, vertices, normals, colors, triangles);
            Triangle(a, c, d, middle, color, vertices, normals, colors, triangles);
        }

        // A flat-shaded triangle facing away from <paramref name="middle"/>: wound clockwise seen from
        // outside (Unity's front face), so its cross product points out.
        private static void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 middle, Color color, List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, (a + b + c) / 3f - middle) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                normals.Add(normal);
                colors.Add(color);
            }
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }
    }
}
