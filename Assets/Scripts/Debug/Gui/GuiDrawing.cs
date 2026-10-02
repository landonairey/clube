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

        /// <summary>Projects a world point into GUI coordinates; false when it is behind the camera.</summary>
        public static bool WorldToGui(Camera camera, Vector3 world, out Vector2 gui)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);

            // Screen y grows upwards, GUI y grows downwards.
            gui = new Vector2(screen.x, Screen.height - screen.y);
            return screen.z > 0f;
        }

        /// <summary>
        /// Draws a label on a dark backing just outside a round marker, pushed away from
        /// <paramref name="localCentre"/> on screen so it never sits on top of the marker.
        /// Points are local to <paramref name="space"/>; the radius is in its units.
        /// </summary>
        public static void LabelBeside(
            Camera camera, Transform space, Vector3 localPoint, Vector3 localCentre, float markerRadius,
            string text, Color color, GUIStyle style)
        {
            const float gap = 4f;
            Vector3 world = space.TransformPoint(localPoint);
            if (!WorldToGui(camera, world, out Vector2 point) ||
                !WorldToGui(camera, space.TransformPoint(localCentre), out Vector2 centre) ||
                !WorldToGui(camera, world + camera.transform.up * (markerRadius * space.lossyScale.y), out Vector2 rim))
            {
                return;
            }

            Vector2 outwards = point - centre;
            outwards = outwards.sqrMagnitude > 1f ? outwards.normalized : Vector2.up;

            var content = new GUIContent(text);
            Vector2 textSize = style.CalcSize(content);

            // Far enough out that the label's box clears the marker in that direction.
            float radius = Vector2.Distance(point, rim);
            float halfExtent = Mathf.Abs(outwards.x) * textSize.x * 0.5f + Mathf.Abs(outwards.y) * textSize.y * 0.5f;
            Vector2 labelCentre = point + outwards * (radius + gap + halfExtent);

            var rect = new Rect(labelCentre - textSize * 0.5f, textSize);
            Rect(new Rect(rect.x - 3f, rect.y - 1f, rect.width + 6f, rect.height + 2f), new Color(0f, 0f, 0f, 0.6f));

            Color previous = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(rect, content, style);
            style.normal.textColor = previous;
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
