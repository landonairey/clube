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
    public class InventoryScreen : MonoBehaviour, IGuiDrawer
    {
        private const float Padding = 12f;

        [Tooltip("Opens and closes the screen (the Inventory action: E also opens it, GL298; Tab no longer does, GL364).")]
        [SerializeField]
        private InputActionReference toggleAction;

        [Tooltip("Interact (E) also opens and closes it when nothing usable is aimed at (GL298), so one key does both.")]
        [SerializeField]
        private InputActionReference interactAction;

        private PlayerInventory inventory;
        private PlayerController player;
        private StationPanel stationPanel;
        private MerchantPanel merchantPanel;
        private ChestPanel chestPanel;
        private FieldLogScreen fieldLog;
        private PlayerInteractor interactor;
        private RestScreen rest;
        private WorldMapScreen map;
        private readonly InventoryDrag drag = new InventoryDrag();
        private readonly InventoryListGui list = new InventoryListGui();
        private EquipmentPanelGui equipment;
        private Hotbar hotbar;
        private Rect trashSlot;

        public bool IsOpen { get; private set; }

        /// <summary>While true the screen cannot be opened (the tutorial's name step, GL539).</summary>
        public bool Blocked { get; set; }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            stationPanel = GetComponent<StationPanel>();
            merchantPanel = GetComponent<MerchantPanel>();
            chestPanel = GetComponent<ChestPanel>();
            fieldLog = GetComponent<FieldLogScreen>();
            interactor = GetComponent<PlayerInteractor>();
            rest = GetComponent<RestScreen>();
            map = GetComponent<WorldMapScreen>();
            hotbar = GetComponent<Hotbar>();
        }

        private void OnEnable()
        {
            GuiHost.Add(this);
            toggleAction?.action.Enable();
            interactAction?.action.Enable();
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
            if (IsOpen)
            {
                SetOpen(false);
            }
        }

        private void Update()
        {
            if (Blocked)
            {
                return;
            }
            if (player != null && !player.enabled)
            {
                return;
            }

            // E in the field log, or the log's key (Q) here, switches between the two (GL384).
            bool interactPressed = interactAction != null && interactAction.action.WasPressedThisFrame();
            if (fieldLog != null && fieldLog.IsOpen && interactPressed)
            {
                fieldLog.Close();
                SetOpen(true);
                return;
            }
            // E in the map closes it and opens the inventory (GL844).
            if (map != null && map.IsOpen && interactPressed)
            {
                map.Close();
                SetOpen(true);
                return;
            }
            // M in the inventory closes it and opens the map (GL861). The map opens this frame, so its own M check
            // (opened this frame, or a screen closed this frame) can't toggle it back.
            if (map != null && map.ToggleAction != null && map.ToggleAction.WasPressedThisFrame()
                && InventoryKeys.MapReplacesInventory(IsOpen, map.CanOpen))
            {
                SetOpen(false);
                map.Open();
                return;
            }
            if (IsOpen && fieldLog != null && !fieldLog.Blocked && fieldLog.ToggleAction != null && fieldLog.ToggleAction.WasPressedThisFrame())
            {
                SetOpen(false);
                fieldLog.Open(fieldLog.Current);
                return;
            }

            // One screen at a time: not over a station's or a merchant's panel.
            bool otherOpen =(stationPanel != null && stationPanel.IsOpen) || (merchantPanel != null && merchantPanel.IsOpen)
                || (fieldLog != null && fieldLog.IsOpen) || (chestPanel != null && chestPanel.IsOpen) || (rest != null && rest.IsOpen)
                // Any other screen that holds the player in a menu, like an open clam (GL852).
                || (!IsOpen && player != null && player.IsInMenu);
            if (toggleAction != null && toggleAction.action.WasPressedThisFrame() && !otherOpen)
            {
                SetOpen(!IsOpen);
            }
            else if (interactAction != null && interactAction.action.WasPressedThisFrame())
            {
                // E closes it, or opens it when nothing usable is aimed at (GL298), no other screen is open and it isn't
                // the E that just closed another screen (GL852).
                bool aimingAtUsable = interactor != null && interactor.Target != null && !PlayerInteractor.IsNotice(interactor.Target);
                InventoryKeys.Result result = InventoryKeys.OnInteract(IsOpen, otherOpen, player != null && player.MenuClosedThisFrame, aimingAtUsable);
                if (result != InventoryKeys.Result.Nothing)
                {
                    SetOpen(result == InventoryKeys.Result.Open);
                }
            }
        }

        /// <summary>Opens the screen, as E does with nothing aimed at.</summary>
        public void Open()
        {
            if (!IsOpen)
            {
                SetOpen(true);
            }
        }

        /// <summary>Closes the screen (Esc, GL204).</summary>
        public void Close()
        {
            if (IsOpen)
            {
                SetOpen(false);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            drag.Reset();
            list.Reset();
            if (!open)
            {
                equipment?.Hide();
            }
            if (player != null)
            {
                player.IsInMenu = open;
            }
        }

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            GameUi.Apply();
            if (!IsOpen)
            {
                return;
            }
            if (inventory.UsesList && hotbar != null)
            {
                DrawListScreen();
                return;
            }

            Inventory items = inventory.Inventory;
            float width = InventoryGridGui.RowWidth + Padding * 2f;
            float height = Padding * 2f + 32f + InventoryGridGui.PlayerHeight(inventory) + InventoryGridGui.RowGap + InventoryGridGui.SlotSize;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            string title = inventory.UsesToolSlot
                ? "Inventory (E to close): tool slot, then items; click a tool to equip it, drag to place"
                : "Inventory (E to close): click to send to the hotbar or back, drag to place (Shift half, Ctrl one), right-click to split";
            GUI.Label(new Rect(panel.x + Padding, panel.y + Padding, width - Padding * 2f, 28f), title);

            var gridTopLeft = new Vector2(panel.x + Padding, panel.y + Padding + 32f);
            float bottom = gridTopLeft.y + InventoryGridGui.PlayerHeight(inventory) + InventoryGridGui.RowGap;
            trashSlot = new Rect(panel.xMax - Padding - InventoryGridGui.SlotSize, bottom, InventoryGridGui.SlotSize, InventoryGridGui.SlotSize);
            DrawSortAndTrash(items, new Rect(gridTopLeft.x, bottom + (InventoryGridGui.SlotSize - 36f) * 0.5f, 190f, 36f));
            HandleMouse(items, gridTopLeft, panel);
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

        private void OnDestroy()
        {
            equipment?.Destroy();
        }

        // The list inventory's screen (prototype): equipment on the left, every item in a sortable list in the middle, and
        // what the clicked item is on the right. A right click on an item puts it in the right hand; drags move items between
        // the list and the hands, onto the trash, or off the screen to the ground.
        private void DrawListScreen()
        {
            Inventory items = inventory.Inventory;
            float width = Mathf.Min(Screen.width - 40f, 2200f);
            float height = Mathf.Clamp(Screen.height - 280f, EquipmentPanelGui.PreferredHeight + Padding * 2f, 1150f);
            var panel = new Rect((Screen.width - width) * 0.5f, Mathf.Max(10f, (Screen.height - height) * 0.5f - 60f), width, height);
            InventoryGridGui.SolidPanel(panel);

            float equipmentWidth = Mathf.Clamp(width * 0.34f, 560f, 740f), detailsWidth = Mathf.Clamp(width * 0.18f, 280f, 400f);
            var equipmentArea = new Rect(panel.x + Padding, panel.y + Padding, equipmentWidth, panel.height - Padding * 2f);
            float listX = equipmentArea.xMax + Padding;
            var detailsArea = new Rect(panel.xMax - Padding - detailsWidth, panel.y + Padding, detailsWidth, panel.height - Padding * 2f);
            // The details window shows only once an item is clicked (closed with its button, or a second click).
            bool showDetails = list.Selected != null;
            float listWidth = (showDetails ? detailsArea.x - Padding : panel.xMax - Padding) - listX;
            GUI.Label(new Rect(listX, panel.y + Padding, listWidth, 30f), "Inventory (E to close): click for details, right-click to hold in your right hand, drag to a hand, onto a stack to combine, or off the screen to drop");
            const float trashHeight = 56f;
            var listArea = new Rect(listX, panel.y + Padding + 36f, listWidth, panel.height - Padding * 2f - 36f - trashHeight - 8f);
            var trash = new Rect(listX, listArea.yMax + 8f, listWidth, trashHeight);

            equipment ??= new EquipmentPanelGui();
            list.Equipped = inventory.IsHandSlot;
            list.HoverTooltips = false;
            ListResult picked = list.Draw(listArea, items);
            InventoryDragResult fromHand = equipment.Draw(equipmentArea, inventory, hotbar);
            bool dragging = list.IsDragging || equipment.IsDragging;
            DrawTrashBar(trash, dragging);

            switch (picked.Kind)
            {
                case InventoryDragKind.RightClicked:
                    hotbar.EquipRight(picked.Slot);
                    break;
                case InventoryDragKind.DroppedOutside:
                    int hand = equipment.HandSlotAt(inventory, picked.Position);
                    if (hand >= 0)
                    {
                        items.Move(picked.Slot, hand, picked.Count);
                    }
                    else if (trash.Contains(picked.Position))
                    {
                        InventoryGridGui.Trash(items, picked.AsDrag, trash);
                    }
                    else if (!panel.Contains(picked.Position))
                    {
                        inventory.DropFromSlot(picked.Slot, picked.Count);
                    }
                    break;
            }
            if (fromHand.Kind == InventoryDragKind.DroppedOutside)
            {
                if (listArea.Contains(fromHand.Position))
                {
                    items.MoveInto(fromHand.Slot, 0, inventory.BagSlots);
                }
                else if (trash.Contains(fromHand.Position))
                {
                    InventoryGridGui.Trash(items, fromHand, trash);
                }
                else if (!panel.Contains(fromHand.Position))
                {
                    inventory.DropFromSlot(fromHand.Slot, fromHand.Count);
                }
            }

            if (showDetails)
            {
                DrawDetails(detailsArea, items);
            }
            if (list.IsDragging)
            {
                list.DrawCarried(items);
            }
            else if (!equipment.IsDragging)
            {
                InventoryGridGui.DrawTooltip();
            }
        }

        private static void DrawTrashBar(Rect trash, bool dragging)
        {
            bool over = dragging && trash.Contains(Event.current.mousePosition);
            ItemSlotGui.Fill(trash, over ? new Color(0.6f, 0.15f, 0.1f, 0.6f) : new Color(0f, 0f, 0f, 0.35f));
            ItemSlotGui.Outline(trash, new Color(1f, 1f, 1f, over ? 0.6f : 0.2f), 1f);
            GUI.Label(trash, "Trash: drop a stack here to destroy it", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }

        // What the clicked row is (the list's "more information"): a stack's full description and buttons to hold it, or a
        // collection's averages.
        private void DrawDetails(Rect area, Inventory items)
        {
            ItemSlotGui.Fill(area, new Color(0f, 0f, 0f, 0.3f));
            ItemSlotGui.Outline(area, new Color(1f, 1f, 1f, 0.2f), 1f);
            ListRow row = list.Selected;
            // Closes the window, as a second click on the item does.
            if (GameUi.Button(new Rect(area.xMax - 44f, area.y + 6f, 38f, 34f), "X") || row == null || items[row.Slot].IsEmpty)
            {
                list.ClearSelection();
                return;
            }
            var inner = new Rect(area.x + 12f, area.y + 12f, area.width - 24f, area.height - 24f);
            ItemStack stack = items[row.Slot];
            ItemSlotGui.Draw(new Rect(inner.x, inner.y, 112f, 112f), stack, false);
            var style = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };
            float y = inner.y + 124f;
            string text = row.IsCollection
                ? $"<b>{row.Item.DisplayName}</b> collection: {row.Slots.Count} stacks\nTotal {ListColumns.Amount(row.Amount)}\nAverage value {ListColumns.Coins(row.Value)} each\nAverage quality {ListColumns.QualityLabel(row, items, out _)}\n\nOpen it with its arrow to see each one."
                : InventoryGridGui.Describe(stack);
            float textHeight = style.CalcHeight(new GUIContent(text), inner.width);
            GUI.Label(new Rect(inner.x, y, inner.width, textHeight), text, style);
            y += textHeight + 12f;
            if (row.IsCollection)
            {
                return;
            }
            if (!inventory.IsHandSlot(row.Slot))
            {
                if (GameUi.Button(new Rect(inner.x, y, inner.width, 36f), "Hold in right hand"))
                {
                    hotbar.EquipRight(row.Slot);
                }
                y += 42f;
                if (GameUi.Button(new Rect(inner.x, y, inner.width, 36f), "Hold in left hand"))
                {
                    items.Move(row.Slot, hotbar.LeftSlot);
                }
            }
            else if (GameUi.Button(new Rect(inner.x, y, inner.width, 36f), "Put back in the bag"))
            {
                items.MoveInto(row.Slot, 0, inventory.BagSlots);
            }
        }

        // Sort (GL174) tidies the backpack, or every item slot past the tool slot; the hotbar stays as the player
        // set it. The trash slot (GL173) destroys what's dropped on it.
        private void DrawSortAndTrash(Inventory items, Rect sortButton)
        {
            int sortFrom = inventory.UsesToolSlot ? PlayerInventory.ToolSlot + 1 : inventory.HotbarSlots;
            if (GameUi.Button(sortButton, inventory.UsesToolSlot ? "Sort items" : "Sort backpack"))
            {
                items.Sort(sortFrom, items.SlotCount);
            }
            InventoryGridGui.DrawTrash(trashSlot, drag.IsDragging);
        }

        // GL71, GL191: InventoryDrag moves, splits and carries stacks between slots; here a click sends a stack
        // across (backpack to hotbar, hotbar to backpack, or into the tool slot), and a drop on the trash destroys it.
        private void HandleMouse(Inventory items, Vector2 gridTopLeft, Rect panel)
        {
            InventoryDragResult result = drag.Handle(inventory, gridTopLeft);
            switch (result.Kind)
            {
                case InventoryDragKind.DroppedOutside:
                    // Gone for good (GL173); let go beyond the screen it drops on the ground (GL768).
                    if (!InventoryGridGui.Trash(items, result, trashSlot) && !panel.Contains(result.Position))
                    {
                        inventory.DropFromSlot(result.Slot, result.Count);
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
