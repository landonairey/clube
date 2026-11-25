using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The equipment panel's figure wears what's worn (20261124b item 5): rags, a loin wrap, garments by slot.</summary>
    public class WornLookTests
    {
        private static ItemDefinition Item(string name)
        {
            return AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Config/Items/{name}.asset");
        }

        [Test]
        public void Nothing_Worn_ShowsSkinAndAWrap()
        {
            FigureLook body = FigureLooks.Of(FigurePreset.Player);
            FigureLook look = WornLook.Dress(body, slot => null);
            Assert.AreEqual(body.Skin, look.Shirt);
            Assert.AreEqual(body.Skin, look.Trousers);
            Assert.AreEqual(WornLook.WrapCloth, look.Wrap);
            Assert.AreEqual(FigureHat.None, look.HatStyle);
            Assert.AreEqual(0f, look.Gloves.a);
        }

        [Test]
        public void Nothing_OnTheChest_DropsThePackAndItsStraps()
        {
            FigureLook body = FigureLooks.Of(FigurePreset.Player);
            Assert.IsTrue(body.Has(FigureOutfit.Backpack));
            FigureLook bare = WornLook.Dress(body, slot => null);
            Assert.IsFalse(bare.Has(FigureOutfit.Backpack));
            Assert.IsFalse(bare.Has(FigureOutfit.Belt));
            FigureLook ragged = WornLook.Dress(body, slot => slot == WearSlot.Chest ? Item("Rags") : null);
            Assert.IsTrue(ragged.Has(FigureOutfit.Backpack));
        }

        [Test]
        public void Rags_AreTornInTheirColour_LegsStillWrapped()
        {
            ItemDefinition rags = Item("Rags");
            FigureLook look = WornLook.Dress(FigureLooks.Of(FigurePreset.Player), slot => slot == WearSlot.Chest ? rags : null);
            Assert.AreEqual(rags.IconTint.r, look.Shirt.r, 1e-5f);
            Assert.IsTrue(look.Has(FigureOutfit.Patched));
            Assert.Greater(look.Wrap.a, 0f);
        }

        [Test]
        public void Garments_ShowOnTheirParts()
        {
            ItemDefinition pants = Item("Pants"), gloves = Item("Leather gloves"), helmet = Item("Leather helmet");
            FigureLook look = WornLook.Dress(FigureLooks.Of(FigurePreset.Player),
                slot => slot == WearSlot.Legs ? pants : slot == WearSlot.Hands ? gloves : slot == WearSlot.Head ? helmet : null);
            Assert.AreEqual(pants.IconTint.b, look.Trousers.b, 1e-5f);
            Assert.AreEqual(0f, look.Wrap.a);
            Assert.AreEqual(gloves.IconTint.g, look.Gloves.g, 1e-5f);
            Assert.AreEqual(FigureHat.Cap, look.HatStyle);
            Assert.AreNotEqual(FigureLooks.Of(FigurePreset.Player).MeshKey(), look.MeshKey());
        }
    }
}
