using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A generator defined by a ground height at each (x, z): solid below it, empty
    /// above. Subclasses only say how tall the ground is.
    /// </summary>
    public abstract class HeightfieldGenerator : ITerrainGenerator
    {
        public float Density(Vector3 position)
        {
            return TerrainDensity.FromDepth(Height(position.x, position.z) - position.y);
        }

        /// <summary>Ground height in world units at a horizontal position.</summary>
        public abstract float Height(float x, float z);
    }
}
