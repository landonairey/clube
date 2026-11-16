using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL570: a bridge span is found where a road crosses river water, and only there.</summary>
    public class RouteBridgesTests
    {
        private static WaterMap Map(int riverFrom, int riverTo, float surface)
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
                    if (world >= riverFrom && world < riverTo)
                    {
                        map.Kind[x + z * size] = (byte)WaterKind.River;
                        map.Surface[x + z * size] = surface;
                        map.Depth[x + z * size] = 1f;
                    }
                }
            }
            return map;
        }

        private static float Flat(float x, float z) => 5f;

        [Test]
        public void RoadAcrossARiver_GetsOneBridgeReachingBothBanks()
        {
            var route = new CartRoute(new Vector2(0f, 0f), new Vector2(100f, 0f), Flat, 1);
            WaterMap map = Map(40, 44, 5.5f);
            var spans = RouteBridges.Find(route, map);
            map.Carve.Dispose(); map.Surface.Dispose(); map.Depth.Dispose(); map.Kind.Dispose();
            map.Flow.Dispose(); map.LakeDistance.Dispose(); map.RiverDistance.Dispose();
            Assert.AreEqual(1, spans.Count);
            Assert.Less(spans[0].From.x, 40f);
            Assert.Greater(spans[0].To.x, 44f);
            Assert.GreaterOrEqual(spans[0].FromHeight, 5.5f + RouteBridges.Clearance - 1e-3f);
            Assert.GreaterOrEqual(spans[0].ToHeight, 5.5f + RouteBridges.Clearance - 1e-3f);
        }

        [Test]
        public void RoadAcrossTheSea_GetsAHumpedDeck_HighEnoughForWhatSailsUnder_ClimbingFromBackOnTheLand()
        {
            var route = new RoadLine(new[] { new Vector2(0f, 0f), new Vector2(118f, 0f) }, Flat);
            WaterMap map = Map(50, 70, 4f);
            for (int i = 0; i < map.Kind.Length; i++)
            {
                map.Kind[i] = map.Kind[i] != 0 ? (byte)WaterKind.Lake : (byte)0;
            }
            map.HasSea = true;
            map.SeaLevel = 4f;
            var low = RouteBridges.Find(route, map, true);
            var high = RouteBridges.Find(route, map, true, middle => 6f);
            map.Carve.Dispose(); map.Surface.Dispose(); map.Depth.Dispose(); map.Kind.Dispose();
            map.Flow.Dispose(); map.LakeDistance.Dispose(); map.RiverDistance.Dispose();
            Assert.AreEqual(1, low.Count);
            Assert.AreEqual(0f, low[0].Rise, "no clearance asked: a low deck, as over a river");
            Assert.AreEqual(1, high.Count);
            RouteBridges.Span span = high[0];
            Assert.GreaterOrEqual(span.HeightAt(0.5f), 4f + 6f - 0.01f, "the middle clears the vessel");
            Assert.Less(span.Start, low[0].Start - 10f, "it starts back on the land, for the climb");
            Assert.Greater(span.End, low[0].End + 10f);
            Assert.AreEqual(span.FromHeight, span.HeightAt(0f), 1e-4f, "and meets the ground at its ends");
            float steepest = 0f;
            for (int i = 0; i < 100; i++)
            {
                steepest = Mathf.Max(steepest, Mathf.Abs(span.HeightAt((i + 1) / 100f) - span.HeightAt(i / 100f)) / ((span.End - span.Start) / 100f));
            }
            Assert.LessOrEqual(steepest, RouteBridges.RampSlope * 1.6f, "at a slope a cart can take");
        }

        [Test]
        public void ARavine_IsSpanned_AGentleValleyIsNot()
        {
            float Notch(float x, float z) => 20f - 6f * Mathf.Clamp01(1f - Mathf.Abs(x - 50f) / 6f);
            float Valley(float x, float z) => 20f - 6f * Mathf.Clamp01(1f - Mathf.Abs(x - 50f) / 40f);
            var none = new System.Collections.Generic.List<RouteBridges.Span>();
            var over = RouteBridges.Ravines(new RoadLine(new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) }, Notch), none);
            Assert.AreEqual(1, over.Count);
            Assert.LessOrEqual(over[0].Start, 45f);
            Assert.GreaterOrEqual(over[0].End, 55f);
            Assert.LessOrEqual(over[0].End - over[0].Start, 24f, "bank to bank, no further");
            Assert.AreEqual(20.1f, over[0].FromHeight, 0.2f);
            Assert.AreEqual(0, RouteBridges.Ravines(new RoadLine(new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) }, Valley), none).Count);
            Assert.AreEqual(0, RouteBridges.Ravines(new RoadLine(new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) }, Notch), over).Count, "nor where a bridge already stands");
        }

        [Test]
        public void RoadOverDryLand_GetsNoBridge()
        {
            var route = new CartRoute(new Vector2(0f, 0f), new Vector2(100f, 0f), Flat, 1);
            WaterMap map = Map(200, 201, 5f);
            var spans = RouteBridges.Find(route, map);
            map.Carve.Dispose(); map.Surface.Dispose(); map.Depth.Dispose(); map.Kind.Dispose();
            map.Flow.Dispose(); map.LakeDistance.Dispose(); map.RiverDistance.Dispose();
            Assert.AreEqual(0, spans.Count);
        }
    }
}
