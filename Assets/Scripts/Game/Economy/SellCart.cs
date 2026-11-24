using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The list inventory's selling cart at a merchant (prototype): what the player has clicked to sell, slot by slot with
    /// how much of each, under their item list. Click a line's Qty to set how much of the stack goes, on a slider or typed;
    /// ✕ takes a line out. Selling the cart is the merchant panel's job: <see cref="Draw"/> says when it was asked for.
    /// </summary>
    public sealed class SellCart
    {
        /// <summary>One stack in the cart: its slot, the item it held when added (a changed slot drops out), and how much goes.</summary>
        public struct Line
        {
            public int Slot;
            public ItemDefinition Item;
            public float Amount;
        }

        private const float RowHeight = 44f;
        private const float HeaderHeight = 32f;
        private const float EditorHeight = 40f;
        private const float FooterHeight = 48f;

        private static readonly Color RowEven = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color RowOdd = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color Editing = new Color(1f, 0.85f, 0.3f, 0.18f);

        private readonly List<Line> lines = new List<Line>();
        private int editing = -1;
        private string typed;
        private float typedFor = -1f;
        private Vector2 scroll;
        private GUIStyle nameStyle, rightStyle, headerStyle;

        /// <summary>What's in the cart.</summary>
        public IReadOnlyList<Line> Lines => lines;

        /// <summary>True when the slot's stack is in the cart.</summary>
        public bool Contains(int slot)
        {
            return IndexOf(slot) >= 0;
        }

        /// <summary>Puts a whole stack in the cart; a stack already there is left as it is.</summary>
        public void Add(Inventory items, int slot)
        {
            if (slot < 0 || slot >= items.SlotCount || items[slot].IsEmpty || Contains(slot))
            {
                return;
            }
            lines.Add(new Line { Slot = slot, Item = items[slot].Item, Amount = items[slot].Amount });
        }

        /// <summary>Empties the cart (nothing moves: the stacks never left the inventory).</summary>
        public void Clear()
        {
            lines.Clear();
            editing = -1;
        }

        /// <summary>
        /// Draws the cart in <paramref name="area"/>: its lines (scrolling when long), the amount editor of the line whose Qty
        /// was clicked, and the total with the sell button. <paramref name="price"/> is what the trader pays for an amount of
        /// a stack (0 when they don't buy it). True when Sell was pressed.
        /// </summary>
        public bool Draw(Rect area, Inventory items, Func<ItemStack, float, float> price)
        {
            EnsureStyles();
            Prune(items);
            float total = 0f;
            foreach (Line line in lines)
            {
                total += price(items[line.Slot], line.Amount);
            }

            GUI.Label(new Rect(area.x, area.y, area.width - 100f, HeaderHeight), $"Cart ({lines.Count})", headerStyle);
            if (lines.Count > 0 && GameUi.Button(new Rect(area.xMax - 90f, area.y + 2f, 90f, HeaderHeight - 4f), "Clear"))
            {
                Clear();
            }
            float bottom = area.yMax - FooterHeight - (editing >= 0 ? EditorHeight + 4f : 0f);
            var listArea = new Rect(area.x, area.y + HeaderHeight + 2f, area.width, Mathf.Max(RowHeight, bottom - area.y - HeaderHeight - 6f));
            ItemSlotGui.Fill(listArea, new Color(0f, 0f, 0f, 0.25f));
            if (lines.Count == 0)
            {
                GUI.Label(ItemSlotGui.Inset(listArea, 10f), "Click your items to put them in the cart.");
            }
            else
            {
                DrawLines(listArea, items, price);
            }
            if (editing >= 0 && editing < lines.Count)
            {
                DrawEditor(new Rect(area.x, listArea.yMax + 4f, area.width, EditorHeight), items);
            }

            var footer = new Rect(area.x, area.yMax - FooterHeight + 4f, area.width, FooterHeight - 4f);
            GUI.Label(new Rect(footer.x, footer.y, footer.width - 200f, footer.height), $"Total  {ListColumns.Coins(total)}", headerStyle);
            GUI.enabled = total > 0f;
            bool sell = GameUi.Button(new Rect(footer.xMax - 190f, footer.y, 190f, footer.height), $"Sell cart\n{ListColumns.Coins(total)}");
            GUI.enabled = true;
            return sell;
        }

        private void DrawLines(Rect area, Inventory items, Func<ItemStack, float, float> price)
        {
            float content = lines.Count * RowHeight;
            bool scrolls = content > area.height;
            float width = area.width - (scrolls ? 18f : 0f);
            scroll = GUI.BeginScrollView(area, scroll, new Rect(0f, 0f, width, content), false, scrolls);
            const float qtyWidth = 90f, coinWidth = 90f, qualWidth = 80f, removeWidth = 34f;
            int remove = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                Line line = lines[i];
                ItemStack stack = items[line.Slot];
                var row = new Rect(0f, i * RowHeight, width, RowHeight);
                ItemSlotGui.Fill(row, i % 2 == 0 ? RowEven : RowOdd);
                if (i == editing)
                {
                    ItemSlotGui.Fill(row, Editing);
                }
                var icon = new Rect(row.x + 6f, row.y + 4f, RowHeight - 8f, RowHeight - 8f);
                if (stack.Item.Icon != null)
                {
                    GUI.DrawTexture(icon, stack.Item.Icon, ScaleMode.ScaleToFit);
                }
                float right = row.xMax - removeWidth;
                float nameX = icon.xMax + 8f;
                GUI.Label(new Rect(nameX, row.y, right - coinWidth - qtyWidth - qualWidth - nameX, row.height), stack.Item.DisplayName, nameStyle);
                Color colour = Color.white;
                string quality = ItemSlotGui.ShowsPurity(stack) ? ItemSlotGui.PurityLabel(stack, out colour) : Clube.Core.QualityText.Percent(ListColumns.QualityOf(stack));
                rightStyle.normal.textColor = colour;
                GUI.Label(new Rect(right - coinWidth - qtyWidth - qualWidth, row.y, qualWidth - 8f, row.height), quality, rightStyle);
                rightStyle.normal.textColor = Color.white;
                // The Qty is a button: it opens the amount editor for this line.
                string amount = line.Amount < stack.Amount - ItemStack.PartEpsilon
                    ? $"{ListColumns.Amount(line.Amount)}/{ListColumns.Amount(stack.Amount)}" : ListColumns.Amount(line.Amount);
                if (GUI.Button(new Rect(right - coinWidth - qtyWidth, row.y + 6f, qtyWidth - 8f, row.height - 12f), amount))
                {
                    editing = editing == i ? -1 : i;
                    typedFor = -1f;
                }
                GUI.Label(new Rect(right - coinWidth, row.y, coinWidth - 8f, row.height), ListColumns.Coins(price(stack, line.Amount)), rightStyle);
                if (GUI.Button(new Rect(right + 2f, row.y + 8f, removeWidth - 6f, row.height - 16f), "✕"))
                {
                    remove = i;
                }
            }
            GUI.EndScrollView();
            if (remove >= 0)
            {
                RemoveAt(remove);
            }
        }

        // The amount of the line being edited: a slider from the least that sells to the whole stack, and a box to type it.
        private void DrawEditor(Rect rect, Inventory items)
        {
            Line line = lines[editing];
            ItemStack stack = items[line.Slot];
            float most = stack.Amount;
            float least = Mathf.Min(most, most < 10f ? 0.01f : 1f);
            ItemSlotGui.Fill(rect, Editing);
            GUI.Label(new Rect(rect.x + 8f, rect.y, 160f, rect.height), $"How many {stack.Item.DisplayName}", nameStyle);
            const float boxWidth = 90f, doneWidth = 80f;
            float sliderX = rect.x + 176f;
            float sliderWidth = Mathf.Max(60f, rect.xMax - sliderX - boxWidth - doneWidth - 24f);
            float amount = Snap(GUI.HorizontalSlider(new Rect(sliderX, rect.y + rect.height * 0.5f - 6f, sliderWidth, 16f), line.Amount, least, most), most);

            // The box shows the amount until typed in; a typed number that reads sets the amount, clamped to the stack.
            if (typed == null || !Mathf.Approximately(typedFor, amount))
            {
                typed = ListColumns.Amount(amount);
                typedFor = amount;
            }
            string edited = GUI.TextField(new Rect(sliderX + sliderWidth + 12f, rect.y + 6f, boxWidth, rect.height - 12f), typed);
            if (edited != typed)
            {
                typed = edited;
                if (float.TryParse(edited, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float read) && read > 0f)
                {
                    amount = Mathf.Clamp(read, least, most);
                    typedFor = amount;
                }
            }
            line.Amount = amount;
            lines[editing] = line;
            if (GameUi.Button(new Rect(rect.xMax - doneWidth - 4f, rect.y + 4f, doneWidth, rect.height - 8f), "Done"))
            {
                editing = -1;
            }
        }

        // Whole items from a big stack; to the hundredth under 10, and the whole amount at the top of the slider.
        private static float Snap(float amount, float most)
        {
            if (amount >= most - 0.005f)
            {
                return most;
            }
            return most < 10f ? Mathf.Round(amount * 100f) / 100f : Mathf.Round(amount);
        }

        // Lines whose stack sold, moved or changed drop out; amounts never pass what the stack holds.
        private void Prune(Inventory items)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                Line line = lines[i];
                ItemStack stack = line.Slot < items.SlotCount ? items[line.Slot] : ItemStack.Empty;
                if (stack.IsEmpty || stack.Item != line.Item)
                {
                    RemoveAt(i);
                    continue;
                }
                if (line.Amount > stack.Amount)
                {
                    line.Amount = stack.Amount;
                    lines[i] = line;
                }
            }
        }

        private void RemoveAt(int index)
        {
            lines.RemoveAt(index);
            if (editing == index)
            {
                editing = -1;
            }
            else if (editing > index)
            {
                editing--;
            }
        }

        private int IndexOf(int slot)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Slot == slot)
                {
                    return i;
                }
            }
            return -1;
        }

        private void EnsureStyles()
        {
            if (nameStyle != null)
            {
                return;
            }
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false };
            rightStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = GameUi.Font(16) };
        }
    }
}
