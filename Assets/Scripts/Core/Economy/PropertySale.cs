namespace Clube.Core
{
    /// <summary>
    /// A property the player can buy from its keeper (GL473: the trading post and its parcel, 10k), which then pays a share
    /// of its sales every game day. Offered only to a player who has the price. Plain data, saved by the game.
    /// </summary>
    public sealed class PropertySale
    {
        public PropertySale(int price, int profitPerDay)
        {
            Price = price;
            ProfitPerDay = profitPerDay;
        }

        /// <summary>What it costs.</summary>
        public int Price { get; }

        /// <summary>Coins it pays the owner each game day.</summary>
        public int ProfitPerDay { get; }

        /// <summary>True once the player owns it.</summary>
        public bool Owned { get; private set; }

        /// <summary>The last game day it paid for (the day it was bought, at first).</summary>
        public int PaidDay { get; private set; }

        /// <summary>True when the keeper offers it: not owned yet, and the player has the price.</summary>
        public bool IsOffered(float coins)
        {
            return !Owned && coins + 1e-3f >= Price;
        }

        /// <summary>Buys it on game day <paramref name="today"/>. False, changing nothing, when it isn't offered or the wallet is short.</summary>
        public bool TryBuy(Wallet wallet, int today)
        {
            if (wallet == null || !IsOffered(wallet.Coins) || !wallet.Spend(Price))
            {
                return false;
            }
            Owned = true;
            PaidDay = today;
            return true;
        }

        /// <summary>The profit owed for the days since it last paid, up to <paramref name="today"/>, marked as paid.</summary>
        public int Collect(int today)
        {
            if (!Owned || today <= PaidDay)
            {
                return 0;
            }
            int days = today - PaidDay;
            PaidDay = today;
            return days * ProfitPerDay;
        }

        /// <summary>Puts back a save's ownership.</summary>
        public void Restore(bool owned, int paidDay)
        {
            Owned = owned;
            PaidDay = paidDay;
        }
    }
}
