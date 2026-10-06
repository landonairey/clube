using NUnit.Framework;

namespace Clube.Core.Tests
{
    /// <summary>The flattened tables the Burst jobs read are generated from <see cref="MarchingCubesTables"/>; they must still match it.</summary>
    public class MarchingCubesJobTablesTests
    {
        [Test]
        public void CornerOffsets_MatchTheTables()
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.That(MarchingCubesJobTables.CornerOffsets[corner * 3 + axis], Is.EqualTo(MarchingCubesTables.CornerOffsets[corner, axis]));
                }
            }
        }

        [Test]
        public void EdgeCorners_MatchTheTables()
        {
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                Assert.That(MarchingCubesJobTables.EdgeCorners[edge * 2], Is.EqualTo(MarchingCubesTables.EdgeCorners[edge, 0]));
                Assert.That(MarchingCubesJobTables.EdgeCorners[edge * 2 + 1], Is.EqualTo(MarchingCubesTables.EdgeCorners[edge, 1]));
            }
        }

        [Test]
        public void EdgeMasksAndTriangles_MatchTheTables()
        {
            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                Assert.That(MarchingCubesJobTables.EdgeMasks[caseIndex], Is.EqualTo(MarchingCubes.GetCrossedEdgeMask(caseIndex)), $"case {caseIndex}");
                for (int i = 0; i < MarchingCubesJobTables.TriangleStride; i++)
                {
                    Assert.That(
                        MarchingCubesJobTables.Triangles[caseIndex * MarchingCubesJobTables.TriangleStride + i],
                        Is.EqualTo(MarchingCubesTables.Triangles[caseIndex, i]), $"case {caseIndex}, entry {i}");
                }
            }
        }
    }
}
