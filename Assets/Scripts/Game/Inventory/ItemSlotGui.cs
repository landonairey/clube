using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Draws one inventory slot with IMGUI (GL7): a dark square, the item's icon (or its
    /// name when it has none), its count, and a bright border when selected. Shared by the
    /// <see cref="Hotbar"/> and the <see cref="InventoryScreen"/> so slots look the same in both.
    /// </summary>
    public static class ItemSlotGui
    {
        private static readonly Color Background = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color SelectedBorder = new Color(1f, 0.85f, 0.3f, 1f);

        private static GUIStyle nameStyle;
        private static GUIStyle countStyle;

        public static void Draw(Rect rect, ItemStack stack, bool selected, string key = null)
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
                if (stack.Count > 1)
                {
                    GUI.Label(Inset(rect, 3f), stack.Count.ToString(), countStyle);
                }
            }

            if (key != null)
            {
                GUI.Label(new Rect(rect.x + 3f, rect.y + 1f, 20f, 16f), key);
            }
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

        private static Rect Inset(Rect rect, float by)
        {
            return new Rect(rect.x + by, rect.y + by, rect.width - by * 2f, rect.height - by * 2f);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Outline(Rect rect, Color color, float width)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }
    }
}
