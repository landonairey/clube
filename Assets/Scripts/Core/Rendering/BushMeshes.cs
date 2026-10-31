using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The low-poly bushes (GL236), one unit tall before scaling, standing on their origin: lumpy clumps of leaf
    /// blobs (subdivided icosahedra, pushed in and out by a hash) over a few woody stems, flat shaded in vertex
    /// colours, darker low down and inside. A colour's alpha is how far up a vertex is, so the wind (Clube/Grass)
    /// sways the top and leaves the foot. The small bulb is a squat, round clump; the medium one is wide and low,
    /// like a boulder; the large one is tall and full.
    /// </summary>
    public static class BushMeshes
    {
        private static readonly Color Stem = new Color(0.22f, 0.15f, 0.08f, 0f);

        /// <summary>How wide a bush of that size is against its height, for hit tests.</summary>
        public static float WidthOf(BushSize size)
        {
            switch (size)
            {
                case BushSize.Small:
                    return 1.1f;
                case BushSize.Medium:
                    return 1.4f;
                default:
                    return 0.95f;
            }
        }

        public static Mesh Build(BushSize size)
        {
            var builder = new Builder();
            var random = new System.Random(17 + (int)size);
            float width = WidthOf(size);
            // A blob's radii: across the ground scaled to the bush's width, up as given.
            Vector3 Wide(float across, float up) => new Vector3(across * width, up, across * width);
            switch (size)
            {
                case BushSize.Small:
                    builder.Stems(3, 0.35f, random);
                    builder.Blob(new Vector3(0f, 0.52f, 0f), Wide(0.46f, 0.46f), random, 0.2f);
                    builder.Blob(new Vector3(0.22f, 0.32f, 0.1f), Wide(0.3f, 0.29f), random, 0.1f);
                    builder.Blob(new Vector3(-0.18f, 0.3f, -0.16f), Wide(0.28f, 0.27f), random, 0.05f);
                    break;
                case BushSize.Medium:
                    builder.Stems(4, 0.4f, random);
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * Mathf.PI * 0.5f + (float)random.NextDouble() * 0.6f;
                        var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.3f * width;
                        builder.Blob(offset + Vector3.up * 0.45f, Wide(0.38f, 0.4f), random, 0.1f + 0.05f * i);
                    }
                    builder.Blob(new Vector3(0f, 0.6f, 0f), Wide(0.45f, 0.38f), random, 0.25f);
                    break;
                default:
                    builder.Stems(5, 0.45f, random);
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = i * Mathf.PI * 0.4f + (float)random.NextDouble() * 0.5f;
                        var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.24f * width;
                        builder.Blob(offset + Vector3.up * (0.4f + 0.12f * (i % 2)), Wide(0.3f, 0.34f), random, 0.05f * i);
                    }
                    builder.Blob(new Vector3(0f, 0.7f, 0f), Wide(0.36f, 0.32f), random, 0.3f);
                    builder.Blob(new Vector3(0.05f, 0.28f, -0.04f), Wide(0.32f, 0.25f), random, 0f);
                    break;
            }
            return builder.Make(size + " bush");
        }

        private sealed class Builder
        {
            private readonly List<Vector3> positions = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<int> indices = new List<int>();

            // A few thin woody stems from the ground up into the leaves.
            public void Stems(int count, float tall, System.Random random)
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + (float)random.NextDouble() * 0.5f) / count * Mathf.PI * 2f;
                    var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var side = new Vector3(-outward.z, 0f, outward.x) * 0.025f;
                    Vector3 top = outward * 0.15f + Vector3.up * tall;
                    int first = positions.Count;
                    positions.Add(-side);
                    positions.Add(side);
                    positions.Add(top);
                    var tip = new Color(Stem.r, Stem.g, Stem.b, tall * tall);
                    colors.Add(Stem);
                    colors.Add(Stem);
                    colors.Add(tip);
                    Vector3 normal = (outward + Vector3.up).normalized;
                    normals.Add(normal);
                    normals.Add(normal);
                    normals.Add(normal);
                    indices.Add(first);
                    indices.Add(first + 2);
                    indices.Add(first + 1);
                }
            }

            // A lumpy blob of leaves: an icosahedron subdivided once, each corner pushed in or out a little.
            public void Blob(Vector3 centre, Vector3 radius, System.Random random, float hue)
            {
                List<Vector3> corners = Icosphere(out List<int> faces);
                for (int i = 0; i < corners.Count; i++)
                {
                    float lump = 0.82f + 0.3f * (float)random.NextDouble();
                    corners[i] = centre + Vector3.Scale(corners[i] * lump, radius);
                }
                var dark = new Color(0.05f + 0.03f * hue, 0.16f + 0.05f * hue, 0.04f, 1f);
                var light = new Color(0.3f + 0.1f * hue, 0.56f + 0.06f * hue, 0.16f, 1f);
                for (int f = 0; f < faces.Count; f += 3)
                {
                    Vector3 a = corners[faces[f]], b = corners[faces[f + 1]], c = corners[faces[f + 2]];
                    Vector3 middle = (a + b + c) / 3f;
                    // Flat shaded, but leaning outwards from the clump's centre, so it's lit as one round mass.
                    Vector3 flat = Vector3.Cross(b - a, c - a).normalized;
                    if (Vector3.Dot(flat, middle - centre) < 0f)
                    {
                        flat = -flat;
                    }
                    Vector3 outward = (middle - new Vector3(0f, 0.45f, 0f)).normalized;
                    Vector3 normal = (flat + outward).normalized;
                    // Darker low down and on the underside.
                    float shade = Mathf.Clamp01(middle.y * 0.9f + 0.25f + 0.25f * normal.y);
                    // Each facet a little lighter or darker, so the clump reads as leaves rather than one smooth mass.
                    float leaf = 0.75f + 0.5f * (float)random.NextDouble();
                    Color color = Color.Lerp(dark, light, shade) * leaf;
                    int first = positions.Count;
                    foreach (Vector3 corner in new[] { a, b, c })
                    {
                        positions.Add(corner);
                        float up = Mathf.Clamp01(corner.y);
                        colors.Add(new Color(color.r, color.g, color.b, up * up));
                        normals.Add(normal);
                    }
                    indices.Add(first);
                    indices.Add(first + 1);
                    indices.Add(first + 2);
                }
            }

            public Mesh Make(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(positions);
                mesh.SetColors(colors);
                mesh.SetNormals(normals);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // The unit icosahedron with each face split in four, its corners on the unit sphere.
        private static List<Vector3> Icosphere(out List<int> faces)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var corners = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < corners.Count; i++)
            {
                corners[i] = corners[i].normalized;
            }
            int[] start =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            faces = new List<int>();
            var middles = new Dictionary<long, int>();
            int Middle(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!middles.TryGetValue(key, out int index))
                {
                    index = corners.Count;
                    corners.Add(((corners[a] + corners[b]) * 0.5f).normalized);
                    middles.Add(key, index);
                }
                return index;
            }
            for (int f = 0; f < start.Length; f += 3)
            {
                int a = start[f], b = start[f + 1], c = start[f + 2];
                int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            return corners;
        }
    }
}
