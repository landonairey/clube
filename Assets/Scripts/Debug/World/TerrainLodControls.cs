using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The coarse voxel terrain's part of the lab panel (P7c): on or off, how many levels, how wide
    /// each is, a tint per level to see the rings, and each level's voxel size and chunk counts.
    /// </summary>
    public static class TerrainLodControls
    {
        public static void Draw(LabPanelFrame frame, TerrainLodView lod)
        {
            bool show = frame.ToggleField("Coarse terrain", lod.Show);
            if (show != lod.Show)
            {
                lod.Show = show;
            }
            if (!show)
            {
                return;
            }

            int levels = Mathf.RoundToInt(frame.Slider("Levels", lod.Levels, 1f, TerrainLodView.MaxLevels, "0"));
            if (levels != lod.Levels)
            {
                lod.Levels = levels;
            }
            int half = Mathf.RoundToInt(frame.Slider("Chunks each way", lod.HalfChunks, 4f, 16f, "0"));
            if (half != lod.HalfChunks)
            {
                lod.HalfChunks = half;
            }
            bool tint = frame.ToggleField("Tint each level", lod.TintLevels);
            if (tint != lod.TintLevels)
            {
                lod.TintLevels = tint;
            }

            if (lod.Problem != null)
            {
                frame.Line(lod.Problem);
                return;
            }
            if (lod.LevelCount == 0)
            {
                return;
            }
            frame.Line($"Reaches {lod.PlannedReach / 1000f:0.#} km · {lod.LastUpdateMilliseconds:0.00} ms a frame");
            for (int i = 0; i < lod.LevelCount; i++)
            {
                lod.CountOf(i, out int drawn, out int waiting);
                string voxel = lod.VoxelSizeOf(i) < 1f ? $"{lod.VoxelSizeOf(i) * 100f:0} cm" : $"{lod.VoxelSizeOf(i):0} m";
                frame.Line(waiting > 0 ? $"{i + 1}: {voxel} voxels, {drawn} chunks, {waiting} to build" : $"{i + 1}: {voxel} voxels, {drawn} chunks");
            }
        }
    }
}
