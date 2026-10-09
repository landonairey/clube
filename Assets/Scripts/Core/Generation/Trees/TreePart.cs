using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One solid piece of a tree (GL30): a tapered capsule from <see cref="From"/> to
    /// <see cref="To"/> (a trunk or branch segment, in wood), or a ball where the two ends meet
    /// (a clump of leaves). Plain values, so generation jobs read it as it is.
    /// </summary>
    public struct TreePart
    {
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

        /// <summary>
        /// How far inside the part a position is, in metres (negative outside), like a terrain
        /// generator's depth, so <see cref="TerrainDensity.FromDepth"/> turns it into a density.
        /// The radius blends along the axis, which is close enough to a true cone for the gentle
        /// tapers of branches.
        /// </summary>
        public float Depth(float3 position)
        {
            float3 axis = To - From;
            float lengthSquared = math.dot(axis, axis);
            float t = lengthSquared > 1e-8f ? math.saturate(math.dot(position - From, axis) / lengthSquared) : 0f;
            float radius = math.lerp(FromRadius, ToRadius, t);
            return radius - math.length(position - (From + axis * t));
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
