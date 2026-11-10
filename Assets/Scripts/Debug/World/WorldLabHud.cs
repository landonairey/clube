using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// WorldLab's heads-up display, top left: the frame rate; where the camera (the
    /// player, for now) is, as a world position, the voxel it's in (global coordinates),
    /// that voxel's chunk (M1) and its place inside the chunk; the player's lifetime volume
    /// collected and placed (GL442); last, the game time and the day (GL259). A fixed number of lines, so it never
    /// resizes. F3 hides it (GL78).
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public class WorldLabHud : MonoBehaviour
    {
        private const float Margin = 10f;
        private const float Width = 400f * Clube.Game.GameUi.FontScale;

        // The frame rate is frames counted over this many seconds, so one long frame
        // (e.g. a regenerate) only affects one window instead of dragging an average.
        private const float FpsWindowSeconds = 0.5f;

        private WorldView worldView;
        private Clube.Game.PlayerVolumeLedger ledger;
        private Clube.Game.DayNightCycle dayNight;
        private GUIStyle style;

        private int windowFrames;
        private float windowSeconds;
        private float framesPerSecond;

        // F3 hides the box (GL78).
        private bool shown = true;

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void Update()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                shown = !shown;
            }

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
            if (!shown)
            {
                return;
            }
            Transform focus = worldView.Focus;
            if (Event.current.type != EventType.Repaint || worldView.World == null || focus == null)
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = Clube.Game.GameUi.Font(13),
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
            // The player's lifetime volume collected and placed (GL442); the brush totals stay in the panel's Brush section.
            if (ledger == null)
            {
                ledger = FindFirstObjectByType<Clube.Game.PlayerVolumeLedger>(FindObjectsInactive.Include);
            }
            if (ledger != null)
            {
                text += $"\nVolume     collected {ledger.Taken:0.0} · placed {ledger.Placed:0.0}";
            }
            // The game's time of day and day count (GL259), last.
            dayNight ??= GetComponent<Clube.Game.DayNightCycle>();
            if (dayNight != null)
            {
                text += $"\nGame time  {ClockText(dayNight.Hour)}  ·  Day {dayNight.Day}";
            }

            var content = new GUIContent(text);
            var rect = new Rect(Margin, Margin, Width, style.CalcHeight(content, Width));
            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, content, style);
            // The active quest goes under the box (GL216).
            Clube.Game.HudLayout.ReportTopLeft(rect.yMax);
        }

        /// <summary>An hour of the day, 0-24, as hh:mm.</summary>
        public static string ClockText(float hour)
        {
            int minutes = Mathf.FloorToInt(Mathf.Repeat(hour, 24f) * 60f);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }
    }
}
