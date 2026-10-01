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
