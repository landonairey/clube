using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// How chunks are built, for the lab panel (K1, K31, M20): voxels per side, voxel size
    /// (down to 1/16 m, or picked as marching voxels per metre) and, for a world, its height
    /// in chunk layers. Changing the voxel size keeps the chunk and world size in metres
    /// (<see cref="ChunkSizing"/>, M25), so the terrain stays the same, only sampled finer or
    /// coarser. Any change makes new chunks, so edits are lost. Also the labs' edge walls (M26) and, for a
    /// world, a fixed area instead of streaming (M27). Edits the runtime config copy and calls <see cref="WorldConfig.NotifyChanged"/>.
    /// </summary>
    public static class ChunkShapeControls
    {
        private const float MinSide = 1f;
        private const float MaxSide = 64f;
        private const float MinVoxelSize = 1f / 16f;
        private const float MaxVoxelSize = 2f;
        private const float MaxLayers = 160f;

        // Chunk sides the voxel size picker chooses between: a world keeps 16³ chunks, the
        // plan of record (M17: every mesh fits 16-bit indices); a lone chunk can be bigger.
        private const int KeepMinSide = 16;
        private const int KeepMaxWorldSide = 16;
        private const int KeepMaxChunkSide = 64;

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

            if (!Mathf.Approximately(voxelSize, config.VoxelSize))
            {
                ChunkSizing.Shape shape = ChunkSizing.KeepWorldSize(
                    config.ChunkSize.x, config.VoxelSize, config.WorldHeightInChunks, voxelSize,
                    KeepMinSide, withLayers ? KeepMaxWorldSide : KeepMaxChunkSide, (int)MaxLayers);
                side = shape.Side;
                layers = withLayers ? shape.Layers : layers;
            }

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
            frame.Line(ChunkIndexBudget.Fits16Bit(size)
                ? "Meshes always fit 16-bit indices (any GPU)"
                : $"Meshes can reach {ChunkIndexBudget.MaxVertices(size):N0} vertices: 32-bit indices, not on every GPU");

            DrawEdges(frame, config, withLayers);
        }

        // Sealed edges (walls showing the underground) and, for a world, a fixed area instead of streaming.
        private static void DrawEdges(LabPanelFrame frame, WorldConfig config, bool withLayers)
        {
            bool seal = frame.ToggleField(withLayers ? "Walls on the fixed area's edges" : "Walls on the chunk's edges", config.SealEdges);
            bool fixedArea = withLayers ? frame.ToggleField("Fixed area (no streaming)", config.FixedArea) : config.FixedArea;
            float areaSize = withLayers && fixedArea
                ? Mathf.Round(frame.Slider("Fixed area size (m)", config.FixedAreaSize, 16f, 400f, "0"))
                : config.FixedAreaSize;
            if (seal != config.SealEdges || fixedArea != config.FixedArea || !Mathf.Approximately(areaSize, config.FixedAreaSize))
            {
                config.SealEdges = seal;
                config.FixedArea = fixedArea;
                config.FixedAreaSize = areaSize;
                config.NotifyChanged();
            }
            if (withLayers && fixedArea && config.FixedColumns is RectInt columns)
            {
                float width = columns.width * config.ChunkSize.x * config.VoxelSize;
                frame.Line($"{columns.width} x {columns.height} chunk columns · {width:0.#} m square");
            }
            else if (withLayers && seal)
            {
                frame.Line("Walls only show on a fixed area.");
            }
        }
    }
}
