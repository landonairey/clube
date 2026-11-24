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
    public class ChestPanel : MonoBehaviour, IGuiDrawer
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
        // The list inventory (prototype): the chest's list on the left, yours on the right.
        private readonly InventoryListGui chestList = new InventoryListGui();
        private readonly InventoryListGui itemList = new InventoryListGui();

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
            GuiHost.Add(this);
            closeAction?.action.Enable();
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
            Close();
        }

        public void Open(StorageChest chest)
        {
            Close();
            Current = chest;
            // GL729: a cart's or vessel's crate counts what the player takes from it.
            if (chest != null && chest.TryGetComponent(out CargoCrate cargo))
            {
                cargo.Opened();
            }
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
            // A chest destroyed while open (a cart gone from view) still closes the screen.
            if (ReferenceEquals(Current, null))
            {
                return;
            }
            if (Current != null && Current.TryGetComponent(out CargoCrate cargo))
            {
                cargo.Closed();
            }
            Current = null;
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        private void Update()
        {
            if (ReferenceEquals(Current, null))
            {
                return;
            }
            if (Current == null)
            {
                Close();
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

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            GameUi.Apply();
            if (Current == null)
            {
                return;
            }
            Inventory chest = Current.Storage;
            Inventory items = inventory.Inventory;
            if (inventory.UsesList)
            {
                DrawLists(chest, items);
                return;
            }
            float width = InventoryGridGui.RowWidth + Padding * 2f;
            float height = Padding * 2f + 32f + InventoryGridGui.GridHeight(chest) + 16f + 30f + InventoryGridGui.PlayerHeight(inventory);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, width - 260f - Padding * 2f, 28f), $"{Current.DisplayName}   {chest.SlotCount} slots   (E to close)");
            // Sort the chest the way the inventory sorts (GL357).
            if (GameUi.Button(new Rect(panel.xMax - Padding - 110f, y - 2f, 110f, 28f), "Sort chest"))
            {
                chest.Sort(0, chest.SlotCount);
            }
            // Everything in the chest to your items (GL705): what doesn't fit stays.
            if (GameUi.Button(new Rect(panel.xMax - Padding - 230f, y - 2f, 110f, 28f), "Take all"))
            {
                for (int slot = 0; slot < chest.SlotCount; slot++)
                {
                    chest.MoveTo(slot, items);
                }
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
            else if (inChest.Kind == InventoryDragKind.DroppedOutside && !panel.Contains(inChest.Position))
            {
                // Let go beyond the screen it drops on the ground (GL792), as from the inventory (GL768).
                inventory.DropFrom(chest, inChest.Slot, inChest.Count);
            }
            InventoryDragResult mouse = drag.Handle(inventory, grid);
            if (mouse.Kind == InventoryDragKind.Clicked)
            {
                items.MoveTo(mouse.Slot, chest, mouse.Count);
            }
            else if (mouse.Kind == InventoryDragKind.DroppedOutside && !panel.Contains(mouse.Position))
            {
                inventory.DropFromSlot(mouse.Slot, mouse.Count);
            }
            else if (mouse.Kind == InventoryDragKind.DroppedOutside && chestArea.Contains(mouse.Position))
            {
                // Dropped on a chest slot it goes in that one (GL706); in a gap or past the last slot, wherever it fits.
                int target = InventoryGridGui.GridSlotNear(chestTopLeft, chest, mouse.Position, InventoryGridGui.Gap);
                if (target < 0 || items.MoveToSlot(mouse.Slot, chest, target, mouse.Count) == 0 && chest[target].IsEmpty)
                {
                    items.MoveTo(mouse.Slot, chest, mouse.Count);
                }
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

        // The list inventory's chest screen (prototype): two lists side by side. A click on a row moves it across (a
        // collection's row moves every stack of it), as does a drag onto the other list; off the screen drops it.
        private void DrawLists(Inventory chest, Inventory items)
        {
            float half = Mathf.Min(900f, (Screen.width - 60f - Padding * 3f) * 0.5f);
            float width = half * 2f + Padding * 3f;
            float height = Mathf.Min(Screen.height - 280f, 1000f);
            var panel = new Rect((Screen.width - width) * 0.5f, Mathf.Max(10f, (Screen.height - height) * 0.5f - 60f), width, height);
            InventoryGridGui.SolidPanel(panel);
            float y = panel.y + Padding;
            var left = new Rect(panel.x + Padding, y + 36f, half, panel.height - Padding * 2f - 36f);
            var right = new Rect(left.xMax + Padding, left.y, half, left.height);
            GUI.Label(new Rect(left.x, y, half - 240f, 28f), $"{Current.DisplayName}   (E to close)");
            if (GameUi.Button(new Rect(left.xMax - 110f, y - 2f, 110f, 28f), "Take all"))
            {
                for (int slot = 0; slot < chest.SlotCount; slot++)
                {
                    chest.MoveTo(slot, items);
                }
            }
            GUI.Label(new Rect(right.x, y, half, 28f), "Your items: click to store (Shift half, Ctrl one)");

            itemList.Equipped = inventory.IsHandSlot;
            ListResult fromChest = chestList.Draw(left, chest);
            ListResult fromItems = itemList.Draw(right, items);
            Move(fromChest, chest, items, right, panel, true);
            Move(fromItems, items, chest, left, panel, false);
            if (chestList.IsDragging)
            {
                chestList.DrawCarried(chest);
            }
            else if (itemList.IsDragging)
            {
                itemList.DrawCarried(items);
            }
            else
            {
                InventoryGridGui.DrawTooltip();
            }
        }

        private void Move(ListResult result, Inventory from, Inventory to, Rect toArea, Rect panel, bool fromChest)
        {
            bool across = result.Kind == InventoryDragKind.Clicked
                || (result.Kind == InventoryDragKind.DroppedOutside && toArea.Contains(result.Position));
            if (across)
            {
                if (result.Row.IsCollection && result.Kind == InventoryDragKind.Clicked)
                {
                    foreach (int slot in result.Row.Slots)
                    {
                        from.MoveTo(slot, to);
                    }
                }
                else
                {
                    from.MoveTo(result.Slot, to, result.Count);
                }
            }
            else if (result.Kind == InventoryDragKind.DroppedOutside && !panel.Contains(result.Position))
            {
                if (fromChest)
                {
                    inventory.DropFrom(from, result.Slot, result.Count);
                }
                else
                {
                    inventory.DropFromSlot(result.Slot, result.Count);
                }
            }
        }
    }
}
