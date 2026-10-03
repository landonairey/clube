using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// In-game control panel for ChunkLab (K33, the start of K31): the settings the
    /// demo needs, drawn with IMGUI so they work in a build, where the custom
    /// inspectors don't exist. Terrain (generator, seed, reset), the brush (tool,
    /// radius, strength, falloff) and display (samples, wireframe). Tab hides it; in a
    /// build, Esc quits. Clicks on it never reach the brush or voxel selection
    /// (<see cref="LabGuiBlocker"/>).
    /// </summary>
    /// <remarks>
    /// Edits the view's runtime config copy, then calls
    /// <see cref="WorldConfig.NotifyChanged"/> so the terrain regenerates, the same as
    /// an Inspector edit.
    /// </remarks>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkLabPanel : MonoBehaviour
    {
        private const float Margin = 10f;

        private static readonly string[] ToolNames = { "Select", "Dig", "Add" };
        private static readonly string[] FalloffNames = { "Hard", "Smooth" };
        private static readonly string[] WireframeNames = { "None", "Outline", "Grid" };

        [Tooltip("Show the panel when Play mode starts. Tab toggles it.")]
        [SerializeField]
        private bool startOpen = true;

        [SerializeField, Min(200f)]
        private float width = 300f;

        [SerializeField]
        private Color background = new Color(0f, 0f, 0f, 0.7f);

        private readonly List<TerrainGeneratorType> generatorChoices = new List<TerrainGeneratorType>();
        private readonly List<string> generatorNames = new List<string>();

        private ChunkView chunkView;
        private ChunkTerrainFill terrainFill;
        private TerrainBrushTool brush;
        private ChunkDebugView debugView;
        private bool open;
        private Rect panelRect;

        private GUIStyle padding;
        private GUIStyle header;
        private GUIStyle label;
        private GUIStyle hint;
        private GUIStyle button;
        private GUIStyle toggle;

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
            terrainFill = GetComponent<ChunkTerrainFill>();
            brush = GetComponent<TerrainBrushTool>();
            debugView = GetComponent<ChunkDebugView>();
            open = startOpen;
        }

        private void OnDisable()
        {
            LabGuiBlocker.SetArea(this, null);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                open = !open;
                if (!open)
                {
                    LabGuiBlocker.SetArea(this, null);
                }
            }
            if (keyboard.escapeKey.wasPressedThisFrame && !Application.isEditor)
            {
                Application.Quit();
            }
        }

        private void OnGUI()
        {
            if (!open || chunkView.Config == null)
            {
                return;
            }

            CreateStyles();
            float x = Screen.width - width - Margin;
            if (Event.current.type == EventType.Repaint && panelRect.width > 0f)
            {
                GuiDrawing.Rect(panelRect, background);
            }

            GUILayout.BeginArea(new Rect(x, Margin, width, Screen.height - 2f * Margin));
            GUILayout.BeginVertical(padding);
            DrawTerrain(chunkView.Config);
            DrawBrush();
            DrawDisplay();
            DrawHelp();
            GUILayout.EndVertical();

            // The background and click blocking use the size the layout came out at.
            if (Event.current.type == EventType.Repaint)
            {
                Rect laidOut = GUILayoutUtility.GetLastRect();
                panelRect = new Rect(x + laidOut.x, Margin + laidOut.y, laidOut.width, laidOut.height);
                LabGuiBlocker.SetArea(this, panelRect);
            }
            GUILayout.EndArea();
        }

        private void DrawTerrain(WorldConfig config)
        {
            TerrainSettings terrain = config.Terrain;
            GUILayout.Label("Terrain", header);

            // Heightmap needs an image asset, so it's only offered when one is assigned.
            generatorChoices.Clear();
            generatorNames.Clear();
            foreach (TerrainGeneratorType type in Enum.GetValues(typeof(TerrainGeneratorType)))
            {
                if (type != TerrainGeneratorType.Heightmap || terrain.HeightmapSource != null)
                {
                    generatorChoices.Add(type);
                    generatorNames.Add(type.ToString());
                }
            }

            int current = generatorChoices.IndexOf(terrain.Generator);
            int picked = GUILayout.SelectionGrid(current, generatorNames.ToArray(), 2, button);
            if (picked != current && picked >= 0)
            {
                terrain.Generator = generatorChoices[picked];
                config.NotifyChanged();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Seed {terrain.Seed}", label, GUILayout.Width(110f));
            int seed = terrain.Seed;
            if (GUILayout.Button("−", button))
            {
                seed--;
            }
            if (GUILayout.Button("+", button))
            {
                seed++;
            }
            if (GUILayout.Button("Random", button))
            {
                seed = UnityEngine.Random.Range(0, 100000);
            }
            GUILayout.EndHorizontal();
            if (seed != terrain.Seed)
            {
                terrain.Seed = seed;
                config.NotifyChanged();
            }

            // Regenerating from the config also throws away every brush edit.
            if (GUILayout.Button("Reset terrain (undo all edits)", button))
            {
                config.NotifyChanged();
            }
            if (terrainFill != null && terrainFill.IsOverridden)
            {
                GUILayout.Label("The test fill is on and overrides the generator.", hint);
            }
        }

        private void DrawBrush()
        {
            if (brush == null)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("Brush", header);
            var tool = (ChunkClickTool)GUILayout.Toolbar((int)brush.Tool, ToolNames, button);
            if (tool != brush.Tool)
            {
                brush.Tool = tool;
            }

            GUILayout.Label($"Radius {brush.Radius:0.##}", label);
            brush.Radius = GUILayout.HorizontalSlider(brush.Radius, TerrainBrushTool.MinRadius, TerrainBrushTool.MaxRadius);
            GUILayout.Label($"Strength {brush.Strength:0.##}", label);
            brush.Strength = GUILayout.HorizontalSlider(brush.Strength, 0.01f, 1f);
            brush.Falloff = (BrushFalloff)GUILayout.Toolbar((int)brush.Falloff, FalloffNames, button);
        }

        private void DrawDisplay()
        {
            if (debugView == null)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("Display", header);
            // Only write real changes: each write makes the view rebuild its meshes.
            bool showSamples = GUILayout.Toggle(debugView.ShowSamples, " Show density samples", toggle);
            if (showSamples != debugView.ShowSamples)
            {
                debugView.ShowSamples = showSamples;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Wireframe", label, GUILayout.Width(80f));
            var wireframe = (ChunkDebugView.Wireframe)GUILayout.Toolbar((int)debugView.WireframeMode, WireframeNames, button);
            if (wireframe != debugView.WireframeMode)
            {
                debugView.WireframeMode = wireframe;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawHelp()
        {
            GUILayout.Space(8f);
            string quit = Application.isEditor ? "" : " · Esc quit";
            GUILayout.Label(
                "Right mouse look · WASD move · Q/E down/up · Shift fast\n" +
                $"1/2/3 tool · [ ] radius · Tab hide panel{quit}",
                hint);
        }

        private void CreateStyles()
        {
            if (padding != null)
            {
                return;
            }

            padding = new GUIStyle { padding = new RectOffset(12, 12, 10, 10) };
            header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            hint = new GUIStyle(label) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 13, normal = { textColor = Color.white }, onNormal = { textColor = Color.white } };
        }
    }
}
