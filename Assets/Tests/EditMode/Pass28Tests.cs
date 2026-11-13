using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL286-GL290: gathering by the mesh's volume, mending tools, torches and the xp curve.
    public class Pass28Tests
    {
        private const float Iso = 0.5f;

        [Test]
        public void LoweringASampleDeepInside_ChangesNoVolume()
        {
            World world = SlabWorld();
            var region = new RegionVolume();
            var sample = new[] { new Vector3Int(2, 0, 2) };
            float before = region.SolidAround(world, sample, Iso);
            world.SetDensity(sample[0], 0.6f);
            Assert.AreEqual(before, region.SolidAround(world, sample, Iso), 1e-5f,
                "Still solid, so the surface hasn't moved: nothing is gathered (GL288).");
        }

        [Test]
        public void LoweringASurfaceSample_TakesPartOfAVoxel()
        {
            World world = SlabWorld();
            var region = new RegionVolume();
            var sample = new[] { new Vector3Int(2, 1, 2) };
            float before = region.SolidAround(world, sample, Iso);
            world.SetDensity(sample[0], 0.55f);
            float taken = before - region.SolidAround(world, sample, Iso);
            Assert.Greater(taken, 0f);
            Assert.Less(taken, 1f, "A small move of the surface is less than a voxel's volume.");
        }

        [Test]
        public void ClearingASurfaceSample_TakesAboutAVoxel()
        {
            World world = SlabWorld();
            var region = new RegionVolume();
            var sample = new[] { new Vector3Int(2, 1, 2) };
            float before = region.SolidAround(world, sample, Iso);
            world.SetDensity(sample[0], 0f);
            float taken = before - region.SolidAround(world, sample, Iso);
            Assert.That(taken, Is.InRange(0.3f, 1.2f));
        }

        [Test]
        public void ARepair_MendsHalf_AndLosesSomeForGood()
        {
            ItemDefinition ingot = ItemDefinition.Create("copper-ingot", "Copper ingot", 64);
            ToolDefinition pick = ToolDefinition.Create("copper-pick", "Copper pick", 2, 1.5f).WithRepair(ingot, 0.5f, 0.25f);
            pick.WithDurability(200);
            var worn = new ItemStack(pick, 1, null, 150);
            Assert.IsTrue(ToolRepair.CanRepair(worn, ingot));
            ItemStack mended = ToolRepair.Repaired(worn);
            Assert.AreEqual(50, mended.Wear, "Half of 200 hits back.");
            Assert.AreEqual(1f - 100 * 0.25f / 200f, mended.Quality, 1e-4f, "A quarter of the mended hits are lost for good.");
            Assert.Less(mended.MaxHits, 200, "It can never be mended back to full (GL286).");
        }

        [Test]
        public void ToolsWithoutARepairItem_CantBeMended()
        {
            ItemDefinition ingot = ItemDefinition.Create("copper-ingot", "Copper ingot", 64);
            ToolDefinition stonePick = ToolDefinition.Create("stone-pick", "Stone pick", 1, 1.5f);
            stonePick.WithDurability(120);
            Assert.IsFalse(ToolRepair.CanRepair(new ItemStack(stonePick, 1, null, 60), ingot));
        }

        [Test]
        public void RepairingInAnInventory_UsesIngotsUntilWhole()
        {
            ItemDefinition ingot = ItemDefinition.Create("copper-ingot", "Copper ingot", 64);
            ToolDefinition pick = ToolDefinition.Create("copper-pick", "Copper pick", 2, 1.5f).WithRepair(ingot, 0.5f, 0.25f);
            pick.WithDurability(200);
            var items = new Inventory(4);
            items.Set(0, new ItemStack(pick, 1, null, 150));
            items.Add(ingot, 5);
            int used = ToolRepair.Repair(items, 0, items, 1);
            Assert.AreEqual(2, used, "150 worn: two ingots (100 each) mend it.");
            Assert.AreEqual(0, items[0].Wear);
            Assert.AreEqual(3, items[1].Count);
        }

        [Test]
        public void ALitTorch_SellsForWhatIsLeftOfIt()
        {
            TorchDefinition torch = TorchDefinition.CreatePair(1200f);
            TorchDefinition lit = torch.Lit;
            Assert.IsTrue(lit.IsLit);
            Assert.AreSame(torch, lit.Unlit);
            var prices = PriceList.Create(new ItemPrice { Item = torch, Coins = 8, Units = 1 });
            Assert.AreEqual(8f, prices.PriceOf(new ItemStack(torch, 1), 1), 1e-4f);
            Assert.AreEqual(2f, prices.PriceOf(new ItemStack(lit, 1, null, 900), 1), 1e-4f, "A quarter of its burning left.");
        }

        [Test]
        public void Levels_CostMoreThanBefore()
        {
            Assert.AreEqual(300f, SkillSet.ExperienceFor(1), 1e-3f, "Ten times GL289's 30 (GL629).");
            Assert.AreEqual(780f, SkillSet.ExperienceFor(2), 1e-2f, "The second level costs 60% more (GL289).");
        }

        // One 4-voxel chunk of dirt up to y = 1, air above.
        private static World SlabWorld()
        {
            var world = new World(new Vector3Int(4, 4, 4), 1f);
            world.Load(Vector3Int.zero, null);
            for (int z = 0; z <= 4; z++)
            {
                for (int y = 0; y <= 4; y++)
                {
                    for (int x = 0; x <= 4; x++)
                    {
                        world.SetDensity(new Vector3Int(x, y, z), y <= 1 ? 1f : 0f);
                    }
                }
            }
            return world;
        }
    }
}
