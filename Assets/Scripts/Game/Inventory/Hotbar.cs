using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The hotbar (GL7, first pass of N3), drawn along the bottom of the screen. The number keys pick a
    /// slot, the scroll wheel and NextSlot (X) step through them. In the classic inventory each cell is one
    /// of the first slots of the <see cref="PlayerInventory"/>, and a tool in the selected one is what
    /// <see cref="PlayerToolUser"/> uses. In the tool-slot inventory (GL99, GL120) the first cell is the tool
    /// slot, whose tool is in hand unless another selected slot holds a tool, and the rest are item slots;
    /// picking an item slot that holds a tool by its number swaps it into the tool slot.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class Hotbar : MonoBehaviour, IGuiDrawer
    {
        private const float SlotSize = GameUi.SlotSize;
        private const float Gap = GameUi.SlotGap;
        private const float BottomMargin = 12f;

        [Tooltip("Picks a slot: bound to the number keys 1-9.")]
        [SerializeField]
        private InputActionReference slotAction;

        [Tooltip("Scroll wheel: steps through the slots.")]
        [SerializeField]
        private InputActionReference scrollAction;

        [Tooltip("Steps to the next slot (Q).")]
        [SerializeField]
        private InputActionReference nextAction;

        private PlayerInventory inventory;
        private PlayerController player;
        private HotbarView view;
        private HandSlotsView handsView;

        /// <summary>The selected slot, 0-based; in the list inventory, the right hand's slot (0-4).</summary>
        public int Selected { get; private set; }

        /// <summary>List inventory: the left hand's selected slot (0-4), stepped with Ctrl and the wheel.</summary>
        public int LeftSelected { get; private set; }

        /// <summary>The inventory slot that's selected: the right hand's in the list inventory, else <see cref="Selected"/>.</summary>
        public int SelectedSlot => inventory.UsesList ? inventory.RightHandSlot(Selected) : Selected;

        /// <summary>List inventory: the inventory slot the left hand holds.</summary>
        public int LeftSlot => inventory.UsesList ? inventory.LeftHandSlot(LeftSelected) : -1;

        /// <summary>What the selected slot holds; empty for an empty slot.</summary>
        public ItemStack SelectedStack => StackIn(SelectedSlot);

        /// <summary>List inventory: what the left hand holds.</summary>
        public ItemStack LeftStack => StackIn(LeftSlot);

        /// <summary>
        /// Where the tool in hand is: the selected slot in the classic inventory. In the tool-slot inventory,
        /// the selected slot when it holds a tool (scrolled onto, not swapped in), which then wears there,
        /// else the tool slot.
        /// </summary>
        public int ToolSlot => !inventory.UsesToolSlot || SelectedStack.Item is ToolDefinition ? SelectedSlot : PlayerInventory.ToolSlot;

        /// <summary>List inventory: when the wheel last stepped a hand, and which, so the HUD shows that hand's other slots a moment.</summary>
        public float LastStepTime { get; private set; } = -10f;

        /// <summary>List inventory: true when the last step was the left hand's.</summary>
        public bool LastStepLeft { get; private set; }

        /// <summary>List inventory: puts a slot's stack in the right hand's selected slot (a right click in the list), swapping out what was there.</summary>
        public void EquipRight(int slot)
        {
            int hand = SelectedSlot;
            if (inventory.UsesList && slot != hand && slot >= 0 && slot < inventory.Inventory.SlotCount && !inventory.Inventory[slot].IsEmpty)
            {
                inventory.Inventory.Move(slot, hand);
            }
        }

        /// <summary>List inventory: selects a left hand slot (0-4).</summary>
        public void SelectLeft(int index)
        {
            if (index >= 0 && index < PlayerInventory.HandSlots)
            {
                LeftSelected = index;
            }
        }

        /// <summary>The tool in hand's stack (see <see cref="ToolSlot"/>).</summary>
        public ItemStack ToolStack => StackIn(ToolSlot);

        /// <summary>Cells along the bar.</summary>
        public int CellCount => inventory.HotbarSlots;

        /// <summary>Wears the tool in hand by one hit (GL37); true when it broke and its slot emptied.</summary>
        public bool WearSelected(int maxHits)
        {
            int slot = ToolSlot;
            if (inventory.Inventory == null || slot < 0)
            {
                return false;
            }
            // A broken tool leaves its slot empty; swapping in a spare is up to the player (GL138).
            return inventory.Inventory.Wear(slot, maxHits);
        }

        /// <summary>
        /// Selects a slot (0-based), as its number key does; out-of-range slots are ignored. In the tool-slot
        /// inventory, an item slot holding a tool swaps it into the tool slot and selects that.
        /// </summary>
        public void Select(int slot)
        {
            if (slot < 0 || slot >= CellCount)
            {
                return;
            }
            Selected = slot;
            if (inventory.UsesToolSlot && slot > PlayerInventory.ToolSlot && inventory.Inventory[slot].Item is ToolDefinition)
            {
                inventory.Inventory.Move(slot, PlayerInventory.ToolSlot);
                Selected = PlayerInventory.ToolSlot;
            }
        }

        private ItemStack StackIn(int slot)
        {
            return inventory.Inventory != null && slot >= 0 && slot < inventory.Inventory.SlotCount ? inventory.Inventory[slot] : ItemStack.Empty;
        }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
        }

        private void Start()
        {
            // Stacks show how pure they are (GL115) once the world's materials are known.
            WorldView world = FindFirstObjectByType<WorldView>();
            if (world != null && world.Config != null)
            {
                ItemSlotGui.Materials = world.Config.Materials;
            }
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
        }

        private void OnEnable()
        {
            GuiHost.Add(this);
            slotAction?.action.Enable();
            scrollAction?.action.Enable();
            nextAction?.action.Enable();
        }

        private void Update()
        {
            if (player != null && (!player.enabled || player.IsCursorFree))
            {
                return;
            }

            int count = CellCount;
            if (Selected >= count)
            {
                Selected = 0;
            }
            if (inventory.UsesList)
            {
                UpdateHands(count);
                return;
            }
            if (slotAction != null && slotAction.action.WasPressedThisFrame()
                && int.TryParse(slotAction.action.activeControl?.name, out int key) && key >= 1 && key <= count)
            {
                Select(key - 1);
            }
            if (scrollAction != null)
            {
                float scroll = scrollAction.action.ReadValue<float>();
                if (scroll != 0f)
                {
                    // Wheel down moves right, like most games' hotbars.
                    Step(scroll < 0f ? 1 : -1, count);
                }
            }
            if (nextAction != null && nextAction.action.WasPressedThisFrame())
            {
                Step(1, count);
            }
        }

        // Stepping moves the selection only; it doesn't swap tools in.
        private void Step(int by, int count)
        {
            Selected = ((Selected + by) % count + count) % count;
        }

        // List inventory (prototype): the number keys and the wheel pick the right hand's slot, Ctrl and the wheel the left's.
        private void UpdateHands(int count)
        {
            if (slotAction != null && slotAction.action.WasPressedThisFrame()
                && int.TryParse(slotAction.action.activeControl?.name, out int key) && key >= 1 && key <= count)
            {
                Select(key - 1);
                MarkStep(false);
            }
            bool left = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
            float scroll = scrollAction != null ? scrollAction.action.ReadValue<float>() : 0f;
            if (scroll != 0f)
            {
                int by = scroll < 0f ? 1 : -1;
                if (left)
                {
                    LeftSelected = ((LeftSelected + by) % count + count) % count;
                }
                else
                {
                    Step(by, count);
                }
                MarkStep(left);
            }
            if (nextAction != null && nextAction.action.WasPressedThisFrame())
            {
                Step(1, count);
                MarkStep(false);
            }
        }

        private void MarkStep(bool left)
        {
            LastStepTime = Time.unscaledTime;
            LastStepLeft = left;
        }

        // The bar is on the HUD canvas (GL777), refreshed after the frame's input and edits; the list inventory shows its
        // two hands in the lower right instead (prototype).
        private void LateUpdate()
        {
            bool visible = player == null || player.enabled;
            if (inventory.UsesList)
            {
                view?.Destroy();
                view = null;
                handsView ??= new HandSlotsView();
                handsView.Refresh(this, inventory, visible);
                return;
            }
            handsView?.Destroy();
            handsView = null;
            view ??= new HotbarView();
            string name = !SelectedStack.IsEmpty ? SelectedStack.Item.DisplayName
                : inventory.UsesToolSlot && Selected == PlayerInventory.ToolSlot ? "Tool slot (empty: bare hands)" : null;
            view.Refresh(inventory, CellCount, Selected, name, visible);
        }

        private void OnDestroy()
        {
            view?.Destroy();
            handsView?.Destroy();
        }

        int IGuiDrawer.GuiDepth => -5;

        // The hands' popups when the cursor is free (a screen open): the same description the inventory's tooltip gives.
        void IGuiDrawer.DrawGui()
        {
            if (handsView == null || player == null || !player.IsCursorFree || !player.enabled)
            {
                return;
            }
            Vector2 mouse = Event.current.mousePosition;
            if (handsView.RightRect.Contains(mouse) && !SelectedStack.IsEmpty)
            {
                InventoryGridGui.Hover(SelectedStack);
                InventoryGridGui.DrawTooltip();
            }
            else if (handsView.LeftRect.Contains(mouse) && !LeftStack.IsEmpty)
            {
                InventoryGridGui.Hover(LeftStack);
                InventoryGridGui.DrawTooltip();
            }
        }
    }
}
