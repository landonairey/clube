using System;

namespace Clube.Core
{
    /// <summary>
    /// A coin balance (GL12, first pass of TR3): earned by selling, spent by buying. One
    /// currency for now; silver and gold coin (TR3, PR5) can come later. Coins are decimal
    /// (GL199): selling a part of a stack, or one ingot of a lot, pays its share of a coin, kept to
    /// the hundredth. Plain data, so the player, merchants and towns can each have one.
    /// </summary>
    public sealed class Wallet
    {
        public Wallet(float coins = 0f)
        {
            Coins = Round(Math.Max(0f, coins));
        }

        /// <summary>Raised after the balance changes.</summary>
        public event Action Changed;

        /// <summary>The balance, to the hundredth of a coin.</summary>
        public float Coins { get; private set; }

        /// <summary>A balance as the player sees it: "12", "12.5", "0.37".</summary>
        public static string Format(float coins)
        {
            return coins.ToString("0.##");
        }

        /// <summary>Adds coins, a part of one included (GL40, GL199); a negative or zero amount does nothing.</summary>
        public void Earn(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }
            Coins = Round(Coins + amount);
            Changed?.Invoke();
        }

        /// <summary>Takes coins if there are enough; false, changing nothing, otherwise.</summary>
        public bool Spend(float amount)
        {
            // A hair over, so a price of exactly the balance always goes through.
            if (amount < 0f || amount > Coins + 1e-4f)
            {
                return false;
            }
            if (amount > 0f)
            {
                Coins = Round(Math.Max(0f, Coins - amount));
                Changed?.Invoke();
            }
            return true;
        }

        // To the hundredth: what's shown is what's held.
        private static float Round(float coins)
        {
            return (float)Math.Round(coins, 2, MidpointRounding.AwayFromZero);
        }
    }
}
