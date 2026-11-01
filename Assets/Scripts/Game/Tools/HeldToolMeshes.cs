using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>The shape a held tool is drawn as (GL269), and how it swings.</summary>
    public enum HeldShape
    {
        Hand,
        HandAxe,
        Pick,
        Hatchet,
        Shovel,
        Hammer,
        Chisel,
        Spear,
    }

    /// <summary>
    /// Low-poly models of the hand and the tools as the player holds them (GL269), made in code: a sleeve and fist with the
    /// tool gripped in it, flat shaded, coloured by vertex (wood handles, stone, bronze or iron heads). The fist is at the
    /// origin, the handle runs up +Y and the tool's working edge faces +Z. Read by <see cref="HeldToolView"/>.
    /// </summary>
    public static class HeldToolMeshes
    {
        private static readonly Color Skin = new Color(0.78f, 0.56f, 0.42f);
        private static readonly Color Sleeve = new Color(0.24f, 0.28f, 0.36f);
        private static readonly Color Wood = new Color(0.42f, 0.28f, 0.15f);
        private static readonly Color Stone = new Color(0.5f, 0.5f, 0.52f);
        private static readonly Color Flint = new Color(0.36f, 0.35f, 0.38f);
        private static readonly Color Bronze = new Color(0.72f, 0.46f, 0.2f);
        private static readonly Color Iron = new Color(0.55f, 0.57f, 0.6f);
        private static readonly Color Cord = new Color(0.62f, 0.52f, 0.34f);

        // A tool made of stone (a stone pick, hatchet or hammer), as against bronze or iron.
        private static bool IsStone(ToolDefinition tool)
        {
            return tool != null && tool.DisplayName.ToLowerInvariant().Contains("stone");
        }

        /// <summary>The shape a tool is drawn as, by its name; the hand when there's none.</summary>
        public static HeldShape ShapeOf(ToolDefinition tool)
        {
            string name = tool != null ? tool.DisplayName.ToLowerInvariant() : "hand";
            if (name.Contains("hand axe"))
            {
                return HeldShape.HandAxe;
            }
            if (name.Contains("pick"))
            {
                return HeldShape.Pick;
            }
            if (name.Contains("hatchet") || name.Contains("axe"))
            {
                return HeldShape.Hatchet;
            }
            if (name.Contains("shovel"))
            {
                return HeldShape.Shovel;
            }
            if (name.Contains("hammer"))
            {
                return HeldShape.Hammer;
            }
            if (name.Contains("chisel") || (tool != null && tool.Chisels))
            {
                return HeldShape.Chisel;
            }
            if (name.Contains("spear"))
            {
                return HeldShape.Spear;
            }
            return HeldShape.Hand;
        }

        /// <summary>The colour of a tool's head, by what it's made of.</summary>
        public static Color HeadColourOf(ToolDefinition tool)
        {
            string name = tool != null ? tool.DisplayName.ToLowerInvariant() : string.Empty;
            if (name.Contains("bronze"))
            {
                return Bronze;
            }
            if (name.Contains("stone") || name.Contains("hand axe"))
            {
                return name.Contains("hand axe") ? Flint : Stone;
            }
            return Iron;
        }

        /// <summary>The model of a tool held in the fist (the bare fist for the hand).</summary>
        public static Mesh Build(ToolDefinition tool)
        {
            HeldShape shape = ShapeOf(tool);
            Color head = HeadColourOf(tool);
            var model = new Builder();
            // The forearm, back and down to the bottom right of the view, and the fist round the grip.
            model.Prism(new Vector3(0.06f, -0.2f, -0.5f), new Vector3(0.01f, -0.03f, -0.06f), 0.05f, 0.042f, 6, Sleeve);
            model.Box(new Vector3(0f, 0f, 0f), new Vector3(0.085f, 0.075f, 0.09f), Quaternion.identity, Skin);
            switch (shape)
            {
                case HeldShape.Hand:
                    // Knuckles a little forward, a thumb over the side.
                    model.Box(new Vector3(0f, 0.01f, 0.05f), new Vector3(0.08f, 0.045f, 0.03f), Quaternion.identity, Skin * 0.95f);
                    model.Box(new Vector3(-0.045f, 0.02f, 0.02f), new Vector3(0.025f, 0.025f, 0.06f), Quaternion.Euler(0f, 20f, 0f), Skin * 0.92f);
                    break;
                case HeldShape.HandAxe:
                    // A knapped stone, pointed end forward, held in the fist.
                    model.Diamond(new Vector3(0f, 0.04f, 0.09f), new Vector3(0.05f, 0.085f, 0.11f), head);
                    break;
                case HeldShape.Pick when IsStone(tool):
                    // A primitive pick (as its icon): a rough stick, and a pointed stone lashed across its top,
                    // sticking out to one side only, bound on with cord.
                    model.Prism(new Vector3(0f, -0.14f, 0f), new Vector3(0.01f, 0.44f, -0.01f), 0.019f, 0.016f, 5, Wood);
                    model.Prism(new Vector3(0f, 0.43f, -0.035f), new Vector3(0f, 0.38f, 0.23f), 0.034f, 0.007f, 5, head);
                    model.Box(new Vector3(0f, 0.44f, -0.045f), new Vector3(0.05f, 0.055f, 0.04f), Quaternion.Euler(8f, 0f, 0f), head * 0.9f);
                    model.Box(new Vector3(0.002f, 0.415f, -0.004f), new Vector3(0.046f, 0.016f, 0.05f), Quaternion.Euler(30f, 0f, 0f), Cord);
                    model.Box(new Vector3(0.002f, 0.45f, -0.004f), new Vector3(0.046f, 0.016f, 0.05f), Quaternion.Euler(-30f, 0f, 0f), Cord);
                    model.Box(new Vector3(0.002f, 0.39f, 0.006f), new Vector3(0.04f, 0.014f, 0.04f), Quaternion.identity, Cord * 0.9f);
                    break;
                case HeldShape.Pick:
                    model.Prism(new Vector3(0f, -0.14f, 0f), new Vector3(0f, 0.46f, 0f), 0.017f, 0.015f, 5, Wood);
                    model.Prism(new Vector3(0f, 0.45f, -0.02f), new Vector3(0f, 0.39f, 0.24f), 0.028f, 0.006f, 4, head);
                    model.Prism(new Vector3(0f, 0.45f, 0.02f), new Vector3(0f, 0.41f, -0.17f), 0.026f, 0.008f, 4, head);
                    break;
                case HeldShape.Hatchet:
                    model.Prism(new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.42f, 0f), 0.017f, 0.015f, 5, Wood);
                    model.Box(new Vector3(0f, 0.4f, 0.04f), new Vector3(0.032f, 0.07f, 0.06f), Quaternion.identity, head);
                    model.Wedge(new Vector3(0f, 0.4f, 0.07f), new Vector3(0f, 0.4f, 0.17f), 0.016f, 0.07f, 0.13f, head);
                    break;
                case HeldShape.Shovel:
                    model.Prism(new Vector3(0f, -0.2f, 0f), new Vector3(0f, 0.55f, 0f), 0.017f, 0.016f, 5, Wood);
                    model.Box(new Vector3(0f, -0.2f, 0f), new Vector3(0.1f, 0.025f, 0.03f), Quaternion.identity, Wood);
                    model.Box(new Vector3(0f, 0.66f, 0.01f), new Vector3(0.16f, 0.2f, 0.012f), Quaternion.Euler(-12f, 0f, 0f), head);
                    break;
                case HeldShape.Hammer:
                    model.Prism(new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.36f, 0f), 0.017f, 0.015f, 5, Wood);
                    model.Box(new Vector3(0f, 0.38f, 0.02f), new Vector3(0.07f, 0.07f, 0.15f), Quaternion.identity, head);
                    break;
                case HeldShape.Chisel:
                    model.Prism(new Vector3(0f, -0.06f, 0.02f), new Vector3(0f, 0.17f, 0.05f), 0.014f, 0.012f, 4, head);
                    model.Wedge(new Vector3(0f, 0.17f, 0.05f), new Vector3(0f, 0.23f, 0.06f), 0.012f, 0.026f, 0.004f, head * 1.1f);
                    break;
                case HeldShape.Spear:
                    model.Prism(new Vector3(0f, -0.35f, -0.1f), new Vector3(0f, 0.85f, 0.25f), 0.015f, 0.014f, 5, Wood);
                    model.Diamond(new Vector3(0f, 0.93f, 0.28f), new Vector3(0.025f, 0.13f, 0.04f), head);
                    break;
            }
            return model.Make($"Held {shape}");
        }

        /// <summary>Collects flat-shaded triangles.</summary>
        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();

            public void Box(Vector3 centre, Vector3 size, Quaternion turn, Color color)
            {
                Vector3 h = size * 0.5f;
                Vector3[] c =
                {
                    new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                    new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z),
                };
                for (int i = 0; i < c.Length; i++)
                {
                    c[i] = centre + turn * c[i];
                }
                Quad(c[0], c[1], c[2], c[3], centre, color);
                Quad(c[5], c[4], c[7], c[6], centre, color);
                Quad(c[4], c[0], c[3], c[7], centre, color);
                Quad(c[1], c[5], c[6], c[2], centre, color);
                Quad(c[3], c[2], c[6], c[7], centre, color);
                Quad(c[4], c[5], c[1], c[0], centre, color);
            }

            // A tapered prism along a line, with ends closed.
            public void Prism(Vector3 from, Vector3 to, float fromRadius, float toRadius, int sides, Color color)
            {
                Vector3 along = (to - from).normalized;
                Vector3 side = Vector3.Cross(along, Mathf.Abs(along.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 other = Vector3.Cross(along, side);
                Vector3 middle = (from + to) * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = side * Mathf.Cos(a0) + other * Mathf.Sin(a0);
                    Vector3 d1 = side * Mathf.Cos(a1) + other * Mathf.Sin(a1);
                    Vector3 b0 = from + d0 * fromRadius, b1 = from + d1 * fromRadius;
                    Vector3 t0 = to + d0 * toRadius, t1 = to + d1 * toRadius;
                    Quad(b0, t0, t1, b1, middle, color);
                    Triangle(from, b1, b0, middle, color);
                    Triangle(to, t0, t1, middle, color);
                }
            }

            // A flat blade from a thick back to a sharp edge: <paramref name="height"/> tall at the back, flaring to
            // <paramref name="edge"/> at the front.
            public void Wedge(Vector3 back, Vector3 front, float thickness, float height, float edge, Color color)
            {
                Vector3 up = Vector3.up;
                Vector3 side = Vector3.right * thickness * 0.5f;
                Vector3 bt = back + up * height * 0.5f, bb = back - up * height * 0.5f;
                Vector3 ft = front + up * edge * 0.5f, fb = front - up * edge * 0.5f;
                Vector3 middle = (back + front) * 0.5f;
                Quad(bb + side, bt + side, ft, fb, middle, color);
                Quad(bt - side, bb - side, fb, ft, middle, color);
                Quad(bt + side, bt - side, ft, ft, middle, color);
                Quad(bb - side, bb + side, fb, fb, middle, color);
                Quad(bb - side, bt - side, bt + side, bb + side, middle, color);
            }

            // An octahedron stretched to these half-sizes: a knapped stone, a spear point.
            public void Diamond(Vector3 centre, Vector3 half, Color color)
            {
                Vector3 px = centre + Vector3.right * half.x, nx = centre - Vector3.right * half.x;
                Vector3 py = centre + Vector3.up * half.y, ny = centre - Vector3.up * half.y;
                Vector3 pz = centre + Vector3.forward * half.z, nz = centre - Vector3.forward * half.z;
                Triangle(py, px, pz, centre, color);
                Triangle(py, pz, nx, centre, color);
                Triangle(py, nx, nz, centre, color);
                Triangle(py, nz, px, centre, color);
                Triangle(ny, pz, px, centre, color * 0.9f);
                Triangle(ny, nx, pz, centre, color * 0.9f);
                Triangle(ny, nz, nx, centre, color * 0.9f);
                Triangle(ny, px, nz, centre, color * 0.9f);
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 middle, Color color)
            {
                Triangle(a, b, c, middle, color);
                Triangle(a, c, d, middle, color);
            }

            // A triangle facing away from <paramref name="middle"/>.
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 middle, Color color)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f)
                {
                    return;
                }
                normal.Normalize();
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

            public Mesh Make(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
