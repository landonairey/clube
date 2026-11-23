using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// GL805 ("two bridges cross eachother at seed 819369 ... this hitboxes made walking through impassable"): no two roads'
    /// bridges cross. A joining road ends where it first meets the road it joins (<see cref="RoadRefiner.EndAtFirstMeeting"/>),
    /// and a road's bridge never crosses one laid before it (<see cref="BridgeDecks"/>).
    /// </summary>
    public class CrossingBridgeTests
    {
        [Test]
        public void AJoiningRoad_EndsWhereItFirstMeetsTheRoadItJoins()
        {
            var joined = new List<Vector2> { new Vector2(0f, -50f), new Vector2(0f, 50f) };
            // Comes in from the west, touches the joined road at x = 0, then runs on beside it and crosses it.
            var road = new List<Vector2>
            {
                new Vector2(-30f, 0f), new Vector2(-20f, 0f), new Vector2(-10f, 0f), new Vector2(-0.5f, 2f),
                new Vector2(3f, 10f), new Vector2(-3f, 20f), new Vector2(0f, 30f),
            };
            List<Vector2> cut = RoadRefiner.EndAtFirstMeeting(road, joined, RoadRefiner.MeetReach);
            Assert.AreEqual(4, cut.Count);
            Assert.AreEqual(new Vector2(0f, 2f), cut[3]);
        }

        [Test]
        public void AJoiningRoad_ThatStartsBesideTheRoad_IsNotCutAtItsStart()
        {
            var joined = new List<Vector2> { new Vector2(0f, -50f), new Vector2(0f, 50f) };
            var road = new List<Vector2> { new Vector2(-1f, 0f), new Vector2(-10f, 0f), new Vector2(-10f, 20f), new Vector2(0f, 20f) };
            List<Vector2> cut = RoadRefiner.EndAtFirstMeeting(road, joined, RoadRefiner.MeetReach);
            Assert.AreEqual(road.Count, cut.Count);
        }

        /// <summary>GL806: a joining road that first meets the other one on its bridge goes on to a dry meeting.</summary>
        [Test]
        public void AJoiningRoad_DoesNotEndOnTheOtherRoadsBridge()
        {
            var joined = new List<Vector2> { new Vector2(0f, -50f), new Vector2(0f, 50f) };
            var road = new List<Vector2>
            {
                new Vector2(-30f, 0f), new Vector2(-10f, 0f), new Vector2(-0.5f, 0f), new Vector2(-6f, 10f),
                new Vector2(-0.5f, 20f), new Vector2(-6f, 30f), new Vector2(0f, 40f),
            };
            // A river across z = 0: the joined road bridges it there.
            List<Vector2> cut = RoadRefiner.EndAtFirstMeeting(road, joined, RoadRefiner.MeetReach, p => Mathf.Abs(p.y) > 4f);
            Assert.AreEqual(5, cut.Count);
            Assert.AreEqual(new Vector2(0f, 20f), cut[4]);
        }

        [Test]
        public void ADeck_IsCrossedOnlyByAStretchThatCrossesIt()
        {
            var decks = new BridgeDecks();
            bool Wet(float x, float z) => Mathf.Abs(x) < 3f;
            decks.Note(new List<Vector2> { new Vector2(-10f, 0f), new Vector2(-2f, 0f), new Vector2(2f, 0f), new Vector2(10f, 0f) },
                (x, z) => Wet(x, z) ? RoadWater.River : RoadWater.Dry);
            Assert.Greater(decks.Count, 0);
            Assert.IsTrue(decks.Crosses(new Vector2(0f, -5f), new Vector2(0f, 5f)));
            Assert.IsFalse(decks.Crosses(new Vector2(0f, 5f), new Vector2(0f, 15f)));
            Assert.IsFalse(decks.Crosses(new Vector2(40f, -5f), new Vector2(40f, 5f)));
        }

        [Test]
        public void Seed819369_NoTwoRoadsBridgesCross()
        {
            WorldConfig source = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldConfig>("Assets/Config/MapGenWorldConfig.asset");
            Assert.IsNotNull(source);
            WorldConfig config = Object.Instantiate(source);
            try
            {
                TerrainSettings terrain = config.Terrain;
                terrain.Generator = TerrainGeneratorType.BoundedMap;
                terrain.Seed = 819369;
                var map = (BoundedMapGenerator)TerrainGenerators.Create(terrain);
                ResourceSurvey survey = ResourceSurvey.Build(terrain, map, OreField.Create(terrain, map), TreeField.Create(terrain, map), 40f);
                var rules = new WorldPlanRules();
                rules.SetWalkableSlope(50f);
                WorldPlan plan = WorldPlanner.Build(survey, terrain.Seed, rules);
                WorldRoads.Finish(plan, map);
                var clearance = BridgeClearance.For(plan, VesselClass.Defaults());
                var spans = new List<(int Road, RouteBridges.Span Span)>();
                for (int i = 0; i < plan.Roads.Count; i++)
                {
                    if (plan.Roads[i].Points.Count < 2)
                    {
                        continue;
                    }
                    foreach (RouteBridges.Span span in RouteBridges.ForRoad(WorldRoads.LineOf(plan.Roads[i], map), map.Water, clearance))
                    {
                        spans.Add((i, span));
                    }
                }
                Assert.Greater(spans.Count, 0);
                var crossing = new List<string>();
                for (int a = 0; a < spans.Count; a++)
                {
                    for (int b = a + 1; b < spans.Count; b++)
                    {
                        RouteBridges.Span one = spans[a].Span, other = spans[b].Span;
                        // Two decks leaving one point (a fork at a settlement) share an end; they do not cross.
                        bool sharedEnd = Vector2.Distance(one.From, other.From) < 0.5f || Vector2.Distance(one.From, other.To) < 0.5f
                            || Vector2.Distance(one.To, other.From) < 0.5f || Vector2.Distance(one.To, other.To) < 0.5f;
                        if (spans[a].Road != spans[b].Road && !sharedEnd && BridgeDecks.SegmentsCross(one.From, one.To, other.From, other.To))
                        {
                            crossing.Add($"roads {spans[a].Road} and {spans[b].Road} near {one.From}");
                        }
                    }
                }
                Assert.IsEmpty(crossing, string.Join("; ", crossing));

                // GL806 ("a weird ramp in the footpath that juts upwards then suddenly stops", where a footpath joined
                // another on its bridge): every road that joins another ends on dry ground, with no bridge of its own
                // running off its end.
                var wetEnds = new List<string>();
                System.Func<float, float, RoadWater> waterAt = WorldRoads.WaterOf(map.Water);
                for (int i = 0; i < plan.Roads.Count; i++)
                {
                    PlannedRoad road = plan.Roads[i];
                    if (road.To >= 0 || road.Joins < 0 || road.Points.Count < 2)
                    {
                        continue;
                    }
                    Vector2 end = road.Points[road.Points.Count - 1];
                    if (waterAt(end.x, end.y) != RoadWater.Dry)
                    {
                        wetEnds.Add($"road {i} ends in water at {end}");
                    }
                    RoadLine line = WorldRoads.LineOf(road, map);
                    foreach ((int index, RouteBridges.Span span) in spans)
                    {
                        if (index == i && span.End >= line.Length - 0.01f)
                        {
                            wetEnds.Add($"road {i} ends on its own bridge at {end}");
                        }
                    }
                }
                Assert.IsEmpty(wetEnds, string.Join("; ", wetEnds));
            }
            finally
            {
                Hydrology.DisposeAll();
                Object.DestroyImmediate(config);
            }
        }
    }
}
