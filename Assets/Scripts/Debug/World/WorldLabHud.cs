using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// WorldLab's heads-up display, top left: the frame rate; where the camera (the
    /// player, for now) is, as a world position, the voxel it's in (global coordinates),
    /// that voxel's chunk (M1) and its place inside the chunk; and the brush's running
    /// totals of volume placed and dug out. A fixed number of lines, so it never resizes.
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public class WorldLabHud : MonoBehaviour
    {
        private const float Margin = 10f;
        private const float Width = 400f;

        // The frame rate is frames counted over this many seconds, so one long frame
        // (e.g. a regenerate) only affects one window instead of dragging an average.
        private const float FpsWindowSeconds = 0.5f;

        private WorldView worldView;
        private TerrainBrushTool brush;
        private GUIStyle style;

        private int windowFrames;
        private float windowSeconds;
        private float framesPerSecond;

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
            brush = GetComponent<TerrainBrushTool>();
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
            Transform focus = worldView.Focus;
            if (Event.current.type != EventType.Repaint || worldView.World == null || focus == null)
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 13,
                padding = new RectOffset(10, 10, 8, 8),
                normal = { textColor = Color.white },
            };

            WorldGrid grid = worldView.World.Grid;
            Vector3 position = worldView.transform.InverseTransformPoint(focus.position);
            Vector3Int voxel = grid.WorldToVoxel(position);
            Vector3Int chunk = grid.VoxelToChunk(voxel);
            Vector3Int local = grid.GlobalToLocal(voxel, chunk);
            string frameTime = framesPerSecond > 0f ? $"{1000f / framesPerSecond:0.0} ms" : "measuring";

            string text =
                $"<b>{framesPerSecond:0} FPS</b>  ({frameTime})\n" +
                $"Position   ({position.x:0.0}, {position.y:0.0}, {position.z:0.0})\n" +
                $"Voxel      ({voxel.x}, {voxel.y}, {voxel.z})\n" +
                $"Chunk      ({chunk.x}, {chunk.y}, {chunk.z}), voxel ({local.x}, {local.y}, {local.z}) in it";
            if (brush != null)
            {
                float net = brush.TotalVolumeAdded - brush.TotalVolumeRemoved;
                text += $"\nBrushed    placed {brush.TotalVolumeAdded:0.0} u³ · dug {brush.TotalVolumeRemoved:0.0} u³ · " +
                        $"net {net:+0.0;-0.0;0.0} u³";
            }

            var content = new GUIContent(text);
            var rect = new Rect(Margin, Margin, Width, style.CalcHeight(content, Width));
            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, content, style);
        }
    }
}
