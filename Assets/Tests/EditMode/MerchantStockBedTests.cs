using System.Collections.Generic;
using System.Linq;
using Clube.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL797 (every shop sells stone and birch wood) and GL798 (sleeping in a keeper's bed costs a point, 0-100).</summary>
    public class MerchantStockBedTests
    {
        private const string Economy = "Assets/Config/Economy/";

        [Test]
        public void Offers_AddSoldEverywhere_OnceEach()
        {
            var own = ScriptableObject.CreateInstance<ItemDefinition>();
            var stone = ScriptableObject.CreateInstance<ItemDefinition>();
            var wood = ScriptableObject.CreateInstance<ItemDefinition>();
            PriceList list = PriceList.Create(new ItemPrice(own, 4), new ItemPrice(stone, 1, 10), new ItemPrice(wood, 5));
            list.SetSoldEverywhere(stone, wood);
            ShopSpecialty shop = ShopSpecialty.Create(ShopKind.Blacksmith, "Smith", new ItemDefinition[0], new[] { own, wood }, 1.5f).WithBasics();
            List<ItemPrice> offers = shop.Offers(list);
            Assert.AreEqual(3, offers.Count);
            Assert.AreEqual(1, offers.FindAll(o => o.Item == wood).Count);
            ItemPrice stoneOffer = offers.Find(o => o.Item == stone);
            Assert.AreEqual(2, stoneOffer.Coins);
            Assert.AreEqual(10, stoneOffer.Units);
            Assert.AreEqual(2, list.Scaled(0.5f).SoldEverywhere.Count);
        }

        [Test]
        public void OnlyTheMerchant_SellsStoneAndBirchWood_AtSanePrices()
        {
            var prices = AssetDatabase.LoadAssetAtPath<PriceList>(Economy + "Merchant prices.asset");
            var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Config/Items/Stone.asset");
            var birch = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Config/Items/Wood.asset");
            Assert.IsNotNull(prices);
            Assert.AreEqual("Birch wood", birch.DisplayName);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Config/Items/ItemCatalog.asset");
            var shops = new List<ShopSpecialty>();
            foreach (string guid in AssetDatabase.FindAssets("t:ShopSpecialty", new[] { Economy + "Shops" }))
            {
                shops.Add(AssetDatabase.LoadAssetAtPath<ShopSpecialty>(AssetDatabase.GUIDToAssetPath(guid)));
            }
            // MapGen's settlement shops (GL613) are made from the economy specialties.
            foreach (EconomySpecialty specialty in EconomySpecialty.Defaults())
            {
                shops.Add(specialty.ToShop(catalog));
            }
            Assert.Greater(shops.Count, 5);
            int merchants = 0;
            foreach (ShopSpecialty shop in shops)
            {
                List<ItemPrice> offers = shop.Offers(prices);
                bool isMerchant = shop.Kind == ShopKind.Wares && shop.BuysEverything;
                Assert.AreEqual(isMerchant, shop.SellsBasics, shop.ShopName);
                merchants += isMerchant ? 1 : 0;
                foreach (ItemDefinition item in new[] { stone, birch })
                {
                    ItemPrice offer = offers.Find(o => o.Item == item);
                    if (isMerchant)
                    {
                        Assert.IsNotNull(offer.Item, $"{shop.ShopName} doesn't sell {item.DisplayName}");
                        Assert.That(offer.Coins, Is.InRange(1, 20), $"{shop.ShopName}: {item.DisplayName} costs {offer.Coins}");
                    }
                    else if (!shop.Sells.Contains(item))
                    {
                        Assert.IsNull(offer.Item, $"{shop.ShopName} (not a Merchant) sells {item.DisplayName} (GL855)");
                    }
                }
            }
            Assert.Greater(merchants, 0);
        }

        [Test]
        public void LeatheryAndMiningShop_AddNothingToTheirOwnStock()
        {
            var prices = AssetDatabase.LoadAssetAtPath<PriceList>(Economy + "Merchant prices.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Config/Items/ItemCatalog.asset");
            var mining = AssetDatabase.LoadAssetAtPath<ShopSpecialty>(Economy + "Shops/Mining Shop.asset");
            ShopSpecialty leathery = EconomySpecialty.Defaults().Find(s => s.Id == "leathery").ToShop(catalog);
            foreach (ShopSpecialty shop in new[] { mining, leathery })
            {
                foreach (ItemPrice offer in shop.Offers(prices))
                {
                    Assert.IsTrue(shop.Sells.Contains(offer.Item), $"{shop.ShopName} sells {offer.Item.DisplayName}, which is not in its own stock");
                }
            }
        }

        [Test]
        public void SleptInBed_TakesOnePoint_NeverBelowZero()
        {
            Assert.AreEqual(1, Disposition.BedPenalty);
            var keeper = new Disposition(10, 3f);
            Assert.AreEqual(1, keeper.SleptInBed());
            Assert.AreEqual(9, keeper.Value);
            Assert.AreEqual(0f, keeper.Carried);
            var cold = new Disposition(0);
            Assert.AreEqual(0, cold.SleptInBed());
            Assert.AreEqual(0, cold.Value);
        }

        [Test]
        public void Disposition_StaysWithin0To100_OnEveryWrite()
        {
            Assert.AreEqual(0, new Disposition(-20).Value);
            Assert.AreEqual(100, new Disposition(250).Value);
            var keeper = new Disposition(99);
            keeper.Record(1e9f);
            Assert.AreEqual(100, keeper.Value);
            keeper.Lower(500);
            Assert.AreEqual(0, keeper.Value);
            keeper.Restore(-5, 0f);
            Assert.AreEqual(0, keeper.Value);
            keeper.Restore(101, 0f);
            Assert.AreEqual(100, keeper.Value);
        }

        [Test]
        public void BedPenalty_IsSaved()
        {
            var book = new DispositionBook();
            book.Of(2, 1).SleptInBed();
            var loaded = new DispositionBook();
            loaded.Restore(book.Save());
            Assert.AreEqual(Disposition.Start - Disposition.BedPenalty, loaded.Of(2, 1).Value);
        }
    }
}
