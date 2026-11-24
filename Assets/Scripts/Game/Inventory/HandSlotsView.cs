using Clube.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Clube.Game
{
    /// <summary>
    /// The list inventory's HUD (prototype), in place of the hotbar: two slots in the lower right, what the left and right
    /// hands hold ("Left hand / Empty" when nothing), with the coins under them (<see cref="PlayerWallet"/> sits in
    /// <see cref="CoinsRect"/>). While the wheel steps a hand, that hand's other four slots show stacked above its slot, and
    /// fade away a moment after the wheel stops.
    /// </summary>
    public sealed class HandSlotsView
    {
        private const float Margin = 16f;
        private const float CoinsHeight = 34f;
        private const float LabelHeight = 26f;
        private const float StackScale = 0.8f;
        private const float ShowSeconds = 1.1f;
        private const float FadeSeconds = 0.5f;

        private static readonly Color Ink = new Color(0.9f, 0.9f, 0.9f);

        private readonly RectTransform root;
        private readonly ItemSlotView left, right;
        private readonly Text leftLabel, rightLabel, leftEmpty, rightEmpty;
        private readonly RectTransform stackRoot;
        private readonly CanvasGroup stackFade;
        private readonly ItemSlotView[] stack = new ItemSlotView[PlayerInventory.HandSlots - 1];
        private bool stackLeft;

        public HandSlotsView()
        {
            root = HudCanvas.Node("Hands", HudCanvas.Root);
            HudCanvas.Place(root, 0f, 0f, 0f, 0f);
            left = new ItemSlotView(root, "Left hand");
            right = new ItemSlotView(root, "Right hand");
            leftLabel = HudCanvas.Label(root, "Left label", GameUi.Font(13), FontStyle.Bold, TextAnchor.LowerCenter, Ink, true);
            rightLabel = HudCanvas.Label(root, "Right label", GameUi.Font(13), FontStyle.Bold, TextAnchor.LowerCenter, Ink, true);
            leftEmpty = HudCanvas.Label(root, "Left empty", GameUi.Font(13), FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f));
            rightEmpty = HudCanvas.Label(root, "Right empty", GameUi.Font(13), FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f));
            HudCanvas.SetText(leftLabel, "Left hand");
            HudCanvas.SetText(rightLabel, "Right hand");
            HudCanvas.SetText(leftEmpty, "Empty");
            HudCanvas.SetText(rightEmpty, "Empty");
            stackRoot = HudCanvas.Node("Hand stack", root);
            HudCanvas.Place(stackRoot, 0f, 0f, 0f, 0f);
            stackFade = stackRoot.gameObject.AddComponent<CanvasGroup>();
            stackFade.blocksRaycasts = false;
            stackFade.interactable = false;
            for (int i = 0; i < stack.Length; i++)
            {
                stack[i] = new ItemSlotView(stackRoot, "Stacked " + (i + 1));
            }
            HudCanvas.Resized += Layout;
            Layout();
        }

        /// <summary>Where the left hand's slot is, in IMGUI pixels (for its popup).</summary>
        public Rect LeftRect { get; private set; }

        /// <summary>Where the right hand's slot is, in IMGUI pixels (for its popup).</summary>
        public Rect RightRect { get; private set; }

        /// <summary>Where the coins line goes, under the two hands.</summary>
        public static Rect CoinsRect()
        {
            float side = GameUi.SlotSize;
            float width = side * 2f + GameUi.SlotGap;
            return new Rect(Screen.width - Margin - width - 60f, Screen.height - Margin - CoinsHeight, width + 60f, CoinsHeight);
        }

        /// <summary>Shows what the hands hold, and while a hand was just stepped, its other slots.</summary>
        public void Refresh(Hotbar hotbar, PlayerInventory inventory, bool visible)
        {
            HudCanvas.SetShown(root.gameObject, visible);
            if (!visible)
            {
                return;
            }
            ItemStack l = hotbar.LeftStack, r = hotbar.SelectedStack;
            left.Set(l, false);
            right.Set(r, true);
            HudCanvas.SetShown(leftEmpty.gameObject, l.IsEmpty);
            HudCanvas.SetShown(rightEmpty.gameObject, r.IsEmpty);

            float since = Time.unscaledTime - hotbar.LastStepTime;
            float alpha = since < ShowSeconds ? 1f : 1f - Mathf.Clamp01((since - ShowSeconds) / FadeSeconds);
            stackFade.alpha = alpha;
            HudCanvas.SetShown(stackRoot.gameObject, alpha > 0f);
            if (alpha <= 0f)
            {
                return;
            }
            if (stackLeft != hotbar.LastStepLeft)
            {
                stackLeft = hotbar.LastStepLeft;
                Layout();
            }
            // The slots after the selected one, upwards, so the next wheel step's slot sits just above the hand.
            int selected = stackLeft ? hotbar.LeftSelected : hotbar.Selected;
            for (int i = 0; i < stack.Length; i++)
            {
                int index = (selected + 1 + i) % PlayerInventory.HandSlots;
                int slot = stackLeft ? inventory.LeftHandSlot(index) : inventory.RightHandSlot(index);
                stack[i].Set(inventory.Inventory[slot], false);
            }
        }

        /// <summary>Takes the hands off the canvas.</summary>
        public void Destroy()
        {
            HudCanvas.Resized -= Layout;
            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }
        }

        private void Layout()
        {
            float side = GameUi.SlotSize, gap = GameUi.SlotGap;
            float y = Screen.height - Margin - CoinsHeight - side;
            float rightX = Screen.width - Margin - side;
            float leftX = rightX - gap - side;
            LeftRect = new Rect(leftX, y, side, side);
            RightRect = new Rect(rightX, y, side, side);
            left.Place(leftX, y, side);
            right.Place(rightX, y, side);
            HudCanvas.Place(leftLabel.rectTransform, leftX - 10f, y - LabelHeight, side + 20f, LabelHeight);
            HudCanvas.Place(rightLabel.rectTransform, rightX - 10f, y - LabelHeight, side + 20f, LabelHeight);
            HudCanvas.Place(leftEmpty.rectTransform, leftX, y, side, side);
            HudCanvas.Place(rightEmpty.rectTransform, rightX, y, side, side);

            float small = side * StackScale;
            float x = (stackLeft ? leftX : rightX) + (side - small) * 0.5f;
            float top = y - LabelHeight - gap;
            for (int i = 0; i < stack.Length; i++)
            {
                stack[i].Place(x, top - (i + 1) * (small + gap), small);
            }
        }
    }
}
