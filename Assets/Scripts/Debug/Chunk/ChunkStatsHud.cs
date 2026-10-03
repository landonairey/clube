using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Game-view readout for the chunk lab (K4): frame rate, chunk size, vertex
    /// and triangle counts, how long meshing and the Unity mesh upload took, the
    /// solid volume when a <see cref="ChunkVolumeStats"/> is present, and a warning
    /// when <see cref="ChunkDebugView"/> has to suppress its sample spheres.
    /// Reads <see cref="ChunkView.LastBuildStats"/> only (A4).
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkStatsHud : MonoBehaviour
    {
        public const string SamplesSuppressedWarning =
            "Too many density samples to draw: the sample spheres are suppressed above 40,000 samples (about a 33³ chunk).";

        // The frame rate is frames counted over this many seconds, so one long frame
        // (e.g. a big rebuild) only affects one window instead of dragging an average.
        private const float FpsWindowSeconds = 0.5f;

        private ChunkView chunkView;
        private ChunkDebugView debugView;
        private ChunkVolumeStats volumeStats;
        private ChunkTerrainFill terrainFill;
        private GUIStyle style;

        private int windowFrames;
        private float windowSeconds;
        private float framesPerSecond;

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
            debugView = GetComponent<ChunkDebugView>();
            volumeStats = GetComponent<ChunkVolumeStats>();
            terrainFill = GetComponent<ChunkTerrainFill>();
        }

        private void Update()
        {
            // Unscaled, so the counter stays honest while the game is paused (timeScale = 0).
            windowFrames++;
            windowSeconds += Time.unscaledDeltaTime;
            if (windowSeconds >= FpsWindowSeconds)
            {
                framesPerSecond = windowFrames / windowSeconds;
                windowFrames = 0;
                windowSeconds = 0f;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || chunkView.Chunk == null)
            {
                return;
            }

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    wordWrap = true,
                    fontSize = 13,
                    padding = new RectOffset(10, 10, 8, 8),
                    normal = { textColor = Color.white },
                };
            }

            string frameTime = framesPerSecond > 0f ? $"{1000f / framesPerSecond:0.0} ms" : "measuring";
            string text = $"<b>{framesPerSecond:0} FPS</b>  ({frameTime})\n" +
                          Describe(chunkView.Chunk.VoxelCount, chunkView.LastBuildStats);
            if (terrainFill != null && terrainFill.enabled && !terrainFill.IsOverridden)
            {
                text += $"\nGenerate   {terrainFill.LastFillMilliseconds:0.00} ms  ({chunkView.Config.Terrain.Generator})";
            }
            if (volumeStats != null && volumeStats.enabled && volumeStats.HasMeasurement)
            {
                text += "\n" + DescribeVolume(volumeStats);
            }
            if (debugView != null && debugView.enabled && debugView.AreSamplesSuppressed)
            {
                text += $"\n<color=#ffcc44><b>Warning:</b> {SamplesSuppressedWarning}</color>";
            }

            var content = new GUIContent(text);
            const float width = 380f;
            float height = style.CalcHeight(content, width);
            var rect = new Rect(10f, 10f, width, height);

            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, content, style);
        }

        /// <summary>
        /// e.g. "Volume  exact 137.4 u³ (26.8%), approx 141.0 u³ (+2.6%)  0.42 ms":
        /// the approximation's error is relative to the exact volume.
        /// </summary>
        private static string DescribeVolume(ChunkVolumeStats volume)
        {
            float capacity = Mathf.Max(volume.ChunkCapacity, 1e-6f);
            string error = volume.Exact > 1e-6f
                ? $"{(volume.Approximate - volume.Exact) / volume.Exact * 100f:+0.0;-0.0;0.0}%"
                : "n/a";
            return $"Volume     exact {volume.Exact:0.0} u³ ({volume.Exact / capacity * 100f:0.0}% of chunk)\n" +
                   $"                approx {volume.Approximate:0.0} u³ ({error} vs exact)  {volume.Milliseconds:0.00} ms";
        }

        /// <summary>The build part of the readout, shared with the Inspector.</summary>
        public static string Describe(Vector3Int voxelCount, ChunkMeshStats stats)
        {
            int voxels = voxelCount.x * voxelCount.y * voxelCount.z;
            return $"<b>Chunk {voxelCount.x} × {voxelCount.y} × {voxelCount.z}</b> ({voxels:N0} voxels)\n" +
                   $"Vertices   {stats.VertexCount:N0}\n" +
                   $"Triangles  {stats.TriangleCount:N0}\n" +
                   $"Build      {stats.TotalMilliseconds:0.00} ms  " +
                   $"(meshing {stats.MeshingMilliseconds:0.00}, upload {stats.UploadMilliseconds:0.00})";
        }
    }
}
