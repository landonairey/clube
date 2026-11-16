using System.IO;
using System.Text;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// The bounded map's roads for a row of seeds, as numbers (GL642): a line per road with its class, length, steepest
    /// slope against its class's limit, the metres over that limit, its bridges and the longest, and whether any of it
    /// lies in the sea, past the playable edge or inside an edge's limit. For judging a change to the terrain or to the
    /// routes without walking them. Written to <c>Logs/RoadSeeds.txt</c>; a seed takes about fifteen seconds (the water).
    /// </summary>
    public static class RoadSeedReport
    {
        private const string ConfigPath = "Assets/Config/MapGenWorldConfig.asset";

        [MenuItem("Clube/Map/Write road report (seeds 1 to 4)")]
        public static void WriteFour()
        {
            UnityEngine.Debug.Log(Write(new[] { 1, 2, 3, 4 }, "Logs/RoadSeeds.txt"));
        }

        /// <summary>Builds each seed's map, plan and roads and writes the report; returns its summary lines.</summary>
        public static string Write(int[] seeds, string path)
        {
            WorldConfig source = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
            var report = new StringBuilder();
            var summary = new StringBuilder();
            foreach (int seed in seeds)
            {
                WorldConfig config = Object.Instantiate(source);
                try
                {
                    TerrainSettings terrain = config.Terrain;
                    terrain.Generator = TerrainGeneratorType.BoundedMap;
                    terrain.Map.PickSidesFromSeed = true;
                    terrain.Seed = seed;
                    var map = (BoundedMapGenerator)TerrainGenerators.Create(terrain);
                    ResourceSurvey survey = ResourceSurvey.Build(terrain, map, OreField.Create(terrain, map), TreeField.Create(terrain, map), 40f);
                    WorldPlan plan = WorldPlanner.Build(survey, terrain.Seed);
                    float seconds = WorldRoads.Finish(plan, map);
                    string line = Describe(report, map, plan);
                    MapLayout layout = map.Layout;
                    summary.AppendLine(
                        $"Seed {seed} ({layout.ShapeType.Name}; {layout.Boundary(MapSide.West).Name}, {layout.Boundary(MapSide.East).Name}, "
                        + $"{layout.Boundary(MapSide.South).Name}, {layout.Boundary(MapSide.North).Name}): {line}; roads on the ground {seconds:0.0} s");
                }
                finally
                {
                    Hydrology.DisposeAll();
                    Object.DestroyImmediate(config);
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, summary + "\n" + report);
            return summary.ToString();
        }

        private static string Describe(StringBuilder report, BoundedMapGenerator map, WorldPlan plan)
        {
            MapLayout layout = map.Layout;
            System.Func<float, float, RoadWater> waterAt = WorldRoads.WaterOf(map.Water);
            report.AppendLine($"SEED {plan.Seed}: {plan.Settlements.Count} settlements, {plan.Roads.Count} roads, {plan.Signposts.Count} signposts");
            float length = 0f, steep = 0f, longestBridge = 0f;
            int bridges = 0, kept = 0, inSea = 0, closed = 0;
            foreach (string note in plan.Notes)
            {
                if (!note.StartsWith("Road"))
                {
                    continue;
                }
                report.AppendLine("  " + note);
                if (note.Contains("keeps its planned line"))
                {
                    kept++;
                }
            }
            foreach (PlannedRoad road in plan.Roads)
            {
                RoadClassDefinition definition = plan.Rules.Road(road.Class);
                int sea = 0, shut = 0;
                float top = float.NegativeInfinity, bridge = 0f;
                foreach (Vector2 p in road.Points)
                {
                    sea += waterAt(p.x, p.y) == RoadWater.Sea ? 1 : 0;
                    shut += layout.WalksFreely(p.x, p.y) ? 0 : 1;
                    top = Mathf.Max(top, map.Height(p.x, p.y));
                }
                foreach (RoadBridge b in road.Bridges)
                {
                    bridge = Mathf.Max(bridge, b.Length);
                }
                report.AppendLine(
                    $"  r{road.Index} {road.Class} {road.Length:0} m, steepest {road.SteepestSlope:0.00} of {definition.MaxSlope:0.00}, {road.SteepLength:0} m over; "
                    + $"{road.Bridges.Count} bridges (longest {bridge:0} m); top {top:0} m; in the sea {sea}, on closed ground {shut}; from {road.Points[0]}");
                length += road.Length;
                steep += road.SteepLength;
                bridges += road.Bridges.Count;
                longestBridge = Mathf.Max(longestBridge, bridge);
                inSea += sea;
                closed += shut;
            }
            return $"{plan.Roads.Count} roads, {length / 1000f:0.0} km, {steep:0} m over their slope limit, {bridges} bridges (longest {longestBridge:0} m), "
                + $"{kept} kept on their planned line, {inSea} points in the sea, {closed} on closed ground";
        }
    }
}
