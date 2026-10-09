using System.IO;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Paints stand-in icons (GL7, GL31), until real art exists: the copper bun (a rough dome,
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

        [MenuItem("Clube/Items/Generate icons")]
        public static void GenerateIcons()
        {
            Directory.CreateDirectory(Folder);
            Write("copper bun", "Copper bun", PaintBun());
            Write("copper ingot", "Copper ingot", PaintIngot());
            Write("wood", "Wood", PaintLog());
            Write("leaves", "Leaves", PaintLeaf());
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
        private static Texture2D PaintBun()
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
                    Color color = Color.Lerp(CopperDark, Copper, shade);
                    if (shade > 0.85f)
                    {
                        color = Color.Lerp(color, CopperLight, (shade - 0.85f) / 0.15f);
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
        private static Texture2D PaintIngot()
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
                            color = Color.Lerp(Copper, CopperLight, 0.35f + 0.4f * t);
                        }
                    }
                    else if (y >= frontY0 && y < topY0)
                    {
                        // Front face, tapering in towards the bottom.
                        float t = (topY0 - y) / (topY0 - frontY0);
                        float half = Mathf.Lerp(Size * 0.36f, Size * 0.4f, t);
                        if (Mathf.Abs(x - Size * 0.5f) <= half)
                        {
                            color = Color.Lerp(Copper, CopperDark, 0.25f + 0.35f * t);
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
            Outline(texture, CopperDark);
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
