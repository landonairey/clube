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
        /// The sketch of an ore, painted the first time it's asked for. For an ore not yet told apart
        /// (GL77, GL91) only its outline is drawn: no wash, speckles or hatching.
        /// </summary>
        public static Texture2D For(VoxelMaterial ore, bool known = true)
        {
            if (ore == null)
            {
                return null;
            }
            if (!Cache.TryGetValue((ore, known), out Texture2D texture) || texture == null)
            {
                texture = Paint(ore, known);
                Cache[(ore, known)] = texture;
            }
            return texture;
        }

        private static Texture2D Paint(VoxelMaterial ore, bool known)
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

            if (known)
            {
                Shade(texture, outline, centre, ore.DebugColor, ore.SketchStyle, random);
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

        // Fills the outline in, by the ore's style (GL627, GL628): a grey wash with speckles of the ore's colour, or with
        // crystals of it, and hatching on the shadow side; or the ore's colour right through.
        private static void Shade(Texture2D texture, Vector2[] outline, Vector2 centre, Color oreColour, OreSketchStyle style, System.Random random)
        {
            if (style == OreSketchStyle.Solid)
            {
                FillSolid(texture, outline, centre, oreColour, random);
                return;
            }
            if (style == OreSketchStyle.Crystals)
            {
                Fill(texture, outline, Wash);
                Crystals(texture, outline, centre, oreColour, random);
                Hatch(texture, outline, centre);
                return;
            }
            Speckle(texture, outline, centre, oreColour, random);
            Hatch(texture, outline, centre);
        }

        private static void Fill(Texture2D texture, Vector2[] outline, Color colour)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (Inside(outline, new Vector2(x, y)))
                    {
                        texture.SetPixel(x, y, colour);
                    }
                }
            }
        }

        // Coal (GL628): the ore's colour over the whole lump, a little uneven like ink laid on with a brush, and a few
        // short pale strokes where its glassy faces catch the light. No wash or hatching: there's no paper left to show.
        private static void FillSolid(Texture2D texture, Vector2[] outline, Vector2 centre, Color oreColour, System.Random random)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (Inside(outline, new Vector2(x, y)))
                    {
                        float shade = 0.8f + (float)random.NextDouble() * 0.5f;
                        texture.SetPixel(x, y, new Color(oreColour.r * shade, oreColour.g * shade, oreColour.b * shade, 1f));
                    }
                }
            }
            var glint = new Color(0.5f, 0.5f, 0.56f, 0.55f);
            for (int i = 0; i < 7; i++)
            {
                var from = new Vector2(centre.x + ((float)random.NextDouble() - 0.6f) * 84f, centre.y + ((float)random.NextDouble() - 0.35f) * 50f);
                var to = from + new Vector2(5f + (float)random.NextDouble() * 6f, 3f + (float)random.NextDouble() * 4f);
                if (Inside(outline, from) && Inside(outline, to))
                {
                    Line(texture, from, to, 0.5f, glint);
                }
            }
        }

        // Magnetite (GL627): octahedra seen end on, diamonds of the ore's colour with the upper left half a bright facet.
        private static void Crystals(Texture2D texture, Vector2[] outline, Vector2 centre, Color oreColour, System.Random random)
        {
            Color facet = Color.Lerp(oreColour, Color.white, 0.62f);
            facet.a = 1f;
            for (int i = 0; i < 30; i++)
            {
                var spot = new Vector2(centre.x + ((float)random.NextDouble() - 0.5f) * 104f, centre.y + ((float)random.NextDouble() - 0.5f) * 66f);
                int reach = 5 + random.Next(5);
                if (!Inside(outline, spot + Vector2.right * reach) || !Inside(outline, spot - Vector2.right * reach)
                    || !Inside(outline, spot + Vector2.up * reach) || !Inside(outline, spot - Vector2.up * reach))
                {
                    continue;
                }
                int cx = Mathf.RoundToInt(spot.x), cy = Mathf.RoundToInt(spot.y);
                for (int dy = -reach; dy <= reach; dy++)
                {
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int from = Mathf.Abs(dx) + Mathf.Abs(dy);
                        if (from > reach)
                        {
                            continue;
                        }
                        // Inked along its edge and down the ridge between its two faces.
                        int side = dy - dx;
                        Color colour = from == reach || side == 0 || side == 1 ? Ink : side > 1 ? facet : new Color(oreColour.r, oreColour.g, oreColour.b, 1f);
                        texture.SetPixel(cx + dx, cy + dy, colour);
                    }
                }
            }
        }

        // A grey wash and speckles of the ore's colour.
        private static void Speckle(Texture2D texture, Vector2[] outline, Vector2 centre, Color oreColour, System.Random random)
        {
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
        }

        // Hatching on the lower right, the side away from the light.
        private static void Hatch(Texture2D texture, Vector2[] outline, Vector2 centre)
        {
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
