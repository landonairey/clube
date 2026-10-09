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
        private const float Padding = 12f;

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
                picked >= 0 ? $"Inventory: place {items[picked].Item?.DisplayName}" : "Inventory (Tab to close)");

            int clicked = InventoryGridGui.DrawPlayer(new Vector2(panel.x + Padding, panel.y + Padding + 24f), inventory, picked);
            InventoryGridGui.DrawTooltip();
            if (clicked < 0)
            {
                return;
            }
            if (picked < 0)
            {
                picked = items[clicked].IsEmpty ? -1 : clicked;
            }
            else
            {
                items.Move(picked, clicked);
                picked = -1;
            }
        }
    }
}
