using UnityEngine;

namespace Clube.Core
{
    /// <summary>Places the vertex where the density linearly crosses the iso level.</summary>
    public sealed class InterpolatedEdgePlacer : IEdgeVertexPlacer
    {
        public static readonly InterpolatedEdgePlacer Instance = new InterpolatedEdgePlacer();

        private InterpolatedEdgePlacer()
        {
        }

        public Vector3 Place(Vector3 cornerA, Vector3 cornerB, float valueA, float valueB, float isoLevel)
        {
            // InverseLerp returns 0 when the values are equal, which is a safe fallback.
            float t = Mathf.InverseLerp(valueA, valueB, isoLevel);
            return Vector3.Lerp(cornerA, cornerB, t);
        }
    }
}
