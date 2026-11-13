using System.Collections.Generic;
using System.IO;
using System.Text;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Lays out every settlement of a few seeds of the MapGen map on its ground and writes what came of it (GL613, GL642,
    /// GL643): buildings placed of those wanted, how many stand on terraces and how deep those are cut, climbs that double
    /// back, settlements on other lands than the capital's. For judging the layout rules without walking the map.
    /// </summary>
    public static class SettlementSeedReport
    {
        private const string ConfigPath = "Assets/Config/MapGenWorldConfig.asset";

        [MenuItem("Clube/Map/Write settlement report")]
        public static void WriteFour()
        {
            UnityEngine.Debug.Log(Write(new[] { 1, 2, 3, 4 }, "Temp/settlement-report.txt"));
        }

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
                    WorldRoads.Finish(plan, map);
                    summary.AppendLine($"Seed {seed} ({map.Layout.ShapeType.Name}): {Describe(report, map, plan)}");
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

        /// <summary>Every settlement of a plan laid out on a map's ground, as the game lays them out.</summary>
        public static List<SettlementSite> Sites(BoundedMapGenerator map, WorldPlan plan, SettlementSiteRules rules = null)
        {
            MapLayout layout = map.Layout;
            System.Func<float, float, RoadWater> water = WorldRoads.WaterOf(map.Water);
            rules ??= new SettlementSiteRules();
            var sites = new List<SettlementSite>();
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                float side = SettlementLayout.SideOf(settlement.Type, plan.Rules.ParcelSide);
                sites.Add(SettlementLayout.Plan(settlement, plan.Rules.ParcelSide, rules, map.Height, WorldRoads.HeightsOf(map),
                    (x, z) => !layout.WalksFreely(x, z) || water(x, z) != RoadWater.Dry, SettlementLayout.RoadsNear(plan, settlement, side + rules.Margin + 40f)));
            }
            return sites;
        }

        private static string Describe(StringBuilder report, BoundedMapGenerator map, WorldPlan plan)
        {
            float started = (float)EditorApplication.timeSinceStartup;
            List<SettlementSite> sites = Sites(map, plan);
            float seconds = (float)EditorApplication.timeSinceStartup - started;
            int wanted = 0, placed = 0, terraced = 0, switchbacks = 0, short_ = 0, islands = 0;
            float deepest = 0f;
            report.AppendLine($"== Seed {plan.Seed}");
            foreach (SettlementSite site in sites)
            {
                PlannedSettlement s = site.Settlement;
                int want = SettlementLayout.BuildingsWanted(s, site.Rules);
                float cut = 0f, fill = 0f;
                foreach (SettlementTerrace terrace in site.Terraces)
                {
                    cut = Mathf.Max(cut, terrace.Cut);
                    fill = Mathf.Max(fill, terrace.Fill);
                }
                int doubled = site.Lanes.FindAll(lane => lane.Switchback).Count;
                wanted += want;
                placed += site.Buildings.Count;
                terraced += site.Levels > 1 ? 1 : 0;
                switchbacks += doubled;
                short_ += site.Buildings.Count < want ? 1 : 0;
                islands += s.Land != plan.Capital.Land ? 1 : 0;
                deepest = Mathf.Max(deepest, cut);
                report.AppendLine($"{s.Name,-28} land {s.Land} network {s.RoadNetwork,2}  slope {site.Slope * 100f,3:0}%  buildings {site.Buildings.Count}/{want}  "
                    + $"levels {site.Levels}  cut {cut:0.0} m  fill {fill:0.0} m  lanes {site.Lanes.Count} ({doubled} doubled back)  at {s.Position}");
            }
            return $"{placed} of {wanted} buildings placed, {short_} settlements short; {terraced} on more than one level, deepest cut {deepest:0.0} m, "
                + $"{switchbacks} climbs doubled back; {islands} off the capital's land; laid out in {seconds:0.00} s";
        }
    }
}
