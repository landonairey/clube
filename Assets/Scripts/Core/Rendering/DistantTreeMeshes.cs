using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The trees drawn past the world's chunks (GL162), one unit tall (scaled to each tree), standing on their origin.
    /// Every level of detail is made from one real tree's parts (<see cref="TreeShape"/>, GL258), so a tree keeps its
    /// shape as it steps down and the nearest level looks like the voxel trees it stands beside:
    /// <list type="number">
    /// <item><see cref="Full"/>: the trunk, every branch and twig, and every leaf ball, as the voxels have them.</item>
    /// <item><see cref="Clusters"/>: the trunk and main branches, and one blob for each cluster of leaf balls.</item>
    /// <item><see cref="Crown"/>: the trunk, and the crown as up to five blobs (the top, and four sides).</item>
    /// <item><see cref="Blob"/>: the trunk, and the crown as one blob.</item>
    /// </list>
    /// The trunk runs on below the ground (the coarse terrain sits a little low, so none floats). Flat shaded, coloured by
    /// vertex (bark, leaves; pass linear colours in a linear project), each leaf blob a little lighter or darker.
    /// </summary>
    public static class DistantTreeMeshes
    {
        public const int Full = 0;
        public const int Clusters = 1;
        public const int Crown = 2;
        public const int Blob = 3;
        public const int Levels = 4;

        // How far below the ground the trunk runs on, against the height.
        private const float Sunk = 0.3f;

        private static readonly Vector3[] Icosahedron = BuildIcosahedron();

        private static readonly int[] IcosahedronFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        /// <summary>A segment of wood (trunk, branch, twig), from the root up, against the tree's height.</summary>
        private struct Wood
        {
            public Vector3 From;
            public Vector3 To;
            public float FromRadius;
            public float ToRadius;
        }

        /// <summary>A ball of leaves.</summary>
        private struct Leaf
        {
            public Vector3 Centre;
            public float Radius;
        }

        /// <summary>
        /// The tree's meshes, one per level (index by <see cref="Full"/> and the others), from its parts as
        /// <see cref="TreeShape.Build"/> made them for a tree rooted at <paramref name="root"/>, <paramref name="height"/> tall.
        /// </summary>
        public static Mesh[] BuildLevels(IReadOnlyList<TreePart> parts, Vector3 root, float height, Color bark, Color leaves, string name)
        {
            var wood = new List<Wood>();
            var clusters = new List<List<Leaf>>();
            List<Leaf> run = null;
            float scale = 1f / Mathf.Max(height, 0.01f);
            foreach (TreePart part in parts)
            {
                Vector3 from = ((Vector3)part.From - root) * scale;
                if (part.IsSegment)
                {
                    wood.Add(new Wood { From = from, To = ((Vector3)part.To - root) * scale, FromRadius = part.FromRadius * scale, ToRadius = part.ToRadius * scale });
                    run = null;
                }
                else
                {
                    // Balls added one after another are one cluster, as TreeShape grows them round a branch's end.
                    if (run == null)
                    {
                        run = new List<Leaf>();
                        clusters.Add(run);
                    }
                    run.Add(new Leaf { Centre = from, Radius = part.FromRadius * scale });
                }
            }

            List<Wood> trunk = Trunk(wood);
            float thinnest = float.MaxValue;
            foreach (Wood segment in wood)
            {
                thinnest = Mathf.Min(thinnest, segment.ToRadius);
            }

            var meshes = new Mesh[Levels];
            meshes[Full] = Make($"{name} (full)", builder =>
            {
                Sink(trunk[0], builder, bark, 6);
                foreach (Wood segment in wood)
                {
                    builder.Segment(segment, segment.FromRadius > 0.02f ? 6 : segment.FromRadius > 0.01f ? 4 : 3, bark);
                }
                for (int c = 0; c < clusters.Count; c++)
                {
                    for (int b = 0; b < clusters[c].Count; b++)
                    {
                        Leaf leaf = clusters[c][b];
                        builder.Ball(leaf.Centre, Vector3.one * leaf.Radius, leaves * Shade(c * 7 + b));
                    }
                }
            });
            meshes[Clusters] = Make($"{name} (clusters)", builder =>
            {
                Sink(trunk[0], builder, bark, 5);
                foreach (Wood segment in wood)
                {
                    // The trunk and the main branches; the twigs, which end as thin as wood gets, are left out.
                    if (segment.ToRadius > thinnest * 1.05f)
                    {
                        builder.Segment(segment, segment.FromRadius > 0.02f ? 5 : 3, bark);
                    }
                }
                for (int c = 0; c < clusters.Count; c++)
                {
                    Enclose(clusters[c], 0.85f, out Vector3 centre, out Vector3 radii);
                    builder.Ball(centre, radii, leaves * Shade(c));
                }
            });
            meshes[Crown] = Make($"{name} (crown)", builder =>
            {
                Sink(trunk[0], builder, bark, 4);
                Wood top = trunk[trunk.Count - 1];
                builder.Segment(new Wood { From = trunk[0].From, To = top.To, FromRadius = trunk[0].FromRadius, ToRadius = top.ToRadius }, 4, bark);
                List<List<Leaf>> groups = CrownGroups(clusters, trunk[trunk.Count - 1].To);
                for (int g = 0; g < groups.Count; g++)
                {
                    Enclose(groups[g], 0.8f, out Vector3 centre, out Vector3 radii);
                    builder.Ball(centre, radii, leaves * Shade(g + 3));
                }
            });
            meshes[Blob] = Make($"{name} (blob)", builder =>
            {
                Sink(trunk[0], builder, bark, 3);
                var trunkTop = trunk[trunk.Count - 1];
                builder.Segment(new Wood { From = trunk[1].From, To = trunkTop.To, FromRadius = trunk[1].FromRadius, ToRadius = trunkTop.ToRadius }, 3, bark);
                var all = new List<Leaf>();
                foreach (List<Leaf> cluster in clusters)
                {
                    all.AddRange(cluster);
                }
                Enclose(all, 0.78f, out Vector3 centre, out Vector3 radii);
                builder.Ball(centre, radii, leaves);
            });
            return meshes;
        }

        // The stump and the trunk above it: the first segment, then each segment that carries straight on from the last.
        private static List<Wood> Trunk(List<Wood> wood)
        {
            var trunk = new List<Wood> { wood[0] };
            if (wood.Count < 2)
            {
                return trunk;
            }
            trunk.Add(wood[1]);
            for (int i = 2; i < wood.Count && (wood[i].From - trunk[trunk.Count - 1].To).sqrMagnitude < 1e-8f; i++)
            {
                trunk.Add(wood[i]);
            }
            return trunk;
        }

        // The stump carried on down below the ground.
        private static void Sink(Wood stump, MeshBuilder builder, Color bark, int sides)
        {
            builder.Segment(new Wood { From = new Vector3(stump.From.x, -Sunk, stump.From.z), To = stump.From, FromRadius = stump.FromRadius, ToRadius = stump.FromRadius }, sides, bark);
        }

        // The crown in up to five groups of clusters: the top one (round the trunk's top) and four sides.
        private static List<List<Leaf>> CrownGroups(List<List<Leaf>> clusters, Vector3 top)
        {
            var groups = new List<Leaf>[5];
            for (int c = 0; c < clusters.Count; c++)
            {
                Enclose(clusters[c], 1f, out Vector3 centre, out _);
                Vector3 off = centre - top;
                int group = c == clusters.Count - 1 || new Vector2(off.x, off.z).magnitude < 0.12f
                    ? 4
                    : (int)Mathf.Repeat(Mathf.Floor((Mathf.Atan2(off.z, off.x) + Mathf.PI) / (Mathf.PI * 0.5f)), 4f);
                groups[group] ??= new List<Leaf>();
                groups[group].AddRange(clusters[c]);
            }
            var found = new List<List<Leaf>>();
            foreach (List<Leaf> group in groups)
            {
                if (group != null)
                {
                    found.Add(group);
                }
            }
            return found;
        }

        // A blob standing in for some leaf balls: at their middle (by volume), as wide and tall as they reach, a little in.
        private static void Enclose(List<Leaf> balls, float tighten, out Vector3 centre, out Vector3 radii)
        {
            Vector3 sum = Vector3.zero;
            float weight = 0f;
            foreach (Leaf ball in balls)
            {
                float w = ball.Radius * ball.Radius * ball.Radius;
                sum += ball.Centre * w;
                weight += w;
            }
            centre = weight > 0f ? sum / weight : Vector3.zero;
            float across = 0f, up = 0f, down = 0f;
            foreach (Leaf ball in balls)
            {
                Vector3 off = ball.Centre - centre;
                across = Mathf.Max(across, new Vector2(off.x, off.z).magnitude + ball.Radius);
                up = Mathf.Max(up, off.y + ball.Radius);
                down = Mathf.Max(down, -off.y + ball.Radius);
            }
            float tall = (up + down) * 0.5f;
            centre.y += (up - down) * 0.5f;
            radii = new Vector3(across, tall, across) * tighten;
        }

        // A little lighter or darker per blob, as the voxel leaves' texture is patchy.
        private static float Shade(int index)
        {
            return 0.9f + 0.2f * Mathf.Repeat(Mathf.Sin(index * 12.9898f) * 43758.5453f, 1f);
        }

        private static Mesh Make(string name, System.Action<MeshBuilder> build)
        {
            var builder = new MeshBuilder();
            build(builder);
            var mesh = new Mesh { name = name };
            if (builder.Vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(builder.Vertices);
            mesh.SetNormals(builder.Normals);
            mesh.SetColors(builder.Colors);
            mesh.SetTriangles(builder.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] BuildIcosahedron()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = corners[i].normalized;
            }
            return corners;
        }

        /// <summary>Collects flat-shaded triangles.</summary>
        private sealed class MeshBuilder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<int> Triangles = new List<int>();

            // A tapered prism along a segment of wood, open at the ends.
            public void Segment(Wood segment, int sides, Color color)
            {
                Vector3 axis = segment.To - segment.From;
                if (axis.sqrMagnitude < 1e-10f)
                {
                    return;
                }
                Vector3 along = axis.normalized;
                Vector3 side = Vector3.Cross(along, Mathf.Abs(along.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 other = Vector3.Cross(along, side);
                Vector3 middle = (segment.From + segment.To) * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides;
                    float a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = side * Mathf.Cos(a0) + other * Mathf.Sin(a0);
                    Vector3 d1 = side * Mathf.Cos(a1) + other * Mathf.Sin(a1);
                    Vector3 b0 = segment.From + d0 * segment.FromRadius;
                    Vector3 b1 = segment.From + d1 * segment.FromRadius;
                    Vector3 t0 = segment.To + d0 * segment.ToRadius;
                    Vector3 t1 = segment.To + d1 * segment.ToRadius;
                    // Facing away from the axis next to each face.
                    Vector3 inside = middle + along * Vector3.Dot((b0 + t1) * 0.5f - middle, along);
                    Triangle(b0, t0, t1, inside, color);
                    Triangle(b0, t1, b1, inside, color);
                }
            }

            // An icosahedron stretched to these radii: a ball of leaves, or a blob standing in for several.
            public void Ball(Vector3 centre, Vector3 radii, Color color)
            {
                for (int i = 0; i < IcosahedronFaces.Length; i += 3)
                {
                    Vector3 a = centre + Vector3.Scale(Icosahedron[IcosahedronFaces[i]], radii);
                    Vector3 b = centre + Vector3.Scale(Icosahedron[IcosahedronFaces[i + 1]], radii);
                    Vector3 c = centre + Vector3.Scale(Icosahedron[IcosahedronFaces[i + 2]], radii);
                    Triangle(a, b, c, centre, color);
                }
            }

            // A flat-shaded triangle facing away from <paramref name="middle"/>: wound clockwise seen from outside
            // (Unity's front face), so its cross product points out.
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 middle, Color color)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-14f)
                {
                    return;
                }
                normal.Normalize();
                if (Vector3.Dot(normal, (a + b + c) / 3f - middle) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }
                int first = Vertices.Count;
                Vertices.Add(a);
                Vertices.Add(b);
                Vertices.Add(c);
                for (int i = 0; i < 3; i++)
                {
                    Normals.Add(normal);
                    Colors.Add(color);
                }
                Triangles.Add(first);
                Triangles.Add(first + 1);
                Triangles.Add(first + 2);
            }
        }
    }
}
