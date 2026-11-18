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
        public void MeteorFallsFromAboveToTheImpact()
        {
            var plan = new MeteorPlan(new Vector3(10f, 5f, -3f), 90f);
            Assert.Greater(plan.Start.y, plan.Impact.y + 300f);
            Assert.AreEqual(plan.Impact, plan.At(1f));
            Assert.AreEqual(plan.Start, plan.At(0f));
        }

        [Test]
        public void MeteorLumpsLieInsideTheCrater()
        {
            for (int i = 0; i < MeteorPlan.Lumps; i++)
            {
                Vector3 o = MeteorPlan.LumpOffset(i, 30f);
                Assert.Less(new Vector2(o.x, o.z).magnitude, MeteorPlan.CraterRadius);
                Assert.Less(o.y, 0f);
            }
        }
    }
}
