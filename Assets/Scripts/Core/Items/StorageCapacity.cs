using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How many slots a storage chest has from the quality it was built at (GL249): a chest of 100% wood holds as many as
    /// the player's inventory, a poorer one fewer, down to a floor.
    /// </summary>
    public static class StorageCapacity
    {
        /// <summary>The slots for a quality: <paramref name="full"/> at 100% (or better), in proportion below, never under <paramref name="floor"/>.</summary>
        public static int SlotsFor(float quality, int full, int floor)
        {
            floor = Mathf.Max(1, Mathf.Min(floor, full));
            return Mathf.Clamp(Mathf.RoundToInt(full * Mathf.Clamp01(quality)), floor, full);
        }
    }
}
