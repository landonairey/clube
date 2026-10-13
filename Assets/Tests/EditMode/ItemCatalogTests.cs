using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The item catalog (GL43): finding an item by id or name, loosely.</summary>
    public class ItemCatalogTests
    {
        [Test]
        public void Find_MatchesIdOrName_IgnoringCaseSpacesAndDashes()
        {
            ItemDefinition ingot = ItemDefinition.Create("copper-ingot", "Copper ingot");
            ItemDefinition ore = ItemDefinition.Create("chalcopyrite", "Chalcopyrite");
            ItemCatalog catalog = ItemCatalog.Create(ingot, ore);
            try
            {
                Assert.AreSame(ingot, catalog.Find("copper ingot"));
                Assert.AreSame(ingot, catalog.Find("Copper_Ingot"));
                Assert.AreSame(ingot, catalog.Find("copper-ingot"));
                Assert.AreSame(ore, catalog.Find("CHALCOPYRITE"));
                Assert.IsNull(catalog.Find("malachite"));
                Assert.IsNull(catalog.Find(""));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(ingot);
                Object.DestroyImmediate(ore);
            }
        }
    }
}
