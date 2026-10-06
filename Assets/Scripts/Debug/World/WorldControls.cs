using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The world section of the lab panel (3A, M20): render distance, live streaming counts,
    /// and how chunks are built (<see cref="ChunkShapeControls"/>). The render distance here
    /// drives the world view directly, as a lab override; in the Game scene it comes from the
    /// player's settings (A3, M3).
    /// </summary>
    public static class WorldControls
    {
        public static void Draw(LabPanelFrame frame, WorldView worldView)
        {
            worldView.RenderDistance = Mathf.RoundToInt(
                frame.Slider("Render distance (chunks)", worldView.RenderDistance, WorldView.MinRenderDistance, WorldView.MaxRenderDistance, "0"));

            // Fixed lines, so the panel doesn't resize as the counts change while loading.
            World world = worldView.World;
            StreamingStats stats = worldView.Stats;
            frame.Line($"Loaded {world.LoadedCount} · waiting {stats.Pending} · edited {world.EditedCount}");
            frame.Line($"Jobs: generating {stats.Generating} · meshing {stats.Meshing} · colliders {stats.Baking}");
            frame.Line($"{stats.Renderers:N0} with surface · {stats.Vertices:N0} vertices · {stats.Triangles:N0} triangles");
            frame.Line($"Streaming {stats.FrameMilliseconds:0.00} ms of the frame");

            ChunkShapeControls.Draw(frame, worldView.Config, withLayers: true);
        }
    }
}
