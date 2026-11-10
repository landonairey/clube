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
