using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// One vessel's run (GL618): up and down one water route for good, lying a while at each end. Where it is follows
    /// from the clock alone (<see cref="At"/>), as a cart's does, so nothing is saved.
    /// </summary>
    public sealed class BoatRun
    {
        public int Index;

        /// <summary>The water route of the plan it sails, and the settlements at its ends (<see cref="To"/> -1 for a route out to the open sea).</summary>
        public int Route;
        public int From, To;

        public VesselClass Class;

        /// <summary>The vessel's own name ("Edmund Fitzgerald", or one drawn for it).</summary>
        public string Name;

        /// <summary>What a named vessel carries, when it carries one thing ("iron ore"); null for general goods.</summary>
        public string Cargo;

        /// <summary>Words that pick its cargo among the items (any, in the item's lower-case name); empty for general goods.</summary>
        public string[] CargoWords = Array.Empty<string>();

        /// <summary>The route's length in metres, as planned.</summary>
        public float Length;

        /// <summary>Real seconds its timetable is shifted by.</summary>
        public float Offset;

        public float OneWay => Length / Math.Max(0.05f, Class.Speed);

        public float Round => 2f * (OneWay + Class.RestSeconds);

        /// <summary>Where the vessel is, <paramref name="seconds"/> real seconds (at the clock's normal rate) after the clock started.</summary>
        public RunPlace At(double seconds)
        {
            return RunPlace.At(seconds, Offset, OneWay, Class.RestSeconds);
        }
    }

    /// <summary>
    /// The vessels of a map (GL618): every water route of the <see cref="WorldPlan"/> gets the boats of its kind's
    /// classes (a river boat on a river route, a sea trader on a sea route), and a route with a named vessel
    /// (GL597: the <i>Edmund Fitzgerald</i>) that vessel as well, of the class its row names. The same plan and seed
    /// give the same vessels.
    /// </summary>
    public static class BoatRuns
    {
        /// <summary>The shortest route, in metres, worth a boat.</summary>
        public const float ShortestRoute = 80f;

        private static readonly string[] Names =
        {
            "Heron", "Grey Gull", "Otter", "Marigold", "Kestrel", "Bluebell", "Tern", "Willow", "Petrel", "Morning Star", "Cormorant", "Fair Wind",
            "Sandpiper", "Rosalind", "Mayfly", "Halcyon", "Teal", "Northern Light", "Curlew", "Patience",
        };

        public static List<BoatRun> Plan(WorldPlan plan, IReadOnlyList<VesselClass> classes, int seed)
        {
            var runs = new List<BoatRun>();
            for (int r = 0; r < plan.WaterRoutes.Count; r++)
            {
                WaterRoute route = plan.WaterRoutes[r];
                if (route.Length < ShortestRoute || route.Points.Count < 2)
                {
                    continue;
                }
                var random = new Random(unchecked(seed * 486187739 + r * 7919 + 23));
                if (!string.IsNullOrEmpty(route.Vessel))
                {
                    NamedVessel named = plan.Rules != null ? plan.Rules.Vessels.Find(vessel => vessel.Name == route.Vessel) : null;
                    VesselClass its = named != null ? VesselClass.Find(classes, named.Class) : null;
                    if (its != null && its.Kind == route.Kind)
                    {
                        BoatRun run = Run(runs.Count, r, route, its, route.Vessel, random);
                        run.Cargo = route.Cargo;
                        run.CargoWords = named.OreWords;
                        runs.Add(run);
                    }
                }
                foreach (VesselClass each in classes)
                {
                    for (int n = 0; each.Kind == route.Kind && !each.Raider && n < each.PerRoute; n++)
                    {
                        runs.Add(Run(runs.Count, r, route, each, Names[random.Next(Names.Length)], random));
                    }
                }
            }
            return runs;
        }

        private static BoatRun Run(int index, int routeIndex, WaterRoute route, VesselClass its, string name, Random random)
        {
            var run = new BoatRun { Index = index, Route = routeIndex, From = route.From, To = route.To, Class = its, Name = name, Length = route.Length };
            run.Offset = (float)random.NextDouble() * run.Round;
            return run;
        }
    }
}
