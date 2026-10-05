using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Edits the Spline generator's height curve (K9) in the lab panel, where Unity's curve
    /// editor isn't available: a preview of the curve, then each key's input (noise, 0-1)
    /// and output (height, 0 = level - amplitude, 1 = level + amplitude) as sliders, and
    /// buttons to add or remove a key. Keys keep flat tangents, so a key with neighbours at
    /// the same height makes a plateau.
    /// </summary>
    public static class HeightCurveControls
    {
        private const float GraphHeight = 90f;
        private const int GraphSamples = 48;
        private const float MinKeyGap = 0.02f;
        private const float KeyLabelWidth = 40f;

        private static readonly Color GraphBackground = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color GraphLine = new Color(1f, 0.75f, 0.3f);
        private static readonly Color GraphKey = Color.white;

        /// <summary>Draws the controls; returns true when the curve was changed.</summary>
        public static bool Draw(LabPanelFrame frame, AnimationCurve curve)
        {
            Rect graph = GUILayoutUtility.GetRect(frame.InnerWidth, GraphHeight, GUILayout.Width(frame.InnerWidth));
            if (Event.current.type == EventType.Repaint)
            {
                DrawGraph(graph, curve);
            }

            Keyframe[] keys = curve.keys;
            bool changed = false;
            for (int i = 0; i < keys.Length; i++)
            {
                // The end keys stay at 0 and 1, so the whole noise range is always mapped.
                bool isEnd = i == 0 || i == keys.Length - 1;
                frame.Line($"Key {i}  in {keys[i].time:0.00} → height {keys[i].value:0.00}");
                GUILayout.BeginHorizontal(GUILayout.Width(frame.InnerWidth));
                GUILayout.Label("in", frame.Hint, GUILayout.Width(KeyLabelWidth * 0.5f));
                float time = isEnd
                    ? keys[i].time
                    : frame.SliderBar(keys[i].time, keys[i - 1].time + MinKeyGap, keys[i + 1].time - MinKeyGap);
                if (isEnd)
                {
                    GUILayout.Label(i == 0 ? "start" : "end", frame.Hint);
                }
                GUILayout.Label("out", frame.Hint, GUILayout.Width(KeyLabelWidth * 0.75f));
                float value = frame.SliderBar(keys[i].value, 0f, 1f);
                GUILayout.EndHorizontal();

                if (!Mathf.Approximately(time, keys[i].time) || !Mathf.Approximately(value, keys[i].value))
                {
                    keys[i] = new Keyframe(time, value);
                    changed = true;
                }
            }

            switch (frame.ButtonRow("Add key", keys.Length > 2 ? "Remove key" : "(2 keys minimum)"))
            {
                case 0:
                    keys = AddKey(keys);
                    changed = true;
                    break;
                case 1 when keys.Length > 2:
                    keys = RemoveKey(keys);
                    changed = true;
                    break;
            }

            if (changed)
            {
                curve.keys = keys;
            }
            return changed;
        }

        // A new key in the middle of the widest gap, on the curve, so the shape doesn't jump.
        private static Keyframe[] AddKey(Keyframe[] keys)
        {
            int widest = 0;
            for (int i = 1; i < keys.Length - 1; i++)
            {
                if (keys[i + 1].time - keys[i].time > keys[widest + 1].time - keys[widest].time)
                {
                    widest = i;
                }
            }

            float time = (keys[widest].time + keys[widest + 1].time) * 0.5f;
            float value = (keys[widest].value + keys[widest + 1].value) * 0.5f;
            var added = new Keyframe[keys.Length + 1];
            for (int i = 0, j = 0; i < added.Length; i++)
            {
                added[i] = i == widest + 1 ? new Keyframe(time, value) : keys[j++];
            }
            return added;
        }

        // Removes the last key before the end key.
        private static Keyframe[] RemoveKey(Keyframe[] keys)
        {
            var removed = new Keyframe[keys.Length - 1];
            for (int i = 0, j = 0; i < keys.Length; i++)
            {
                if (i != keys.Length - 2)
                {
                    removed[j++] = keys[i];
                }
            }
            return removed;
        }

        private static void DrawGraph(Rect rect, AnimationCurve curve)
        {
            GuiDrawing.Rect(rect, GraphBackground);
            Vector2 ToGui(float x, float y) => new Vector2(rect.x + x * rect.width, rect.yMax - Mathf.Clamp01(y) * rect.height);

            Vector2 previous = ToGui(0f, curve.Evaluate(0f));
            for (int i = 1; i <= GraphSamples; i++)
            {
                float x = i / (float)GraphSamples;
                Vector2 next = ToGui(x, curve.Evaluate(x));
                GuiDrawing.Line(previous, next, GraphLine, 2f);
                previous = next;
            }

            foreach (Keyframe key in curve.keys)
            {
                Vector2 point = ToGui(key.time, key.value);
                GuiDrawing.Rect(new Rect(point.x - 3f, point.y - 3f, 6f, 6f), GraphKey);
            }
        }
    }
}
