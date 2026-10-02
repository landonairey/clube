using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Game-view extras for <see cref="VoxelSelector"/>'s selection (K7): labels on
    /// the selected voxel's corners ("c3 0.52", green when solid), and a tint over
    /// the triangles that voxel contributes, so its piece of the surface stands
    /// out from the rest of the chunk.
    /// </summary>
    /// <remarks>
    /// The tint is the voxel re-polygonised on its own and nudged a hair along each
    /// face normal, so it sits just in front of the chunk's own triangles.
    /// </remarks>
    [RequireComponent(typeof(ChunkView), typeof(VoxelSelector))]
    public class SelectedVoxelOverlay : MonoBehaviour
    {
        private const string HighlightShader = "Universal Render Pipeline/Unlit";

        // How far the tint sits in front of the surface, as a fraction of the voxel size.
        private const float SurfaceOffset = 0.004f;

        private static readonly Color SolidColor = new Color(0.35f, 0.95f, 0.45f);
        private static readonly Color EmptyColor = new Color(0.8f, 0.8f, 0.8f);

        [Tooltip("Label the selected voxel's corners with their name and density.")]
        [SerializeField]
        private bool showCornerLabels = true;

        [Tooltip("Tint the triangles the selected voxel contributes to the chunk mesh.")]
        [SerializeField]
        private bool highlightTriangles = true;

        [SerializeField]
        private Color highlightColor = new Color(1f, 0.6f, 0.15f);

        [Tooltip("Unlit material for the tint. Falls back to URP Unlit if empty.")]
        [SerializeField]
        private Material highlightMaterial;

        [Tooltip("Camera the labels are projected with. Defaults to the main camera.")]
        [SerializeField]
        private Camera targetCamera;

        private readonly float[] corners = new float[MarchingCubes.CornerCount];
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        private ChunkView chunkView;
        private VoxelSelector selector;
        private LabMeshObject tint;
        private Material ownedMaterial;
        private GUIStyle labelStyle;
        private bool rebuildRequested;

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
            selector = GetComponent<VoxelSelector>();
        }

        private void OnEnable()
        {
            chunkView.MeshRebuilt += OnMeshRebuilt;
            selector.SelectionChanged += OnSelectionChanged;
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            chunkView.MeshRebuilt -= OnMeshRebuilt;
            selector.SelectionChanged -= OnSelectionChanged;
            if (tint != null)
            {
                tint.Visible = false;
            }
        }

        private void OnDestroy()
        {
            tint?.Dispose();
            LabMeshObject.DestroyNow(ownedMaterial);
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            rebuildRequested = true;
        }

        private void OnMeshRebuilt(Mesh mesh)
        {
            rebuildRequested = true;
        }

        private void OnSelectionChanged(Vector3Int? voxel)
        {
            rebuildRequested = true;
        }

        private void LateUpdate()
        {
            if (rebuildRequested)
            {
                rebuildRequested = false;
                RebuildTint();
            }
        }

        private void OnGUI()
        {
            Vector3Int? selected = selector.SelectedVoxel;
            if (Event.current.type != EventType.Repaint || !showCornerLabels || selected == null || chunkView.Chunk == null)
            {
                return;
            }

            Camera viewCamera = targetCamera != null ? targetCamera : Camera.main;
            if (viewCamera == null)
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter,
            };

            float size = chunkView.Config.VoxelSize;
            float iso = chunkView.Config.IsoLevel;
            Vector3 voxelCentre = ((Vector3)selected.Value + Vector3.one * 0.5f) * size;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Vector3Int sample = selected.Value + MarchingCubes.CornerOffset(corner);
                float density = chunkView.Chunk.GetDensity(sample);
                GuiDrawing.LabelBeside(
                    viewCamera, transform, (Vector3)sample * size, voxelCentre, 0.08f * size,
                    $"c{corner} {density:0.00}", density >= iso ? SolidColor : EmptyColor, labelStyle);
            }
        }

        private void RebuildTint()
        {
            Vector3Int? selected = selector.SelectedVoxel;
            Chunk chunk = chunkView.Chunk;
            if (!highlightTriangles || selected == null || chunk == null)
            {
                if (tint != null)
                {
                    tint.Visible = false;
                }
                return;
            }

            ChunkMeshSettings settings = chunkView.Config.MeshSettings;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                corners[corner] = chunk.GetDensity(selected.Value + MarchingCubes.CornerOffset(corner));
            }

            vertices.Clear();
            triangles.Clear();
            MarchingCubes.Polygonise(
                corners, settings.IsoLevel, (Vector3)selected.Value * settings.VoxelSize, settings.VoxelSize,
                EdgeVertexPlacers.For(settings.EdgePlacement), new FlatVertexWriter(vertices), triangles);

            // Flat vertices: each triangle owns its three, so each can move along its own normal.
            float offset = SurfaceOffset * settings.VoxelSize;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized * offset;
                vertices[triangles[i]] = a + normal;
                vertices[triangles[i + 1]] = b + normal;
                vertices[triangles[i + 2]] = c + normal;
            }

            tint ??= new LabMeshObject(transform, "Selected Voxel Tint", Material());
            tint.Mesh.Clear();
            tint.Mesh.SetVertices(vertices);
            tint.Mesh.SetTriangles(triangles, 0);
            tint.SetColor(highlightColor);
            tint.Visible = true;
        }

        private Material Material()
        {
            if (highlightMaterial != null)
            {
                return highlightMaterial;
            }
            ownedMaterial ??= new Material(Shader.Find(HighlightShader)) { name = "Selected Voxel Tint" };
            return ownedMaterial;
        }
    }
}
