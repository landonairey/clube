using System;
using System.Collections.Generic;
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

        // The family's parcel (GL744): parcels searched each way, metres kept from a road, and the most its ground may rise.
        private const int ParcelSearchRings = 12;
        private const float RoadClearance = 6f;
        private const float ParcelMostRelief = 8f;

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

        /// <summary>
        /// GL744: the centre of the family's parcel, the nearest to <paramref name="near"/> that can be theirs: clear of every
        /// settlement's land (a settlement's parcels are locked, so one there is never granted) and of the roads, on open,
        /// fairly level ground. <paramref name="near"/> itself when no parcel in reach is suitable.
        /// </summary>
        /// <param name="open">True where the player can walk and nothing is under water; may be null.</param>
        /// <param name="heightAt">The land's height at (x, z).</param>
        public static Vector2 FamilyParcel(WorldPlan plan, Vector2 near, float parcelSize, Func<float, float, bool> open, Func<float, float, float> heightAt)
        {
            var home = new Vector2Int(Mathf.FloorToInt(near.x / parcelSize), Mathf.FloorToInt(near.y / parcelSize));
            var rules = new SettlementSiteRules();
            float reach = (ParcelSearchRings + 1) * parcelSize;
            var search = new Rect(near.x - reach, near.y - reach, reach * 2f, reach * 2f);
            var roadPoints = new List<Vector2>();
            foreach (PlannedRoad road in plan.Roads)
            {
                roadPoints.AddRange(road.Points.FindAll(search.Contains));
            }
            var parcels = new List<Vector2Int>();
            for (int z = -ParcelSearchRings; z <= ParcelSearchRings; z++)
            {
                for (int x = -ParcelSearchRings; x <= ParcelSearchRings; x++)
                {
                    parcels.Add(home + new Vector2Int(x, z));
                }
            }
            Vector2 Centre(Vector2Int parcel) => new Vector2((parcel.x + 0.5f) * parcelSize, (parcel.y + 0.5f) * parcelSize);
            parcels.Sort((a, b) => (Centre(a) - near).sqrMagnitude.CompareTo((Centre(b) - near).sqrMagnitude));
            foreach (Vector2Int parcel in parcels)
            {
                var area = new Rect(parcel.x * parcelSize, parcel.y * parcelSize, parcelSize, parcelSize);
                if (IsFree(plan, rules, area, roadPoints) && IsOpenLevel(area, open, heightAt))
                {
                    return area.center;
                }
            }
            return near;
        }

        // Off every settlement's land, with room for what it changes round its parcels, and off the roads.
        private static bool IsFree(WorldPlan plan, SettlementSiteRules rules, Rect area, List<Vector2> roadPoints)
        {
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                float half = SettlementLayout.SideOf(settlement.Type, plan.Rules.ParcelSide) * 0.5f + rules.Margin
                    + (settlement.IsQuarry ? QuarryYard.Reach(rules) : 0f);
                if (new Rect(settlement.Position.x - half, settlement.Position.y - half, half * 2f, half * 2f).Overlaps(area))
                {
                    return false;
                }
            }
            var clear = new Rect(area.x - RoadClearance, area.y - RoadClearance, area.width + RoadClearance * 2f, area.height + RoadClearance * 2f);
            return !roadPoints.Exists(clear.Contains);
        }

        private static bool IsOpenLevel(Rect area, Func<float, float, bool> open, Func<float, float, float> heightAt)
        {
            float low = float.MaxValue, high = float.MinValue;
            for (int j = 0; j <= 2; j++)
            {
                for (int i = 0; i <= 2; i++)
                {
                    float x = area.x + area.width * i * 0.5f, z = area.y + area.height * j * 0.5f;
                    if (open != null && !open(x, z))
                    {
                        return false;
                    }
                    float height = heightAt(x, z);
                    low = Mathf.Min(low, height);
                    high = Mathf.Max(high, height);
                }
            }
            return high - low <= ParcelMostRelief;
        }

        /// <summary>True when the survey cell at a position is mostly land.</summary>
        public static bool IsDry(ResourceSurvey survey, Vector2 position)
        {
            return survey.TryCell(position, out Vector2Int cell) && survey.Cell(cell.x, cell.y).Land > 0.9f;
        }
    }
}
