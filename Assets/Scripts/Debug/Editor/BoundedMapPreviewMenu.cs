using System.Collections.Generic;
using System.IO;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// A quick look at the bounded map (GL589-GL592) without playing it: surveys the map the MapGen scene's terrain
    /// settings give with the BoundedMap generator, and writes it as a picture to <c>Temp/BoundedMapPreview.png</c>
    /// (biome colours shaded by height, sea, rivers and lakes, bare rock, and a dot per cell holding ore).
    /// </summary>
    public static class BoundedMapPreviewMenu
    {
        private const string ConfigPath = "Assets/Config/MapGenWorldConfig.asset";
        private const float CellSize = 40f;
        private const int Scale = 6;

        [MenuItem("Clube/Map/Write bounded map preview")]
        public static void Write()
        {
            Write(null, true, "Temp/BoundedMapPreview.png");
        }

        /// <summary>The same picture written outside Temp (which Unity empties on quit), for a batch-mode run.</summary>
        public static void WriteToLogs()
        {
            Write(null, true, "Logs/BoundedMapPreview.png");
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

                Texture2D picture = Draw(generator, survey);
                double planStart = EditorApplication.timeSinceStartup;
                WorldPlan plan = WorldPlanner.Build(survey, terrain.Seed);
                double planned = EditorApplication.timeSinceStartup;
                DrawPlan(picture, survey, plan);
                File.WriteAllBytes(path, picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
                MapLayout layout = generator.Layout;
                string summary = $"Bounded map, seed {terrain.Seed}: west {layout.Boundary(MapSide.West).Name}, east {layout.Boundary(MapSide.East).Name}, "
                    + $"south {layout.Boundary(MapSide.South).Name}, north {layout.Boundary(MapSide.North).Name}; "
                    + $"generator and water {(generated - start) * 1000.0:F0} ms, survey of {survey.Size} x {survey.Size} cells "
                    + $"{(surveyed - generated) * 1000.0:F0} ms ({survey.Timings}); world plan {(planned - planStart) * 1000.0:F0} ms ({plan.Timings}): "
                    + $"{plan.Settlements.Count} settlements, {plan.Roads.Count} roads, {plan.WaterRoutes.Count} water routes, "
                    + $"notes: {string.Join(" | ", plan.Notes)}; written to {path}";
                UnityEngine.Debug.Log(summary);
                return summary;
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        // The picture the world map shows (MapPicture, GL599), with a dot per survey cell holding ore.
        private static Texture2D Draw(BoundedMapGenerator map, ResourceSurvey survey)
        {
            int side = survey.Size * Scale;
            var picture = new Texture2D(side, side, TextureFormat.RGB24, false);
            picture.SetPixels32(MapPicture.Render(map, survey, side));
            for (int z = 0; z < survey.Size; z++)
            {
                for (int x = 0; x < survey.Size; x++)
                {
                    float ore = 0f;
                    for (int i = 0; i < survey.Ores.Count; i++)
                    {
                        ore += survey.Ore(i, x, z);
                    }
                    if (ore >= 3f && survey.Cell(x, z).Sea <= 0.5f)
                    {
                        picture.SetPixel(x * Scale + Scale / 2, z * Scale + Scale / 2, new Color(0.85f, 0.35f, 0.1f));
                    }
                }
            }
            picture.Apply();
            return picture;
        }

        // GL593-GL597 over the map: water routes (white sea, cyan river), roads by class (dark paved, brown dirt, thin tan
        // footpath), bridges (white dots) and settlements as squares sized and coloured by type.
        private static void DrawPlan(Texture2D picture, ResourceSurvey survey, WorldPlan plan)
        {
            Vector2 Pixel(Vector2 position)
            {
                return new Vector2((position.x - survey.Area.xMin) / survey.CellSize * Scale, (position.y - survey.Area.yMin) / survey.CellSize * Scale);
            }
            void Square(Vector2 centre, int half, Color colour)
            {
                for (int y = -half; y <= half; y++)
                {
                    for (int x = -half; x <= half; x++)
                    {
                        int px = Mathf.RoundToInt(centre.x) + x, py = Mathf.RoundToInt(centre.y) + y;
                        if (px >= 0 && py >= 0 && px < picture.width && py < picture.height)
                        {
                            picture.SetPixel(px, py, colour);
                        }
                    }
                }
            }
            void Line(List<Vector2> points, int half, Color colour, int dash)
            {
                int drawn = 0;
                for (int i = 1; i < points.Count; i++)
                {
                    Vector2 from = Pixel(points[i - 1]), to = Pixel(points[i]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to)));
                    for (int s = 0; s <= steps; s++, drawn++)
                    {
                        if (dash == 0 || drawn / dash % 2 == 0)
                        {
                            Square(Vector2.Lerp(from, to, s / (float)steps), half, colour);
                        }
                    }
                }
            }

            foreach (WaterRoute route in plan.WaterRoutes)
            {
                Line(route.Points, 0, MapPicture.RouteColour(route.Kind), 4);
            }
            for (RoadClass roadClass = RoadClass.Footpath; roadClass <= RoadClass.PavedCartPath; roadClass++)
            {
                foreach (PlannedRoad road in plan.Roads)
                {
                    if (road.Class != roadClass)
                    {
                        continue;
                    }
                    Color colour = MapPicture.RoadColour(roadClass);
                    Line(road.Points, roadClass == RoadClass.Footpath ? 0 : 1, colour, 0);
                    foreach (RoadBridge bridge in road.Bridges)
                    {
                        Square(Pixel(bridge.Position), 2, Color.white);
                    }
                }
            }
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                int half = 2 + Mathf.RoundToInt(Mathf.Sqrt(settlement.Type.Parcels));
                Color colour = MapPicture.SettlementColour(settlement.Type);
                Square(Pixel(settlement.Position), half + 1, Color.black);
                Square(Pixel(settlement.Position), half, colour);
            }
            picture.Apply();
        }
    }
}
