using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Terrain a brush can aim at and edit (K13–K16, M5): one chunk (<see cref="ChunkView"/>)
    /// or a whole world of them (<see cref="WorldView"/>). Lets the same click tool edit
    /// either; positions are in world space.
    /// </summary>
    public interface IEditableTerrain
    {
        /// <summary>False until there is terrain to hit (e.g. before Play mode creates it).</summary>
        bool IsReady { get; }

        /// <summary>Where a world-space ray first meets the surface, in world space.</summary>
        bool Raycast(Ray worldRay, out Vector3 worldPoint);

        /// <summary>Adds or removes a sphere of terrain around a world-space centre; returns samples changed.</summary>
        int ApplyBrush(Vector3 worldCentre, BrushSettings brush, BrushOperation operation);
    }
}
