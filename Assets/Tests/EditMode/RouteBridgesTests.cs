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
