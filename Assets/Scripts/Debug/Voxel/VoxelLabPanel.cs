using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// In-game control panel for VoxelLab (K31, V22): the voxel lab's Inspector panel,
    /// drawn with IMGUI so it works in a build. Foldout sections for the corner sliders
    /// (V2), the case and what it means (V7, V9, V20), presets (V10), meshing (iso,
    /// edges, shading), step-through, the volume readout (V11, V12, V14) and display
    /// toggles (labels, normals, flipped faces, samples, axes). Tab hides it.
    /// </summary>
    /// <remarks>
    /// Edits go through the same paths as the Inspector: the corner editor writes the
    /// chunk's densities (A7), and config edits call <see cref="WorldConfig.NotifyChanged"/>.
    /// The frame (scrolling, click blocking, styles) is <see cref="LabPanelFrame"/>.
    /// </remarks>
    [RequireComponent(typeof(VoxelCornerEditor))]
    public class VoxelLabPanel : MonoBehaviour
    {
        private const int PresetColumns = 2;
        private const float CornerLabelWidth = 64f;

        private static readonly Color CurrentPresetTint = new Color(1f, 0.8f, 0.4f);

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
        private bool cornersOpen = true;

        [SerializeField]
        private bool caseOpen = true;

        [SerializeField]
        private bool presetsOpen;

        [SerializeField]
        private bool meshingOpen;

        [SerializeField]
        private bool stepThroughOpen;

        [SerializeField]
        private bool volumeOpen;

        [SerializeField]
        private bool displayOpen;

        [SerializeField]
        private bool cameraOpen;

        private VoxelCornerEditor corners;
        private ChunkView chunkView;
        private VoxelLabels labels;
        private NormalLines normals;
        private FlipFaces flipFaces;
        private ChunkDebugView debugView;
        private StepThroughLab stepThrough;
        private VoxelVolumeLab volumeLab;
        private AxesHud axesHud;
        private LabPanelFrame frame;
        private FreeFlyCamera flyCamera;
        private bool open;

        private void Awake()
        {
            corners = GetComponent<VoxelCornerEditor>();
            chunkView = GetComponent<ChunkView>();
            labels = GetComponent<VoxelLabels>();
            normals = GetComponent<NormalLines>();
            flipFaces = GetComponent<FlipFaces>();
            debugView = GetComponent<ChunkDebugView>();
            stepThrough = GetComponentInChildren<StepThroughLab>(true);
            volumeLab = GetComponentInChildren<VoxelVolumeLab>(true);
            axesHud = FindFirstObjectByType<AxesHud>();
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
            if (frame.Section("Corners", ref cornersOpen))
            {
                DrawCorners();
            }
            if (frame.Section("Case", ref caseOpen))
            {
                DrawCase();
            }
            if (frame.Section("Presets", ref presetsOpen))
            {
                DrawPresets();
            }
            if (frame.Section("Meshing", ref meshingOpen))
            {
                MeshingControls.Draw(frame, chunkView.Config);
            }
            if (stepThrough != null && frame.Section("Step-through", ref stepThroughOpen))
            {
                StepThroughControls.Draw(frame, stepThrough);
            }
            if (volumeLab != null && frame.Section("Volume", ref volumeOpen))
            {
                DrawVolume();
            }
            if (flyCamera != null && frame.Section("Camera", ref cameraOpen))
            {
                CameraControls.Draw(frame, flyCamera);
            }
            if (frame.Section("Display", ref displayOpen))
            {
                DrawDisplay();
            }

            GUILayout.Space(8f);
            GUILayout.Label(
                "← → step case · Right mouse look · WASD move · Q/E down/up · Shift fast · " +
                "Scroll pull volume pieces apart · Tab hide panel",
                frame.Hint);
            frame.End();
        }

        // One slider per corner, 0 = empty to 1 = solid (V2).
        private void DrawCorners()
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                float value = corners.CornerValues[corner];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"c{corner} {value:0.00}", frame.Label, GUILayout.Width(CornerLabelWidth));
                float chosen = GUILayout.HorizontalSlider(value, 0f, 1f);
                GUILayout.EndHorizontal();
                if (!Mathf.Approximately(chosen, value))
                {
                    corners.SetCornerValue(corner, chosen);
                }
            }
            GUILayout.Label("0 = empty, 1 = solid. Solid corner c sets bit c of the case index.", frame.Hint);
        }

        // The case index, stepping and jumping (V20), and what it means (V7, V9).
        private void DrawCase()
        {
            int caseIndex = corners.CaseIndex;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Case {caseIndex}", frame.Header, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("◄", frame.Button))
            {
                corners.StepCase(-1);
            }
            if (GUILayout.Button("►", frame.Button))
            {
                corners.StepCase(+1);
            }
            GUILayout.EndHorizontal();

            int chosen = Mathf.RoundToInt(GUILayout.HorizontalSlider(caseIndex, 0f, MarchingCubes.CaseCount - 1));
            if (chosen != caseIndex)
            {
                corners.ApplyCase(chosen);
                caseIndex = corners.CaseIndex;
            }

            BaseConfiguration configuration = MarchingCubesCases.GetBaseConfiguration(caseIndex);
            GUILayout.Label(
                $"Binary (c7 → c0)  {VoxelCaseText.Binary(caseIndex)}\n" +
                $"Solid corners  {VoxelCaseText.List(VoxelCaseText.SolidCorners(caseIndex))}\n" +
                $"Base configuration  {(int)configuration} · {BaseConfigurationText.Name(configuration)}\n" +
                $"Ambiguous face  {(MarchingCubesCases.HasAmbiguousFace(caseIndex) ? "yes" : "no")}\n" +
                $"Crossed edges  {VoxelCaseText.List(VoxelCaseText.CrossedEdges(caseIndex))}\n" +
                $"Triangles  {VoxelCaseText.TriangleCount(caseIndex)}",
                frame.Label);
            GUILayout.Label(BaseConfigurationText.Description(configuration), frame.Hint);
        }

        // One button per base configuration (V10); the current one is highlighted.
        private void DrawPresets()
        {
            BaseConfiguration current = MarchingCubesCases.GetBaseConfiguration(corners.CaseIndex);
            for (int row = 0; row * PresetColumns < MarchingCubesCases.BaseConfigurationCount; row++)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < PresetColumns; column++)
                {
                    int index = row * PresetColumns + column;
                    if (index >= MarchingCubesCases.BaseConfigurationCount)
                    {
                        break;
                    }

                    var configuration = (BaseConfiguration)index;
                    int representative = MarchingCubesCases.GetRepresentativeCase(configuration);
                    bool ambiguous = MarchingCubesCases.HasAmbiguousFace(representative);
                    Color previous = GUI.backgroundColor;
                    if (configuration == current)
                    {
                        GUI.backgroundColor = CurrentPresetTint;
                    }
                    if (GUILayout.Button($"{index} {BaseConfigurationText.Name(configuration)}{(ambiguous ? " (!)" : "")}", frame.Button))
                    {
                        corners.ApplyCase(representative);
                    }
                    GUI.backgroundColor = previous;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("One case per base configuration; (!) = ambiguous face.", frame.Hint);
        }

        // The volume measures (V11, V12, V14) and the exact-volume view.
        private void DrawVolume()
        {
            VolumeReport report = volumeLab.Measure();
            float cubeVolume = Mathf.Pow(corners.VoxelSize, 3f);
            string text = $"Approximate (corner mean)  {VolumeText.Volume(report.Approximate, cubeVolume)}";
            if (report.HasExact)
            {
                text += $"\nExact (tetrahedra)  {VolumeText.Volume(report.Exact, cubeVolume)}" +
                        $"\nApproximate − exact  {VolumeText.Error(report.Approximate, report.Exact)}";
            }
            if (report.MonteCarloSamples > 0)
            {
                text += $"\nSmooth field  {VolumeText.Volume(report.TrilinearEstimate, cubeVolume)}";
            }
            GUILayout.Label(text, frame.Label);

            bool exact = frame.ToggleField("Exact volume (draw the tetrahedra)", volumeLab.ExactVolume);
            if (exact != volumeLab.ExactVolume)
            {
                volumeLab.ExactVolume = exact;
            }
            if (exact)
            {
                volumeLab.ExplodeDistance = frame.Slider("Explode distance", volumeLab.ExplodeDistance, 0f, 1f);
            }
        }

        // Each toggle only writes a real change, since some rebuild meshes.
        private void DrawDisplay()
        {
            if (labels != null)
            {
                labels.CornerLabelsOn = frame.ToggleField("Corner labels and bit table", labels.CornerLabelsOn);
                labels.CrossedEdgesOn = frame.ToggleField("Highlight crossed edges", labels.CrossedEdgesOn);
                labels.EdgeLabelsOn = frame.ToggleField("Edge numbers", labels.EdgeLabelsOn);
            }
            if (normals != null)
            {
                SetIfChanged(frame.ToggleField("Vertex normals", normals.ShowVertexNormals), normals.ShowVertexNormals, v => normals.ShowVertexNormals = v);
                SetIfChanged(frame.ToggleField("Face normals", normals.ShowFaceNormals), normals.ShowFaceNormals, v => normals.ShowFaceNormals = v);
                SetIfChanged(frame.ToggleField("Triangle outlines", normals.ShowTriangleEdges), normals.ShowTriangleEdges, v => normals.ShowTriangleEdges = v);
            }
            if (flipFaces != null)
            {
                SetIfChanged(frame.ToggleField("Flip faces (reverse winding)", flipFaces.Flip), flipFaces.Flip, v => flipFaces.Flip = v);
            }
            if (debugView != null)
            {
                SetIfChanged(frame.ToggleField("Density samples", debugView.ShowSamples), debugView.ShowSamples, v => debugView.ShowSamples = v);
            }
            if (axesHud != null)
            {
                axesHud.ShowAxes = frame.ToggleField("Axes", axesHud.ShowAxes);
            }
        }

        private static void SetIfChanged(bool chosen, bool current, System.Action<bool> set)
        {
            if (chosen != current)
            {
                set(chosen);
            }
        }
    }
}
