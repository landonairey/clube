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
    }
}
