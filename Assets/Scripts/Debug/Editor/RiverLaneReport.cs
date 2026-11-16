using System.Collections.Generic;
using System.Text;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// How navigable a few seeds' rivers are, as numbers (GL644): for every planned river route, the lane a river boat
    /// takes on the real water, whether it is open end to end, and how much of it lies on water that carries the boat
    /// clear of both banks (<see cref="WaterLanes.Floats"/>, strict). For judging a change to the rivers' width and depth.
    /// </summary>
    public static class RiverLaneReport
    {
        private const string ConfigPath = "Assets/Config/MapGenWorldConfig.asset";

        [MenuItem("Clube/Map/Write river lane report (seeds 1 and 2)")]
        public static void WriteTwo()
        {
            UnityEngine.Debug.Log(Write(new[] { 1, 2 }));
        }

        /// <summary>Builds each seed's map and plan and measures its river lanes; a line per seed.</summary>
        public static string Write(int[] seeds)
        {
            WorldConfig source = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
            VesselClass boat = VesselClass.Find(VesselClass.Defaults(), VesselClass.RiverBoat);
            var report = new StringBuilder();
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
                    System.Func<float, float, bool> clear = WaterLanes.Floats(map.Water, boat, true);
                    int routes = 0, whole = 0, none = 0;
                    float length = 0f, planned = 0f, clearLength = 0f;
                    foreach (WaterRoute route in plan.WaterRoutes)
                    {
                        if (route.Kind != WaterRouteKind.River)
                        {
                            continue;
                        }
                        routes++;
                        planned += WaterLanes.Length(route.Points);
                        List<Vector2> lane = WaterLanes.For(route, boat, map.Water, out bool all);
                        if (lane == null)
                        {
                            none++;
                            continue;
                        }
                        whole += all ? 1 : 0;
                        float laneLength = WaterLanes.Length(lane);
                        length += laneLength;
                        for (float d = 0f; d < laneLength; d += 2f)
                        {
                            Vector2 point = WaterLanes.Along(lane, d);
                            clearLength += clear(point.x, point.y) ? 2f : 0f;
                        }
                    }
                    report.AppendLine($"Seed {seed}: {routes} river routes ({planned:0} m planned), {whole} open end to end, {none} with no water; "
                        + $"lanes {length:0} m, {clearLength:0} m of it on water that carries the boat clear of both banks");
                }
                finally
                {
                    Hydrology.DisposeAll();
                    Object.DestroyImmediate(config);
                }
            }
            return report.ToString();
        }
    }
}
