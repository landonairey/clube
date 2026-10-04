using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The world section of the lab panel (3A, M20): render distance, live counts, and how
    /// chunks are built (<see cref="ChunkShapeControls"/>). The render distance here drives
    /// the world view directly, as a lab override; in the Game scene it comes from the
    /// player's settings (A3, M3).
    /// </summary>
    public static class WorldControls
    {
        public static void Draw(LabPanelFrame frame, WorldView worldView)
        {
            worldView.RenderDistance = Mathf.RoundToInt(
                frame.Slider("Render distance (chunks)", worldView.RenderDistance, WorldView.MinRenderDistance, WorldView.MaxRenderDistance, "0"));

            int vertices = 0;
            int triangles = 0;
            foreach (ChunkRenderer chunkRenderer in worldView.Renderers.Values)
            {
                vertices += chunkRenderer.LastBuildStats.VertexCount;
                triangles += chunkRenderer.LastBuildStats.TriangleCount;
            }

            // Fixed lines, so the panel doesn't resize as the counts change while loading.
            World world = worldView.World;
            frame.Line($"Loaded {world.LoadedCount} · waiting {worldView.PendingCount} · edited {world.EditedCount}");
            frame.Line($"{vertices:N0} vertices · {triangles:N0} triangles");

            ChunkShapeControls.Draw(frame, worldView.Config, withLayers: true);
        }
    }
}
