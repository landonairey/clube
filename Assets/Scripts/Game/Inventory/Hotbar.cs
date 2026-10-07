using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The hotbar (GL7, first pass of N3): the first slots of the <see cref="PlayerInventory"/>,
    /// drawn along the bottom of the screen. The number keys pick a slot, the scroll wheel and
    /// NextSlot (Q) step through them. What the selected slot holds is what the player holds:
    /// a tool there is used by <see cref="PlayerToolUser"/>.
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

        /// <summary>What the selected slot holds; empty for an empty slot.</summary>
        public ItemStack SelectedStack => inventory.Inventory != null ? inventory.Inventory[Selected] : ItemStack.Empty;

        /// <summary>Selects a hotbar slot (0-based), as its number key does; out-of-range slots are ignored.</summary>
        public void Select(int slot)
        {
            if (slot >= 0 && slot < inventory.HotbarSlots)
            {
                Selected = slot;
            }
        }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
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

            int count = inventory.HotbarSlots;
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

            int count = inventory.HotbarSlots;
            float width = count * SlotSize + (count - 1) * Gap;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - SlotSize - BottomMargin;
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(x + i * (SlotSize + Gap), y, SlotSize, SlotSize);
                ItemSlotGui.Draw(rect, inventory.Inventory[i], i == Selected, (i + 1).ToString());
            }

            ItemStack selected = SelectedStack;
            if (!selected.IsEmpty)
            {
                GUI.Label(new Rect(x, y - 22f, width, 20f), selected.Item.DisplayName);
            }
        }
    }
}
