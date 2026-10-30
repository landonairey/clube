using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Draws one inventory slot with IMGUI (GL7): a dark square, the item's icon (or its
    /// name when it has none), its count, a bright border when selected, and an optional
    /// corner note such as a price. Shared by the
    /// <see cref="Hotbar"/> and the <see cref="InventoryScreen"/> so slots look the same in both.
    /// </summary>
    public static class ItemSlotGui
    {
        private static readonly Color Background = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color SelectedBorder = new Color(1f, 0.85f, 0.3f, 1f);

        private static GUIStyle nameStyle;
        private static GUIStyle countStyle;
        private static GUIStyle purityStyle;

        /// <summary>The terrain materials, for how pure a stack is (GL115); set by whoever has them, null shows no purity.</summary>
        public static MaterialRegistry Materials { get; set; }

        // Every stack shows how well it was made, in the upper right, 100% included (GL158): its quality
        // (GL118: sticks of 60% wood), or for mined material that isn't all its own material, how much of it
        // is (GL115: ore of a grade).
        private static void DrawPurity(Rect rect, ItemStack stack)
        {
            float purity = stack.Quality;
            if (purity >= 0.995f && purity <= 1.005f && Materials != null && stack.Contents != null && !stack.Item.HidesContents && Materials.IsMaterialItem(stack.Item))
            {
                purity = Materials.Quality(stack.Contents, stack.Item);
            }
            // No padding or clipping: the skin's label padding cut the bottom off the digits (GL222).
            purityStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Overflow, normal = { textColor = new Color(1f, 0.8f, 0.35f) } };
            GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width - 4f, 16f), (purity * 100f).ToString("0") + "%", purityStyle);
        }

        /// <param name="corner">Small text for the lower left, e.g. a merchant's price; null for none.</param>
        public static void Draw(Rect rect, ItemStack stack, bool selected, string key = null, string corner = null)
        {
            EnsureStyles();
            Fill(rect, Background);
            Outline(rect, selected ? SelectedBorder : Border, selected ? 2f : 1f);

            if (!stack.IsEmpty)
            {
                Rect inner = Inset(rect, 6f);
                if (stack.Item.Icon != null)
                {
                    GUI.DrawTexture(inner, stack.Item.Icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.Label(inner, stack.Item.DisplayName, nameStyle);
                }
                if (stack.Part > 0f)
                {
                    // A part item (GL136): 0.48 of a tin bun.
                    GUI.Label(Inset(rect, 3f), stack.Amount.ToString("0.##"), countStyle);
                }
                else if (stack.Count > 1)
                {
                    GUI.Label(Inset(rect, 3f), stack.Count.ToString(), countStyle);
                }
                DrawPurity(rect, stack);
                if (stack.MaxHits > 0 && (stack.Wear > 0 || stack.Quality < 0.995f))
                {
                    // Wear bar along the bottom: green when fresh, red when nearly gone.
                    // Against its full hits, so a tool of 60% quality starts with 60% of the bar (GL118).
                    float left = Mathf.Clamp01((float)(stack.MaxHits - stack.Wear) / ((ToolDefinition)stack.Item).MaxHits);
                    var bar = new Rect(rect.x + 4f, rect.yMax - 8f, (rect.width - 8f) * left, 4f);
                    Fill(bar, Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.35f, 0.8f, 0.25f), left));
                }
            }

            if (key != null)
            {
                GUI.Label(new Rect(rect.x + 3f, rect.y + 1f, 20f, 16f), key);
            }
            if (!string.IsNullOrEmpty(corner))
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.35f);
                GUI.Label(new Rect(rect.x + 3f, rect.yMax - 18f, rect.width - 6f, 16f), corner);
                GUI.color = previous;
            }
        }

        /// <summary>
        /// An item drawn greyed out over an empty slot (GL58): what a station is about to make.
        /// Draw it after the slot itself.
        /// </summary>
        public static void Ghost(Rect rect, ItemDefinition item, int count)
        {
            if (item == null)
            {
                return;
            }
            EnsureStyles();
            Color previous = GUI.color;
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.4f);
            Rect inner = Inset(rect, 6f);
            if (item.Icon != null)
            {
                GUI.DrawTexture(inner, item.Icon, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(inner, item.DisplayName, nameStyle);
            }
            if (count > 1)
            {
                GUI.Label(Inset(rect, 3f), count.ToString(), countStyle);
            }
            GUI.color = previous;
        }

        private static void EnsureStyles()
        {
            if (nameStyle != null)
            {
                return;
            }
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 11,
            };
            countStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                fontStyle = FontStyle.Bold,
            };
        }

        internal static Rect Inset(Rect rect, float by)
        {
            return new Rect(rect.x + by, rect.y + by, rect.width - by * 2f, rect.height - by * 2f);
        }

        internal static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        internal static void Outline(Rect rect, Color color, float width)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }
    }
}
