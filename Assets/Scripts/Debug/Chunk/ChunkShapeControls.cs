using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// How chunks are built, for the lab panel (K1, K31, M20): voxels per side, voxel size
    /// and, for a world, its height in chunk layers. Any change makes new chunks, so edits
    /// are lost. Edits the runtime config copy and calls <see cref="WorldConfig.NotifyChanged"/>.
    /// </summary>
    public static class ChunkShapeControls
    {
        private const float MinSide = 1f;
        private const float MaxSide = 48f;
        private const float MaxLayers = 8f;

        /// <param name="withLayers">Show the world's height in chunk layers (WorldLab).</param>
        public static void Draw(LabPanelFrame frame, WorldConfig config, bool withLayers)
        {
            int side = Mathf.RoundToInt(frame.Slider("Voxels per chunk side", config.ChunkSize.x, MinSide, MaxSide, "0"));
            // Quarter-unit steps, so the slider lands on tidy sizes.
            float voxelSize = Mathf.Round(frame.Slider("Voxel size (u)", config.VoxelSize, 0.25f, 2f, "0.00") * 4f) / 4f;
            int layers = withLayers
                ? Mathf.RoundToInt(frame.Slider("World height (chunk layers)", config.WorldHeightInChunks, 1f, MaxLayers, "0"))
                : config.WorldHeightInChunks;

            var size = new Vector3Int(side, side, side);
            if (size != config.ChunkSize || !Mathf.Approximately(voxelSize, config.VoxelSize) || layers != config.WorldHeightInChunks)
            {
                config.ChunkSize = size;
                config.VoxelSize = voxelSize;
                config.WorldHeightInChunks = layers;
                config.NotifyChanged();
            }

            frame.Line(withLayers
                ? $"Chunk {side * voxelSize:0.##} u wide · world {side * voxelSize * layers:0.##} u high"
                : $"Chunk {side * voxelSize:0.##} u wide · {side * side * side:N0} voxels");
        }
    }
}
