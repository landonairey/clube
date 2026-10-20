using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Hand-drawn ore sketches for the mining journal (GL73), painted once per ore at run time: a
    /// lumpy rock in black ink with a little hatching on its shadow side, a faint grey wash, and
    /// speckles of the ore in its own colour (<see cref="VoxelMaterial.DebugColor"/>). The shape is
    /// seeded by the material id, so each ore always looks the same.
    /// </summary>
    public static class OreSketch
    {
        public const int Width = 160;
        public const int Height = 120;

        private static readonly Color Ink = new Color(0.1f, 0.1f, 0.14f, 1f);
        private static readonly Color Wash = new Color(0.45f, 0.45f, 0.48f, 0.22f);
        private static readonly Dictionary<(VoxelMaterial, bool), Texture2D> Cache = new Dictionary<(VoxelMaterial, bool), Texture2D>();

        /// <summary>
        /// The sketch of an ore, painted the first time it's asked for. Uncoloured (an ore not yet told
        /// apart, GL77), its speckles are pencilled in grey instead of the ore's colour.
        /// </summary>
        public static Texture2D For(VoxelMaterial ore, bool coloured = true)
        {
            if (ore == null)
            {
                return null;
            }
            if (!Cache.TryGetValue((ore, coloured), out Texture2D texture) || texture == null)
            {
                texture = Paint(ore, coloured);
                Cache[(ore, coloured)] = texture;
            }
            return texture;
        }

        private static Texture2D Paint(VoxelMaterial ore, bool coloured)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = $"{ore.DisplayName} sketch" };
            var clear = new Color[Width * Height];
            texture.SetPixels(clear);
            var random = new System.Random(1000 + ore.Id);

            // The outline: a lumpy loop of points around the middle.
            const int corners = 15;
            var centre = new Vector2(Width * 0.5f, Height * 0.48f);
            var outline = new Vector2[corners];
            for (int i = 0; i < corners; i++)
            {
                float angle = i / (float)corners * Mathf.PI * 2f;
                float reach = 1f + ((float)random.NextDouble() - 0.5f) * 0.38f;
                outline[i] = centre + new Vector2(Mathf.Cos(angle) * 60f, Mathf.Sin(angle) * 40f) * reach;
            }

            // Wash and speckles inside it.
            Color oreColour = coloured ? ore.DebugColor : new Color(0.42f, 0.42f, 0.44f);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (Inside(outline, new Vector2(x, y)))
                    {
                        texture.SetPixel(x, y, Wash);
                    }
                }
            }
            for (int i = 0; i < 46; i++)
            {
                var spot = new Vector2(centre.x + ((float)random.NextDouble() - 0.5f) * 110f, centre.y + ((float)random.NextDouble() - 0.5f) * 72f);
                if (!Inside(outline, spot))
                {
                    continue;
                }
                float size = 1.4f + (float)random.NextDouble() * 2.8f;
                float shade = 0.8f + (float)random.NextDouble() * 0.35f;
                Dot(texture, spot, size, new Color(oreColour.r * shade, oreColour.g * shade, oreColour.b * shade, 1f));
            }

            // Hatching on the lower right, the side away from the light.
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                var from = centre + new Vector2(Mathf.Lerp(8f, 52f, t), -30f + t * 8f);
                var to = from + new Vector2(-10f, 14f);
                if (Inside(outline, from) && Inside(outline, to))
                {
                    Line(texture, from, to, 0.6f, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
                }
            }

            // The ink outline, a little uneven, as if drawn by hand.
            for (int i = 0; i < corners; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % corners];
                float width = 1.1f + (float)random.NextDouble() * 0.6f;
                Line(texture, a, b, width, Ink);
            }
            texture.Apply();
            return texture;
        }

        // Even-odd test against the outline.
        private static bool Inside(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y)
                    && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static void Dot(Texture2D texture, Vector2 centre, float radius, Color color)
        {
            int r = Mathf.CeilToInt(radius);
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    int px = Mathf.RoundToInt(centre.x) + x, py = Mathf.RoundToInt(centre.y) + y;
                    if (px >= 0 && py >= 0 && px < Width && py < Height && x * x + y * y <= radius * radius)
                    {
                        texture.SetPixel(px, py, color);
                    }
                }
            }
        }

        // A line of a width, its edge softened over half a pixel, blended over what's there.
        private static void Line(Texture2D texture, Vector2 from, Vector2 to, float width, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - width - 1));
            int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + width + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - width - 1));
            int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + width + 1));
            Vector2 along = to - from;
            float length = Mathf.Max(along.sqrMagnitude, 1e-4f);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - from, along) / length);
                    float distance = Vector2.Distance(p, from + along * t);
                    float cover = Mathf.Clamp01(width - distance + 0.5f) * color.a;
                    if (cover > 0f)
                    {
                        Color under = texture.GetPixel(x, y);
                        var over = new Color(color.r, color.g, color.b, 1f);
                        texture.SetPixel(x, y, Color.Lerp(under, over, cover));
                    }
                }
            }
        }
    }
}
