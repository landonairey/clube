using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// In-game control panel for ChunkLab (K33, K31): drawn with IMGUI so it works in a
    /// build, where the custom inspectors don't exist. Foldout sections for the terrain
    /// (generator, seed, reset), the brush (tool, radius, strength, falloff), meshing
    /// (iso, edges, shading), step-through and display (samples, wireframe). Tab hides
    /// it. The frame (scrolling, click blocking, styles) is <see cref="LabPanelFrame"/>.
    /// </summary>
    /// <remarks>
    /// Edits the view's runtime config copy, then calls
    /// <see cref="WorldConfig.NotifyChanged"/> so the terrain regenerates, the same as
    /// an Inspector edit.
    /// </remarks>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkLabPanel : MonoBehaviour
    {
        private static readonly string[] WireframeNames = { "None", "Outline", "Grid" };

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
        private bool terrainOpen = true;

        [SerializeField]
        private bool brushOpen = true;

        [SerializeField]
        private bool meshingOpen;

        [SerializeField]
        private bool stepThroughOpen;

        [SerializeField]
        private bool displayOpen = true;

        [SerializeField]
        private bool cameraOpen;

        private ChunkView chunkView;
        private ChunkTerrainFill terrainFill;
        private TerrainBrushTool brush;
        private ChunkDebugView debugView;
        private StepThroughLab stepThrough;
        private LabPanelFrame frame;
        private FreeFlyCamera flyCamera;
        private bool open;

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
            terrainFill = GetComponent<ChunkTerrainFill>();
            brush = GetComponent<TerrainBrushTool>();
            debugView = GetComponent<ChunkDebugView>();
            stepThrough = GetComponentInChildren<StepThroughLab>(true);
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
            if (!open || chunkView.Config == null)
            {
                return;
            }

            frame.Width = width;
            frame.Begin();
            if (frame.Section("Terrain", ref terrainOpen))
            {
                DrawTerrain(chunkView.Config);
            }
            if (brush != null && frame.Section("Brush", ref brushOpen))
            {
                BrushControls.Draw(frame, brush);
            }
            if (frame.Section("Meshing", ref meshingOpen))
            {
                MeshingControls.Draw(frame, chunkView.Config);
            }
            if (stepThrough != null && frame.Section("Step-through", ref stepThroughOpen))
            {
                StepThroughControls.Draw(frame, stepThrough);
            }
            if (flyCamera != null && frame.Section("Camera", ref cameraOpen))
            {
                CameraControls.Draw(frame, flyCamera);
            }
            if (debugView != null && frame.Section("Display", ref displayOpen))
            {
                DrawDisplay();
            }
            DrawHelp();
            frame.End();
        }

        private void DrawTerrain(WorldConfig config)
        {
            TerrainControls.Draw(frame, config);
            if (terrainFill != null && terrainFill.IsOverridden)
            {
                GUILayout.Label("The test fill is on and overrides the generator.", frame.Hint);
            }
        }

        private void DrawDisplay()
        {
            // Only write real changes: each write makes the view rebuild its meshes.
            bool showSamples = frame.ToggleField("Show density samples", debugView.ShowSamples);
            if (showSamples != debugView.ShowSamples)
            {
                debugView.ShowSamples = showSamples;
            }
            var wireframe = (ChunkDebugView.Wireframe)frame.Toolbar("Wireframe", (int)debugView.WireframeMode, WireframeNames);
            if (wireframe != debugView.WireframeMode)
            {
                debugView.WireframeMode = wireframe;
            }
        }

        private void DrawHelp()
        {
            GUILayout.Space(8f);
            GUILayout.Label(
                "Right mouse look · WASD move · Q/E down/up · Shift fast\n" +
                "1/2/3 tool · [ ] radius · Tab hide panel",
                frame.Hint);
        }
    }
}
