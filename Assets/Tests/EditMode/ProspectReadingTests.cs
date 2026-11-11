using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class ProspectReadingTests
    {
        [Test]
        public void Grades_FollowTheBands()
        {
            Assert.AreEqual(Grade.Pure, ProspectReading.GradeOf(1f));
            Assert.AreEqual(Grade.HighGrade, ProspectReading.GradeOf(0.999f));
            Assert.AreEqual(Grade.HighGrade, ProspectReading.GradeOf(0.9f));
            Assert.AreEqual(Grade.Good, ProspectReading.GradeOf(0.75f));
            Assert.AreEqual(Grade.Medium, ProspectReading.GradeOf(0.5f));
            Assert.AreEqual(Grade.Poor, ProspectReading.GradeOf(0.49f));
        }

        [Test]
        public void EachLevel_ReadsOneMoreComponent_AllAtTen()
        {
            Assert.AreEqual(1, ProspectReading.ComponentsRead(0));
            Assert.AreEqual(4, ProspectReading.ComponentsRead(3));
            Assert.AreEqual(int.MaxValue, ProspectReading.ComponentsRead(ProspectReading.ExactLevel));
        }

        [Test]
        public void OwnMaterial_ReadsFirst_EvenWhenTheHostRockIsMore()
        {
            VoxelMaterial ore = ScriptableObject.CreateInstance<VoxelMaterial>();
            VoxelMaterial stone = ScriptableObject.CreateInstance<VoxelMaterial>();
            VoxelMaterial silver = ScriptableObject.CreateInstance<VoxelMaterial>();
            try
            {
                Composition contents = Composition.From(new Content(stone, 0.6f), new Content(ore, 0.3f), new Content(silver, 0.1f));
                var order = ProspectReading.InReadingOrder(contents, m => m == ore);
                Assert.AreEqual(ore, order[0].Material);
                Assert.AreEqual(stone, order[1].Material);
                Assert.AreEqual(silver, order[2].Material);
            }
            finally
            {
                Object.DestroyImmediate(ore);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(silver);
            }
        }

        [Test]
        public void ShareWord_NamesTheBandsOfANonPrimaryComponent()
        {
            Assert.AreEqual("Dominant", ProspectReading.ShareWord(0.5f));
            Assert.AreEqual("Abundant", ProspectReading.ShareWord(0.49f));
            Assert.AreEqual("Abundant", ProspectReading.ShareWord(0.25f));
            Assert.AreEqual("Moderate", ProspectReading.ShareWord(0.24f));
            Assert.AreEqual("Moderate", ProspectReading.ShareWord(0.10f));
            Assert.AreEqual("Minor", ProspectReading.ShareWord(0.09f));
            Assert.AreEqual("Minor", ProspectReading.ShareWord(0.01f));
            Assert.AreEqual("Trace", ProspectReading.ShareWord(0.009f));
            Assert.AreEqual("Trace", ProspectReading.ShareWord(0.001f));
        }

        [Test]
        public void QualityAt_IsAGradeWordBelowExactLevel_APercentFromIt()
        {
            Assert.AreEqual("Good", ProspectReading.QualityAt(0.8f, 3));
            Assert.AreEqual("80%", ProspectReading.QualityAt(0.8f, ProspectReading.ExactLevel));
        }
    }
}
