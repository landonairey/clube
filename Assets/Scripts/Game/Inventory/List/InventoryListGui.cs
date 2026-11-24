using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>What the mouse did to a list inventory in one IMGUI event (<see cref="InventoryListGui.Draw"/>).</summary>
    public readonly struct ListResult
    {
        public ListResult(InventoryDragKind kind, ListRow row, int count, Vector2 position)
        {
            Kind = kind;
            Row = row;
            Count = count;
            Position = position;
        }

        public InventoryDragKind Kind { get; }

        /// <summary>The row clicked or dragged; null for nothing.</summary>
        public ListRow Row { get; }

        /// <summary>How many items the click or drag carries: all of the stack, half with Shift, one with Ctrl.</summary>
        public int Count { get; }

        public Vector2 Position { get; }

        /// <summary>The row's first slot, -1 for none.</summary>
        public int Slot => Row != null ? Row.Slot : -1;

        /// <summary>The same as a grid's result, so the station screens handle a list the way they handled the grid.</summary>
        public InventoryDragResult AsDrag => new InventoryDragResult(Kind, Slot, Count, Position);
    }

    /// <summary>
    /// A list inventory (prototype, in the style of a role-playing game's inventory): one line per stack with its icon, name,
    /// value, quality and amount under column headers that sort it (click again to turn the order round). Stacks of one item
    /// gather into a collection (<see cref="InventoryListing"/>) whose arrow opens it; the wheel scrolls, and a scroll bar shows
    /// when the list is longer than the panel. A click selects a row and goes back to the screen, as does a right click; a row
    /// dragged onto another stack of its item joins it, and one let go off the list goes back to the screen to place.
    /// </summary>
    public sealed class InventoryListGui
    {
        public const float RowHeight = 56f;
        public const float HeaderHeight = 40f;
        private const float ArrowWidth = 30f;
        private float ColumnWidth = 104f;
        private const float ScrollBarWidth = 18f;
        private const float DragDistance = 6f;
        private const float Indent = 26f;

        private static readonly Color RowEven = new Color(1f, 1f, 1f, 0.03f);
        private static readonly Color RowOdd = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color EquippedColour = new Color(0.95f, 0.75f, 0.25f, 0.22f);
        private static readonly Color SelectedColour = new Color(0.45f, 0.7f, 1f, 0.3f);
        private static readonly Color HeaderColour = new Color(0f, 0f, 0f, 0.35f);

        private readonly HashSet<ItemDefinition> expanded = new HashSet<ItemDefinition>();
        private List<ListRow> rows = new List<ListRow>();
        private Vector2 scroll;
        private ListSort sort = ListSort.Name;
        private bool descending;
        private int pressedSlot = -1;
        private bool pressedCollection;
        private Vector2 pressedAt;
        private bool dragging;
        private int carry;
        private int selectedSlot = -1;
        private ItemDefinition selectedItem;
        private bool selectedCollection;
        private GUIStyle nameStyle, columnStyle, headerStyle, arrowStyle;

        /// <summary>Which slots are listed; null for all.</summary>
        public Func<int, bool> Include { get; set; }

        /// <summary>Which slots show as equipped (highlighted); null for none.</summary>
        public Func<int, bool> Equipped { get; set; }

        /// <summary>The Val column for a slot; null reads the merchant's price (<see cref="ListColumns.ValueOf"/>).</summary>
        public Func<int, float> Value { get; set; }

        /// <summary>Rows show their description beside the mouse (<see cref="InventoryGridGui.DrawTooltip"/>).</summary>
        public bool HoverTooltips { get; set; } = true;

        /// <summary>A row is being dragged.</summary>
        public bool IsDragging => dragging && pressedSlot >= 0;

        /// <summary>The selected row, if it is still listed.</summary>
        public ListRow Selected
        {
            get
            {
                foreach (ListRow row in rows)
                {
                    if (selectedCollection ? row.IsCollection && row.Item == selectedItem : !row.IsCollection && row.Slot == selectedSlot && row.Item == selectedItem)
                    {
                        return row;
                    }
                }
                return null;
            }
        }

        /// <summary>Forgets the selection, a press and a drag (a screen opening or closing).</summary>
        public void Reset()
        {
            pressedSlot = -1;
            dragging = false;
            ClearSelection();
        }

        public void ClearSelection()
        {
            selectedSlot = -1;
            selectedItem = null;
            selectedCollection = false;
        }

        /// <summary>Handles the mouse and draws the list in <paramref name="area"/>, its column headers at the top.</summary>
        public ListResult Draw(Rect area, Inventory items)
        {
            EnsureStyles();
            Event current = Event.current;
            if (current.type == EventType.Layout || rows == null)
            {
                Rebuild(items);
            }

            // Narrower number columns on a narrow list, so names keep their room.
            ColumnWidth = Mathf.Clamp(area.width * 0.13f, 78f, 110f);
            var header = new Rect(area.x, area.y, area.width, HeaderHeight);
            var list = new Rect(area.x, area.y + HeaderHeight, area.width, area.height - HeaderHeight);
            float contentHeight = rows.Count * RowHeight;
            bool scrolls = contentHeight > list.height;
            float width = scrolls ? list.width - ScrollBarWidth : list.width;
            if (!scrolls)
            {
                scroll = Vector2.zero;
            }
            DrawHeader(header, width, items);

            ListResult result = HandleMouse(list, width, items);
            int hoverIndex = list.Contains(current.mousePosition) && current.mousePosition.x < list.x + width ? RowAt(list, current.mousePosition) : -1;

            scroll = GUI.BeginScrollView(list, scroll, new Rect(0f, 0f, width, Mathf.Max(contentHeight, list.height)), false, false);
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            int last = Mathf.Min(rows.Count - 1, Mathf.CeilToInt((scroll.y + list.height) / RowHeight));
            for (int i = first; i <= last; i++)
            {
                DrawRow(new Rect(0f, i * RowHeight, width, RowHeight), i, rows[i], items, i == hoverIndex);
            }
            if (rows.Count == 0)
            {
                GUI.Label(new Rect(12f, 8f, width - 24f, 30f), "Nothing here.");
            }
            GUI.EndScrollView();

            if (hoverIndex >= 0 && hoverIndex < rows.Count && HoverTooltips && !IsDragging)
            {
                InventoryGridGui.Hover(items[rows[hoverIndex].Slot]);
            }
            return result;
        }

        /// <summary>The row being dragged, under the mouse; draw last, over everything.</summary>
        public void DrawCarried(Inventory items)
        {
            if (!IsDragging || pressedSlot >= items.SlotCount || items[pressedSlot].IsEmpty)
            {
                return;
            }
            ItemStack stack = items[pressedSlot];
            int count = Mathf.Min(carry, stack.Count);
            ItemStack shown = count < stack.Count && count > 0 ? new ItemStack(stack.Item, count, stack.Contents, stack.Wear, stack.Quality) : stack;
            Vector2 mouse = Event.current.mousePosition;
            const float side = 72f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            ItemSlotGui.Draw(new Rect(mouse.x - side * 0.5f, mouse.y - side * 0.5f, side, side), shown, true);
            GUI.color = previous;
        }

        private void Rebuild(Inventory items)
        {
            Func<int, float> value = Value ?? (slot => ListColumns.ValueOf(items[slot]));
            rows = InventoryListing.Build(items, Include, value, slot => ListColumns.QualityOf(items[slot]), expanded, sort, descending, Equipped);
        }

        private int RowAt(Rect list, Vector2 point)
        {
            int index = Mathf.FloorToInt((point.y - list.y + scroll.y) / RowHeight);
            return index >= 0 && index < rows.Count ? index : -1;
        }

        private ListResult HandleMouse(Rect list, float width, Inventory items)
        {
            Event current = Event.current;
            var none = new ListResult(InventoryDragKind.None, null, 0, current.mousePosition);
            bool overRows = list.Contains(current.mousePosition) && current.mousePosition.x < list.x + width;
            switch (current.type)
            {
                case EventType.MouseDown when overRows:
                {
                    int index = RowAt(list, current.mousePosition);
                    if (index < 0)
                    {
                        break;
                    }
                    ListRow row = rows[index];
                    current.Use();
                    if (current.button == 1)
                    {
                        return new ListResult(InventoryDragKind.RightClicked, row, items[row.Slot].Count, current.mousePosition);
                    }
                    if (current.button != 0)
                    {
                        break;
                    }
                    float arrowX = list.x + (row.IsChild ? Indent : 0f);
                    if (row.IsCollection && current.mousePosition.x < arrowX + ArrowWidth)
                    {
                        if (!expanded.Remove(row.Item))
                        {
                            expanded.Add(row.Item);
                        }
                        Rebuild(items);
                        break;
                    }
                    pressedSlot = row.Slot;
                    pressedCollection = row.IsCollection;
                    pressedAt = current.mousePosition;
                    dragging = false;
                    carry = Carry(items[row.Slot], current);
                    break;
                }
                case EventType.MouseDrag when pressedSlot >= 0:
                    dragging |= (current.mousePosition - pressedAt).sqrMagnitude > DragDistance * DragDistance;
                    current.Use();
                    break;
                case EventType.MouseUp when pressedSlot >= 0:
                {
                    int from = pressedSlot;
                    bool collection = pressedCollection;
                    bool dragged = dragging;
                    pressedSlot = -1;
                    dragging = false;
                    current.Use();
                    if (from >= items.SlotCount || items[from].IsEmpty)
                    {
                        break;
                    }
                    int count = Mathf.Max(1, Mathf.Min(carry, items[from].Count));
                    ListRow pressedRow = Find(from, collection);
                    int index = overRows ? RowAt(list, current.mousePosition) : -1;
                    ListRow target = index >= 0 ? rows[index] : null;
                    if (!dragged)
                    {
                        if (target != null && target.Slot == from && target.IsCollection == collection)
                        {
                            // A second click on the selected row lets it go (closing the inventory's details).
                            if (Selected == target)
                            {
                                ClearSelection();
                                return new ListResult(InventoryDragKind.Clicked, target, count, current.mousePosition);
                            }
                            selectedSlot = target.Slot;
                            selectedItem = target.Item;
                            selectedCollection = target.IsCollection;
                            return new ListResult(InventoryDragKind.Clicked, target, count, current.mousePosition);
                        }
                        break;
                    }
                    if (target == null)
                    {
                        if (!overRows && pressedRow != null)
                        {
                            return new ListResult(InventoryDragKind.DroppedOutside, pressedRow, count, current.mousePosition);
                        }
                        break;
                    }
                    // Dropped on another stack: a repair item mends the tool (GL286), the same item joins it (combining
                    // stacks by hand), anything else stays where it was: the list has no places to swap.
                    int onto = target.Slot;
                    if (onto != from)
                    {
                        if (ToolRepair.CanRepair(items[onto], items[from].Item))
                        {
                            ToolRepair.Repair(items, onto, items, from, count);
                        }
                        else if (items[onto].Item == items[from].Item)
                        {
                            items.Move(from, onto, count);
                        }
                        Rebuild(items);
                    }
                    break;
                }
            }
            return none;
        }

        private ListRow Find(int slot, bool collection)
        {
            foreach (ListRow row in rows)
            {
                if (row.Slot == slot && row.IsCollection == collection)
                {
                    return row;
                }
            }
            return null;
        }

        // All of it, half with Shift, one with Ctrl, as the grid's drags carry (GL191).
        private static int Carry(ItemStack stack, Event current)
        {
            Keyboard keyboard = Keyboard.current;
            bool control = current.control || (keyboard != null && keyboard.ctrlKey.isPressed);
            bool shift = current.shift || (keyboard != null && keyboard.shiftKey.isPressed);
            return control ? 1 : shift ? Mathf.Max(1, (stack.Count + 1) / 2) : Mathf.Max(1, stack.Count);
        }

        private void DrawHeader(Rect header, float width, Inventory items)
        {
            ItemSlotGui.Fill(header, HeaderColour);
            float right = header.x + width;
            SortButton(new Rect(header.x + ArrowWidth, header.y, width - ArrowWidth - ColumnWidth * 3f, header.height), "Name", ListSort.Name, TextAnchor.MiddleLeft, items);
            SortButton(new Rect(right - ColumnWidth * 3f, header.y, ColumnWidth, header.height), "Val", ListSort.Value, TextAnchor.MiddleRight, items);
            SortButton(new Rect(right - ColumnWidth * 2f, header.y, ColumnWidth, header.height), "Qual", ListSort.Quality, TextAnchor.MiddleRight, items);
            SortButton(new Rect(right - ColumnWidth, header.y, ColumnWidth, header.height), "Qty", ListSort.Quantity, TextAnchor.MiddleRight, items);
        }

        // A column's header sorts by it; again turns the order round. Names start A to Z, numbers high to low.
        private void SortButton(Rect rect, string label, ListSort by, TextAnchor anchor, Inventory items)
        {
            string arrow = sort == by ? (descending ? " ▼" : " ▲") : "";
            headerStyle.alignment = anchor;
            GUI.Label(new Rect(rect.x, rect.y, rect.width - 12f, rect.height), label + arrow, headerStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                if (sort == by)
                {
                    descending = !descending;
                }
                else
                {
                    sort = by;
                    descending = by != ListSort.Name;
                }
                Rebuild(items);
            }
        }

        private void DrawRow(Rect rect, int index, ListRow row, Inventory items, bool hovered)
        {
            ItemStack stack = items[row.Slot];
            if (stack.IsEmpty)
            {
                return;
            }
            ItemSlotGui.Fill(rect, index % 2 == 0 ? RowEven : RowOdd);
            if (row.Equipped)
            {
                ItemSlotGui.Fill(rect, EquippedColour);
            }
            bool selected = selectedCollection ? row.IsCollection && row.Item == selectedItem : !row.IsCollection && row.Slot == selectedSlot && row.Item == selectedItem;
            if (selected || (pressedSlot == row.Slot && pressedCollection == row.IsCollection))
            {
                ItemSlotGui.Fill(rect, SelectedColour);
            }
            if (hovered)
            {
                ItemSlotGui.Outline(rect, new Color(1f, 1f, 1f, 0.3f), 1f);
            }

            float x = rect.x + (row.IsChild ? Indent : 0f);
            if (row.IsCollection)
            {
                GUI.Label(new Rect(x, rect.y, ArrowWidth, rect.height), row.Expanded ? "▼" : "▶", arrowStyle);
            }
            x += ArrowWidth;
            var icon = new Rect(x, rect.y + 5f, rect.height - 10f, rect.height - 10f);
            if (stack.Item.Icon != null)
            {
                GUI.DrawTexture(icon, stack.Item.Icon, ScaleMode.ScaleToFit);
            }
            if (ItemSlotGui.WearBar(stack, out float left, out _) && !row.IsCollection)
            {
                ItemSlotGui.Fill(new Rect(icon.x, icon.yMax - 5f, icon.width, 4f), new Color(0.2f, 0.2f, 0.22f));
                ItemSlotGui.Fill(new Rect(icon.x, icon.yMax - 5f, icon.width * left, 4f), Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.35f, 0.8f, 0.25f), left));
            }
            x = icon.xMax + 10f;
            float right = rect.x + rect.width;
            string name = row.IsCollection ? $"{row.Item.DisplayName}  ({row.Slots.Count})" : row.Item.DisplayName;
            if (row.Equipped && !row.IsCollection)
            {
                name += "  <color=#f2c040>(equipped)</color>";
            }
            GUI.Label(new Rect(x, rect.y, right - ColumnWidth * 3f - x, rect.height), name, nameStyle);

            columnStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(right - ColumnWidth * 3f, rect.y, ColumnWidth - 12f, rect.height), ListColumns.Coins(row.Value), columnStyle);
            string quality = ListColumns.QualityLabel(row, items, out Color colour);
            columnStyle.normal.textColor = colour;
            GUI.Label(new Rect(right - ColumnWidth * 2f, rect.y, ColumnWidth - 12f, rect.height), quality, columnStyle);
            columnStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(right - ColumnWidth, rect.y, ColumnWidth - 12f, rect.height), ListColumns.Amount(row.Amount), columnStyle);
        }

        private void EnsureStyles()
        {
            if (nameStyle != null)
            {
                return;
            }
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, richText = true, clipping = TextClipping.Clip, wordWrap = false };
            columnStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            arrowStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
