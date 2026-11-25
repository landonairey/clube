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
        /// Coins of trade that raise the disposition from <paramref name="value"/> to the next point (GL769, GL830): the user's
        /// fit <c>round(5.619 * exp(0.057575 * value))</c>, in whole coins (6c at 0, 10c at 10, 100c at 50, 1000c at 90). GL854: no
        /// clamp at the start, so a keeper at 0 costs 6c a point, not 10c.
        /// </summary>
        public static float CoinsForPoint(int value)
        {
            return Mathf.Round(5.619f * Mathf.Exp(0.057575f * Mathf.Clamp(value, 0, Max)));
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

        /// <summary>
        /// Points added for the moment, not earned or saved (item 20: the player's outfit while they trade). Prices follow
        /// <see cref="Effective"/>; trade still raises <see cref="Value"/> at its own cost.
        /// </summary>
        public int Bonus
        {
            get => bonus;
            set
            {
                if (value != bonus)
                {
                    bonus = value;
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>The value with the bonus, 0 to 100: what the prices follow.</summary>
        public int Effective => Mathf.Clamp(Value + bonus, 0, Max);

        private int bonus;

        /// <summary>The share of a fair price added to what they sell, and taken off what they pay: 0.9 at 10, 0 at 100.</summary>
        public float Markup => (Max - Effective) / (float)Max;

        /// <summary>
        /// The share of an item's listed price they pay for it: itemValue - (100 - Disposition)/2 read as a percentage, so
        /// 0.5 at 0, 0.55 at 10, 1 at 100.
        /// </summary>
        public float PayShare => 1f - Markup * 0.5f;

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
