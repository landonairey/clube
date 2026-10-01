using System;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Game-view HUD showing the world X, Y and Z axes as seen from this camera,
    /// like the orientation gizmo in the Scene view. The triad sits in a screen
    /// corner and turns as the camera turns. Axes pointing away from the viewer
    /// are dimmed and drawn behind the others.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class AxesHud : MonoBehaviour
    {
        private static readonly Vector3[] AxisDirections = { Vector3.right, Vector3.up, Vector3.forward };
        private static readonly string[] AxisLabels = { "X", "Y", "Z" };
        private static readonly Color[] AxisColors =
        {
            new Color(0.95f, 0.3f, 0.25f),
            new Color(0.45f, 0.9f, 0.3f),
            new Color(0.3f, 0.55f, 1f),
        };

        [Tooltip("Length of each axis in screen pixels.")]
        [SerializeField, Min(10f)]
        private float axisLength = 45f;

        [Tooltip("Distance of the triad's centre from the bottom-left corner, in pixels.")]
        [SerializeField]
        private Vector2 margin = new Vector2(70f, 70f);

        [SerializeField, Min(1f)]
        private float lineThickness = 3f;

        [Tooltip("Brightness of axes pointing away from the viewer.")]
        [SerializeField, Range(0f, 1f)]
        private float awayDimming = 0.45f;

        private GUIStyle labelStyle;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
            }

            var origin = new Vector2(margin.x, Screen.height - margin.y);

            // Each axis in camera space: x right, y up, z away from the viewer.
            var cameraDirections = new Vector3[AxisDirections.Length];
            var order = new int[AxisDirections.Length];
            for (int i = 0; i < AxisDirections.Length; i++)
            {
                cameraDirections[i] = transform.InverseTransformDirection(AxisDirections[i]);
                order[i] = i;
            }

            // Farthest first, so axes pointing towards the viewer draw on top.
            Array.Sort(order, (a, b) => cameraDirections[b].z.CompareTo(cameraDirections[a].z));

            foreach (int axis in order)
            {
                Vector3 direction = cameraDirections[axis];

                // GUI y grows downwards, camera y grows upwards.
                var screenDirection = new Vector2(direction.x, -direction.y);
                Color color = direction.z > 0f ? AxisColors[axis] * awayDimming : AxisColors[axis];
                color.a = 1f;

                GuiDrawing.Line(origin, origin + screenDirection * axisLength, color, lineThickness);

                Vector2 labelCentre = origin + screenDirection * (axisLength + 12f);
                labelStyle.normal.textColor = color;
                GUI.Label(new Rect(labelCentre.x - 10f, labelCentre.y - 10f, 20f, 20f), AxisLabels[axis], labelStyle);
            }
        }
    }
}
