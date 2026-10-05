using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// How chunks are built, for the lab panel (K1, K31, M20): voxels per side, voxel size
    /// (down to 1/16 m, or picked as marching voxels per metre) and, for a world, its height
    /// in chunk layers. Any change makes new chunks, so edits
    /// are lost. Edits the runtime config copy and calls <see cref="WorldConfig.NotifyChanged"/>.
    /// </summary>
    public static class ChunkShapeControls
    {
        private const float MinSide = 1f;
        private const float MaxSide = 64f;
        private const float MinVoxelSize = 1f / 16f;
        private const float MaxVoxelSize = 2f;
        private const float MaxLayers = 8f;

        // Marching voxels per metre (one build-grid cell): the sizes the gameplay test compares.
        private static readonly int[] VoxelsPerMetre = { 1, 2, 4, 8, 16 };
        private static readonly string[] VoxelsPerMetreNames = { "1", "2", "4", "8", "16" };

        /// <param name="withLayers">Show the world's height in chunk layers (WorldLab).</param>
        public static void Draw(LabPanelFrame frame, WorldConfig config, bool withLayers)
        {
            int side = Mathf.RoundToInt(frame.Slider("Voxels per chunk side", config.ChunkSize.x, MinSide, MaxSide, "0"));
            // Sixteenth-of-a-metre steps, so the slider lands on tidy sizes.
            float voxelSize = Mathf.Round(frame.Slider("Voxel size (m)", config.VoxelSize, MinVoxelSize, MaxVoxelSize, "0.####") * 16f) / 16f;
            int perMetre = System.Array.IndexOf(VoxelsPerMetre, Mathf.RoundToInt(1f / voxelSize));
            bool exact = perMetre >= 0 && Mathf.Approximately(voxelSize * VoxelsPerMetre[perMetre], 1f);
            int pickedPerMetre = frame.Toolbar("Voxels per metre", exact ? perMetre : -1, VoxelsPerMetreNames);
            if (pickedPerMetre >= 0 && pickedPerMetre != (exact ? perMetre : -1))
            {
                voxelSize = 1f / VoxelsPerMetre[pickedPerMetre];
            }
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
                ? $"Chunk {side * voxelSize:0.##} m wide · world {side * voxelSize * layers:0.##} m high"
                : $"Chunk {side * voxelSize:0.##} m wide · {side * side * side:N0} voxels");
        }
    }
}
