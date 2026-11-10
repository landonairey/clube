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
        private static readonly Color Silver = new Color(0.76f, 0.78f, 0.82f);
        private static readonly Color SilverLight = new Color(0.98f, 0.99f, 1f);
        private static readonly Color SilverDark = new Color(0.36f, 0.38f, 0.42f);
        private static readonly Color Gold = new Color(0.9f, 0.7f, 0.18f);
        private static readonly Color GoldLight = new Color(1f, 0.93f, 0.55f);
        private static readonly Color GoldDark = new Color(0.46f, 0.3f, 0.04f);

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
            // GL225: silver and gold smelt like the rest.
            Write("silver bun", "Silver bun", PaintBun(Silver, SilverLight, SilverDark));
            Write("silver ingot", "Silver ingot", PaintIngot(Silver, SilverLight, SilverDark));
            Write("gold bun", "Gold bun", PaintBun(Gold, GoldLight, GoldDark));
            Write("gold ingot", "Gold ingot", PaintIngot(Gold, GoldLight, GoldDark));
            // Birch (GL150): pale bark with dark marks.
            Write("wood", "Wood", PaintLog(new Color(0.86f, 0.84f, 0.78f), new Color(0.32f, 0.3f, 0.28f), new Color(0.9f, 0.82f, 0.62f), new Color(0.76f, 0.64f, 0.44f)));
            Write("maple wood", "Maple wood", PaintLog(new Color(0.36f, 0.24f, 0.14f), new Color(0.22f, 0.14f, 0.08f), new Color(0.85f, 0.7f, 0.48f), new Color(0.62f, 0.42f, 0.24f)));
            Write("hand axe", "Hand axe", PaintHandAxe());
            Write("crushed stone", "Crushed stone", PaintGravel(new Color(0.55f, 0.55f, 0.57f), null));
            Write("crushed cassiterite", "Crushed cassiterite", PaintGravel(new Color(0.5f, 0.5f, 0.52f), new Color(0.22f, 0.15f, 0.1f)));
            Write("crushed chalcopyrite", "Crushed chalcopyrite", PaintGravel(new Color(0.5f, 0.5f, 0.52f), MaterialTextureTools.ChalcopyriteFleck));
            Write("crushed malachite", "Crushed malachite", PaintGravel(new Color(0.5f, 0.5f, 0.52f), new Color(0.48f, 0.66f, 0.3f)));
            WriteCrushedPreciousOres();
            Write("leaves", "Leaves", PaintLeaf());
            Write("clay", "Clay", PaintClay());
            Write("brick", "Brick", PaintBrick());
            Write("shovel", "Shovel", PaintShovel());
            Write("stick", "Stick", PaintStick());
            Write("spear", "Spear", PaintSpear());
            Write("stone hatchet", "Stone hatchet", PaintHatchet());
            Write("stone hammer", "Stone hammer", PaintHammer());
            Write("stone pick", "Stone pick", PaintStonePick());
            Write("campfire", "Campfire", PaintCampfire());
            Write("unknown ore", "Unknown ore", PaintUnknownOre());
            Write("bronze pick", "Bronze pick", PaintPickaxe(Bronze));
            Write("bronze hatchet", "Bronze hatchet", PaintHatchet(Bronze, BronzeLight));
            Write("bronze shovel", "Bronze shovel", PaintShovel(Bronze, BronzeLight));
            Write("bronze chisel", "Bronze chisel", PaintChisel());
            Write("wooden chest", "Wooden chest", PaintChest());
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Wrote item icons to {Folder}.");
        }

        /// <summary>Writes only the crushed chalcopyrite icon again, in the ore's current colour (GL346).</summary>
        internal static void WriteCrushedChalcopyrite()
        {
            Write("crushed chalcopyrite", "Crushed chalcopyrite", PaintGravel(new Color(0.5f, 0.5f, 0.52f), MaterialTextureTools.ChalcopyriteFleck));
        }

        /// <summary>Writes the crushed silver and gold ore icons (GL389).</summary>
        public static void WriteCrushedPreciousOres()
        {
            Write("crushed silver ore", "Crushed silver ore", PaintGravel(new Color(0.5f, 0.5f, 0.52f), new Color(0.86f, 0.87f, 0.9f)));
            Write("crushed gold ore", "Crushed gold ore", PaintGravel(new Color(0.5f, 0.5f, 0.52f), new Color(0.95f, 0.75f, 0.2f)));
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
        private static Texture2D PaintLog(Color bark, Color barkDark, Color sapwood, Color heart)
        {
            Texture2D texture = Blank();
            var random = new System.Random(23);
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

        // The same shape with a head of another metal (bronze tools, GL63).
        private static Texture2D PaintPickaxe(Color head)
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
                Line(texture, previous, point, width, head);
                previous = point;
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A wooden shaft with a short grip across the top and a rounded steel blade at the foot.
        private static Texture2D PaintShovel() => PaintShovel(Steel, SteelLight);

        private static Texture2D PaintShovel(Color blade, Color bladeLight)
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
                        texture.SetPixel(x, y, side < -0.35f ? bladeLight : blade);
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
        /// <summary>Writes only the coffee icon (GL456), leaving the rest as they are.</summary>
        [MenuItem("Clube/Items/Generate coffee icon")]
        public static void GenerateCoffeeIcon()
        {
            Write("coffee", "Coffee", PaintCoffee());
            AssetDatabase.SaveAssets();
        }

        // A mug of coffee with a handle and a curl of steam (GL456).
        private static Texture2D PaintCoffee()
        {
            Texture2D texture = Blank();
            var mug = new Color(0.9f, 0.88f, 0.82f);
            var shade = new Color(0.72f, 0.7f, 0.65f);
            var coffee = new Color(0.32f, 0.2f, 0.12f);
            FillQuad(texture, new Vector2(16f, 10f), new Vector2(44f, 10f), new Vector2(46f, 42f), new Vector2(14f, 42f), mug);
            FillQuad(texture, new Vector2(36f, 10f), new Vector2(44f, 10f), new Vector2(46f, 42f), new Vector2(38f, 42f), shade);
            FillQuad(texture, new Vector2(15f, 38f), new Vector2(45f, 38f), new Vector2(45f, 42f), new Vector2(15f, 42f), coffee);
            // The handle: a ring on the right side.
            for (int i = 0; i < 16; i++)
            {
                float a = Mathf.Lerp(-1.4f, 1.4f, i / 15f), b = Mathf.Lerp(-1.4f, 1.4f, (i + 1) / 15f);
                Line(texture, new Vector2(46f + 8f * Mathf.Cos(a), 26f + 9f * Mathf.Sin(a)), new Vector2(46f + 8f * Mathf.Cos(b), 26f + 9f * Mathf.Sin(b)), 2.4f, mug);
            }
            // Steam.
            Line(texture, new Vector2(24f, 46f), new Vector2(21f, 52f), 1.4f, new Color(0.85f, 0.85f, 0.85f, 0.8f));
            Line(texture, new Vector2(21f, 52f), new Vector2(25f, 58f), 1.4f, new Color(0.85f, 0.85f, 0.85f, 0.7f));
            Line(texture, new Vector2(34f, 46f), new Vector2(31f, 53f), 1.4f, new Color(0.85f, 0.85f, 0.85f, 0.8f));
            Line(texture, new Vector2(31f, 53f), new Vector2(35f, 59f), 1.4f, new Color(0.85f, 0.85f, 0.85f, 0.7f));
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

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
        private static Texture2D PaintHatchet() => PaintHatchet(StoneHead, StoneHeadLight);

        private static Texture2D PaintHatchet(Color head, Color headLight)
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(14f, 8f), new Vector2(36f, 50f), 3.4f, Handle);
            Line(texture, new Vector2(13f, 10f), new Vector2(35f, 51f), 1.1f, HandleLight);
            // The blade: a wedge, wide at its cutting edge on the right.
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                Vector2 from = Vector2.Lerp(new Vector2(30f, 50f), new Vector2(54f, 42f), t);
                Line(texture, from, from + new Vector2(0f, Mathf.Lerp(6f, 18f, t)), 3f, head);
            }
            Line(texture, new Vector2(50f, 44f), new Vector2(54f, 58f), 1.6f, headLight);
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

        // A hand axe (GL146): a teardrop of grey stone, flaked to an edge, with darker flake scars.
        private static Texture2D PaintHandAxe()
        {
            Texture2D texture = Blank();
            var random = new System.Random(31);
            var centre = new Vector2(Size * 0.5f, Size * 0.46f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Wide and round at the butt (bottom), narrowing to a point at the top.
                    float t = Mathf.InverseLerp(Size * 0.1f, Size * 0.9f, y);
                    float half = Size * 0.3f * Mathf.Sqrt(Mathf.Clamp01(1f - t)) * Mathf.Lerp(0.85f, 1f, Mathf.Sin(t * Mathf.PI));
                    float dx = x - centre.x;
                    if (t <= 0f || t >= 1f || Mathf.Abs(dx) > half)
                    {
                        continue;
                    }
                    float facet = Mathf.Repeat((x * 0.7f + y * 1.1f) / 9f + (float)random.NextDouble() * 0.15f, 1f);
                    Color stone = Color.Lerp(StoneHead, StoneHeadLight, 0.35f + 0.5f * (1f - Mathf.Abs(dx) / Mathf.Max(1f, half)));
                    if (facet < 0.18f)
                    {
                        stone *= 0.78f;
                    }
                    stone.a = 1f;
                    texture.SetPixel(x, y, stone);
                }
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A little heap of gravel (GL148): grey grit, flecked with ore when there is some.
        private static Texture2D PaintGravel(Color grit, Color? ore)
        {
            Texture2D texture = Blank();
            var random = new System.Random(37);
            for (int i = 0; i < 70; i++)
            {
                // Pebbles piled in a low mound: more of them low and in the middle.
                float u = (float)random.NextDouble() * 2f - 1f;
                float x = Size * 0.5f + u * Size * 0.4f;
                float top = Size * 0.18f + Size * 0.42f * (1f - u * u);
                float y = Size * 0.16f + (float)random.NextDouble() * (top - Size * 0.16f);
                float radius = 2.5f + (float)random.NextDouble() * 3f;
                Color pebble = grit * (0.75f + 0.4f * (float)random.NextDouble());
                if (ore.HasValue && random.NextDouble() < 0.3)
                {
                    pebble = ore.Value * (0.85f + 0.3f * (float)random.NextDouble());
                }
                pebble.a = 1f;
                Line(texture, new Vector2(x, y), new Vector2(x, y), radius, pebble);
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A handle with a pointed stone head lashed across its top.
        // A stick handle with a stone head lashed to its top, coming off the right side only and
        // tapering to a point, like an early stone pick (GL82).
        private static Texture2D PaintStonePick()
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(18f, 6f), new Vector2(26f, 54f), 3.4f, Handle);
            Line(texture, new Vector2(17f, 8f), new Vector2(25f, 55f), 1.1f, HandleLight);
            var from = new Vector2(24f, 49f);
            var bend = new Vector2(36f, 49f);
            var to = new Vector2(47f, 45f);
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                Vector2 at = (1f - t) * (1f - t) * from + 2f * (1f - t) * t * bend + t * t * to;
                // Broad where it's lashed to the handle, worked down to a point.
                Line(texture, at, at, Mathf.Lerp(9f, 3.5f, t), StoneHead);
                Line(texture, at + new Vector2(0f, 2f), at + new Vector2(0f, 2f), Mathf.Lerp(4f, 1.2f, t), StoneHeadLight);
            }
            Line(texture, new Vector2(21f, 44f), new Vector2(30f, 54f), 2f, Lashing);
            Line(texture, new Vector2(21f, 50f), new Vector2(29f, 45f), 2f, Lashing);
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

        // A chunky wooden handle, a dark ferrule, and a broad flat bronze blade flaring out to a wide,
        // bright cutting edge (GL87, GL93): the blade's width is what reads as a chisel, not a stick.
        private static Texture2D PaintChisel()
        {
            Texture2D texture = Blank();
            var along = new Vector2(1f, 1f).normalized;
            var across = new Vector2(-1f, 1f).normalized;
            // Handle, rounded at the butt, with a highlight down one side.
            Line(texture, new Vector2(9f, 9f), new Vector2(25f, 25f), 10f, Handle);
            Line(texture, new Vector2(8f, 12f), new Vector2(23f, 27f), 2.5f, HandleLight);
            // Ferrule.
            Line(texture, new Vector2(25f, 25f), new Vector2(29f, 29f), 11f, BronzeDark);
            // Blade: narrow at the ferrule, wide at the edge.
            var neck = new Vector2(29f, 29f);
            var tip = new Vector2(52f, 52f);
            FillQuad(texture, neck - across * 3.5f, tip - across * 9f, tip + across * 9f, neck + across * 3.5f, Bronze);
            // A light face along one half, and the bevel and edge at the tip.
            FillQuad(texture, neck, tip, tip + across * 9f, neck + across * 3.5f, Color.Lerp(Bronze, BronzeLight, 0.55f));
            FillQuad(texture, tip - along * 5f - across * 8f, tip - across * 9f, tip + across * 9f, tip - along * 5f + across * 8f, BronzeLight);
            Line(texture, tip - across * 9f, tip + across * 9f, 1.5f, new Color(1f, 0.97f, 0.88f));
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // Fills a convex four-sided shape, its corners in order.
        private static void FillQuad(Texture2D texture, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            Vector2[] corners = { a, b, c, d };
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    bool positive = false;
                    bool negative = false;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 from = corners[i];
                        Vector2 to = corners[(i + 1) % 4];
                        float side = (to.x - from.x) * (p.y - from.y) - (to.y - from.y) * (p.x - from.x);
                        positive |= side > 0f;
                        negative |= side < 0f;
                    }
                    if (!(positive && negative))
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }

        /// <summary>Writes the twenty-eighth pass's icons only (GL286, GL287, GL288, GL290), leaving the rest as they are.</summary>
        [MenuItem("Clube/Items/Generate pass 28 icons")]
        public static void GeneratePass28()
        {
            Directory.CreateDirectory(Folder);
            Write("copper pick", "Copper pick", Weathered(PaintPickaxe(Copper)));
            Write("copper hatchet", "Copper hatchet", Weathered(PaintHatchet(Copper, CopperLight)));
            Write("charcoal", "Charcoal", PaintCharcoal());
            Write("torch", "Torch", PaintTorch(false));
            Write("lit torch", "Lit torch", PaintTorch(true));
            Write("sleeping roll", "Sleeping roll", PaintSleepingRoll());
            AssetDatabase.SaveAssets();
        }

        // Copper weathers green (GL286): the metal's pixels near its edges take a patina.
        private static Texture2D Weathered(Texture2D texture)
        {
            var patina = new Color(0.33f, 0.64f, 0.5f);
            Color[] pixels = texture.GetPixels();
            var result = (Color[])pixels.Clone();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color c = pixels[y * Size + x];
                    bool metal = c.a > 0.5f && Mathf.Min(Distance(c, Copper), Mathf.Min(Distance(c, CopperLight), Distance(c, CopperDark))) < 0.14f;
                    if (!metal)
                    {
                        continue;
                    }
                    // Next to the outline (transparent or ink), with a hashed speckle further in.
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !edge; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            Color n = nx < 0 || ny < 0 || nx >= Size || ny >= Size ? Color.clear : pixels[ny * Size + nx];
                            edge = n.a < 0.5f || n.maxColorComponent < 0.2f;
                        }
                    }
                    bool speck = ((x * 73 + y * 151) % 17) == 0;
                    // Patchy along the edges, not a solid rim.
                    bool patch = ((x / 3 + y / 3) % 3) != 0;
                    if ((edge && patch) || speck)
                    {
                        result[y * Size + x] = Color.Lerp(c, patina, edge ? 0.75f : 0.45f) * new Color(1f, 1f, 1f, 0f) + new Color(0f, 0f, 0f, c.a);
                    }
                }
            }
            texture.SetPixels(result);
            texture.Apply();
            return texture;
        }

        private static float Distance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }

        // Lumps of charcoal: dark, faceted, with a dull sheen on their tops.
        private static Texture2D PaintCharcoal()
        {
            Texture2D texture = Blank();
            var dark = new Color(0.1f, 0.1f, 0.11f);
            var mid = new Color(0.2f, 0.2f, 0.22f);
            var sheen = new Color(0.42f, 0.42f, 0.46f);
            Vector2[] lumps = { new Vector2(22f, 22f), new Vector2(40f, 20f), new Vector2(31f, 36f), new Vector2(46f, 34f) };
            float[] sizes = { 11f, 9f, 12f, 8f };
            for (int i = 0; i < lumps.Length; i++)
            {
                Vector2 c = lumps[i];
                float s = sizes[i];
                FillQuad(texture, c + new Vector2(-s, -s * 0.6f), c + new Vector2(s * 0.8f, -s * 0.8f), c + new Vector2(s, s * 0.5f), c + new Vector2(-s * 0.7f, s * 0.8f), mid);
                FillQuad(texture, c + new Vector2(-s * 0.7f, s * 0.8f), c + new Vector2(s, s * 0.5f), c + new Vector2(s * 0.4f, s * 0.95f), c + new Vector2(-s * 0.3f, s), sheen);
                FillQuad(texture, c + new Vector2(-s, -s * 0.6f), c + new Vector2(s * 0.8f, -s * 0.8f), c + new Vector2(s * 0.5f, -s * 0.3f), c + new Vector2(-s * 0.6f, -s * 0.2f), dark);
            }
            Outline(texture, new Color(0.04f, 0.04f, 0.05f));
            texture.Apply();
            return texture;
        }

        // A torch leaning to the top right: a stick, a charred head bound with cord, and a flame when lit.
        private static Texture2D PaintTorch(bool lit)
        {
            Texture2D texture = Blank();
            Line(texture, new Vector2(14f, 8f), new Vector2(36f, 36f), 3.6f, Handle);
            Line(texture, new Vector2(14f, 10f), new Vector2(34f, 36f), 1.1f, HandleLight);
            Line(texture, new Vector2(35f, 35f), new Vector2(43f, 45f), 7.5f, new Color(0.12f, 0.11f, 0.1f));
            Line(texture, new Vector2(33f, 37f), new Vector2(37f, 33f), 2f, new Color(0.62f, 0.52f, 0.34f));
            if (lit)
            {
                var flame = new Color(1f, 0.55f, 0.15f);
                var core = new Color(1f, 0.92f, 0.5f);
                FillQuad(texture, new Vector2(38f, 44f), new Vector2(48f, 42f), new Vector2(54f, 60f), new Vector2(42f, 54f), flame);
                FillQuad(texture, new Vector2(41f, 45f), new Vector2(46f, 44f), new Vector2(50f, 55f), new Vector2(44f, 52f), core);
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        // A rolled sleeping roll seen from the side and a little above: the roll's body, its end spiralled, two straps.
        private static Texture2D PaintSleepingRoll()
        {
            Texture2D texture = Blank();
            var cloth = new Color(0.36f, 0.48f, 0.42f);
            var clothLight = new Color(0.5f, 0.62f, 0.54f);
            var strap = new Color(0.4f, 0.26f, 0.14f);
            FillQuad(texture, new Vector2(8f, 18f), new Vector2(48f, 18f), new Vector2(48f, 44f), new Vector2(8f, 44f), cloth);
            FillQuad(texture, new Vector2(8f, 36f), new Vector2(48f, 36f), new Vector2(48f, 44f), new Vector2(8f, 44f), clothLight);
            for (int ring = 0; ring < 3; ring++)
            {
                float r = 13f - ring * 4f;
                Color c = ring % 2 == 0 ? clothLight : cloth;
                FillQuad(texture, new Vector2(48f - r * 0.5f, 31f - r), new Vector2(48f + r * 0.5f, 31f - r), new Vector2(48f + r * 0.5f, 31f + r), new Vector2(48f - r * 0.5f, 31f + r), c);
            }
            foreach (float x in new[] { 18f, 36f })
            {
                FillQuad(texture, new Vector2(x - 2.5f, 17f), new Vector2(x + 2.5f, 17f), new Vector2(x + 2.5f, 45f), new Vector2(x - 2.5f, 45f), strap);
            }
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        /// <summary>Writes the brick mix and refractory brick icons (GL321), leaving the rest as they are.</summary>
        [MenuItem("Clube/Items/Generate refractory brick icons")]
        public static void GenerateRefractoryIcons()
        {
            Directory.CreateDirectory(Folder);
            // Sand worked into clay: the clay lump, paler and sandier, flecked with grains.
            Texture2D mix = Tinted(PaintClay(), new Color(1.22f, 1.45f, 1.5f));
            var random = new System.Random(29);
            for (int i = 0; i < 90; i++)
            {
                int x = random.Next(Size), y = random.Next(Size);
                Color pixel = mix.GetPixel(x, y);
                if (pixel.a > 0.5f)
                {
                    mix.SetPixel(x, y, Color.Lerp(pixel, new Color(0.95f, 0.9f, 0.75f), 0.7f));
                }
            }
            mix.Apply();
            Write("brick mix", "Brick mix", mix);
            // Fired sand and clay: the brick's shape in a pale buff.
            Write("refractory brick", "Refractory brick", Tinted(PaintBrick(), new Color(1.18f, 1.95f, 2.3f)));
            AssetDatabase.SaveAssets();
        }

        /// <summary>Writes the oak and maple boards icons (GL411), leaving the rest as they are.</summary>
        [MenuItem("Clube/Items/Generate boards icons")]
        public static void GenerateBoardsIcons()
        {
            Directory.CreateDirectory(Folder);
            Write("oak boards", "Oak boards", PaintBoards(new Color(0.7f, 0.53f, 0.33f), 3));
            Write("maple boards", "Maple boards", PaintBoards(new Color(0.62f, 0.38f, 0.24f), 5));
            AssetDatabase.SaveAssets();
        }

        // Three sawn planks lying in a stack, each a lit top, a shaded front and a pale end grain on the right, with grain
        // lines along the tops.
        private static Texture2D PaintBoards(Color wood, int seed)
        {
            Texture2D texture = Blank();
            var random = new System.Random(seed);
            Color dark = wood * 0.55f;
            dark.a = 1f;
            for (int plank = 0; plank < 3; plank++)
            {
                float left = Size * (0.1f + plank * 0.04f), right = Size * (0.84f - plank * 0.03f);
                float bottom = Size * (0.2f + plank * 0.19f);
                float front = Size * 0.1f, top = Size * 0.09f;
                float skew = Size * 0.06f;
                float shade = 0.92f + 0.14f * (float)random.NextDouble();
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Color? colour = null;
                        if (y >= bottom && y < bottom + front && x >= left && x <= right)
                        {
                            colour = wood * 0.78f * shade;
                        }
                        else if (y >= bottom + front && y < bottom + front + top)
                        {
                            float shift = (y - bottom - front) / top * skew;
                            if (x >= left + shift && x <= right + shift)
                            {
                                bool grain = Mathf.Abs(Mathf.Sin((y + Mathf.Sin(x * 0.3f + plank) * 0.8f) * 1.7f)) > 0.93f;
                                colour = wood * (grain ? 0.85f : 1.05f) * shade;
                            }
                        }
                        if (y >= bottom && y < bottom + front + top && x > right)
                        {
                            float shift = (y - bottom) / (front + top) * skew;
                            if (x <= right + shift)
                            {
                                colour = Color.Lerp(wood, Color.white, 0.25f) * shade;
                            }
                        }
                        if (colour.HasValue)
                        {
                            Color c = colour.Value;
                            c.a = 1f;
                            texture.SetPixel(x, y, c);
                        }
                    }
                }
            }
            // Dark outline round what was drawn, like the other icons.
            Color[] pixels = texture.GetPixels();
            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    if (pixels[x + y * Size].a > 0.5f)
                    {
                        continue;
                    }
                    if (pixels[x + 1 + y * Size].a > 0.5f || pixels[x - 1 + y * Size].a > 0.5f
                        || pixels[x + (y + 1) * Size].a > 0.5f || pixels[x + (y - 1) * Size].a > 0.5f)
                    {
                        texture.SetPixel(x, y, dark);
                    }
                }
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D Tinted(Texture2D texture, Color tint)
        {
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                pixels[i] = new Color(Mathf.Clamp01(p.r * tint.r), Mathf.Clamp01(p.g * tint.g), Mathf.Clamp01(p.b * tint.b), p.a);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Writes only the wooden bucket's icon (GL319), leaving the rest as they are.</summary>
        [MenuItem("Clube/Items/Generate bucket icon")]
        public static void GenerateBucketIcon()
        {
            Directory.CreateDirectory(Folder);
            Write("wooden bucket", "Wooden bucket", PaintBucket());
            AssetDatabase.SaveAssets();
        }

        // A wooden bucket from the front and a little above: tapered staves, two dark hoops, water at the rim, a rope handle.
        private static Texture2D PaintBucket()
        {
            Texture2D texture = Blank();
            var stave = new Color(0.62f, 0.44f, 0.25f);
            var staveLight = new Color(0.74f, 0.55f, 0.33f);
            var hoop = new Color(0.22f, 0.2f, 0.19f);
            var water = new Color(0.3f, 0.5f, 0.58f);
            var rope = new Color(0.66f, 0.56f, 0.36f);
            float bottom = Size * 0.1f, top = Size * 0.62f, half = Size * 0.22f, rim = Size * 0.3f, mid = Size * 0.5f;
            FillQuad(texture, new Vector2(mid - half, bottom), new Vector2(mid + half, bottom), new Vector2(mid + rim, top), new Vector2(mid - rim, top), stave);
            // Light staves, every other one.
            for (int i = 0; i < 5; i += 2)
            {
                float a = -1f + i * 0.4f, b = a + 0.4f;
                FillQuad(texture, new Vector2(mid + half * a, bottom), new Vector2(mid + half * b, bottom), new Vector2(mid + rim * b, top), new Vector2(mid + rim * a, top), staveLight);
            }
            foreach (float y in new[] { 0.2f, 0.52f })
            {
                float at = bottom + (top - bottom) * y, w = Mathf.Lerp(half, rim, y);
                FillQuad(texture, new Vector2(mid - w - 1f, at), new Vector2(mid + w + 1f, at), new Vector2(mid + w + 1f, at + 3f), new Vector2(mid - w - 1f, at + 3f), hoop);
            }
            FillQuad(texture, new Vector2(mid - rim, top), new Vector2(mid + rim, top), new Vector2(mid + rim * 0.9f, top + 5f), new Vector2(mid - rim * 0.9f, top + 5f), water);
            FillQuad(texture, new Vector2(mid - rim - 1f, top), new Vector2(mid - rim + 2f, top), new Vector2(mid - 2f, Size * 0.9f), new Vector2(mid - 4f, Size * 0.9f), rope);
            FillQuad(texture, new Vector2(mid + rim - 2f, top), new Vector2(mid + rim + 1f, top), new Vector2(mid + 4f, Size * 0.9f), new Vector2(mid + 2f, Size * 0.9f), rope);
            Outline(texture, new Color(0.12f, 0.12f, 0.14f));
            texture.Apply();
            return texture;
        }

        /// <summary>Writes only the wooden chest's icon (GL249), leaving the rest as they are.</summary>
        [MenuItem("Clube/Items/Generate chest icon")]
        public static void GenerateChestIcon()
        {
            Directory.CreateDirectory(Folder);
            Write("wooden chest", "Wooden chest", PaintChest());
            AssetDatabase.SaveAssets();
        }

        // A wooden crate seen from the front and a little above (GL282): light slats, a dark frame round each face and
        // a brace across the front and the end.
        private static Texture2D PaintChest()
        {
            Texture2D texture = Blank();
            var front = new Color(0.7f, 0.53f, 0.33f);
            var top = new Color(0.8f, 0.63f, 0.41f);
            var end = new Color(0.52f, 0.38f, 0.22f);
            var frame = new Color(0.42f, 0.27f, 0.15f);
            var frameEnd = new Color(0.3f, 0.19f, 0.1f);
            var dark = new Color(0.2f, 0.12f, 0.06f);
            float l = Size * 0.1f, r = Size * 0.7f, b = Size * 0.12f, t = Size * 0.66f, back = Size * 0.18f, lift = back * 0.8f;
            var endTop = new Vector2(r + back, t + lift);
            FillQuad(texture, new Vector2(l, b), new Vector2(r, b), new Vector2(r, t), new Vector2(l, t), front);
            FillQuad(texture, new Vector2(l, t), new Vector2(r, t), endTop, new Vector2(l + back, t + lift), top);
            FillQuad(texture, new Vector2(r, b), new Vector2(r + back, b + lift), endTop, new Vector2(r, t), end);
            // Slat seams across the front and the end.
            for (int i = 1; i < 3; i++)
            {
                float y = b + (t - b) * i / 3f;
                Line(texture, new Vector2(l, y), new Vector2(r, y), 1f, Color.Lerp(front, dark, 0.45f));
                Line(texture, new Vector2(r, y), new Vector2(r + back, y + lift), 1f, Color.Lerp(end, dark, 0.45f));
            }
            // The frame: a border round the front, the end and the top, and a brace corner to corner on the front and end.
            float w = 4f;
            Line(texture, new Vector2(l, b), new Vector2(r, b), w, frame);
            Line(texture, new Vector2(l, t), new Vector2(r, t), w, frame);
            Line(texture, new Vector2(l, b), new Vector2(l, t), w, frame);
            Line(texture, new Vector2(r, b), new Vector2(r, t), w, frame);
            Line(texture, new Vector2(l + w, b + w), new Vector2(r - w, t - w), w, frame);
            Line(texture, new Vector2(r + back, b + lift), endTop, w * 0.8f, frameEnd);
            Line(texture, new Vector2(r, b), new Vector2(r + back, b + lift), w * 0.8f, frameEnd);
            Line(texture, new Vector2(r + 2f, t - 3f), new Vector2(r + back - 2f, b + lift + 3f), w * 0.8f, frameEnd);
            Line(texture, new Vector2(l + back, t + lift), endTop, w * 0.8f, frame);
            Line(texture, new Vector2(l, t), new Vector2(l + back, t + lift), w * 0.8f, frame);
            Line(texture, new Vector2(r, t), endTop, w * 0.8f, frame);
            Outline(texture, dark);
            texture.Apply();
            return texture;
        }

        // Ore not yet told apart (GL54, GL75): the host rock's own texture, filled to the edges like
        // the other ore icons, with a large question mark over it.
        private static Texture2D PaintUnknownOre()
        {
            Texture2D texture = Blank();
            var stone = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Terrain/stone.png");
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color rock = stone != null && stone.isReadable ? stone.GetPixelBilinear((x + 0.5f) / Size, (y + 0.5f) / Size) : StoneHead;
                    rock.a = 1f;
                    texture.SetPixel(x, y, rock);
                }
            }
            // The mark twice: a pale halo so it reads on the speckled rock, then the ink over it.
            DrawQuestionMark(texture, 7.5f, new Color(0.92f, 0.9f, 0.84f));
            DrawQuestionMark(texture, 4.5f, new Color(0.12f, 0.12f, 0.15f));
            texture.Apply();
            return texture;
        }

        private static void DrawQuestionMark(Texture2D texture, float width, Color color)
        {
            var centre = new Vector2(32f, 41f);
            Vector2 previous = centre + new Vector2(-11f, 1f);
            for (int i = 1; i <= 14; i++)
            {
                float angle = Mathf.Lerp(Mathf.PI * 0.95f, -Mathf.PI * 0.4f, i / 14f);
                var at = centre + new Vector2(Mathf.Cos(angle) * 11f, Mathf.Sin(angle) * 10f);
                Line(texture, previous, at, width, color);
                previous = at;
            }
            Line(texture, previous, new Vector2(32f, 24f), width, color);
            Line(texture, new Vector2(32f, 13f), new Vector2(32f, 13f), width + 1.5f, color);
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
