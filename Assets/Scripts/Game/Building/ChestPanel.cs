using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The screen for a <see cref="StorageChest"/> the player opened (GL249): the chest's slots on top, the player's
    /// below. Click a stack in the chest to take it; click one of yours to put it in (Shift half, Ctrl one), or drag it
    /// onto the chest's slots; drag between your own slots and right-click to split, as in the inventory. Frees the
    /// cursor while open; Interact (E) closes it, as does walking away.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class ChestPanel : MonoBehaviour
    {
        private const float Padding = 12f;

        [Tooltip("Closes the panel (E).")]
        [SerializeField]
        private InputActionReference closeAction;

        [Tooltip("The panel closes when the player is this far from the chest, in metres.")]
        [SerializeField, Min(1f)]
        private float closeDistance = 5f;

        private PlayerInventory inventory;
        private PlayerController player;
        private int openedFrame;
        private readonly InventoryDrag drag = new InventoryDrag();

        /// <summary>The chest open, or null when closed.</summary>
        public StorageChest Current { get; private set; }

        public bool IsOpen => Current != null;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            closeAction?.action.Enable();
        }

        private void OnDisable()
        {
            Close();
        }

        public void Open(StorageChest chest)
        {
            Current = chest;
            drag.Reset();
            openedFrame = Time.frameCount;
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        public void Close()
        {
            if (Current == null)
            {
                return;
            }
            Current = null;
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        private void Update()
        {
            if (Current == null)
            {
                return;
            }
            // The press that opened the panel mustn't close it in the same frame.
            bool closePressed = Time.frameCount != openedFrame && closeAction != null && closeAction.action.WasPressedThisFrame();
            bool tooFar = (Current.transform.position - transform.position).sqrMagnitude > closeDistance * closeDistance;
            if (closePressed || tooFar || (player != null && !player.enabled))
            {
                Close();
            }
        }

        private void OnGUI()
        {
            if (Current == null)
            {
                return;
            }
            Inventory chest = Current.Storage;
            Inventory items = inventory.Inventory;
            float width = InventoryGridGui.RowWidth + Padding * 2f;
            float height = Padding * 2f + 24f + InventoryGridGui.GridHeight(chest) + 16f + 22f + InventoryGridGui.PlayerHeight(inventory);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), $"{Current.DisplayName}   {chest.SlotCount} slots   (E to close)");
            y += 24f;

            var chestArea = new Rect(x, y, InventoryGridGui.RowWidth, InventoryGridGui.GridHeight(chest));
            int taken = InventoryGridGui.DrawGrid(new Vector2(x, y), chest);
            if (taken >= 0)
            {
                chest.MoveTo(taken, items);
            }
            InventoryGridGui.PullHalf(chest, items, false);
            y += chestArea.height + 16f;

            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), "Your items: click to store (Shift half, Ctrl one), drag onto the chest, right-click to split");
            y += 22f;
            var grid = new Vector2(x, y);
            InventoryDragResult mouse = drag.Handle(inventory, grid);
            if (mouse.Kind == InventoryDragKind.Clicked
                || (mouse.Kind == InventoryDragKind.DroppedOutside && chestArea.Contains(mouse.Position)))
            {
                items.MoveTo(mouse.Slot, chest, mouse.Count);
            }
            InventoryGridGui.DrawPlayer(grid, inventory, drag.Pressed);
            if (drag.IsDragging)
            {
                drag.DrawCarried(items);
            }
            else
            {
                InventoryGridGui.DrawTooltip();
            }
        }
    }
}
