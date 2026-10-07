using System.Collections.Generic;
using Clube.Core;
using Clube.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// The in-game control panel for every lab (K31, M20), drawn with IMGUI so it also works
    /// in the demo exe. It finds the lab's parts in the scene and shows a section for each one
    /// there, from the world down to the voxel: world, terrain, brush, meshing, chunk (its
    /// shape and test fill, or WorldLab's focused chunk), voxel (corners, case, presets,
    /// volume), step-through, camera and display. Each section is drawn by a shared
    /// <c>*Controls</c> class, so a knob looks and works the same in every lab. Tab hides it.
    /// </summary>
    /// <remarks>
    /// Edits go through the same paths as the Inspector: config edits call
    /// <see cref="WorldConfig.NotifyChanged"/>, corner edits the chunk's edit path (A7).
    /// The frame (scrolling, click blocking, styles) is <see cref="LabPanelFrame"/>.
    /// </remarks>
    public class LabPanel : MonoBehaviour
    {
        [Tooltip("Show the panel when Play mode starts. Tab toggles it.")]
        [SerializeField]
        private bool startOpen = true;

        [SerializeField, Min(200f)]
        private float width = 340f;

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
        private bool chunkOpen;

        [SerializeField]
        private bool cornersOpen;

        [SerializeField]
        private bool caseOpen;

        [SerializeField]
        private bool presetsOpen;

        [SerializeField]
        private bool volumeOpen;

        [SerializeField]
        private bool stepThroughOpen;

        [SerializeField]
        private bool cameraOpen;

        [SerializeField]
        private bool playerOpen;

        [SerializeField]
        private bool displayOpen;

        [SerializeField]
        private bool buildGridOpen;

        [SerializeField]
        private bool storageOpen;

        [SerializeField]
        private bool oresOpen;

        private WorldView worldView;
        private ChunkView chunkView;
        private ChunkTerrainFill terrainFill;
        private ChunkTestFill testFill;
        private TerrainBrushTool brush;
        private ChunkFocus chunkFocus;
        private ChunkVolumeStats chunkVolume;
        private VoxelSelector selector;
        private VoxelCornerEditor corners;
        private VoxelVolumeLab volumeLab;
        private StepThroughLab stepThrough;
        private FreeFlyCamera flyCamera;
        private PlayerCameraToggle playerToggle;
        private PlayerController player;
        private VoxelLabels labels;
        private NormalLines normals;
        private FlipFaces flipFaces;
        private ChunkDebugView debugView;
        private WorldDebugView worldDebug;
        private AxesHud axesHud;
        private BuildGridOverlay buildGrid;
        private LabChunkTarget chunkTarget;
        private StorageView storageView;
        private OreDebugView oreView;

        private bool partsFound;
        private LabPanelFrame frame;
        private string help;
        private bool open;

        // The config the lab's sections edit: the world's, or the lone chunk's.
        private WorldConfig Config => worldView != null ? worldView.Config : chunkView != null ? chunkView.Config : null;

        private void Awake()
        {
            FindParts();
            frame = new LabPanelFrame(this, width, background);
            help = DescribeKeys();
            open = startOpen;
        }

        /// <summary>
        /// Finds the lab's parts in the scene, once unless <paramref name="again"/>. The Inspector
        /// (LabPanelEditor) calls it too, outside Play mode, to draw the same sections.
        /// </summary>
        public void FindParts(bool again = false)
        {
            if (partsFound && !again)
            {
                return;
            }
            partsFound = true;

            worldView = FindFirstObjectByType<WorldView>();
            chunkView = FindFirstObjectByType<ChunkView>();
            terrainFill = FindFirstObjectByType<ChunkTerrainFill>();
            testFill = FindFirstObjectByType<ChunkTestFill>();
            brush = FindFirstObjectByType<TerrainBrushTool>();
            chunkFocus = FindFirstObjectByType<ChunkFocus>();
            chunkVolume = FindFirstObjectByType<ChunkVolumeStats>();
            selector = FindFirstObjectByType<VoxelSelector>();
            corners = FindFirstObjectByType<VoxelCornerEditor>();
            volumeLab = FindFirstObjectByType<VoxelVolumeLab>();
            stepThrough = FindFirstObjectByType<StepThroughLab>();
            // Include inactive: while walking as the player (M7) the fly camera is switched off.
            flyCamera = FindFirstObjectByType<FreeFlyCamera>(FindObjectsInactive.Include);
            playerToggle = FindFirstObjectByType<PlayerCameraToggle>();
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            labels = FindFirstObjectByType<VoxelLabels>();
            normals = FindFirstObjectByType<NormalLines>();
            flipFaces = FindFirstObjectByType<FlipFaces>();
            debugView = FindFirstObjectByType<ChunkDebugView>();
            worldDebug = FindFirstObjectByType<WorldDebugView>();
            axesHud = FindFirstObjectByType<AxesHud>();
            buildGrid = FindFirstObjectByType<BuildGridOverlay>();
            chunkTarget = FindFirstObjectByType<LabChunkTarget>();
            storageView = FindFirstObjectByType<StorageView>();
            oreView = FindFirstObjectByType<OreDebugView>();
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
            WorldConfig config = Config;
            if (!open || config == null || (worldView != null && worldView.World == null))
            {
                return;
            }

            frame.Width = width;
            frame.Begin();
            DrawSections(frame);
            GUILayout.Space(8f);
            GUILayout.Label(help, frame.Hint);
            frame.End();
        }

        /// <summary>Everything the sections can change, for the Inspector to record for Undo and saving.</summary>
        public Object[] EditableParts()
        {
            FindParts();
            var parts = new List<Object>();
            foreach (Object part in new Object[]
                     {
                         Config, worldView, testFill, brush, chunkFocus, chunkVolume, selector, corners, volumeLab,
                         stepThrough, flyCamera, playerToggle, player, labels, normals, flipFaces, debugView, worldDebug, axesHud, buildGrid, storageView, oreView,
                     })
            {
                if (part != null)
                {
                    parts.Add(part);
                }
            }
            parts.Add(this);
            return parts.ToArray();
        }

        /// <summary>
        /// Draws every section this lab has into <paramref name="frame"/>: the game panel's frame,
        /// or an Inspector frame, so the Inspector always has every panel knob (M22).
        /// </summary>
        public void DrawSections(LabPanelFrame frame)
        {
            FindParts();
            WorldConfig config = Config;
            if (config == null)
            {
                return;
            }

            if (worldView != null && frame.Section("World", ref worldOpen))
            {
                if (worldView.World != null)
                {
                    WorldControls.Draw(frame, worldView);
                }
                else
                {
                    ChunkShapeControls.Draw(frame, config, withLayers: true);
                }
            }
            if ((worldView != null || terrainFill != null) && frame.Section("Terrain", ref terrainOpen))
            {
                DrawTerrain(frame, config);
            }
            if (brush != null && frame.Section("Brush", ref brushOpen))
            {
                BrushControls.Draw(frame, brush, config.Materials);
            }
            if (frame.Section("Meshing", ref meshingOpen))
            {
                MeshingControls.Draw(frame, config);
            }
            if (frame.Section("Storage", ref storageOpen))
            {
                StorageControls.Draw(frame, config, chunkTarget, storageView);
            }
            if (worldView != null && frame.Section("Ores", ref oresOpen))
            {
                OreControls.Draw(frame, config, oreView, chunkFocus);
            }
            if (chunkFocus != null && frame.Section("Focused chunk", ref chunkOpen))
            {
                if (worldView.World != null)
                {
                    ChunkFocusControls.Draw(frame, chunkFocus);
                }
                else
                {
                    GUILayout.Label("Enter Play mode, then click the terrain to focus a chunk.", frame.Hint);
                }
                DrawChunkVolumeToggle(frame);
            }
            // A lone generated chunk (ChunkLab); VoxelLab's one voxel keeps its size.
            else if (worldView == null && terrainFill != null && frame.Section("Chunk", ref chunkOpen))
            {
                ChunkShapeControls.Draw(frame, config, withLayers: false);
                if (testFill != null)
                {
                    TestFillControls.Draw(frame, testFill);
                }
                DrawChunkVolumeToggle(frame);
            }
            if (corners != null)
            {
                if (frame.Section(corners.UsesStoredCorners ? "Corners" : "Selected voxel", ref cornersOpen))
                {
                    VoxelControls.DrawCorners(frame, corners);
                }
                if (frame.Section("Case", ref caseOpen))
                {
                    VoxelControls.DrawCase(frame, corners);
                }
                if (frame.Section("Presets", ref presetsOpen))
                {
                    VoxelControls.DrawPresets(frame, corners);
                }
            }
            if (volumeLab != null && frame.Section("Volume", ref volumeOpen))
            {
                VolumeControls.Draw(frame, volumeLab);
            }
            if (stepThrough != null && frame.Section("Step-through", ref stepThroughOpen))
            {
                StepThroughControls.Draw(frame, stepThrough);
            }
            if (flyCamera != null && frame.Section("Camera", ref cameraOpen))
            {
                CameraControls.Draw(frame, flyCamera, playerToggle);
            }
            if (player != null && frame.Section("Player", ref playerOpen))
            {
                PlayerControls.Draw(frame, player);
            }
            if (frame.Section("Display", ref displayOpen))
            {
                DisplayControls.Draw(frame, chunkFocus, selector, labels, normals, flipFaces, debugView, worldDebug, axesHud);
            }
            if (buildGrid != null && frame.Section("Build grid", ref buildGridOpen))
            {
                BuildGridControls.Draw(frame, buildGrid, config.VoxelSize);
            }
        }

        private void DrawTerrain(LabPanelFrame frame, WorldConfig config)
        {
            TerrainControls.Draw(frame, config);
            if (worldView != null && worldView.Problem != null)
            {
                GUILayout.Label($"Generate skipped: {worldView.Problem}", frame.Hint);
            }
            if (terrainFill != null && terrainFill.IsOverridden)
            {
                GUILayout.Label("The test fill is on and overrides the generator.", frame.Hint);
            }
        }

        // The chunk's solid volume (V11, V12 over every voxel) is measured after each rebuild unless this is off.
        private void DrawChunkVolumeToggle(LabPanelFrame frame)
        {
            if (chunkVolume == null)
            {
                return;
            }
            bool measure = frame.ToggleField("Measure the chunk's volume", chunkVolume.Calculate);
            if (measure != chunkVolume.Calculate)
            {
                chunkVolume.Calculate = measure;
            }
        }

        // The keys of the parts this lab has.
        private string DescribeKeys()
        {
            var keys = new List<string> { "Right mouse look · WASD move · Q/E down/up · Shift fast" };
            if (playerToggle != null)
            {
                keys.Add("P walk as the player: mouse look · WASD · Space jump · left mine with the held tool · right place · [ ] brush size · 1-9, scroll, Q hotbar · Tab inventory · Alt free the cursor for this panel");
            }
            if (brush != null)
            {
                keys.Add("1/2/3 select, dig, add · [ ] radius");
            }
            if (chunkFocus != null)
            {
                keys.Add("Click focuses a chunk, click inside it selects a voxel · F frames it");
            }
            else if (selector != null)
            {
                keys.Add("Click selects a voxel · F frames it");
            }
            if (corners != null)
            {
                keys.Add("← → step case");
            }
            if (volumeLab != null)
            {
                keys.Add("Scroll pull volume pieces apart");
            }
            keys.Add("Tab hide panel");
            return string.Join("\n", keys);
        }
    }
}
