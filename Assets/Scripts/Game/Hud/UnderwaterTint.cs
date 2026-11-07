using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The underwater view (GL316): while the player's head is under water the screen is tinted the water's muted
    /// blue-green, darker towards the edges, and the light ripples gently across it. Drawn behind the rest of the HUD.
    /// </summary>
    [RequireComponent(typeof(PlayerSwimming))]
    public sealed class UnderwaterTint : MonoBehaviour
    {
        [SerializeField]
        private Color tint = new Color(0.12f, 0.3f, 0.33f, 0.55f);

        [SerializeField]
        private Color edge = new Color(0.03f, 0.1f, 0.13f, 0.8f);

        private PlayerSwimming swimming;
        private PlayerController player;
        private Texture2D shade;
        private float shown;

        private void Awake()
        {
            swimming = GetComponent<PlayerSwimming>();
            player = GetComponent<PlayerController>();
        }

        private void Update()
        {
            bool under = swimming.HeadUnderwater && player != null && player.enabled;
            shown = Mathf.MoveTowards(shown, under ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        }

        private void OnDestroy()
        {
            if (shade != null)
            {
                Destroy(shade);
            }
        }

        private void OnGUI()
        {
            GameUi.Apply();
            if (shown <= 0f || Event.current.type != EventType.Repaint)
            {
                return;
            }
            shade ??= MakeShade();
            GUI.depth = 50;
            Color before = GUI.color;
            float wave = 0.92f + 0.08f * Mathf.Sin(Time.time * 1.7f);
            GUI.color = new Color(1f, 1f, 1f, shown * wave);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), shade, ScaleMode.StretchToFill, true);
            GUI.color = before;
        }

        // The tint, deepening into the edge colour towards the corners.
        private Texture2D MakeShade()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Underwater tint" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x / (size - 1f) * 2f - 1f, dy = y / (size - 1f) * 2f - 1f;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.35f) / 0.9f));
                    pixels[x + y * size] = Color.Lerp(tint, edge, t);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
