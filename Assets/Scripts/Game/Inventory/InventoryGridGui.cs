using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Draws inventory slots as a grid with IMGUI (GL7, GL20), shared by the inventory screen
    /// and the station and merchant screens so every screen shows items the same way: the
    /// player's backpack above and the hotbar row below, or any run of slots in a row. Each
    /// draw returns the slot clicked, if any, and leaves what a click does to the screen.
    /// </summary>
    public static class InventoryGridGui
    {
        public const int Columns = 9;
        public const float SlotSize = 56f;
        public const float Gap = 4f;
        public const float RowGap = 14f;

        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.1f, 0.92f);

        /// <summary>A screen's background: nearly opaque, so the world doesn't show through the slots.</summary>
        public static void Panel(Rect area)
        {
            Color previous = GUI.color;
            GUI.color = PanelColor;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Width of a full row of <see cref="Columns"/> slots.</summary>
        public static float RowWidth => Columns * SlotSize + (Columns - 1) * Gap;

        /// <summary>Height of the player's backpack rows, the gap and the hotbar row.</summary>
        public static float PlayerHeight(PlayerInventory player)
        {
            int rows = BackpackRows(player);
            return rows * (SlotSize + Gap) - Gap + RowGap + SlotSize;
        }

        /// <summary>
        /// The player's slots: backpack rows from <paramref name="topLeft"/>, then the hotbar row
        /// (numbered like the hotbar). Returns the slot clicked, or -1.
        /// </summary>
        /// <param name="selected">A slot to draw highlighted, or -1.</param>
        /// <param name="corner">Optional small text for a slot's lower left, e.g. a price; null for none.</param>
        public static int DrawPlayer(Vector2 topLeft, PlayerInventory player, int selected, System.Func<ItemStack, string> corner = null)
        {
            Inventory items = player.Inventory;
            int hotbar = player.HotbarSlots;
            int clicked = -1;
            for (int slot = hotbar; slot < items.SlotCount; slot++)
            {
                int index = slot - hotbar;
                var position = new Vector2(topLeft.x + (index % Columns) * (SlotSize + Gap), topLeft.y + (index / Columns) * (SlotSize + Gap));
                if (Slot(position, items[slot], slot == selected, null, corner))
                {
                    clicked = slot;
                }
            }
            float hotbarTop = topLeft.y + BackpackRows(player) * (SlotSize + Gap) - Gap + RowGap;
            for (int slot = 0; slot < hotbar; slot++)
            {
                var position = new Vector2(topLeft.x + slot * (SlotSize + Gap), hotbarTop);
                if (Slot(position, items[slot], slot == selected, (slot + 1).ToString(), corner))
                {
                    clicked = slot;
                }
            }
            return clicked;
        }

        /// <summary>A row of an inventory's slots from <paramref name="topLeft"/>. Returns the slot clicked, or -1.</summary>
        public static int DrawRow(Vector2 topLeft, Inventory items, int selected = -1)
        {
            int clicked = -1;
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                var position = new Vector2(topLeft.x + slot * (SlotSize + Gap), topLeft.y);
                if (Slot(position, items[slot], slot == selected, null, null))
                {
                    clicked = slot;
                }
            }
            return clicked;
        }

        /// <summary>One slot as a button. True when clicked.</summary>
        public static bool Slot(Vector2 position, ItemStack stack, bool selected, string key, System.Func<ItemStack, string> corner)
        {
            var rect = new Rect(position.x, position.y, SlotSize, SlotSize);
            ItemSlotGui.Draw(rect, stack, selected, key, corner?.Invoke(stack));
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static int BackpackRows(PlayerInventory player)
        {
            return Mathf.CeilToInt((player.Inventory.SlotCount - player.HotbarSlots) / (float)Columns);
        }
    }
}
