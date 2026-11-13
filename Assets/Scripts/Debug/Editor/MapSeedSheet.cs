using System;
using System.IO;
using Clube.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Pictures of the bounded map for a row of seeds (GL637: "judge it from pictures of several seeds"): each seed's
    /// land as shaded relief in its biomes' colours, out past the map's square so what lies beyond each edge shows,
    /// with the square, the playable edge and the line the player is stopped at drawn over it. No water is worked
    /// out, so a seed takes a second or two. Written to <c>Logs/MapSeeds/</c>, with a line per seed in the Console.
    /// </summary>
    /// <remarks>
    /// In batch mode: <c>-executeMethod Clube.Debug.Editor.MapSeedSheet.WriteFromArguments -mapSeeds 1,2,3</c>, with
    /// <c>-mapOut folder</c>, <c>-mapSides West,East,South,North kinds</c> and <c>-mapShape name</c> as wanted;
    /// <c>-mapFull</c> writes the world map's own picture instead (<see cref="BoundedMapPreviewMenu"/>), water and plan included.
    /// </remarks>
    public static class MapSeedSheet
    {
        private const string ConfigPath = "Assets/Config/MapGenWorldConfig.asset";
        private const int Resolution = 760;

        // The picture's side as a share of the map's: the rest shows the land past the edges.
        private const float Margin = 1.3f;

        [MenuItem("Clube/Map/Write seed sheet (seeds 1 to 8)")]
        public static void WriteEight()
        {
            Write(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, "Logs/MapSeeds", null, null);
        }

        /// <summary>The batch-mode entry: reads the seeds and folder from the command line.</summary>
        public static void WriteFromArguments()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string Value(string name)
            {
                int at = Array.IndexOf(arguments, name);
                return at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
            }
            string[] names = (Value("-mapSeeds") ?? "1,2,3,4").Split(',');
            int[] seeds = Array.ConvertAll(names, int.Parse);
            string folder = Value("-mapOut") ?? "Logs/MapSeeds";
            if (Array.IndexOf(arguments, "-mapFull") >= 0)
            {
                // The whole map as the world map draws it: water, survey, settlements and roads (slow: the water).
                Directory.CreateDirectory(folder);
                foreach (int seed in seeds)
                {
                    BoundedMapPreviewMenu.Write(seed, true, Path.Combine(folder, $"full_{seed}.png"));
                    Hydrology.DisposeAll();
                }
                return;
            }
            Write(seeds, folder, Value("-mapSides")?.Split(','), Value("-mapShape"));
        }

        /// <summary>Draws each seed's map and writes it as <c>seed_N.png</c> in a folder.</summary>
        /// <param name="sides">Four boundary type names to set by hand (west, east, south, north); null for the seed's.</param>
        /// <param name="shape">An outline's name to set by hand; null for the seed's.</param>
        public static void Write(int[] seeds, string folder, string[] sides, string shape)
        {
            Directory.CreateDirectory(folder);
            WorldConfig source = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
            foreach (int seed in seeds)
            {
                WorldConfig config = UnityEngine.Object.Instantiate(source);
                try
                {
                    TerrainSettings terrain = config.Terrain;
                    terrain.Generator = TerrainGeneratorType.BoundedMap;
                    terrain.Seed = seed;
                    if (sides != null && sides.Length == 4)
                    {
                        terrain.Map.SetSides(sides[0], sides[1], sides[2], sides[3]);
                    }
                    if (!string.IsNullOrEmpty(shape))
                    {
                        terrain.Map.SetShape(shape);
                    }
                    double start = EditorApplication.timeSinceStartup;
                    BoundedMapGenerator map = BoundedMapGenerator.CreateDry(terrain);
                    string path = Path.Combine(folder, $"seed_{seed}.png");
                    string facts = Draw(map, path);
                    MapLayout layout = map.Layout;
                    UnityEngine.Debug.Log(
                        $"Map seed {seed}: {layout.ShapeType.Name}; west {layout.Boundary(MapSide.West).Name}, east {layout.Boundary(MapSide.East).Name}, "
                        + $"south {layout.Boundary(MapSide.South).Name}, north {layout.Boundary(MapSide.North).Name}; summit {layout.Shape.PeakHeight:0} m at "
                        + $"{layout.PeakPosition}; {layout.LandCount} lands, {layout.RangeBumpCount} range bumps; {facts}; "
                        + $"{(EditorApplication.timeSinceStartup - start) * 1000.0:0} ms; {path}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(config);
                }
            }
        }

        private static string Draw(BoundedMapGenerator map, string path)
        {
            MapLayout layout = map.Layout;
            MapShape shape = layout.Shape;
            float side = layout.Size * Margin;
            float step = side / Resolution;
            int count = Resolution * Resolution;
            var points = new NativeArray<float2>(count, Allocator.TempJob);
            var heights = new NativeArray<float>(count, Allocator.TempJob);
            var pixels = new Color32[count];
            int playable = 0, land = 0;
            float highest = float.NegativeInfinity, lowest = float.PositiveInfinity, highestReached = float.NegativeInfinity;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    points[i] = new float2(-side * 0.5f + (i % Resolution + 0.5f) * step, -side * 0.5f + (i / Resolution + 0.5f) * step);
                }
                map.SchedulePointHeights(points, heights).Complete();
                BiomeField biomes = map.BiomeField;
                float sea = layout.SeaLevel, half = layout.Size * 0.5f;
                for (int z = 0; z < Resolution; z++)
                {
                    for (int x = 0; x < Resolution; x++)
                    {
                        int i = x + z * Resolution;
                        float2 p = points[i];
                        float h = heights[i];
                        float inside = shape.EdgeInside(p.x, p.y);
                        float limit = shape.LimitAt(p.x, p.y);
                        if (limit < 1f)
                        {
                            highestReached = Mathf.Max(highestReached, h);
                            lowest = Mathf.Min(lowest, h);
                        }
                        if (inside >= 0f)
                        {
                            playable++;
                            land += h >= sea ? 1 : 0;
                            highest = Mathf.Max(highest, h);
                        }
                        Color colour;
                        if (h < sea)
                        {
                            colour = MapPicture.Sea * Mathf.Lerp(0.55f, 1.05f, Mathf.InverseLerp(sea - 30f, sea, h));
                        }
                        else
                        {
                            colour = map.Biomes.Biomes[biomes.IndexAt(p.x, p.y, h)].MapColor;
                            float west = heights[(x > 0 ? x - 1 : x) + z * Resolution], east = heights[(x + 1 < Resolution ? x + 1 : x) + z * Resolution];
                            float south = heights[x + (z > 0 ? z - 1 : z) * Resolution], north = heights[x + (z + 1 < Resolution ? z + 1 : z) * Resolution];
                            float relief = Mathf.Clamp(((east - west) - (north - south)) / (2f * step) * 0.9f, -0.5f, 0.5f);
                            colour *= Mathf.Lerp(0.8f, 1.15f, Mathf.InverseLerp(sea, sea + 350f, h)) + relief;
                        }
                        // The map's square in white, the playable edge in red, the stop line in black.
                        float square = Mathf.Abs(Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half);
                        if (square < step * 0.6f)
                        {
                            colour = Color.Lerp(colour, Color.white, 0.6f);
                        }
                        if (Mathf.Abs(inside) < step * 0.9f)
                        {
                            colour = new Color(0.85f, 0.1f, 0.1f);
                        }
                        else if (limit >= 1f && Neighbour(shape, p, step) < 1f)
                        {
                            colour = Color.black;
                        }
                        colour.a = 1f;
                        pixels[i] = colour;
                    }
                }
            }
            finally
            {
                points.Dispose();
                heights.Dispose();
            }
            var picture = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
            picture.SetPixels32(pixels);
            picture.Apply();
            File.WriteAllBytes(path, picture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(picture);
            return $"playable {playable * step * step / 1e6f:0.0} km2, {100f * land / Mathf.Max(1, playable):0}% land; highest inside the edge "
                + $"{highest - layout.SeaLevel:0} m over the sea; reachable ground from y {lowest:0} to {highestReached:0}";
        }

        // The least limit among a point's four neighbours a pixel away: under 1 on the stop line's outer side.
        private static float Neighbour(in MapShape shape, float2 p, float step)
        {
            return Mathf.Min(
                Mathf.Min(shape.LimitAt(p.x - step, p.y), shape.LimitAt(p.x + step, p.y)),
                Mathf.Min(shape.LimitAt(p.x, p.y - step), shape.LimitAt(p.x, p.y + step)));
        }
    }
}
