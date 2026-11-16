using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How high a road's bridge over the sea must stand (GL651: "mind the sea vessel sizes and their routes ... build the
    /// bridge up high enough to allow them to fit underneath"): where one of the plan's sea routes passes near, the
    /// tallest sea-going vessel class, mast and all, with a margin; elsewhere room for a small boat.
    /// </summary>
    public static class BridgeClearance
    {
        /// <summary>Metres over the sea of a deck no route passes under: a rowed boat and the man standing in it.</summary>
        public const float SmallCraft = 3f;

        /// <summary>Metres over a mast's top.</summary>
        public const float Margin = 0.8f;

        /// <summary>
        /// Metres from a deck's middle within which a planned sea route counts as passing under it: the plan's routes run
        /// through 40 m cells, and a vessel's lane on the real water may lie a corridor's width off them.
        /// </summary>
        public const float RouteReach = 240f;

        /// <summary>The clearance the tallest of the sea-going classes needs.</summary>
        public static float Tallest(IReadOnlyList<VesselClass> classes)
        {
            float tallest = SmallCraft;
            foreach (VesselClass vessel in classes)
            {
                if (vessel.Kind == WaterRouteKind.Sea)
                {
                    tallest = Mathf.Max(tallest, BoatMeshes.AirDraught(vessel) + Margin);
                }
            }
            return tallest;
        }

        /// <summary>The clearance a deck over the sea needs, by its middle (x, z), for a plan's sea routes.</summary>
        public static Func<Vector2, float> For(WorldPlan plan, IReadOnlyList<VesselClass> classes)
        {
            float tallest = Tallest(classes);
            return middle =>
            {
                foreach (WaterRoute route in plan.WaterRoutes)
                {
                    if (route.Kind != WaterRouteKind.Sea)
                    {
                        continue;
                    }
                    for (int i = 0; i + 1 < route.Points.Count; i++)
                    {
                        if (SettlementSite.Distance(middle, route.Points[i], route.Points[i + 1]) <= RouteReach)
                        {
                            return tallest;
                        }
                    }
                }
                return SmallCraft;
            };
        }
    }
}
