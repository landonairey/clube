using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Game-view readout of the chunk's last mesh build (K4): size, vertex and
    /// triangle counts, and how long meshing and the Unity mesh upload took.
    /// Reads <see cref="ChunkView.LastBuildStats"/> only (A4).
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkStatsHud : MonoBehaviour
    {
        private ChunkView chunkView;
        private GUIStyle style;

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
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
                    fontSize = 13,
                    padding = new RectOffset(10, 10, 8, 8),
                    normal = { textColor = Color.white },
                };
            }

            string text = Describe(chunkView.Chunk.VoxelCount, chunkView.LastBuildStats);
            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);
            var rect = new Rect(10f, 10f, size.x, size.y);

            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, content, style);
        }

        /// <summary>The readout text, shared with the Inspector.</summary>
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
