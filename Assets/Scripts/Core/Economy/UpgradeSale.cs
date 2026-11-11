namespace Clube.Core
{
    /// <summary>
    /// A one-time permanent upgrade a trader sells for coins (GL585: the adventurer's climbing upgrade). Holds the price
    /// and whether the player owns it; buying takes the coins from the player and gives them to the seller, once.
    /// </summary>
    public sealed class UpgradeSale
    {
        public UpgradeSale(int price)
        {
            Price = price < 0 ? 0 : price;
        }

        /// <summary>What it costs, in coins.</summary>
        public int Price { get; }

        /// <summary>True once the player has bought it.</summary>
        public bool Owned { get; private set; }

        /// <summary>Marks it owned or not without paying (restoring a save).</summary>
        public void Restore(bool owned)
        {
            Owned = owned;
        }

        /// <summary>True while it's for sale and <paramref name="player"/> has the coins.</summary>
        public bool CanBuy(Wallet player)
        {
            return !Owned && player != null && player.Coins + 1e-4f >= Price;
        }

        /// <summary>
        /// Buys it: <paramref name="player"/> pays <see cref="Price"/> to <paramref name="seller"/> (which may be null).
        /// False, changing nothing, when it's owned already or the wallet is short.
        /// </summary>
        public bool TryBuy(Wallet player, Wallet seller)
        {
            if (!CanBuy(player) || !player.Spend(Price))
            {
                return false;
            }
            seller?.Earn(Price);
            Owned = true;
            return true;
        }
    }
}
