using System.Collections.Generic;
using Clube.Core;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The trading post (GL470-GL475): its name, the keepers' disposition, the open pit and the cave.</summary>
    public class TradingPostTests
    {
        [Test]
        public void SettlementNames_SameSeedAndParcel_SameName()
        {
            Assert.AreEqual(SettlementNames.Generate(1234, new Vector2Int(3, -2)), SettlementNames.Generate(1234, new Vector2Int(3, -2)));
        }

        [Test]
        public void SettlementNames_AreCapitalisedWordsAndVary()
        {
            var names = new HashSet<string>();
            for (int i = 0; i < 50; i++)
            {
                string name = SettlementNames.Generate(i * 7919, new Vector2Int(i, -i));
                Assert.IsFalse(string.IsNullOrEmpty(name));
                Assert.IsTrue(char.IsUpper(name[0]), name);
                Assert.IsFalse(name.Contains(" "), name);
                names.Add(name);
            }
            // Fifty settlements get many different names, not a handful.
            Assert.Greater(names.Count, 35);
        }

        [Test]
        public void SettlementNames_OtherParcelOfOneWorld_UsuallyAnotherName()
        {
            int differ = 0;
            for (int i = 0; i < 20; i++)
            {
                if (SettlementNames.Generate(99, new Vector2Int(i, 0)) != SettlementNames.Generate(99, new Vector2Int(i, 10)))
                {
                    differ++;
                }
            }
            Assert.Greater(differ, 15);
        }

        [Test]
        public void Disposition_StartsAtTen_RisesOnePointPerHundredCoins()
        {
            var disposition = new Disposition();
            Assert.AreEqual(10, disposition.Value);
            Assert.AreEqual(0, disposition.Record(99f));
            Assert.AreEqual(1, disposition.Record(1f));
            Assert.AreEqual(11, disposition.Value);
            Assert.AreEqual(3, disposition.Record(350f));
            Assert.AreEqual(14, disposition.Value);
            Assert.AreEqual(50f, disposition.Carried, 1e-3f);
        }

        [Test]
        public void Disposition_StopsAtHundred()
        {
            var disposition = new Disposition(98);
            Assert.AreEqual(2, disposition.Record(100000f));
            Assert.AreEqual(100, disposition.Value);
            Assert.AreEqual(0f, disposition.Carried);
            Assert.AreEqual(0, disposition.Record(500f));
        }

        [Test]
        public void Disposition_EachPointUnderHundred_IsAPointOfMarkupAndAPointOffWhatTheyPay()
        {
            var cold = new Disposition(10);
            Assert.AreEqual(190, cold.SellingPrice(100));
            Assert.AreEqual(0.1f, cold.PayShare, 1e-5f);
            var fair = new Disposition(100);
            Assert.AreEqual(100, fair.SellingPrice(100));
            Assert.AreEqual(1f, fair.PayShare, 1e-5f);
            var warm = new Disposition(75);
            Assert.AreEqual(63, warm.SellingPrice(50)); // 62.5 rounds up
            Assert.AreEqual(0.75f, warm.PayShare, 1e-5f);
        }

        [Test]
        public void PriceList_Scaled_PaysItsShare()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            PriceList list = PriceList.Create(new ItemPrice(item, 40));
            PriceList scaled = list.Scaled(0.25f);
            Assert.IsTrue(scaled.TryGetPrice(item, out int coins));
            Assert.AreEqual(40, coins);
            var inventory = new Inventory(4);
            inventory.Add(item, 2);
            var wallet = new Wallet();
            Assert.AreEqual(2, scaled.Sell(item, 2, inventory, wallet));
            Assert.AreEqual(20f, wallet.Coins, 1e-3f);
            Assert.AreEqual(1f, list.Share);
        }

        [Test]
        public void PropertySale_OfferedOnlyWithThePrice_ThenPaysEachDay()
        {
            var sale = new PropertySale(10000, 500);
            Assert.IsFalse(sale.IsOffered(9999f));
            Assert.IsTrue(sale.IsOffered(10000f));
            var wallet = new Wallet(12000f);
            Assert.IsTrue(sale.TryBuy(wallet, 4));
            Assert.AreEqual(2000f, wallet.Coins, 1e-3f);
            Assert.IsFalse(sale.IsOffered(50000f));
            Assert.AreEqual(0, sale.Collect(4));
            Assert.AreEqual(500, sale.Collect(5));
            Assert.AreEqual(1500, sale.Collect(8));
            Assert.AreEqual(0, sale.Collect(8));
        }

        [Test]
        public void PropertySale_ShortWallet_ChangesNothing()
        {
            var sale = new PropertySale(10000, 500);
            var wallet = new Wallet(500f);
            Assert.IsFalse(sale.TryBuy(wallet, 1));
            Assert.AreEqual(500f, wallet.Coins);
            Assert.IsFalse(sale.Owned);
            Assert.AreEqual(0, sale.Collect(9));
        }

        [Test]
        public void LandParcels_Unlock_LetsALockedParcelBeOwned()
        {
            var parcels = new LandParcels(32f, 100, 100);
            var town = new Vector2Int(2, 3);
            parcels.Lock(town);
            Assert.IsFalse(parcels.Grant(town));
            Assert.IsTrue(parcels.Unlock(town));
            Assert.IsFalse(parcels.Unlock(town));
            Assert.IsTrue(parcels.Grant(town));
            Assert.IsTrue(parcels.IsOwned(town));
        }

        [Test]
        public void OpenPit_Cone_FloorRadiusThenFortyFiveDegreeWalls()
        {
            var centre = new Vector2(10f, 10f);
            Assert.IsTrue(OpenPit.Contains(new Vector3(11f, 5f, 10f), centre, 5f));
            Assert.IsFalse(OpenPit.Contains(new Vector3(12f, 5f, 10f), centre, 5f));
            Assert.IsTrue(OpenPit.Contains(new Vector3(12f, 5.6f, 10f), centre, 5f));
            Assert.IsFalse(OpenPit.Contains(new Vector3(10f, 4.9f, 10f), centre, 5f));
            Assert.AreEqual(4.5f, OpenPit.RimRadius(3f), 1e-5f);
        }

        [Test]
        public void OpenPit_DigsTopAndMiddleFirst_AndCountsOre()
        {
            // A 16 m block of ground up to y = 6 m with ore at the floor, a pit 2 m deep in its middle.
            var world = new World(new Vector3Int(16, 16, 16), 1f);
            world.Load(Vector3Int.zero, null);
            for (int z = 0; z <= 16; z++)
            {
                for (int y = 0; y <= 16; y++)
                {
                    for (int x = 0; x <= 16; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        world.SetDensity(sample, y <= 6 ? 1f : 0f);
                        world.SetMaterial(sample, (byte)(y <= 4 ? 7 : 1));
                    }
                }
            }
            var centre = new Vector2(8f, 8f);
            Queue<Vector3Int> order = OpenPit.Plan(world, centre, 4f, 7f, 0.5f);
            Assert.Greater(order.Count, 0);
            Assert.AreEqual(new Vector3Int(8, 6, 8), order.Peek());
            Assert.AreEqual(0, OpenPit.Dig(world, order, 1, 0.5f, new HashSet<byte> { 7 }));
            Assert.Less(world.GetDensity(new Vector3Int(8, 6, 8)) ?? 1f, 0.5f);
            // Dug out to the end, the floor's ore comes up and nothing outside the cone is touched.
            int ore = OpenPit.Dig(world, order, 10000, 0.5f, new HashSet<byte> { 7 });
            Assert.Greater(ore, 0);
            Assert.AreEqual(0, order.Count);
            Assert.GreaterOrEqual(world.GetDensity(new Vector3Int(8, 3, 8)) ?? 0f, 0.5f);
            Assert.GreaterOrEqual(world.GetDensity(new Vector3Int(14, 6, 8)) ?? 0f, 0.5f);
        }

        [Test]
        public void TownLayout_PitClear_OutsideTheWallOrSmallInAFreeSpot()
        {
            Assert.IsTrue(TownLayout.IsClearForPit(new Vector2(TownLayout.OuterHalf + 5f, 0f), 3f));
            Assert.IsFalse(TownLayout.IsClearForPit(new Vector2(TownLayout.OuterHalf + 1f, 0f), 3f));
            // On the way up to the gate.
            Assert.IsFalse(TownLayout.IsClearForPit(new Vector2(0f, -TownLayout.OuterHalf - 5f), 3f));
            // On a shop.
            Assert.IsFalse(TownLayout.IsClearForPit(TownLayout.PlotOf(ShopKind.Blacksmith).Centre, 2f));
        }

        [Test]
        public void TownLayout_ShaftHeads_KeepOffPits()
        {
            var pit = new Vector2(TownLayout.OuterHalf + 6f, 4f);
            List<Vector2> heads = TownLayout.ShaftHeads(new List<Vector2> { pit }, new List<(Vector2, float)> { (pit, 4f) });
            Assert.AreEqual(1, heads.Count);
            Assert.GreaterOrEqual((heads[0] - pit).magnitude, 4f + TownLayout.ShaftHeadRadius + 1f - 1e-3f);
        }

        // A town on gently sloping land, its tin node near the surface outside the wall and three deep ones.
        private static TownPlan PlanOnSlope(Vector3[] nodes)
        {
            var distances = new float[nodes.Length];
            for (int i = 0; i < distances.Length; i++)
            {
                distances[i] = 20f;
            }
            var site = new TownPlacement(new Vector2(100f, 100f), 40f, 1f, 1f, 50f, distances, nodes);
            return new TownPlan(site, 0f, (x, z) => 40f + x * 0.02f, 7);
        }

        [Test]
        public void TownPlan_TinNodeNearTheSurface_IsAPit_DeepOnesShafts()
        {
            TownPlan plan = PlanOnSlope(new[]
            {
                new Vector3(100f + TownLayout.OuterHalf + 8f, 39f, 100f), new Vector3(90f, 20f, 90f),
                new Vector3(110f, 22f, 92f), new Vector3(95f, 18f, 108f),
            });
            TownShaft tin = plan.Shafts.Find(s => s.Group == 0);
            Assert.IsNotNull(tin);
            Assert.IsTrue(tin.IsPit);
            Assert.AreEqual(1, tin.Path.Count);
            Assert.AreEqual(38f, tin.PitFloor, 1e-3f);
            Assert.IsFalse(plan.Shafts.Find(s => s.Group == 1).IsPit);
        }

        [Test]
        public void TownPlan_DeepTinNode_GetsAStairShaft()
        {
            TownPlan plan = PlanOnSlope(new[] { new Vector3(100f + TownLayout.OuterHalf + 8f, 20f, 100f) });
            Assert.IsFalse(plan.Shafts[0].IsPit);
            Assert.Greater(plan.Shafts[0].Path.Count, 2);
        }

        [Test]
        public void TownCave_SealedUnderground_WithTunnelsToTheFourParcelFaces()
        {
            TownPlan plan = PlanOnSlope(new Vector3[0]);
            TownCave cave = plan.Cave;
            Assert.IsNotNull(cave);
            Vector2 room = plan.ToTown(new Vector2(cave.Room.x, cave.Room.z));
            Assert.IsTrue(TownLayout.IsInside(room));
            // Ground left over the ceiling.
            Assert.LessOrEqual(cave.Room.y + TownCave.RoomHeight + TownCave.Cover, plan.GroundAt(room) + 1e-3f);
            Assert.AreEqual(4, cave.Tunnels.Count);
            var reached = new HashSet<Vector2Int>();
            foreach (List<Vector3> tunnel in cave.Tunnels)
            {
                Vector3 end = tunnel[tunnel.Count - 1];
                Vector2 endTown = plan.ToTown(new Vector2(end.x, end.z));
                // Level, under the ground all along, and ending at a face of the parcel.
                Assert.AreEqual(cave.Room.y, end.y, 1e-3f);
                Assert.LessOrEqual(end.y + TownStructures.ShaftHeight + TownCave.Cover, plan.GroundAt(endTown) + 1e-3f);
                Assert.AreEqual(TownLayout.ParcelSize * 0.5f, Mathf.Max(Mathf.Abs(endTown.x), Mathf.Abs(endTown.y)) + TownStructures.ShaftWidth * 0.5f, 1e-2f);
                reached.Add(new Vector2Int(Mathf.RoundToInt(Mathf.Sign(Mathf.Abs(endTown.x) > Mathf.Abs(endTown.y) ? endTown.x : 0f)), Mathf.RoundToInt(Mathf.Sign(Mathf.Abs(endTown.y) >= Mathf.Abs(endTown.x) ? endTown.y : 0f))));
            }
            Assert.AreEqual(4, reached.Count);
        }

        [Test]
        public void TownCave_KeepsClearOfAShaftAtTheBack()
        {
            // A deep node behind the town draws a shaft head to the back; the cave goes to a side.
            TownPlan plan = PlanOnSlope(new[] { new Vector3(100f, 10f, 100f + TownLayout.InnerHalf - 4f) });
            Assert.IsNotNull(plan.Cave);
            Vector2 room = plan.ToTown(new Vector2(plan.Cave.Room.x, plan.Cave.Room.z));
            foreach (TownShaft shaft in plan.Shafts)
            {
                Assert.Greater((shaft.Head - room).magnitude, TownCave.RoomRadius + TownLayout.ShaftHeadRadius);
            }
        }

        [Test]
        public void Disposition_Restore_ClampsAndRaisesChanged()
        {
            var disposition = new Disposition();
            int changes = 0;
            disposition.Changed += () => changes++;
            disposition.Restore(140, 50f);
            Assert.AreEqual(100, disposition.Value);
            Assert.AreEqual(0f, disposition.Carried);
            Assert.AreEqual(1, changes);
        }
    }
}
