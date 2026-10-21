using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The hotbar (GL7, first pass of N3), drawn along the bottom of the screen. The number keys pick a
    /// cell, the scroll wheel and NextSlot (X) step through them. In the classic inventory each cell is
    /// one of the first slots of the <see cref="PlayerInventory"/>, and a tool in the selected one is what
    /// <see cref="PlayerToolUser"/> uses. In the volume inventory (GL99) the cells are the tool slot, the
    /// volume slot (the mix, what Drop pours from) and the item slots; the tool in the tool slot is always
    /// the one in hand, and picking an item slot that holds a tool swaps it into the tool slot.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class Hotbar : MonoBehaviour
    {
        private const float SlotSize = 56f;
        private const float Gap = 4f;
        private const float BottomMargin = 12f;

        // The volume inventory's cell for the volume slot, between the tool slot and the item slots.
        private const int MixCell = 1;

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

        /// <summary>The selected cell, 0-based.</summary>
        public int Selected { get; private set; }

        /// <summary>True when the volume slot is selected (GL99): Drop pours from the mix.</summary>
        public bool MixSelected => inventory.UsesVolume && Selected == MixCell;

        /// <summary>The inventory slot of the selected cell; -1 for the volume slot.</summary>
        public int SelectedSlot => SlotOf(Selected);

        /// <summary>What the selected slot holds; empty for an empty slot or the volume slot.</summary>
        public ItemStack SelectedStack
        {
            get
            {
                int slot = SelectedSlot;
                return inventory.Inventory != null && slot >= 0 && slot < inventory.Inventory.SlotCount ? inventory.Inventory[slot] : ItemStack.Empty;
            }
        }

        /// <summary>Where the tool in hand is: the tool slot in the volume inventory, else the selected slot.</summary>
        public int ToolSlot => inventory.UsesVolume ? PlayerInventory.ToolSlot : SelectedSlot;

        /// <summary>The tool in hand's stack (see <see cref="ToolSlot"/>).</summary>
        public ItemStack ToolStack
        {
            get
            {
                int slot = ToolSlot;
                return inventory.Inventory != null && slot >= 0 && slot < inventory.Inventory.SlotCount ? inventory.Inventory[slot] : ItemStack.Empty;
            }
        }

        /// <summary>Cells along the bar: the slots, plus the volume slot in the volume inventory.</summary>
        public int CellCount => inventory.UsesVolume ? inventory.HotbarSlots + 1 : inventory.HotbarSlots;

        /// <summary>Wears the tool in hand by one hit (GL37); true when it broke and its slot emptied.</summary>
        public bool WearSelected(int maxHits)
        {
            int slot = ToolSlot;
            return inventory.Inventory != null && slot >= 0 && inventory.Inventory.Wear(slot, maxHits);
        }

        /// <summary>
        /// Selects a cell (0-based), as its number key does; out-of-range cells are ignored. In the volume
        /// inventory, an item slot holding a tool swaps it into the tool slot and selects that.
        /// </summary>
        public void Select(int cell)
        {
            if (cell < 0 || cell >= CellCount)
            {
                return;
            }
            Selected = cell;
            int slot = SlotOf(cell);
            if (inventory.UsesVolume && slot > PlayerInventory.ToolSlot && inventory.Inventory[slot].Item is ToolDefinition)
            {
                inventory.Inventory.Move(slot, PlayerInventory.ToolSlot);
                Selected = 0;
            }
        }

        // A cell's inventory slot: the same in the classic inventory; past the volume slot, one less.
        private int SlotOf(int cell)
        {
            if (!inventory.UsesVolume)
            {
                return cell;
            }
            return cell == MixCell ? -1 : cell < MixCell ? cell : cell - 1;
        }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            MixSlotGui.Knowledge = GetComponent<OreKnowledge>();
        }

        private void OnEnable()
        {
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

        private void OnGUI()
        {
            if (player != null && !player.enabled)
            {
                return;
            }

            int count = CellCount;
            float width = count * SlotSize + (count - 1) * Gap;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - SlotSize - BottomMargin;
            for (int cell = 0; cell < count; cell++)
            {
                var rect = new Rect(x + cell * (SlotSize + Gap), y, SlotSize, SlotSize);
                string label = (cell + 1).ToString();
                if (inventory.UsesVolume && cell == MixCell)
                {
                    MixSlotGui.Draw(rect, inventory.Mix, cell == Selected, label);
                    continue;
                }
                ItemSlotGui.Draw(rect, inventory.Inventory[SlotOf(cell)], cell == Selected, label);
            }

            string name = MixSelected ? MixSlotGui.Describe(inventory.Mix)
                : !SelectedStack.IsEmpty ? SelectedStack.Item.DisplayName
                : inventory.UsesVolume && Selected == 0 ? "Tool slot (empty: bare hands)" : null;
            if (name != null)
            {
                GUI.Label(new Rect(x, y - 22f, Mathf.Max(width, 420f), 20f), name);
            }
        }
    }
}
