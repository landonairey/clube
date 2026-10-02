using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Draws a voxel's tetrahedra (V13) as real meshes: translucent faces and solid
    /// edges, each tetrahedron in its own colour and pushed out from the cube centre
    /// by the explode distance. Red is reserved for an inside-out (negative)
    /// tetrahedron, which would be a bug. Positions are in unit-voxel space.
    /// </summary>
    /// <remarks>
    /// Meshes rather than gizmos, which dim wrongly in this project (see
    /// <see cref="ChunkDebugView"/>). All faces share one mesh and all edges another.
    /// </remarks>
    public sealed class TetrahedraView : IDisposable
    {
        private static readonly Color NegativeColor = new Color(1f, 0.25f, 0.2f);
        private static readonly Vector3 CubeCentre = new Vector3(0.5f, 0.5f, 0.5f);

        private readonly LabMeshObject faces;
        private readonly LabMeshObject edges;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> indices = new List<int>();

        /// <param name="parent">Transform the unit-voxel positions are relative to.</param>
        /// <param name="faceMaterial">Transparent material that shows vertex colours and alpha.</param>
        /// <param name="edgeMaterial">Material that shows vertex colours.</param>
        public TetrahedraView(Transform parent, Material faceMaterial, Material edgeMaterial)
        {
            faces = new LabMeshObject(parent, "Tetrahedra Faces", faceMaterial);
            edges = new LabMeshObject(parent, "Tetrahedra Edges", edgeMaterial);
        }

        public bool Visible
        {
            set
            {
                faces.Visible = value;
                edges.Visible = value;
            }
        }

        /// <summary>Edge length of the voxel the tetrahedra fill, in the parent's units.</summary>
        public float Scale
        {
            set
            {
                faces.Transform.localScale = Vector3.one * value;
                edges.Transform.localScale = Vector3.one * value;
            }
        }

        public void Show(IReadOnlyList<Tetrahedron> tetrahedra, float explodeDistance, float faceOpacity)
        {
            BuildFaces(tetrahedra, explodeDistance, faceOpacity);
            BuildEdges(tetrahedra, explodeDistance);
        }

        public void Dispose()
        {
            faces.Dispose();
            edges.Dispose();
        }

        // Four outward-facing triangles per tetrahedron. A negative tetrahedron is
        // inside out, so two corners are swapped to keep its faces pointing outwards.
        private void BuildFaces(IReadOnlyList<Tetrahedron> tetrahedra, float explodeDistance, float faceOpacity)
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();

            int positiveIndex = 0;
            foreach (Tetrahedron tetrahedron in tetrahedra)
            {
                Color color = ColorOf(tetrahedron, ref positiveIndex);
                color.a = faceOpacity;
                Vector3 offset = Offset(tetrahedron, explodeDistance);

                bool positive = tetrahedron.SignedVolume >= 0f;
                Vector3 p0 = tetrahedron.Apex + offset;
                Vector3 p1 = tetrahedron.A + offset;
                Vector3 p2 = (positive ? tetrahedron.B : tetrahedron.C) + offset;
                Vector3 p3 = (positive ? tetrahedron.C : tetrahedron.B) + offset;

                foreach (Vector3 corner in new[] { p1, p2, p3, p0, p3, p2, p0, p1, p3, p0, p2, p1 })
                {
                    indices.Add(vertices.Count);
                    vertices.Add(corner);
                    colors.Add(color);
                }
            }

            Mesh mesh = faces.Mesh;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
        }

        private void BuildEdges(IReadOnlyList<Tetrahedron> tetrahedra, float explodeDistance)
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();

            int positiveIndex = 0;
            foreach (Tetrahedron tetrahedron in tetrahedra)
            {
                Color color = ColorOf(tetrahedron, ref positiveIndex);
                Vector3 offset = Offset(tetrahedron, explodeDistance);
                Vector3 apex = tetrahedron.Apex + offset;
                Vector3 a = tetrahedron.A + offset;
                Vector3 b = tetrahedron.B + offset;
                Vector3 c = tetrahedron.C + offset;

                AddLine(apex, a, color);
                AddLine(apex, b, color);
                AddLine(apex, c, color);
                AddLine(a, b, color);
                AddLine(b, c, color);
                AddLine(c, a, color);
            }

            Mesh mesh = edges.Mesh;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
        }

        private void AddLine(Vector3 from, Vector3 to, Color color)
        {
            indices.Add(vertices.Count);
            vertices.Add(from);
            colors.Add(color);
            indices.Add(vertices.Count);
            vertices.Add(to);
            colors.Add(color);
        }

        private static Vector3 Offset(Tetrahedron tetrahedron, float explodeDistance)
        {
            Vector3 outwards = tetrahedron.Centroid - CubeCentre;
            return outwards.sqrMagnitude > 1e-8f ? outwards.normalized * explodeDistance : Vector3.zero;
        }

        private static Color ColorOf(Tetrahedron tetrahedron, ref int positiveIndex)
        {
            return tetrahedron.SignedVolume >= 0f ? DistinctColor(positiveIndex++) : NegativeColor;
        }

        // Golden-ratio hue steps keep neighbouring tetrahedra visibly different. Hues
        // stay between orange and violet, leaving red for negative tetrahedra.
        private static Color DistinctColor(int index)
        {
            float hue = 0.08f + (index * 0.618034f % 1f) * 0.8f;
            return Color.HSVToRGB(hue, 0.6f, 1f);
        }
    }
}
