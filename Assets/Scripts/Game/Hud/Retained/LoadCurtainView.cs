using UnityEngine;
using UnityEngine.UI;

namespace Clube.Game
{
    /// <summary>
    /// The full-screen loading curtain on its own overlay canvas (GL780): black, with "Loading NN%", the stage under it and a
    /// bar. Whoever is waiting calls <see cref="Show"/> every frame (<see cref="GameLoadScreen"/> while a scene loads,
    /// <see cref="LoadCurtain"/> while the ground does); it hides itself a few frames after the last call, so the hand-over
    /// between the two shows no gap and nothing can leave it stuck up. It is above the HUD canvas; IMGUI screens draw over it.
    /// </summary>
    public static class LoadCurtainView
    {
        // Frames without a Show before the curtain drops: covers the frame or two between a scene's load screen and its curtain.
        private const int GraceFrames = 3;

        private static GameObject root;
        private static Text percentText, stageText;
        private static RectTransform fill;
        private static int shownFrame = int.MinValue;
        private static int shownPercent = -1;

        /// <summary>True while the curtain covers the screen (shown this frame or the last).</summary>
        public static bool IsUp => root != null && root.activeSelf && Time.frameCount - shownFrame <= 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            root = null;
            shownFrame = int.MinValue;
            shownPercent = -1;
        }

        /// <summary>The whole number printed for a 0 to 1 fraction: rounded down, so "100%" is only ever shown by the full bar of a finished load.</summary>
        public static int Percent(float fraction)
        {
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(fraction) * 100f), 0, 100);
        }

        /// <summary>Covers the screen this frame, with the load's overall progress (0 to 1) and what it is doing.</summary>
        public static void Show(float fraction, string stage)
        {
            Build();
            shownFrame = Time.frameCount;
            HudCanvas.SetShown(root, true);
            int percent = Percent(fraction);
            if (percent != shownPercent)
            {
                shownPercent = percent;
                percentText.text = $"Loading {percent}%";
                fill.anchorMax = new Vector2(percent / 100f, 1f);
            }
            HudCanvas.SetText(stageText, stage);
        }

        /// <summary>Takes the curtain down at once.</summary>
        public static void Hide()
        {
            if (root != null)
            {
                HudCanvas.SetShown(root, false);
            }
            shownPercent = -1;
        }

        private static void Build()
        {
            if (root != null)
            {
                return;
            }
            root = new GameObject("LoadCurtainView", typeof(RectTransform));
            Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            root.AddComponent<Dropper>();

            Image back = HudCanvas.Fill(root.transform, "Curtain", new Color(0.05f, 0.06f, 0.07f, 1f));
            Stretch(back.rectTransform);
            back.raycastTarget = true; // nothing underneath can be clicked while it is up

            percentText = HudCanvas.Label(root.transform, "Percent", 44, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Centre(percentText.rectTransform, 0.5f, 0.2f, 600f, 70f);
            stageText = HudCanvas.Label(root.transform, "Stage", 22, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.8f, 0.82f, 0.85f));
            Centre(stageText.rectTransform, 0.5f, 0.1f, 800f, 36f);

            Image track = HudCanvas.Fill(root.transform, "BarTrack", new Color(1f, 1f, 1f, 0.15f));
            Centre(track.rectTransform, 0.5f, 0.145f, 420f, 8f);
            Image bar = HudCanvas.Fill(track.transform, "BarFill", new Color(0.95f, 0.8f, 0.35f));
            fill = bar.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            root.SetActive(false);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // Centred at a fraction of the screen's width and height, whatever its size.
        private static void Centre(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(x, y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
        }

        // Hides the curtain once nobody has asked for it for a few frames.
        private sealed class Dropper : MonoBehaviour
        {
            private void LateUpdate()
            {
                if (Time.frameCount - shownFrame > GraceFrames)
                {
                    Hide();
                }
            }
        }
    }
}
