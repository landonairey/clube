using Clube.Game;
using NUnit.Framework;

namespace Clube.Game.Tests
{
    /// <summary>GL786: a click on a save only selects it; Load (or a double-click) loads.</summary>
    public class SaveListSelectionTests
    {
        [Test]
        public void NothingIsSelectedAtFirst()
        {
            var selection = new SaveListSelection(3);
            Assert.IsFalse(selection.HasSelection);
            Assert.AreEqual(-1, selection.Selected);
        }

        [Test]
        public void AClickOnTheTopSaveOnlySelectsIt()
        {
            var selection = new SaveListSelection(3);
            Assert.IsFalse(selection.Click(0, 10f));
            Assert.AreEqual(0, selection.Selected);
        }

        [Test]
        public void ASecondClickLaterOnlySelectsAgain()
        {
            var selection = new SaveListSelection(3);
            selection.Click(1, 10f);
            Assert.IsFalse(selection.Click(1, 12f));
            Assert.AreEqual(1, selection.Selected);
        }

        [Test]
        public void ADoubleClickLoads()
        {
            var selection = new SaveListSelection(3);
            selection.Click(2, 10f);
            Assert.IsTrue(selection.Click(2, 10f + SaveListSelection.DoubleClickSeconds * 0.5f));
        }

        [Test]
        public void ClicksOnTwoRowsDoNotLoad()
        {
            var selection = new SaveListSelection(3);
            selection.Click(0, 10f);
            Assert.IsFalse(selection.Click(1, 10.1f));
            Assert.AreEqual(1, selection.Selected);
        }

        [Test]
        public void AThirdQuickClickDoesNotLoadAgain()
        {
            var selection = new SaveListSelection(3);
            selection.Click(0, 10f);
            selection.Click(0, 10.1f);
            Assert.IsFalse(selection.Click(0, 10.2f));
        }

        [Test]
        public void ArrowsSelectTheTopRowFirstAndStayInTheList()
        {
            var selection = new SaveListSelection(2);
            selection.Move(1);
            Assert.AreEqual(0, selection.Selected);
            selection.Move(5);
            Assert.AreEqual(1, selection.Selected);
        }

        [Test]
        public void DeletingTheSelectedSaveLeavesNoneSelected()
        {
            var selection = new SaveListSelection(2);
            selection.Click(1, 1f);
            selection.RemoveSelected();
            Assert.IsFalse(selection.HasSelection);
            Assert.IsFalse(selection.Click(1, 2f), "the list is one shorter");
            Assert.IsFalse(selection.HasSelection);
        }
    }
}
