using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// A cheat sheet of the keys (GL29): a small list in the lower left of the screen while the
    /// player is in control. Each row names an action in the input asset and shows its
    /// keyboard-and-mouse binding as the Input System spells it, so the sheet follows the
    /// bindings; a row can give its own key text where the binding is unwieldy (nine hotbar
    /// keys as "1-9"), and a scene can add plain lines for keys outside the player's actions
    /// (the labs' P for the fly camera). Help (F1) hides and shows it.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class KeybindSheet : MonoBehaviour
    {
        private const string BindingGroup = "Keyboard&Mouse";

        /// <summary>One row: what it does, and the action (or the text) that says which key.</summary>
        [Serializable]
        public struct Row
        {
            [Tooltip("What the key does.")]
            public string Label;

            [Tooltip("The action in the input asset whose binding is shown.")]
            public string Action;

            [Tooltip("Shown instead of the action's binding when set.")]
            public string Keys;

            public Row(string label, string action, string keys = null)
            {
                Label = label;
                Action = action;
                Keys = keys;
            }
        }

        [Tooltip("The input asset the rows' actions are in.")]
        [SerializeField]
        private InputActionAsset actions;

        [Tooltip("Hides and shows the sheet (F1).")]
        [SerializeField]
        private InputActionReference helpAction;

        [SerializeField]
        private List<Row> rows = DefaultRows();

        [Tooltip("Extra lines for keys outside the player's actions, as \"key  what it does\" (e.g. the labs' P).")]
        [SerializeField]
        private List<string> extraLines = new List<string>();

        [Tooltip("Whether the sheet shows when the player starts.")]
        [SerializeField]
        private bool shown = true;

        private readonly List<(string Keys, string Label)> lines = new List<(string, string)>();
        private PlayerController player;
        private GUIStyle keyStyle;
        private GUIStyle labelStyle;
        private bool linesBuilt;

        /// <summary>The sheet's rows as shown: the key text and what it does.</summary>
        public IReadOnlyList<(string Keys, string Label)> Lines
        {
            get
            {
                BuildLines();
                return lines;
            }
        }

        public bool Shown
        {
            get => shown;
            set => shown = value;
        }

        /// <summary>The rows a new sheet starts with: every key the player has.</summary>
        public static List<Row> DefaultRows()
        {
            return new List<Row>
            {
                new Row("Move", "Move", "WASD"),
                new Row("Jump", "Jump"),
                new Row("Sprint", "Sprint"),
                new Row("Use tool", "Attack", "LMB"),
                new Row("Add ground", "Place"),
                new Row("Brush size", "Previous", "[ ]"),
                new Row("Use station", "Interact"),
                new Row("Hotbar slot", "Hotbar", "1-9"),
                new Row("Next slot", "NextSlot"),
                new Row("Inventory", "Inventory"),
                new Row("Drop (hold to pour)", "Drop"),
                new Row("Place (hold to pick up)", "Build"),
                new Row("Rotate placement", "Rotate"),
                new Row("Free cursor", "FreeCursor"),
                new Row("Hide this list", "Help"),
            };
        }

        /// <summary>Puts the rows back to <see cref="DefaultRows"/>.</summary>
        public void ResetRows()
        {
            rows = DefaultRows();
            linesBuilt = false;
        }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            helpAction?.action.Enable();
        }

        private void Update()
        {
            if (helpAction != null && helpAction.action.WasPressedThisFrame() && player.enabled && !player.IsInMenu)
            {
                shown = !shown;
            }
        }

        // The key text for every row, worked out once (bindings don't change while playing).
        private void BuildLines()
        {
            if (linesBuilt)
            {
                return;
            }
            linesBuilt = true;
            lines.Clear();
            foreach (Row row in rows)
            {
                string keys = row.Keys;
                if (string.IsNullOrEmpty(keys))
                {
                    InputAction action = actions != null && !string.IsNullOrEmpty(row.Action) ? actions.FindAction(row.Action) : null;
                    keys = action != null ? action.GetBindingDisplayString(InputBinding.MaskByGroup(BindingGroup)) : "?";
                }
                lines.Add((keys, row.Label));
            }
            foreach (string line in extraLines)
            {
                int gap = line.IndexOf("  ", StringComparison.Ordinal);
                lines.Add(gap > 0 ? (line.Substring(0, gap), line.Substring(gap).Trim()) : (string.Empty, line));
            }
        }

        private void OnGUI()
        {
            if (!player.enabled || player.IsCursorFree)
            {
                return;
            }
            keyStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12, alignment = TextAnchor.MiddleRight };
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12 };
            if (!shown)
            {
                string help = helpAction != null ? helpAction.action.GetBindingDisplayString(InputBinding.MaskByGroup(BindingGroup)) : "F1";
                GUI.Label(new Rect(12f, Screen.height - 150f, 200f, 18f), $"{help}  Keys", labelStyle);
                return;
            }

            BuildLines();
            const float rowHeight = 17f;
            const float keyWidth = 96f;
            const float labelWidth = 150f;
            // Above the axes gizmo in the corner.
            float height = lines.Count * rowHeight + 10f;
            var box = new Rect(8f, Screen.height - 130f - height, keyWidth + labelWidth + 16f, height);
            GUI.Box(box, GUIContent.none);
            float y = box.y + 5f;
            foreach ((string keys, string label) in lines)
            {
                GUI.Label(new Rect(box.x + 4f, y, keyWidth, rowHeight), keys, keyStyle);
                GUI.Label(new Rect(box.x + keyWidth + 12f, y, labelWidth, rowHeight), label, labelStyle);
                y += rowHeight;
            }
        }
    }
}
