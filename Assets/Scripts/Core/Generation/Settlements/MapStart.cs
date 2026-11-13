using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where a new arrival is set down on a bounded map (GL601): by the capital's edge, else by the largest friendly
    /// settlement, else on the flat dry land nearest the middle of the map.
    /// </summary>
    public static class MapStart
    {
        /// <summary>Metres from the settlement's middle the start stands, towards the map's middle.</summary>
        public const float Offset = 24f;

        /// <summary>The start's place on the map (x, z); always on a survey cell that is mostly dry when the map has one.</summary>
        public static Vector2 Pick(WorldPlan plan, ResourceSurvey survey)
        {
            PlannedSettlement home = plan != null ? plan.Capital : null;
            if (home == null && plan != null)
            {
                foreach (PlannedSettlement settlement in plan.Settlements)
                {
                    if (!settlement.Type.Hostile && (home == null || settlement.Type.Parcels > home.Type.Parcels))
                    {
                        home = settlement;
                    }
                }
            }
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
