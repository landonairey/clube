using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The build grid (GL17, GL23): square cells of <see cref="CellSize"/> metres across the
    /// ground, the unit placed objects snap to and hard material stacks in. Columns only: a
    /// cell is (x, z), relative to the world origin, floored so negatives work.
    /// </summary>
    public readonly struct BuildGrid
    {
        public BuildGrid(float cellSize)
        {
            CellSize = Mathf.Max(0.01f, cellSize);
        }

        public float CellSize { get; }

        /// <summary>The cell a position (relative to the world origin) lies in.</summary>
        public Vector2Int Cell(Vector3 position)
        {
            return new Vector2Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.z / CellSize));
        }

        /// <summary>A cell's centre, (x, z) relative to the world origin.</summary>
        public Vector2 Centre(Vector2Int cell)
        {
            return ((Vector2)cell + Vector2.one * 0.5f) * CellSize;
        }

        /// <summary>
        /// The global sample columns inside a cell, both ends included: those whose positions lie
        /// in [cell, cell + 1) × <see cref="CellSize"/>. Empty (max below min) when the cell is
        /// smaller than a voxel.
        /// </summary>
        public void SampleColumns(Vector2Int cell, float voxelSize, out Vector2Int min, out Vector2Int max)
        {
            min = new Vector2Int(
                Mathf.CeilToInt(cell.x * CellSize / voxelSize - 1e-4f),
                Mathf.CeilToInt(cell.y * CellSize / voxelSize - 1e-4f));
            max = new Vector2Int(
                Mathf.CeilToInt((cell.x + 1) * CellSize / voxelSize - 1e-4f) - 1,
                Mathf.CeilToInt((cell.y + 1) * CellSize / voxelSize - 1e-4f) - 1);
        }
    }
}
