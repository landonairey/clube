using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>What a thieves' boat is about.</summary>
    public enum RaiderPhase
    {
        /// <summary>Lying in its cove by the camp.</summary>
        Moored,
        /// <summary>On its way out to the water route.</summary>
        Out,
        /// <summary>At the route, waiting for a vessel to come by.</summary>
        Waiting,
        /// <summary>Alongside a vessel, taking its goods.</summary>
        Boarding,
        /// <summary>On its way back to the cove.</summary>
        Home,
    }

    /// <summary>
    /// A thieves' camp's boat (GL617: "water-based pirates that go out and intercept sea vessels and/or river boat
    /// vessels for their goods"): from the camp's own water (<see cref="Cove"/>) out to the nearest place on a water
    /// route (<see cref="Spot"/>) while its crew's duty is a sortie, and back when the sortie is over or it has a haul.
    /// How far out it is (<see cref="Along"/>) moves with the clock (<see cref="Step"/>); the boarding itself is the
    /// game's, since it needs the vessels in the world.
    /// </summary>
    public sealed class RaiderRun
    {
        public int Index;

        /// <summary>The camp it sails from (a settlement of the plan).</summary>
        public int Camp;

        public VesselClass Class;

        /// <summary>The water route it preys on.</summary>
        public int Route;

        public Vector2 Cove, Spot;

        /// <summary>Metres from the cove along its way to the spot.</summary>
        public float Along;

        public RaiderPhase Phase = RaiderPhase.Moored;

        /// <summary>True from a boarding that took something until the haul is landed at the cove.</summary>
        public bool Loaded;

        /// <summary>
        /// Moves it for <paramref name="seconds"/> real seconds on a way <paramref name="length"/> metres long: out and
        /// then waiting while <paramref name="sortie"/> (and it has no haul to land), otherwise home. True the moment
        /// it comes home with a haul (which the caller lands, clearing <see cref="Loaded"/>).
        /// </summary>
        public bool Step(bool sortie, float length, float seconds)
        {
            if (Phase == RaiderPhase.Boarding)
            {
                return false;
            }
            float move = Class.Speed * Mathf.Max(0f, seconds);
            if (sortie && !Loaded)
            {
                Along = Mathf.Min(length, Along + move);
                Phase = Along >= length - 0.01f ? RaiderPhase.Waiting : RaiderPhase.Out;
                return false;
            }
            Along = Mathf.Max(0f, Along - move);
            Phase = Along <= 0.01f ? RaiderPhase.Moored : RaiderPhase.Home;
            return Phase == RaiderPhase.Moored && Loaded;
        }
    }

    /// <summary>
    /// The thieves' boats of a map (GL617): every thieves' camp with water of its own (<see cref="PlannedSettlement.Harbour"/>,
    /// <see cref="PlannedSettlement.Landing"/>) and a water route within reach of it gets one boat, of the raider class
    /// of that water. The same plan gives the same boats.
    /// </summary>
    public static class RaiderRuns
    {
        public static List<RaiderRun> Plan(WorldPlan plan, IReadOnlyList<VesselClass> classes, float cellSize, float range)
        {
            var runs = new List<RaiderRun>();
            foreach (PlannedSettlement camp in plan.Settlements)
            {
                if (!camp.Type.Hostile || !ThiefHaunts.TryIntercept(plan, camp, cellSize, range, out WaterRouteKind kind, out Vector2 cove, out Vector2 spot, out int route))
                {
                    continue;
                }
                VesselClass its = null;
                foreach (VesselClass each in classes)
                {
                    its = its == null && each.Raider && each.Kind == kind ? each : its;
                }
                if (its != null)
                {
                    runs.Add(new RaiderRun { Index = runs.Count, Camp = camp.Index, Class = its, Route = route, Cove = cove, Spot = spot });
                }
            }
            return runs;
        }
    }
}
