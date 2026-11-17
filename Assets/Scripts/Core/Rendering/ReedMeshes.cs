using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A clump of reeds (GL723), one unit tall before scaling, standing on its origin: a few dozen thin straight stalks,
    /// some leaning out, with long narrow leaves off the lower half and a brown seed head on a few. Vertex colours as the
    /// bushes' (<see cref="BushMeshes"/>): alpha is how far up a vertex is, so the wind (Clube/Grass) sways the tops.
    /// </summary>
    public static class ReedMeshes
    {
        /// <summary>How wide a clump is against its height, for hit tests.</summary>
        public const float Width = 0.5f;

        private const int Stalks = 26;

        private static readonly Color Root = new Color(0.24f, 0.3f, 0.12f, 0f);
        private static readonly Color Green = new Color(0.42f, 0.52f, 0.2f, 1f);
        private static readonly Color Straw = new Color(0.66f, 0.6f, 0.34f, 1f);
        private static readonly Color Head = new Color(0.32f, 0.2f, 0.1f, 1f);

        public static Mesh Build()
        {
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            var random = new System.Random(23);
            for (int i = 0; i < Stalks; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-outward.z, 0f, outward.x);
                Vector3 foot = outward * ((float)random.NextDouble() * Width * 0.35f);
                float tall = 0.65f + (float)random.NextDouble() * 0.35f;
                Vector3 top = foot + outward * (0.04f + (float)random.NextDouble() * 0.16f) * tall + Vector3.up * tall;
                // Some stalks have dried to straw.
                Color tip = Color.Lerp(Green, Straw, (float)random.NextDouble() < 0.3f ? 0.8f : (float)random.NextDouble() * 0.3f);
                Quad(foot, top, side * 0.012f, side * 0.006f, outward, Root, tip, positions, colors, normals, indices);
                // A long leaf off the lower half, arching out.
                if (i % 2 == 0)
                {
                    Vector3 from = Vector3.Lerp(foot, top, 0.15f + 0.2f * (float)random.NextDouble());
                    Vector3 to = from + outward * (0.18f + 0.12f * (float)random.NextDouble()) + Vector3.up * (0.3f + 0.15f * (float)random.NextDouble());
                    Quad(from, to, side * 0.02f, Vector3.zero, outward, Color.Lerp(Root, Green, from.y), Green, positions, colors, normals, indices);
                }
                // A seed head: a short fat brown spike just under the top.
                if (i % 5 == 0)
                {
                    Vector3 low = Vector3.Lerp(foot, top, 0.8f), high = Vector3.Lerp(foot, top, 0.93f);
                    Quad(low, high, side * 0.03f, side * 0.03f, outward, Head, Head, positions, colors, normals, indices);
                    Quad(low, high, outward * 0.03f, outward * 0.03f, side, Head, Head, positions, colors, normals, indices);
                }
            }
            var mesh = new Mesh { name = "Reeds" };
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // A thin strip from bottom to top, half as wide as given at each end, facing along facing; alpha is the height.
        private static void Quad(
            Vector3 bottom, Vector3 top, Vector3 bottomHalf, Vector3 topHalf, Vector3 facing, Color low, Color high,
            List<Vector3> positions, List<Color> colors, List<Vector3> normals, List<int> indices)
        {
            int first = positions.Count;
            positions.Add(bottom - bottomHalf);
            positions.Add(bottom + bottomHalf);
            positions.Add(top + topHalf);
            positions.Add(top - topHalf);
            float lowUp = Mathf.Clamp01(bottom.y), highUp = Mathf.Clamp01(top.y);
            colors.Add(new Color(low.r, low.g, low.b, lowUp * lowUp));
            colors.Add(new Color(low.r, low.g, low.b, lowUp * lowUp));
            colors.Add(new Color(high.r, high.g, high.b, highUp * highUp));
            colors.Add(new Color(high.r, high.g, high.b, highUp * highUp));
            Vector3 normal = (facing + Vector3.up * 0.5f).normalized;
            for (int i = 0; i < 4; i++)
            {
                normals.Add(normal);
            }
            indices.Add(first);
            indices.Add(first + 2);
            indices.Add(first + 1);
            indices.Add(first);
            indices.Add(first + 3);
            indices.Add(first + 2);
        }
    }
}
