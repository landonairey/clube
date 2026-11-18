using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL734: the stone quarry pit beside the tutorial's quarry settlement (<see cref="PlannedSettlement.IsQuarry"/>), where
    /// the opening tutorial is played: a level floor the size of the tutorial's yard (<see cref="CompanyWork.Yard"/>: the
    /// warehouse at its south end, the work fields north of it), sunk <see cref="SettlementSiteRules.QuarryDepth"/> under
    /// the lowest of the land round it so stone faces close it in, and all of it stone (<see cref="StoneDepth"/> down). It is
    /// one of the site's terraces, so the settlement's writer levels it and its town wall rings it.
    /// </summary>
    public sealed class QuarryYard
    {
        /// <summary>Metres under the floor the quarry's ground is stone.</summary>
        public const float StoneDepth = 6f;

        // Places tried for the pit: rings out from the settlement, and slides along each side either way of its middle.
        private const int QuarryRings = 3;
        private const float QuarryRingStep = 16f;
        private const int QuarrySlides = 3;
        private const float QuarrySlideStep = 16f;

        /// <summary>The floor (x, z): <see cref="CompanyWork.Yard"/>'s size, north up.</summary>
        public Rect Area;

        /// <summary>The floor's height.</summary>
        public float Floor;

        /// <summary>Its terrace in <see cref="SettlementSite.Terraces"/>.</summary>
        public int Terrace;

        /// <summary>
        /// Where the tutorial's map space (<see cref="CompanyWork"/>, whose ground is <see cref="CompanyWork.Ground"/>) puts
        /// its origin in the world: the tutorial's coordinates plus this are the quarry's.
        /// </summary>
        public Vector3 Origin => new Vector3(Area.xMin - CompanyWork.Yard.xMin, Floor - CompanyWork.Ground, Area.yMin - CompanyWork.Yard.yMin);

        /// <summary>A point of the tutorial's map space, in the world.</summary>
        public Vector3 ToWorld(Vector3 company)
        {
            return company + Origin;
        }

        /// <summary>Metres the quarry's walls and its stone reach past its floor: the banks round it.</summary>
        public static float Reach(SettlementSiteRules rules)
        {
            return rules.QuarryGap + (QuarryRings - 1) * QuarryRingStep + QuarrySlides * QuarrySlideStep
                + Mathf.Max(CompanyWork.Yard.width, CompanyWork.Yard.height) + rules.Ramp + rules.WallSetback + 4f;
        }

        /// <summary>
        /// Finds the yard's place beside a laid-out settlement: on one of its four sides, <see cref="SettlementSiteRules.QuarryGap"/>
        /// past everything it changes, clear of planned roads and closed ground, on the most level land; null when no side has room.
        /// </summary>
        /// <param name="heightAt">The land's height at (x, z), as generated.</param>
        /// <param name="closedAt">True where nothing may be built (water, ground the player is held back from); may be null.</param>
        public static QuarryYard Plan(SettlementSite site, Func<float, float, float> heightAt, Func<float, float, bool> closedAt, float voxel)
        {
            SettlementSiteRules rules = site.Rules;
            Rect bounds = site.Bounds;
            Vector2 size = CompanyWork.Yard.size;
            float gap = rules.QuarryGap + rules.Ramp;
            Vector2 centre = bounds.center;
            QuarryYard best = null;
            float bestScore = float.MaxValue;
            // Each side of the settlement (north, south, east, west), sliding along it, and in rings further out.
            for (int ring = 0; ring < QuarryRings; ring++)
            {
                float away = gap + ring * QuarryRingStep;
                for (int side = 0; side < 4; side++)
                {
                    for (int s = -QuarrySlides; s <= QuarrySlides; s++)
                    {
                        float slide = s * QuarrySlideStep;
                        Vector2 corner = side switch
                        {
                            0 => new Vector2(centre.x - size.x * 0.5f + slide, bounds.yMax + away),
                            1 => new Vector2(centre.x - size.x * 0.5f + slide, bounds.yMin - away - size.y),
                            2 => new Vector2(bounds.xMax + away, centre.y - size.y * 0.5f + slide),
                            _ => new Vector2(bounds.xMin - away - size.x, centre.y - size.y * 0.5f + slide),
                        };
                        var area = new Rect(Snap(corner.x, voxel), Snap(corner.y, voxel), size.x, size.y);
                        if (!Survey(site, area, rules.Ramp, heightAt, closedAt, out float low, out float high))
                        {
                            continue;
                        }
                        // The most level ground, nearest the settlement: a metre of relief weighs as much as 4 m further out.
                        float score = (high - low) + (ring * QuarryRingStep + Mathf.Abs(slide)) * 0.25f;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = new QuarryYard { Area = area, Floor = Snap(low - rules.QuarryDepth, voxel) };
                            best.natural = new Vector2(low, high);
                        }
                    }
                }
            }
            if (best == null)
            {
                site.Notes.Add($"{site.Settlement.Name}: no room beside it for the quarry pit.");
                return null;
            }
            best.Terrace = site.Terraces.Count;
            site.Terraces.Add(new SettlementTerrace
            {
                Area = best.Area, Height = best.Floor, NaturalLow = best.natural.x, NaturalHigh = best.natural.y, Row = -1,
            });
            site.Quarry = best;
            return best;
        }

        private Vector2 natural;

        // The land under the yard and its banks: false where any of it is closed or a planned road's.
        private static bool Survey(SettlementSite site, Rect area, float ramp, Func<float, float, float> heightAt, Func<float, float, bool> closedAt,
            out float low, out float high)
        {
            low = float.MaxValue;
            high = float.MinValue;
            const float step = 4f;
            for (float x = area.xMin - ramp; x <= area.xMax + ramp; x += step)
            {
                for (float z = area.yMin - ramp; z <= area.yMax + ramp; z += step)
                {
                    if ((closedAt != null && closedAt(x, z)) || site.RoadBeyond(x, z) <= 0f)
                    {
                        return false;
                    }
                    float h = heightAt(x, z);
                    low = Mathf.Min(low, h);
                    high = Mathf.Max(high, h);
                }
            }
            return true;
        }

        private static float Snap(float value, float voxel)
        {
            return Mathf.Round(value / voxel) * voxel;
        }
    }
}
