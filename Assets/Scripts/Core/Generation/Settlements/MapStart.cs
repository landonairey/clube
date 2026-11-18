using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where a new arrival is set down on a bounded map (GL601, GL656): beside an outpost with a Merchant shop, else the capital, else the largest friendly
    /// settlement, else on the flat dry land nearest the middle of the map.
    /// </summary>
    public static class MapStart
    {
        /// <summary>Metres from the settlement's middle the start stands, towards the map's middle.</summary>
        public const float Offset = 24f;

        /// <summary>The settlement type a new arrival starts beside (GL656).</summary>
        public const string StartType = "outpost";

        // The Merchant specialty's id (its row in EconomySpecialty.Defaults).
        private const string MerchantId = "merchant";

        /// <summary>
        /// The settlement the start stands beside (GL656): an outpost with a Merchant shop, the one nearest the capital on the
        /// capital's own road network if any is. With no such outpost, the outpost nearest the capital (on its network first) is
        /// given a Merchant shop. With no outpost at all, the capital, else the largest friendly settlement.
        /// </summary>
        public static PlannedSettlement Home(WorldPlan plan)
        {
            if (plan == null)
            {
                return null;
            }
            PlannedSettlement capital = plan.Capital;
            PlannedSettlement best = null, bestWithShop = null;
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                // GL734: the tutorial's quarry is where the job was, not the family's land.
                if (settlement.Type.Id != StartType || settlement.Type.Hostile || settlement.IsQuarry)
                {
                    continue;
                }
                if (IsBetterStart(settlement, best, capital))
                {
                    best = settlement;
                }
                if (settlement.Has(MerchantId) && IsBetterStart(settlement, bestWithShop, capital))
                {
                    bestWithShop = settlement;
                }
            }
            if (bestWithShop != null && (best == null || OnCapitalNetwork(bestWithShop, capital) || !OnCapitalNetwork(best, capital)))
            {
                return bestWithShop;
            }
            if (best != null)
            {
                EconomySpecialty merchant = EconomySpecialty.Defaults().Find(s => s.Id == MerchantId);
                if (merchant != null)
                {
                    best.Shops.Add(new PlannedShop { Specialty = merchant, Guaranteed = true, Reason = "the start's shop (GL656)" });
                }
                return best;
            }
            PlannedSettlement home = capital;
            if (home == null)
            {
                foreach (PlannedSettlement settlement in plan.Settlements)
                {
                    if (!settlement.Type.Hostile && (home == null || settlement.Type.Parcels > home.Type.Parcels))
                    {
                        home = settlement;
                    }
                }
            }
            return home;
        }

        private static bool OnCapitalNetwork(PlannedSettlement settlement, PlannedSettlement capital)
        {
            return capital == null || settlement.RoadNetwork == capital.RoadNetwork;
        }

        // On the capital's network beats off it; then nearer the capital (or the map's first, with no capital).
        private static bool IsBetterStart(PlannedSettlement candidate, PlannedSettlement current, PlannedSettlement capital)
        {
            if (current == null)
            {
                return true;
            }
            bool candidateOn = OnCapitalNetwork(candidate, capital), currentOn = OnCapitalNetwork(current, capital);
            if (candidateOn != currentOn)
            {
                return candidateOn;
            }
            return capital != null
                && (candidate.Position - capital.Position).sqrMagnitude < (current.Position - capital.Position).sqrMagnitude;
        }

        /// <summary>
        /// The start's place on the map (x, z), beside <see cref="Home"/> (which may give an outpost its Merchant shop, so call it
        /// before the settlements are built); always on a survey cell that is mostly dry when the map has one.
        /// </summary>
        public static Vector2 Pick(WorldPlan plan, ResourceSurvey survey)
        {
            PlannedSettlement home = Home(plan);
            Vector2 centre = survey.Area.center;
            if (home != null)
            {
                Vector2 inland = centre - home.Position;
                Vector2 beside = home.Position + (inland.sqrMagnitude > 1f ? inland.normalized * Offset : Vector2.zero);
                return IsDry(survey, beside) ? beside : home.Position;
            }

            Vector2 best = centre;
            float bestDistance = float.MaxValue;
            for (int z = 0; z < survey.Size; z++)
            {
                for (int x = 0; x < survey.Size; x++)
                {
                    SurveyCell cell = survey.Cell(x, z);
                    Vector2 at = survey.CellCentre(x, z);
                    float distance = (at - centre).sqrMagnitude;
                    if (cell.Land > 0.9f && cell.Flatness > 0.5f && distance < bestDistance)
                    {
                        best = at;
                        bestDistance = distance;
                    }
                }
            }
            return best;
        }

        /// <summary>True when the survey cell at a position is mostly land.</summary>
        public static bool IsDry(ResourceSurvey survey, Vector2 position)
        {
            return survey.TryCell(position, out Vector2Int cell) && survey.Cell(cell.x, cell.y).Land > 0.9f;
        }
    }
}
