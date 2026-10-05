using System;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Clube.Debug
{
    /// <summary>
    /// Click a voxel in the Game view to select it (K5): the selected voxel's
    /// wireframe is highlighted, and F glides the free-fly camera to frame it (K6).
    /// The click selects the voxel whose piece of surface the click ray hits first
    /// (<see cref="SurfaceRaycast"/>), so voxels without surface can't be picked.
    /// Clicking anywhere else clears the selection.
    /// </summary>
    /// <remarks>
    /// The highlight is a real line mesh rather than a gizmo, so it shows in the
    /// Game view even with gizmos off. Play mode only, like the chunk itself.
    /// </remarks>
    [RequireComponent(typeof(LabChunkTarget))]
    public class VoxelSelector : MonoBehaviour
    {
        private const string HighlightShader = "Universal Render Pipeline/Unlit";
        private const string ColorProperty = "_BaseColor";

        [Tooltip("Outline the selected voxel.")]
        [SerializeField]
        private bool showHighlight = true;

        [SerializeField]
        private Color highlightColor = new Color(1f, 0.85f, 0.2f);

        [Tooltip("Unlit material for the highlight. Falls back to URP Unlit if empty.")]
        [SerializeField]
        private Material highlightMaterial;

        [Tooltip("How far from the voxel F places the camera, in voxels.")]
        [SerializeField, Min(0.5f)]
        private float focusDistance = 4f;

        [Tooltip("Pick on left click and frame on F. Off where something else drives the selection (WorldLab's ChunkFocus picks the chunk and voxel together).")]
        [SerializeField]
        private bool handleInput = true;

        [Tooltip("Camera clicks are cast from and F moves. Defaults to the main camera.")]
        [SerializeField]
        private Camera targetCamera;

        private readonly float[] cornerValues = new float[MarchingCubes.CornerCount];

        private LabChunkTarget target;
        private GameObject highlight;
        private Mesh highlightMesh;
        private Material ownedMaterial;
        private GUIStyle labelStyle;

        /// <summary>Raised with the new selection, or null when it is cleared.</summary>
        public event Action<Vector3Int?> SelectionChanged;

        /// <summary>The selected voxel, or null when nothing is selected.</summary>
        public Vector3Int? SelectedVoxel { get; private set; }

        /// <summary>Whether the selected voxel is outlined.</summary>
        public bool ShowHighlight
        {
            get => showHighlight;
            set
            {
                showHighlight = value;
                UpdateHighlight();
            }
        }

        private Camera ViewCamera => targetCamera != null ? targetCamera : Camera.main;

        /// <summary>Selects a voxel, or clears the selection with null.</summary>
        public void Select(Vector3Int? voxel)
        {
            if (voxel == SelectedVoxel)
            {
                return;
            }

            SelectedVoxel = voxel;
            UpdateHighlight();
            SelectionChanged?.Invoke(voxel);
        }

        /// <summary>The voxel under a screen point (pixels, origin bottom left), or null if none can be picked there.</summary>
        public Vector3Int? Pick(Vector2 screenPoint)
        {
            Camera viewCamera = ViewCamera;
            Chunk chunk = target.Chunk;
            if (viewCamera == null || chunk == null)
            {
                return null;
            }

            Ray worldRay = viewCamera.ScreenPointToRay(screenPoint);
            var localRay = new Ray(
                transform.InverseTransformPoint(worldRay.origin),
                transform.InverseTransformDirection(worldRay.direction));

            return SurfaceRaycast.Cast(localRay, chunk, target.MeshSettings, out Vector3Int hit, out _)
                ? hit
                : (Vector3Int?)null;
        }

        /// <summary>Glides the camera to frame the selected voxel, if there is one (K6).</summary>
        public void FocusSelected()
        {
            Camera viewCamera = ViewCamera;
            var flyCamera = viewCamera != null ? viewCamera.GetComponent<FreeFlyCamera>() : null;
            if (SelectedVoxel == null || flyCamera == null)
            {
                return;
            }

            float size = target.VoxelSize;
            Vector3 centre = transform.TransformPoint(((Vector3)SelectedVoxel.Value + Vector3.one * 0.5f) * size);
            flyCamera.Focus(centre, focusDistance * size * transform.lossyScale.x);
        }

        private void Awake()
        {
            target = GetComponent<LabChunkTarget>();
        }

        private void OnEnable()
        {
            target.Changed += OnTargetChanged;
        }

        private void OnDisable()
        {
            target.Changed -= OnTargetChanged;
            Select(null);
        }

        private void OnDestroy()
        {
            if (highlight != null)
            {
                Destroy(highlight);
                Destroy(highlightMesh);
            }
            if (ownedMaterial != null)
            {
                Destroy(ownedMaterial);
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (handleInput && mouse != null && mouse.leftButton.wasPressedThisFrame && !LabGuiBlocker.IsOverGui(mouse.position.ReadValue()))
            {
                Select(Pick(mouse.position.ReadValue()));
            }
            if (handleInput && keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                FocusSelected();
            }

            // An Inspector edit to the highlight toggle skips the property.
            bool wanted = SelectedVoxel != null && showHighlight;
            if (wanted != (highlight != null && highlight.activeSelf))
            {
                UpdateHighlight();
            }

            // Follows voxel size changes, which move the voxel without a new selection.
            if (highlight != null && highlight.activeSelf)
            {
                highlight.transform.localScale = Vector3.one * target.VoxelSize;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || SelectedVoxel == null || target.Chunk == null)
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

            // Step-through reveals the case itself, so the label leaves it out while that's on.
            Vector3Int voxel = SelectedVoxel.Value;
            string caseText = StepThroughMode.IsOn(this)
                ? ""
                : $"case {MarchingCubes.GetCaseIndex(ReadCorners(voxel), target.IsoLevel)}   ";
            var content = new GUIContent(
                $"<b>Voxel ({voxel.x}, {voxel.y}, {voxel.z})</b>  {caseText}" +
                "<color=#aaaaaa>F focus · click empty space to clear</color>");

            Vector2 size = labelStyle.CalcSize(content);
            // Under WorldLab's focus label, which names the chunk this voxel is in.
            float top = target.FollowsFocus ? 10f + size.y + 4f : 10f;
            var rect = new Rect((Screen.width - size.x) * 0.5f, top, size.x, size.y);
            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, content, labelStyle);
        }

        // Another chunk (a new chunk size, or WorldLab's focus moved): the old selection doesn't apply.
        private void OnTargetChanged()
        {
            Select(null);
        }

        private float[] ReadCorners(Vector3Int voxel)
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                cornerValues[corner] = target.Chunk.GetDensity(voxel + MarchingCubes.CornerOffset(corner));
            }
            return cornerValues;
        }

        private void UpdateHighlight()
        {
            if (SelectedVoxel == null || !showHighlight)
            {
                if (highlight != null)
                {
                    highlight.SetActive(false);
                }
                return;
            }

            EnsureHighlight();
            highlight.SetActive(true);
            float size = target.VoxelSize;
            highlight.transform.localPosition = (Vector3)SelectedVoxel.Value * size;
            highlight.transform.localScale = Vector3.one * size;
        }

        // A unit cube's 12 edges as a line mesh, nudged slightly outwards so it sits
        // on top of the voxel grid gizmo instead of flickering against it.
        private void EnsureHighlight()
        {
            if (highlight != null)
            {
                return;
            }

            const float inflate = 0.01f;
            var corners = new Vector3[MarchingCubes.CornerCount];
            for (int corner = 0; corner < corners.Length; corner++)
            {
                Vector3 offset = MarchingCubes.CornerOffset(corner);
                corners[corner] = offset + (offset - Vector3.one * 0.5f) * (2f * inflate);
            }

            var indices = new int[MarchingCubes.EdgeCount * 2];
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                indices[edge * 2] = MarchingCubesTables.EdgeCorners[edge, 0];
                indices[edge * 2 + 1] = MarchingCubesTables.EdgeCorners[edge, 1];
            }

            highlightMesh = new Mesh { name = "Voxel Highlight" };
            highlightMesh.vertices = corners;
            highlightMesh.SetIndices(indices, MeshTopology.Lines, 0);

            Material material = highlightMaterial;
            if (material == null)
            {
                ownedMaterial = new Material(Shader.Find(HighlightShader)) { name = "Voxel Highlight" };
                material = ownedMaterial;
            }

            highlight = new GameObject("Voxel Highlight");
            highlight.transform.SetParent(transform, false);
            highlight.AddComponent<MeshFilter>().sharedMesh = highlightMesh;
            var renderer = highlight.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var properties = new MaterialPropertyBlock();
            properties.SetColor(ColorProperty, highlightColor);
            renderer.SetPropertyBlock(properties);
        }
    }
}
