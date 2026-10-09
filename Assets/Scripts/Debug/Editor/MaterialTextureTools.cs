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

        // Cracked and loose stages: fine enough to read per voxel at 4 voxels per metre.
        private const int DetailSize = 512;

        /// <summary>How broken a placeholder looks: whole, cracked or loose rubble (GL3).</summary>
        private enum Wear
        {
            Whole,
            Cracked,
            Loose,
            Bark,
            Leaves,
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
                { "rock", (new Color(0.38f, 0.37f, 0.36f), new Color(0.55f, 0.53f, 0.5f), null, Wear.Whole) },
                { "wood", (new Color(0.36f, 0.25f, 0.16f), new Color(0.48f, 0.35f, 0.23f), null, Wear.Bark) },
                { "leaves", (new Color(0.16f, 0.33f, 0.12f), new Color(0.30f, 0.50f, 0.18f), null, Wear.Leaves) },
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
            // Broken stages show per voxel, so they get the finer texture.
            int size = wear == Wear.Whole || wear == Wear.Bark ? PlaceholderSize : DetailSize;
            float scale = size / (float)PlaceholderSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var random = new System.Random(seed);
            var noise = new TileableNoise(random, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
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
                int clusters = size * size / 300;
                for (int i = 0; i < clusters; i++)
                {
                    int cx = random.Next(size);
                    int cy = random.Next(size);
                    int radius = Mathf.Max(1, Mathf.RoundToInt((1 + random.Next(3)) * Mathf.Sqrt(scale)));
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (dx * dx + dy * dy <= radius * radius)
                            {
                                int px = (cx + dx + size) % size;
                                int py = (cy + dy + size) % size;
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
            else if (wear == Wear.Bark)
            {
                PaintBark(texture, random);
            }
            else if (wear == Wear.Leaves)
            {
                PaintLeaves(texture, random, mix);
            }
            texture.Apply();
            return texture;
        }

        // Angular fractures to suit the low-poly terrain: from impact points about 40 cm
        // apart, rays of straight segments that kink and sometimes fork, each a dark groove
        // with a light bevel along one side. Sized so every 25 cm voxel shows some (a voxel
        // covers 64 pixels of a 512-pixel tile at 4 voxels per metre). Wraps so it tiles.
        private static void PaintCracks(Texture2D texture, System.Random random)
        {
            int size = texture.width;
            float scale = size / (float)PlaceholderSize;
            Color[] original = texture.GetPixels();
            // Impact points on a jittered grid, so cracks cover the tile evenly.
            int across = Mathf.Max(1, Mathf.RoundToInt(1.2f * scale));
            float cell = size / (float)across;
            for (int i = 0; i < across * across; i++)
            {
                var start = new Vector2(
                    (i % across + 0.15f + 0.7f * (float)random.NextDouble()) * cell,
                    (i / across + 0.15f + 0.7f * (float)random.NextDouble()) * cell);
                int rays = 3 + random.Next(2);
                float firstAngle = (float)(random.NextDouble() * Mathf.PI * 2.0);
                for (int ray = 0; ray < rays; ray++)
                {
                    float angle = firstAngle + ray * Mathf.PI * 2f / rays + (float)(random.NextDouble() - 0.5) * 0.6f;
                    PaintFracture(texture, original, random, start, angle, 3 + random.Next(2), 1.4f + scale * 0.5f, scale);
                }
            }
        }

        // One fracture: straight segments, kinking up to ±0.5 rad, thinning as it goes.
        private static void PaintFracture(
            Texture2D texture, Color[] original, System.Random random, Vector2 from, float angle, int segments, float width, float scale)
        {
            for (int segment = 0; segment < segments && width > 0.6f; segment++)
            {
                angle += (float)(random.NextDouble() - 0.5);
                float length = (9f + random.Next(9)) * scale * 0.5f;
                Vector2 to = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                PaintGroove(texture, original, from, to, width);
                if (random.NextDouble() < 0.12)
                {
                    float side = random.NextDouble() < 0.5 ? -1f : 1f;
                    PaintFracture(texture, original, random, to, angle + side * (0.6f + (float)random.NextDouble() * 0.5f), 2, width * 0.6f, scale);
                }
                from = to;
                width *= 0.85f;
            }
        }

        // A light bevel offset by a pixel, then the dark groove over it.
        private static void PaintGroove(Texture2D texture, Color[] original, Vector2 from, Vector2 to, float width)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 2f);
            for (int pass = 0; pass < 2; pass++)
            {
                Vector2 offset = pass == 0 ? new Vector2(1f, -1f) : Vector2.zero;
                float factor = pass == 0 ? 1.35f : 0.18f;
                float radius = pass == 0 ? width * 0.6f : width * 0.5f;
                for (int step = 0; step <= steps; step++)
                {
                    Vector2 point = Vector2.Lerp(from, to, (float)step / steps) + offset;
                    int r = Mathf.CeilToInt(radius);
                    for (int dy = -r; dy <= r; dy++)
                    {
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (dx * dx + dy * dy <= radius * radius + 0.25f)
                            {
                                SetShade(texture, original, Mathf.RoundToInt(point.x) + dx, Mathf.RoundToInt(point.y) + dy, factor);
                            }
                        }
                    }
                }
            }
        }

        // Shades from the original pixel, so overlapping stamps don't stack into black or white.
        private static void SetShade(Texture2D texture, Color[] original, int x, int y, float factor)
        {
            int size = texture.width;
            x = ((x % size) + size) % size;
            y = ((y % size) + size) % size;
            Color color = original[y * size + x] * factor;
            color.a = 1f;
            texture.SetPixel(x, y, color);
        }

        // Pebbles about 12 cm across: wrapped Voronoi cells, each its own shade, with dark
        // gaps between them, so a 25 cm voxel shows a few stones.
        private static void PaintRubble(Texture2D texture, System.Random random)
        {
            int size = texture.width;
            float scale = size / (float)PlaceholderSize;
            int pebbles = Mathf.RoundToInt(14f * scale * scale);
            float gap = 1.2f + scale;
            // Centres kept apart: two nearly on top of each other smear a dark band between them.
            float spacing = size / Mathf.Sqrt(pebbles);
            var placed = new List<Vector2>();
            for (int attempt = 0; attempt < pebbles * 30 && placed.Count < pebbles; attempt++)
            {
                var candidate = new Vector2(random.Next(size), random.Next(size));
                bool clear = true;
                foreach (Vector2 other in placed)
                {
                    if (WrappedDistance(candidate, other, size) < spacing * 0.6f)
                    {
                        clear = false;
                        break;
                    }
                }
                if (clear)
                {
                    placed.Add(candidate);
                }
            }
            pebbles = placed.Count;
            Vector2[] centres = placed.ToArray();
            var shades = new float[pebbles];
            for (int i = 0; i < pebbles; i++)
            {
                shades[i] = 0.75f + 0.4f * (float)random.NextDouble();
            }

            // Pebbles by coarse grid cell, so each pixel only checks the ones nearby.
            int cells = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(pebbles) / 2f));
            float cellSize = size / (float)cells;
            var grid = new List<int>[cells, cells];
            for (int i = 0; i < pebbles; i++)
            {
                int gx = Mathf.Min(cells - 1, (int)(centres[i].x / cellSize));
                int gy = Mathf.Min(cells - 1, (int)(centres[i].y / cellSize));
                (grid[gx, gy] ??= new List<int>()).Add(i);
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nearest = float.MaxValue;
                    float second = float.MaxValue;
                    int cell = 0;
                    int cx = (int)(x / cellSize);
                    int cy = (int)(y / cellSize);
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            List<int> nearby = grid[((cx + ox) % cells + cells) % cells, ((cy + oy) % cells + cells) % cells];
                            if (nearby == null)
                            {
                                continue;
                            }
                            foreach (int i in nearby)
                            {
                                float distance = WrappedDistance(new Vector2(x, y), centres[i], size);
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
                        }
                    }
                    float edge = Mathf.Clamp01((second - nearest) / gap);
                    Color color = texture.GetPixel(x, y) * shades[cell] * Mathf.Lerp(0.25f, 1f, edge);
                    color.a = 1f;
                    texture.SetPixel(x, y, color);
                }
            }
        }

        // Bark: dark grooves running up the tile, wandering a little side to side and breaking
        // off now and then, between lighter ridges. Wraps so it tiles.
        private static void PaintBark(Texture2D texture, System.Random random)
        {
            int size = texture.width;
            Color[] original = texture.GetPixels();
            int grooves = 9 + random.Next(4);
            for (int g = 0; g < grooves; g++)
            {
                float x = (g + (float)random.NextDouble() * 0.6f) * size / grooves;
                float width = 1f + (float)random.NextDouble() * 1.5f;
                float drift = 0f;
                for (int y = 0; y < size; y++)
                {
                    drift = Mathf.Clamp(drift + ((float)random.NextDouble() - 0.5f) * 0.6f, -1f, 1f);
                    x += drift * 0.35f;
                    if (random.NextDouble() < 0.015)
                    {
                        // A break in the groove: skip a few pixels.
                        y += 2 + random.Next(4);
                        continue;
                    }
                    int r = Mathf.CeilToInt(width);
                    for (int dx = -r - 1; dx <= r + 1; dx++)
                    {
                        float distance = Mathf.Abs(dx);
                        float factor = distance <= width ? 0.45f : distance <= width + 1.2f ? 1.2f : 1f;
                        SetShade(texture, original, Mathf.RoundToInt(x) + dx, y, factor);
                    }
                }
            }
        }

        // Leaves: overlapping small leaf ovals at every angle, each its own green, darker where
        // they lie deeper, over a dark green base.
        private static void PaintLeaves(Texture2D texture, System.Random random, Color light)
        {
            int size = texture.width;
            float scale = size / (float)PlaceholderSize;
            int leaves = Mathf.RoundToInt(260f * scale * scale);
            for (int i = 0; i < leaves; i++)
            {
                var centre = new Vector2(random.Next(size), random.Next(size));
                float length = (4f + (float)random.NextDouble() * 3f) * scale;
                float width = length * (0.4f + (float)random.NextDouble() * 0.15f);
                float angle = (float)(random.NextDouble() * Mathf.PI);
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var across = new Vector2(-along.y, along.x);
                // Later leaves lie on top, so they're lighter.
                float depth = (float)i / leaves;
                Color colour = Color.Lerp(texture.GetPixel((int)centre.x, (int)centre.y), light, 0.3f + 0.6f * depth)
                               * (0.85f + 0.25f * (float)random.NextDouble());
                colour.a = 1f;
                int r = Mathf.CeilToInt(length);
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var offset = new Vector2(dx, dy);
                        float u = Vector2.Dot(offset, along) / length;
                        float v = Vector2.Dot(offset, across) / width;
                        if (u * u + v * v > 1f)
                        {
                            continue;
                        }
                        // A pale midrib down the middle of the leaf.
                        Color pixel = Mathf.Abs(v) < 0.12f ? colour * 1.15f : colour;
                        pixel.a = 1f;
                        texture.SetPixel(((int)centre.x + dx + size) % size, ((int)centre.y + dy + size) % size, pixel);
                    }
                }
            }
        }

        private static float WrappedDistance(Vector2 a, Vector2 b, int size)
        {
            float dx = Mathf.Abs(a.x - b.x);
            float dy = Mathf.Abs(a.y - b.y);
            dx = Mathf.Min(dx, size - dx);
            dy = Mathf.Min(dy, size - dy);
            return Mathf.Sqrt(dx * dx + dy * dy);
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
