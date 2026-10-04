using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// In-game control panel for WorldLab (3A, K31): foldout sections for the world (render
    /// distance and live counts: loaded, waiting, edited, triangles, frame rate), the
    /// terrain, the brush, meshing, the focused chunk (M4) and display (chunk borders).
    /// Tab hides it. The frame (scrolling, click blocking, styles) is
    /// <see cref="LabPanelFrame"/>.
    /// </summary>
    /// <remarks>
    /// The render distance here drives the world view directly, as a lab override; in the
    /// Game scene it comes from the player's settings (A3, M3).
    /// </remarks>
    [RequireComponent(typeof(WorldView))]
    public class WorldLabPanel : MonoBehaviour
    {
        // The frame rate is frames counted over this many seconds.
        private const float FpsWindowSeconds = 0.5f;

        [Tooltip("Show the panel when Play mode starts. Tab toggles it.")]
        [SerializeField]
        private bool startOpen = true;

        [SerializeField, Min(200f)]
        private float width = 320f;

        [SerializeField]
        private Color background = new Color(0f, 0f, 0f, 0.7f);

        // Which sections are open; serialized so each scene picks its starting layout.
        [Header("Sections open at start")]
        [SerializeField]
        private bool worldOpen = true;

        [SerializeField]
        private bool terrainOpen;

        [SerializeField]
        private bool brushOpen = true;

        [SerializeField]
        private bool meshingOpen;

        [SerializeField]
        private bool focusOpen = true;

        [SerializeField]
        private bool displayOpen;

        private WorldView worldView;
        private TerrainBrushTool brush;
        private ChunkFocus chunkFocus;
        private WorldDebugView debugView;
        private LabPanelFrame frame;
        private bool open;

        private int windowFrames;
        private float windowSeconds;
        private float framesPerSecond;

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
            brush = GetComponent<TerrainBrushTool>();
            chunkFocus = GetComponent<ChunkFocus>();
            debugView = GetComponent<WorldDebugView>();
            frame = new LabPanelFrame(this, width, background);
            open = startOpen;
        }

        private void OnDisable()
        {
            frame.Hide();
        }

        private void Update()
        {
            windowFrames++;
            windowSeconds += Time.unscaledDeltaTime;
            if (windowSeconds >= FpsWindowSeconds)
            {
                framesPerSecond = windowFrames / windowSeconds;
                windowFrames = 0;
                windowSeconds = 0f;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                open = !open;
                if (!open)
                {
                    frame.Hide();
                }
            }
        }

        private void OnGUI()
        {
            if (!open || worldView.World == null)
            {
                return;
            }

            frame.Width = width;
            frame.Begin();
            if (frame.Section("World", ref worldOpen))
            {
                DrawWorld();
            }
            if (frame.Section("Terrain", ref terrainOpen))
            {
                TerrainControls.Draw(frame, worldView.Config);
                if (worldView.Problem != null)
                {
                    GUILayout.Label($"Generate skipped: {worldView.Problem}", frame.Hint);
                }
            }
            if (brush != null && frame.Section("Brush", ref brushOpen))
            {
                BrushControls.Draw(frame, brush);
            }
            if (frame.Section("Meshing", ref meshingOpen))
            {
                MeshingControls.Draw(frame, worldView.Config);
            }
            if (chunkFocus != null && frame.Section("Focused chunk", ref focusOpen))
            {
                DrawFocus();
            }
            if (debugView != null && frame.Section("Display", ref displayOpen))
            {
                debugView.ShowChunkBorders = frame.ToggleField("Chunk borders", debugView.ShowChunkBorders);
            }

            GUILayout.Space(8f);
            GUILayout.Label(
                "Right mouse look · WASD move · Q/E down/up · Shift fast\n" +
                "1/2/3 select, dig, add · [ ] radius · click a chunk to focus it · F frame it · Tab hide panel",
                frame.Hint);
            frame.End();
        }

        // Fixed lines, so the panel doesn't resize as the counts change while loading.
        private void DrawWorld()
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

            World world = worldView.World;
            Vector3Int focusChunk = worldView.FocusChunk;
            frame.Line($"{framesPerSecond:0} FPS · camera in chunk ({focusChunk.x}, {focusChunk.y}, {focusChunk.z})");
            frame.Line($"Loaded {world.LoadedCount} · waiting {worldView.PendingCount} · edited {world.EditedCount}");
            frame.Line($"{vertices:N0} vertices · {triangles:N0} triangles");
            Vector3Int size = worldView.Config.ChunkSize;
            frame.Line($"Chunks {size.x}×{size.y}×{size.z} voxels, {worldView.Config.WorldHeightInChunks} layers high");
        }

        private void DrawFocus()
        {
            string description = chunkFocus.DescribeFocused();
            if (description == null)
            {
                GUILayout.Label("Click the terrain (Select tool) to focus a chunk.", frame.Hint);
                return;
            }

            GUILayout.Label(description, frame.Label);
            switch (frame.ButtonRow("Frame it (F)", "Clear"))
            {
                case 0:
                    chunkFocus.FrameFocused();
                    break;
                case 1:
                    chunkFocus.Focus(null);
                    break;
            }
        }
    }
}
