namespace Clube.Core
{
    /// <summary>
    /// When E (the interact key) opens or closes the inventory screen (GL298, GL852). One screen at a time: E never opens
    /// the inventory over another screen, nor on the frame another screen closed, since that E was the other screen's
    /// (closing a clam with E must not also open the pack). A pure rule, so it is tested apart from the screen.
    /// </summary>
    public static class InventoryKeys
    {
        /// <summary>What a press of E does to the inventory screen.</summary>
        public enum Result
        {
            Nothing,
            Open,
            Close,
        }

        /// <param name="inventoryOpen">The inventory screen is open.</param>
        /// <param name="otherScreenOpen">Another player screen (station, merchant, chest, clam...) is open.</param>
        /// <param name="screenClosedThisFrame">A screen closed this frame, by this same key press perhaps.</param>
        /// <param name="aimingAtUsable">Something usable is under the crosshair, which E uses instead.</param>
        public static Result OnInteract(bool inventoryOpen, bool otherScreenOpen, bool screenClosedThisFrame, bool aimingAtUsable)
        {
            // The other screen takes its own E (it closes itself).
            if (otherScreenOpen)
            {
                return Result.Nothing;
            }
            if (inventoryOpen)
            {
                return Result.Close;
            }
            if (screenClosedThisFrame || aimingAtUsable)
            {
                return Result.Nothing;
            }
            return Result.Open;
        }
    }
}
