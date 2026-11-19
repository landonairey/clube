using Clube.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Clube.Game
{
    /// <summary>
    /// The hotbar on the HUD canvas (GL777): a row of <see cref="ItemSlotView"/>s along the bottom and the selected
    /// item's name above them, laid out like the IMGUI bar it replaced and updated only when a slot changes.
    /// </summary>
    public sealed class HotbarView
    {
        private const float BottomMargin = 12f;

        private readonly RectTransform root;
        private readonly Text nameText;
        private ItemSlotView[] slots = new ItemSlotView[0];

        public HotbarView()
        {
            root = HudCanvas.Node("Hotbar", HudCanvas.Root);
            HudCanvas.Place(root, 0f, 0f, 0f, 0f);
            nameText = HudCanvas.Label(root, "SelectedName", GameUi.Font(13), FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.9f, 0.9f, 0.9f));
            HudCanvas.Resized += OnResized;
        }

        /// <summary>Shows the bar's slots; <paramref name="name"/> is the line over them (null for none).</summary>
        public void Refresh(PlayerInventory inventory, int count, int selected, string name, bool visible)
        {
            HudCanvas.SetShown(root.gameObject, visible);
            if (!visible)
            {
                return;
            }
            if (slots.Length != count)
            {
                Rebuild(count);
            }
            for (int i = 0; i < count; i++)
            {
                slots[i].Set(inventory.Inventory[i], i == selected);
            }
            HudCanvas.SetText(nameText, name);
            HudCanvas.SetShown(nameText.gameObject, name != null);
        }

        /// <summary>Takes the bar off the canvas.</summary>
        public void Destroy()
        {
            HudCanvas.Resized -= OnResized;
            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }
        }

        private void Rebuild(int count)
        {
            foreach (ItemSlotView slot in slots)
            {
                slot.Destroy();
            }
            slots = new ItemSlotView[count];
            for (int i = 0; i < count; i++)
            {
                slots[i] = new ItemSlotView(root, "Slot " + (i + 1));
            }
            Layout();
        }

        private void OnResized()
        {
            Layout();
        }

        private void Layout()
        {
            int count = slots.Length;
            float side = GameUi.SlotSize, gap = GameUi.SlotGap;
            float width = count * side + (count - 1) * gap;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - side - BottomMargin;
            for (int i = 0; i < count; i++)
            {
                slots[i].Place(x + i * (side + gap), y, side);
            }
            // The default label's left padding.
            HudCanvas.Place(nameText.rectTransform, x + 3f, y - 32f, Mathf.Max(width, 560f), 30f);
        }
    }
}
