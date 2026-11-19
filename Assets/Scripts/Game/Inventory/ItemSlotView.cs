using Clube.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Clube.Game
{
    /// <summary>
    /// One inventory slot on the HUD canvas (GL777): the retained twin of <see cref="ItemSlotGui.Draw"/>, with the same
    /// square, border, icon, count, purity and bar rules (the shared label and bar functions live in
    /// <see cref="ItemSlotGui"/>). <see cref="Set"/> touches the canvas only when the stack or selection changed.
    /// </summary>
    public sealed class ItemSlotView
    {
        private static readonly Color Background = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color SelectedBorder = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color BarBack = new Color(0.2f, 0.2f, 0.22f);
        private static readonly Color BarWater = new Color(0.3f, 0.6f, 0.85f);
        private static readonly Color BarLost = new Color(0.45f, 0.45f, 0.47f);
        private static readonly Color WearLow = new Color(0.85f, 0.2f, 0.15f);
        private static readonly Color WearHigh = new Color(0.35f, 0.8f, 0.25f);
        private static readonly Color Ink = new Color(0.9f, 0.9f, 0.9f);

        private readonly RectTransform root;
        private readonly Image[] edges = new Image[4];
        private readonly RawImage icon;
        private readonly Text nameText, countText, purityText, waterText;
        private readonly Image waterBack, waterFill, wearLost, wearFill;
        private float size;

        // What was last shown, to skip identical sets.
        private ItemDefinition shownItem;
        private int shownCount, shownWear, shownProspecting;
        private float shownPart, shownQuality;
        private Composition shownContents;
        private bool shownSelected, hasShown;

        public ItemSlotView(Transform parent, string name)
        {
            root = HudCanvas.Node(name, parent);
            Image back = root.gameObject.AddComponent<Image>();
            back.color = Background;
            back.raycastTarget = false;
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = HudCanvas.Fill(root, "Edge", Border);
            }
            icon = HudCanvas.Picture(root, "Icon");
            nameText = HudCanvas.Label(root, "Name", 20, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            countText = HudCanvas.Label(root, "Count", 24, FontStyle.Bold, TextAnchor.LowerRight, Ink, true);
            purityText = HudCanvas.Label(root, "Purity", 20, FontStyle.Bold, TextAnchor.UpperRight, ItemSlotGui.PurityColour, true);
            waterBack = HudCanvas.Fill(root, "WaterBack", BarBack);
            waterFill = HudCanvas.Fill(root, "WaterFill", BarWater);
            wearLost = HudCanvas.Fill(root, "WearLost", BarLost);
            wearFill = HudCanvas.Fill(root, "WearFill", WearHigh);
            waterText = HudCanvas.Label(root, "Water", 24, FontStyle.Bold, TextAnchor.LowerRight, Ink);
        }

        /// <summary>Puts the slot at pixels from the canvas's top-left, <paramref name="side"/> square.</summary>
        public void Place(float x, float y, float side)
        {
            size = side;
            HudCanvas.Place(root, x, y, side, side);
            Inset(nameText.rectTransform, 6f);
            Inset(countText.rectTransform, 3f);
            Inset(waterText.rectTransform, 3f);
            HudCanvas.Place(purityText.rectTransform, 0f, 3f, side - 6f, 26f);
            hasShown = false;
        }

        /// <summary>Shows or hides the whole slot.</summary>
        public void Show(bool shown)
        {
            HudCanvas.SetShown(root.gameObject, shown);
        }

        /// <summary>Takes the slot off the canvas.</summary>
        public void Destroy()
        {
            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }
        }

        /// <summary>Shows a stack and whether it is the selected slot.</summary>
        public void Set(ItemStack stack, bool selected)
        {
            int prospecting = ItemSlotGui.ProspectingLevel;
            if (hasShown && stack.Item == shownItem && stack.Count == shownCount && stack.Part == shownPart && stack.Wear == shownWear
                && stack.Quality == shownQuality && stack.Contents == shownContents && selected == shownSelected && prospecting == shownProspecting)
            {
                return;
            }
            hasShown = true;
            shownItem = stack.Item;
            shownCount = stack.Count;
            shownPart = stack.Part;
            shownWear = stack.Wear;
            shownQuality = stack.Quality;
            shownContents = stack.Contents;
            shownSelected = selected;
            shownProspecting = prospecting;

            float width = selected ? 2f : 1f;
            Color border = selected ? SelectedBorder : Border;
            Edge(0, 0f, 0f, size, width, border);
            Edge(1, 0f, size - width, size, width, border);
            Edge(2, 0f, 0f, width, size, border);
            Edge(3, size - width, 0f, width, size, border);

            bool has = !stack.IsEmpty;
            Texture2D picture = has ? stack.Item.Icon : null;
            HudCanvas.SetShown(icon.gameObject, picture != null);
            if (picture != null)
            {
                icon.texture = picture;
                Fit(picture);
            }
            HudCanvas.SetShown(nameText.gameObject, has && picture == null);
            if (has && picture == null)
            {
                nameText.text = stack.Item.DisplayName;
            }

            string count = has ? ItemSlotGui.CountLabel(stack) : null;
            HudCanvas.SetShown(countText.gameObject, count != null);
            if (count != null)
            {
                countText.text = count;
            }

            bool purity = has && ItemSlotGui.ShowsPurity(stack);
            HudCanvas.SetShown(purityText.gameObject, purity);
            if (purity)
            {
                purityText.text = ItemSlotGui.PurityLabel(stack, out Color colour);
                purityText.color = colour;
            }

            // A bucket's water (GL319), then a tool's wear (GL37), in the strip along the bottom.
            bool bucketHeld = has && stack.Item is BucketDefinition;
            HudCanvas.SetShown(waterBack.gameObject, bucketHeld);
            HudCanvas.SetShown(waterFill.gameObject, bucketHeld);
            HudCanvas.SetShown(waterText.gameObject, bucketHeld);
            if (bucketHeld)
            {
                var bucket = (BucketDefinition)stack.Item;
                float full = (float)bucket.WaterIn(stack) / bucket.CapacityOf(stack);
                Bar(waterBack, 1f, BarBack);
                Bar(waterFill, full, BarWater);
                waterText.text = bucket.WaterIn(stack).ToString();
            }

            bool worn = has && ItemSlotGui.WearBar(stack, out _, out _);
            HudCanvas.SetShown(wearFill.gameObject, worn);
            HudCanvas.SetShown(wearLost.gameObject, false);
            if (worn)
            {
                ItemSlotGui.WearBar(stack, out float left, out float best);
                if (best < 0.995f)
                {
                    float room = size - 8f;
                    HudCanvas.SetShown(wearLost.gameObject, true);
                    HudCanvas.Place(wearLost.rectTransform, 4f + room * best, size - 12f, room * (1f - best), 7f);
                }
                Bar(wearFill, left, Color.Lerp(WearLow, WearHigh, left));
            }
        }

        private void Edge(int i, float x, float y, float w, float h, Color color)
        {
            edges[i].color = color;
            HudCanvas.Place(edges[i].rectTransform, x, y, w, h);
        }

        private void Bar(Image bar, float fraction, Color color)
        {
            bar.color = color;
            HudCanvas.Place(bar.rectTransform, 4f, size - 12f, (size - 8f) * fraction, 7f);
        }

        // ScaleToFit inside the slot's inner square, centred.
        private void Fit(Texture2D picture)
        {
            float inner = size - 12f;
            float aspect = picture.height > 0 ? (float)picture.width / picture.height : 1f;
            float w = aspect >= 1f ? inner : inner * aspect;
            float h = aspect >= 1f ? inner / aspect : inner;
            HudCanvas.Place(icon.rectTransform, 6f + (inner - w) * 0.5f, 6f + (inner - h) * 0.5f, w, h);
        }

        private void Inset(RectTransform rt, float inset)
        {
            HudCanvas.Place(rt, inset, inset, size - inset * 2f, size - inset * 2f);
        }
    }
}
