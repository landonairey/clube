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
    public class Hotbar : MonoBehaviour
    {
        private const float SlotSize = 56f;
        private const float Gap = 4f;
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

        /// <summary>The selected slot, 0-based.</summary>
        public int Selected { get; private set; }

        /// <summary>The inventory slot that's selected (the same as <see cref="Selected"/>).</summary>
        public int SelectedSlot => Selected;

        /// <summary>What the selected slot holds; empty for an empty slot.</summary>
        public ItemStack SelectedStack => StackIn(Selected);

        /// <summary>
        /// Where the tool in hand is: the selected slot in the classic inventory. In the tool-slot inventory,
        /// the selected slot when it holds a tool (scrolled onto, not swapped in), which then wears there,
        /// else the tool slot.
        /// </summary>
        public int ToolSlot => !inventory.UsesToolSlot || SelectedStack.Item is ToolDefinition ? Selected : PlayerInventory.ToolSlot;

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
            for (int slot = 0; slot < count; slot++)
            {
                var rect = new Rect(x + slot * (SlotSize + Gap), y, SlotSize, SlotSize);
                ItemSlotGui.Draw(rect, inventory.Inventory[slot], slot == Selected, null);
            }

            string name = !SelectedStack.IsEmpty ? SelectedStack.Item.DisplayName
                : inventory.UsesToolSlot && Selected == PlayerInventory.ToolSlot ? "Tool slot (empty: bare hands)" : null;
            if (name != null)
            {
                GUI.Label(new Rect(x, y - 22f, Mathf.Max(width, 420f), 20f), name);
            }
        }
    }
}
