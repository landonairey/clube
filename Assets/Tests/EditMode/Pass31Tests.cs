using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The 2026-11-03 fixes (GL349-GL371): a station never loses a run it can't deliver, the flat rock's full repair, a whole
    /// stack's part going into a station, specks of a stack vanishing, and felled wood keeping its density.
    /// </summary>
    public class Pass31Tests
    {
        [Test]
        public void ARunWhoseResultDoesntFit_Waits_AndNothingIsLost()
        {
            // GL359: something else waits in the output (GL385: ingots of another quality now blend in), so the next run waits.
            ItemDefinition bun = ItemDefinition.Create("bronze-bun", "Bronze bun", 16);
            ItemDefinition ingot = ItemDefinition.Create("bronze-ingot", "Bronze ingot", 16);
            Recipe hammer = Recipe.Create(StationKind.FlatRock, new ItemAmount(ingot, 1), RecipeWork.Strikes, 2f, new ItemAmount(bun, 1));
            try
            {
                var station = new CraftingStation(StationKind.FlatRock, new[] { hammer });
                station.Output.Add(bun, 2, null, 0, 0.5f);
                station.Input.Add(bun, 4);
                for (int i = 0; i < 10; i++)
                {
                    station.Strike();
                }
                Assert.AreEqual(4, station.Input.Count(bun), "No bun was used up for an ingot with nowhere to go.");
                Assert.AreEqual(2, station.Output[0].Count);

                // Taken out, the work goes on and every bun becomes an ingot.
                var bag = new Inventory(4);
                station.Output.MoveTo(0, bag);
                for (int i = 0; i < 40 && station.Input.Count(bun) > 0; i++)
                {
                    station.Strike();
                    if (!station.Output[0].IsEmpty && station.Output[0].Count >= 16)
                    {
                        station.Output.MoveTo(0, bag);
                    }
                }
                Assert.AreEqual(0, station.Input.Count(bun));
                Assert.AreEqual(4, station.Output[0].Count + bag.Count(ingot), "Four buns, four new ingots.");
            }
            finally
            {
                Object.DestroyImmediate(hammer);
                Object.DestroyImmediate(bun);
                Object.DestroyImmediate(ingot);
            }
        }

        [Test]
        public void TheFlatRock_FullyRepairsATool_WithOneIngot()
        {
            ItemDefinition ingot = ItemDefinition.Create("bronze-ingot", "Bronze ingot", 16);
            ToolDefinition pick = ToolDefinition.Create("bronze-pick", "Bronze pick", 2, 1.5f).WithRepair(ingot, 0.5f, 0.1f);
            pick.WithDurability(300);
            try
            {
                var input = new Inventory(2);
                input.Set(0, new ItemStack(pick, 1, null, 250));
                input.Add(ingot, 3);
                Assert.IsTrue(ToolRepair.FindPair(input, out int toolSlot, out int itemSlot));
                Assert.IsTrue(ToolRepair.RepairFully(input, toolSlot, itemSlot));
                Assert.AreEqual(0, input[0].Wear, "Fully mended.");
                Assert.AreEqual(2, input[1].Count, "With one ingot.");
                Assert.AreEqual(1f - 250 * 0.1f / 300f, input[0].Quality, 1e-4f, "The usual share lost for good.");
            }
            finally
            {
                Object.DestroyImmediate(pick);
                Object.DestroyImmediate(ingot);
            }
        }

        [Test]
        public void LoadingARecipe_TakesTheWholeStack_PartAndAll()
        {
            // GL371: 6.07 gold buttons all go in.
            ItemDefinition button = ItemDefinition.Create("gold-button", "Gold button", 16);
            ItemDefinition ingot = ItemDefinition.Create("gold-ingot", "Gold ingot", 16);
            Recipe hammer = Recipe.Create(StationKind.Anvil, new ItemAmount(ingot, 1), RecipeWork.Strikes, 6f, new ItemAmount(button, 1));
            try
            {
                var bag = new Inventory(4);
                bag.Add(button, 6, null, 0, 1f, 0.07f);
                var station = new CraftingStation(StationKind.Anvil, new[] { hammer });
                station.LoadFor(hammer, bag);
                Assert.AreEqual(6.07f, station.Input[0].Amount + station.Input[1].Amount, 1e-3f);
                Assert.IsTrue(bag[0].IsEmpty, "Nothing left behind.");
            }
            finally
            {
                Object.DestroyImmediate(hammer);
                Object.DestroyImmediate(button);
                Object.DestroyImmediate(ingot);
            }
        }

        [Test]
        public void ASpeckOfAStack_IsNothing()
        {
            // GL363: less than 0.05 of an item, with none whole, would show as "0.0".
            ItemDefinition stone = ItemDefinition.Create("stone", "Stone", 1024);
            try
            {
                Assert.IsTrue(new ItemStack(stone, 0, null, 0, 1f, 0.03f).IsEmpty);
                Assert.IsFalse(new ItemStack(stone, 0, null, 0, 1f, 0.07f).IsEmpty);
                Assert.AreEqual(0.03f, new ItemStack(stone, 2, null, 0, 1f, 0.03f).Part, 1e-4f, "Beside whole items a small part stays.");
            }
            finally
            {
                Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void LandedWood_KeepsItsDensity_AndLeavesLandAsTheirFallenKind()
        {
            VoxelMaterial wood = VoxelMaterial.Create(11, "Wood");
            VoxelMaterial fallen = VoxelMaterial.Create(26, "Fallen leaves");
            VoxelMaterial leaves = VoxelMaterial.Create(12, "Leaves").WithFallenForm(fallen);
            VoxelMaterial dirt = VoxelMaterial.Create(1, "Dirt");
            var world = new World(new Vector3Int(16, 16, 16), 1f);
            world.Load(Vector3Int.zero, null);
            try
            {
                var trees = new TreeMaterials(new[] { wood }, new[] { leaves });
                Assert.AreEqual(fallen.Id, trees.FallenFor(leaves.Id));
                Assert.IsTrue(trees.IsLeaves(fallen.Id), "Fallen leaves are leaves to felling.");
                for (int z = 0; z <= 16; z++)
                {
                    for (int x = 0; x <= 16; x++)
                    {
                        Set(world, new Vector3Int(x, 0, z), 1f, dirt.Id);
                    }
                }
                // A trunk of solid wood with a softer bark sample, and a leaf on top; cut at y = 2.
                for (int y = 1; y <= 6; y++)
                {
                    Set(world, new Vector3Int(4, y, 8), y == 4 ? 0.62f : 1f, wood.Id);
                }
                Set(world, new Vector3Int(4, 7, 8), 1f, leaves.Id);
                world.SetDensity(new Vector3Int(4, 2, 8), 0f);
                FelledLimb limb = TreeFelling.FindSevered(world, new Vector3Int(4, 2, 8), new Vector3Int(4, 2, 8), trees, 0.5f);
                Assert.NotNull(limb);
                TreeFelling.Remove(world, limb);
                var landedWood = new System.Collections.Generic.List<Vector3Int>();
                var landedLeaves = new System.Collections.Generic.List<Vector3Int>();
                TreeFelling.Land(world, limb, new Vector3Int(1, 0, 0), trees, 0.5f, leafPacking: 1f, landedWood: landedWood, landedLeaves: landedLeaves);
                Assert.AreEqual(4, landedWood.Count);
                bool softKept = false;
                foreach (Vector3Int sample in landedWood)
                {
                    softKept |= Mathf.Abs((world.GetDensity(sample) ?? 0f) - 0.62f) < 0.01f;
                }
                Assert.IsTrue(softKept, "The softer wood sample lands as soft as it stood, so it chops as easily (GL349).");
                Assert.AreEqual(1, landedLeaves.Count);
                Assert.AreEqual(fallen.Id, world.GetMaterial(landedLeaves[0]), "Leaves land as fallen leaves (GL350).");

                // Lying down, cut again: a straight drop puts it back as it lies, no turning.
                Vector3Int before = landedWood[0];
                Assert.AreEqual(before, before + TreeFelling.Turn(Vector3Int.zero, Vector3Int.zero));
            }
            finally
            {
                Object.DestroyImmediate(wood);
                Object.DestroyImmediate(fallen);
                Object.DestroyImmediate(leaves);
                Object.DestroyImmediate(dirt);
            }
        }

        private static void Set(World world, Vector3Int sample, float density, byte material)
        {
            world.SetDensity(sample, density);
            world.SetMaterial(sample, material);
        }
    }
}
