using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The tutorial's own map of the Mining Corp's site (GL760).</summary>
    public class CompanySiteMapTests
    {
        private const int Size = 112;

        private static Color32 PixelAt(Color32[] pixels, float x, float z)
        {
            Vector2 fraction = CompanySiteMap.Fraction(new Vector2(x, z));
            return pixels[Mathf.FloorToInt(fraction.y * Size) * Size + Mathf.FloorToInt(fraction.x * Size)];
        }

        [Test]
        public void TheMap_CoversTheWholeYard()
        {
            Assert.IsTrue(CompanySiteMap.Area.Contains(new Vector2(CompanyWork.Yard.xMin, CompanyWork.Yard.yMin)));
            Assert.IsTrue(CompanySiteMap.Area.Contains(new Vector2(CompanyWork.Yard.xMax, CompanyWork.Yard.yMax)));
        }

        [Test]
        public void Fraction_RunsFromTheSouthWestCornerToTheNorthEast()
        {
            Rect area = CompanySiteMap.Area;
            Assert.AreEqual(Vector2.zero, CompanySiteMap.Fraction(new Vector2(area.xMin, area.yMin)));
            Assert.AreEqual(Vector2.one, CompanySiteMap.Fraction(new Vector2(area.xMax, area.yMax)));
        }

        [Test]
        public void ThePicture_ShowsTheWarehouseTheFieldsAndTheMountainInTheirPlaces()
        {
            Color32[] pixels = CompanySiteMap.Render(Size);
            Assert.AreEqual(Size * Size, pixels.Length);

            Vector2 warehouse = CompanyWork.Warehouse.center;
            Vector2 fields = CompanyWork.Fields.center;
            Assert.AreEqual(CompanySiteMap.WarehouseRoof, PixelAt(pixels, warehouse.x, warehouse.y));
            Assert.AreEqual(CompanySiteMap.OpenGround, PixelAt(pixels, fields.x, fields.y));
            Assert.AreEqual(CompanySiteMap.OpenGround, PixelAt(pixels, fields.x, CompanySiteMap.Area.yMin + 1f), "Open to the south.");
            Assert.AreNotEqual(CompanySiteMap.OpenGround, PixelAt(pixels, fields.x, CompanySiteMap.Area.yMax - 1f), "The mountain to the north.");
        }
    }
}
