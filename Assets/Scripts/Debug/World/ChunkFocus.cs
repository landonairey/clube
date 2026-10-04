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
    /// camera to frame it. Clicking empty space clears the focus. While the brush digs
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

        // Seconds between volume measurements while the focused chunk keeps changing.
        private const float VolumeInterval = 0.25f;

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

        private readonly List<Vector3> lineVertices = new List<Vector3>();
        private readonly List<int> lineIndices = new List<int>();

        private WorldView worldView;
        private TerrainBrushTool brush;
        private WorldDebugView debugView;
        private LabMeshObject outline;
        private Material ownedMaterial;
        private GUIStyle labelStyle;

        // The focused chunk's solid volume, measured again after its mesh rebuilds.
        private ChunkRenderer watchedRenderer;
        private bool volumeDirty;
        private float nextVolumeTime;
        private float exactVolume;
        private float approximateVolume;
        private double volumeMilliseconds;

        /// <summary>Raised with the newly focused chunk, or null when the focus clears.</summary>
        public event Action<Vector3Int?> FocusChanged;

        /// <summary>The focused chunk's coordinate, or null.</summary>
        public Vector3Int? Focused { get; private set; }

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
            WatchRenderer(coord);
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
            string text = $"Chunk ({coord.x}, {coord.y}, {coord.z}), origin {worldView.ChunkWorldOrigin(coord)}\n" +
                          $"{voxels.x} × {voxels.y} × {voxels.z} voxels, {sampleTotal:N0} samples ({sampleTotal * sizeof(float) / 1024f:0.0} KB)\n" +
                          (worldView.World.IsEdited(coord) ? "Edited (kept in memory when unloaded)" : "Not edited (regenerated on reload)");
            if (worldView.TryGetRenderer(coord, out ChunkRenderer chunkRenderer))
            {
                ChunkMeshStats stats = chunkRenderer.LastBuildStats;
                text += $"\n{stats.VertexCount:N0} vertices, {stats.TriangleCount:N0} triangles\n" +
                        $"Build {stats.TotalMilliseconds:0.00} ms (meshing {stats.MeshingMilliseconds:0.00}, upload {stats.UploadMilliseconds:0.00})";
            }

            // Solid volume inside the chunk (V11, V12 summed over its voxels).
            float capacity = Mathf.Max(voxels.x * voxels.y * voxels.z * Mathf.Pow(worldView.Config.VoxelSize, 3f), 1e-6f);
            text += $"\nVolume exact {exactVolume:0.0} u³ ({exactVolume / capacity * 100f:0.0}% of chunk)\n" +
                    $"Volume approx {approximateVolume:0.0} u³ (corner mean) · {volumeMilliseconds:0.0} ms";
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
            // The exact volume polygonises every surface voxel, so it's measured at most a
            // few times a second while the brush keeps rebuilding the chunk.
            if (volumeDirty && Time.unscaledTime >= nextVolumeTime)
            {
                MeasureVolume();
            }

            // While the chunk grid is drawn, it highlights the focused chunk's edges itself.
            if (outline != null)
            {
                outline.Visible = Focused.HasValue && (debugView == null || !debugView.IsDrawingBorders);
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
                Focus(Pick(mouse.position.ReadValue()));
            }
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                FrameFocused();
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
                "<color=#aaaaaa>F focus · click empty space to clear</color>");
            Vector2 size = labelStyle.CalcSize(content);
            var rect = new Rect((Screen.width - size.x) * 0.5f, 10f, size.x, size.y);
            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, content, labelStyle);
        }

        private Vector3Int? Pick(Vector2 screenPoint)
        {
            Camera viewCamera = ViewCamera;
            if (viewCamera == null)
            {
                return null;
            }
            return worldView.Raycast(viewCamera.ScreenPointToRay(screenPoint), out WorldHit hit) ? hit.Chunk : (Vector3Int?)null;
        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            if (coord == Focused)
            {
                Focus(null);
            }
        }

        // Follows the focused chunk's renderer, so the volume is measured again after edits.
        private void WatchRenderer(Vector3Int? coord)
        {
            if (watchedRenderer != null)
            {
                watchedRenderer.MeshRebuilt -= OnFocusedRebuilt;
                watchedRenderer = null;
            }
            if (coord.HasValue && worldView.TryGetRenderer(coord.Value, out ChunkRenderer chunkRenderer))
            {
                watchedRenderer = chunkRenderer;
                watchedRenderer.MeshRebuilt += OnFocusedRebuilt;
            }
            volumeDirty = coord.HasValue;
            nextVolumeTime = 0f;
        }

        private void OnFocusedRebuilt(Vector3Int coord, Mesh mesh)
        {
            volumeDirty = true;
        }

        private void MeasureVolume()
        {
            volumeDirty = false;
            nextVolumeTime = Time.unscaledTime + VolumeInterval;
            if (Focused == null || !worldView.World.TryGetChunk(Focused.Value, out Chunk chunk))
            {
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ChunkMeshSettings settings = worldView.Config.MeshSettings;
            exactVolume = ChunkVolume.Exact(chunk, settings.IsoLevel, EdgeVertexPlacers.For(settings.EdgePlacement), settings.VoxelSize);
            approximateVolume = ChunkVolume.Approximate(chunk, settings.VoxelSize);
            volumeMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
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
