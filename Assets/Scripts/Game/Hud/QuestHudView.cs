using Clube.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Clube.Game
{
    /// <summary>
    /// The active quest box and its tip box on the HUD canvas (GL777), laid out like the IMGUI boxes of <see cref="QuestHud"/>.
    /// It rebuilds its layout only when the quest, its position (under the minimap) or the screen changes.
    /// </summary>
    public sealed class QuestHudView
    {
        private const float Width = 400f * GameUi.FontScale;
        private const float HeadingHeight = 30f;

        private static readonly Color PanelColour = new Color(0f, 0f, 0f, 0.65f);

        private readonly RectTransform root;
        private readonly Image box, card, tipBox;
        private readonly RawImage sketchImage;
        private readonly Text heading, quest, tipHeading, tip;

        private string shownKey;

        public QuestHudView()
        {
            root = HudCanvas.Node("QuestHud", HudCanvas.Root);
            HudCanvas.Place(root, 0f, 0f, 0f, 0f);
            box = HudCanvas.Fill(root, "Box", PanelColour);
            heading = HudCanvas.Label(box.transform, "Heading", GameUi.Font(12), FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.4f));
            card = HudCanvas.Fill(box.transform, "SketchCard", new Color(0.93f, 0.89f, 0.8f));
            sketchImage = HudCanvas.Picture(card.transform, "Sketch");
            quest = HudCanvas.Label(box.transform, "Quest", GameUi.Font(13), FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            quest.horizontalOverflow = HorizontalWrapMode.Wrap;
            tipBox = HudCanvas.Fill(root, "TipBox", PanelColour);
            tipHeading = HudCanvas.Label(tipBox.transform, "TipHeading", GameUi.Font(12), FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.6f, 0.85f, 1f));
            tip = HudCanvas.Label(tipBox.transform, "Tip", GameUi.Font(12), FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.92f));
            tip.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudCanvas.SetText(tipHeading, "Tip");
        }

        /// <summary>Shows the quest (null for none) with its heading, sketch and tip, below <paramref name="top"/> pixels.</summary>
        public void Refresh(string id, string title, string headingText, Texture2D sketch, string tipText, float top)
        {
            bool shown = id != null;
            HudCanvas.SetShown(root.gameObject, shown);
            if (!shown)
            {
                shownKey = null;
                return;
            }
            string key = id + "|" + title + "|" + top.ToString("0") + "|" + Screen.width;
            if (key == shownKey)
            {
                return;
            }
            shownKey = key;

            HudCanvas.SetText(heading, headingText);
            HudCanvas.SetText(quest, title);
            float sketchSize = sketch != null ? 64f * GameUi.FontScale : 0f;
            float textWidth = Width - sketchSize;
            // The text sits 10 pixels in from each side, 2 down from the heading.
            HudCanvas.Place(quest.rectTransform, sketchSize + 10f, HeadingHeight + 2f, textWidth - 20f, 0f);
            float questHeight = Mathf.Max(quest.preferredHeight + 10f, sketchSize > 0f ? sketchSize + 6f : 0f);
            HudCanvas.Place(quest.rectTransform, sketchSize + 10f, HeadingHeight + 2f, textWidth - 20f, questHeight);
            HudCanvas.Place(box.rectTransform, 10f, top, Width, HeadingHeight + questHeight);
            HudCanvas.Place(heading.rectTransform, 10f, 6f, Width - 20f, 24f);

            HudCanvas.SetShown(card.gameObject, sketch != null);
            if (sketch != null)
            {
                // A paper card behind the ink, so the sketch reads on the dark box.
                HudCanvas.Place(card.rectTransform, 8f, HeadingHeight, sketchSize - 6f, sketchSize - 6f);
                sketchImage.texture = sketch;
                HudCanvas.Place(sketchImage.rectTransform, 0f, 0f, sketchSize - 6f, sketchSize - 6f);
            }

            // The quest's tip in its own box underneath (GL447).
            HudCanvas.SetShown(tipBox.gameObject, tipText != null);
            if (tipText != null)
            {
                HudCanvas.SetText(tip, tipText);
                HudCanvas.Place(tip.rectTransform, 10f, HeadingHeight + 2f, Width - 20f, 0f);
                float tipHeight = tip.preferredHeight + 10f;
                HudCanvas.Place(tip.rectTransform, 10f, HeadingHeight + 2f, Width - 20f, tipHeight);
                HudCanvas.Place(tipBox.rectTransform, 10f, top + HeadingHeight + questHeight + 6f, Width, HeadingHeight + tipHeight);
                HudCanvas.Place(tipHeading.rectTransform, 10f, 6f, Width - 20f, 24f);
            }
        }

        /// <summary>Takes the boxes off the canvas.</summary>
        public void Destroy()
        {
            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }
        }
    }
}
