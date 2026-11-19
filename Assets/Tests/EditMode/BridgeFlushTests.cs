using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL799 ("bridges are not flush with paths"): a road's bed meets each bridge's deck at both ends, with no step.</summary>
    public class BridgeFlushTests
    {
        /// <summary>The most a deck end and the road's bed may differ by, in metres.</summary>
        private const float Flush = 0.1f;

        private static WaterMap River(int from, int to, float surface)
        {
            int size = 140;
            int cells = size * size;
            var map = new WaterMap
            {
                Origin = new float2(-20f, -60f),
                CellSize = 1f,
                Size = new int2(size, size),
                Carve = new NativeArray<float>(cells, Allocator.Temp),
                Surface = new NativeArray<float>(cells, Allocator.Temp),
                Depth = new NativeArray<float>(cells, Allocator.Temp),
                Kind = new NativeArray<byte>(cells, Allocator.Temp),
                Flow = new NativeArray<float2>(cells, Allocator.Temp),
                LakeDistance = new NativeArray<byte>(cells, Allocator.Temp),
                RiverDistance = new NativeArray<byte>(cells, Allocator.Temp),
            };
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int world = x - 20;
                    if (world >= from && world < to)
                    {
                        map.Kind[x + z * size] = (byte)WaterKind.River;
                        map.Surface[x + z * size] = surface;
                        map.Depth[x + z * size] = 1f;
                    }
                }
            }
            return map;
        }

        private static void Dispose(WaterMap map)
        {
            map.Carve.Dispose(); map.Surface.Dispose(); map.Depth.Dispose(); map.Kind.Dispose();
            map.Flow.Dispose(); map.LakeDistance.Dispose(); map.RiverDistance.Dispose();
        }

        // Ground rising along the road, with low banks sloping into a river at x 40-46 whose water stands above them,
        // and a bank that rises just past the far end: both lift the deck's ends off the ground.
        private static float Ground(float x, float z)
        {
            float bank = Mathf.Clamp01(1f - Mathf.Abs(x - 43f) / 8f) * 1.2f;
            float rise = Mathf.Clamp01(1f - Mathf.Abs(x - 49.5f) / 1.5f) * 0.5f;
            return 5f + 0.02f * x - bank + rise;
        }

        [Test]
        public void EveryRoadClass_MeetsTheDeck_AtBothEnds_AndRampsToIt()
        {
            var line = new RoadLine(new[] { new Vector2(0f, 0f), new Vector2(110f, 0f) }, Ground);
            WaterMap map = River(40, 46, 5.4f);
            List<RouteBridges.Span> spans = RouteBridges.ForRoad(line, map, null);
            Dispose(map);
            Assert.AreEqual(1, spans.Count);
            RouteBridges.Span span = spans[0];
            Assert.Greater(span.FromHeight - line.HeightAlong(span.Start), 0.3f, "the test's deck stands off the ground");
            var rules = new WorldPlanRules();
            foreach (RoadClass roadClass in new[] { RoadClass.Footpath, RoadClass.DirtCartPath, RoadClass.PavedCartPath })
            {
                var bed = new RoadBed(line, rules.Road(roadClass), spans, 3);
                Assert.AreEqual(span.HeightAt(0f), bed.HeightAlong(span.Start), Flush, $"{roadClass}: the near end");
                Assert.AreEqual(span.HeightAt(1f), bed.HeightAlong(span.End), Flush, $"{roadClass}: the far end");
                for (float d = 0f; d < line.Length - 0.25f; d += 0.25f)
                {
                    float step = Mathf.Abs(bed.HeightAlong(d + 0.25f) - bed.HeightAlong(d));
                    Assert.Less(step, 0.25f * 0.35f, $"{roadClass}: walkable all the way, at {d:0.00} m");
                }
            }
        }

        [Test]
        public void Seed1_EveryBridgeWithinAKilometreOfTheStart_IsFlushAtBothEnds()
        {
            WorldConfig source = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldConfig>("Assets/Config/MapGenWorldConfig.asset");
            Assert.IsNotNull(source);
            WorldConfig config = Object.Instantiate(source);
            try
            {
                TerrainSettings terrain = config.Terrain;
                terrain.Generator = TerrainGeneratorType.BoundedMap;
                terrain.Seed = 1;
                var map = (BoundedMapGenerator)TerrainGenerators.Create(terrain);
                ResourceSurvey survey = ResourceSurvey.Build(terrain, map, OreField.Create(terrain, map), TreeField.Create(terrain, map), 40f);
                WorldPlan plan = WorldPlanner.Build(survey, terrain.Seed);
                WorldRoads.Finish(plan, map);
                Vector2 start = MapStart.Pick(plan, survey);
                var clearance = BridgeClearance.For(plan, VesselClass.Defaults());
                int checkedEnds = 0;
                var worst = new List<string>();
                for (int index = 0; index < plan.Roads.Count; index++)
                {
                    PlannedRoad road = plan.Roads[index];
                    if (road.Points.Count < 2 || !road.Points.Exists(p => Vector2.Distance(p, start) < 1000f))
                    {
                        continue;
                    }
                    RoadLine line = WorldRoads.LineOf(road, map);
                    List<RouteBridges.Span> spans = RouteBridges.ForRoad(line, map.Water, clearance);
                    var bed = new RoadBed(line, plan.Rules.Road(road.Class), spans, plan.Seed + index);
                    foreach (RouteBridges.Span span in spans)
                    {
                        foreach ((Vector2 end, float along, float deck) in new[] { (span.From, span.Start, span.HeightAt(0f)), (span.To, span.End, span.HeightAt(1f)) })
                        {
                            if (Vector2.Distance(end, start) > 1000f)
                            {
                                continue;
                            }
                            checkedEnds++;
                            float gap = Mathf.Abs(bed.HeightAlong(along) - deck);
                            if (gap >= Flush)
                            {
                                worst.Add($"road {index} at {end}: {gap:0.00} m");
                            }
                        }
                    }
                }
                Assert.Greater(checkedEnds, 0, "seed 1 has bridges near the start");
                Assert.IsEmpty(worst, string.Join("; ", worst));
            }
            finally
            {
                Hydrology.DisposeAll();
                Object.DestroyImmediate(config);
            }
        }
    }
}
