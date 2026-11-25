using Clube.Core;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class PearlMeteorTests
    {
        [Test]
        public void ClamHoldsAPearlOnlyBelowTheChance()
        {
            Assert.AreEqual(ClamFind.Empty, ClamPearls.Open(0.25f, 0.5f));
            Assert.AreEqual(ClamFind.Pearl, ClamPearls.Open(0.24f, 0.5f));
        }

        [Test]
        public void OneInAHundredPearlsIsBlack()
        {
            Assert.AreEqual(ClamFind.BlackPearl, ClamPearls.Open(0.1f, 0.005f));
            Assert.AreEqual(ClamFind.Pearl, ClamPearls.Open(0.1f, 0.01f));
            Assert.IsNull(ClamPearls.IdOf(ClamFind.Empty));
            Assert.AreEqual("black-pearl", ClamPearls.IdOf(ClamFind.BlackPearl));
        }

        [Test]
        public void PearlRatesHoldOverManyClams()
        {
            var random = new System.Random(1);
            int pearls = 0, black = 0, n = 100000;
            for (int i = 0; i < n; i++)
            {
                ClamFind f = ClamPearls.Open((float)random.NextDouble(), (float)random.NextDouble());
                pearls += f != ClamFind.Empty ? 1 : 0;
                black += f == ClamFind.BlackPearl ? 1 : 0;
            }
            Assert.AreEqual(0.25, pearls / (double)n, 0.01);
            Assert.AreEqual(0.01, black / (double)pearls, 0.004);
        }

        [Test]
        public void ClamScatterIsStableAndInsideItsCell()
        {
            int found = 0;
            for (int x = -20; x < 20; x++)
            {
                for (int z = -20; z < 20; z++)
                {
                    var cell = new Vector2Int(x, z);
                    bool a = ClamScatter.TryClam(cell, 735, out Vector2 p);
                    bool b = ClamScatter.TryClam(cell, 735, out Vector2 q);
                    Assert.AreEqual(a, b);
                    if (!a)
                    {
                        continue;
                    }
                    found++;
                    Assert.AreEqual(p, q);
                    Assert.AreEqual(cell, ClamScatter.CellOf(p.x, p.y));
                }
            }
            Assert.AreEqual(ClamScatter.Density, found / 1600f, 0.06f);
        }

        [Test]
        public void MeteorComesFromFarOffAndHighUp()
        {
            var plan = new MeteorPlan(new Vector3(10f, 5f, -3f), 90f);
            Assert.Greater(plan.Start.y, plan.Impact.y + 1000f);
            Vector3 flat = plan.Start - plan.Impact;
            flat.y = 0f;
            Assert.Greater(flat.magnitude, 2000f);
            Assert.AreEqual(plan.Impact, plan.At(1f));
            Assert.AreEqual(plan.Start, plan.At(0f));
            Assert.GreaterOrEqual(MeteorPlan.FallSeconds, 10f);
        }

        [Test]
        public void MeteorFallIsSlowFarOffAndKeepsComingDown()
        {
            var plan = new MeteorPlan(Vector3.zero, 0f);
            float last = plan.Start.y;
            for (int i = 1; i <= 20; i++)
            {
                float y = plan.At(i / 20f).y;
                Assert.Less(y, last);
                last = y;
            }
            float firstStep = (plan.At(0.05f) - plan.At(0f)).magnitude, lastStep = (plan.At(1f) - plan.At(0.95f)).magnitude;
            Assert.Less(firstStep, lastStep);
        }

        [Test]
        public void GougeRunsBackAlongTheApproachAndDeepensTowardsTheCrater()
        {
            var plan = new MeteorPlan(Vector3.zero, 30f);
            float lastDistance = float.MaxValue, lastRadius = 0f;
            for (int i = 0; i < MeteorPlan.GougeCuts; i++)
            {
                MeteorCut cut = plan.Cut(i);
                Assert.Greater(Vector2.Dot(cut.Offset.normalized, plan.Approach), 0.999f);
                Assert.Less(cut.Offset.magnitude, lastDistance);
                Assert.Greater(cut.Radius, lastRadius);
                lastDistance = cut.Offset.magnitude;
                lastRadius = cut.Radius;
            }
            Assert.Greater(plan.Cut(0).Offset.magnitude, MeteorPlan.GougeLength);
            Assert.Less(plan.Cut(MeteorPlan.GougeCuts - 1).Offset.magnitude, MeteorPlan.CraterRadius);
        }

        [Test]
        public void OreBallSitsInTheCraterAndIsMostlyIron()
        {
            var plan = new MeteorPlan(Vector3.zero, 45f);
            MeteorPiece ball = plan.Ball;
            Assert.AreEqual(MeteorPlan.IronOre, ball.Ore);
            Assert.Less(ball.Offset.y + ball.Radius, MeteorPlan.CraterLift + 0.5f, "the ball's top is about the old ground level");
            Assert.Greater(ball.Offset.y, MeteorPlan.CraterLift - MeteorPlan.CraterRadius, "the ball's middle is above the crater floor");
            float other = 0f;
            for (int i = 0; i < MeteorPlan.BallInclusions.Length; i++)
            {
                MeteorPiece lump = plan.Inclusion(i);
                Assert.AreNotEqual(MeteorPlan.IronOre, lump.Ore);
                float fromMiddle = (lump.Offset - ball.Offset).magnitude;
                Assert.Less(fromMiddle, ball.Radius, "each lump is set into the ball");
                Assert.Greater(lump.Offset.y, ball.Offset.y, "lumps sit on the upper side, where they show");
                other += lump.Volume;
            }
            Assert.Less(other / ball.Volume, 0.2f, "mostly iron ore");
        }

        [Test]
        public void ScatteredPiecesLieRoundTheSiteMostlyIron()
        {
            var plan = new MeteorPlan(Vector3.zero, 200f);
            int iron = 0;
            for (int i = 0; i < MeteorPlan.ScatteredCount; i++)
            {
                MeteorPiece piece = plan.Scattered(i);
                float d = new Vector2(piece.Offset.x, piece.Offset.z).magnitude;
                Assert.That(d, Is.InRange(MeteorPlan.ScatterNear - 0.01f, MeteorPlan.ScatterFar + 0.01f));
                Assert.Less(piece.Radius, MeteorPlan.BallRadius * 0.5f);
                Assert.IsTrue(piece.OnGround);
                iron += piece.Ore == MeteorPlan.IronOre ? 1 : 0;
            }
            Assert.Greater(iron, MeteorPlan.ScatteredCount / 2);
            Assert.LessOrEqual(MeteorPlan.ScatterFar, MeteorPlan.Reach);
        }
    }
}
