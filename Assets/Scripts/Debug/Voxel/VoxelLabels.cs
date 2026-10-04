using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The teaching labels for the lab's voxel (<see cref="VoxelCornerEditor"/>'s: VoxelLab's
    /// one voxel, or the selected one), in the Game view while playing: corner labels with
    /// their densities and the case table, crossed edges, edge numbers (V9, V21, K7). It also
    /// outlines the triangles the voxel contributes, so its piece of a chunk's surface stands
    /// out. The Scene view shows the same labels through an editor overlay, which reads the
    /// toggles from here.
    /// </summary>
    /// <remarks>
    /// Everything hides while step-through mode is on (a <see cref="StepThroughLab"/>
    /// child is enabled): it shows the finished answer, which the animation reveals one
    /// step at a time. The triangle outline comes from the voxel re-polygonised on its
    /// own, nudged a hair along each face normal so it sits in front of the chunk's mesh.
    /// </remarks>
    [RequireComponent(typeof(VoxelCornerEditor))]
    public class VoxelLabels : MonoBehaviour
    {
        private const string HighlightShader = "Universal Render Pipeline/Unlit";

        // How far the triangle outline sits in front of the surface, as a fraction of the voxel size.
        private const float SurfaceOffset = 0.004f;

        [Tooltip("c0-c7 on the corners, and a table of which bit each one sets in the case index (V21).")]
        [SerializeField]
        private bool showCornerLabels = true;

        [Tooltip("Add each corner's density to its label (\"c3 0.52\").")]
        [SerializeField]
        private bool showCornerValues;

        [Tooltip("Highlight the edges the surface crosses (V9).")]
        [SerializeField]
        private bool showCrossedEdges = true;

        [Tooltip("Number all 12 edges (e0-e11); crossed edges are labelled in the highlight colour.")]
        [SerializeField]
        private bool showEdgeLabels = true;

        [Tooltip("Outline the triangles the voxel contributes to the chunk's mesh (Play mode).")]
        [SerializeField]
        private bool showTriangleEdges;

        [SerializeField]
        private Color triangleEdgeColor = new Color(1f, 0.6f, 0.15f);

        [Tooltip("Unlit material for the triangle outline. Falls back to URP Unlit if empty.")]
        [SerializeField]
        private Material triangleEdgeMaterial;

        [Tooltip("Camera the Game-view labels are projected with. Defaults to the main camera.")]
        [SerializeField]
        private Camera targetCamera;

        private readonly float[] corners = new float[MarchingCubes.CornerCount];
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<int> lineIndices = new List<int>();

        private VoxelCornerEditor cornerEditor;
        private LabChunkTarget target;
        private VoxelSelector selector;
        private LabMeshObject triangleEdges;
        private Material ownedMaterial;
        private bool hasTriangleEdges;
        private bool rebuildRequested;

        /// <summary>The corner-labels toggle itself; <see cref="ShowCornerLabels"/> also checks step-through.</summary>
        public bool CornerLabelsOn
        {
            get => showCornerLabels;
            set => showCornerLabels = value;
        }

        public bool CornerValuesOn
        {
            get => showCornerValues;
            set => showCornerValues = value;
        }

        public bool CrossedEdgesOn
        {
            get => showCrossedEdges;
            set => showCrossedEdges = value;
        }

        public bool EdgeLabelsOn
        {
            get => showEdgeLabels;
            set => showEdgeLabels = value;
        }

        public bool TriangleEdgesOn
        {
            get => showTriangleEdges;
            set
            {
                showTriangleEdges = value;
                rebuildRequested = true;
            }
        }

        public bool ShowCornerLabels => showCornerLabels && !StepThroughMode.IsOn(this);

        public bool ShowCornerValues => showCornerValues;

        public bool ShowCrossedEdges => showCrossedEdges && !StepThroughMode.IsOn(this);

        public bool ShowEdgeLabels => showEdgeLabels && !StepThroughMode.IsOn(this);

        public VoxelCornerEditor Corners => cornerEditor != null ? cornerEditor : GetComponent<VoxelCornerEditor>();

        private void Awake()
        {
            cornerEditor = GetComponent<VoxelCornerEditor>();
            target = GetComponent<LabChunkTarget>();
            selector = GetComponent<VoxelSelector>();
        }

        private void OnEnable()
        {
            target.MeshRebuilt += OnMeshRebuilt;
            target.Changed += RequestRebuild;
            if (selector != null)
            {
                selector.SelectionChanged += OnSelectionChanged;
            }
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            target.MeshRebuilt -= OnMeshRebuilt;
            target.Changed -= RequestRebuild;
            if (selector != null)
            {
                selector.SelectionChanged -= OnSelectionChanged;
            }
            if (triangleEdges != null)
            {
                triangleEdges.Visible = false;
            }
        }

        private void OnDestroy()
        {
            triangleEdges?.Dispose();
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

        private void RequestRebuild()
        {
            rebuildRequested = true;
        }

        private void LateUpdate()
        {
            if (rebuildRequested)
            {
                rebuildRequested = false;
                RebuildTriangleEdges();
            }

            // Checked per frame because step-through can be switched on at any time.
            if (triangleEdges != null)
            {
                triangleEdges.Visible = hasTriangleEdges && !StepThroughMode.IsOn(this);
            }
        }

        private void OnGUI()
        {
            bool cornerLabels = ShowCornerLabels;
            bool crossedEdges = ShowCrossedEdges;
            bool edgeLabels = ShowEdgeLabels;
            if (Event.current.type != EventType.Repaint || (!cornerLabels && !crossedEdges && !edgeLabels))
            {
                return;
            }

            Camera viewCamera = targetCamera != null ? targetCamera : Camera.main;
            if (viewCamera == null)
            {
                return;
            }

            VoxelLabelPainter.Draw(cornerEditor, cornerLabels, showCornerValues, crossedEdges, edgeLabels, (Vector3 world, out Vector2 gui) =>
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(world);

                // Screen y grows upwards, GUI y grows downwards.
                gui = new Vector2(screen.x, Screen.height - screen.y);
                return screen.z > 0f;
            });
        }

        private void RebuildTriangleEdges()
        {
            Chunk chunk = target.Chunk;
            if (!showTriangleEdges || chunk == null || !cornerEditor.HasVoxel)
            {
                hasTriangleEdges = false;
                if (triangleEdges != null)
                {
                    triangleEdges.Visible = false;
                }
                return;
            }

            IReadOnlyList<float> values = cornerEditor.CornerValues;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                corners[corner] = values[corner];
            }

            ChunkMeshSettings settings = target.MeshSettings;
            vertices.Clear();
            triangles.Clear();
            MarchingCubes.Polygonise(
                corners, settings.IsoLevel, cornerEditor.VoxelOrigin, settings.VoxelSize,
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

            // Each triangle's three edges as line segments.
            lineIndices.Clear();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                lineIndices.Add(triangles[i]);
                lineIndices.Add(triangles[i + 1]);
                lineIndices.Add(triangles[i + 1]);
                lineIndices.Add(triangles[i + 2]);
                lineIndices.Add(triangles[i + 2]);
                lineIndices.Add(triangles[i]);
            }

            triangleEdges ??= new LabMeshObject(transform, "Voxel Triangle Edges", TriangleEdgeMaterial());
            triangleEdges.Mesh.Clear();
            triangleEdges.Mesh.SetVertices(vertices);
            triangleEdges.Mesh.SetIndices(lineIndices, MeshTopology.Lines, 0);
            triangleEdges.SetColor(triangleEdgeColor);
            hasTriangleEdges = true;
            triangleEdges.Visible = !StepThroughMode.IsOn(this);
        }

        private Material TriangleEdgeMaterial()
        {
            if (triangleEdgeMaterial != null)
            {
                return triangleEdgeMaterial;
            }
            ownedMaterial ??= new Material(Shader.Find(HighlightShader)) { name = "Voxel Triangle Edges" };
            return ownedMaterial;
        }
    }
}
