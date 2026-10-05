using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The build grid's part of the lab panel (M22): on or off, the cell size, and how
    /// many marching-cubes voxels fit across one cell at the current voxel size, which is
    /// what the 1-16 voxels-per-metre test compares.
    /// </summary>
    public static class BuildGridControls
    {
        public static void Draw(LabPanelFrame frame, BuildGridOverlay grid, float voxelSize)
        {
            bool show = frame.ToggleField("Build grid on the terrain", grid.Show);
            if (show != grid.Show)
            {
                grid.Show = show;
            }
            if (!show)
            {
                return;
            }

            // Quarter-metre steps.
            float cell = Mathf.Round(frame.Slider("Build cell (m)", grid.CellSize, BuildGridOverlay.MinCellSize, BuildGridOverlay.MaxCellSize, "0.##") * 4f) / 4f;
            if (!Mathf.Approximately(cell, grid.CellSize))
            {
                grid.CellSize = cell;
            }

            if (voxelSize > 0f)
            {
                frame.Line($"{grid.CellSize / voxelSize:0.##} marching voxels across one cell");
            }
        }
    }
}
