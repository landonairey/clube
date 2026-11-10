using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How a shopkeeper feels about the player (GL471), 0 to 100, starting at <see cref="Start"/>. Every
    /// <see cref="CoinsPerPoint"/> coins of trade with them (either way) raises it by one. At 100 their prices are fair;
    /// each point under 100 is a point (1%) of markup on what they sell and a point off what they pay. Plain data, saved by
    /// the game.
    /// </summary>
    public sealed class Disposition
    {
        /// <summary>Where a keeper starts.</summary>
        public const int Start = 10;

        /// <summary>The most it reaches: fair prices.</summary>
        public const int Max = 100;

        /// <summary>Coins of trade for each point.</summary>
        public const float CoinsPerPoint = 100f;

        public Disposition(int value = Start, float carried = 0f)
        {
            Restore(value, carried);
        }

        /// <summary>Raised when the value changes.</summary>
        public event Action Changed;

        /// <summary>0 to 100.</summary>
        public int Value { get; private set; }

        /// <summary>Coins traded towards the next point.</summary>
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
            while (Carried + 1e-4f >= CoinsPerPoint && Value < Max)
            {
                Value++;
                Carried = Mathf.Max(0f, Carried - CoinsPerPoint);
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

        /// <summary>Puts back a saved value and the coins carried towards the next point.</summary>
        public void Restore(int value, float carried)
        {
            int before = Value;
            Value = Mathf.Clamp(value, 0, Max);
            Carried = Value >= Max ? 0f : Mathf.Clamp(carried, 0f, CoinsPerPoint);
            if (Value != before)
            {
                Changed?.Invoke();
            }
        }
    }
}
