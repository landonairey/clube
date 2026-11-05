using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The 2026-11-04 changes (GL384-GL398): station output blends qualities, part stacks merge by amount, refining fails
    /// more the purer a stack is, a bucket holds its quality's share, and a tap of Drop puts one sample on the aimed spot.
    /// </summary>
    public class Pass32Tests
    {
        [Test]
        public void SmeltsOfDifferentPurity_BlendInTheOutput()
        {
            // GL385: each run's bun has its own purity; the furnace jammed at 1.75 buns.
            ItemDefinition ore = ItemDefinition.Create("cassiterite", "Cassiterite", 256);
            ItemDefinition bun = ItemDefinition.Create("tin-bun", "Tin bun", 32);
            Recipe smelt = Recipe.Create(StationKind.FlatRock, new ItemAmount(bun, 1), RecipeWork.Strikes, 1f, new ItemAmount(ore, 1));
            try
            {
                var station = new CraftingStation(StationKind.FlatRock, new[] { smelt });
                station.Output.Add(bun, 1, null, 0, 0.94f, 0.75f);
                Assert.IsTrue(station.OutputFits(smelt), "Another quality of bun still fits.");
                station.Output.Add(bun, 1, null, 0, 0.98f, 0f, anyQuality: true);
                Assert.AreEqual(2.75f, station.Output[0].Amount, 1e-3f);
                Assert.AreEqual((0.94f * 1.75f + 0.98f) / 2.75f, station.Output[0].Quality, 1e-3f, "Qualities blend by amount.");
            }
            finally
            {
                Object.DestroyImmediate(smelt);
                Object.DestroyImmediate(ore);
                Object.DestroyImmediate(bun);
            }
        }

        [Test]
        public void DraggingPartStacks_FillsTheTargetByAmount()
        {
            // GL390: 1.05 buns onto 15.24 (and the other way) makes a full 16 and leaves 0.29.
            ItemDefinition bun = ItemDefinition.Create("tin-bun", "Tin bun", 16);
            try
            {
                var bag = new Inventory(2);
                bag.Set(0, new ItemStack(bun, 15, null, 0, 1f, 0.24f));
                bag.Set(1, new ItemStack(bun, 1, null, 0, 1f, 0.05f));
                bag.Move(1, 0);
                Assert.AreEqual(16f, bag[0].Amount, 1e-3f);
                Assert.AreEqual(0.29f, bag[1].Amount, 1e-3f);

                bag.Set(0, new ItemStack(bun, 1, null, 0, 1f, 0.05f));
                bag.Set(1, new ItemStack(bun, 15, null, 0, 1f, 0.24f));
                bag.Move(1, 0);
                Assert.AreEqual(16f, bag[0].Amount, 1e-3f);
                Assert.AreEqual(0.29f, bag[1].Amount, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(bun);
            }
        }

        [Test]
        public void RefiningFails_MoreOften_ThePurerTheStack()
        {
            Assert.AreEqual(0f, Refining.FailChance(0.3f));
            Assert.AreEqual(0.1f, Refining.FailChance(0.55f));
            Assert.AreEqual(0.35f, Refining.FailChance(0.75f));
            Assert.AreEqual(0.63f, Refining.FailChance(0.92f));
            Assert.AreEqual(0.95f, Refining.FailChance(0.985f));
            Assert.AreEqual(0.99f, Refining.FailChance(0.995f));
        }

        [Test]
        public void ARefiningPass_TakesOneImpurity_OrFails()
        {
            ItemDefinition clayItem = ItemDefinition.Create("clay", "Clay");
            VoxelMaterial clay = VoxelMaterial.Create(13, "Clay").WithBreaking(1f, clayItem);
            VoxelMaterial dirt = VoxelMaterial.Create(1, "Dirt");
            VoxelMaterial sand = VoxelMaterial.Create(24, "Sand");
            MaterialRegistry registry = MaterialRegistry.Create(clay, dirt, sand);
            try
            {
                // 60% clay, 20% dirt, 20% sand: fails on a roll under 20%.
                var stack = new ItemStack(clayItem, 10, Composition.From(new Content(clay, 0.6f), new Content(dirt, 0.2f), new Content(sand, 0.2f)), 0, 0.6f);
                var removed = new List<Content>();
                ItemStack same = Refining.Pass(stack, registry, 1f, removed, 0.1f, 0f, out bool failed);
                Assert.IsTrue(failed);
                Assert.AreEqual(10f, same.Amount, 1e-3f);
                Assert.AreEqual(0, removed.Count);

                ItemStack refined = Refining.Pass(stack, registry, 1f, removed, 0.5f, 0.9f, out failed);
                Assert.IsFalse(failed);
                Assert.AreEqual(1, removed.Count, "One material at a time.");
                Assert.AreEqual(sand, removed[0].Material, "The pick chose the second impurity.");
                Assert.AreEqual(1f, removed[0].Fraction, 1e-3f);
                Assert.AreEqual(9f, refined.Amount, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(clay);
                Object.DestroyImmediate(dirt);
                Object.DestroyImmediate(sand);
                Object.DestroyImmediate(clayItem);
            }
        }

        [Test]
        public void ABucket_HoldsItsQualitysShare()
        {
            BucketDefinition bucket = BucketDefinition.CreateBucket(100);
            try
            {
                var poor = new ItemStack(bucket, 1, null, 0, 0.6f);
                Assert.AreEqual(60, bucket.CapacityOf(poor));
                Assert.AreEqual(60, bucket.WaterIn(bucket.WithWater(poor, 100)));
                Assert.AreEqual(100, bucket.CapacityOf(new ItemStack(bucket, 1)));
            }
            finally
            {
                Object.DestroyImmediate(bucket);
            }
        }
    }
}
