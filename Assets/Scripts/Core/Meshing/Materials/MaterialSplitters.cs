using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Turns a plain chunk mesh plus a material per vertex into a <see cref="MaterialMesh"/>
    /// (M15). A mesh variant (A6): picked once per build by <see cref="MaterialSplitters.For"/>,
    /// never checked per vertex. A source vertex is copied once for every different set of
    /// material values it needs, so vertex counts differ by mode (the M15 readout).
    /// </summary>
    public interface IMaterialSplitter
    {
        /// <param name="materials">One material id per source vertex (<see cref="VertexMaterialSampler"/>).</param>
        void Split(
            IReadOnlyList<Vector3> vertices, IReadOnlyList<Vector3> normals, IReadOnlyList<int> triangles,
            IReadOnlyList<byte> materials, MaterialMesh output);
    }

    /// <summary>Picks the splitter for a <see cref="MaterialDisplay"/>.</summary>
    public static class MaterialSplitters
    {
        private static readonly IMaterialSplitter HardSeams = new HardSeamSplitter();
        private static readonly IMaterialSplitter Blended = new BlendedSplitter();

        public static IMaterialSplitter For(MaterialDisplay display)
        {
            switch (display)
            {
                case MaterialDisplay.HardSeams:
                case MaterialDisplay.DebugColours:
                    return HardSeams;
                case MaterialDisplay.Blended:
                    return Blended;
                default:
                    throw new ArgumentOutOfRangeException(nameof(display), display, "No material pass for this display.");
            }
        }
    }

    /// <summary>
    /// Hard seams: each triangle shows one material, the one most of its corners have (the
    /// first corner's when all three differ). A vertex shared by triangles of different
    /// materials is split, so no material bleeds into its neighbour.
    /// </summary>
    public sealed class HardSeamSplitter : IMaterialSplitter
    {
        private static readonly Vector3 Whole = new Vector3(1f, 0f, 0f);

        private readonly Dictionary<long, int> copies = new Dictionary<long, int>();

        public void Split(
            IReadOnlyList<Vector3> vertices, IReadOnlyList<Vector3> normals, IReadOnlyList<int> triangles,
            IReadOnlyList<byte> materials, MaterialMesh output)
        {
            output.Clear();
            copies.Clear();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                byte material = Dominant(materials[triangles[i]], materials[triangles[i + 1]], materials[triangles[i + 2]]);
                var ids = new Vector3(material, material, material);
                for (int corner = 0; corner < 3; corner++)
                {
                    int source = triangles[i + corner];
                    long key = ((long)material << 32) | (uint)source;
                    if (!copies.TryGetValue(key, out int index))
                    {
                        index = output.AddVertex(vertices[source], normals[source], ids, Whole);
                        copies.Add(key, index);
                    }
                    output.Triangles.Add(index);
                }
            }
        }

        /// <summary>The material at least two corners share, or the first corner's.</summary>
        public static byte Dominant(byte a, byte b, byte c)
        {
            return a == b || a == c ? a : b == c ? b : a;
        }
    }

    /// <summary>
    /// Blended (M13): every vertex keeps its own material. Each triangle's vertices carry the
    /// triangle's three material ids, sorted, with a weight of 1 on their own; the GPU
    /// interpolates the weights, so materials fade into each other across the triangle.
    /// Vertices are only split where neighbouring triangles hold different sets of materials,
    /// so uniform ground stays shared.
    /// </summary>
    public sealed class BlendedSplitter : IMaterialSplitter
    {
        private readonly Dictionary<long, int> copies = new Dictionary<long, int>();

        public void Split(
            IReadOnlyList<Vector3> vertices, IReadOnlyList<Vector3> normals, IReadOnlyList<int> triangles,
            IReadOnlyList<byte> materials, MaterialMesh output)
        {
            output.Clear();
            copies.Clear();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Sort(materials[triangles[i]], materials[triangles[i + 1]], materials[triangles[i + 2]], out byte low, out byte mid, out byte high);
                var ids = new Vector3(low, mid, high);
                long set = ((long)low << 32) | ((long)mid << 40) | ((long)high << 48);
                for (int corner = 0; corner < 3; corner++)
                {
                    int source = triangles[i + corner];
                    long key = set | (uint)source;
                    if (!copies.TryGetValue(key, out int index))
                    {
                        byte own = materials[source];
                        Vector3 weights = own == low ? new Vector3(1f, 0f, 0f) : own == mid ? new Vector3(0f, 1f, 0f) : new Vector3(0f, 0f, 1f);
                        index = output.AddVertex(vertices[source], normals[source], ids, weights);
                        copies.Add(key, index);
                    }
                    output.Triangles.Add(index);
                }
            }
        }

        private static void Sort(byte a, byte b, byte c, out byte low, out byte mid, out byte high)
        {
            if (a > b)
            {
                (a, b) = (b, a);
            }
            if (b > c)
            {
                (b, c) = (c, b);
            }
            if (a > b)
            {
                (a, b) = (b, a);
            }
            low = a;
            mid = b;
            high = c;
        }
    }
}
