using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL610, GL611: what an item drops as is its definition's, and dropped items survive a save as text.
    public class DroppedItemTests
    {
        [Test]
        public void DropForm_ComesFromTheKindOfItem_UnlessTheAssetSays()
        {
            Assert.AreEqual(DropForm.Pile, ItemDefinition.Create("stone", "Stone").DropsAs);
            Assert.AreEqual(DropForm.Model, ToolDefinition.Create("pick", "Bronze pick", 1, 1f).DropsAs);
            Assert.AreEqual(DropForm.Model, BucketDefinition.CreateBucket().DropsAs);
            Assert.AreEqual(DropForm.Bag, PlaceableDefinition.Create("campfire", "Campfire").DropsAs);
            // A torch is a placeable, but lies on the ground as itself.
            Assert.AreEqual(DropForm.Model, TorchDefinition.CreatePair().DropsAs);
            // An ingot is big enough to lie as itself, not in a bag (GL684).
            Assert.AreEqual(DropForm.Model, ItemDefinition.Create("iron-ingot", "Iron ingot").WithIcon(ItemIconShape.Ingot).DropsAs);
            // Very small things are bagged by their asset.
            Assert.AreEqual(DropForm.Bag, ItemDefinition.Create("pearl", "Pearl").WithDropForm(DropForm.Bag).DropsAs);
            Assert.AreEqual(DropForm.Bag, ToolDefinition.Create("needle", "Needle", 1, 1f).WithDropForm(DropForm.Bag).DropsAs);
        }

        [Test]
        public void DroppedItems_ReadBackAsWritten()
        {
            var items = new List<DroppedSave>
            {
                new DroppedSave
                {
                    Stack = new StackSave { Item = "bronze-pick", Count = 1, Wear = 37, Quality = 1.25f },
                    Position = new Vector3(12.5f, -3.25f, 1024.125f),
                    Rotation = Quaternion.Euler(10f, 200f, 85f),
                },
                new DroppedSave
                {
                    Stack = new StackSave
                    {
                        Item = "campfire", Count = 3, Part = 0.5f, Quality = 0.8f,
                        Contents = new List<ContentSave> { new ContentSave { Material = 4, Fraction = 0.75f }, new ContentSave { Material = 19, Fraction = 0.25f } },
                    },
                    Position = Vector3.one,
                    Rotation = Quaternion.identity,
                },
            };

            List<DroppedSave> read = DroppedItemSaving.Read(DroppedItemSaving.Write(items));

            Assert.AreEqual(2, read.Count);
            Assert.AreEqual("bronze-pick", read[0].Stack.Item);
            Assert.AreEqual(1, read[0].Stack.Count);
            Assert.AreEqual(37, read[0].Stack.Wear);
            Assert.AreEqual(1.25f, read[0].Stack.Quality);
            Assert.AreEqual(items[0].Position, read[0].Position);
            Assert.AreEqual(items[0].Rotation, read[0].Rotation);
            Assert.IsNull(read[0].Stack.Contents);
            Assert.AreEqual(3, read[1].Stack.Count);
            Assert.AreEqual(0.5f, read[1].Stack.Part);
            Assert.AreEqual(2, read[1].Stack.Contents.Count);
            Assert.AreEqual(19, read[1].Stack.Contents[1].Material);
            Assert.AreEqual(0.25f, read[1].Stack.Contents[1].Fraction);
        }

        [Test]
        public void DroppedItems_ReadTheSame_WhereACommaIsTheDecimalMark()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var item = new DroppedSave { Stack = new StackSave { Item = "spear", Count = 1, Quality = 1.5f }, Position = new Vector3(0.5f, 1.5f, 2.5f), Rotation = Quaternion.identity };
                List<DroppedSave> read = DroppedItemSaving.Read(DroppedItemSaving.Write(new[] { item }));
                Assert.AreEqual(1, read.Count);
                Assert.AreEqual(item.Position, read[0].Position);
                Assert.AreEqual(1.5f, read[0].Stack.Quality);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [Test]
        public void DroppedItems_SkipLinesThatDoNotRead_AndEmptyStacks()
        {
            var good = new DroppedSave { Stack = new StackSave { Item = "spear", Count = 1, Quality = 1f }, Rotation = Quaternion.identity };
            string text = "nonsense\n" + DroppedItemSaving.Write(new[] { good, default }) + "\nspear|x|0|0|1|0|0|0|0|0|0|1|";

            List<DroppedSave> read = DroppedItemSaving.Read(text);

            Assert.AreEqual(1, read.Count);
            Assert.AreEqual("spear", read[0].Stack.Item);
            Assert.AreEqual(0, DroppedItemSaving.Read(null).Count);
            Assert.AreEqual(0, DroppedItemSaving.Read(string.Empty).Count);
        }

        [Test]
        public void DroppedItems_KeepWhetherBaggedAndHowManySacks()
        {
            var items = new List<DroppedSave>
            {
                new DroppedSave { Stack = new StackSave { Item = "hand-axe", Count = 1 }, IsBag = true, Sacks = 107, Rotation = Quaternion.identity },
                new DroppedSave { Stack = new StackSave { Item = "hand-axe", Count = 1 }, IsBag = false, Rotation = Quaternion.identity },
            };
            List<DroppedSave> read = DroppedItemSaving.Read(DroppedItemSaving.Write(items));
            Assert.IsTrue(read[0].BagKnown && read[0].IsBag);
            Assert.AreEqual(107, read[0].Sacks);
            Assert.IsTrue(read[1].BagKnown && !read[1].IsBag);
            Assert.AreEqual(1, read[1].Sacks);
            // A line from before the bag was kept: unknown.
            string old = DroppedItemSaving.Write(new[] { items[1] });
            old = old.Substring(0, old.IndexOf("|m|", System.StringComparison.Ordinal) + 0);
            Assert.IsFalse(DroppedItemSaving.Read(old)[0].BagKnown);
        }

        [Test]
        public void SackPiles_MergeOnlyMoreThanTenIdenticalSacksClose()
        {
            ItemDefinition axe = ToolDefinition.Create("hand-axe", "Hand axe", 1, 1f);
            ItemDefinition stone = ItemDefinition.Create("stone", "Stone");
            var stacks = new List<ItemStack>();
            var at = new List<Vector3>();
            var sacks = new List<int>();
            for (int i = 0; i < 11; i++)
            {
                stacks.Add(new ItemStack(axe, 1));
                at.Add(new Vector3(i * 0.2f, 0f, 0f));
                sacks.Add(1);
            }
            stacks.Add(new ItemStack(stone, 5));
            at.Add(Vector3.zero);
            sacks.Add(1);
            stacks.Add(new ItemStack(axe, 1));
            at.Add(new Vector3(50f, 0f, 0f));
            sacks.Add(1);
            List<List<int>> plan = SackPiles.Plan(stacks, at, sacks);
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(11, plan[0].Count);

            // Ten are not over the threshold.
            stacks.RemoveRange(10, 3);
            at.RemoveRange(10, 3);
            sacks.RemoveRange(10, 3);
            Assert.AreEqual(0, SackPiles.Plan(stacks, at, sacks).Count);
        }

        [Test]
        public void SackPiles_FiftySacksDroppedOneByOne_EndAsOneStack()
        {
            // GL857: fifty axes crafted with a full inventory, each dropped in its own sack, the heap spreading as it grows
            // (sacks tumble off each other, the player turns): the last lie 4.2 m from the first, past Radius, yet one pile. Merged the way
            // DroppedItems does it, after every drop.
            ItemDefinition axe = ToolDefinition.Create("hand-axe", "Hand axe", 1, 1f);
            var stacks = new List<ItemStack>();
            var at = new List<Vector3>();
            var sacks = new List<int>();
            for (int drop = 0; drop < 50; drop++)
            {
                float turn = drop * 2.4f;
                float reach = 0.15f * Mathf.Sqrt(drop) * 4f;
                stacks.Add(new ItemStack(axe, 1));
                at.Add(new Vector3(Mathf.Cos(turn) * reach, 0f, Mathf.Sin(turn) * reach));
                sacks.Add(1);
                List<List<int>> plan = SackPiles.Plan(stacks, at, sacks);
                var gone = new List<int>();
                foreach (List<int> group in plan)
                {
                    for (int i = 1; i < group.Count; i++)
                    {
                        sacks[group[0]] += sacks[group[i]];
                        gone.Add(group[i]);
                    }
                }
                gone.Sort();
                for (int i = gone.Count - 1; i >= 0; i--)
                {
                    stacks.RemoveAt(gone[i]);
                    at.RemoveAt(gone[i]);
                    sacks.RemoveAt(gone[i]);
                }
            }
            Assert.AreEqual(1, stacks.Count);
            Assert.AreEqual(50, sacks[0]);
        }
    }
}
