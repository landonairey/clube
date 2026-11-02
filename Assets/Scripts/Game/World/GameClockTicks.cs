using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Counts whole game seconds as a <see cref="GameClock"/> moves, a frame at a time, carrying the fractions over: what
    /// burns or grows by game time (torches, GL287) asks it each frame, so hours slept pass for them as hours. Without a
    /// clock it counts real seconds.
    /// </summary>
    public sealed class GameClockTicks
    {
        private readonly GameClock clock;
        private double lastDays = -1d;
        private double carried;

        public GameClockTicks(GameClock clock)
        {
            this.clock = clock;
        }

        /// <summary>The whole game seconds since the last call (none on the first).</summary>
        public int WholeSeconds()
        {
            if (clock == null)
            {
                carried += Time.deltaTime;
            }
            else
            {
                double days = clock.Days;
                if (lastDays >= 0d && days > lastDays)
                {
                    carried += (days - lastDays) * clock.DayLengthSeconds;
                }
                lastDays = days;
            }
            int whole = (int)System.Math.Floor(carried);
            carried -= whole;
            return whole;
        }
    }
}
