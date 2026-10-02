using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Step-through animation of the parent chunk's mesh build (1D). Whenever the
    /// chunk's mesh is rebuilt, the build is recorded once (V15, A11); playback
    /// then replays the log (V16) with play/pause, stepping and speed (V17), an
    /// info line (V18) and Game-view visuals (V19). Turning this component on is
    /// the step-through mode: it hides the real mesh and takes the arrow keys.
    /// </summary>
    /// <remarks>
    /// Play mode only: the chunk doesn't exist before then. A rebuild (corner,
    /// case or iso change) starts the new recording from the first step, playing
    /// or paused as before.
    /// </remarks>
    public class StepThroughLab : MonoBehaviour
    {
        private const string MarkerShader = "Universal Render Pipeline/Unlit";

        // Above this many samples the density field is drawn without value labels.
        private const int MaxLabelledSamples = 64;

        [Tooltip("Playback speed multiplier.")]
        [SerializeField, Range(0.1f, 5f)]
        private float speed = 1f;

        [Tooltip("Start playing when step-through mode is turned on.")]
        [SerializeField]
        private bool autoPlay = true;

        [SerializeField]
        private StepTimings timings = new StepTimings();

        [Tooltip("Unlit material for corner spheres and edge lines. Falls back to URP Unlit if empty.")]
        [SerializeField]
        private Material markerMaterial;

        [Tooltip("Camera the Game-view labels are projected with. Defaults to the main camera.")]
        [SerializeField]
        private Camera targetCamera;

        private readonly MeshingRecorder recording = new MeshingRecorder();
        private readonly StepPlayback playback = new StepPlayback();
        private readonly List<Vector3> scratchVertices = new List<Vector3>();
        private readonly List<int> scratchTriangles = new List<int>();

        private ChunkView chunkView;
        private MeshRenderer chunkRenderer;
        private StepThroughVisuals visuals;
        private Material ownedMarkerMaterial;

        // Set when turned on before the chunk exists; the first rebuild then autoplays.
        private bool awaitingFirstRecording;

        private GUIStyle infoStyle;
        private GUIStyle labelStyle;

        public StepPlayback Playback => playback;

        public MeshingRecorder Recording => recording;

        public bool HasRecording => recording.Steps.Count > 0;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Clamp(value, 0.1f, 5f);
        }

        private Camera ViewCamera => targetCamera != null ? targetCamera : Camera.main;

        private void Awake()
        {
            chunkView = GetComponentInParent<ChunkView>();
            if (chunkView == null)
            {
                UnityEngine.Debug.LogError($"{nameof(StepThroughLab)} on '{name}' needs a {nameof(ChunkView)} parent.", this);
                enabled = false;
                return;
            }

            chunkRenderer = chunkView.GetComponent<MeshRenderer>();
        }

        private void OnEnable()
        {
            if (chunkView == null)
            {
                return;
            }

            chunkView.MeshRebuilt += OnMeshRebuilt;
            chunkRenderer.enabled = false;

            EnsureVisuals();
            visuals.Visible = true;

            // The chunk may already be clean, with no rebuild coming to trigger a recording.
            if (chunkView.Chunk != null)
            {
                Record(autoPlay);
            }
            else
            {
                awaitingFirstRecording = true;
            }
        }

        private void OnDisable()
        {
            if (chunkView == null)
            {
                return;
            }

            chunkView.MeshRebuilt -= OnMeshRebuilt;
            chunkRenderer.enabled = true;
            if (visuals != null)
            {
                visuals.Visible = false;
            }
            playback.Pause();
        }

        private void OnDestroy()
        {
            visuals?.Dispose();
            if (ownedMarkerMaterial != null)
            {
                Destroy(ownedMarkerMaterial);
            }
        }

        private void Update()
        {
            if (!HasRecording)
            {
                return;
            }

            playback.Advance(Time.deltaTime * speed, index => timings.For(recording.Steps[index].Type));
            visuals.Show(playback.StepIndex, playback.Progress);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !HasRecording)
            {
                return;
            }

            EnsureStyles();
            Camera viewCamera = ViewCamera;
            if (viewCamera != null)
            {
                DrawSceneLabels(viewCamera);
            }
            DrawInfoBox();
        }

        private void OnMeshRebuilt(Mesh mesh)
        {
            Record(awaitingFirstRecording ? autoPlay : playback.IsPlaying);
            awaitingFirstRecording = false;
        }

        // Re-runs the real mesher once with a recorder; playback only ever reads the log.
        // Always starts again from the first step, since the old steps no longer apply.
        private void Record(bool play)
        {
            ChunkMesher.Build(chunkView.Chunk, chunkView.Config.MeshSettings, scratchVertices, scratchTriangles, recording);
            playback.Load(recording.Steps.Count);
            visuals.Load(recording);
            if (play)
            {
                playback.Play();
            }

            // Draw now rather than next Update, so markers never show a frame unplaced.
            visuals.Show(playback.StepIndex, playback.Progress);
        }

        private void EnsureVisuals()
        {
            if (visuals != null)
            {
                return;
            }

            Material marker = markerMaterial;
            if (marker == null)
            {
                ownedMarkerMaterial = new Material(Shader.Find(MarkerShader)) { name = "Step Through Markers" };
                marker = ownedMarkerMaterial;
            }
            visuals = new StepThroughVisuals(chunkView.transform, marker, chunkRenderer.sharedMaterial);
        }

        private void DrawSceneLabels(Camera viewCamera)
        {
            MeshingStep step = recording.Steps[playback.StepIndex];
            float progress = playback.Progress;
            float size = recording.Settings.VoxelSize;

            if (step.Type == MeshingStepType.DensityField)
            {
                DrawSampleLabels(viewCamera, progress, size);
                return;
            }

            RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];
            Vector3 centre = voxel.Origin + Vector3.one * (0.5f * size);
            int sampled = StepReveal.CornersSampled(step.Type, progress);

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Color color = corner >= sampled ? Color.white
                    : MarchingCubes.IsCornerSolid(voxel.CaseIndex, corner) ? StepThroughVisuals.SolidColor
                    : StepThroughVisuals.EmptyColor;
                Vector3 position = voxel.Origin + (Vector3)MarchingCubes.CornerOffset(corner) * size;
                DrawLabelBeside(viewCamera, position, centre, StepThroughVisuals.HighlightedCornerRadius * size,
                    $"c{corner} {voxel.CornerValues[corner]:0.00}", color);
            }

            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                if (!StepReveal.IsEdgeLit(voxel, edge, step.Type, progress))
                {
                    continue;
                }

                Vector3 a = voxel.Origin + (Vector3)MarchingCubes.CornerOffset(MarchingCubesTables.EdgeCorners[edge, 0]) * size;
                Vector3 b = voxel.Origin + (Vector3)MarchingCubes.CornerOffset(MarchingCubesTables.EdgeCorners[edge, 1]) * size;
                DrawLabelBeside(viewCamera, (a + b) * 0.5f, centre, 0.06f * size, $"e{edge}", StepThroughVisuals.CrossedEdgeColor);
            }
        }

        private void DrawSampleLabels(Camera viewCamera, float progress, float size)
        {
            Vector3Int count = recording.SampleCount;
            int total = count.x * count.y * count.z;
            if (total > MaxLabelledSamples)
            {
                return;
            }

            Vector3 centre = (Vector3)(count - Vector3Int.one) * (0.5f * size);
            int shown = StepReveal.Revealed(progress, total);
            for (int i = 0; i < shown; i++)
            {
                var sample = new Vector3Int(i % count.x, i / count.x % count.y, i / (count.x * count.y));
                DrawLabelBeside(viewCamera, (Vector3)sample * size, centre, StepThroughVisuals.HighlightedCornerRadius * size,
                    recording.GetDensity(sample).ToString("0.00"), Color.white);
            }
        }

        /// <summary>
        /// Draws a label just outside a marker of the given radius, pushed away from
        /// <paramref name="centre"/> on screen so it never sits on top of the marker.
        /// </summary>
        private void DrawLabelBeside(Camera viewCamera, Vector3 localPoint, Vector3 localCentre, float markerRadius, string text, Color color)
        {
            GuiDrawing.LabelBeside(viewCamera, chunkView.transform, localPoint, localCentre, markerRadius, text, color, labelStyle);
        }

        private void DrawInfoBox()
        {
            int index = playback.StepIndex;
            MeshingStep step = recording.Steps[index];

            string title = $"Step {index + 1} / {playback.StepCount} · {StepTitle(step.Type)}{(playback.IsPlaying ? "" : " (paused)")}";
            string detail = StepDescriber.Describe(recording, index);
            if (step.Type == MeshingStepType.CaseIndex)
            {
                RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];
                int known = StepReveal.CaseBitsKnown(step.Type, playback.Progress);
                detail += $"\nBits so far (c7 → c0): {StepDescriber.PartialBinary(voxel.CaseIndex, known)}";
            }

            string text = $"<b>{title}</b>\n{StepDescriber.Summarise(recording, index)}\n{detail}\n" +
                          "<color=#aaaaaa>Space play/pause · ← → step · R restart</color>";

            // Bottom right, clear of the axes HUD in the bottom-left corner.
            const float margin = 10f;
            float width = Mathf.Min(Screen.width - 2f * margin, 760f);
            float height = infoStyle.CalcHeight(new GUIContent(text), width);
            var rect = new Rect(Screen.width - width - margin, Screen.height - height - margin, width, height);

            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, text, infoStyle);
        }

        private static string StepTitle(MeshingStepType type)
        {
            switch (type)
            {
                case MeshingStepType.DensityField: return "Density field";
                case MeshingStepType.SampleCorners: return "Sample corners";
                case MeshingStepType.CaseIndex: return "Case index";
                case MeshingStepType.EdgeTable: return "Edge table";
                case MeshingStepType.Interpolate: return "Interpolate";
                case MeshingStepType.Triangle: return "Triangle table";
                default: return type.ToString();
            }
        }

        private void EnsureStyles()
        {
            if (infoStyle != null)
            {
                return;
            }

            infoStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                wordWrap = true,
                fontSize = 13,
                padding = new RectOffset(10, 10, 8, 8),
                normal = { textColor = Color.white },
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter,
            };
        }
    }
}
