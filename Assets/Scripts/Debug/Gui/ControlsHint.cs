using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// A line of help text at the bottom centre of the Game view, listing a lab's
    /// controls, for scenes whose controls are keys rather than an on-screen panel
    /// (VoxelLab in the demo exe, V22). Wraps in narrow windows and keeps the bottom
    /// corners free for buttons (the demo's Exit) and the axes HUD.
    /// </summary>
    public class ControlsHint : MonoBehaviour
    {
        private const float Margin = 10f;
        private const float BottomCornerReserve = 150f;

        [Tooltip("The controls to list, e.g. \"← → step case · Right mouse look\".")]
        [SerializeField, TextArea]
        private string text = "";

        private GUIStyle style;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text))
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(10, 10, 6, 6),
                normal = { textColor = Color.white },
            };

            var content = new GUIContent(text);
            float width = Mathf.Min(style.CalcSize(content).x, Screen.width - 2f * BottomCornerReserve);
            float height = style.CalcHeight(content, width);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - height - Margin, width, height);
            GuiDrawing.Rect(rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(rect, content, style);
        }
    }
}
