using Clube.Core;

namespace Clube.Game
{
    /// <summary>
    /// A trader whose wares are real goods they carry (GL868: a sherpa's pack): the <see cref="MerchantPanel"/> sells no
    /// more of an offer than they hold, and tells them what the player bought so it leaves their stock.
    /// </summary>
    public interface IStockedTrader : ITrader
    {
        /// <summary>How much of <paramref name="item"/> they have to sell now.</summary>
        float StockOf(ItemDefinition item);

        /// <summary>The player bought <paramref name="amount"/> of <paramref name="item"/>.</summary>
        void SoldToPlayer(ItemDefinition item, float amount);
    }
}
