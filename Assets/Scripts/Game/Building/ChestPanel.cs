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
        private readonly InventoryDrag chestDrag = new InventoryDrag();

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
            chestDrag.Reset();
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
            GameUi.Apply();
            if (Current == null)
            {
                return;
            }
            Inventory chest = Current.Storage;
            Inventory items = inventory.Inventory;
            float width = InventoryGridGui.RowWidth + Padding * 2f;
            float height = Padding * 2f + 32f + InventoryGridGui.GridHeight(chest) + 16f + 30f + InventoryGridGui.PlayerHeight(inventory);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, width - 140f - Padding * 2f, 28f), $"{Current.DisplayName}   {chest.SlotCount} slots   (E to close)");
            // Sort the chest the way the inventory sorts (GL357).
            if (GameUi.Button(new Rect(panel.xMax - Padding - 110f, y - 2f, 110f, 28f), "Sort chest"))
            {
                chest.Sort(0, chest.SlotCount);
            }
            y += 32f;

            // The chest's slots take drags too (GL367): moved, split and stacked inside it, or dragged out to your items.
            var chestTopLeft = new Vector2(x, y);
            var chestArea = new Rect(x, y, InventoryGridGui.RowWidth, InventoryGridGui.GridHeight(chest));
            y += chestArea.height + 16f;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 28f), "Your items: click to store (Shift half, Ctrl one), drag onto the chest, right-click to split");
            y += 30f;
            var grid = new Vector2(x, y);
            var playerArea = new Rect(grid.x, grid.y, InventoryGridGui.RowWidth, InventoryGridGui.PlayerHeight(inventory));

            InventoryDragResult inChest = chestDrag.HandleGrid(chest, chestTopLeft);
            if (inChest.Kind == InventoryDragKind.Clicked
                || (inChest.Kind == InventoryDragKind.DroppedOutside && playerArea.Contains(inChest.Position)))
            {
                chest.MoveTo(inChest.Slot, items, inChest.Count);
            }
            InventoryDragResult mouse = drag.Handle(inventory, grid);
            if (mouse.Kind == InventoryDragKind.Clicked
                || (mouse.Kind == InventoryDragKind.DroppedOutside && chestArea.Contains(mouse.Position)))
            {
                items.MoveTo(mouse.Slot, chest, mouse.Count);
            }
            InventoryGridGui.DrawGrid(chestTopLeft, chest, chestDrag.Pressed);
            InventoryGridGui.PullHalf(chest, items, false);
            InventoryGridGui.DrawPlayer(grid, inventory, drag.Pressed);
            if (chestDrag.IsDragging)
            {
                chestDrag.DrawCarried(chest);
            }
            else if (drag.IsDragging)
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
