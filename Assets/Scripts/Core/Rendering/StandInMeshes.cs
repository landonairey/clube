using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// GL718: the unit meshes of the building stand-ins (<see cref="StandInShape"/>), flat-shaded and vertex-coloured, and
    /// a settlement's stand-ins merged into one mesh for the farthest level.
    /// </summary>
    public static class StandInMeshes
    {
        /// <summary>The unit mesh of a shape in one colour (linear: the shader takes vertex colours as they are).</summary>
        public static UnityEngine.Mesh Unit(StandInShape shape, Color colour)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            AddShape(shape, Matrix4x4.identity, vertices, normals, triangles);
            return Build(shape.ToString(), vertices, normals, Filled(colour, vertices.Count), triangles);
        }

        /// <summary>
        /// Parts merged into one mesh around <paramref name="origin"/> (the mesh's own zero), each in its shape's colour,
        /// indexed by <see cref="StandInShape"/>.
        /// </summary>
        public static UnityEngine.Mesh Merge(IReadOnlyList<StandInPart> parts, Vector3 origin, IReadOnlyList<Color> colours)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colourList = new List<Color>();
            var triangles = new List<int>();
            Matrix4x4 back = Matrix4x4.Translate(-origin);
            foreach (StandInPart part in parts)
            {
                int from = vertices.Count;
                AddShape(part.Shape, back * part.Matrix, vertices, normals, triangles);
                Color colour = colours[(int)part.Shape];
                for (int i = from; i < vertices.Count; i++)
                {
                    colourList.Add(colour);
                }
            }
            return Build("Settlement stand-in", vertices, normals, colourList, triangles);
        }

        private static List<Color> Filled(Color colour, int count)
        {
            var list = new List<Color>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(colour);
            }
            return list;
        }

        private static UnityEngine.Mesh Build(string name, List<Vector3> vertices, List<Vector3> normals, List<Color> colours, List<int> triangles)
        {
            var mesh = new UnityEngine.Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (vertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddShape(StandInShape shape, Matrix4x4 matrix, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            if (shape == StandInShape.Roof)
            {
                AddRoof(matrix, vertices, normals, triangles);
            }
            else
            {
                AddBox(matrix, vertices, normals, triangles);
            }
        }

        // A box over x, z ±0.5 and y 0 to 1: its four sides and top (the bottom is under the ground).
        private static void AddBox(Matrix4x4 m, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            var p000 = new Vector3(-0.5f, 0f, -0.5f);
            var p100 = new Vector3(0.5f, 0f, -0.5f);
            var p001 = new Vector3(-0.5f, 0f, 0.5f);
            var p101 = new Vector3(0.5f, 0f, 0.5f);
            Vector3 up = Vector3.up;
            Quad(m, v, n, t, p000, p000 + up, p100 + up, p100);
            Quad(m, v, n, t, p101, p101 + up, p001 + up, p001);
            Quad(m, v, n, t, p001, p001 + up, p000 + up, p000);
            Quad(m, v, n, t, p100, p100 + up, p101 + up, p101);
            Quad(m, v, n, t, p000 + up, p001 + up, p101 + up, p100 + up);
        }

        // A gable roof: eaves at y 0 along z ±0.5, the ridge at y 1 over z 0, the gables at x ±0.5.
        private static void AddRoof(Matrix4x4 m, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            var frontLeft = new Vector3(-0.5f, 0f, 0.5f);
            var frontRight = new Vector3(0.5f, 0f, 0.5f);
            var backLeft = new Vector3(-0.5f, 0f, -0.5f);
            var backRight = new Vector3(0.5f, 0f, -0.5f);
            var ridgeLeft = new Vector3(-0.5f, 1f, 0f);
            var ridgeRight = new Vector3(0.5f, 1f, 0f);
            Quad(m, v, n, t, frontRight, ridgeRight, ridgeLeft, frontLeft);
            Quad(m, v, n, t, backLeft, ridgeLeft, ridgeRight, backRight);
            Triangle(m, v, n, t, frontLeft, ridgeLeft, backLeft);
            Triangle(m, v, n, t, backRight, ridgeRight, frontRight);
        }

        // Corners in clockwise order seen from outside (Unity's front faces).
        private static void Quad(Matrix4x4 m, List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(m, v, n, t, a, b, c);
            Triangle(m, v, n, t, a, c, d);
        }

        private static void Triangle(Matrix4x4 m, List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 wa = m.MultiplyPoint3x4(a), wb = m.MultiplyPoint3x4(b), wc = m.MultiplyPoint3x4(c);
            Vector3 normal = Vector3.Cross(wb - wa, wc - wa).normalized;
            int start = v.Count;
            v.Add(wa);
            v.Add(wb);
            v.Add(wc);
            n.Add(normal);
            n.Add(normal);
            n.Add(normal);
            t.Add(start);
            t.Add(start + 1);
            t.Add(start + 2);
        }
    }
}
