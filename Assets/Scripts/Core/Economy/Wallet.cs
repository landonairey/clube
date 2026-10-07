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
