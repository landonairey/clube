using UnityEngine;
using UnityEngine.UI;

namespace Clube.Game
{
    /// <summary>
    /// The one canvas the always-on HUD lives on (GL777): a screen-space overlay at one canvas unit to the pixel, so the
    /// layout numbers of the old IMGUI drawing (pixels from the top-left corner) carry over. Parts are built once and only
    /// touched when what they show changes, which is what keeps the HUD off the per-frame IMGUI path. Player screens still
    /// draw with IMGUI, and IMGUI is drawn over a canvas, so they cover the HUD as before.
    /// </summary>
    public static class HudCanvas
    {
        private static Canvas canvas;

        /// <summary>Raised when the screen's size changes, so views can lay themselves out again.</summary>
        public static event System.Action Resized;

        /// <summary>The canvas root.</summary>
        public static RectTransform Root
        {
            get
            {
                if (canvas == null)
                {
                    var go = new GameObject("HudCanvas", typeof(RectTransform));
                    canvas = go.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 0;
                    var scaler = go.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = 1f;
                    go.AddComponent<SizeWatch>();
                }
                return (RectTransform)canvas.transform;
            }
        }

        /// <summary>A new child that is placed by pixels from the parent's top-left corner (see <see cref="Place"/>).</summary>
        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        /// <summary>Puts a node at pixels from the top-left of its parent, as an IMGUI <c>Rect</c> would.</summary>
        public static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        /// <summary>A flat colour rectangle.</summary>
        public static Image Fill(Transform parent, string name, Color color)
        {
            RectTransform rt = Node(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A texture drawn to fit its rectangle.</summary>
        public static RawImage Picture(Transform parent, string name)
        {
            RectTransform rt = Node(name, parent);
            var image = rt.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A line of text in the game's font; <paramref name="outlined"/> gives it the dark edge of <see cref="GameUi.OutlinedLabel"/>.</summary>
        public static Text Label(Transform parent, string name, int size, FontStyle style, TextAnchor anchor, Color color, bool outlined = false)
        {
            RectTransform rt = Node(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = GameFont.Get();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            if (outlined)
            {
                var edge = rt.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0f, 0f, 0f, 0.85f);
                edge.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return text;
        }

        /// <summary>Sets a text's string only when it differs, so unchanged text costs nothing.</summary>
        public static void SetText(Text text, string value)
        {
            if (text.text != value)
            {
                text.text = value ?? string.Empty;
            }
        }

        /// <summary>Shows or hides a part, touching it only when its state changes.</summary>
        public static void SetShown(GameObject go, bool shown)
        {
            if (go.activeSelf != shown)
            {
                go.SetActive(shown);
            }
        }

        private sealed class SizeWatch : MonoBehaviour
        {
            private int width, height;

            private void Update()
            {
                if (Screen.width != width || Screen.height != height)
                {
                    width = Screen.width;
                    height = Screen.height;
                    Resized?.Invoke();
                }
            }
        }
    }
}
