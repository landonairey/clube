using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The inventory screen (GL7, first pass of N2): Inventory (Tab) opens a grid of every
    /// slot, backpack above the hotbar row, and frees the cursor. Clicking a stack sends it to the
    /// first open hotbar slot, or from the hotbar to the backpack (GL71); pressing and dragging
    /// drops it on a chosen slot (<see cref="Inventory.Move"/>: the same item stacks, the rest
    /// staying behind; anything else swaps). Splitting stacks comes with N2.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class InventoryScreen : MonoBehaviour
    {
        private const float Padding = 12f;

        // Pixels the mouse moves with the button held before a press becomes a drag.
        private const float DragDistance = 6f;

        [Tooltip("Opens and closes the screen (Tab).")]
        [SerializeField]
        private InputActionReference toggleAction;

        private PlayerInventory inventory;
        private PlayerController player;
        private StationPanel stationPanel;
        private MerchantPanel merchantPanel;
        private JournalScreen journalScreen;
        private int pressed = -1;
        private Vector2 pressedAt;
        private bool dragging;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            stationPanel = GetComponent<StationPanel>();
            merchantPanel = GetComponent<MerchantPanel>();
            journalScreen = GetComponent<JournalScreen>();
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
                || (journalScreen != null && journalScreen.IsOpen);
            if (toggleAction != null && toggleAction.action.WasPressedThisFrame() && !otherOpen)
            {
                SetOpen(!IsOpen);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            pressed = -1;
            dragging = false;
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
            float height = Padding * 2f + 24f + InventoryGridGui.PlayerHeight(inventory);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            GUI.Label(new Rect(panel.x + Padding, panel.y + Padding, width - Padding * 2f, 20f),
                "Inventory (Tab to close): click to send to the hotbar or back, drag to place");

            var gridTopLeft = new Vector2(panel.x + Padding, panel.y + Padding + 24f);
            HandleMouse(items, gridTopLeft);
            InventoryGridGui.DrawPlayer(gridTopLeft, inventory, pressed);
            if (dragging && pressed >= 0 && !items[pressed].IsEmpty)
            {
                // The stack follows the mouse while it's carried.
                Vector2 mouse = Event.current.mousePosition;
                float half = InventoryGridGui.SlotSize * 0.5f;
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                ItemSlotGui.Draw(new Rect(mouse.x - half, mouse.y - half, InventoryGridGui.SlotSize, InventoryGridGui.SlotSize), items[pressed], true);
                GUI.color = previous;
            }
            else
            {
                InventoryGridGui.DrawTooltip();
            }
        }

        // GL71: a click sends a stack across (backpack to hotbar, hotbar to backpack); pressing and
        // dragging carries it to the slot it's dropped on, swapping or stacking there (Inventory.Move).
        private void HandleMouse(Inventory items, Vector2 gridTopLeft)
        {
            Event current = Event.current;
            if (current.button != 0)
            {
                return;
            }
            int over = InventoryGridGui.PlayerSlotAt(gridTopLeft, inventory, current.mousePosition);
            switch (current.type)
            {
                case EventType.MouseDown:
                    if (over >= 0 && !items[over].IsEmpty)
                    {
                        pressed = over;
                        pressedAt = current.mousePosition;
                        dragging = false;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (pressed >= 0)
                    {
                        dragging |= (current.mousePosition - pressedAt).sqrMagnitude > DragDistance * DragDistance;
                        current.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (pressed < 0)
                    {
                        break;
                    }
                    if (dragging)
                    {
                        if (over >= 0)
                        {
                            items.Move(pressed, over);
                        }
                    }
                    else if (over == pressed)
                    {
                        int hotbar = inventory.HotbarSlots;
                        if (pressed < hotbar)
                        {
                            items.MoveInto(pressed, hotbar, items.SlotCount);
                        }
                        else
                        {
                            items.MoveInto(pressed, 0, hotbar);
                        }
                    }
                    pressed = -1;
                    dragging = false;
                    current.Use();
                    break;
            }
        }
    }
}
