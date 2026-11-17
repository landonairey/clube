using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL718: the roads painted into one picture over the map, for the terrain past the world's chunks to colour its
    /// ground with (the coarse and far terrain have no road voxels). Each texel holds, in alpha, how much of it the road
    /// over it covers (a road narrower than a texel still shows, fainter) and the road's colour times that (premultiplied),
    /// so a filtered sample composites as ground × (1 − a) + rgb with no dark fringe.
    /// </summary>
    public sealed class RoadMask
    {
        /// <summary>A road to paint: its line (x, z), half its width in metres and its surface colour.</summary>
        public readonly struct Road
        {
            public Road(IReadOnlyList<Vector2> points, float halfWidth, Color32 colour, IReadOnlyList<RoadTunnel> tunnels = null)
            {
                Points = points;
                HalfWidth = halfWidth;
                Colour = colour;
                Tunnels = tunnels;
            }

            public IReadOnlyList<Vector2> Points { get; }
            public float HalfWidth { get; }
            public Color32 Colour { get; }
            /// <summary>Its tunnels: straight parts of its line under a ridge, left unpainted (the ridge over them is not road).</summary>
            public IReadOnlyList<RoadTunnel> Tunnels { get; }
        }

        public RoadMask(Rect area, int size)
        {
            Area = area;
            Size = Mathf.Max(1, size);
            Texels = new Color32[Size * Size];
        }

        /// <summary>The square of ground (x, z) the picture covers; texel (0, 0) is at its minimum corner.</summary>
        public Rect Area { get; }

        public int Size { get; }

        /// <summary>Metres of ground across one texel.</summary>
        public float TexelSize => Area.width / Size;

        /// <summary>Row by row from the minimum z, as a texture takes them.</summary>
        public Color32[] Texels { get; }

        /// <summary>Paints a road over what is there; where roads cross, the one covering more of a texel wins it.</summary>
        public void Paint(Road road)
        {
            if (road.Points == null)
            {
                return;
            }
            for (int i = 1; i < road.Points.Count; i++)
            {
                Vector2 a = road.Points[i - 1], b = road.Points[i];
                if (!InTunnel((a + b) * 0.5f, road.Tunnels))
                {
                    PaintSegment(a, b, road.HalfWidth, road.Colour);
                }
            }
        }

        private static bool InTunnel(Vector2 point, IReadOnlyList<RoadTunnel> tunnels)
        {
            if (tunnels == null)
            {
                return false;
            }
            foreach (RoadTunnel tunnel in tunnels)
            {
                if (tunnel.DistanceTo(point, out float along) < 1f && along > 0f && along < tunnel.Length)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How much of the texel over a point (x, z) a road covers, 0 to 1; 0 outside the area.</summary>
        public float CoverageAt(Vector2 point)
        {
            Vector2Int texel = TexelOf(point);
            bool inside = texel.x >= 0 && texel.y >= 0 && texel.x < Size && texel.y < Size;
            return inside ? Texels[texel.y * Size + texel.x].a / 255f : 0f;
        }

        public Vector2Int TexelOf(Vector2 point)
        {
            float texel = TexelSize;
            return new Vector2Int(Mathf.FloorToInt((point.x - Area.xMin) / texel), Mathf.FloorToInt((point.y - Area.yMin) / texel));
        }

        private void PaintSegment(Vector2 a, Vector2 b, float half, Color32 colour)
        {
            float texel = TexelSize;
            float reach = half + texel;
            Vector2Int min = TexelOf(Vector2.Min(a, b) - Vector2.one * reach);
            Vector2Int max = TexelOf(Vector2.Max(a, b) + Vector2.one * reach);
            min = Vector2Int.Max(min, Vector2Int.zero);
            max = Vector2Int.Min(max, new Vector2Int(Size - 1, Size - 1));
            Vector2 along = b - a;
            float lengthSquared = Mathf.Max(along.sqrMagnitude, 1e-6f);
            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    var centre = new Vector2(Area.xMin + (x + 0.5f) * texel, Area.yMin + (y + 0.5f) * texel);
                    float t = Mathf.Clamp01(Vector2.Dot(centre - a, along) / lengthSquared);
                    float distance = Vector2.Distance(centre, a + along * t);
                    // The share of the texel inside the road's edge, roughly: full within it, none a texel past it.
                    float cover = Mathf.Clamp01((half + texel * 0.5f - distance) / texel);
                    if (half < texel * 0.5f)
                    {
                        cover *= half * 2f / texel;
                    }
                    byte alpha = (byte)Mathf.RoundToInt(cover * 255f);
                    int index = y * Size + x;
                    if (alpha > Texels[index].a)
                    {
                        Texels[index] = new Color32(Times(colour.r, alpha), Times(colour.g, alpha), Times(colour.b, alpha), alpha);
                    }
                }
            }
        }

        private static byte Times(byte channel, byte alpha) => (byte)((channel * alpha + 127) / 255);
    }
}
