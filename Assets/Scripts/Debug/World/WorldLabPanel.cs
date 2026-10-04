using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// In-game control panel for WorldLab (3A, K31): foldout sections for the world (render
    /// distance, live counts, and how chunks are built: voxels per side, voxel size,
    /// layers), the terrain and its shape, the brush with its volume totals, meshing, the
    /// focused chunk (M4), the camera's speeds and display (chunk borders). Tab hides it.
    /// The frame (scrolling, click blocking, styles) is <see cref="LabPanelFrame"/>.
    /// Position and frame rate are on the <see cref="WorldLabHud"/>.
    /// </summary>
    /// <remarks>
    /// The render distance here drives the world view directly, as a lab override; in the
    /// Game scene it comes from the player's settings (A3, M3).
    /// </remarks>
    [RequireComponent(typeof(WorldView))]
    public class WorldLabPanel : MonoBehaviour
    {
        private const float MinChunkSide = 4f;
        private const float MaxChunkSide = 48f;
        private const float MaxLayers = 8f;

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

        [SerializeField]
        private bool cameraOpen;

        private WorldView worldView;
        private TerrainBrushTool brush;
        private ChunkFocus chunkFocus;
        private WorldDebugView debugView;
        private LabPanelFrame frame;
        private FreeFlyCamera flyCamera;
        private bool open;

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
            brush = GetComponent<TerrainBrushTool>();
            chunkFocus = GetComponent<ChunkFocus>();
            debugView = GetComponent<WorldDebugView>();
            frame = new LabPanelFrame(this, width, background);
            flyCamera = Camera.main != null ? Camera.main.GetComponent<FreeFlyCamera>() : null;
            open = startOpen;
        }

        private void OnDisable()
        {
            frame.Hide();
        }

        private void Update()
        {
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
            if (flyCamera != null && frame.Section("Camera", ref cameraOpen))
            {
                CameraControls.Draw(frame, flyCamera);
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
            frame.Line($"Loaded {world.LoadedCount} · waiting {worldView.PendingCount} · edited {world.EditedCount}");
            frame.Line($"{vertices:N0} vertices · {triangles:N0} triangles");

            DrawChunkShape(worldView.Config);
        }

        // How chunks are built: voxels per side, voxel size, layers. Any change regenerates
        // the world, so edits are lost.
        private void DrawChunkShape(WorldConfig config)
        {
            int side = Mathf.RoundToInt(frame.Slider("Voxels per chunk side", config.ChunkSize.x, MinChunkSide, MaxChunkSide, "0"));
            // Quarter-unit steps, so the slider lands on tidy sizes.
            float voxelSize = Mathf.Round(frame.Slider("Voxel size (u)", config.VoxelSize, 0.25f, 2f, "0.00") * 4f) / 4f;
            int layers = Mathf.RoundToInt(frame.Slider("World height (chunk layers)", config.WorldHeightInChunks, 1f, MaxLayers, "0"));

            var size = new Vector3Int(side, side, side);
            if (size != config.ChunkSize || !Mathf.Approximately(voxelSize, config.VoxelSize) || layers != config.WorldHeightInChunks)
            {
                config.ChunkSize = size;
                config.VoxelSize = voxelSize;
                config.WorldHeightInChunks = layers;
                config.NotifyChanged();
            }
            frame.Line($"Chunk {side * voxelSize:0.##} u wide · world {side * voxelSize * layers:0.##} u high");
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
