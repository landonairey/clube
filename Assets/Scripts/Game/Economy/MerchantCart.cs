using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The list inventory's trade cart at a merchant (prototype): one panel along the bottom of the trade screen, what the
    /// player clicked to sell on its left and what they clicked to buy on its right, each line with how much of the stack
    /// goes. Click a line's Qty to set the amount on a slider or by typing it (whole items only for things that don't stack;
    /// a single one that doesn't stack has no amount to set); ✕ takes a line out. The total is the trade's net: what the
    /// player gets for what they sell less what they pay for what they buy. Carrying the trade out is the merchant panel's
    /// job: <see cref="Draw"/> says when it was asked for.
    /// </summary>
    public sealed class MerchantCart
    {
        /// <summary>Which way a line goes.</summary>
        public enum Side
        {
            Sell,
            Buy,
        }

        /// <summary>One stack in the cart: its side, its slot (in the player's items or the trader's stock), the item it held
        /// when added (a changed slot drops out), and how much goes.</summary>
        public struct Line
        {
            public Side Side;
            public int Slot;
            public ItemDefinition Item;
            public float Amount;
        }

        /// <summary>What the cart asks its panel about a line.</summary>
        public interface ISource
        {
            /// <summary>The stack the line takes from (the player's slot, or the trader's stock).</summary>
            ItemStack StackOf(Line line);

            /// <summary>Coins for an amount of the line's stack: what the trader pays, or what they ask.</summary>
            float CoinsFor(Line line, float amount);
        }

        private const float RowHeight = 44f;
        private const float HeaderHeight = 30f;
        private const float EditorHeight = 40f;
        private const float FooterHeight = 48f;

        private static readonly Color RowEven = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color RowOdd = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color Editing = new Color(1f, 0.85f, 0.3f, 0.18f);
        private static readonly Color Gain = new Color(0.5f, 0.9f, 0.45f);
        private static readonly Color Cost = new Color(1f, 0.55f, 0.45f);

        private readonly List<Line> lines = new List<Line>();
        private int editing = -1;
        private string typed;
        private float typedFor = -1f;
        private Vector2 sellScroll, buyScroll;
        private GUIStyle nameStyle, rightStyle, headerStyle;

        /// <summary>What's in the cart.</summary>
        public IReadOnlyList<Line> Lines => lines;

        /// <summary>True when the slot's stack is in the cart on that side.</summary>
        public bool Contains(Side side, int slot)
        {
            return IndexOf(side, slot) >= 0;
        }

        /// <summary>Puts an amount of a stack in the cart; a stack already there on that side is left as it is.</summary>
        public void Add(Side side, int slot, ItemDefinition item, float amount)
        {
            if (item == null || amount <= ItemStack.PartEpsilon || Contains(side, slot))
            {
                return;
            }
            lines.Add(new Line { Side = side, Slot = slot, Item = item, Amount = amount });
        }

        /// <summary>Empties the cart (nothing moves until the trade is made).</summary>
        public void Clear()
        {
            lines.Clear();
            editing = -1;
        }

        /// <summary>The trade's net in coins: positive when the player comes out ahead.</summary>
        public float Net(ISource source)
        {
            float net = 0f;
            foreach (Line line in lines)
            {
                float coins = source.CoinsFor(line, line.Amount);
                net += line.Side == Side.Sell ? coins : -coins;
            }
            return net;
        }

        /// <summary>
        /// Draws the cart in <paramref name="area"/>: what's sold on the left and bought on the right (each list scrolling
        /// when long), the amount editor of the line whose Qty was clicked, and the net with the trade button. True when the
        /// trade was asked for.
        /// </summary>
        public bool Draw(Rect area, ISource source)
        {
            EnsureStyles();
            Prune(source);
            ItemSlotGui.Fill(area, new Color(0f, 0f, 0f, 0.25f));
            ItemSlotGui.Outline(area, new Color(1f, 1f, 1f, 0.15f), 1f);
            var inner = new Rect(area.x + 8f, area.y + 6f, area.width - 16f, area.height - 12f);
            float bottom = inner.yMax - FooterHeight - (editing >= 0 ? EditorHeight + 4f : 0f);
            float half = (inner.width - 12f) * 0.5f;
            var sellArea = new Rect(inner.x, inner.y, half, bottom - inner.y);
            var buyArea = new Rect(inner.x + half + 12f, inner.y, half, sellArea.height);
            int remove = -1;
            DrawSide(sellArea, Side.Sell, "Selling", "Click your items to sell them.", source, ref sellScroll, ref remove);
            DrawSide(buyArea, Side.Buy, "Buying", "Click their items to buy them.", source, ref buyScroll, ref remove);
            if (remove >= 0)
            {
                RemoveAt(remove);
            }
            if (editing >= 0 && editing < lines.Count)
            {
                DrawEditor(new Rect(inner.x, bottom + 4f, inner.width, EditorHeight), source);
            }

            float net = Net(source);
            var footer = new Rect(inner.x, inner.yMax - FooterHeight + 4f, inner.width, FooterHeight - 4f);
            string what = lines.Count == 0 ? "Nothing in the cart"
                : net >= 0f ? $"You get  {ListColumns.Coins(net)}" : $"You pay  {ListColumns.Coins(-net)}";
            headerStyle.normal.textColor = lines.Count == 0 ? Color.white : net >= 0f ? Gain : Cost;
            GUI.Label(new Rect(footer.x, footer.y, footer.width - 330f, footer.height), what, headerStyle);
            headerStyle.normal.textColor = Color.white;
            if (lines.Count > 0 && GameUi.Button(new Rect(footer.xMax - 320f, footer.y, 110f, footer.height), "Clear"))
            {
                Clear();
            }
            GUI.enabled = lines.Count > 0;
            bool trade = GameUi.Button(new Rect(footer.xMax - 200f, footer.y, 200f, footer.height), "Trade");
            GUI.enabled = true;
            return trade;
        }

        private void DrawSide(Rect area, Side side, string title, string empty, ISource source, ref Vector2 scroll, ref int remove)
        {
            int count = 0;
            foreach (Line line in lines)
            {
                count += line.Side == side ? 1 : 0;
            }
            GUI.Label(new Rect(area.x, area.y, area.width, HeaderHeight), $"{title} ({count})", headerStyle);
            var list = new Rect(area.x, area.y + HeaderHeight, area.width, Mathf.Max(RowHeight, area.height - HeaderHeight));
            ItemSlotGui.Fill(list, new Color(0f, 0f, 0f, 0.2f));
            if (count == 0)
            {
                GUI.Label(ItemSlotGui.Inset(list, 10f), empty);
                return;
            }
            float content = count * RowHeight;
            bool scrolls = content > list.height;
            float width = list.width - (scrolls ? 18f : 0f);
            scroll = GUI.BeginScrollView(list, scroll, new Rect(0f, 0f, width, content), false, scrolls);
            const float qtyWidth = 96f, coinWidth = 90f, qualWidth = 76f, removeWidth = 34f;
            int row = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                Line line = lines[i];
                if (line.Side != side)
                {
                    continue;
                }
                ItemStack stack = source.StackOf(line);
                var rect = new Rect(0f, row * RowHeight, width, RowHeight);
                ItemSlotGui.Fill(rect, row % 2 == 0 ? RowEven : RowOdd);
                row++;
                if (i == editing)
                {
                    ItemSlotGui.Fill(rect, Editing);
                }
                var icon = new Rect(rect.x + 6f, rect.y + 4f, RowHeight - 8f, RowHeight - 8f);
                if (stack.Item.Icon != null)
                {
                    GUI.DrawTexture(icon, stack.Item.Icon, ScaleMode.ScaleToFit);
                }
                float right = rect.xMax - removeWidth;
                float nameX = icon.xMax + 8f;
                GUI.Label(new Rect(nameX, rect.y, right - coinWidth - qtyWidth - qualWidth - nameX, rect.height), stack.Item.DisplayName, nameStyle);
                Color colour = Color.white;
                string quality = ItemSlotGui.ShowsPurity(stack) ? ItemSlotGui.PurityLabel(stack, out colour) : Clube.Core.QualityText.Percent(ListColumns.QualityOf(stack));
                rightStyle.normal.textColor = colour;
                GUI.Label(new Rect(right - coinWidth - qtyWidth - qualWidth, rect.y, qualWidth - 8f, rect.height), quality, rightStyle);
                rightStyle.normal.textColor = Color.white;
                // The Qty is a button that opens the amount editor, unless there's no amount to choose (one that doesn't stack).
                string amount = line.Amount < stack.Amount - ItemStack.PartEpsilon
                    ? $"{ListColumns.Amount(line.Amount)}/{ListColumns.Amount(stack.Amount)}" : ListColumns.Amount(line.Amount);
                var qty = new Rect(right - coinWidth - qtyWidth, rect.y + 6f, qtyWidth - 8f, rect.height - 12f);
                if (!HasChoice(stack))
                {
                    GUI.Label(qty, amount, rightStyle);
                }
                else if (GUI.Button(qty, amount))
                {
                    editing = editing == i ? -1 : i;
                    typedFor = -1f;
                }
                float coins = source.CoinsFor(line, line.Amount);
                rightStyle.normal.textColor = side == Side.Sell ? Gain : Cost;
                GUI.Label(new Rect(right - coinWidth, rect.y, coinWidth - 8f, rect.height), (side == Side.Sell ? "+" : "-") + ListColumns.Coins(coins), rightStyle);
                rightStyle.normal.textColor = Color.white;
                if (GUI.Button(new Rect(right + 2f, rect.y + 8f, removeWidth - 6f, rect.height - 16f), "✕"))
                {
                    remove = i;
                }
            }
            GUI.EndScrollView();
        }

        // The amount of the line being edited: a slider from the least that trades to the whole stack, and a box to type it.
        private void DrawEditor(Rect rect, ISource source)
        {
            Line line = lines[editing];
            ItemStack stack = source.StackOf(line);
            float most = stack.Amount;
            float least = Least(stack);
            ItemSlotGui.Fill(rect, Editing);
            string verb = line.Side == Side.Sell ? "sell" : "buy";
            GUI.Label(new Rect(rect.x + 8f, rect.y, 300f, rect.height), $"How many {stack.Item.DisplayName} to {verb}", nameStyle);
            const float boxWidth = 90f, doneWidth = 80f;
            float sliderX = rect.x + 316f;
            float sliderWidth = Mathf.Max(60f, rect.xMax - sliderX - boxWidth - doneWidth - 24f);
            float amount = Snap(GUI.HorizontalSlider(new Rect(sliderX, rect.y + rect.height * 0.5f - 6f, sliderWidth, 16f), line.Amount, least, most), most, Whole(stack));

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
                    amount = Mathf.Clamp(Whole(stack) ? Mathf.Round(read) : read, least, most);
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

        // Things that don't stack (a tool) trade as whole items only.
        private static bool Whole(ItemStack stack)
        {
            return stack.Item.MaxStack <= 1;
        }

        private static float Least(ItemStack stack)
        {
            float most = stack.Amount;
            return Whole(stack) ? Mathf.Min(most, 1f) : Mathf.Min(most, most < 10f ? 0.01f : 1f);
        }

        // True when there is an amount to choose: more than the least that trades.
        private static bool HasChoice(ItemStack stack)
        {
            return stack.Amount > Least(stack) + ItemStack.PartEpsilon;
        }

        // Whole items for what doesn't stack or from a big stack; to the hundredth under 10; the whole amount at the top.
        private static float Snap(float amount, float most, bool whole)
        {
            if (amount >= most - 0.005f)
            {
                return most;
            }
            return whole || most >= 10f ? Mathf.Max(1f, Mathf.Round(amount)) : Mathf.Round(amount * 100f) / 100f;
        }

        // Lines whose stack traded, moved or changed drop out; amounts never pass what the stack holds.
        private void Prune(ISource source)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                Line line = lines[i];
                ItemStack stack = source.StackOf(line);
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

        private int IndexOf(Side side, int slot)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Side == side && lines[i].Slot == slot)
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
