using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A settlement's land as its Merchant sells it (GL863, GL864): the area it stands on and the price of the whole lot by its
    /// type. Its parcels are the ones that area touches, the same ones the map locks when the settlement is built.
    /// </summary>
    public sealed class SettlementLand
    {
        /// <param name="area">The ground it stands on (x, z, metres from the world origin).</param>
        /// <param name="salePrice">Coins for the whole settlement before the disposition (its type's <c>SalePrice</c>).</param>
        public SettlementLand(string name, string typeName, Rect area, int salePrice)
        {
            Name = name;
            TypeName = typeName;
            Area = area;
            SalePrice = salePrice;
        }

        public string Name { get; }

        /// <summary>Camp, Outpost, Village... as shown.</summary>
        public string TypeName { get; }

        /// <summary>The ground it stands on (x, z, metres from the world origin).</summary>
        public Rect Area { get; }

        /// <summary>Coins for the whole settlement before the disposition; 0 when it isn't for sale.</summary>
        public int SalePrice { get; }

        /// <summary>The parcels it stands on.</summary>
        public List<Vector2Int> ParcelsIn(LandParcels parcels)
        {
            return parcels.ParcelsCovering(Area);
        }
    }

    /// <summary>A trader who sells land round their settlement (GL863: the Merchant); <see cref="Land"/> is null for one who doesn't.</summary>
    public interface IParcelSeller
    {
        SettlementLand Land { get; }
    }
}
