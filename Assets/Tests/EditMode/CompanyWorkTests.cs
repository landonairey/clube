using Clube.Core;
using NUnit.Framework;

namespace Clube.Core.Tests
{
    public class CompanyWorkTests
    {
        [Test]
        public void PaysTenCoinsForEveryHundred()
        {
            Assert.AreEqual(10f, CompanyWork.Wage(100f), 1e-5f);
            Assert.AreEqual(0.25f, CompanyWork.Wage(2.5f), 1e-5f);
            Assert.AreEqual(0f, CompanyWork.Wage(-5f));
        }

        [Test]
        public void QuotaDoublesEachDay()
        {
            Assert.AreEqual(1024, CompanyWork.Quota(1));
            Assert.AreEqual(2048, CompanyWork.Quota(2));
            Assert.AreEqual(4096, CompanyWork.Quota(3));
            Assert.AreEqual(8192, CompanyWork.Quota(4));
            Assert.Greater(CompanyWork.Quota(60), 0, "no overflow far out");
            Assert.AreEqual(1024, CompanyWork.Quota(0));
        }

        [Test]
        public void EarlyBirdWindowCanBeMovedToTheWorkHours()
        {
            Assert.AreEqual(0.75f, ToolCooldown.SecondsFor(false, 10f, 1f, 9f, 11f), 1e-5f);
            Assert.AreEqual(1f, ToolCooldown.SecondsFor(false, 12f, 1f, 9f, 11f), 1e-5f);
            Assert.AreEqual(1f, ToolCooldown.SecondsFor(false, 4f, 1f, 9f, 11f), 1e-5f, "the small hours no longer count");
        }

        [Test]
        public void DiggingIsAllowedOnlyFromNineToFive()
        {
            Assert.IsFalse(CompanyWork.IsWorkHour(8.99f));
            Assert.IsTrue(CompanyWork.IsWorkHour(9f));
            Assert.IsTrue(CompanyWork.IsWorkHour(16.99f));
            Assert.IsFalse(CompanyWork.IsWorkHour(17f));
            Assert.IsFalse(CompanyWork.IsWorkHour(2f));
            Assert.IsTrue(CompanyWork.IsWorkHour(33f), "wraps past 24");
        }

        [Test]
        public void FieldsAndWarehouseAreLevel()
        {
            var fields = CompanyWork.Fields;
            var house = CompanyWork.Warehouse;
            Assert.AreEqual(CompanyWork.Ground, CompanyWork.Height(fields.center.x, fields.center.y), 1e-4f);
            Assert.AreEqual(CompanyWork.Ground, CompanyWork.Height(house.xMin, house.yMin), 1e-4f);
            Assert.AreEqual(CompanyWork.Ground, CompanyWork.Height(house.xMax, house.yMax), 1e-4f);
        }

        [Test]
        public void MountainRisesOnThreeSides()
        {
            var fields = CompanyWork.Fields;
            float tall = CompanyWork.Ground + 20f;
            Assert.Greater(CompanyWork.Height(fields.xMin - 15f, fields.center.y), tall, "west");
            Assert.Greater(CompanyWork.Height(fields.xMax + 15f, fields.center.y), tall, "east");
            Assert.Greater(CompanyWork.Height(fields.center.x, fields.yMax + 15f), tall, "north");
            Assert.AreEqual(CompanyWork.Ground, CompanyWork.Height(fields.center.x, -40f), 1e-4f, "open to the south");
        }
    }
}
