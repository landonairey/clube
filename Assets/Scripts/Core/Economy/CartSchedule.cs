namespace Clube.Core
{
    /// <summary>Where the trade cart is in its trip (GL477).</summary>
    public enum CartPhase
    {
        /// <summary>At the distant town, between trips.</summary>
        Away,

        /// <summary>On the road from the distant town to the trading post.</summary>
        ToPost,

        /// <summary>Stopped at the trading post.</summary>
        AtPost,

        /// <summary>On the road back to the distant town.</summary>
        ToTown,
    }

    /// <summary>The trade cart's trip at a moment (GL477).</summary>
    public struct CartState
    {
        /// <summary>The trip, counting from 0; -1 before the first.</summary>
        public int Trip;

        public CartPhase Phase;

        /// <summary>How far along the road it is: 0 at the distant town, 1 at the trading post.</summary>
        public float Progress;
    }

    /// <summary>
    /// When the trade cart travels (GL477): every two or three days (picked from the seed, trip by trip) it leaves the
    /// distant town in the morning, takes the road to the trading post, stops there a while, and goes back. Everything
    /// follows from the seed and the clock, so the cart itself is never saved.
    /// </summary>
    public static class CartSchedule
    {
        /// <summary>Days on the clock (which starts at 07:00 on day 1) when the first trip leaves: 09:00 on day 1 (GL511: it used to be day 2, over an hour of play away, so nobody saw it).</summary>
        public const double FirstTrip = 2d / 24d;

        /// <summary>The fewest and most whole days from one trip's start to the next.</summary>
        public const int MinGap = 2;
        public const int MaxGap = 3;

        /// <summary>The days between trip <paramref name="trip"/> and the next: <see cref="MinGap"/> to <see cref="MaxGap"/>.</summary>
        public static int Gap(int seed, int trip)
        {
            uint h = Hash((uint)seed * 0x9E3779B9u + (uint)trip * 0x85EBCA6Bu + 477u);
            return MinGap + (int)(h % (uint)(MaxGap - MinGap + 1));
        }

        /// <summary>The clock's days when trip <paramref name="trip"/> leaves the distant town.</summary>
        public static double TripStart(int seed, int trip)
        {
            double start = FirstTrip;
            for (int i = 0; i < trip; i++)
            {
                start += Gap(seed, i);
            }
            return start;
        }

        /// <summary>
        /// Where the cart is at <paramref name="days"/> on the clock, for a road that takes <paramref name="legDays"/> each
        /// way and a stop of <paramref name="waitDays"/> at the trading post.
        /// </summary>
        public static CartState At(int seed, double days, double legDays, double waitDays)
        {
            if (days < FirstTrip)
            {
                return new CartState { Trip = -1, Phase = CartPhase.Away, Progress = 0f };
            }
            // Trips repeat at least every two days, so this is short for any game's length.
            int trip = 0;
            double start = FirstTrip;
            while (true)
            {
                double next = start + Gap(seed, trip);
                if (next > days)
                {
                    break;
                }
                start = next;
                trip++;
            }
            double t = days - start;
            legDays = System.Math.Max(1e-6, legDays);
            if (t < legDays)
            {
                return new CartState { Trip = trip, Phase = CartPhase.ToPost, Progress = (float)(t / legDays) };
            }
            t -= legDays;
            if (t < waitDays)
            {
                return new CartState { Trip = trip, Phase = CartPhase.AtPost, Progress = 1f };
            }
            t -= waitDays;
            if (t < legDays)
            {
                return new CartState { Trip = trip, Phase = CartPhase.ToTown, Progress = 1f - (float)(t / legDays) };
            }
            return new CartState { Trip = trip, Phase = CartPhase.Away, Progress = 0f };
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }
    }
}
