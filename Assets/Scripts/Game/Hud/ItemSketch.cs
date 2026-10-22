using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Ink sketches of items for the crafting journal (GL137), drawn once per item at run time from its
    /// icon, to sit beside the ore sketches (<see cref="OreSketch"/>): a black ink outline where the icon
    /// meets its background, diagonal hatching over its darker half, and a faint wash of its own colours.
    /// A thing the player hasn't made yet is only outlined.
    /// </summary>
    public static class ItemSketch
    {
        public const int Size = 128;

        private static readonly Color Ink = new Color(0.1f, 0.1f, 0.14f, 1f);
        private static readonly Dictionary<(ItemDefinition, bool), Texture2D> Cache = new Dictionary<(ItemDefinition, bool), Texture2D>();

        /// <summary>The sketch of an item, drawn the first time it's asked for; null for an item with no icon.</summary>
        public static Texture2D For(ItemDefinition item, bool made = true)
        {
            if (item == null || item.Icon == null)
            {
                return null;
            }
            if (!Cache.TryGetValue((item, made), out Texture2D texture) || texture == null)
            {
                texture = Paint(item, made);
                Cache[(item, made)] = texture;
            }
            return texture;
        }

        private static Texture2D Paint(ItemDefinition item, bool made)
        {
            Color[] icon = ReadIcon(item.Icon);
            var pixels = new Color[Size * Size];
            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    int i = y * Size + x;
                    float alpha = icon[i].a;
                    // An edge: where the icon's coverage changes sharply.
                    float edge = Mathf.Abs(icon[i + 1].a - icon[i - 1].a) + Mathf.Abs(icon[i + Size].a - icon[i - Size].a);
                    if (edge > 0.6f)
                    {
                        pixels[i] = new Color(Ink.r, Ink.g, Ink.b, Mathf.Clamp01(edge));
                        continue;
                    }
                    if (alpha < 0.5f || !made)
                    {
                        continue;
                    }
                    float luminance = icon[i].r * 0.3f + icon[i].g * 0.59f + icon[i].b * 0.11f;
                    // Hatching on the darker parts, every fourth diagonal; a light wash of its colour elsewhere.
                    if (luminance < 0.45f && (x + y) % 4 == 0)
                    {
                        pixels[i] = new Color(Ink.r, Ink.g, Ink.b, 0.75f);
                    }
                    else
                    {
                        Color wash = Color.Lerp(new Color(0.5f, 0.5f, 0.52f), icon[i], 0.5f);
                        wash.a = 0.3f;
                        pixels[i] = wash;
                    }
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = $"{item.DisplayName} sketch", hideFlags = HideFlags.DontSave };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // The icon at the sketch's size, read back through a render texture: icons needn't be readable.
        private static Color[] ReadIcon(Texture icon)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(icon, target);
            RenderTexture.active = target;
            var read = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Color[] pixels = read.GetPixels();
            Object.Destroy(read);
            return pixels;
        }
    }
}
