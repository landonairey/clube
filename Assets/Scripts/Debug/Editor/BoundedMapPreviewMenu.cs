using System.IO;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// A quick look at the bounded map (GL589-GL592) before it has a scene: surveys the map the streamed lab's terrain
    /// settings give with the BoundedMap generator, and writes it as a picture to <c>Temp/BoundedMapPreview.png</c>
    /// (biome colours shaded by height, sea, rivers and lakes, bare rock, and a dot per cell holding ore).
    /// </summary>
    public static class BoundedMapPreviewMenu
    {
        private const string ConfigPath = "Assets/Config/ProceduralWorldLabWorldConfig.asset";
        private const float CellSize = 40f;
        private const int Scale = 6;

        [MenuItem("Clube/Map/Write bounded map preview")]
        public static void Write()
        {
            Write(null, true, "Temp/BoundedMapPreview.png");
        }

        /// <summary>Surveys and draws the map for a seed (null: the config's own) with its sides from the seed or as set.</summary>
        public static string Write(int? seed, bool sidesFromSeed, string path)
        {
            WorldConfig source = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
            WorldConfig config = Object.Instantiate(source);
            try
            {
                TerrainSettings terrain = config.Terrain;
                terrain.Generator = TerrainGeneratorType.BoundedMap;
                terrain.Map.PickSidesFromSeed = sidesFromSeed;
                if (seed.HasValue)
                {
                    terrain.Seed = seed.Value;
                }
                double start = EditorApplication.timeSinceStartup;
                var generator = (BoundedMapGenerator)TerrainGenerators.Create(terrain);
                double generated = EditorApplication.timeSinceStartup;
                ResourceSurvey survey = ResourceSurvey.Build(
                    terrain, generator, OreField.Create(terrain, generator), TreeField.Create(terrain, generator), CellSize);
                double surveyed = EditorApplication.timeSinceStartup;

                Texture2D picture = Draw(survey, generator.Layout.SeaLevel);
                File.WriteAllBytes(path, picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
                MapLayout layout = generator.Layout;
                string summary = $"Bounded map, seed {terrain.Seed}: west {layout.Boundary(MapSide.West).Name}, east {layout.Boundary(MapSide.East).Name}, "
                    + $"south {layout.Boundary(MapSide.South).Name}, north {layout.Boundary(MapSide.North).Name}; "
                    + $"generator and water {(generated - start) * 1000.0:F0} ms, survey of {survey.Size} x {survey.Size} cells "
                    + $"{(surveyed - generated) * 1000.0:F0} ms ({survey.Timings}); written to {path}";
                UnityEngine.Debug.Log(summary);
                return summary;
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static Texture2D Draw(ResourceSurvey survey, float seaLevel)
        {
            int size = survey.Size;
            var picture = new Texture2D(size * Scale, size * Scale, TextureFormat.RGB24, false);
            var sea = new Color(0.13f, 0.3f, 0.55f);
            var fresh = new Color(0.3f, 0.62f, 0.9f);
            var rock = new Color(0.55f, 0.55f, 0.57f);
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    SurveyCell cell = survey.Cell(x, z);
                    Color colour = survey.Biomes[cell.Biome].MapColor;
                    colour = Color.Lerp(colour, rock, cell.Stone);
                    // Lighter with height, darker on slopes.
                    float shade = Mathf.Lerp(0.75f, 1.25f, Mathf.InverseLerp(seaLevel, seaLevel + 350f, cell.Height)) * Mathf.Lerp(0.8f, 1f, cell.Flatness);
                    colour *= shade;
                    if (cell.Sea > 0.5f)
                    {
                        colour = sea * Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(seaLevel - 30f, seaLevel, cell.Height));
                    }
                    else if (cell.FreshWater > 0.04f)
                    {
                        colour = Color.Lerp(colour, fresh, Mathf.Clamp01(cell.FreshWater * 4f));
                    }
                    else if (cell.Wood > 0f)
                    {
                        colour = Color.Lerp(colour, new Color(0.07f, 0.25f, 0.1f), Mathf.Clamp01(cell.Wood / 6f) * 0.6f);
                    }
                    float ore = 0f;
                    for (int i = 0; i < survey.Ores.Count; i++)
                    {
                        ore += survey.Ore(i, x, z);
                    }
                    for (int py = 0; py < Scale; py++)
                    {
                        for (int px = 0; px < Scale; px++)
                        {
                            bool dot = ore >= 3f && px == Scale / 2 && py == Scale / 2;
                            picture.SetPixel(x * Scale + px, z * Scale + py, dot ? new Color(0.85f, 0.35f, 0.1f) : colour);
                        }
                    }
                }
            }
            picture.Apply();
            return picture;
        }
    }
}
