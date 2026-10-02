using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class MeshingRecorderTests
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly MeshingRecorder recorder = new MeshingRecorder();

        private static readonly ChunkMeshSettings Settings = new ChunkMeshSettings(0.5f, 1f);

        [Test]
        public void SingleVoxel_LogsStagesInTeachingOrder()
        {
            // Case 1: only corner 0 solid, so three crossed edges and one triangle.
            ChunkMesher.Build(SingleVoxel(1), Settings, vertices, triangles, recorder);

            MeshingStepType[] expected =
            {
                MeshingStepType.DensityField,
                MeshingStepType.SampleCorners,
                MeshingStepType.CaseIndex,
                MeshingStepType.EdgeTable,
                MeshingStepType.Interpolate,
                MeshingStepType.Interpolate,
                MeshingStepType.Interpolate,
                MeshingStepType.Triangle,
            };
            Assert.That(recorder.Steps.Select(step => step.Type), Is.EqualTo(expected));
        }

        [Test]
        public void SingleVoxel_RecordsCaseAndEdgeMask()
        {
            ChunkMesher.Build(SingleVoxel(41), Settings, vertices, triangles, recorder);

            RecordedVoxel voxel = recorder.Voxels.Single();
            Assert.That(voxel.CaseIndex, Is.EqualTo(41));
            Assert.That(voxel.CrossedEdgeMask, Is.EqualTo(MarchingCubes.GetCrossedEdgeMask(41)));
            Assert.That(voxel.CornerValues[0], Is.EqualTo(1f));
            Assert.That(voxel.CornerValues[1], Is.EqualTo(0f));
        }

        [Test]
        public void InterpolateSteps_CoverExactlyTheCrossedEdges()
        {
            ChunkMesher.Build(SingleVoxel(41), Settings, vertices, triangles, recorder);

            int mask = 0;
            foreach (MeshingStep step in recorder.Steps.Where(s => s.Type == MeshingStepType.Interpolate))
            {
                Assert.That(mask & (1 << step.Edge), Is.Zero, $"Edge {step.Edge} placed twice.");
                mask |= 1 << step.Edge;
            }
            Assert.That(mask, Is.EqualTo(recorder.Voxels[0].CrossedEdgeMask));
        }

        [Test]
        public void TriangleSteps_ReproduceTheBuiltMesh()
        {
            var chunk = new Chunk(new Vector3Int(2, 2, 2));
            chunk.SetDensity(new Vector3Int(1, 1, 1), 1f);
            chunk.SetDensity(new Vector3Int(0, 0, 0), 0.8f);
            chunk.SetDensity(new Vector3Int(2, 1, 0), 0.7f);

            ChunkMesher.Build(chunk, Settings, vertices, triangles, recorder);

            var replayed = new List<Vector3>();
            foreach (MeshingStep step in recorder.Steps.Where(s => s.Type == MeshingStepType.Triangle))
            {
                IReadOnlyList<Vector3> edgeVertices = recorder.Voxels[step.VoxelIndex].EdgeVertices;
                replayed.Add(edgeVertices[step.Triangle.A]);
                replayed.Add(edgeVertices[step.Triangle.B]);
                replayed.Add(edgeVertices[step.Triangle.C]);
            }

            // Flat shading writes one vertex per triangle corner, in order.
            Assert.That(replayed, Is.EqualTo(triangles.Select(index => vertices[index]).ToList()));
        }

        [Test]
        public void EmptyVoxels_LogOnlyTheClassificationStages()
        {
            ChunkMesher.Build(new Chunk(new Vector3Int(2, 1, 1)), Settings, vertices, triangles, recorder);

            Assert.That(recorder.Voxels.Count, Is.EqualTo(2));
            Assert.That(recorder.Steps.Count, Is.EqualTo(1 + 2 * 3));
            Assert.That(recorder.Steps.Any(step => step.Type == MeshingStepType.Interpolate), Is.False);
        }

        [Test]
        public void DensityField_IsRecordedOnceBeforeAnyVoxel()
        {
            var chunk = new Chunk(new Vector3Int(2, 1, 1));
            chunk.SetDensity(new Vector3Int(2, 1, 0), 0.7f);

            ChunkMesher.Build(chunk, Settings, vertices, triangles, recorder);

            Assert.That(recorder.Steps[0].Type, Is.EqualTo(MeshingStepType.DensityField));
            Assert.That(recorder.Steps[0].VoxelIndex, Is.EqualTo(-1));
            Assert.That(recorder.Steps.Count(step => step.Type == MeshingStepType.DensityField), Is.EqualTo(1));
            Assert.That(recorder.SampleCount, Is.EqualTo(new Vector3Int(3, 2, 2)));
            Assert.That(recorder.GetDensity(new Vector3Int(2, 1, 0)), Is.EqualTo(0.7f));
            Assert.That(recorder.GetDensity(new Vector3Int(1, 1, 0)), Is.EqualTo(0f));
        }

        [Test]
        public void Rebuild_ReplacesThePreviousLog()
        {
            ChunkMesher.Build(SingleVoxel(1), Settings, vertices, triangles, recorder);
            ChunkMesher.Build(SingleVoxel(0), Settings, vertices, triangles, recorder);

            Assert.That(recorder.Voxels.Count, Is.EqualTo(1));
            Assert.That(recorder.Steps.Count, Is.EqualTo(4));
        }

        [Test]
        public void Recording_DoesNotChangeTheMesh()
        {
            var chunk = SingleVoxel(105);
            var shading = new ChunkMeshSettings(0.5f, 1f, EdgePlacement.Interpolated, Shading.Smooth);
            var plainVertices = new List<Vector3>();
            var plainTriangles = new List<int>();

            ChunkMesher.Build(chunk, shading, plainVertices, plainTriangles);
            ChunkMesher.Build(chunk, shading, vertices, triangles, recorder);

            Assert.That(vertices, Is.EqualTo(plainVertices));
            Assert.That(triangles, Is.EqualTo(plainTriangles));
        }

        /// <summary>A 1x1x1 chunk whose solid corners (density 1) are the set bits of the case index.</summary>
        private static Chunk SingleVoxel(int caseIndex)
        {
            var chunk = new Chunk(Vector3Int.one);
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                float density = MarchingCubes.IsCornerSolid(caseIndex, corner) ? 1f : 0f;
                chunk.SetDensity(MarchingCubes.CornerOffset(corner), density);
            }
            return chunk;
        }
    }
}
