using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>What the mouse did to the player's slots in one IMGUI event (<see cref="InventoryDrag.Handle"/>).</summary>
    public enum InventoryDragKind
    {
        /// <summary>Nothing for the screen to do.</summary>
        None,

        /// <summary>A click on a stack, no drag: the screen decides what that does (send it across, load it into a station).</summary>
        Clicked,

        /// <summary>A stack dragged off the player's slots and let go: the screen decides where it lands (trash, a station slot).</summary>
        DroppedOutside,
    }

    /// <summary>One mouse result: what happened, to which slot, how many items it carried, and where the mouse was.</summary>
    public readonly struct InventoryDragResult
    {
        public InventoryDragResult(InventoryDragKind kind, int slot, int count, Vector2 position)
        {
            Kind = kind;
            Slot = slot;
            Count = count;
            Position = position;
        }

        public InventoryDragKind Kind { get; }

        public int Slot { get; }

        public int Count { get; }

        public Vector2 Position { get; }
    }

    /// <summary>
    /// Clicking, dragging and splitting the player's stacks on any screen that shows their slots (GL71, GL191, GL195),
    /// so the inventory and the station screens handle the mouse the same way. Press and drag carries a stack to the
    /// slot it's let go on (stacking there, or swapping); Shift carries half of it and Ctrl one, so a stack splits
    /// as it's dragged; a right click splits a stack in half into an empty slot. A plain click, or a drag let go off
    /// the slots, goes back to the screen. Call <see cref="Handle"/> before drawing the slots and
    /// <see cref="DrawCarried"/> after.
    /// </summary>
    public sealed class InventoryDrag
    {
        // Pixels the mouse moves with the button held before a press becomes a drag.
        private const float DragDistance = 6f;

        private int pressed = -1;
        private int carry;
        private Vector2 pressedAt;
        private bool dragging;

        /// <summary>The slot pressed, to draw highlighted; -1 when none.</summary>
        public int Pressed => pressed;

        /// <summary>True while a stack is carried under the mouse.</summary>
        public bool IsDragging => dragging && pressed >= 0;

        /// <summary>Lets go of anything held (the screen closed).</summary>
        public void Reset()
        {
            pressed = -1;
            dragging = false;
        }

        /// <summary>
        /// How many of a stack a click or drag takes (GL176, GL191): all of it, half with Shift (rounded up), one with Ctrl.
        /// </summary>
        public static int CountFor(ItemStack stack)
        {
            Keyboard keyboard = Keyboard.current;
            Event current = Event.current;
            bool shift = (current != null && current.shift) || (keyboard != null && keyboard.shiftKey.isPressed);
            bool control = (current != null && current.control) || (keyboard != null && keyboard.ctrlKey.isPressed);
            if (control)
            {
                return 1;
            }
            return shift ? Mathf.Max(1, (stack.Count + 1) / 2) : int.MaxValue;
        }

        /// <summary>
        /// Handles this IMGUI event over the player's slots laid out from <paramref name="gridTopLeft"/>
        /// (<see cref="InventoryGridGui.DrawPlayer"/>). Moves and splits within the player's slots itself.
        /// </summary>
        public InventoryDragResult Handle(PlayerInventory inventory, Vector2 gridTopLeft)
        {
            Event current = Event.current;
            Inventory items = inventory.Inventory;
            int over = InventoryGridGui.PlayerSlotAt(gridTopLeft, inventory, current.mousePosition);
            var none = new InventoryDragResult(InventoryDragKind.None, -1, 0, current.mousePosition);

            // A right click splits a stack in half into an empty slot (GL191).
            if (current.type == EventType.MouseDown && current.button == 1 && over >= 0 && items[over].Count >= 2)
            {
                items.Split(over, items[over].Count / 2);
                current.Use();
                return none;
            }
            if (current.button != 0)
            {
                return none;
            }
            switch (current.type)
            {
                case EventType.MouseDown:
                    if (over >= 0 && !items[over].IsEmpty)
                    {
                        pressed = over;
                        pressedAt = current.mousePosition;
                        carry = CountFor(items[over]);
                        dragging = false;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (pressed >= 0)
                    {
                        dragging |= (current.mousePosition - pressedAt).sqrMagnitude > DragDistance * DragDistance;
                        current.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (pressed < 0)
                    {
                        break;
                    }
                    int from = pressed;
                    int count = Mathf.Min(carry, items[from].Count);
                    bool dragged = dragging;
                    Reset();
                    current.Use();
                    if (items[from].IsEmpty)
                    {
                        break;
                    }
                    // A release over another slot is a drop even when no drag event came in between (a quick
                    // flick), and a release in the gap between slots lands on the nearer one.
                    int target = InventoryGridGui.PlayerSlotNear(gridTopLeft, inventory, current.mousePosition, InventoryGridGui.RowGap * 0.5f);
                    if (target >= 0 && target != from)
                    {
                        items.Move(from, target, Mathf.Max(1, count));
                        break;
                    }
                    if (dragged && target < 0)
                    {
                        return new InventoryDragResult(InventoryDragKind.DroppedOutside, from, Mathf.Max(1, count), current.mousePosition);
                    }
                    if (!dragged && over == from)
                    {
                        return new InventoryDragResult(InventoryDragKind.Clicked, from, Mathf.Max(1, count), current.mousePosition);
                    }
                    break;
            }
            return none;
        }

        /// <summary>The stack being carried, under the mouse, with how many of it go; draw last, over the slots.</summary>
        public void DrawCarried(Inventory items)
        {
            if (!IsDragging || items[pressed].IsEmpty)
            {
                return;
            }
            ItemStack stack = items[pressed];
            int count = Mathf.Min(carry, stack.Count);
            ItemStack shown = count < stack.Count ? new ItemStack(stack.Item, count, stack.Contents, stack.Wear, stack.Quality) : stack;
            Vector2 mouse = Event.current.mousePosition;
            float half = InventoryGridGui.SlotSize * 0.5f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            ItemSlotGui.Draw(new Rect(mouse.x - half, mouse.y - half, InventoryGridGui.SlotSize, InventoryGridGui.SlotSize), shown, true);
            GUI.color = previous;
        }
    }
}
