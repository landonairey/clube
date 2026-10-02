using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class VoxelVolumeTests
    {
        private const float Tolerance = 1e-4f;

        // A right-angled corner tetrahedron with legs of 0.5: 0.5³ / 6.
        private const float CornerTetrahedron = 0.125f / 6f;

        private static readonly IEdgeVertexPlacer Interpolated = InterpolatedEdgePlacer.Instance;

        [Test]
        public void Exact_AllEmpty_IsZero()
        {
            Assert.That(VoxelVolume.Exact(Binary(0), 0.5f, Interpolated), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Exact_AllSolid_IsWholeCube()
        {
            Assert.That(VoxelVolume.Exact(Binary(255), 0.5f, Interpolated), Is.EqualTo(1f).Within(Tolerance));
        }

        [TestCase(0)] // c0: on the reference point, only surface tetrahedra count
        [TestCase(6)] // c6: opposite corner, the far-face patches do most of the work
        [TestCase(3)]
        public void Exact_SingleCorner_IsCornerTetrahedron(int corner)
        {
            Assert.That(VoxelVolume.Exact(Binary(1 << corner), 0.5f, Interpolated),
                Is.EqualTo(CornerTetrahedron).Within(Tolerance));
        }

        [TestCase(0b0000_0011)] // c0, c1
        [TestCase(0b1100_0000)] // c6, c7
        public void Exact_Edge_IsTriangularPrism(int caseIndex)
        {
            // Right triangle with legs 0.5, extruded along a whole edge: 0.125.
            Assert.That(VoxelVolume.Exact(Binary(caseIndex), 0.5f, Interpolated), Is.EqualTo(0.125f).Within(Tolerance));
        }

        [Test]
        public void Exact_BottomFace_IsHalfCube()
        {
            Assert.That(VoxelVolume.Exact(Binary(0b0000_1111), 0.5f, Interpolated), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Exact_FollowsInterpolatedIsoLevel()
        {
            // Bottom solid, top empty, iso 0.25: the surface sits at height 0.75.
            Assert.That(VoxelVolume.Exact(Binary(0b0000_1111), 0.25f, Interpolated), Is.EqualTo(0.75f).Within(Tolerance));
        }

        [Test]
        public void Exact_MidpointPlacement_IgnoresIsoLevel()
        {
            Assert.That(VoxelVolume.Exact(Binary(1), 0.2f, MidpointEdgePlacer.Instance),
                Is.EqualTo(CornerTetrahedron).Within(Tolerance));
        }

        [Test]
        public void Exact_EveryBinaryCase_IsBetweenEmptyAndFull()
        {
            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                float volume = VoxelVolume.Exact(Binary(caseIndex), 0.5f, Interpolated);
                Assert.That(volume, Is.InRange(-Tolerance, 1f + Tolerance), $"Case {caseIndex}");
            }
        }

        [Test]
        public void Exact_CaseAndComplement_FillTheCube_WhenNoFaceIsAmbiguous()
        {
            // Swapping solid and empty gives the same surface, so the two solids
            // together fill the cube. (Ambiguous faces may be resolved differently
            // for a case and its complement, so those are excluded.)
            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                if (MarchingCubesCases.HasAmbiguousFace(caseIndex))
                {
                    continue;
                }

                float volume = VoxelVolume.Exact(Binary(caseIndex), 0.5f, Interpolated);
                float complement = VoxelVolume.Exact(Binary(~caseIndex & 0xFF), 0.5f, Interpolated);
                Assert.That(volume + complement, Is.EqualTo(1f).Within(Tolerance), $"Case {caseIndex}");
            }
        }

        [Test]
        public void Tetrahedra_SumToExactVolume()
        {
            float[] corners = { 0.9f, 0.1f, 0.7f, 0.3f, 0.6f, 0.2f, 0.8f, 0.4f };
            var tetrahedra = new List<Tetrahedron>();
            VoxelVolume.Tetrahedralise(corners, 0.5f, Interpolated, tetrahedra);

            float sum = 0f;
            tetrahedra.ForEach(t => sum += t.SignedVolume);

            Assert.That(sum, Is.EqualTo(VoxelVolume.Exact(corners, 0.5f, Interpolated)).Within(Tolerance));
        }

        [Test]
        public void Tetrahedra_AreNeverInsideOut_ForAnyBinaryCase()
        {
            // Every tetrahedron should lie inside the solid; a negative one would be
            // volume outside the solid cancelled by others.
            var tetrahedra = new List<Tetrahedron>();
            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                VoxelVolume.Tetrahedralise(Binary(caseIndex), 0.5f, Interpolated, tetrahedra);
                foreach (Tetrahedron tetrahedron in tetrahedra)
                {
                    Assert.That(tetrahedron.SignedVolume, Is.GreaterThan(0f), $"Case {caseIndex}");
                }
            }
        }

        [Test]
        public void Tetrahedra_AreNeverInsideOut_ForRandomDensities()
        {
            // In-between densities can fold the surface, so a piece may not be fully
            // visible from any of its solid corners; the apex search must still find
            // a point that sees every boundary triangle from the front.
            const int runsPerIso = 1000;
            var random = new System.Random(42);
            var tetrahedra = new List<Tetrahedron>();
            var corners = new float[MarchingCubes.CornerCount];
            var failingRuns = new List<string>();
            foreach (float iso in new[] { 0.3f, 0.5f, 0.7f })
            {
                for (int run = 0; run < runsPerIso; run++)
                {
                    for (int corner = 0; corner < corners.Length; corner++)
                    {
                        corners[corner] = (float)random.NextDouble();
                    }

                    VoxelVolume.Tetrahedralise(corners, iso, Interpolated, tetrahedra);
                    if (tetrahedra.Exists(t => t.SignedVolume <= 0f))
                    {
                        failingRuns.Add($"iso {iso} run {run} case {MarchingCubes.GetCaseIndex(corners, iso)}");
                    }
                }
            }

            Assert.That(failingRuns, Is.Empty, $"{failingRuns.Count} failing: {string.Join("; ", failingRuns)}");
        }

        [TestCase(0b0000_1000, 1)] // case 8, c3 alone: the corner tetrahedron itself
        [TestCase(0b0000_1010, 2)] // case 10, c1 and c3 across a face: two separate corners
        [TestCase(0b0000_1100, 3)] // case 12, edge c2-c3: a triangular prism
        [TestCase(0b1111_1111, 6)] // full cube from one corner: six tetrahedra
        public void Tetrahedra_UseTheFewestPieces(int caseIndex, int expectedCount)
        {
            var tetrahedra = new List<Tetrahedron>();
            VoxelVolume.Tetrahedralise(Binary(caseIndex), 0.5f, Interpolated, tetrahedra);

            Assert.That(tetrahedra.Count, Is.EqualTo(expectedCount));
        }

        [Test]
        public void Approximate_IsMeanOfClampedCorners()
        {
            float[] corners = { 1f, 1f, 0f, 0f, 0.5f, 0.5f, 2f, -1f };

            // Clamped: 1 + 1 + 0 + 0 + 0.5 + 0.5 + 1 + 0 = 4, over 8 corners.
            Assert.That(VoxelVolume.Approximate(corners), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Trilinear_MatchesCornerValuesAtCorners()
        {
            float[] corners = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Assert.That(VoxelVolume.Trilinear(corners, MarchingCubes.CornerPosition(corner)),
                    Is.EqualTo(corners[corner]).Within(Tolerance));
            }
        }

        [Test]
        public void SampleTrilinear_BottomFace_IsAboutHalf()
        {
            // Density falls linearly from 1 at the bottom to 0 at the top: half is solid.
            float estimate = VoxelVolume.SampleTrilinear(Binary(0b0000_1111), 0.5f, 20000);

            Assert.That(estimate, Is.EqualTo(0.5f).Within(0.02f));
        }

        [Test]
        public void SampleTrilinear_IsRepeatableForSameSeed()
        {
            float[] corners = { 0.9f, 0.1f, 0.7f, 0.3f, 0.6f, 0.2f, 0.8f, 0.4f };

            Assert.That(VoxelVolume.SampleTrilinear(corners, 0.5f, 1000, seed: 7),
                Is.EqualTo(VoxelVolume.SampleTrilinear(corners, 0.5f, 1000, seed: 7)));
        }

        [Test]
        public void Exact_CornersExactlyOnTheIsoLevel_NeverThrowsAndStaysInRange()
        {
            // Every mix of empty (0), exactly-on-iso (0.5) and solid (1) corners: 3^8 sets.
            // A corner at the iso level puts surface vertices on the corner itself,
            // which used to leave face outlines that couldn't close.
            var corners = new float[MarchingCubes.CornerCount];
            for (int combination = 0; combination < 6561; combination++)
            {
                int digits = combination;
                for (int corner = 0; corner < corners.Length; corner++)
                {
                    corners[corner] = digits % 3 * 0.5f;
                    digits /= 3;
                }

                float volume = 0f;
                Assert.DoesNotThrow(
                    () => volume = VoxelVolume.Exact(corners, 0.5f, Interpolated),
                    $"Corners {string.Join(", ", corners)}");
                Assert.That(volume, Is.InRange(-Tolerance, 1f + Tolerance), $"Corners {string.Join(", ", corners)}");
            }
        }

        [Test]
        public void Exact_CornerExactlyOnTheIsoLevel_MatchesAHairAbove()
        {
            float[] onIso = { 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 1f };
            float[] justAbove = { 0.5001f, 0f, 0f, 0f, 0f, 0f, 0f, 1f };

            Assert.That(VoxelVolume.Exact(onIso, 0.5f, Interpolated),
                Is.EqualTo(VoxelVolume.Exact(justAbove, 0.5f, Interpolated)).Within(Tolerance));
        }

        /// <summary>Corner values of 1 (solid) or 0 (empty) matching a case index.</summary>
        private static float[] Binary(int caseIndex)
        {
            var corners = new float[MarchingCubes.CornerCount];
            for (int corner = 0; corner < corners.Length; corner++)
            {
                corners[corner] = MarchingCubes.IsCornerSolid(caseIndex, corner) ? 1f : 0f;
            }
            return corners;
        }
    }
}
