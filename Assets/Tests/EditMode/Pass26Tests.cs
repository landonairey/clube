using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL245-GL250, GL256: how stacks mix (gathering and dropping by hand blend; out of a station keeps apart), part
    // items sold, the chest's size, the sun and moon's paths, and birds that leave.
    public class Pass26Tests
    {
        [Test]
        public void Digging_BlendsIntoTheStackItGoesTo()
        {
            // GL256 (replacing GL245's tolerance): what's gathered goes onto the stack and mixes, whatever its purity.
            VoxelMaterial clay = VoxelMaterial.Create(13, "Clay");
            VoxelMaterial dirt = VoxelMaterial.Create(1, "Dirt");
            ItemDefinition item = ItemDefinition.Create("clay", "Clay", 1024);
            var items = new Inventory(4);
            items.Add(item, 10, Composition.From(new Content(clay, 1f)), 0, 1f);
            items.Add(item, 10, Composition.From(new Content(clay, 0.55f), new Content(dirt, 0.45f)), 0, 0.55f);
            Assert.AreEqual(20, items[0].Count);
            Assert.AreEqual(0.775f, items[0].Quality, 1e-3f);
            Assert.IsTrue(items[1].IsEmpty);
        }

        [Test]
        public void DroppingAStackOnAnother_CombinesThem_AtAnyQuality()
        {
            ItemDefinition sticks = ItemDefinition.Create("stick", "Stick", 64);
            var items = new Inventory(3);
            items.Add(sticks, 4, null, 0, 1.4f);
            items.Add(sticks, 4, null, 0, 1f);
            Assert.AreEqual(4, items[0].Count, "Crafted items of different quality don't stack by themselves.");
            items.Move(1, 0);
            Assert.AreEqual(8, items[0].Count, "Dropped on it by hand, they join.");
            Assert.AreEqual(1.2f, items[0].Quality, 1e-3f);
            Assert.IsTrue(items[1].IsEmpty);

            items.Add(sticks, 6, null, 0, 0.5f);
            Assert.AreEqual(3, items.Move(1, 0, 3), "Half a stack dragged on joins too.");
            Assert.AreEqual(11, items[0].Count);
        }

        [Test]
        public void PullingOutOfAStation_KeepsQualitiesApart()
        {
            VoxelMaterial clay = VoxelMaterial.Create(13, "Clay");
            VoxelMaterial dirt = VoxelMaterial.Create(1, "Dirt");
            ItemDefinition item = ItemDefinition.Create("clay", "Clay", 1024);
            Composition pure = Composition.From(new Content(clay, 1f));
            Composition dirty = Composition.From(new Content(clay, 0.55f), new Content(dirt, 0.45f));
            var player = new Inventory(3);
            player.Add(item, 30, dirty, 0, 0.55f);
            var station = new Inventory(2);
            station.Add(item, 5, pure, 0, 1f);

            Assert.AreEqual(5, station.MoveTo(0, player, keepApart: true));
            Assert.AreEqual(30, player[0].Count, "The dirty stack took nothing in.");
            Assert.AreEqual(0.55f, player[0].Quality, 1e-4f);
            Assert.AreEqual(5, player[1].Count, "The refined clay came out on its own.");
            Assert.AreEqual(1f, player[1].Quality, 1e-4f);

            station.Add(item, 3, pure, 0, 1f);
            station.MoveTo(0, player, keepApart: true);
            Assert.AreEqual(8, player[1].Count, "It joins a stack of the same quality.");
        }

        [Test]
        public void LoadingAStation_TakesTheBestStackFirst()
        {
            ItemDefinition item = ItemDefinition.Create("clay", "Clay", 1024);
            VoxelMaterial clay = VoxelMaterial.Create(13, "Clay");
            VoxelMaterial dirt = VoxelMaterial.Create(1, "Dirt");
            var from = new Inventory(3);
            from.Add(item, 20, Composition.From(new Content(clay, 0.55f), new Content(dirt, 0.45f)), 0, 0.55f);
            from.Add(item, 20, Composition.From(new Content(clay, 1f)), 0, 1f);
            var to = new Inventory(2);
            Assert.AreEqual(5, from.MoveItemTo(item, 5, to));
            Assert.AreEqual(1f, to[0].Quality, 1e-4f, "The refined clay went in.");
            Assert.AreEqual(15, from[1].Count);
            Assert.AreEqual(20, from[0].Count);
        }

        [Test]
        public void RemoveAmount_TakesAPartItem()
        {
            ItemDefinition item = ItemDefinition.Create("clay", "Clay", 1024);
            var items = new Inventory(1);
            items.Add(item, 0, null, 0, 0.46f, 0.99f);
            Assert.AreEqual(0.99f, items[0].Amount, 1e-3f);
            Assert.AreEqual(0.4f, items.RemoveAmount(0, 0.4f), 1e-4f);
            Assert.AreEqual(0.59f, items[0].Amount, 1e-3f);
            Assert.AreEqual(0.46f, items[0].Quality, 1e-3f, "What's left keeps its quality.");
            items.RemoveAmount(0, 5f);
            Assert.IsTrue(items[0].IsEmpty);

            items.Add(item, 3, null, 0, 1f, 0.5f);
            items.RemoveAmount(0, 1.25f);
            Assert.AreEqual(2.25f, items[0].Amount, 1e-3f);
        }

        [Test]
        public void ChestSlots_FollowTheWoodsQuality_DownToAFloor()
        {
            Assert.AreEqual(27, StorageCapacity.SlotsFor(1f, 27, 5));
            Assert.AreEqual(27, StorageCapacity.SlotsFor(1.4f, 27, 5), "Better than pure: still the full size.");
            Assert.AreEqual(14, StorageCapacity.SlotsFor(0.5f, 27, 5));
            Assert.AreEqual(5, StorageCapacity.SlotsFor(0.1f, 27, 5));
            Assert.AreEqual(5, StorageCapacity.SlotsFor(0f, 27, 5));
        }

        [Test]
        public void Sun_RisesEast_HighestAtNoon_UnderTheHorizonAtNight()
        {
            const float latitude = 28f;
            Vector3 sunrise = SkyPositions.Sun(6f, latitude);
            Assert.AreEqual(0f, sunrise.y, 1e-4f, "On the horizon at 06:00.");
            Assert.Greater(sunrise.x, 0.99f, "Due east.");
            Vector3 noon = SkyPositions.Sun(12f, latitude);
            Assert.AreEqual(Mathf.Sin((90f - latitude) * Mathf.Deg2Rad), noon.y, 1e-4f, "Its noon height.");
            Assert.Less(noon.z, 0f, "South at noon, from the north.");
            Assert.Less(SkyPositions.Sun(0f, latitude).y, -0.5f, "Well under the horizon at midnight.");
            Assert.Less(SkyPositions.Sun(18f, latitude).x, -0.99f, "Sets in the west.");
        }

        [Test]
        public void Moon_FollowsItsPhase_AndRisesLaterEachDay()
        {
            const float latitude = 28f;
            double full = SkyPositions.SynodicMonthDays * 0.5;
            Assert.AreEqual(0.5f, SkyPositions.MoonPhase(full), 1e-4f);
            Assert.AreEqual(1f, SkyPositions.MoonIllumination(0.5f), 1e-4f);
            Assert.AreEqual(0f, SkyPositions.MoonIllumination(0f), 1e-4f);
            Vector3 fullAtMidnight = SkyPositions.Moon(full, 0f, latitude);
            Assert.Greater(fullAtMidnight.y, 0.3f, "A full moon is up at midnight.");
            Vector3 fullAtNoon = SkyPositions.Moon(full, 12f, latitude);
            Assert.Less(fullAtNoon.y, 0f, "And down at noon, opposite the sun.");
            Assert.Less(SkyPositions.Moon(0d, 0f, latitude).y, 0f, "A new moon is down with the sun at midnight.");
            Assert.AreEqual(0f, SkyPositions.MoonPhase(SkyPositions.SynodicMonthDays * 3d), 1e-4f, "It comes round every month.");

            // The moon's hour angle falls back about 12 degrees a day: about 50 minutes later.
            float lag = (SkyPositions.MoonPhase(1d) - SkyPositions.MoonPhase(0d)) * 24f * 60f;
            Assert.AreEqual(48.8f, lag, 1f);
        }

        [Test]
        public void Birds_FlyThePattern_ThenLeaveOutOfSight()
        {
            foreach (FlightPattern pattern in new[] { FlightPattern.Pass, FlightPattern.Circle, FlightPattern.Swirl, FlightPattern.Vee })
            {
                var flock = new BirdFlock(pattern, 12, new Vector3(0f, 50f, 0f), Vector3.forward, Vector3.right, 20f, 7);
                Assert.AreEqual(12, flock.Birds.Count);
                bool gone = false;
                for (int step = 0; step < 4000 && !gone; step++)
                {
                    flock.Step(0.05f);
                    gone = flock.IsGone(Vector3.zero, 650f);
                }
                Assert.IsTrue(gone, $"{pattern}: they leave and get out of sight.");
                foreach (Bird bird in flock.Birds)
                {
                    Assert.IsFalse(float.IsNaN(bird.Position.x), $"{pattern}: no bird lost its way.");
                    Assert.Less(bird.Velocity.magnitude, 25f, $"{pattern}: birds fly at bird speeds.");
                }
            }
        }
    }
}
