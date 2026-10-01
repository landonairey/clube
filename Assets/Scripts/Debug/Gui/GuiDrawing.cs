using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Small IMGUI drawing helpers for lab HUDs and overlays. They work in any
    /// GUI pass (runtime OnGUI or the editor's Scene view), in GUI coordinates.
    /// </summary>
    public static class GuiDrawing
    {
        /// <summary>Draws a straight line of the given pixel thickness.</summary>
        public static void Line(Vector2 from, Vector2 to, Color color, float thickness)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.5f)
            {
                return;
            }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;

            GUIUtility.RotateAroundPivot(angle, from);
            GUI.color = color;
            GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        /// <summary>Fills a rectangle with a flat colour.</summary>
        public static void Rect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Draws a label centred on a point.</summary>
        public static void CentredLabel(Vector2 centre, string text, GUIStyle style)
        {
            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);
            GUI.Label(new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y), content, style);
        }
    }
}
