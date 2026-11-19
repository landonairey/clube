using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How a shopkeeper feels about the player (GL471), 0 to 100, starting at <see cref="Start"/>. Every
    /// <see cref="CoinsForPoint"/> coins of trade with them (either way) raises it by one, a cost that climbs with the value (GL769). At 100 their prices are fair;
    /// each point under 100 is a point (1%) of markup on what they sell and a point off what they pay. Plain data, saved by
    /// the game.
    /// </summary>
    public sealed class Disposition
    {
        /// <summary>Where a keeper starts.</summary>
        public const int Start = 10;

        /// <summary>The most it reaches: fair prices.</summary>
        public const int Max = 100;

        /// <summary>GL798: points a keeper's disposition falls when the player sleeps in their bed (the user: "only 1pt").</summary>
        public const int BedPenalty = 1;

        /// <summary>Coins of trade for a point at disposition 10, where a keeper starts (GL769: "around 1pt for ~10c").</summary>
        public const float CoinsAtStart = 10f;

        /// <summary>Coins of trade for a point at disposition 50 ("around ~100c trade for 1pt").</summary>
        public const float CoinsAtMiddle = 100f;

        /// <summary>Coins of trade for a point at disposition 90 ("~1000c trade for 1pt").</summary>
        public const float CoinsAtHigh = 1000f;

        /// <summary>
        /// Coins of trade that raise the disposition from <paramref name="value"/> to the next point (GL769). The cost grows by
        /// a steady factor per point between the three anchors (10, 50 and 90: ten times for each 40 points), stays at the
        /// first anchor's below 10, and keeps growing at the last rate past 90.
        /// </summary>
        public static float CoinsForPoint(int value)
        {
            float exponent;
            if (value <= 10)
            {
                exponent = 1f;
            }
            else if (value <= 50)
            {
                exponent = 1f + (value - 10) / 40f;
            }
            else
            {
                exponent = 2f + (value - 50) / 40f;
            }
            return Mathf.Pow(10f, exponent);
        }

        /// <summary>Coins of trade it takes to go from disposition <paramref name="from"/> up to <paramref name="to"/>.</summary>
        public static float CoinsBetween(int from, int to)
        {
            float total = 0f;
            for (int value = Mathf.Max(0, from); value < Mathf.Min(Max, to); value++)
            {
                total += CoinsForPoint(value);
            }
            return total;
        }

        public Disposition(int value = Start, float carried = 0f)
        {
            Restore(value, carried);
        }

        /// <summary>Raised when the value changes.</summary>
        public event Action Changed;

        /// <summary>0 to 100.</summary>
        public int Value { get; private set; }

        /// <summary>Coins traded towards the next point (which costs <see cref="CoinsForPoint"/> of the current value).</summary>
        public float Carried { get; private set; }

        /// <summary>The share of a fair price added to what they sell, and taken off what they pay: 0.9 at 10, 0 at 100.</summary>
        public float Markup => (Max - Value) / (float)Max;

        /// <summary>The share of an item's listed price they pay for it: 0.1 at 10, 1 at 100.</summary>
        public float PayShare => 1f - Markup;

        /// <summary>What they ask for something listed at <paramref name="coins"/>, in whole coins, rounded up.</summary>
        public int SellingPrice(int coins)
        {
            return Mathf.Max(coins, Mathf.CeilToInt(coins * (1f + Markup) - 1e-4f));
        }

        /// <summary>Counts coins of trade with them; returns the points it raised.</summary>
        public int Record(float coins)
        {
            if (coins <= 0f || Value >= Max)
            {
                return 0;
            }
            int before = Value;
            Carried += coins;
            while (Carried + 1e-4f >= CoinsForPoint(Value) && Value < Max)
            {
                Carried = Mathf.Max(0f, Carried - CoinsForPoint(Value));
                Value++;
            }
            if (Value >= Max)
            {
                Carried = 0f;
            }
            if (Value != before)
            {
                Changed?.Invoke();
            }
            return Value - before;
        }

        /// <summary>
        /// Takes points off (GL729: goods stolen on their way to this keeper), down to 0; coins carried towards the next
        /// point are lost with them. Returns the points it fell.
        /// </summary>
        public int Lower(int points)
        {
            if (points <= 0 || Value <= 0)
            {
                return 0;
            }
            int before = Value;
            Value = Mathf.Max(0, Value - points);
            Carried = 0f;
            Changed?.Invoke();
            return before - Value;
        }

        /// <summary>GL798: the player slept in this keeper's bed; takes <see cref="BedPenalty"/> off, never below 0.</summary>
        public int SleptInBed()
        {
            return Lower(BedPenalty);
        }

        /// <summary>Puts back a saved value and the coins carried towards the next point.</summary>
        public void Restore(int value, float carried)
        {
            int before = Value;
            Value = Mathf.Clamp(value, 0, Max);
            Carried = Value >= Max ? 0f : Mathf.Clamp(carried, 0f, CoinsForPoint(Value));
            if (Value != before)
            {
                Changed?.Invoke();
            }
        }
    }
}
