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

        /// <summary>
        /// The player's slot under a point, laid out as <see cref="DrawPlayer"/> draws them from
        /// <paramref name="topLeft"/>; -1 when the point is on no slot (GL71's drag and drop).
        /// </summary>
        public static int PlayerSlotAt(Vector2 topLeft, PlayerInventory player, Vector2 point)
        {
            Inventory items = player.Inventory;
            int hotbar = player.HotbarSlots;
            for (int slot = hotbar; slot < items.SlotCount; slot++)
            {
                int index = slot - hotbar;
                var rect = new Rect(topLeft.x + (index % Columns) * (SlotSize + Gap), topLeft.y + (index / Columns) * (SlotSize + Gap), SlotSize, SlotSize);
                if (rect.Contains(point))
                {
                    return slot;
                }
            }
            float hotbarTop = topLeft.y + BackpackRows(player) * (SlotSize + Gap) - Gap + RowGap;
            for (int slot = 0; slot < hotbar; slot++)
            {
                if (new Rect(topLeft.x + slot * (SlotSize + Gap), hotbarTop, SlotSize, SlotSize).Contains(point))
                {
                    return slot;
                }
            }
            return -1;
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
            if (!stack.IsEmpty && Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition))
            {
                hovered = stack;
            }
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static ItemStack hovered;
        private static GUIStyle tooltipStyle;

        /// <summary>
        /// Describes the stack under the mouse beside it (GL32, GL33): its count and volume, what
        /// it's made of, how long it burns. Call after drawing a screen's slots.
        /// </summary>
        public static void DrawTooltip()
        {
            if (Event.current.type != EventType.Repaint || hovered.IsEmpty)
            {
                return;
            }
            ItemStack stack = hovered;
            hovered = ItemStack.Empty;

            tooltipStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(8, 8, 6, 6) };
            var content = new GUIContent(Describe(stack));
            float width = 240f;
            float height = tooltipStyle.CalcHeight(content, width);
            Vector2 mouse = Event.current.mousePosition;
            var rect = new Rect(Mathf.Min(mouse.x + 16f, Screen.width - width), Mathf.Min(mouse.y + 16f, Screen.height - height), width, height);
            Panel(rect);
            GUI.Label(rect, content, tooltipStyle);
        }

        /// <summary>
        /// One fact a line (GL70): "Chalcopyrite ×12", "Volume 12", then a line per component,
        /// "80% Stone", "20% Chalcopyrite".
        /// </summary>
        public static string Describe(ItemStack stack)
        {
            string text = stack.Count > 1 ? $"{stack.Item.DisplayName} ×{stack.Count}" : stack.Item.DisplayName;
            text += $"\nVolume {stack.Volume:0.##}";
            if (stack.Contents != null)
            {
                foreach (Content part in stack.Contents.Parts)
                {
                    float percent = part.Fraction * 100f;
                    text += percent >= 1f ? $"\n{percent:0}% {part.Material.DisplayName}" : $"\n{percent:0.#}% {part.Material.DisplayName}";
                }
            }
            if (stack.Item.IsFuel)
            {
                text += $"\nFuel: burns {stack.Item.BurnSeconds:0.#} s each";
            }
            return text;
        }

        private static int BackpackRows(PlayerInventory player)
        {
            return Mathf.CeilToInt((player.Inventory.SlotCount - player.HotbarSlots) / (float)Columns);
        }
    }
}
