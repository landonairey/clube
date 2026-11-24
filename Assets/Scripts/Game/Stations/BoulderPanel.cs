using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A claimed boulder's screen (prototype, backlog 2026-11-24 item 9): E at the boulder opens it. On the left its
    /// "Recipes", refining material and knapping a hand axe from a stone; on the right the player's stacks each one can
    /// use, with a button that does it once (<see cref="BoulderBashing.RefineAt"/>, <see cref="BoulderBashing.KnapAt"/>).
    /// Right clicking the boulder with a stone in hand still works without the screen.
    /// </summary>
    [RequireComponent(typeof(BoulderBashing))]
    public class BoulderPanel : MonoBehaviour, IGuiDrawer
    {
        private const float Padding = 12f;
        private const float Width = 760f;
        private const float RowHeight = 34f;
        private const float CloseDistance = 5f;

        private static readonly string[] RecipeNames = { "Refine material", "Hand axe (from Stone)" };

        private readonly List<int> usable = new List<int>();
        private BoulderBashing bashing;
        private PlayerInventory inventory;
        private PlayerController player;
        private StationPanel stations;
        private Vector3 boulder;
        private int openedFrame;
        private int chosen;
        private Vector2 scroll;
        private string lastResult;

        /// <summary>True while the screen is open.</summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            bashing = GetComponent<BoulderBashing>();
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            stations = GetComponent<StationPanel>();
            bashing.Message += text => lastResult = IsOpen ? text : lastResult;
        }

        private void OnEnable()
        {
            GuiHost.Add(this);
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
            Close();
        }

        /// <summary>Opens the screen for the boulder at this world position.</summary>
        public void Open(Vector3 at)
        {
            boulder = at;
            IsOpen = true;
            openedFrame = Time.frameCount;
            lastResult = null;
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            // The press that opened the screen mustn't close it in the same frame; E closes it as it does a station's.
            var close = stations != null ? stations.CloseAction : null;
            bool closePressed = Time.frameCount != openedFrame && close != null && close.action.WasPressedThisFrame();
            bool tooFar = (boulder - transform.position).sqrMagnitude > CloseDistance * CloseDistance;
            if (closePressed || tooFar || (player != null && !player.enabled))
            {
                Close();
            }
        }

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            GameUi.Apply();
            if (!IsOpen || inventory == null || inventory.Inventory == null)
            {
                return;
            }
            float height = Mathf.Min(Screen.height - 160f, 560f);
            var panel = new Rect((Screen.width - Width) * 0.5f, (Screen.height - height) * 0.5f, Width, height);
            InventoryGridGui.SolidPanel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            float inner = Width - Padding * 2f;
            GUI.Label(new Rect(x, y, inner, 28f), "Boulder   (E to close)");
            y += 34f;

            // The recipes on the left, what each can work on the right.
            float left = 240f;
            GUI.Label(new Rect(x, y, left, 26f), "Recipes");
            for (int i = 0; i < RecipeNames.Length; i++)
            {
                string label = i == chosen ? $"> {RecipeNames[i]}" : RecipeNames[i];
                if (GameUi.Button(new Rect(x, y + 30f + i * (RowHeight + 4f), left, RowHeight), label))
                {
                    chosen = i;
                    lastResult = null;
                }
            }

            var right = new Rect(x + left + 16f, y, inner - left - 16f, panel.yMax - Padding - y - 30f);
            DrawUsable(right);
            if (lastResult != null)
            {
                GUI.Label(new Rect(x, panel.yMax - Padding - 26f, inner, 26f), lastResult);
            }
        }

        private void DrawUsable(Rect area)
        {
            Inventory items = inventory.Inventory;
            bool refine = chosen == 0;
            usable.Clear();
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                ItemStack stack = items[slot];
                if (!stack.IsEmpty && (refine ? bashing.CanRefine(stack) : bashing.CanKnap(stack)))
                {
                    usable.Add(slot);
                }
            }
            GUI.Label(new Rect(area.x, area.y, area.width, 26f), refine
                ? "Pick the impurities out of a mixed stack, a pass at a time"
                : "Knock one stone into a hand axe");
            if (usable.Count == 0)
            {
                GUI.Label(new Rect(area.x, area.y + 30f, area.width, 26f), refine ? "Nothing in your items to refine" : "You have no stone to knap");
                return;
            }
            var view = new Rect(area.x, area.y + 30f, area.width, area.height - 30f);
            var content = new Rect(0f, 0f, area.width - 20f, usable.Count * (RowHeight + 4f));
            scroll = GUI.BeginScrollView(view, scroll, content);
            float button = 110f;
            for (int i = 0; i < usable.Count; i++)
            {
                int slot = usable[i];
                ItemStack stack = items[slot];
                float rowY = i * (RowHeight + 4f);
                string amount = stack.Count > 0 ? $"{stack.Count} x " : "";
                GUI.Label(new Rect(0f, rowY, content.width - button - 8f, RowHeight), $"{amount}{stack.Item.DisplayName} ({bashing.QualityOf(stack)})");
                if (GameUi.Button(new Rect(content.width - button, rowY, button, RowHeight), refine ? "Refine" : "Make"))
                {
                    if (refine)
                    {
                        bashing.RefineAt(slot);
                    }
                    else
                    {
                        bashing.KnapAt(slot);
                    }
                }
            }
            GUI.EndScrollView();
        }
    }
}
