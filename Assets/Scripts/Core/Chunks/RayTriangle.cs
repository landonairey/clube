using UnityEngine;

namespace Clube.Core
{
    /// <summary>Ray against a single triangle (Möller–Trumbore), hitting either side.</summary>
    public static class RayTriangle
    {
        private const float ParallelEpsilon = 1e-8f;

        /// <summary>
        /// True when <paramref name="ray"/> hits triangle a, b, c in front of its origin;
        /// <paramref name="distance"/> is then measured along the normalised ray direction.
        /// </summary>
        public static bool Intersect(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0f;
            Vector3 direction = ray.direction.normalized;
            Vector3 edgeAB = b - a;
            Vector3 edgeAC = c - a;

            Vector3 p = Vector3.Cross(direction, edgeAC);
            float determinant = Vector3.Dot(edgeAB, p);
            if (Mathf.Abs(determinant) < ParallelEpsilon)
            {
                return false;
            }

            float inverse = 1f / determinant;
            Vector3 fromA = ray.origin - a;
            float u = Vector3.Dot(fromA, p) * inverse;
            if (u < 0f || u > 1f)
            {
                return false;
            }

            Vector3 q = Vector3.Cross(fromA, edgeAB);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < 0f || u + v > 1f)
            {
                return false;
            }

            distance = Vector3.Dot(edgeAC, q) * inverse;
            return distance >= 0f;
        }
    }
}
