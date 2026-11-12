using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Builds flat-shaded, vertex-coloured low-poly meshes in code from boxes, prisms, wedges and the like: the held tools
    /// (GL269) and the town's buildings and people (GL400, GL404). Every triangle has its own vertices, so each face is lit
    /// flat. Parts can be placed through <see cref="Placement"/>, so a model is put together from parts in their own frames.
    /// </summary>
    public sealed class LowPolyMeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        /// <summary>Where the parts added next go: every point is moved by it. Identity by default.</summary>
        public Matrix4x4 Placement { get; set; } = Matrix4x4.identity;

        /// <summary>Vertices so far.</summary>
        public int VertexCount => vertices.Count;

        /// <summary>A box of a size, turned about its centre.</summary>
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

        /// <summary>An upright box standing on <paramref name="foot"/>, not turned.</summary>
        public void Block(Vector3 foot, Vector3 size, Color color)
        {
            Box(foot + Vector3.up * (size.y * 0.5f), size, Quaternion.identity, color);
        }

        /// <summary>
        /// A faceted ball stretched to <paramref name="radii"/> and turned about its centre, its poles along its own Z
        /// (GL604: the animals' bodies and heads). Few <paramref name="sides"/> and <paramref name="rings"/> keep it low-poly.
        /// </summary>
        public void Blob(Vector3 centre, Vector3 radii, Quaternion turn, Color color, int sides = 6, int rings = 4)
        {
            for (int r = 0; r < rings; r++)
            {
                float p0 = Mathf.PI * r / rings, p1 = Mathf.PI * (r + 1) / rings;
                for (int s = 0; s < sides; s++)
                {
                    float a0 = Mathf.PI * 2f * s / sides, a1 = Mathf.PI * 2f * (s + 1) / sides;
                    Quad(BlobPoint(centre, radii, turn, p0, a0), BlobPoint(centre, radii, turn, p0, a1),
                        BlobPoint(centre, radii, turn, p1, a1), BlobPoint(centre, radii, turn, p1, a0), centre, color);
                }
            }
        }

        private static Vector3 BlobPoint(Vector3 centre, Vector3 radii, Quaternion turn, float polar, float around)
        {
            float ring = Mathf.Sin(polar);
            return centre + turn * new Vector3(Mathf.Cos(around) * ring * radii.x, Mathf.Sin(around) * ring * radii.y, Mathf.Cos(polar) * radii.z);
        }

        /// <summary>A tapered prism along a line, with its ends closed.</summary>
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

        /// <summary>
        /// A flat blade from a thick back to a sharp edge: <paramref name="height"/> tall at the back, flaring to
        /// <paramref name="edge"/> at the front.
        /// </summary>
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

        /// <summary>
        /// A flat, tapering bar along a line: <paramref name="fromWidth"/> to <paramref name="toWidth"/> wide across x, and
        /// <paramref name="fromThickness"/> to <paramref name="toThickness"/> thick across the other side (a chisel's blade).
        /// </summary>
        public void Blade(Vector3 from, Vector3 to, float fromWidth, float toWidth, float fromThickness, float toThickness, Color color)
        {
            Vector3 along = (to - from).normalized;
            Vector3 width = Vector3.right;
            Vector3 thick = Vector3.Cross(along, width).normalized;
            Vector3 middle = (from + to) * 0.5f;
            Vector3 Corner(Vector3 at, float w, float t, float sw, float st) => at + width * (w * 0.5f * sw) + thick * (t * 0.5f * st);
            Vector3 b0 = Corner(from, fromWidth, fromThickness, -1f, -1f), b1 = Corner(from, fromWidth, fromThickness, 1f, -1f);
            Vector3 b2 = Corner(from, fromWidth, fromThickness, 1f, 1f), b3 = Corner(from, fromWidth, fromThickness, -1f, 1f);
            Vector3 t0 = Corner(to, toWidth, toThickness, -1f, -1f), t1 = Corner(to, toWidth, toThickness, 1f, -1f);
            Vector3 t2 = Corner(to, toWidth, toThickness, 1f, 1f), t3 = Corner(to, toWidth, toThickness, -1f, 1f);
            Quad(b0, t0, t1, b1, middle, color);
            Quad(b1, t1, t2, b2, middle, color);
            Quad(b2, t2, t3, b3, middle, color * 0.92f);
            Quad(b3, t3, t0, b0, middle, color);
            Quad(b0, b1, b2, b3, middle, color);
            Quad(t3, t2, t1, t0, middle, color * 1.1f);
        }

        /// <summary>An octahedron stretched to these half-sizes: a knapped stone, a spear point, a lump of ore.</summary>
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

        /// <summary>A roof of two sloping planks over a rectangle: its ridge runs along x, <paramref name="rise"/> above the eaves.</summary>
        public void GableRoof(Vector3 eavesCentre, float length, float depth, float rise, float thickness, Color color)
        {
            float half = depth * 0.5f;
            float slope = Mathf.Sqrt(half * half + rise * rise);
            float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            Vector3 front = eavesCentre + new Vector3(0f, rise * 0.5f, half * 0.5f);
            Vector3 back = eavesCentre + new Vector3(0f, rise * 0.5f, -half * 0.5f);
            Box(front, new Vector3(length, thickness, slope), Quaternion.Euler(angle, 0f, 0f), color);
            Box(back, new Vector3(length, thickness, slope), Quaternion.Euler(-angle, 0f, 0f), color * 0.92f);
        }

        /// <summary>A four-sided face; the side facing away from <paramref name="middle"/> is its front.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 middle, Color color)
        {
            Triangle(a, b, c, middle, color);
            Triangle(a, c, d, middle, color);
        }

        /// <summary>A triangle facing away from <paramref name="middle"/>.</summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 middle, Color color)
        {
            Matrix4x4 m = Placement;
            a = m.MultiplyPoint3x4(a);
            b = m.MultiplyPoint3x4(b);
            c = m.MultiplyPoint3x4(c);
            middle = m.MultiplyPoint3x4(middle);
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

        /// <summary>The mesh of everything added; 32-bit indices when it needs them.</summary>
        public Mesh Make(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
