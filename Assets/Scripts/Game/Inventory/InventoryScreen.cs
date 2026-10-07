using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The inventory screen (GL7, first pass of N2): Inventory (Tab) opens a grid of every
    /// slot, hotbar row first, and frees the cursor. Click a slot to pick its stack up, then
    /// another to put it there (<see cref="Inventory.Move"/>: same items merge, others swap).
    /// Drag and drop and splitting stacks come with N2.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class InventoryScreen : MonoBehaviour
    {
        private const int Columns = 9;
        private const float SlotSize = 56f;
        private const float Gap = 4f;
        private const float Padding = 12f;
        private const float RowGap = 14f;

        [Tooltip("Opens and closes the screen (Tab).")]
        [SerializeField]
        private InputActionReference toggleAction;

        private PlayerInventory inventory;
        private PlayerController player;
        private StationPanel stationPanel;
        private MerchantPanel merchantPanel;
        private int picked = -1;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            stationPanel = GetComponent<StationPanel>();
            merchantPanel = GetComponent<MerchantPanel>();
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
            bool otherOpen = (stationPanel != null && stationPanel.IsOpen) || (merchantPanel != null && merchantPanel.IsOpen);
            if (toggleAction != null && toggleAction.action.WasPressedThisFrame() && !otherOpen)
            {
                SetOpen(!IsOpen);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            picked = -1;
            if (player != null)
            {
                player.IsCursorFree = open;
            }
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            Inventory items = inventory.Inventory;
            int hotbar = inventory.HotbarSlots;
            int backpackRows = Mathf.CeilToInt((items.SlotCount - hotbar) / (float)Columns);
            float width = Columns * SlotSize + (Columns - 1) * Gap + Padding * 2f;
            float height = Padding * 2f + 24f + SlotSize * (1 + backpackRows) + Gap * Mathf.Max(0, backpackRows - 1) + RowGap;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + Padding, panel.y + Padding, width - Padding * 2f, 20f),
                picked >= 0 ? $"Inventory: place {items[picked].Item?.DisplayName}" : "Inventory (Tab to close)");

            // The backpack above, the hotbar row along the bottom, as on screen.
            float top = panel.y + Padding + 24f;
            for (int slot = hotbar; slot < items.SlotCount; slot++)
            {
                int index = slot - hotbar;
                DrawSlot(items, slot, panel.x + Padding + (index % Columns) * (SlotSize + Gap), top + (index / Columns) * (SlotSize + Gap));
            }
            float hotbarTop = top + backpackRows * (SlotSize + Gap) - Gap + RowGap;
            for (int slot = 0; slot < hotbar; slot++)
            {
                DrawSlot(items, slot, panel.x + Padding + slot * (SlotSize + Gap), hotbarTop);
            }
        }

        private void DrawSlot(Inventory items, int slot, float x, float y)
        {
            var rect = new Rect(x, y, SlotSize, SlotSize);
            ItemSlotGui.Draw(rect, items[slot], slot == picked, slot < inventory.HotbarSlots ? (slot + 1).ToString() : null);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                if (picked < 0)
                {
                    picked = items[slot].IsEmpty ? -1 : slot;
                }
                else
                {
                    items.Move(picked, slot);
                    picked = -1;
                }
            }
        }
    }
}
