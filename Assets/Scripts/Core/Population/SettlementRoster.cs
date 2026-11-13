using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>One person living in a settlement (GL615) who is not a shop's keeper: a worker of one of its shops.</summary>
    public sealed class Resident
    {
        /// <summary>The resident's number in the settlement, from 0.</summary>
        public int Index;

        public Occupation Occupation;

        /// <summary>The building slept in (a house; the shop itself where the settlement has no houses).</summary>
        public int Home;

        /// <summary>The shop worked for.</summary>
        public int Shop;

        /// <summary>Picks the resident's looks and timing.</summary>
        public int Seed;
    }

    /// <summary>
    /// Who lives in a settlement (GL615): its population (GL593's numbers, on the <see cref="PlannedSettlement"/>) less
    /// the keeper each shop already has, shared out among the shops in turn (the first shops get the most hands), each
    /// working the occupation of its shop's specialty and housed in the site's houses,
    /// <see cref="SettlementSiteRules.PeoplePerHouse"/> to a house. The same site and seed give the same people.
    /// </summary>
    public static class SettlementRoster
    {
        public static List<Resident> Build(SettlementSite site, IReadOnlyList<Occupation> occupations, int seed)
        {
            var residents = new List<Resident>();
            var shops = new List<int>();
            var houses = new List<int>();
            for (int b = 0; b < site.Buildings.Count; b++)
            {
                SettlementBuilding building = site.Buildings[b];
                if (building.Kind == SettlementBuildingKind.Shop && building.Shop != null)
                {
                    shops.Add(b);
                }
                else if (building.Kind == SettlementBuildingKind.House)
                {
                    houses.Add(b);
                }
            }
            int perHouse = System.Math.Max(1, site.Rules.PeoplePerHouse);
            if (shops.Count == 0)
            {
                // No shop to work for: a thieves' camp (GL617), whose band lives by the occupation its type names.
                Occupation band = Occupation.Find(occupations, site.Settlement.Type.Residents);
                for (int i = 0; band != null && houses.Count > 0 && i < site.Settlement.Type.Band; i++)
                {
                    int home = houses[System.Math.Min(i / perHouse, houses.Count - 1)];
                    residents.Add(new Resident
                    {
                        Index = i, Occupation = band, Shop = home, Home = home, Seed = unchecked(seed * 31 + site.Settlement.Index * 7919 + i * 613),
                    });
                }
                return residents;
            }
            int workers = System.Math.Max(0, site.Settlement.Population - shops.Count);
            for (int i = 0; i < workers; i++)
            {
                int shop = shops[i % shops.Count];
                Occupation occupation = Occupation.For(occupations, site.Buildings[shop].Shop.Specialty.Id);
                if (occupation == null)
                {
                    continue;
                }
                residents.Add(new Resident
                {
                    Index = i,
                    Occupation = occupation,
                    Shop = shop,
                    Home = houses.Count > 0 ? houses[System.Math.Min(i / perHouse, houses.Count - 1)] : shop,
                    Seed = unchecked(seed * 31 + site.Settlement.Index * 7919 + i * 613),
                });
            }
            return residents;
        }
    }
}
