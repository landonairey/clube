using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Strategy for positioning a vertex on a crossed cube edge (V3). Chosen once
    /// per chunk build (A6), never switched on per vertex.
    /// </summary>
    public interface IEdgeVertexPlacer
    {
        /// <summary>Returns the vertex position between two corners, in the corners' space.</summary>
        Vector3 Place(Vector3 cornerA, Vector3 cornerB, float valueA, float valueB, float isoLevel);
    }
}
