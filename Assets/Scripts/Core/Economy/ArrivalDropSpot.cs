using System;

namespace Clube.Core
{
    /// <summary>
    /// Which of the places round the family's parcel the arrival cart stops at (GL765, GL766): the first with clear ground
    /// and a clear view of the parcel's arrow, else the first with clear ground, else the first the cart can reach at all.
    /// </summary>
    public static class ArrivalDropSpot
    {
        /// <summary>The index of the best place of <paramref name="count"/>, in the order given; -1 when none is usable.</summary>
        public static int Pick(int count, Func<int, bool> usable, Func<int, bool> clearGround, Func<int, bool> clearView)
        {
            int firstClear = -1, firstUsable = -1;
            for (int i = 0; i < count; i++)
            {
                if (!usable(i))
                {
                    continue;
                }
                if (firstUsable < 0)
                {
                    firstUsable = i;
                }
                if (!clearGround(i))
                {
                    continue;
                }
                if (clearView(i))
                {
                    return i;
                }
                if (firstClear < 0)
                {
                    firstClear = i;
                }
            }
            return firstClear >= 0 ? firstClear : firstUsable;
        }
    }
}
