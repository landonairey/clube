using System.Collections.Generic;
using NUnit.Framework;

namespace Clube.Core.Tests
{
    public class MarchingCubesCasesTests
    {
        [Test]
        public void Rotations_MoveOneCornerToEveryCorner()
        {
            var reached = new HashSet<int>();
            for (int r = 0; r < MarchingCubesCases.RotationCount; r++)
            {
                reached.Add(MarchingCubesCases.Rotate(1, r));
            }

            Assert.That(reached.Count, Is.EqualTo(8));
        }

        [Test]
        public void Rotations_AreAllDistinct()
        {
            // Corners 0, 1 and 3 together are not symmetric under any rotation, so
            // 24 distinct rotations give 24 distinct images.
            var images = new HashSet<int>();
            for (int r = 0; r < MarchingCubesCases.RotationCount; r++)
            {
                images.Add(MarchingCubesCases.Rotate(0b1011, r));
            }

            Assert.That(images.Count, Is.EqualTo(MarchingCubesCases.RotationCount));
        }

        [Test]
        public void EveryCase_KeepsItsConfigurationUnderRotation()
        {
            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                BaseConfiguration expected = MarchingCubesCases.GetBaseConfiguration(caseIndex);
                for (int r = 0; r < MarchingCubesCases.RotationCount; r++)
                {
                    int rotated = MarchingCubesCases.Rotate(caseIndex, r);
                    Assert.That(MarchingCubesCases.GetBaseConfiguration(rotated), Is.EqualTo(expected),
                        $"Case {caseIndex}, rotation {r}");
                }
            }
        }

        [Test]
        public void EveryCase_SharesConfigurationWithItsComplement()
        {
            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                Assert.That(MarchingCubesCases.GetBaseConfiguration(~caseIndex & 0xFF),
                    Is.EqualTo(MarchingCubesCases.GetBaseConfiguration(caseIndex)), $"Case {caseIndex}");
            }
        }

        // How many of the 256 cases fall into each configuration. If the
        // classification merged or split shapes wrongly, these counts would change.
        [TestCase(BaseConfiguration.Empty, 2)]
        [TestCase(BaseConfiguration.SingleCorner, 16)]
        [TestCase(BaseConfiguration.Edge, 24)]
        [TestCase(BaseConfiguration.FaceDiagonal, 24)]
        [TestCase(BaseConfiguration.BodyDiagonal, 8)]
        [TestCase(BaseConfiguration.LOnFace, 48)]
        [TestCase(BaseConfiguration.EdgeAndFarCorner, 48)]
        [TestCase(BaseConfiguration.ThreeFaceDiagonals, 16)]
        [TestCase(BaseConfiguration.Face, 6)]
        [TestCase(BaseConfiguration.Star, 8)]
        [TestCase(BaseConfiguration.OppositeEdges, 6)]
        [TestCase(BaseConfiguration.Zigzag, 12)]
        [TestCase(BaseConfiguration.LAndFarCorner, 24)]
        [TestCase(BaseConfiguration.Alternating, 2)]
        [TestCase(BaseConfiguration.ZigzagMirrored, 12)]
        public void Configuration_CoversExpectedNumberOfCases(BaseConfiguration configuration, int expectedCases)
        {
            int count = 0;
            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                if (MarchingCubesCases.GetBaseConfiguration(caseIndex) == configuration)
                {
                    count++;
                }
            }

            Assert.That(count, Is.EqualTo(expectedCases));
        }

        [TestCase(0b0000_0001, BaseConfiguration.SingleCorner)]
        [TestCase(0b0000_0011, BaseConfiguration.Edge)]           // corners 0, 1
        [TestCase(0b0000_0101, BaseConfiguration.FaceDiagonal)]   // corners 0, 2
        [TestCase(0b0100_0001, BaseConfiguration.BodyDiagonal)]   // corners 0, 6
        [TestCase(0b0000_1111, BaseConfiguration.Face)]           // bottom face
        [TestCase(0b0010_1001, BaseConfiguration.EdgeAndFarCorner)] // corners 0, 3, 5 (case 41)
        [TestCase(0b1010_0101, BaseConfiguration.Alternating)]    // corners 0, 2, 5, 7
        public void KnownCase_HasExpectedConfiguration(int caseIndex, BaseConfiguration expected)
        {
            Assert.That(MarchingCubesCases.GetBaseConfiguration(caseIndex), Is.EqualTo(expected));
        }

        [TestCase(0b0000_0001, false)]
        [TestCase(0b0000_1111, false)]
        [TestCase(0b0000_0101, true)]  // face diagonal
        [TestCase(0b1010_0101, true)]  // alternating: every face is ambiguous
        public void HasAmbiguousFace_DetectsDiagonalPairsOnAFace(int caseIndex, bool expected)
        {
            Assert.That(MarchingCubesCases.HasAmbiguousFace(caseIndex), Is.EqualTo(expected));
        }

        [Test]
        public void RepresentativeCase_BelongsToItsConfiguration()
        {
            for (int i = 0; i < MarchingCubesCases.BaseConfigurationCount; i++)
            {
                var configuration = (BaseConfiguration)i;
                int representative = MarchingCubesCases.GetRepresentativeCase(configuration);

                Assert.That(MarchingCubesCases.GetBaseConfiguration(representative), Is.EqualTo(configuration));
            }
        }
    }
}
