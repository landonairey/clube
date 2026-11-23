using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL808 (the clam model and the pearl icon) and GL809 (merchants buy pearls).</summary>
    public class ClamPearlTests
    {
        [Test]
        public void MerchantsBuyPearlsAt40AndBlackPearlsAt400()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Config/Items/ItemCatalog.asset");
            var prices = AssetDatabase.LoadAssetAtPath<PriceList>("Assets/Config/Economy/Merchant prices.asset");
            Assert.IsTrue(prices.TryGetLot(catalog.Find(ClamPearls.PearlId), out int pearl, out int pearlLot));
            Assert.IsTrue(prices.TryGetLot(catalog.Find(ClamPearls.BlackPearlId), out int black, out int blackLot));
            Assert.AreEqual(40, pearl);
            Assert.AreEqual(400, black);
            Assert.AreEqual(1, pearlLot);
            Assert.AreEqual(1, blackLot);
        }

        [Test]
        public void PearlIconIsATwentyFourSidedBallWithASheen()
        {
            Assert.IsTrue(ItemChunkMeshes.Has(ItemIconShape.Pearl));
            Mesh mesh = ItemChunkMeshes.Build(ItemIconShape.Pearl, new Color(0.96f, 0.93f, 0.88f));
            try
            {
                // 24 sides round by 12 rings of quads; the facets at the poles are triangles.
                int sides = ItemChunkMeshes.PearlSides, rings = sides / 2;
                Assert.AreEqual(24, sides);
                Assert.AreEqual(sides * rings * 2 - sides * 2, mesh.triangles.Length / 3);
                // The equator's silhouette has 24 corners.
                var around = new HashSet<int>();
                foreach (Vector3 v in mesh.vertices)
                {
                    if (Mathf.Abs(v.y - 0.42f) < 1e-3f)
                    {
                        around.Add(Mathf.RoundToInt(Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg));
                    }
                }
                Assert.AreEqual(sides, around.Count);
                // Iridescence: the facets are not all one colour, and some lean red and some blue.
                bool redder = false, bluer = false;
                foreach (Color c in mesh.colors)
                {
                    redder |= c.r > c.b + 0.08f;
                    bluer |= c.b > c.r + 0.08f;
                }
                Assert.IsTrue(redder && bluer, "the sheen runs through several hues");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ClamShellsMeetAtTheRimAndOpenFromTheHinge()
        {
            Mesh top = ClamMeshes.BuildShell(true, Color.gray), bottom = ClamMeshes.BuildShell(false, Color.gray);
            try
            {
                Assert.Greater(top.bounds.max.y, 0.03f);
                Assert.Less(bottom.bounds.min.y, -0.03f);
                Assert.AreEqual(0f, top.bounds.min.y, 1e-4f, "the top half sits on the rim");
                Assert.AreEqual(0f, top.bounds.min.z, 1e-3f, "the hinge is at the origin");
                Assert.AreEqual(ClamMeshes.Length, top.bounds.max.z, 0.02f);
                Assert.Less(top.vertexCount, 1000, "low poly");
            }
            finally
            {
                Object.DestroyImmediate(top);
                Object.DestroyImmediate(bottom);
            }
        }

        [Test]
        public void BubblesAreARunOfRisingBlups()
        {
            float[] samples = SoundSynth.Bubbles(808);
            Assert.AreEqual(SoundSynth.SampleRate * 1.1f, samples.Length, 2f);
            float peak = 0f;
            foreach (float s in samples)
            {
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            Assert.AreEqual(0.7f, peak, 0.02f);
        }
    }
}
