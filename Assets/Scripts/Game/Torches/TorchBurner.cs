using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's torches (GL287). An unlit torch selected on the hotbar lights (one of the stack: the rest move to another
    /// slot, or it waits for room); every lit torch in the inventory burns by game time, so resting burns them too, and is
    /// gone once it has burned its <see cref="TorchDefinition.LifeSeconds"/>. While a lit torch is held its light glows in
    /// front of the player.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory), typeof(Hotbar))]
    public class TorchBurner : MonoBehaviour
    {
        [Tooltip("The game's clock, so torches burn by game time. Optional: found in the scene, or real time without one.")]
        [SerializeField]
        private GameClock clock;

        [Tooltip("The smoke rising from a held torch (the Drying steam material, tinted grey).")]
        [SerializeField]
        private Material smokeMaterial;

        private PlayerInventory inventory;
        private Hotbar hotbar;
        private PlayerController player;
        private TorchFlame flame;
        private GameClockTicks ticks;

        /// <summary>Raised when a torch burns out.</summary>
        public event Action<TorchDefinition> BurntOut;

        /// <summary>True while a lit torch is in a hand (either one in the list inventory).</summary>
        public bool HoldingLit => LitHeld(out _);

        // The hand holding a lit torch: the right first, then the left (list inventory); false when neither does.
        private bool LitHeld(out bool left)
        {
            left = false;
            if (hotbar == null)
            {
                return false;
            }
            if (hotbar.SelectedStack.Item is TorchDefinition right && right.IsLit)
            {
                return true;
            }
            left = hotbar.LeftStack.Item is TorchDefinition other && other.IsLit;
            return left;
        }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            hotbar = GetComponent<Hotbar>();
            player = GetComponent<PlayerController>();
            if (clock == null)
            {
                clock = FindAnyObjectByType<GameClock>();
            }
            ticks = new GameClockTicks(clock);
        }

        private void Update()
        {
            if (inventory.Inventory == null)
            {
                return;
            }
            LightIn(hotbar.SelectedSlot);
            LightIn(hotbar.LeftSlot);
            Burn(ticks.WholeSeconds());
            ShowLight();
        }

        // An unlit torch in a hand lights: the right hand's, or the left's in the list inventory, which holds a torch beside
        // whatever the right hand holds.
        private void LightIn(int slot)
        {
            if (slot < 0 || (player != null && player.IsInMenu))
            {
                return;
            }
            Inventory items = inventory.Inventory;
            ItemStack stack = items[slot];
            if (!(stack.Item is TorchDefinition torch) || torch.IsLit || torch.Lit == null)
            {
                return;
            }
            // The rest of the stack needs somewhere to go first.
            if (stack.Count > 1 && !HasRoomElsewhere(items, slot, torch, stack.Count - 1))
            {
                return;
            }
            items.Set(slot, new ItemStack(torch.Lit, 1));
            if (stack.Count > 1)
            {
                items.Add(torch, stack.Count - 1, stack.Contents, 0, stack.Quality);
            }
        }

        private static bool HasRoomElsewhere(Inventory items, int slot, ItemDefinition item, int count)
        {
            int room = 0;
            for (int i = 0; i < items.SlotCount && room < count; i++)
            {
                if (i == slot)
                {
                    continue;
                }
                ItemStack other = items[i];
                room += other.IsEmpty ? item.MaxStack : other.Item == item ? other.Space : 0;
            }
            return room >= count;
        }

        // Every lit torch burns the same whole seconds.
        private void Burn(int seconds)
        {
            if (seconds <= 0)
            {
                return;
            }
            Inventory items = inventory.Inventory;
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                ItemStack stack = items[slot];
                if (!(stack.Item is TorchDefinition torch) || !torch.IsLit)
                {
                    continue;
                }
                int burned = stack.Wear + seconds;
                if (burned >= torch.LifeSeconds)
                {
                    items.Set(slot, ItemStack.Empty);
                    BurntOut?.Invoke(torch);
                }
                else
                {
                    items.Set(slot, new ItemStack(torch, 1, stack.Contents, burned, stack.Quality));
                }
            }
        }

        private void ShowLight()
        {
            bool shown = LitHeld(out bool left) && player != null && player.enabled;
            if (shown && flame == null)
            {
                Transform head = player.Head != null ? player.Head : transform;
                flame = new TorchFlame(head, new Vector3(0.3f, 0.12f, 0.55f), 18f, 5f, smokeMaterial);
            }
            if (flame == null)
            {
                return;
            }
            flame.Shown = shown;
            if (shown)
            {
                flame.Position = new Vector3(left ? -0.3f : 0.3f, 0.12f, 0.55f);
                ItemStack held = left ? hotbar.LeftStack : hotbar.SelectedStack;
                var torch = (TorchDefinition)held.Item;
                flame.Flicker(Mathf.Lerp(0.55f, 1f, torch.Left(held) * 4f));
            }
        }
    }
}
