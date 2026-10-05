using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The frame of an in-game lab panel (K31): a column down the right of the Game
    /// view with a dark background, scrolling when the window is too short for it,
    /// whose clicks never reach the click tools (<see cref="LabGuiBlocker"/>). Also the
    /// panels' shared styles and controls: foldout sections, labelled sliders, toggles
    /// and toolbars. Drawn with IMGUI, so it works in a build, where the custom
    /// inspectors don't exist.
    /// </summary>
    /// <remarks>
    /// Call <see cref="Begin"/> and <see cref="End"/> from the panel's OnGUI with the
    /// panel's controls in between, and <see cref="Hide"/> when it stops drawing.
    /// The column has a fixed width: text wraps inside it and buttons wrap or clip, so
    /// changing text can never resize the panel and make it jitter.
    /// <para>An Inspector frame (<see cref="ForInspector"/>) draws the same controls in a
    /// custom inspector, in the editor's colours, so every panel knob is also an Inspector
    /// knob (the Inspector is the superset).</para>
    /// </remarks>
    public sealed class LabPanelFrame
    {
        private const float Margin = 10f;

        // Keeps the bottom-right corner free for buttons (the demo's Exit, K34).
        private const float BottomReserve = 45f;

        private const int PaddingX = 12;
        private const float ScrollbarAllowance = 16f;

        private readonly Object owner;
        private readonly Color background;
        private readonly bool inInspector;

        private Rect panelRect;
        private Vector2 scroll;
        private float contentHeight = float.MaxValue;
        private float x;

        private GUIStyle padding;

        /// <param name="owner">The panel component, which owns the click-blocking area.</param>
        public LabPanelFrame(Object owner, float width, Color background)
        {
            this.owner = owner;
            Width = width;
            this.background = background;
        }

        private LabPanelFrame(float width)
        {
            Width = width;
            inInspector = true;
        }

        /// <summary>A frame for drawing panel controls inside a custom inspector; set <see cref="Width"/> to the view's width each time.</summary>
        public static LabPanelFrame ForInspector(float width)
        {
            return new LabPanelFrame(width);
        }

        /// <summary>True when drawing in an Inspector, where edits outside Play mode must be recorded for saving.</summary>
        public bool InInspector => inInspector;

        public float Width { get; set; }

        /// <summary>
        /// Width left for controls inside the padding, also allowing for the scrollbar,
        /// so a row sized to it fits whether or not the panel is scrolling.
        /// </summary>
        public float InnerWidth => inInspector ? Width : Width - 2f * PaddingX - ScrollbarAllowance;

        public GUIStyle Header { get; private set; }

        public GUIStyle Label { get; private set; }

        public GUIStyle Hint { get; private set; }

        public GUIStyle Button { get; private set; }

        public GUIStyle Toggle { get; private set; }

        /// <summary>A label that never wraps and clips instead, for text that changes as it plays.</summary>
        public GUIStyle FixedLine { get; private set; }

        public GUIStyle SectionButton { get; private set; }

        /// <summary>Starts the panel; draw its controls, then call <see cref="End"/>.</summary>
        public void Begin()
        {
            CreateStyles();
            if (inInspector)
            {
                GUILayout.BeginVertical(GUILayout.Width(Width));
                return;
            }

            x = Screen.width - Width - Margin;
            if (Event.current.type == EventType.Repaint && panelRect.width > 0f)
            {
                GuiDrawing.Rect(panelRect, background);
            }

            // Scrolls only when the window is too short for the contents, measured on the
            // last repaint; otherwise the view is exactly as tall as they are, with no scrollbar.
            float available = Screen.height - 2f * Margin - BottomReserve;
            GUILayout.BeginArea(new Rect(x, Margin, Width, available));
            scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Height(Mathf.Min(contentHeight, available)));
            GUILayout.BeginVertical(padding, GUILayout.Width(Width - ScrollbarAllowance));
        }

        public void End()
        {
            GUILayout.EndVertical();
            if (inInspector)
            {
                return;
            }
            if (Event.current.type == EventType.Repaint)
            {
                contentHeight = GUILayoutUtility.GetLastRect().height;
            }
            GUILayout.EndScrollView();

            // The background and click blocking use the size the layout came out at.
            if (Event.current.type == EventType.Repaint)
            {
                Rect laidOut = GUILayoutUtility.GetLastRect();
                panelRect = new Rect(x + laidOut.x, Margin + laidOut.y, Width, laidOut.height);
                LabGuiBlocker.SetArea(owner, panelRect);
            }
            GUILayout.EndArea();
        }

        /// <summary>Stops blocking clicks, for when the panel is hidden or disabled.</summary>
        public void Hide()
        {
            if (owner != null)
            {
                LabGuiBlocker.SetArea(owner, null);
            }
        }

        /// <summary>A section header that opens and closes its section; returns whether it's open.</summary>
        public bool Section(string title, ref bool open)
        {
            GUILayout.Space(6f);
            if (GUILayout.Button((open ? "▼ " : "► ") + title, SectionButton))
            {
                open = !open;
            }
            return open;
        }

        /// <summary>"Label 0.50" over a slider; returns the new value.</summary>
        public float Slider(string label, float value, float min, float max, string format = "0.00")
        {
            GUILayout.Label($"{label} {value.ToString(format)}", Label);
            return GUILayout.HorizontalSlider(value, min, max);
        }

        /// <summary>
        /// One line of text at the full inner width that never wraps (it clips), so text
        /// that changes every frame can't change the panel's size.
        /// </summary>
        public void Line(string text)
        {
            GUILayout.Label(text, FixedLine, GUILayout.Width(InnerWidth));
        }

        /// <summary>
        /// A row of equal-width buttons spanning the inner width, so a button whose label
        /// changes (Play / Pause) doesn't shift the others. Returns the clicked index, or -1.
        /// </summary>
        public int ButtonRow(params string[] labels)
        {
            const float spacing = 4f;
            float buttonWidth = (InnerWidth - spacing * (labels.Length - 1)) / labels.Length;
            int clicked = -1;
            GUILayout.BeginHorizontal(GUILayout.Width(InnerWidth));
            for (int i = 0; i < labels.Length; i++)
            {
                if (GUILayout.Button(labels[i], Button, GUILayout.Width(buttonWidth)))
                {
                    clicked = i;
                }
            }
            GUILayout.EndHorizontal();
            return clicked;
        }

        public bool ToggleField(string label, bool value)
        {
            return GUILayout.Toggle(value, " " + label, Toggle);
        }

        /// <summary>A label and a row of buttons, one selected; returns the selected index.</summary>
        public int Toolbar(string label, int selected, string[] names)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Label, GUILayout.ExpandWidth(false));
            int chosen = GUILayout.Toolbar(selected, names, Button, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            return chosen;
        }

        private GUIStyle Text(GUIStyle baseStyle, int fontSize, FontStyle fontStyle = FontStyle.Normal)
        {
            var style = new GUIStyle(baseStyle) { fontSize = fontSize, fontStyle = fontStyle };
            if (!inInspector)
            {
                style.normal.textColor = Color.white;
            }
            return style;
        }

        private void CreateStyles()
        {
            if (padding != null)
            {
                return;
            }

            padding = new GUIStyle { padding = new RectOffset(PaddingX, PaddingX, 10, 10) };

            // The game panel has white text on its dark background; the Inspector keeps the
            // editor's own colours, so it reads in the light theme too.
            Header = Text(GUI.skin.label, 14, FontStyle.Bold);
            Label = Text(GUI.skin.label, 13);
            Label.wordWrap = true;
            FixedLine = new GUIStyle(Label) { wordWrap = false, clipping = TextClipping.Clip };
            Hint = new GUIStyle(Label) { fontSize = 12, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };

            // Long button and toggle labels wrap rather than widen the fixed-width column.
            Button = new GUIStyle(GUI.skin.button) { fontSize = 13, wordWrap = true };
            Toggle = Text(GUI.skin.toggle, 13);
            Toggle.wordWrap = true;
            if (!inInspector)
            {
                Toggle.onNormal.textColor = Color.white;
            }
            SectionButton = Text(GUI.skin.label, 14, FontStyle.Bold);
            SectionButton.hover.textColor = new Color(1f, 0.85f, 0.5f);
        }
    }
}
