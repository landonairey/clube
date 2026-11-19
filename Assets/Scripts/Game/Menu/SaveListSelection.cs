namespace Clube.Game
{
    /// <summary>
    /// Which save the load page has picked, and what a click on a row does (GL786): a click only selects (highlights) a
    /// save; a second click on the same row soon after (a double-click) loads it, the usual shortcut. Nothing is selected
    /// when the page opens, so Load stays off until the player picks a save.
    /// </summary>
    public sealed class SaveListSelection
    {
        /// <summary>Most seconds between the two clicks of a double-click.</summary>
        public const float DoubleClickSeconds = 0.4f;

        private int count;
        private int lastClicked = -1;
        private float lastClickAt = float.NegativeInfinity;

        public SaveListSelection(int count)
        {
            this.count = count;
        }

        /// <summary>The selected row, or -1 with none.</summary>
        public int Selected { get; private set; } = -1;

        /// <summary>True when a row is selected.</summary>
        public bool HasSelection => Selected >= 0 && Selected < count;

        /// <summary>A click on row <paramref name="index"/> at <paramref name="time"/> seconds; true when it should load.</summary>
        public bool Click(int index, float time)
        {
            if (index < 0 || index >= count)
            {
                return false;
            }
            bool isDouble = index == lastClicked && index == Selected && time - lastClickAt <= DoubleClickSeconds;
            Selected = index;
            // A third click starts a new pair rather than loading twice.
            lastClicked = isDouble ? -1 : index;
            lastClickAt = time;
            return isDouble;
        }

        /// <summary>Moves the selection by <paramref name="step"/> rows (the arrow keys); the first press selects the top row.</summary>
        public void Move(int step)
        {
            if (count == 0)
            {
                return;
            }
            Selected = Selected < 0 ? 0 : UnityEngine.Mathf.Clamp(Selected + step, 0, count - 1);
            lastClicked = -1;
        }

        /// <summary>The selected row was removed (deleted): nothing is selected, and the list is one shorter.</summary>
        public void RemoveSelected()
        {
            if (!HasSelection)
            {
                return;
            }
            count--;
            Selected = -1;
            lastClicked = -1;
        }
    }
}
