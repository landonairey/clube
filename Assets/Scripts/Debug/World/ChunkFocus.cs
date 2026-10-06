using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// Focus mode for WorldLab (M4): click the terrain to focus the chunk you hit. The
    /// chunk is outlined, its stats show in the lab panel and a label, and F glides the
    /// camera to frame it. Clicking empty space clears the focus. The chunk tools follow
    /// the focus (<see cref="LabChunkTarget"/>, M18), and the click also selects the voxel
    /// hit in their <see cref="VoxelSelector"/>, so F then frames that voxel. While the brush digs
    /// or adds, clicks edit instead (<see cref="TerrainBrushTool"/>).
    /// </summary>
    /// <remarks>
    /// The outline is a real line mesh rather than a gizmo, so it shows in the Game
    /// view. If the focused chunk unloads, the focus clears.
    /// </remarks>
    [RequireComponent(typeof(WorldView))]
    public class ChunkFocus : MonoBehaviour
    {
        private const string LineShader = "Universal Render Pipeline/Unlit";

        [Tooltip("Outline the focused chunk (its border).")]
        [SerializeField]
        private bool highlightChunk = true;

        [SerializeField]
        private Color outlineColor = new Color(1f, 0.85f, 0.2f);

        [Tooltip("Unlit material for the outline. Falls back to URP Unlit if empty.")]
        [SerializeField]
        private Material lineMaterial;

        [Tooltip("How far from the chunk's centre F places the camera, in chunk widths.")]
        [SerializeField, Min(0.5f)]
        private float focusDistance = 1.5f;

        [Tooltip("Camera clicks are cast from and F moves. Defaults to the main camera.")]
        [SerializeField]
        private Camera targetCamera;

        [Tooltip("The chunk tools' voxel selector: a click selects the voxel hit in the focused chunk. Optional.")]
        [SerializeField]
        private VoxelSelector voxelSelector;

        [Tooltip("The chunk tools' volume measurement, shown in the panel. Optional.")]
        [SerializeField]
        private ChunkVolumeStats volumeStats;

        private readonly List<Vector3> lineVertices = new List<Vector3>();
        private readonly List<int> lineIndices = new List<int>();

        private WorldView worldView;
        private TerrainBrushTool brush;
        private WorldDebugView debugView;
        private LabMeshObject outline;
        private Material ownedMaterial;
        private GUIStyle labelStyle;

        /// <summary>Raised with the newly focused chunk, or null when the focus clears.</summary>
        public event Action<Vector3Int?> FocusChanged;

        /// <summary>Raised when <see cref="HighlightChunk"/> changes.</summary>
        public event Action HighlightChanged;

        /// <summary>Whether the focused chunk's border is highlighted.</summary>
        public bool HighlightChunk
        {
            get => highlightChunk;
            set
            {
                if (value == highlightChunk)
                {
                    return;
                }
                highlightChunk = value;
                HighlightChanged?.Invoke();
            }
        }

        /// <summary>The focused chunk's coordinate, or null.</summary>
        public Vector3Int? Focused { get; private set; }

        /// <summary>The focused chunk's renderer, or null with nothing focused.</summary>
        public ChunkRenderer FocusedRenderer =>
            Focused.HasValue && worldView.TryGetRenderer(Focused.Value, out ChunkRenderer chunkRenderer) && chunkRenderer.Chunk != null
                ? chunkRenderer
                : null;

        private Camera ViewCamera => targetCamera != null ? targetCamera : Camera.main;

        /// <summary>Focuses a loaded chunk, or clears the focus with null.</summary>
        public void Focus(Vector3Int? coord)
        {
            if (coord.HasValue && !worldView.World.IsLoaded(coord.Value))
            {
                coord = null;
            }
            if (coord == Focused)
            {
                return;
            }

            Focused = coord;
            UpdateOutline();
            FocusChanged?.Invoke(coord);
        }

        /// <summary>Glides the camera to frame the focused chunk.</summary>
        public void FrameFocused()
        {
            Camera viewCamera = ViewCamera;
            var flyCamera = viewCamera != null ? viewCamera.GetComponent<FreeFlyCamera>() : null;
            if (Focused == null || flyCamera == null)
            {
                return;
            }

            Vector3 size = worldView.World.Grid.ChunkWorldSize;
            Vector3 centre = worldView.ChunkWorldOrigin(Focused.Value) + size * 0.5f;
            flyCamera.Focus(centre, focusDistance * size.magnitude);
        }

        /// <summary>What the panel shows for the focused chunk, or null with nothing focused.</summary>
        public string DescribeFocused()
        {
            if (Focused == null || !worldView.World.TryGetChunk(Focused.Value, out Chunk chunk))
            {
                return null;
            }

            Vector3Int coord = Focused.Value;
            Vector3Int voxels = chunk.VoxelCount;
            Vector3Int samples = chunk.SampleCount;
            int sampleTotal = samples.x * samples.y * samples.z;
            long bytes = chunk.DensityMemoryBytes + chunk.Materials.MemoryBytes;
            string storage = chunk.IsUniform
                ? (chunk.UniformDensity >= worldView.Config.IsoLevel ? "all solid, no density storage" : "all air, no density storage")
                : $"{bytes / 1024f:0.0} KB";
            string text = $"Chunk ({coord.x}, {coord.y}, {coord.z}), origin {worldView.ChunkWorldOrigin(coord)}\n" +
                          $"{voxels.x} × {voxels.y} × {voxels.z} voxels, {sampleTotal:N0} samples ({storage})\n" +
                          (worldView.World.IsEdited(coord) ? "Edited (kept in memory when unloaded)" : "Not edited (regenerated on reload)");
            if (worldView.TryGetRenderer(coord, out ChunkRenderer chunkRenderer))
            {
                ChunkMeshStats stats = chunkRenderer.LastBuildStats;
                text += $"\n{stats.VertexCount:N0} vertices, {stats.TriangleCount:N0} triangles\n" +
                        $"Meshed in a job; upload {stats.UploadMilliseconds:0.00} ms";
            }

            // Solid volume inside the chunk (V11, V12 summed over its voxels).
            if (volumeStats != null && volumeStats.HasMeasurement)
            {
                float percent = volumeStats.Exact / Mathf.Max(volumeStats.ChunkCapacity, 1e-6f) * 100f;
                text += $"\nVolume exact {volumeStats.Exact:0.0} u³ ({percent:0.0}% of chunk)\n" +
                        $"Volume approx {volumeStats.Approximate:0.0} u³ (corner mean) · {volumeStats.Milliseconds:0.0} ms";
            }
            return text;
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
            brush = GetComponent<TerrainBrushTool>();
            debugView = GetComponent<WorldDebugView>();
        }

        private void LateUpdate()
        {
            // While the chunk grid is drawn, it highlights the focused chunk's edges itself.
            if (outline != null)
            {
                outline.Visible = Focused.HasValue && highlightChunk && (debugView == null || !debugView.IsDrawingBorders);
            }
        }

        private void OnEnable()
        {
            worldView.ChunkUnloaded += OnChunkUnloaded;
        }

        private void OnDisable()
        {
            worldView.ChunkUnloaded -= OnChunkUnloaded;
            Focus(null);
        }

        private void OnDestroy()
        {
            outline?.Dispose();
            LabMeshObject.DestroyNow(ownedMaterial);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (worldView.World == null)
            {
                return;
            }

            // Selecting shares the left button with the brush, so it only picks in Select.
            bool selecting = brush == null || brush.Tool == ChunkClickTool.Select;
            if (selecting && mouse != null && mouse.leftButton.wasPressedThisFrame && !LabGuiBlocker.IsOverGui(mouse.position.ReadValue()))
            {
                Camera viewCamera = ViewCamera;
                if (viewCamera != null)
                {
                    ClickAt(viewCamera.ScreenPointToRay(mouse.position.ReadValue()));
                }
            }
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                if (voxelSelector != null && voxelSelector.SelectedVoxel.HasValue)
                {
                    voxelSelector.FocusSelected();
                }
                else
                {
                    FrameFocused();
                }
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || Focused == null)
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 6, 6),
                normal = { textColor = Color.white },
            };

            Vector3Int coord = Focused.Value;
            int triangles = worldView.TryGetRenderer(coord, out ChunkRenderer chunkRenderer) ? chunkRenderer.LastBuildStats.TriangleCount : 0;
            var content = new GUIContent(
                $"<b>Chunk ({coord.x}, {coord.y}, {coord.z})</b>  {triangles:N0} triangles   " +
                "<color=#aaaaaa>click inside to select a voxel · F frame · click empty space to clear</color>");
            Vector2 size = labelStyle.CalcSize(content);
            var rect = new Rect((Screen.width - size.x) * 0.5f, 10f, size.x, size.y);
            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, content, labelStyle);
        }

        /// <summary>
        /// A Select-tool click along a world-space ray, in two steps: a click on another chunk
        /// focuses it; a click inside the focused chunk selects the voxel hit. Empty space
        /// clears the voxel first, then the chunk.
        /// </summary>
        public void ClickAt(Ray worldRay)
        {
            WorldHit? hit = worldView.Raycast(worldRay, out WorldHit worldHit) ? worldHit : (WorldHit?)null;
            bool hasVoxel = voxelSelector != null && voxelSelector.SelectedVoxel.HasValue;
            if (hit == null)
            {
                if (hasVoxel)
                {
                    voxelSelector.Select(null);
                }
                else
                {
                    Focus(null);
                }
                return;
            }

            if (hit.Value.Chunk != Focused || voxelSelector == null)
            {
                Focus(hit.Value.Chunk);
                return;
            }
            voxelSelector.Select(hit.Value.Voxel);
        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            if (coord == Focused)
            {
                Focus(null);
            }
        }

        // A box around the chunk's bounds, in the world view's space.
        private void UpdateOutline()
        {
            if (Focused == null)
            {
                if (outline != null)
                {
                    outline.Visible = false;
                }
                return;
            }

            if (outline == null)
            {
                Material material = lineMaterial;
                if (material == null)
                {
                    ownedMaterial = new Material(Shader.Find(LineShader)) { name = "Chunk Focus Outline" };
                    material = ownedMaterial;
                }
                outline = new LabMeshObject(transform, "Chunk Focus Outline", material);
            }

            Vector3 origin = worldView.World.Grid.ChunkOrigin(Focused.Value);
            Vector3 size = worldView.World.Grid.ChunkWorldSize;
            lineVertices.Clear();
            lineIndices.Clear();
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                lineVertices.Add(origin + Vector3.Scale(MarchingCubes.CornerOffset(corner), size));
            }
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                lineIndices.Add(MarchingCubesTables.EdgeCorners[edge, 0]);
                lineIndices.Add(MarchingCubesTables.EdgeCorners[edge, 1]);
            }

            outline.Mesh.Clear();
            outline.Mesh.SetVertices(lineVertices);
            outline.Mesh.SetIndices(lineIndices, MeshTopology.Lines, 0);
            outline.Mesh.RecalculateBounds();
            outline.SetColor(outlineColor);
            outline.Visible = true;
        }
    }
}
