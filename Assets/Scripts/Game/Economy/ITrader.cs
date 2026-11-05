using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Someone the player trades with in the <see cref="MerchantPanel"/>: the merchant table (GL14) or a town shopkeeper
    /// (GL401). What they pay, what they sell, and the skills they teach for coins (GL402).
    /// </summary>
    public interface ITrader
    {
        /// <summary>Shown at the top of the trade screen.</summary>
        string DisplayName { get; }

        /// <summary>What they pay for what they buy; items not listed aren't bought.</summary>
        PriceList Prices { get; }

        /// <summary>What they sell: a lot of each item for its coins.</summary>
        IReadOnlyList<ItemPrice> Offers { get; }

        /// <summary>The skills they teach (none for a plain merchant).</summary>
        IReadOnlyList<SkillType> Trains { get; }

        /// <summary>Coins per point of experience a lesson gives (GL402).</summary>
        float CoinsPerExperience { get; }

        /// <summary>Where they are: the screen closes when the player walks away, or they do.</summary>
        Vector3 Position { get; }

        /// <summary>False once they stop trading (a shop closing), which closes the screen.</summary>
        bool IsTrading { get; }
    }
}
