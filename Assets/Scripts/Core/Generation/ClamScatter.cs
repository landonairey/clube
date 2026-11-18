using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where clams lie on lake and ocean beds (GL735): the ground is cut into square cells, and each cell holds a clam or
    /// not, at a spot inside it, by a hash of the cell and a seed. The same cell always answers the same, so clams stay
    /// where they were and a streamed-out bed comes back as it was. Whether the spot is under water is the caller's.
    /// </summary>
    public static class ClamScatter
    {
        /// <summary>Cell side in metres.</summary>
        public const float CellSize = 6f;

        /// <summary>Share of cells that hold a clam.</summary>
        public const float Density = 0.4f;

        /// <summary>The cell a point is in.</summary>
        public static Vector2Int CellOf(float x, float z)
        {
            return new Vector2Int(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize));
        }

        /// <summary>True, with the clam's (x, z), when the cell holds one.</summary>
        public static bool TryClam(Vector2Int cell, int seed, out Vector2 position)
        {
            uint h = Hash(cell, seed);
            position = default;
            if ((h & 0xFFFF) / 65536f >= Density)
            {
                return false;
            }
            float u = ((h >> 16) & 0xFF) / 256f;
            float v = ((h >> 24) & 0xFF) / 256f;
            position = new Vector2((cell.x + 0.1f + 0.8f * u) * CellSize, (cell.y + 0.1f + 0.8f * v) * CellSize);
            return true;
        }

        private static uint Hash(Vector2Int cell, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u + (uint)cell.x * 668265263u + (uint)cell.y * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }
    }
}
