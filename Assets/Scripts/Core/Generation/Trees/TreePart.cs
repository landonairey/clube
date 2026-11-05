using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One solid piece stamped into the terrain after it's generated: a tree's tapered capsule from <see cref="From"/> to
    /// <see cref="To"/> (a trunk or branch segment, in wood) or ball where the two ends meet (a clump of leaves, GL30), or a
    /// large boulder (GL342, <see cref="Boulder"/>): a squashed, turned ellipsoid cut by up to four planes. Plain values, so
    /// generation jobs read it as it is.
    /// </summary>
    public struct TreePart
    {
        /// <summary>A boulder's half-widths along its own x, y and z; zero for tree parts.</summary>
        public float3 Radii;

        /// <summary>A boulder's turn about the vertical: (cos, sin).</summary>
        public float2 Turn;

        /// <summary>A boulder's flat faces, in its own frame: (normal, distance from its centre); a zero normal is no cut.</summary>
        public float4 Cut0, Cut1, Cut2, Cut3;

        /// <summary>True for a boulder.</summary>
        public bool IsBoulder => Radii.x > 0f;

        /// <summary>
        /// A boulder centred at <paramref name="centre"/>: an ellipsoid of half-widths <paramref name="radii"/> turned by
        /// <paramref name="turn"/> radians about the vertical, flattened by up to four <paramref name="cuts"/> (plane normal in the
        /// boulder's frame, and distance from the centre).
        /// </summary>
        public static TreePart Boulder(float3 centre, float3 radii, float turn, float4 cut0, float4 cut1, float4 cut2, float4 cut3, byte material)
        {
            float reach = math.cmax(radii);
            math.sincos(turn, out float sin, out float cos);
            return new TreePart
            {
                From = centre, To = centre, FromRadius = reach, ToRadius = reach, Material = material,
                Radii = radii, Turn = new float2(cos, sin), Cut0 = cut0, Cut1 = cut1, Cut2 = cut2, Cut3 = cut3,
            };
        }
        /// <summary>Start of the axis, relative to the world origin.</summary>
        public float3 From;

        /// <summary>End of the axis; equal to <see cref="From"/> for a ball.</summary>
        public float3 To;

        public float FromRadius;
        public float ToRadius;
        public byte Material;

        public static TreePart Segment(float3 from, float3 to, float fromRadius, float toRadius, byte material)
        {
            return new TreePart { From = from, To = to, FromRadius = fromRadius, ToRadius = toRadius, Material = material };
        }

        public static TreePart Ball(float3 centre, float radius, byte material)
        {
            return new TreePart { From = centre, To = centre, FromRadius = radius, ToRadius = radius, Material = material };
        }

        /// <summary>True for a trunk or branch segment (wood), false for a ball of leaves (GL189).</summary>
        public bool IsSegment => math.any(From != To);

        /// <summary>
        /// How far inside the part a position is, in metres (negative outside), like a terrain
        /// generator's depth, so <see cref="TerrainDensity.FromDepth"/> turns it into a density.
        /// The radius blends along the axis, which is close enough to a true cone for the gentle
        /// tapers of branches.
        /// </summary>
        public float Depth(float3 position)
        {
            if (IsBoulder)
            {
                return BoulderDepth(position);
            }
            float3 axis = To - From;
            float lengthSquared = math.dot(axis, axis);
            float t = lengthSquared > 1e-8f ? math.saturate(math.dot(position - From, axis) / lengthSquared) : 0f;
            float radius = math.lerp(FromRadius, ToRadius, t);
            return radius - math.length(position - (From + axis * t));
        }

        // Inside the ellipsoid and behind every cut: the least of their depths. The ellipsoid's is the usual cheap estimate
        // (scaled to its smallest half-width), close enough near the surface, which is all the density ramp sees.
        private float BoulderDepth(float3 position)
        {
            float3 p = position - From;
            var local = new float3(Turn.x * p.x + Turn.y * p.z, p.y, -Turn.y * p.x + Turn.x * p.z);
            float depth = (1f - math.length(local / Radii)) * math.cmin(Radii);
            depth = math.min(depth, CutDepth(local, Cut0));
            depth = math.min(depth, CutDepth(local, Cut1));
            depth = math.min(depth, CutDepth(local, Cut2));
            return math.min(depth, CutDepth(local, Cut3));
        }

        private static float CutDepth(float3 local, float4 cut)
        {
            return math.any(cut.xyz != 0f) ? cut.w - math.dot(local, cut.xyz) : float.MaxValue;
        }

        /// <summary>The box holding the part, grown by <paramref name="margin"/> on every side.</summary>
        public Bounds Bounds(float margin)
        {
            float reach = math.max(FromRadius, ToRadius) + margin;
            float3 min = math.min(From, To) - reach;
            float3 max = math.max(From, To) + reach;
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }
}
