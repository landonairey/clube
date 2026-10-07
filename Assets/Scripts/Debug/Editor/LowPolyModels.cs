using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Builds the stand-in low-poly models for the loop's stations (GL10, GL11, GL14) as mesh
    /// assets, until real art exists: <i>Clube → Models → Build station models</i> writes them
    /// to <c>Assets/Models/</c>. Each is a handful of tapered blocks, flat shaded (every face
    /// its own vertices), with one submesh per material in the order listed on each model.
    /// Origins sit on the ground at the model's centre, front facing +Z.
    /// </summary>
    public static class LowPolyModels
    {
        private const string Folder = "Assets/Models";

        [MenuItem("Clube/Models/Build station models")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            Save("Furnace", Furnace());
            Save("Anvil", Anvil());
            Save("Merchant Table", MerchantTable());
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Built station models in {Folder}.");
        }

        // Submeshes: 0 stone, 1 embers. A squat kiln of two tapering courses, a chimney, and
        // a glowing mouth in the front.
        private static Mesh Furnace()
        {
            var model = new ModelBuilder(2);
            model.Loft(0, 0f, new Vector2(1.4f, 1.4f), 1f, new Vector2(1.15f, 1.15f));
            model.Loft(0, 1f, new Vector2(1.15f, 1.15f), 1.45f, new Vector2(0.7f, 0.7f));
            model.Box(0, new Vector3(0f, 1.75f, -0.05f), new Vector3(0.38f, 0.65f, 0.38f));
            model.Box(0, new Vector3(0f, 2.1f, -0.05f), new Vector3(0.48f, 0.08f, 0.48f));
            // The mouth: a dark recess with embers glowing in it.
            model.Box(1, new Vector3(0f, 0.38f, 0.64f), new Vector3(0.5f, 0.38f, 0.12f));
            model.Box(0, new Vector3(0f, 0.6f, 0.68f), new Vector3(0.66f, 0.08f, 0.12f));
            return model.Build("Furnace");
        }

        // Submeshes: 0 iron, 1 wood. A stump, a narrow waist, the flat face with a heel, and
        // a horn tapering to a point along +X.
        private static Mesh Anvil()
        {
            var model = new ModelBuilder(2);
            model.Loft(1, 0f, new Vector2(0.5f, 0.5f), 0.42f, new Vector2(0.42f, 0.42f));
            model.Loft(0, 0.42f, new Vector2(0.36f, 0.26f), 0.5f, new Vector2(0.3f, 0.2f));
            model.Loft(0, 0.5f, new Vector2(0.22f, 0.15f), 0.62f, new Vector2(0.3f, 0.2f));
            model.Box(0, new Vector3(-0.04f, 0.69f, 0f), new Vector3(0.58f, 0.14f, 0.24f));
            model.LoftX(0, new Vector3(0.25f, 0.71f, 0f), new Vector2(0.1f, 0.2f), new Vector3(0.56f, 0.75f, 0f), new Vector2(0.02f, 0.03f));
            return model.Build("Anvil");
        }

        // Submeshes: 0 wood, 1 cloth. A plank table on four legs, two posts at the back
        // holding a canopy that slopes down over the front, and a crate on the table.
        private static Mesh MerchantTable()
        {
            var model = new ModelBuilder(2);
            model.Box(0, new Vector3(0f, 0.86f, 0f), new Vector3(1.7f, 0.08f, 0.8f));
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                foreach (float z in new[] { -0.32f, 0.32f })
                {
                    model.Box(0, new Vector3(x, 0.41f, z), new Vector3(0.08f, 0.82f, 0.08f));
                }
                model.Box(0, new Vector3(x, 1.05f, -0.42f), new Vector3(0.07f, 2.1f, 0.07f));
                model.Box(0, new Vector3(x, 0.95f, 0.42f), new Vector3(0.06f, 1.9f, 0.06f));
            }
            model.Box(1, new Vector3(0f, 1.98f, 0f), new Vector3(1.9f, 0.05f, 1.15f), Quaternion.Euler(-10f, 0f, 0f));
            model.Box(0, new Vector3(-0.45f, 1.07f, -0.1f), new Vector3(0.35f, 0.34f, 0.35f));
            return model.Build("Merchant Table");
        }

        private static void Save(string name, Mesh mesh)
        {
            string path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Keeps the asset's GUID, so scenes that use it stay connected.
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
        }

        /// <summary>Collects flat-shaded quads per submesh.</summary>
        private sealed class ModelBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<int>[] triangles;

            public ModelBuilder(int submeshes)
            {
                triangles = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++)
                {
                    triangles[i] = new List<int>();
                }
            }

            public void Box(int submesh, Vector3 centre, Vector3 size, Quaternion? rotation = null)
            {
                Quaternion r = rotation ?? Quaternion.identity;
                Vector3 h = size * 0.5f;
                Vector3 P(float x, float y, float z) => centre + r * new Vector3(x * h.x, y * h.y, z * h.z);
                Hexahedron(submesh,
                    P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1),
                    P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1));
            }

            /// <summary>A block tapering upwards: a rectangle at one height to another above it, both centred.</summary>
            public void Loft(int submesh, float y0, Vector2 size0, float y1, Vector2 size1)
            {
                Vector2 a = size0 * 0.5f;
                Vector2 b = size1 * 0.5f;
                Hexahedron(submesh,
                    new Vector3(-a.x, y0, -a.y), new Vector3(a.x, y0, -a.y), new Vector3(a.x, y0, a.y), new Vector3(-a.x, y0, a.y),
                    new Vector3(-b.x, y1, -b.y), new Vector3(b.x, y1, -b.y), new Vector3(b.x, y1, b.y), new Vector3(-b.x, y1, b.y));
            }

            /// <summary>A block tapering along +X, from a (height, depth) rectangle at one point to one at another.</summary>
            public void LoftX(int submesh, Vector3 start, Vector2 size0, Vector3 end, Vector2 size1)
            {
                Vector2 a = size0 * 0.5f;
                Vector2 b = size1 * 0.5f;
                // Bottom face is the start/end at -y, top at +y; listed in the same corner order as Box.
                Hexahedron(submesh,
                    start + new Vector3(0f, -a.x, -a.y), end + new Vector3(0f, -b.x, -b.y), end + new Vector3(0f, -b.x, b.y), start + new Vector3(0f, -a.x, a.y),
                    start + new Vector3(0f, a.x, -a.y), end + new Vector3(0f, b.x, -b.y), end + new Vector3(0f, b.x, b.y), start + new Vector3(0f, a.x, a.y));
            }

            // Corners 0-3 the bottom ring, 4-7 the top ring above them, both counter-clockwise seen from above.
            private void Hexahedron(int submesh, params Vector3[] c)
            {
                Quad(submesh, c[0], c[1], c[2], c[3]); // bottom
                Quad(submesh, c[7], c[6], c[5], c[4]); // top
                Quad(submesh, c[4], c[5], c[1], c[0]); // -z
                Quad(submesh, c[5], c[6], c[2], c[1]); // +x
                Quad(submesh, c[6], c[7], c[3], c[2]); // +z
                Quad(submesh, c[7], c[4], c[0], c[3]); // -x
            }

            // A face wound clockwise as seen from outside (Unity's front face).
            private void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                for (int i = 0; i < 4; i++)
                {
                    normals.Add(normal);
                }
                triangles[submesh].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++)
                {
                    mesh.SetTriangles(triangles[i], i);
                }
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
