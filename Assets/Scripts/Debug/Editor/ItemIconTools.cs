using System.IO;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Paints stand-in icons (GL7, GL31, GL39), until real art exists: the pickaxe and shovel, the copper bun (a rough dome,
    /// as a smelt leaves it), the copper ingot (a bar seen from above at an angle), wood (a log
    /// end-on, with its rings) and leaves (one leaf). <i>Clube → Items → Generate icons</i> writes them to
    /// <c>Assets/Textures/Items/</c> and links each to its item. Other material drops use their
    /// material's terrain texture.
    /// </summary>
    public static class ItemIconTools
    {
        private const string Folder = "Assets/Textures/Items";
        private const string ItemFolder = "Assets/Config/Items";
        private const int Size = 64;

        private static readonly Color Copper = new Color(0.78f, 0.42f, 0.2f);
        private static readonly Color CopperLight = new Color(1f, 0.72f, 0.45f);
        private static readonly Color CopperDark = new Color(0.4f, 0.17f, 0.08f);
        private static readonly Color Tin = new Color(0.68f, 0.7f, 0.72f);
        private static readonly Color TinLight = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color TinDark = new Color(0.32f, 0.33f, 0.36f);
        private static readonly Color Bronze = new Color(0.7f, 0.5f, 0.24f);
        private static readonly Color BronzeLight = new Color(0.95f, 0.78f, 0.45f);
        private static readonly Color BronzeDark = new Color(0.34f, 0.22f, 0.08f);

        [MenuItem("Clube/Items/Generate icons")]
        public static void GenerateIcons()
        {
            Directory.CreateDirectory(Folder);
            Write("copper bun", "Copper bun", PaintBun(Copper, CopperLight, CopperDark));
            Write("copper ingot", "Copper ingot", PaintIngot(Copper, CopperLight, CopperDark));
            Write("tin bun", "Tin bun", PaintBun(Tin, TinLight, TinDark));
            Write("tin ingot", "Tin ingot", PaintIngot(Tin, TinLight, TinDark));
            Write("bronze bun", "Bronze bun", PaintBun(Bronze, BronzeLight, BronzeDark));
            Write("bronze ingot", "Bronze ingot", PaintIngot(Bronze, BronzeLight, BronzeDark));
            Write("wood", "Wood", PaintLog());
            Write("leaves", "Leaves", PaintLeaf());
            Write("clay", "Clay", PaintClay());
            Write("brick", "Brick", PaintBrick());
            Write("pickaxe", "Pickaxe", PaintPickaxe());
            Write("shovel", "Shovel", PaintShovel());
            Write("stick", "Stick", PaintStick());
            Write("spear", "Spear", PaintSpear());
            Write("stone hatchet", "Stone hatchet", PaintHatchet());
            Write("stone hammer", "Stone hammer", PaintHammer());
            Write("pointy stick", "Pointy stick", PaintPointyStick());
            Write("stone pick", "Stone pick", PaintStonePick());
            Write("campfire", "Campfire", PaintCampfire());
            Write("unknown ore", "Unknown ore", PaintUnknownOre());
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Wrote item icons to {Folder}.");
        }

        private static void Write(string file, string item, Texture2D texture)
        {
            string path = $"{Folder}/{file}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{item}.asset");
            if (definition == null)
            {
                UnityEngine.Debug.LogWarning($"No item at {ItemFolder}/{item}.asset to give the icon to.");
                return;
            }
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // A dome lit from the upper left, with a dark rim and a pitted, uneven surface.
        private static Texture2D PaintBun(Color metal, Color bright, Color dark)
        {
            Texture2D texture = Blank();
            var random = new System.Random(7);
            var centre = new Vector2(Size * 0.5f, Size * 0.45f);
            var radii = new Vector2(Size * 0.42f, Size * 0.3f);
            var light = new Vector2(-0.45f, 0.55f).normalized;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 p = new Vector2((x - centre.x) / radii.x, (y - centre.y) / radii.y);
                    float r2 = p.sqrMagnitude;
                    if (r2 > 1f)
                    {
                        continue;
                    }
                    float height = Mathf.Sqrt(1f - r2);
                    float shade = Mathf.Clamp01(0.55f + 0.45f * Vector2.Dot(p, light) * (1f - height) + 0.35f * height);
                    Color color = Color.Lerp(dark, metal, shade);
                    if (shade > 0.85f)
                    {
                        color = Color.Lerp(color, bright, (shade - 0.85f) / 0.15f);
                    }
                    // Pits and grain from the smelt.
                    color *= 0.88f + 0.2f * (float)random.NextDouble();
                    float edge = Mathf.Clamp01((1f - r2) * 6f);
                    color.a = edge;
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            return texture;
        }

        // A trapezoid bar: a bright top face, a darker front face below it, and an outline.
        private static Texture2D PaintIngot(Color metal, Color light, Color dark)
        {
            Texture2D texture = Blank();
            var random = new System.Random(11);
            float topY0 = Size * 0.46f, topY1 = Size * 0.7f;
            float frontY0 = Size * 0.24f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color? color = null;
                    if (y >= topY0 && y <= topY1)
                    {
                        // Top face, narrower at the back.
                        float t = (y - topY0) / (topY1 - topY0);
                        float half = Mathf.Lerp(Size * 0.36f, Size * 0.28f, t);
                        if (Mathf.Abs(x - Size * 0.5f) <= half)
                        {
                            color = Color.Lerp(metal, light, 0.35f + 0.4f * t);
                        }
                    }
                    else if (y >= frontY0 && y < topY0)
                    {
                        // Front face, tapering in towards the bottom.
                        float t = (topY0 - y) / (topY0 - frontY0);
                        float half = Mathf.Lerp(Size * 0.36f, Size * 0.4f, t);
                        if (Mathf.Abs(x - Size * 0.5f) <= half)
                        {
                            color = Color.Lerp(metal, dark, 0.25f + 0.35f * t);
                        }
                    }
                    if (color.HasValue)
                    {
                        Color c = color.Value * (0.94f + 0.1f * (float)random.NextDouble());
                        c.a = 1f;
                        texture.SetPixel(x, y, c);
                    }
                }
            }
            Outline(texture, dark);
            texture.Apply();
            return texture;
        }

        // A lump of wet clay (GL34): a lumpy blob lit from the upper left, with a soft sheen.
        private static Texture2D PaintClay()
        {
            Texture2D texture = Blank();
            var random = new System.Random(13);
            var dark = new Color(0.42f, 0.28f, 0.2f);
            var mid = new Color(0.66f, 0.48f, 0.36f);
            var sheen = new Color(0.85f, 0.7f, 0.58f);
            var centre = new Vector2(Size * 0.5f, Size * 0.42f);
            var light = new Vector2(-0.45f, 0.55f).normalized;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 offset = new Vector2(x - centre.x, y - centre.y);
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    // A wobbly outline, so it reads as a lump rather than a dome.
                    float radius = Size * (0.36f + 0.04f * Mathf.Sin(angle * 3f + 0.7f) + 0.025f * Mathf.Sin(angle * 5f));
                    Vector2 p = new Vector2(offset.x / radius, offset.y / (radius * 0.78f));
                    float r2 = p.sqrMagnitude;
                    if (r2 > 1f)
                    {
                        continue;
                    }
                    float height = Mathf.Sqrt(1f - r2);
                    float shade = Mathf.Clamp01(0.5f + 0.5f * Vector2.Dot(p, light) * (1f - height) + 0.3f * height);
                    Color color = Color.Lerp(dark, mid, shade);
                    if (shade > 0.8f)
                    {
                        color = Color.Lerp(color, sheen, (shade - 0.8f) / 0.2f * 0.6f);
                    }
                    color *= 0.95f + 0.08f * (float)random.NextDouble();
                    color.a = Mathf.Clamp01((1f - r2) * 6f);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            return texture;
        }

        // A brick seen from above at an angle, like the ingot: a lighter top face, a darker
        // front face, a few pits, and an outline.
        private static Texture2D PaintBrick()
        {
            Texture2D texture = Blank();
            var random = new System.Random(17);
            var light = new Color(0.78f, 0.38f, 0.25f);
            var face = new Color(0.62f, 0.27f, 0.17f);
            var dark = new Color(0.3f, 0.12f, 0.08f);
            float left = Size * 0.14f, right = Size * 0.86f;
            float frontY0 = Size * 0.26f, topY0 = Size * 0.5f, topY1 = Size * 0.72f;
            float skew = Size * 0.08f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color? color = null;
                    if (y >= frontY0 && y < topY0 && x >= left && x <= right)
                    {
                        color = face;
                    }
                    else if (y >= topY0 && y <= topY1)
                    {
                        // The top face leans back to the right.
                        float shift = (y - topY0) / (topY1 - topY0) * skew;
                        if (x >= left + shift && x <= right + shift && x < Size)
                        {
                            color = light;
                        }
                    }
                    else if (y >= frontY0 && y < topY1 && x > right)
                    {
                        // The right end, in shadow, widening towards the top.
                        if (x <= right + (y - frontY0) / (topY1 - frontY0) * skew)
                        {
                            color = Color.Lerp(face, dark, 0.45f);
                        }
                    }
                    if (color.HasValue)
                    {
                        Color c = color.Value * (0.9f + 0.15f * (float)random.NextDouble());
                        if (random.NextDouble() < 0.03)
                        {
                            c *= 0.7f;
                        }
                        c.a = 1f;
                        texture.SetPixel(x, y, c);
                    }
                }
            }
            Outline(texture, dark);
            texture.Apply();
            return texture;
        }

        // A short log lying across the icon: a bark-brown side, and its cut end facing the viewer
        // with growth rings around a dark heart.
        private static Texture2D PaintLog()
        {
            Texture2D texture = Blank();
            var random = new System.Random(23);
            var bark = new Color(0.36f, 0.24f, 0.14f);
            var barkDark = new Color(0.22f, 0.14f, 0.08f);
            var sapwood = new Color(0.85f, 0.70f, 0.48f);
            var heart = new Color(0.62f, 0.42f, 0.24f);
            var end = new Vector2(Size * 0.3f, Size * 0.5f);
            float radius = Size * 0.24f;
            float right = Size * 0.86f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dy = y - end.y;
                    Color? color = null;
                    if (x >= end.x && x <= right && Mathf.Abs(dy) <= radius)
                    {
                        // The side: rounded shading, with streaks of bark along it.
                        float round = 1f - Mathf.Abs(dy) / radius;
                        color = Color.Lerp(barkDark, bark, 0.4f + 0.6f * round);
                        if (random.NextDouble() < 0.12)
                        {
                            color = color.Value * 0.7f;
                        }
                    }
                    float d = Vector2.Distance(new Vector2(x, y), end);
                    if (d <= radius)
                    {
                        // The cut end: rings every few pixels, darker towards the heart.
                        float ring = Mathf.Repeat(d / 2.6f, 1f);
                        Color wood = Color.Lerp(heart, sapwood, d / radius);
                        color = ring < 0.25f ? wood * 0.8f : wood;
                        if (d > radius - 2f)
                        {
                            color = bark;
                        }
                    }
                    if (color.HasValue)
                    {
                        Color c = color.Value * (0.95f + 0.08f * (float)random.NextDouble());
                        c.a = 1f;
                        texture.SetPixel(x, y, c);
                    }
                }
            }
            Outline(texture, barkDark);
            texture.Apply();
            return texture;
        }

        // One leaf on a slant: a pointed oval, lighter on top, with a midrib and a short stem.
        private static Texture2D PaintLeaf()
        {
            Texture2D texture = Blank();
            var random = new System.Random(29);
            var green = new Color(0.25f, 0.52f, 0.18f);
            var light = new Color(0.45f, 0.70f, 0.28f);
            var dark = new Color(0.10f, 0.25f, 0.07f);
            var centre = new Vector2(Size * 0.52f, Size * 0.54f);
            var along = new Vector2(1f, 1f).normalized;
            var across = new Vector2(-along.y, along.x);
            float length = Size * 0.4f;
            float width = Size * 0.2f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 offset = new Vector2(x, y) - centre;
                    float u = Vector2.Dot(offset, along) / length;
                    float v = Vector2.Dot(offset, across) / width;
                    // Narrower towards both tips than an ellipse, for a leaf's point.
                    float half = Mathf.Pow(Mathf.Max(0f, 1f - u * u), 0.75f);
                    Color? color = null;
                    if (Mathf.Abs(u) <= 1f && Mathf.Abs(v) <= half)
                    {
                        color = Color.Lerp(green, light, 0.5f + 0.5f * v / Mathf.Max(half, 0.01f) * 0.6f);
                        if (Mathf.Abs(v) < 0.06f || (Mathf.Abs(Mathf.Repeat(u * 4f + Mathf.Abs(v) * 1.5f, 1f) - 0.5f) < 0.05f && Mathf.Abs(v) < half * 0.8f))
                        {
                            // Midrib and side veins.
                            color = Color.Lerp(color.Value, light, 0.6f);
                        }
                    }
                    else if (u < -1f && u > -1.35f && Mathf.Abs(v) < 0.08f)
                    {
                        color = dark;
                    }
                    if (color.HasValue)
                    {
                        Color c = color.Value * (0.95f + 0.08f * (float)random.NextDouble());
                        c.a = 1f;
                        texture.SetPixel(x, y, c);
                    }
                }
            }
            Outline(texture, dark);
            texture.Apply();
            return texture;
        }

        private static readonly Color Steel = new Color(0.62f, 0.66f, 0.72f);
        private static readonly Color SteelLight = new Color(0.86f, 0.9f, 0.95f);
        private static readonly Color Handle = new Color(0.55f, 0.36f, 0.18f);
        private static readonly Color HandleLight = new Color(0.72f, 0.5f, 0.28f);

        // A wooden handle running up to the right, with a curved steel head across its top.
        private static Texture2D PaintPickaxe()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(12f, 8f), new Vector2(46f, 46f), 3.2f, Handle);
            Line(texture, new Vector2(11f, 10f), new Vector2(45f, 48f), 1.2f, HandleLight);
            var from = new Vector2(14f, 40f);
            var bend = new Vector2(44f, 66f);
            var to = new Vector2(58f, 30f);
            Vector2 previous = from;
            for (int i = 1; i <= 16; i++)
            {
                float t = i / 16f;
                Vector2 point = (1f - t) * (1f - t) * from + 2f * (1f - t) * t * bend + t * t * to;
                // Thickest at the middle, tapering to a point at each end like a pick.
                float width = 1.2f + 4.2f * Mathf.Sin(t * Mathf.PI);
                Line(texture, previous, point, width, Steel);
                previous = point;
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A wooden shaft with a short grip across the top and a rounded steel blade at the foot.
        private static Texture2D PaintShovel()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(18f, 54f), new Vector2(40f, 26f), 3.2f, Handle);
            Line(texture, new Vector2(17f, 56f), new Vector2(39f, 28f), 1.2f, HandleLight);
            Line(texture, new Vector2(11f, 50f), new Vector2(26f, 61f), 3f, Handle);

            // The blade: an ellipse long along the shaft, centred below its end.
            var axis = new Vector2(0.62f, -0.78f).normalized;
            var across = new Vector2(axis.y, -axis.x);
            var centre = new Vector2(47f, 16f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 offset = new Vector2(x, y) - centre;
                    float along = Vector2.Dot(offset, axis) / 13f;
                    float side = Vector2.Dot(offset, across) / 10f;
                    // Flatter at the shaft end, rounder at the tip.
                    if (along * along + side * side < 1f && (along > -0.7f))
                    {
                        texture.SetPixel(x, y, side < -0.35f ? SteelLight : Steel);
                    }
                }
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        private static readonly Color StoneHead = new Color(0.55f, 0.55f, 0.58f);
        private static readonly Color StoneHeadLight = new Color(0.72f, 0.72f, 0.74f);
        private static readonly Color Lashing = new Color(0.78f, 0.68f, 0.45f);

        // A plain stick, slightly bent, with a knot.
        private static Texture2D PaintStick()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(12f, 10f), new Vector2(34f, 34f), 3.4f, Handle);
            Line(texture, new Vector2(34f, 34f), new Vector2(52f, 54f), 3f, Handle);
            Line(texture, new Vector2(12f, 12f), new Vector2(50f, 55f), 1.1f, HandleLight);
            Line(texture, new Vector2(30f, 30f), new Vector2(24f, 42f), 2f, Handle);
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A long shaft with a whittled point at the top right.
        private static Texture2D PaintSpear()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(6f, 6f), new Vector2(50f, 50f), 3f, Handle);
            Line(texture, new Vector2(6f, 8f), new Vector2(49f, 51f), 1f, HandleLight);
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                Line(texture, Vector2.Lerp(new Vector2(48f, 48f), new Vector2(60f, 60f), t), Vector2.Lerp(new Vector2(48f, 48f), new Vector2(60f, 60f), t), Mathf.Lerp(4.5f, 1f, t), HandleLight);
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A short handle with a stone blade lashed across its top.
        private static Texture2D PaintHatchet()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(14f, 8f), new Vector2(36f, 50f), 3.4f, Handle);
            Line(texture, new Vector2(13f, 10f), new Vector2(35f, 51f), 1.1f, HandleLight);
            // The blade: a wedge, wide at its cutting edge on the right.
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                Vector2 from = Vector2.Lerp(new Vector2(30f, 50f), new Vector2(54f, 42f), t);
                Line(texture, from, from + new Vector2(0f, Mathf.Lerp(6f, 18f, t)), 3f, StoneHead);
            }
            Line(texture, new Vector2(50f, 44f), new Vector2(54f, 58f), 1.6f, StoneHeadLight);
            Line(texture, new Vector2(30f, 47f), new Vector2(40f, 53f), 2f, Lashing);
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A handle with a blocky stone head lashed across its top.
        private static Texture2D PaintHammer()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(18f, 6f), new Vector2(32f, 46f), 3.4f, Handle);
            Line(texture, new Vector2(17f, 8f), new Vector2(31f, 47f), 1.1f, HandleLight);
            Line(texture, new Vector2(16f, 50f), new Vector2(48f, 42f), 12f, StoneHead);
            Line(texture, new Vector2(17f, 54f), new Vector2(47f, 47f), 2.5f, StoneHeadLight);
            Line(texture, new Vector2(28f, 42f), new Vector2(36f, 50f), 2f, Lashing);
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A short stick whittled to a point at the top right.
        private static Texture2D PaintPointyStick()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(12f, 10f), new Vector2(44f, 42f), 3.4f, Handle);
            Line(texture, new Vector2(12f, 12f), new Vector2(43f, 43f), 1.1f, HandleLight);
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                Vector2 at = Vector2.Lerp(new Vector2(42f, 40f), new Vector2(54f, 52f), t);
                Line(texture, at, at, Mathf.Lerp(4f, 1f, t), new Color(0.86f, 0.74f, 0.52f));
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A handle with a pointed stone head lashed across its top.
        private static Texture2D PaintStonePick()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(14f, 8f), new Vector2(34f, 48f), 3.4f, Handle);
            Line(texture, new Vector2(13f, 10f), new Vector2(33f, 49f), 1.1f, HandleLight);
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                Vector2 at = Vector2.Lerp(new Vector2(16f, 52f), new Vector2(54f, 44f), t);
                // Thickest in the middle, a point at each end.
                Line(texture, at, at, 1.5f + 6f * Mathf.Sin(t * Mathf.PI), t < 0.5f ? StoneHead : StoneHeadLight);
            }
            Line(texture, new Vector2(30f, 44f), new Vector2(38f, 52f), 2f, Lashing);
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A ring of stones, crossed logs and a flame.
        private static Texture2D PaintCampfire()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(14f, 18f), new Vector2(50f, 30f), 5f, Handle);
            Line(texture, new Vector2(14f, 30f), new Vector2(50f, 18f), 5f, HandleLight);
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2f / 9f;
                var at = new Vector2(32f + Mathf.Cos(angle) * 22f, 22f + Mathf.Sin(angle) * 8f);
                Line(texture, at, at, 7f, i % 2 == 0 ? StoneHead : StoneHeadLight);
            }
            var flame = new Color(1f, 0.55f, 0.15f);
            var core = new Color(1f, 0.88f, 0.4f);
            for (int i = 0; i < 10; i++)
            {
                float t = i / 9f;
                Vector2 at = new Vector2(32f + Mathf.Sin(t * 5f) * 3f, 26f + t * 28f);
                Line(texture, at, at, Mathf.Lerp(14f, 2f, t), flame);
                Line(texture, at, at, Mathf.Lerp(7f, 0.5f, t), core);
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A grey lump of rock with a question mark: ore the player can't tell yet (GL54).
        private static Texture2D PaintUnknownOre()
        {
            Texture2D texture = Blank();
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                var at = new Vector2(32f + Mathf.Cos(angle) * 10f, 30f + Mathf.Sin(angle) * 8f);
                Line(texture, at, at, 18f, i % 2 == 0 ? StoneHead : StoneHeadLight);
            }
            var mark = new Color(0.14f, 0.14f, 0.16f);
            Vector2 previous = new Vector2(25f, 42f);
            for (int i = 1; i <= 10; i++)
            {
                float angle = Mathf.Lerp(Mathf.PI * 0.95f, -Mathf.PI * 0.35f, i / 10f);
                var at = new Vector2(32f + Mathf.Cos(angle) * 7f, 40f + Mathf.Sin(angle) * 7f);
                Line(texture, previous, at, 3.2f, mark);
                previous = at;
            }
            Line(texture, previous, new Vector2(32f, 28f), 3.2f, mark);
            Line(texture, new Vector2(32f, 21f), new Vector2(32f, 21f), 4f, mark);
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A round-ended line from one point to the other, <width> pixels thick.
        private static void Line(Texture2D texture, Vector2 from, Vector2 to, float width, Color color)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 2f) + 1;
            float radius = width * 0.5f;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
                for (int y = Mathf.Max(0, Mathf.FloorToInt(point.y - radius)); y <= Mathf.Min(Size - 1, Mathf.CeilToInt(point.y + radius)); y++)
                {
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(point.x - radius)); x <= Mathf.Min(Size - 1, Mathf.CeilToInt(point.x + radius)); x++)
                    {
                        if ((new Vector2(x, y) - point).sqrMagnitude <= radius * radius)
                        {
                            texture.SetPixel(x, y, color);
                        }
                    }
                }
            }
        }

        private static Texture2D Blank()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var clear = new Color[Size * Size];
            texture.SetPixels(clear);
            return texture;
        }

        // Darkens opaque pixels that touch transparent ones.
        private static void Outline(Texture2D texture, Color color)
        {
            Color[] pixels = texture.GetPixels();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (pixels[y * Size + x].a < 0.5f)
                    {
                        continue;
                    }
                    bool edge = x == 0 || y == 0 || x == Size - 1 || y == Size - 1
                        || pixels[y * Size + x - 1].a < 0.5f || pixels[y * Size + x + 1].a < 0.5f
                        || pixels[(y - 1) * Size + x].a < 0.5f || pixels[(y + 1) * Size + x].a < 0.5f;
                    if (edge)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }
    }
}
