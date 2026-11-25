using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The inventory screen's left panel (list inventory prototype): the player's figure, turned by dragging, with slots for
    /// Head, Chest, Arms, Hands, Legs and Feet beside it, the left hand's five slots down the left and the right hand's down
    /// the right, and under them what the outfit is worth and its bonuses. A click on a hand's slot makes it the one held,
    /// a right click puts it back in the bag, and a drag moves it to another hand slot or off the panel (the screen decides).
    /// The body slots hold what's worn (items 18, 19): a right click takes it off, a drag moves it, and the outfit's points
    /// show as its disposition bonus with shopkeepers (item 20).
    /// </summary>
    public sealed class EquipmentPanelGui
    {
        private const float HandSide = 96f;
        private const float BodySide = 76f;
        private const float Gap = 8f;
        private const float DragDistance = 6f;

        private readonly CharacterPreview preview = new CharacterPreview();
        private readonly Rect[] leftSlots = new Rect[PlayerInventory.HandSlots];
        private readonly Rect[] rightSlots = new Rect[PlayerInventory.HandSlots];
        // Head, Chest, Arms down the figure's left; Hands, Legs, Feet down its right (WearSlot order after None).
        private readonly Rect[] bodySlots = new Rect[PlayerInventory.BodySlots];
        private int pressedSlot = -1;
        private Vector2 pressedAt;
        private bool dragging;
        private GUIStyle titleStyle, smallStyle, bodyStyle;

        /// <summary>Height the panel wants, for a screen laying it out.</summary>
        public static float PreferredHeight => 40f + 30f + HandSide * PlayerInventory.HandSlots + Gap * (PlayerInventory.HandSlots - 1) + 16f + 90f;

        /// <summary>A hand or body slot is being dragged.</summary>
        public bool IsDragging => dragging && pressedSlot >= 0;

        /// <summary>The inventory slot of the hand or body slot under <paramref name="point"/>, or -1.</summary>
        public int SlotAt(PlayerInventory inventory, Vector2 point)
        {
            for (int i = 0; i < PlayerInventory.BodySlots; i++)
            {
                if (bodySlots[i].Contains(point))
                {
                    return inventory.BodySlot((WearSlot)(i + 1));
                }
            }
            for (int i = 0; i < PlayerInventory.HandSlots; i++)
            {
                if (leftSlots[i].Contains(point))
                {
                    return inventory.LeftHandSlot(i);
                }
                if (rightSlots[i].Contains(point))
                {
                    return inventory.RightHandSlot(i);
                }
            }
            return -1;
        }

        /// <summary>Handles the mouse and draws the panel; a hand slot dragged off its slots comes back as DroppedOutside.</summary>
        public InventoryDragResult Draw(Rect area, PlayerInventory inventory, Hotbar hotbar)
        {
            EnsureStyles();
            Inventory items = inventory.Inventory;
            InventoryGridGui.Panel(area);
            float x = area.x + 12f, y = area.y + 8f;
            GUI.Label(new Rect(x, y, area.width - 24f, 32f), "Equipment", titleStyle);
            y += 40f;

            float leftX = area.x + 12f;
            float rightX = area.xMax - 12f - HandSide;
            GUI.Label(new Rect(leftX - 8f, y, HandSide + 40f, 26f), "Left hand", smallStyle);
            smallStyle.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(rightX - 32f, y, HandSide + 32f, 26f), "Right hand", smallStyle);
            smallStyle.alignment = TextAnchor.UpperLeft;
            y += 30f;
            float columnHeight = HandSide * PlayerInventory.HandSlots + Gap * (PlayerInventory.HandSlots - 1);
            for (int i = 0; i < PlayerInventory.HandSlots; i++)
            {
                leftSlots[i] = new Rect(leftX, y + i * (HandSide + Gap), HandSide, HandSide);
                rightSlots[i] = new Rect(rightX, y + i * (HandSide + Gap), HandSide, HandSide);
            }

            // The figure between the hands, the body slots in two columns either side of it.
            float centreX = leftX + HandSide + 16f;
            float centreWidth = rightX - 16f - centreX;
            float bodyLeft = centreX, bodyRight = centreX + centreWidth - BodySide;
            float bodyStep = (columnHeight - BodySide) / 2f;
            for (int i = 0; i < PlayerInventory.BodySlots; i++)
            {
                bodySlots[i] = new Rect(i < 3 ? bodyLeft : bodyRight, y + (i % 3) * bodyStep, BodySide, BodySide);
            }

            InventoryDragResult result = HandleMouse(inventory, hotbar);

            for (int i = 0; i < PlayerInventory.HandSlots; i++)
            {
                DrawHand(leftSlots[i], items[inventory.LeftHandSlot(i)], inventory.LeftHandSlot(i) == hotbar.LeftSlot, (i + 1).ToString());
                DrawHand(rightSlots[i], items[inventory.RightHandSlot(i)], inventory.RightHandSlot(i) == hotbar.SelectedSlot, (i + 1).ToString());
            }

            var figure = new Rect(bodyLeft + BodySide + Gap, y, centreWidth - (BodySide + Gap) * 2f, columnHeight);
            preview.Draw(figure);
            GUI.Label(new Rect(figure.x, figure.yMax - 26f, figure.width, 24f), "Drag to turn", bodyStyle);
            for (int i = 0; i < PlayerInventory.BodySlots; i++)
            {
                var part = (WearSlot)(i + 1);
                ItemStack worn = items[inventory.BodySlot(part)];
                ItemSlotGui.Draw(bodySlots[i], worn, false);
                if (worn.IsEmpty)
                {
                    GUI.Label(bodySlots[i], part.ToString(), bodyStyle);
                }
                else if (bodySlots[i].Contains(Event.current.mousePosition) && !IsDragging)
                {
                    InventoryGridGui.Hover(worn);
                }
            }
            y += columnHeight + 16f;

            // What the outfit is worth, and what it gives (item 20: disposition with shopkeepers).
            float worth = 0f;
            for (int i = 0; i < PlayerInventory.HandSlots; i++)
            {
                worth += StackWorth(items[inventory.LeftHandSlot(i)]) + StackWorth(items[inventory.RightHandSlot(i)]);
            }
            for (int i = 0; i < PlayerInventory.BodySlots; i++)
            {
                worth += StackWorth(items[inventory.BodySlot((WearSlot)(i + 1))]);
            }
            GUI.Label(new Rect(x, y, area.width - 24f, 28f), $"Outfit value: {ListColumns.Coins(worth)}", titleStyle);
            y += 32f;
            int bonus = Outfit.DispositionBonus(inventory.OutfitPoints);
            string bonuses = bonus > 0
                ? $"Active bonuses: +{bonus} disposition with shopkeepers ({Outfit.Describe(inventory.OutfitPoints)})."
                : "Active bonuses: none. Cloth and leather clothing raise shopkeepers' disposition.";
            GUI.Label(new Rect(x, y, area.width - 24f, 50f), bonuses, smallStyle);

            if (IsDragging && pressedSlot < items.SlotCount && !items[pressedSlot].IsEmpty)
            {
                ItemStack stack = items[pressedSlot];
                Vector2 mouse = Event.current.mousePosition;
                ItemSlotGui.Draw(new Rect(mouse.x - 36f, mouse.y - 36f, 72f, 72f), stack, true);
            }
            return result;
        }

        /// <summary>Stops drawing the figure (the screen closed).</summary>
        public void Hide()
        {
            preview.Hide();
            pressedSlot = -1;
            dragging = false;
        }

        public void Destroy()
        {
            preview.Destroy();
        }

        private static float StackWorth(ItemStack stack)
        {
            return stack.IsEmpty ? 0f : ListColumns.ValueOf(stack) * stack.Amount;
        }

        private void DrawHand(Rect rect, ItemStack stack, bool held, string key)
        {
            ItemSlotGui.Draw(rect, stack, held, key);
            if (stack.IsEmpty)
            {
                GUI.Label(rect, "Empty", bodyStyle);
            }
            else if (rect.Contains(Event.current.mousePosition) && !IsDragging)
            {
                InventoryGridGui.Hover(stack);
            }
        }

        private InventoryDragResult HandleMouse(PlayerInventory inventory, Hotbar hotbar)
        {
            Event current = Event.current;
            Inventory items = inventory.Inventory;
            var none = new InventoryDragResult(InventoryDragKind.None, -1, 0, current.mousePosition);
            int over = SlotAt(inventory, current.mousePosition);
            switch (current.type)
            {
                case EventType.MouseDown when over >= 0:
                    current.Use();
                    if (current.button == 1)
                    {
                        // Back in the bag, if it has room.
                        items.MoveInto(over, 0, inventory.BagSlots);
                        break;
                    }
                    if (current.button == 0)
                    {
                        pressedSlot = over;
                        pressedAt = current.mousePosition;
                        dragging = false;
                    }
                    break;
                case EventType.MouseDrag when pressedSlot >= 0:
                    dragging |= (current.mousePosition - pressedAt).sqrMagnitude > DragDistance * DragDistance;
                    current.Use();
                    break;
                case EventType.MouseUp when pressedSlot >= 0:
                    int from = pressedSlot;
                    bool dragged = dragging;
                    pressedSlot = -1;
                    dragging = false;
                    current.Use();
                    if (!dragged)
                    {
                        if (over == from)
                        {
                            Hold(inventory, hotbar, from);
                        }
                        break;
                    }
                    if (items[from].IsEmpty)
                    {
                        break;
                    }
                    if (over >= 0)
                    {
                        if (over != from)
                        {
                            items.Move(from, over);
                        }
                        break;
                    }
                    return new InventoryDragResult(InventoryDragKind.DroppedOutside, from, items[from].Count, current.mousePosition);
            }
            return none;
        }

        // A click on a hand's slot makes it the one that hand holds.
        private static void Hold(PlayerInventory inventory, Hotbar hotbar, int slot)
        {
            for (int i = 0; i < PlayerInventory.HandSlots; i++)
            {
                if (inventory.RightHandSlot(i) == slot)
                {
                    hotbar.Select(i);
                }
                else if (inventory.LeftHandSlot(i) == slot)
                {
                    hotbar.SelectLeft(i);
                }
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }
            titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 20 };
            smallStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            bodyStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
        }
    }
}
