using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A claimed boulder's screen (prototype, backlog 2026-11-24 item 9), laid out like a station's (backlog 2026-11-24b
    /// item 7): E at the boulder opens it. On the left its "Recipes", refining material and knapping a hand axe from a
    /// stone; on the right the input slot, the work button and the output slots, and beside them the player's items that
    /// the chosen recipe can use (a click loads one into the input slot).
    /// <para>
    /// Refining: the mix stays in the input slot and gets purer with each pass (<see cref="BoulderBashing.RefineIn"/>), and
    /// each material picked out of it builds up in an output slot of its own, a new slot showing as each new material
    /// comes out. Take All moves every output to the player's items. A new mix can't be loaded until the outputs are
    /// taken. The hand axe works the same way, a stone in, a hand axe out (<see cref="BoulderBashing.KnapIn"/>).
    /// Whatever is left in the slots goes back to the player when the screen closes. Right clicking the boulder with a
    /// stone in hand still works without the screen.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(BoulderBashing))]
    public class BoulderPanel : MonoBehaviour, IGuiDrawer
    {
        private const float Padding = 12f;
        private const float Width = 760f;
        private const float ListWidth = 900f;
        private const float RowHeight = 34f;
        private const float CloseDistance = 5f;

        // More than one mix ever gives: every material in the registry could come out of it.
        private const int OutputCapacity = 24;

        private static readonly string[] RecipeNames = { "Refine material", "Hand axe (from Stone)" };

        private readonly Inventory input = new Inventory(1);
        private readonly Inventory output = new Inventory(OutputCapacity);
        private readonly InventoryListGui itemList = new InventoryListGui();
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
        private GUIStyle noteStyle;

        /// <summary>True while the screen is open.</summary>
        public bool IsOpen { get; private set; }

        private bool Refines => chosen == 0;

        private void Awake()
        {
            bashing = GetComponent<BoulderBashing>();
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            stations = GetComponent<StationPanel>();
            bashing.Message += text => lastResult = IsOpen ? text : lastResult;
            itemList.Include = slot => inventory != null && inventory.Inventory != null && Usable(inventory.Inventory[slot]);
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

        /// <summary>Closes the screen; what is still in its input and output slots goes back to the player's items.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            ReturnAll(input);
            ReturnAll(output);
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

        // Whether the chosen recipe works on a stack.
        private bool Usable(ItemStack stack)
        {
            return !stack.IsEmpty && (Refines ? bashing.CanRefine(stack) : bashing.CanKnap(stack));
        }

        private bool HasOutputs()
        {
            for (int slot = 0; slot < output.SlotCount; slot++)
            {
                if (!output[slot].IsEmpty)
                {
                    return true;
                }
            }
            return false;
        }

        // Every stack of a row back to the player's items; what doesn't fit drops in front of the player, never lost.
        private void ReturnAll(Inventory from)
        {
            if (inventory == null || inventory.Inventory == null)
            {
                return;
            }
            for (int slot = 0; slot < from.SlotCount; slot++)
            {
                if (from[slot].IsEmpty)
                {
                    continue;
                }
                from.MoveTo(slot, inventory.Inventory, keepApart: true);
                ItemStack left = from[slot];
                if (!left.IsEmpty)
                {
                    inventory.NotifyFull(left.Item);
                    if (inventory.DropAhead(left))
                    {
                        from.Set(slot, ItemStack.Empty);
                    }
                }
            }
        }

        // A click on one of the player's stacks: into the input slot, when it is free and the outputs are taken.
        private void Load(int slot, int count)
        {
            Inventory items = inventory.Inventory;
            ItemStack stack = items[slot];
            if (!Usable(stack))
            {
                return;
            }
            if (!input[0].IsEmpty)
            {
                lastResult = $"Take the {input[0].Item.DisplayName.ToLowerInvariant()} out of the input slot first (click it)";
                return;
            }
            if (HasOutputs())
            {
                lastResult = "Take All the outputs before starting on something new";
                return;
            }
            items.MoveToSlot(slot, input, 0, Mathf.Max(1, count));
            lastResult = null;
        }

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            GameUi.Apply();
            if (!IsOpen || inventory == null || inventory.Inventory == null)
            {
                return;
            }
            noteStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            bool listed = inventory.UsesList;
            float width = listed ? Width + ListWidth + Padding : Width;
            float height = listed ? Mathf.Max(420f, Mathf.Min(Screen.height - 280f, 900f)) : Mathf.Min(Screen.height - 160f, 600f);
            var panel = new Rect((Screen.width - width) * 0.5f, Mathf.Max(0f, (Screen.height - height) * 0.5f), width, height);
            InventoryGridGui.SolidPanel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            float inner = Width - Padding * 2f;
            GUI.Label(new Rect(x, y, inner, 28f), "Boulder   (E to close)");
            y += 34f;

            // The recipes on the left, the work on the right, as at a station.
            float left = 240f;
            GUI.Label(new Rect(x, y, left, 26f), "Recipes");
            for (int i = 0; i < RecipeNames.Length; i++)
            {
                string label = i == chosen ? $"> {RecipeNames[i]}" : RecipeNames[i];
                if (GameUi.Button(new Rect(x, y + 30f + i * (RowHeight + 4f), left, RowHeight), label) && i != chosen)
                {
                    chosen = i;
                    lastResult = null;
                    // What the other recipe can't use goes back to the player.
                    if (!input[0].IsEmpty && !Usable(input[0]))
                    {
                        ReturnAll(input);
                    }
                }
            }

            var work = new Rect(x + left + 16f, y, inner - left - 16f, panel.yMax - Padding - y - 30f);
            float workBottom = DrawWork(work);
            if (!listed)
            {
                DrawUsable(new Rect(work.x, workBottom + 8f, work.width, work.yMax - workBottom - 8f));
            }
            if (lastResult != null)
            {
                GUI.Label(new Rect(x, panel.yMax - Padding - 26f, inner, 26f), lastResult);
            }
            if (listed)
            {
                DrawItemList(panel);
            }
            else
            {
                InventoryGridGui.DrawTooltip();
            }
        }

        // The input slot, the button that does the work, and the output slots under them with Take All. Returns the bottom.
        private float DrawWork(Rect area)
        {
            Inventory items = inventory.Inventory;
            float slot = InventoryGridGui.SlotSize;
            float gap = InventoryGridGui.Gap;
            float x = area.x;
            float y = area.y;
            GUI.Label(new Rect(x, y, area.width, 26f), Refines
                ? "Pick the impurities out of a mixed stack, a pass at a time"
                : "Knock one stone into a hand axe");
            y += 30f;

            GUI.Label(new Rect(x, y, slot, 22f), "Input");
            y += 24f;
            // A click on the input slot takes the stack back.
            if (InventoryGridGui.Slot(new Vector2(x, y), input[0], false, null, null) && !input[0].IsEmpty)
            {
                ReturnAll(input);
            }
            string doing = Refines ? "Refine" : "Make";
            var button = new Rect(x + slot + 12f, y + (slot - RowHeight) * 0.5f, 160f, RowHeight);
            GUI.enabled = Usable(input[0]);
            if (GameUi.Button(button, doing))
            {
                if (Refines)
                {
                    bashing.RefineIn(input, 0, output);
                }
                else
                {
                    bashing.KnapIn(input, 0, output);
                }
            }
            GUI.enabled = true;
            if (!input[0].IsEmpty && Refines)
            {
                GUI.Label(new Rect(button.xMax + 12f, button.y, area.xMax - button.xMax - 12f, RowHeight),
                    $"{bashing.QualityOf(input[0])} now");
            }
            else if (input[0].IsEmpty)
            {
                GUI.Label(new Rect(button.xMax + 12f, button.y, area.xMax - button.xMax - 12f, RowHeight),
                    HasOutputs() ? "Take the outputs to start on something new" : "Click one of your items to load it");
            }
            y += slot + 12f;

            // The outputs: one slot per material picked out, wrapping into rows; a click takes one stack.
            GUI.Label(new Rect(x, y, 160f, 22f), "Output");
            bool any = HasOutputs();
            GUI.enabled = any;
            if (GameUi.Button(new Rect(area.xMax - 120f, y - 4f, 120f, 28f), "Take All"))
            {
                ReturnAll(output);
            }
            GUI.enabled = true;
            y += 28f;
            int across = Mathf.Max(1, Mathf.FloorToInt((area.width + gap) / (slot + gap)));
            int shown = 0;
            for (int i = 0; i < output.SlotCount; i++)
            {
                if (output[i].IsEmpty)
                {
                    continue;
                }
                var at = new Vector2(x + (shown % across) * (slot + gap), y + (shown / across) * (slot + gap));
                if (InventoryGridGui.Slot(at, output[i], false, null, null))
                {
                    output.MoveTo(i, items, keepApart: true);
                }
                shown++;
            }
            if (!any)
            {
                GUI.Label(new Rect(x, y, area.width, slot), Refines ? "What's picked out of the mix builds up here" : "The hand axe comes out here", noteStyle);
                shown = 1;
            }
            return y + Mathf.CeilToInt(shown / (float)across) * (slot + gap);
        }

        // The list inventory's half of the screen: the player's items the chosen recipe can use; a click loads one.
        private void DrawItemList(Rect panel)
        {
            Inventory items = inventory.Inventory;
            float x = panel.xMax - Padding - ListWidth;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, ListWidth, 28f), Refines ? "Your mixed stacks: click to load (Shift half, Ctrl one)" : "Your stones: click to load (Shift half, Ctrl one)");
            y += 32f;
            itemList.Equipped = inventory.IsEquippedSlot;
            ListResult picked = itemList.Draw(new Rect(x, y, ListWidth, panel.yMax - Padding - y), items);
            if (picked.Kind == InventoryDragKind.Clicked || picked.Kind == InventoryDragKind.DroppedOutside)
            {
                Load(picked.Slot, picked.Count);
            }
            if (itemList.IsDragging)
            {
                itemList.DrawCarried(items);
            }
            else
            {
                InventoryGridGui.DrawTooltip();
            }
        }

        // Without the list inventory: the player's usable stacks as rows, each with a button that loads it.
        private void DrawUsable(Rect area)
        {
            Inventory items = inventory.Inventory;
            usable.Clear();
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                if (Usable(items[slot]))
                {
                    usable.Add(slot);
                }
            }
            GUI.Label(new Rect(area.x, area.y, area.width, 26f), "Your items");
            if (usable.Count == 0)
            {
                GUI.Label(new Rect(area.x, area.y + 30f, area.width, 26f), Refines ? "Nothing in your items to refine" : "You have no stone to knap");
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
                if (GameUi.Button(new Rect(content.width - button, rowY, button, RowHeight), "Load"))
                {
                    Load(slot, stack.Count);
                }
            }
            GUI.EndScrollView();
        }
    }
}
