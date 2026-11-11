using Clube.Core;

namespace Clube.Game
{
    /// <summary>
    /// Something special a trader offers beside their wares (GL473: the merchant sells the trading post; GL585: the
    /// adventurer teaches climbing), shown as a button on the <see cref="MerchantPanel"/> while it's offered.
    /// </summary>
    public interface ITraderDeal
    {
        /// <summary>The button's words.</summary>
        string Label { get; }

        /// <summary>True while it's on offer to a player with these coins.</summary>
        bool IsOffered(Wallet player);

        /// <summary>Takes it, paying from the player's wallet; false when it couldn't.</summary>
        bool Accept(Wallet player);

        /// <summary>The message shown when <see cref="Accept"/> fails; null for none.</summary>
        string RefusalText(Wallet player) => null;
    }
}
