using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The vertex streams of a sheet of the map's water (<see cref="WaterMap"/>) while it is built, in the layout of a
    /// chunk's water (<see cref="WaterMesher"/>): every triangle has its own three vertices and carries its centre and
    /// kind, so the shader shades it as one facet. <see cref="AddCell"/> is the full detail: two triangles per map cell.
    /// <see cref="CoarseWaterMesher"/> fills the same streams with far fewer triangles for distant water (GL626).
    /// </summary>
    public sealed class WaterSheetMesh
    {
        /// <summary>The shader's kind of a flowing facet, and of a still one.</summary>
        public const float RiverLook = 1f, StillLook = 2f;

        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector2> Flows = new List<Vector2>();
        public readonly List<Vector4> Looks = new List<Vector4>();
        public readonly List<Vector4> Ripples = new List<Vector4>();
        public readonly List<int> Triangles = new List<int>();

        public int TriangleCount => Triangles.Count / 3;

        public void Clear()
        {
            Vertices.Clear();
            Flows.Clear();
            Looks.Clear();
            Ripples.Clear();
            Triangles.Clear();
        }

        /// <summary>One triangle (clockwise seen from above), with the ripple inputs of its corners.</summary>
        public void Add(Vector3 p, Vector3 q, Vector3 r, Vector4 rp, Vector4 rq, Vector4 rr, Vector2 flow, float kind)
        {
            Vector3 centre = (p + q + r) / 3f;
            var look = new Vector4(centre.x, centre.y, centre.z, kind);
            Add(p, rp, flow, look);
            Add(q, rq, flow, look);
            Add(r, rr, flow, look);
        }

        private void Add(Vector3 position, Vector4 ripple, Vector2 flow, Vector4 look)
        {
            Triangles.Add(Vertices.Count);
            Vertices.Add(position);
            Flows.Add(flow);
            Looks.Add(look);
            Ripples.Add(ripple);
        }

        /// <summary>
        /// Two triangles over a wet cell, the diagonal alternating like a checkerboard, so the facets don't line up.
        /// Corners are shared with the cells round them (<see cref="Corner"/>), so the sheet never cracks.
        /// </summary>
        public void AddCell(in WaterMap water, int x, int z)
        {
            Vector3 a = Corner(water, x, z, out Vector4 ra), b = Corner(water, x + 1, z, out Vector4 rb);
            Vector3 c = Corner(water, x + 1, z + 1, out Vector4 rc), d = Corner(water, x, z + 1, out Vector4 rd);
            int cell = x + z * water.Size.x;
            float2 riverFlow = water.Flow[cell];
            bool river = water.Kind[cell] == (byte)WaterKind.River;
            Vector2 flow = river && math.lengthsq(riverFlow) > 1e-6f ? new Vector2(riverFlow.x, riverFlow.y).normalized * 0.4f : Vector2.zero;
            float kind = river ? RiverLook : StillLook;
            if (((x + z) & 1) == 0)
            {
                Add(a, d, c, ra, rd, rc, flow, kind);
                Add(a, c, b, ra, rc, rb, flow, kind);
            }
            else
            {
                Add(a, d, b, ra, rd, rb, flow, kind);
                Add(b, d, c, rb, rd, rc, flow, kind);
            }
        }

        /// <summary>
        /// A corner of the cell grid: at the average height of the wet cells round it (exactly their height when they
        /// agree, so level water is level to the last bit), with their average flow and share of river for the ripples.
        /// </summary>
        public static Vector3 Corner(in WaterMap water, int cx, int cz, out Vector4 ripple)
        {
            int2 size = water.Size;
            float sum = 0f, river = 0f, low = float.MaxValue, high = float.MinValue;
            var flow = Vector2.zero;
            int count = 0;
            for (int dz = -1; dz <= 0; dz++)
            {
                for (int dx = -1; dx <= 0; dx++)
                {
                    int nx = cx + dx, nz = cz + dz;
                    int n = nx + nz * size.x;
                    if (nx >= 0 && nz >= 0 && nx < size.x && nz < size.y && water.Kind[n] != (byte)WaterKind.Dry)
                    {
                        float surface = water.Surface[n];
                        sum += surface;
                        low = Mathf.Min(low, surface);
                        high = Mathf.Max(high, surface);
                        float2 f = water.Flow[n];
                        flow += new Vector2(f.x, f.y);
                        river += water.Kind[n] == (byte)WaterKind.River ? 1f : 0f;
                        count++;
                    }
                }
            }
            float height = count == 0 ? 0f : low == high ? low : sum / count;
            count = Mathf.Max(1, count);
            ripple = new Vector4(flow.x / count, flow.y / count, river / count, 0f);
            return new Vector3(water.Origin.x + cx * water.CellSize, height, water.Origin.y + cz * water.CellSize);
        }
    }
}
