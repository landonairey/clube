using System;

namespace Clube.Core
{
    /// <summary>
    /// A coin balance (GL12, first pass of TR3): earned by selling, spent by buying. One
    /// currency for now; silver and gold coin (TR3, PR5) can come later. Plain data, so the
    /// player, merchants and towns can each have one.
    /// </summary>
    public sealed class Wallet
    {
        public Wallet(int coins = 0)
        {
            Coins = Math.Max(0, coins);
        }

        /// <summary>Raised after the balance changes.</summary>
        public event Action Changed;

        public int Coins { get; private set; }

        // Part of a coin earned so far (selling one ingot of ten to the coin), kept until it adds up.
        private double fraction;

        /// <summary>Adds coins; a negative or zero amount does nothing.</summary>
        public void Earn(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            Coins += amount;
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds an amount that needn't be whole (GL40): a coin is paid out each time the parts add up to
        /// one, and the rest is kept for next time. Negative or zero amounts do nothing.
        /// </summary>
        public void EarnFraction(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }
            fraction += amount;
            // A little over, so ten tenths of a coin pay a coin despite float error.
            int whole = (int)Math.Floor(fraction + 1e-4);
            if (whole > 0)
            {
                fraction -= whole;
                Coins += whole;
                Changed?.Invoke();
            }
        }

        /// <summary>Takes coins if there are enough; false, changing nothing, otherwise.</summary>
        public bool Spend(int amount)
        {
            if (amount < 0 || amount > Coins)
            {
                return false;
            }
            if (amount > 0)
            {
                Coins -= amount;
                Changed?.Invoke();
            }
            return true;
        }
    }
}
