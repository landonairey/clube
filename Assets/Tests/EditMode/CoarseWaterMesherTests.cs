using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// GL626: distant water as a quadtree of level sheets covers what the cell-per-quad sheets cover, with far fewer
    /// triangles and no gaps, steps or corners part-way along an edge.
    /// </summary>
    public class CoarseWaterMesherTests
    {
        private const int Size = 96;
        private const float Cell = 2f;

        private WaterMap map;

        [SetUp]
        public void SetUp()
        {
            int cells = Size * Size;
            map = new WaterMap
            {
                Origin = new float2(-40f, 12f),
                CellSize = Cell,
                Size = new int2(Size, Size),
                Carve = new NativeArray<float>(cells, Allocator.Temp),
                Surface = new NativeArray<float>(cells, Allocator.Temp),
                Depth = new NativeArray<float>(cells, Allocator.Temp),
                Kind = new NativeArray<byte>(cells, Allocator.Temp),
                Flow = new NativeArray<float2>(cells, Allocator.Temp),
                LakeDistance = new NativeArray<byte>(cells, Allocator.Temp),
                RiverDistance = new NativeArray<byte>(cells, Allocator.Temp),
            };
            for (int z = 0; z < Size; z++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = x + z * Size;
                    // A sea along the low-z side with a ragged shore, a round lake higher up, and a river running
                    // down from the lake into the sea, three cells wide.
                    int shore = 30 + (x * 7 % 5);
                    float dx = x - 60f, dz = z - 70f;
                    if (z < shore)
                    {
                        map.Kind[i] = (byte)WaterKind.Lake;
                        map.Surface[i] = 1f;
                    }
                    else if (dx * dx + dz * dz < 18f * 18f)
                    {
                        map.Kind[i] = (byte)WaterKind.Lake;
                        map.Surface[i] = 6.5f;
                    }
                    else if (x >= 58 && x <= 60 && z < 70)
                    {
                        map.Kind[i] = (byte)WaterKind.River;
                        map.Surface[i] = 1f + 5.5f * (z - 30) / 40f;
                        map.Flow[i] = new float2(0f, -1f);
                    }
                }
            }
        }

        private WaterSheetMesh Coarse(int regionCells, out CoarseWaterMesher mesher)
        {
            mesher = new CoarseWaterMesher(map, regionCells);
            var sheet = new WaterSheetMesh();
            for (int region = 0; region < mesher.RegionCount; region++)
            {
                mesher.Build(region, sheet);
            }
            return sheet;
        }

        private WaterSheetMesh PerCell(out int wet)
        {
            var sheet = new WaterSheetMesh();
            wet = 0;
            for (int z = 0; z < Size; z++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (map.Kind[x + z * Size] != (byte)WaterKind.Dry)
                    {
                        sheet.AddCell(map, x, z);
                        wet++;
                    }
                }
            }
            return sheet;
        }

        // Area seen from above, signed: positive for a triangle facing up.
        private static double Area(WaterSheetMesh sheet)
        {
            double area = 0;
            for (int i = 0; i < sheet.Vertices.Count; i += 3)
            {
                Vector3 a = sheet.Vertices[i], b = sheet.Vertices[i + 1], c = sheet.Vertices[i + 2];
                double facing = (double)(b.z - a.z) * (c.x - a.x) - (double)(b.x - a.x) * (c.z - a.z);
                Assert.Greater(facing, 0.0, "a triangle faces down or has no area");
                area += facing * 0.5;
            }
            return area;
        }

        // The length of the edges only one triangle has: the water's outline, plus any edge whose neighbour is split
        // by a corner part-way along it.
        private static double OpenEdgeLength(WaterSheetMesh sheet)
        {
            var edges = new Dictionary<(long, long), int>();
            long Key(Vector3 p) => (long)math.round(p.x * 2f) * 100000L + (long)math.round(p.z * 2f);
            for (int i = 0; i < sheet.Vertices.Count; i += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    long p = Key(sheet.Vertices[i + e]), q = Key(sheet.Vertices[i + (e + 1) % 3]);
                    (long, long) key = p < q ? (p, q) : (q, p);
                    edges.TryGetValue(key, out int count);
                    edges[key] = count + 1;
                }
            }
            double length = 0;
            foreach (KeyValuePair<(long, long), int> edge in edges)
            {
                Assert.LessOrEqual(edge.Value, 2, "three triangles share an edge");
                if (edge.Value == 1)
                {
                    long ax = (long)math.round(edge.Key.Item1 / 100000.0), bx = (long)math.round(edge.Key.Item2 / 100000.0);
                    long az = edge.Key.Item1 - ax * 100000L, bz = edge.Key.Item2 - bx * 100000L;
                    length += math.sqrt((double)((ax - bx) * (ax - bx) + (az - bz) * (az - bz))) * 0.5;
                }
            }
            return length;
        }

        [Test]
        public void CoversTheSameAreaAsCellPerQuad()
        {
            WaterSheetMesh coarse = Coarse(32, out _);
            WaterSheetMesh cells = PerCell(out int wet);
            Assert.AreEqual(wet * Cell * Cell, Area(cells), 0.01);
            Assert.AreEqual(Area(cells), Area(coarse), 0.01);
        }

        [Test]
        public void UsesFarFewerTriangles_AndRiversKeepTheirCells()
        {
            WaterSheetMesh coarse = Coarse(32, out CoarseWaterMesher mesher);
            WaterSheetMesh cells = PerCell(out _);
            Assert.Less(coarse.TriangleCount, cells.TriangleCount / 3);
            int riverCells = 0, riverQuads = 0;
            for (int i = 0; i < Size * Size; i++)
            {
                riverCells += map.Kind[i] == (byte)WaterKind.River ? 1 : 0;
            }
            for (int region = 0; region < mesher.RegionCount; region++)
            {
                foreach (CoarseWaterMesher.Quad quad in mesher.Quads(region))
                {
                    bool river = map.Kind[quad.X + quad.Z * Size] == (byte)WaterKind.River;
                    Assert.IsTrue(!river || quad.Size == 1, "a river cell was merged into a sheet");
                    riverQuads += river ? 1 : 0;
                }
            }
            Assert.AreEqual(riverCells, riverQuads);
        }

        [Test]
        public void NoCornerLiesPartWayAlongAnEdge()
        {
            // With a corner part-way along a sheet's side, the side and the two short edges meeting it would each
            // belong to one triangle only: more open edge than the water's outline.
            WaterSheetMesh coarse = Coarse(32, out _);
            WaterSheetMesh cells = PerCell(out _);
            Assert.AreEqual(OpenEdgeLength(cells), OpenEdgeLength(coarse), 0.01);
        }

        [Test]
        public void EveryGridPointHasOneHeight_TheCellPerQuadOne()
        {
            WaterSheetMesh cells = PerCell(out _);
            var heights = new Dictionary<Vector2, float>();
            foreach (Vector3 v in cells.Vertices)
            {
                heights[new Vector2(v.x, v.z)] = v.y;
            }
            WaterSheetMesh coarse = Coarse(32, out _);
            var seen = new Dictionary<Vector2, float>();
            foreach (Vector3 v in coarse.Vertices)
            {
                var at = new Vector2(v.x, v.z);
                if (seen.TryGetValue(at, out float before))
                {
                    Assert.AreEqual(before, v.y, "two heights at " + at);
                }
                seen[at] = v.y;
                if (heights.TryGetValue(at, out float cellHeight))
                {
                    Assert.AreEqual(cellHeight, v.y, "a step against the cell-per-quad sheet at " + at);
                }
            }
        }

        [Test]
        public void OpenSeaRegion_IsOneSheetOfTwoTriangles()
        {
            for (int i = 0; i < Size * Size; i++)
            {
                map.Kind[i] = (byte)WaterKind.Lake;
                map.Surface[i] = 1f;
            }
            WaterSheetMesh coarse = Coarse(32, out CoarseWaterMesher mesher);
            Assert.AreEqual(9, mesher.QuadCount);
            Assert.AreEqual(18, coarse.TriangleCount);
        }

        [Test]
        public void RegionSizeDoesNotChangeTheCover()
        {
            Assert.AreEqual(Area(Coarse(32, out _)), Area(Coarse(16, out _)), 0.01);
            Assert.AreEqual(Area(Coarse(32, out _)), Area(Coarse(128, out _)), 0.01);
        }
    }
}
