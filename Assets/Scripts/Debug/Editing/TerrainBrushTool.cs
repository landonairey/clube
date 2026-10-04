using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>The click tools a <see cref="TerrainBrushTool"/> switches between.</summary>
    public enum ChunkClickTool
    {
        /// <summary>Clicks select voxels (<see cref="VoxelSelector"/>, K5).</summary>
        Select,

        /// <summary>Clicks remove terrain (K14).</summary>
        Dig,

        /// <summary>Clicks add terrain (K13).</summary>
        Add,
    }

    /// <summary>
    /// Edits terrain with a sphere brush in the Game view (K13–K16): 1, 2 and 3
    /// pick Select, Dig or Add; in Dig and Add, the left mouse button applies the
    /// brush where the cursor meets the surface, repeating while held; [ and ] change
    /// the radius. A translucent sphere previews the brush. The terrain is whatever
    /// <see cref="IEditableTerrain"/> sits on the same object: one chunk
    /// (<see cref="ChunkView"/>) or a world of them (<see cref="WorldView"/>, where a
    /// brush across a border edits every chunk it reaches, M5). Edits go through
    /// <see cref="TerrainBrush"/> and so through each chunk's single edit path (A7).
    /// </summary>
    /// <remarks>
    /// In Dig and Add it switches the <see cref="VoxelSelector"/> off, if there is one,
    /// so clicks edit instead of selecting. Play mode only, like the terrain itself.
    /// </remarks>
    public class TerrainBrushTool : MonoBehaviour
    {
        public const float MinRadius = 0.25f;
        public const float MaxRadius = 16f;
        private const float RadiusStep = 1.25f;
        private const float BottomCornerReserve = 150f;

        [Tooltip("What left clicks do. Keys 1, 2 and 3 switch in Play mode.")]
        [SerializeField]
        private ChunkClickTool tool = ChunkClickTool.Select;

        [Tooltip("Brush radius in world units (K15). [ and ] change it in Play mode.")]
        [SerializeField, Range(0.25f, 16f)]
        private float radius = 2f;

        [Tooltip("Density added or removed per application at full effect (K16). 1 fills or empties a sample at once.")]
        [SerializeField, Range(0.01f, 1f)]
        private float strength = 1f;

        [Tooltip("Hard: every sample within the radius changes. Soft: only the surface layer changes, so holding it digs down or piles up layer by layer (K16).")]
        [SerializeField]
        private BrushFalloff falloff = BrushFalloff.Hard;

        [Tooltip("Applications per second while the mouse button is held.")]
        [SerializeField, Range(1f, 60f)]
        private float repeatRate = 10f;

        [SerializeField]
        private Color digPreviewColor = new Color(1f, 0.3f, 0.2f, 0.45f);

        [SerializeField]
        private Color addPreviewColor = new Color(0.3f, 1f, 0.4f, 0.45f);

        [Tooltip("Material that shows vertex colours and alpha (LabVertexColorTransparent), for the preview sphere.")]
        [SerializeField]
        private Material previewMaterial;

        [Tooltip("Camera the brush is aimed from. Defaults to the main camera.")]
        [SerializeField]
        private Camera targetCamera;

        private readonly List<Vector3> sphereVertices = new List<Vector3>();
        private readonly List<int> sphereTriangles = new List<int>();
        private readonly List<Color32> sphereColors = new List<Color32>();

        private IEditableTerrain terrain;
        private VoxelSelector selector;
        private LabMeshObject preview;
        private Color32 previewColor;
        private Color32? meshColor;
        private float nextApplyTime;
        private GUIStyle labelStyle;

        public ChunkClickTool Tool
        {
            get => tool;
            set
            {
                tool = value;
                ApplyTool();
            }
        }

        /// <summary>Brush radius in world units (K15).</summary>
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Clamp(value, MinRadius, MaxRadius);
        }

        /// <summary>Density per application at full effect, 0.01-1 (K16).</summary>
        public float Strength
        {
            get => strength;
            set => strength = Mathf.Clamp(value, 0.01f, 1f);
        }

        public BrushFalloff Falloff
        {
            get => falloff;
            set => falloff = value;
        }

        public BrushSettings Brush => new BrushSettings(radius, strength, falloff);

        /// <summary>Samples changed by the last application, for the readout.</summary>
        public int LastChangedSamples { get; private set; }

        /// <summary>Approximate solid volume placed since the last reset, in world units³.</summary>
        public float TotalVolumeAdded { get; private set; }

        /// <summary>Approximate solid volume dug out since the last reset, in world units³.</summary>
        public float TotalVolumeRemoved { get; private set; }

        /// <summary>Starts the placed and dug totals again from zero.</summary>
        public void ResetVolumeTotals()
        {
            TotalVolumeAdded = 0f;
            TotalVolumeRemoved = 0f;
        }

        private Camera ViewCamera => targetCamera != null ? targetCamera : Camera.main;

        private bool IsEditing => tool != ChunkClickTool.Select;

        private void Awake()
        {
            terrain = GetComponent<IEditableTerrain>();
            selector = GetComponent<VoxelSelector>();
        }

        private void OnEnable()
        {
            ApplyTool();
        }

        private void OnDisable()
        {
            if (selector != null)
            {
                selector.enabled = true;
            }
            if (preview != null)
            {
                preview.Visible = false;
            }
        }

        private void OnDestroy()
        {
            preview?.Dispose();
        }

        // Inspector changes to the tool apply at once in Play mode.
        private void OnValidate()
        {
            if (terrain != null && enabled && gameObject.activeInHierarchy)
            {
                ApplyTool();
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                ReadKeys(keyboard);
            }

            if (!IsEditing || mouse == null || terrain == null || !terrain.IsReady)
            {
                return;
            }

            // Right mouse is the fly camera's look button; clicks on a lab panel are for the panel.
            Vector2 pointer = mouse.position.ReadValue();
            Vector3 centre = default;
            bool hasTarget = !mouse.rightButton.isPressed && !LabGuiBlocker.IsOverGui(pointer) && Aim(pointer, out centre);
            UpdatePreview(hasTarget, centre);
            if (!hasTarget)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame || (mouse.leftButton.isPressed && Time.unscaledTime >= nextApplyTime))
            {
                var operation = tool == ChunkClickTool.Add ? BrushOperation.Add : BrushOperation.Remove;
                BrushResult result = terrain.ApplyBrush(centre, Brush, operation);
                LastChangedSamples = result.ChangedSamples;
                TotalVolumeAdded += result.VolumeAdded(terrain.VoxelSize);
                TotalVolumeRemoved += result.VolumeRemoved(terrain.VoxelSize);
                nextApplyTime = Time.unscaledTime + 1f / repeatRate;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || terrain == null || !terrain.IsReady)
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(10, 10, 6, 6),
                normal = { textColor = Color.white },
            };

            string brush = IsEditing
                ? $"   radius {radius:0.##} · strength {strength:0.##} · {falloff.ToString().ToLowerInvariant()}"
                : "";
            var content = new GUIContent(
                $"<b>{tool}</b>{brush}   <color=#aaaaaa>1 select · 2 dig · 3 add · [ ] radius</color>");

            // One line when it fits; in a narrow window it wraps instead of running off the
            // edges, keeping the bottom corners free for buttons (the demo's Exit, K34).
            float width = Mathf.Min(labelStyle.CalcSize(content).x, Screen.width - 2f * BottomCornerReserve);
            float height = labelStyle.CalcHeight(content, width);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 10f, width, height);
            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, content, labelStyle);
        }

        private void ReadKeys(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Tool = ChunkClickTool.Select;
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                Tool = ChunkClickTool.Dig;
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                Tool = ChunkClickTool.Add;
            }

            if (keyboard.leftBracketKey.wasPressedThisFrame)
            {
                Radius = radius / RadiusStep;
            }
            else if (keyboard.rightBracketKey.wasPressedThisFrame)
            {
                Radius = radius * RadiusStep;
            }
        }

        // Selecting and editing share the left mouse button, so only one has it at a time.
        private void ApplyTool()
        {
            if (selector != null)
            {
                selector.enabled = !IsEditing;
            }
            if (!IsEditing && preview != null)
            {
                preview.Visible = false;
            }
            previewColor = tool == ChunkClickTool.Add ? addPreviewColor : digPreviewColor;
        }

        /// <summary>Where the cursor's ray meets the surface, in world space.</summary>
        private bool Aim(Vector2 screenPoint, out Vector3 centre)
        {
            centre = default;
            Camera viewCamera = ViewCamera;
            return viewCamera != null && terrain.Raycast(viewCamera.ScreenPointToRay(screenPoint), out centre);
        }

        private void UpdatePreview(bool visible, Vector3 centre)
        {
            if (!visible)
            {
                if (preview != null)
                {
                    preview.Visible = false;
                }
                return;
            }

            if (preview == null)
            {
                preview = new LabMeshObject(transform, "Brush Preview", previewMaterial);
                LabMeshes.Icosphere(sphereVertices, sphereTriangles);
                preview.Mesh.SetVertices(sphereVertices);
                preview.Mesh.SetTriangles(sphereTriangles, 0);
            }

            if (!meshColor.HasValue || !meshColor.Value.Equals(previewColor))
            {
                sphereColors.Clear();
                for (int i = 0; i < sphereVertices.Count; i++)
                {
                    sphereColors.Add(previewColor);
                }
                preview.Mesh.SetColors(sphereColors);
                meshColor = previewColor;
            }

            preview.Visible = true;
            preview.Transform.position = centre;
            preview.Transform.localScale = Vector3.one * radius;
        }
    }
}
