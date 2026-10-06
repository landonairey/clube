using UnityEngine;

namespace Clube.Core
{
    /// <summary>Places the vertex at the edge midpoint, ignoring the densities.</summary>
    public sealed class MidpointEdgePlacer : IEdgeVertexPlacer
    {
        public static readonly MidpointEdgePlacer Instance = new MidpointEdgePlacer();

        private MidpointEdgePlacer()
        {
        }

        public Vector3 Place(Vector3 cornerA, Vector3 cornerB, float valueA, float valueB, float isoLevel)
        {
            return (cornerA + cornerB) * 0.5f;
        }
    }
}
