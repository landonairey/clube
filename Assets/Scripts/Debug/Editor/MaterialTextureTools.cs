using System.Collections.Generic;
using System.IO;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Editor tools for terrain material textures (M11):
    /// <list type="bullet">
    /// <item><i>Build texture array</i> packs every registry material's albedo into the
    /// registry's texture array, one layer per id. Run it after changing materials or textures.</item>
    /// <item><i>Generate placeholder textures</i> paints simple tileable noise textures for the
    /// starting materials (grass, dirt, stone and the three ores in stone), and cracked and
    /// loose stages of stone and copper (GL3), until real art exists.</item>
    /// </list>
    /// </summary>
    public static class MaterialTextureTools
    {
        private const string PlaceholderFolder = "Assets/Textures/Terrain";
        private const int PlaceholderSize = 128;

        /// <summary>How broken a placeholder looks: whole, cracked or loose rubble (GL3).</summary>
        private enum Wear
        {
            Whole,
            Cracked,
            Loose,
        }

        private static readonly Color StoneBase = new Color(0.45f, 0.45f, 0.47f);
        private static readonly Color StoneMix = new Color(0.60f, 0.60f, 0.62f);
        private static readonly Color CopperFleck = new Color(0.80f, 0.42f, 0.22f);

        // Natural colours for the placeholders: a base, a second colour mixed in by noise, for
        // ores the colour of the flecks scattered through stone, and how broken it looks (GL3).
        private static readonly Dictionary<string, (Color Base, Color Mix, Color? Fleck, Wear Wear)> Placeholders =
            new Dictionary<string, (Color, Color, Color?, Wear)>
            {
                { "grass", (new Color(0.30f, 0.50f, 0.18f), new Color(0.45f, 0.62f, 0.25f), null, Wear.Whole) },
                { "dirt", (new Color(0.40f, 0.28f, 0.18f), new Color(0.52f, 0.38f, 0.25f), null, Wear.Whole) },
                { "stone", (new Color(0.45f, 0.45f, 0.47f), new Color(0.60f, 0.60f, 0.62f), null, Wear.Whole) },
                { "gold", (new Color(0.45f, 0.45f, 0.47f), new Color(0.60f, 0.60f, 0.62f), new Color(0.95f, 0.78f, 0.25f), Wear.Whole) },
                { "silver", (new Color(0.45f, 0.45f, 0.47f), new Color(0.60f, 0.60f, 0.62f), new Color(0.88f, 0.90f, 0.95f), Wear.Whole) },
                { "copper", (new Color(0.45f, 0.45f, 0.47f), new Color(0.60f, 0.60f, 0.62f), new Color(0.80f, 0.42f, 0.22f), Wear.Whole) },
                { "cracked stone", (StoneBase, StoneMix, null, Wear.Cracked) },
                { "loose stone", (StoneBase, StoneMix, null, Wear.Loose) },
                { "cracked copper", (StoneBase, StoneMix, CopperFleck, Wear.Cracked) },
                { "loose copper", (StoneBase, StoneMix, CopperFleck, Wear.Loose) },
            };

        [MenuItem("Clube/Materials/Build texture array")]
        public static void BuildTextureArrays()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(MaterialRegistry)))
            {
                var registry = AssetDatabase.LoadAssetAtPath<MaterialRegistry>(AssetDatabase.GUIDToAssetPath(guid));
                BuildTextureArray(registry);
            }
        }

        /// <summary>Packs the registry's albedos into its texture array asset, next to the registry.</summary>
        public static void BuildTextureArray(MaterialRegistry registry)
        {
            int size = 0;
            int layers = 0;
            foreach (VoxelMaterial material in registry.Materials)
            {
                if (material == null)
                {
                    continue;
                }
                layers = Mathf.Max(layers, material.Id + 1);
                if (material.Albedo != null)
                {
                    size = Mathf.Max(size, material.Albedo.width);
                }
            }
            if (layers == 0)
            {
                UnityEngine.Debug.LogWarning($"{registry.name} has no materials; no texture array built.", registry);
                return;
            }
            size = size > 0 ? size : PlaceholderSize;

            var array = new Texture2DArray(size, size, layers, TextureFormat.RGBA32, true)
            {
                name = registry.name + " Textures",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            for (int layer = 0; layer < layers; layer++)
            {
                VoxelMaterial material = registry.Get((byte)layer);
                array.SetPixels(LayerPixels(material, size), layer, 0);
            }
            array.Apply(updateMipmaps: true, makeNoLongerReadable: false);

            string path = Path.ChangeExtension(AssetDatabase.GetAssetPath(registry), null) + " Textures.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (existing != null)
            {
                // Keeps the asset's GUID, so materials that point at it stay connected.
                EditorUtility.CopySerialized(array, existing);
                Object.DestroyImmediate(array);
                array = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(array, path);
            }

            Undo.RecordObject(registry, "Build texture array");
            registry.Textures = array;
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Built {path}: {layers} layers of {size}×{size}.", registry);
        }

        // A material's albedo scaled to the array size, or its debug colour where there's no texture.
        private static Color[] LayerPixels(VoxelMaterial material, int size)
        {
            var pixels = new Color[size * size];
            Texture2D albedo = material != null ? material.Albedo : null;
            if (albedo == null || !albedo.isReadable)
            {
                if (albedo != null)
                {
                    UnityEngine.Debug.LogWarning($"'{albedo.name}' isn't readable; enable Read/Write in its import settings.", albedo);
                }
                Color fill = material != null ? material.DebugColor : Color.magenta;
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = fill;
                }
                return pixels;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = albedo.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
                }
            }
            return pixels;
        }

        [MenuItem("Clube/Materials/Generate placeholder textures")]
        public static void GeneratePlaceholders()
        {
            Directory.CreateDirectory(PlaceholderFolder);
            int seed = 1;
            foreach (KeyValuePair<string, (Color Base, Color Mix, Color? Fleck, Wear Wear)> entry in Placeholders)
            {
                string path = $"{PlaceholderFolder}/{entry.Key}.png";
                File.WriteAllBytes(path, Paint(entry.Value.Base, entry.Value.Mix, entry.Value.Fleck, entry.Value.Wear, seed++).EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.isReadable = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            UnityEngine.Debug.Log($"Wrote {Placeholders.Count} placeholder textures to {PlaceholderFolder}.");
        }

        private static Texture2D Paint(Color baseColor, Color mix, Color? fleck, Wear wear, int seed)
        {
            var texture = new Texture2D(PlaceholderSize, PlaceholderSize, TextureFormat.RGBA32, false);
            var random = new System.Random(seed);
            var noise = new TileableNoise(random, PlaceholderSize);
            for (int y = 0; y < PlaceholderSize; y++)
            {
                for (int x = 0; x < PlaceholderSize; x++)
                {
                    float n = noise.Sample(x, y);
                    Color color = Color.Lerp(baseColor, mix, n);
                    // A little per-pixel grain so it doesn't look airbrushed.
                    color *= 0.92f + 0.16f * (float)random.NextDouble();
                    texture.SetPixel(x, y, color);
                }
            }

            if (fleck.HasValue)
            {
                // Small clusters of ore in the stone.
                int clusters = PlaceholderSize * PlaceholderSize / 300;
                for (int i = 0; i < clusters; i++)
                {
                    int cx = random.Next(PlaceholderSize);
                    int cy = random.Next(PlaceholderSize);
                    int radius = 1 + random.Next(3);
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (dx * dx + dy * dy <= radius * radius)
                            {
                                int px = (cx + dx + PlaceholderSize) % PlaceholderSize;
                                int py = (cy + dy + PlaceholderSize) % PlaceholderSize;
                                texture.SetPixel(px, py, fleck.Value * (0.85f + 0.3f * (float)random.NextDouble()));
                            }
                        }
                    }
                }
            }

            if (wear == Wear.Cracked)
            {
                PaintCracks(texture, random);
            }
            else if (wear == Wear.Loose)
            {
                PaintRubble(texture, random);
            }
            texture.Apply();
            return texture;
        }

        // Dark wandering lines, wrapping at the edges so the texture still tiles.
        private static void PaintCracks(Texture2D texture, System.Random random)
        {
            const int Cracks = 7;
            for (int i = 0; i < Cracks; i++)
            {
                float x = random.Next(PlaceholderSize);
                float y = random.Next(PlaceholderSize);
                float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
                int length = 40 + random.Next(50);
                for (int step = 0; step < length; step++)
                {
                    angle += (float)(random.NextDouble() - 0.5) * 0.8f;
                    x += Mathf.Cos(angle);
                    y += Mathf.Sin(angle);
                    Darken(texture, Mathf.RoundToInt(x), Mathf.RoundToInt(y), 0.3f);
                    Darken(texture, Mathf.RoundToInt(x) + 1, Mathf.RoundToInt(y), 0.75f);
                }
            }
        }

        // Pebbles: wrapped Voronoi cells, each its own shade, with dark gaps between them.
        private static void PaintRubble(Texture2D texture, System.Random random)
        {
            const int Pebbles = 36;
            const float Gap = 2.5f;
            var centres = new Vector2[Pebbles];
            var shades = new float[Pebbles];
            for (int i = 0; i < Pebbles; i++)
            {
                centres[i] = new Vector2(random.Next(PlaceholderSize), random.Next(PlaceholderSize));
                shades[i] = 0.75f + 0.4f * (float)random.NextDouble();
            }

            for (int y = 0; y < PlaceholderSize; y++)
            {
                for (int x = 0; x < PlaceholderSize; x++)
                {
                    float nearest = float.MaxValue;
                    float second = float.MaxValue;
                    int cell = 0;
                    for (int i = 0; i < Pebbles; i++)
                    {
                        float distance = WrappedDistance(new Vector2(x, y), centres[i]);
                        if (distance < nearest)
                        {
                            second = nearest;
                            nearest = distance;
                            cell = i;
                        }
                        else if (distance < second)
                        {
                            second = distance;
                        }
                    }
                    float edge = Mathf.Clamp01((second - nearest) / Gap);
                    Color color = texture.GetPixel(x, y) * shades[cell] * Mathf.Lerp(0.25f, 1f, edge);
                    color.a = 1f;
                    texture.SetPixel(x, y, color);
                }
            }
        }

        private static float WrappedDistance(Vector2 a, Vector2 b)
        {
            float dx = Mathf.Abs(a.x - b.x);
            float dy = Mathf.Abs(a.y - b.y);
            dx = Mathf.Min(dx, PlaceholderSize - dx);
            dy = Mathf.Min(dy, PlaceholderSize - dy);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static void Darken(Texture2D texture, int x, int y, float factor)
        {
            x = ((x % PlaceholderSize) + PlaceholderSize) % PlaceholderSize;
            y = ((y % PlaceholderSize) + PlaceholderSize) % PlaceholderSize;
            Color color = texture.GetPixel(x, y) * factor;
            color.a = 1f;
            texture.SetPixel(x, y, color);
        }

        /// <summary>Value noise on lattices that divide the texture size, so it wraps without seams.</summary>
        private sealed class TileableNoise
        {
            private readonly float[][] lattices;
            private readonly int[] periods = { 4, 8, 16, 32 };
            private readonly int size;

            public TileableNoise(System.Random random, int size)
            {
                this.size = size;
                lattices = new float[periods.Length][];
                for (int octave = 0; octave < periods.Length; octave++)
                {
                    int period = periods[octave];
                    lattices[octave] = new float[period * period];
                    for (int i = 0; i < lattices[octave].Length; i++)
                    {
                        lattices[octave][i] = (float)random.NextDouble();
                    }
                }
            }

            public float Sample(int x, int y)
            {
                float total = 0f;
                float weight = 1f;
                float weights = 0f;
                for (int octave = 0; octave < periods.Length; octave++)
                {
                    int period = periods[octave];
                    float gx = (float)x / size * period;
                    float gy = (float)y / size * period;
                    total += Lattice(lattices[octave], period, gx, gy) * weight;
                    weights += weight;
                    weight *= 0.5f;
                }
                return total / weights;
            }

            private static float Lattice(float[] values, int period, float gx, float gy)
            {
                int x0 = Mathf.FloorToInt(gx);
                int y0 = Mathf.FloorToInt(gy);
                float tx = Mathf.SmoothStep(0f, 1f, gx - x0);
                float ty = Mathf.SmoothStep(0f, 1f, gy - y0);
                float Value(int lx, int ly) => values[(ly % period) * period + (lx % period)];
                float bottom = Mathf.Lerp(Value(x0, y0), Value(x0 + 1, y0), tx);
                float top = Mathf.Lerp(Value(x0, y0 + 1), Value(x0 + 1, y0 + 1), tx);
                return Mathf.Lerp(bottom, top, ty);
            }
        }
    }
}
