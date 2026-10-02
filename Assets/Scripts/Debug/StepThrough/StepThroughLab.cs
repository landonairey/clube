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
    /// Play mode only: the chunk doesn't exist before then.
    /// </remarks>
    public class StepThroughLab : MonoBehaviour
    {
        private const string MarkerShader = "Universal Render Pipeline/Unlit";

        [Tooltip("Playback speed multiplier.")]
        [SerializeField, Range(0.1f, 5f)]
        private float speed = 1f;

        [Tooltip("Start playing as soon as a new recording is made.")]
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
        private GUIStyle infoStyle;
        private GUIStyle valueStyle;

        public StepPlayback Playback => playback;

        public MeshingRecorder Recording => recording;

        public bool HasRecording => recording.Steps.Count > 0;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Clamp(value, 0.1f, 5f);
        }

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
                Record();
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
            DrawCornerLabels();
            DrawInfoBox();
        }

        private void OnMeshRebuilt(Mesh mesh)
        {
            Record();
        }

        // Re-runs the real mesher once with a recorder; playback only ever reads the log.
        private void Record()
        {
            ChunkMesher.Build(chunkView.Chunk, chunkView.Config.MeshSettings, scratchVertices, scratchTriangles, recording);
            playback.Load(recording.Steps.Count);
            visuals.Load(recording);
            if (autoPlay)
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

        // Name and density next to each corner of the current voxel, e.g. "c3 0.20".
        // VoxelLabels hides while step-through is on, so these replace its corner names.
        private void DrawCornerLabels()
        {
            MeshingStep step = recording.Steps[playback.StepIndex];
            Camera viewCamera = targetCamera != null ? targetCamera : Camera.main;
            if (viewCamera == null)
            {
                return;
            }

            RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];
            float size = recording.Settings.VoxelSize;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Vector3 local = voxel.Origin + (Vector3)MarchingCubes.CornerOffset(corner) * size;
                Vector3 screen = viewCamera.WorldToScreenPoint(chunkView.transform.TransformPoint(local));
                if (screen.z <= 0f)
                {
                    continue;
                }

                // Screen y grows upwards, GUI y grows downwards; sit just below the corner.
                var gui = new Vector2(screen.x, Screen.height - screen.y + 18f);
                GuiDrawing.CentredLabel(gui, $"c{corner} {voxel.CornerValues[corner]:0.00}", valueStyle);
            }
        }

        private void DrawInfoBox()
        {
            int index = playback.StepIndex;
            MeshingStep step = recording.Steps[index];

            string title = $"Step {index + 1} / {playback.StepCount} · {step.Type}{(playback.IsPlaying ? "" : " (paused)")}";
            string detail = StepDescriber.Describe(recording, index);
            if (step.Type == MeshingStepType.CaseIndex)
            {
                RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];
                int known = playback.Progress >= 1f
                    ? MarchingCubes.CornerCount
                    : Mathf.Min(Mathf.FloorToInt(playback.Progress * MarchingCubes.CornerCount) + 1, MarchingCubes.CornerCount);
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
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.95f, 1f) },
            };
        }
    }
}
