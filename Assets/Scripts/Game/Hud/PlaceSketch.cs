using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Ink sketches of the two places to craft that have no item to draw from (GL232): a tree stump (a cut log end
    /// with growth rings over a barked side and flared roots) and a stone boulder (a lumpy rock with cracks, shaded
    /// with hatching, on a ground line). Drawn once in code, in the style of <see cref="ItemSketch"/>: ink lines,
    /// diagonal hatching on the shaded side, a faint wash of colour.
    /// </summary>
    public static class PlaceSketch
    {
        public const int Size = ItemSketch.Size;

        private static readonly Color Ink = new Color(0.1f, 0.1f, 0.14f, 1f);
        private static Texture2D stump;
        private static Texture2D boulder;

        /// <summary>A tree stump, as the tool journal's first entry shows it.</summary>
        public static Texture2D Stump => stump != null ? stump : stump = PaintStump();

        /// <summary>A stone boulder, as the tool journal's second entry shows it.</summary>
        public static Texture2D Boulder => boulder != null ? boulder : boulder = PaintBoulder();

        private static Texture2D PaintStump()
        {
            var pixels = new Color[Size * Size];
            const float cx = 64f, rx = 34f, ry = 11f, top = 82f, bottom = 34f;
            bool InSide(float x, float y)
            {
                // The roots flare out towards the foot.
                float rise = 1f - Mathf.Clamp01((y - (bottom - 4f)) / 18f);
                float width = rx + 10f * rise * rise;
                float dx = (x - cx) / width;
                if (Mathf.Abs(dx) > 1f)
                {
                    return false;
                }
                float arc = ry * Mathf.Sqrt(1f - dx * dx);
                return y > bottom - arc && y < top;
            }
            bool InTop(float x, float y)
            {
                float dx = (x - cx) / rx, dy = (y - top) / ry;
                return dx * dx + dy * dy <= 1f;
            }

            float[] side = Coverage((x, y) => InSide(x, y) || InTop(x, y));
            float[] face = Coverage(InTop);
            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    int i = y * Size + x;
                    if (face[i] > 0.5f)
                    {
                        pixels[i] = Wash(new Color(0.86f, 0.74f, 0.52f));
                    }
                    else if (side[i] > 0.5f)
                    {
                        // Bark: hatching on the shaded right side, a brown wash on the rest.
                        bool shaded = x > cx + rx * 0.25f && (x + y) % 4 == 0;
                        pixels[i] = shaded ? new Color(Ink.r, Ink.g, Ink.b, 0.7f) : Wash(new Color(0.42f, 0.3f, 0.2f));
                    }
                }
            }
            Outline(pixels, side);
            Outline(pixels, face);
            // Growth rings on the cut, and a crack across them.
            for (int ring = 1; ring <= 3; ring++)
            {
                float scale = ring / 4f;
                Ellipse(pixels, new Vector2(cx + 2f, top), rx * scale, ry * scale, 0.9f, 0.75f);
            }
            Line(pixels, new Vector2(cx + 2f, top), new Vector2(cx + rx * 0.7f, top + ry * 0.45f), 0.9f, 0.9f);
            // Bark furrows down the side.
            for (int k = -3; k <= 3; k++)
            {
                float x = cx + k * 9f + (k % 2) * 2f;
                float edge = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((x - cx) / rx, 2f)));
                Line(pixels, new Vector2(x, top - edge - 3f), new Vector2(x + (k % 2 == 0 ? 1.5f : -1.5f), bottom - edge + 6f), 0.8f, 0.55f);
            }
            // The ground it stands in.
            Line(pixels, new Vector2(10f, bottom - ry * 0.6f - 2f), new Vector2(118f, bottom - ry * 0.6f - 2f), 1f, 0.8f);
            return Make(pixels, "Tree stump sketch");
        }

        private static Texture2D PaintBoulder()
        {
            var pixels = new Color[Size * Size];
            var centre = new Vector2(64f, 58f);
            const float ground = 26f;
            bool Inside(float x, float y)
            {
                if (y < ground)
                {
                    return false;
                }
                float dx = x - centre.x, dy = (y - centre.y) * 1.15f;
                float angle = Mathf.Atan2(dy, dx);
                float radius = 44f * (1f + 0.1f * Mathf.Sin(3f * angle + 1f) + 0.07f * Mathf.Sin(5f * angle + 2.3f) + 0.04f * Mathf.Sin(9f * angle));
                return dx * dx + dy * dy <= radius * radius;
            }

            float[] cover = Coverage(Inside);
            var light = new Vector2(-0.6f, 0.8f);
            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    int i = y * Size + x;
                    if (cover[i] < 0.5f)
                    {
                        continue;
                    }
                    Vector2 from = new Vector2(x - centre.x, y - centre.y) / 44f;
                    float lit = Vector2.Dot(from, light);
                    // Shaded away from the light: hatching, crossed where it's darkest.
                    bool hatch = (lit < -0.15f && (x + y) % 4 == 0) || (lit < -0.45f && (x - y + 256) % 4 == 0);
                    pixels[i] = hatch ? new Color(Ink.r, Ink.g, Ink.b, 0.7f) : Wash(new Color(0.58f, 0.58f, 0.6f));
                }
            }
            Outline(pixels, cover);
            // Cracks and a ledge across the face.
            Line(pixels, new Vector2(52f, 92f), new Vector2(60f, 74f), 0.9f, 0.9f);
            Line(pixels, new Vector2(60f, 74f), new Vector2(56f, 60f), 0.9f, 0.9f);
            Line(pixels, new Vector2(60f, 74f), new Vector2(74f, 66f), 0.9f, 0.8f);
            Line(pixels, new Vector2(30f, 58f), new Vector2(48f, 54f), 0.8f, 0.7f);
            Line(pixels, new Vector2(80f, 88f), new Vector2(94f, 80f), 0.8f, 0.7f);
            // The ground, and a few pebbles beside it.
            Line(pixels, new Vector2(6f, ground - 1f), new Vector2(122f, ground - 1f), 1f, 0.8f);
            Ellipse(pixels, new Vector2(16f, ground + 3f), 4f, 2.5f, 0.8f, 0.8f);
            Ellipse(pixels, new Vector2(112f, ground + 2f), 3f, 2f, 0.8f, 0.8f);
            return Make(pixels, "Stone boulder sketch");
        }

        // How much of each pixel a shape covers, from four samples a pixel.
        private static float[] Coverage(System.Func<float, float, bool> inside)
        {
            var cover = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        hits += inside(x + 0.25f + 0.5f * (s & 1), y + 0.25f + 0.5f * (s >> 1)) ? 1 : 0;
                    }
                    cover[y * Size + x] = hits / 4f;
                }
            }
            return cover;
        }

        // Ink where the coverage changes: the shape's outline, about two pixels wide.
        private static void Outline(Color[] pixels, float[] cover)
        {
            for (int y = 2; y < Size - 2; y++)
            {
                for (int x = 2; x < Size - 2; x++)
                {
                    int i = y * Size + x;
                    float edge = Mathf.Abs(cover[i + 1] - cover[i - 1]) + Mathf.Abs(cover[i + Size] - cover[i - Size])
                                 + 0.5f * (Mathf.Abs(cover[i + 2] - cover[i - 2]) + Mathf.Abs(cover[i + 2 * Size] - cover[i - 2 * Size]));
                    if (edge > 0.5f)
                    {
                        Put(pixels, i, Mathf.Clamp01(edge));
                    }
                }
            }
        }

        // An ink line from a to b, width in pixels, with soft edges.
        private static void Line(Color[] pixels, Vector2 a, Vector2 b, float width, float alpha)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width - 1f));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width - 1f));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width + 1f));
            Vector2 along = b - a;
            float length = Mathf.Max(1e-4f, along.sqrMagnitude);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, along) / length);
                    float distance = (a + along * t - p).magnitude;
                    float ink = Mathf.Clamp01(width - distance + 0.5f) * alpha;
                    if (ink > 0f)
                    {
                        Put(pixels, y * Size + x, ink);
                    }
                }
            }
        }

        private static void Ellipse(Color[] pixels, Vector2 centre, float rx, float ry, float width, float alpha)
        {
            const int steps = 40;
            Vector2 previous = centre + new Vector2(rx, 0f);
            for (int s = 1; s <= steps; s++)
            {
                float angle = s * 2f * Mathf.PI / steps;
                Vector2 next = centre + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
                Line(pixels, previous, next, width, alpha);
                previous = next;
            }
        }

        // Ink laid over what's there, never lighter.
        private static void Put(Color[] pixels, int i, float alpha)
        {
            if (pixels[i].a < alpha || pixels[i].r > 0.2f)
            {
                pixels[i] = new Color(Ink.r, Ink.g, Ink.b, Mathf.Max(alpha, pixels[i].r > 0.2f ? alpha : pixels[i].a));
            }
        }

        private static Color Wash(Color colour)
        {
            Color wash = Color.Lerp(new Color(0.5f, 0.5f, 0.52f), colour, 0.6f);
            wash.a = 0.35f;
            return wash;
        }

        private static Texture2D Make(Color[] pixels, string name)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = name, hideFlags = HideFlags.DontSave };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
