using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The inventory screen (GL7, first pass of N2): Inventory (Tab) opens a grid of every
    /// slot, backpack above the hotbar row, and frees the cursor. Clicking a stack sends it to the
    /// first open hotbar slot, or from the hotbar to the backpack (GL71); pressing and dragging
    /// drops it on a chosen slot (<see cref="Inventory.Move(int, int)"/>: the same item stacks, the rest
    /// staying behind; anything else swaps). Shift drags half a stack and Ctrl one, and a right click splits a
    /// stack in half (GL191, <see cref="InventoryDrag"/>). Dropped on the trash slot, a stack is gone for good (GL173);
    /// Sort tidies the backpack (GL174, <see cref="Inventory.Sort"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class InventoryScreen : MonoBehaviour
    {
        private const float Padding = 12f;

        [Tooltip("Opens and closes the screen (Tab).")]
        [SerializeField]
        private InputActionReference toggleAction;

        private PlayerInventory inventory;
        private PlayerController player;
        private StationPanel stationPanel;
        private MerchantPanel merchantPanel;
        private JournalScreen journalScreen;
        private CraftingJournalScreen craftingJournal;
        private readonly InventoryDrag drag = new InventoryDrag();
        private Rect trashSlot;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            stationPanel = GetComponent<StationPanel>();
            merchantPanel = GetComponent<MerchantPanel>();
            journalScreen = GetComponent<JournalScreen>();
            craftingJournal = GetComponent<CraftingJournalScreen>();
        }

        private void OnEnable()
        {
            toggleAction?.action.Enable();
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                SetOpen(false);
            }
        }

        private void Update()
        {
            if (player != null && !player.enabled)
            {
                return;
            }

            // One screen at a time: not over a station's or a merchant's panel.
            bool otherOpen = (stationPanel != null && stationPanel.IsOpen) || (merchantPanel != null && merchantPanel.IsOpen)
                || (journalScreen != null && journalScreen.IsOpen)
                || (craftingJournal != null && craftingJournal.IsOpen);
            if (toggleAction != null && toggleAction.action.WasPressedThisFrame() && !otherOpen)
            {
                SetOpen(!IsOpen);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            drag.Reset();
            if (player != null)
            {
                player.IsInMenu = open;
            }
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            Inventory items = inventory.Inventory;
            float width = InventoryGridGui.RowWidth + Padding * 2f;
            float height = Padding * 2f + 24f + InventoryGridGui.PlayerHeight(inventory) + InventoryGridGui.RowGap + InventoryGridGui.SlotSize;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            string title = inventory.UsesToolSlot
                ? "Inventory (Tab to close): tool slot, then items; click a tool to equip it, drag to place"
                : "Inventory (Tab to close): click to send to the hotbar or back, drag to place (Shift half, Ctrl one), right-click to split";
            GUI.Label(new Rect(panel.x + Padding, panel.y + Padding, width - Padding * 2f, 20f), title);

            var gridTopLeft = new Vector2(panel.x + Padding, panel.y + Padding + 24f);
            float bottom = gridTopLeft.y + InventoryGridGui.PlayerHeight(inventory) + InventoryGridGui.RowGap;
            trashSlot = new Rect(panel.xMax - Padding - InventoryGridGui.SlotSize, bottom, InventoryGridGui.SlotSize, InventoryGridGui.SlotSize);
            DrawSortAndTrash(items, new Rect(gridTopLeft.x, bottom + (InventoryGridGui.SlotSize - 28f) * 0.5f, 120f, 28f));
            HandleMouse(items, gridTopLeft);
            InventoryGridGui.DrawPlayer(gridTopLeft, inventory, drag.Pressed);
            if (drag.IsDragging)
            {
                // The stack follows the mouse while it's carried.
                drag.DrawCarried(items);
            }
            else
            {
                InventoryGridGui.DrawTooltip();
            }
        }

        // Sort (GL174) tidies the backpack, or every item slot past the tool slot; the hotbar stays as the player
        // set it. The trash slot (GL173) destroys what's dropped on it.
        private void DrawSortAndTrash(Inventory items, Rect sortButton)
        {
            int sortFrom = inventory.UsesToolSlot ? PlayerInventory.ToolSlot + 1 : inventory.HotbarSlots;
            if (GUI.Button(sortButton, inventory.UsesToolSlot ? "Sort items" : "Sort backpack"))
            {
                items.Sort(sortFrom, items.SlotCount);
            }
            bool over = drag.IsDragging && trashSlot.Contains(Event.current.mousePosition);
            ItemSlotGui.Draw(trashSlot, ItemStack.Empty, over);
            var centred = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(trashSlot, "Trash", centred);
            var right = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(trashSlot.x - 228f, trashSlot.y, 220f, trashSlot.height), "Drop a stack here to destroy it", right);
        }

        // GL71, GL191: InventoryDrag moves, splits and carries stacks between slots; here a click sends a stack
        // across (backpack to hotbar, hotbar to backpack, or into the tool slot), and a drop on the trash destroys it.
        private void HandleMouse(Inventory items, Vector2 gridTopLeft)
        {
            InventoryDragResult result = drag.Handle(inventory, gridTopLeft);
            switch (result.Kind)
            {
                case InventoryDragKind.DroppedOutside:
                    if (trashSlot.Contains(result.Position))
                    {
                        // Gone for good (GL173): the part dragged, or the whole stack.
                        if (result.Count >= items[result.Slot].Count)
                        {
                            items.TakeAll(result.Slot);
                        }
                        else
                        {
                            items.RemoveAt(result.Slot, result.Count);
                        }
                    }
                    break;
                case InventoryDragKind.Clicked:
                    int pressed = result.Slot;
                    if (inventory.UsesToolSlot)
                    {
                        // The tool-slot inventory (GL99): a click equips a tool, or puts the equipped one away.
                        if (pressed == PlayerInventory.ToolSlot)
                        {
                            items.MoveInto(pressed, 1, items.SlotCount);
                        }
                        else if (items[pressed].Item is ToolDefinition)
                        {
                            items.Move(pressed, PlayerInventory.ToolSlot);
                        }
                        break;
                    }
                    int hotbar = inventory.HotbarSlots;
                    if (pressed < hotbar)
                    {
                        items.MoveInto(pressed, hotbar, items.SlotCount);
                    }
                    else
                    {
                        items.MoveInto(pressed, 0, hotbar);
                    }
                    break;
            }
        }
    }
}
