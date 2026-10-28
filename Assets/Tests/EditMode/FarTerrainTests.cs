using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The far terrain's clipmap (P7): levels nest, their seams close, and the distant ranges stay far.</summary>
    public class FarTerrainTests
    {
        private const float BaseSpacing = 2f;
        private const int Quads = 128;

        private static readonly float2[] Focuses =
        {
            new float2(0f, 0f), new float2(3.9f, -7.1f), new float2(-1234.5f, 987.6f), new float2(15003f, -22001f), new float2(-0.9f, 1.1f),
        };

        [Test]
        public void LevelsFor_ReachesTheRange()
        {
            int levels = FarTerrainGrid.LevelsFor(30000f, BaseSpacing, Quads);
            Assert.That(levels, Is.EqualTo(9));
            Assert.That(FarTerrainGrid.Around(float2.zero, levels - 1, BaseSpacing, Quads).HalfExtent, Is.GreaterThanOrEqualTo(30000f));
            Assert.That(FarTerrainGrid.Around(float2.zero, levels - 2, BaseSpacing, Quads).HalfExtent, Is.LessThan(30000f));
        }

        [Test]
        public void EachLevel_SitsInsideTheNextCoarser_OnItsGridLines()
        {
            foreach (float2 focus in Focuses)
            {
                for (int level = 1; level < 10; level++)
                {
                    FarTerrainGrid fine = FarTerrainGrid.Around(focus, level - 1, BaseSpacing, Quads);
                    FarTerrainGrid coarse = FarTerrainGrid.Around(focus, level, BaseSpacing, Quads);
                    int4 hole = coarse.Hole(fine);

                    // Inside, with at least one quad of coarse ring all round.
                    Assert.That(hole.x, Is.GreaterThanOrEqualTo(1), $"Level {level} at {focus}");
                    Assert.That(hole.y, Is.GreaterThanOrEqualTo(1));
                    Assert.That(hole.z, Is.LessThanOrEqualTo(Quads - 1));
                    Assert.That(hole.w, Is.LessThanOrEqualTo(Quads - 1));

                    // The hole's corners are the finer level's corners.
                    float2 fineMin = (float2)fine.First * fine.Spacing;
                    float2 holeMin = (float2)(coarse.First + hole.xy) * coarse.Spacing;
                    float2 holeMax = (float2)(coarse.First + hole.zw) * coarse.Spacing;
                    Assert.That(math.all(fineMin == holeMin), $"Hole starts at the finer level's corner, level {level} at {focus}");
                    Assert.That(math.all(fineMin + fine.Quads * fine.Spacing == holeMax), "and ends at its far corner");
                    // The focus is well inside the finer level.
                    Assert.That(math.all(math.abs(focus - fine.CentrePosition) <= fine.Spacing * 2f));
                }
            }
        }

        [Test]
        public void QuadsDrawn_LeavesOutTheHole()
        {
            Assert.That(FarTerrainGrid.QuadsDrawn(Quads, int4.zero), Is.EqualTo(Quads * Quads));
            Assert.That(FarTerrainGrid.QuadsDrawn(Quads, new int4(32, 33, 96, 97)), Is.EqualTo(Quads * Quads - 64 * 64));
        }

        [Test]
        public void FinerLevelsEdge_LiesOnTheCoarserLevelsHoleEdge()
        {
            var generator = new LandscapeGenerator(3, 20f, 110f, 250f, 0.35f, distant: new DistantRanges(1500f, 4000f, 100f));
            float2 focus = new float2(517.3f, -233.9f);
            FarTerrainGrid fine = FarTerrainGrid.Around(focus, 2, BaseSpacing, 32);
            FarTerrainGrid coarse = FarTerrainGrid.Around(focus, 3, BaseSpacing, 32);
            Vector3[] fineVertices = Build(generator, fine, int4.zero, stitch: true);
            Vector3[] coarseVertices = Build(generator, coarse, coarse.Hole(fine), stitch: true);

            // The coarse hole's edge, as segments between its vertices.
            int4 hole = coarse.Hole(fine);
            int side = coarse.Quads + 1;
            var edge = new List<(Vector3, Vector3)>();
            for (int i = hole.x; i < hole.z; i++)
            {
                edge.Add((coarseVertices[i + side * hole.y], coarseVertices[i + 1 + side * hole.y]));
                edge.Add((coarseVertices[i + side * hole.w], coarseVertices[i + 1 + side * hole.w]));
            }
            for (int j = hole.y; j < hole.w; j++)
            {
                edge.Add((coarseVertices[hole.x + side * j], coarseVertices[hole.x + side * (j + 1)]));
                edge.Add((coarseVertices[hole.z + side * j], coarseVertices[hole.z + side * (j + 1)]));
            }

            int fineSide = fine.Quads + 1;
            for (int j = 0; j < fineSide; j++)
            {
                for (int i = 0; i < fineSide; i++)
                {
                    if (i != 0 && j != 0 && i != fineSide - 1 && j != fineSide - 1)
                    {
                        continue;
                    }
                    Vector3 vertex = fineVertices[i + fineSide * j];
                    float nearest = float.MaxValue;
                    foreach ((Vector3 a, Vector3 b) in edge)
                    {
                        nearest = Mathf.Min(nearest, DistanceToSegment(vertex, a, b));
                    }
                    Assert.That(nearest, Is.LessThan(1e-3f), $"Fine edge vertex {i}, {j} at {vertex} is off the coarse edge.");
                }
            }
        }

        [Test]
        public void DistantRanges_LeaveTheNearLandAlone_AndStandTallFarOff()
        {
            var plain = new LandscapeHeight(5, 20f, 110f, 250f, 0.35f);
            var ranged = new LandscapeHeight(5, 20f, 110f, 250f, 0.35f, distant: new DistantRanges(1500f, 5000f, 2500f));
            for (float x = -2400f; x <= 2400f; x += 160f)
            {
                for (float z = -2400f; z <= 2400f; z += 160f)
                {
                    if (x * x + z * z >= 2500f * 2500f)
                    {
                        continue;
                    }
                    Assert.AreEqual(plain.Height(x, z), ranged.Height(x, z), $"No great range within its start, at {x}, {z}.");
                }
            }

            float highest = float.MinValue;
            for (float angle = 0f; angle < 360f; angle += 3f)
            {
                for (float distance = 6000f; distance <= 30000f; distance += 1000f)
                {
                    float2 p = distance * new float2(math.cos(math.radians(angle)), math.sin(math.radians(angle)));
                    highest = math.max(highest, ranged.Height(p.x, p.y));
                }
            }
            Assert.That(highest, Is.GreaterThan(800f), "Somewhere out there a great range stands kilometres tall.");
        }

        [Test]
        public void ColumnHeightRowsJob_MatchesTheManagedHeight()
        {
            var generator = new LandscapeGenerator(2, 20f, 110f, 250f, 0.35f, distant: new DistantRanges(1500f, 4000f, 0f));
            var block = new ColumnBlock(new int2(-70, 1230), new int2(37, 29), 16f);
            var heights = new NativeArray<float>(block.Length, Allocator.TempJob);
            try
            {
                generator.ScheduleColumnHeightsParallel(block, heights).Complete();
                for (int z = 0; z < block.Count.y; z++)
                {
                    for (int x = 0; x < block.Count.x; x++)
                    {
                        float2 position = block.Position(x, z);
                        Assert.AreEqual(generator.Height(position.x, position.y), heights[block.Index(x, z)], 1e-2f, $"Column {x}, {z}");
                    }
                }
            }
            finally
            {
                heights.Dispose();
            }
        }

        private static Vector3[] Build(HeightfieldGenerator generator, FarTerrainGrid grid, int4 hole, bool stitch)
        {
            ColumnBlock block = FarTerrainMeshJob.BorderedBlock(grid);
            var heights = new NativeArray<float>(block.Length, Allocator.TempJob);
            Mesh.MeshDataArray data = Mesh.AllocateWritableMeshData(1);
            var mesh = new Mesh();
            try
            {
                var job = new FarTerrainMeshJob
                {
                    Heights = heights, Quads = grid.Quads, Spacing = grid.Spacing, First = grid.First, Hole = hole, StitchEdges = stitch, Sink = 0f, Output = data[0],
                };
                Unity.Jobs.IJobExtensions.Schedule(job, generator.ScheduleColumnHeightsParallel(block, heights)).Complete();
                Mesh.ApplyAndDisposeWritableMeshData(data, mesh);
                Assert.That(mesh.GetIndexCount(0), Is.EqualTo((uint)(FarTerrainGrid.QuadsDrawn(grid.Quads, hole) * 6)));
                return mesh.vertices;
            }
            finally
            {
                heights.Dispose();
                Object.DestroyImmediate(mesh);
            }
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 along = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, along) / along.sqrMagnitude);
            return Vector3.Distance(point, a + along * t);
        }
    }
}
