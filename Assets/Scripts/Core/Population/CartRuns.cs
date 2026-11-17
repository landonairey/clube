using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// A kind of cart (GL616), as a row: what pulls it, how fast it goes, how long it stands at each end, and which road
    /// classes it runs on ("carts are both horse drawn and hand pulled"). A new kind is a new row.
    /// </summary>
    public sealed class CartKind
    {
        public string Id;
        public string Name;

        /// <summary>True when a horse is between the shafts and the carter leads it; false when the carter pulls.</summary>
        public bool HorseDrawn;

        /// <summary>Metres a real second on the road, at the clock's normal rate.</summary>
        public float Speed = 1.3f;

        /// <summary>Real seconds it stands at each end of its road: drawing up to a shop, unloading, loading and back (GL726).</summary>
        public float RestSeconds = 90f;

        /// <summary>How many of this kind run on each road of a class; a class not listed has none.</summary>
        public RoadClass[] Roads = Array.Empty<RoadClass>();

        /// <summary>
        /// On a dirt cart path (one cart to a road), the share of the draw this kind takes among the kinds that run there.
        /// </summary>
        public float DirtShare = 1f;

        /// <summary>
        /// GL728: how many of this kind run on each paved cart path, spread evenly round their timetable ("horse drawn
        /// carts can occupy the larger paved trade routes").
        /// </summary>
        public int PavedCount = 1;

        /// <summary>The two kinds the user named: a hand cart on every cart path, horse carts on the paved ones (two each, GL728) and on some dirt ones.</summary>
        public static List<CartKind> Defaults()
        {
            return new List<CartKind>
            {
                new CartKind
                {
                    Id = "hand-cart", Name = "Hand cart", HorseDrawn = false, Speed = 1.3f, RestSeconds = 150f,
                    Roads = new[] { RoadClass.DirtCartPath, RoadClass.PavedCartPath }, DirtShare = 0.6f,
                },
                new CartKind
                {
                    Id = "horse-cart", Name = "Horse cart", HorseDrawn = true, Speed = 2.1f, RestSeconds = 180f,
                    Roads = new[] { RoadClass.DirtCartPath, RoadClass.PavedCartPath }, DirtShare = 0.4f, PavedCount = 2,
                },
            };
        }
    }

    /// <summary>Where something that plies a route (a cart on its road, a boat on its water route) is at a moment.</summary>
    public readonly struct RunPlace
    {
        public RunPlace(float fraction, bool outbound, bool moving, int trip, float rested = 0f)
        {
            Fraction = fraction;
            Outbound = outbound;
            Moving = moving;
            Trip = trip;
            Rested = rested;
        }

        /// <summary>GL726: real seconds it has stood at the end it is at; 0 while moving.</summary>
        public float Rested { get; }

        /// <summary>How far along its route it is, 0 at the settlement the route was laid from to 1 at its end.</summary>
        public float Fraction { get; }

        /// <summary>True on the way out (or standing at the far end, loaded there); false on the way back.</summary>
        public bool Outbound { get; }

        /// <summary>False while it stands at an end.</summary>
        public bool Moving { get; }

        /// <summary>Which one-way trip this is, counted from the clock's start: its load changes with it.</summary>
        public int Trip { get; }

        /// <summary>
        /// The place on a there-and-back timetable, <paramref name="seconds"/> after the clock started: out in
        /// <paramref name="oneWay"/> seconds, a rest of <paramref name="rest"/>, back, a rest, and round again, the whole
        /// shifted by <paramref name="offset"/>.
        /// </summary>
        public static RunPlace At(double seconds, float offset, float oneWay, float rest)
        {
            double round = 2.0 * (oneWay + rest);
            double shifted = seconds + offset;
            double rounds = Math.Floor(shifted / round);
            float into = (float)(shifted - rounds * round);
            int trip = (int)(rounds * 2);
            if (into < oneWay)
            {
                return new RunPlace(into / oneWay, true, true, trip);
            }
            if (into < oneWay + rest)
            {
                return new RunPlace(1f, true, false, trip, into - oneWay);
            }
            if (into < 2f * oneWay + rest)
            {
                return new RunPlace(1f - (into - oneWay - rest) / oneWay, false, true, trip + 1);
            }
            return new RunPlace(0f, false, false, trip + 1, into - 2f * oneWay - rest);
        }
    }

    /// <summary>
    /// One cart's run (GL616): up and down one cart path for good, standing a while at each end. Where it is follows
    /// from the clock alone (<see cref="At"/>), so nothing is saved and a cart is where it should be whenever the
    /// player comes back to the road.
    /// </summary>
    public sealed class CartRun
    {
        public int Index;

        /// <summary>The road of the plan it runs on, and the settlements at its ends (<see cref="To"/> -1 where the road ends on another road).</summary>
        public int Road;
        public int From, To;

        public CartKind Kind;

        /// <summary>The road's length in metres, and for a road that leaves the map the way on past its end (GL725).</summary>
        public float Length;

        /// <summary>GL725: true when its road leaves the map: its far end is the larger world, past the map's border.</summary>
        public bool LeavesMap;

        /// <summary>GL725: the share of <see cref="Length"/> that lies past the road's end, off the map.</summary>
        public float OffMapShare;

        /// <summary>Real seconds its timetable is shifted by, so carts are spread along their roads.</summary>
        public float Offset;

        /// <summary>Real seconds one way takes, and a whole round with both rests.</summary>
        public float OneWay => Length / Math.Max(0.05f, Kind.Speed);

        public float Round => 2f * (OneWay + Kind.RestSeconds);

        /// <summary>Where the cart is, <paramref name="seconds"/> real seconds (at the clock's normal rate) after the clock started.</summary>
        public RunPlace At(double seconds)
        {
            return RunPlace.At(seconds, Offset, OneWay, Kind.RestSeconds);
        }
    }

    /// <summary>
    /// The carts of a map (GL616): every cart path of the <see cref="WorldPlan"/> long enough to be worth a trip gets its
    /// carts: on a paved one ("meant for 2 way traffic carts") one of each kind that runs there, on a dirt one a single
    /// cart whose kind the seed draws. The same plan and seed give the same carts.
    /// </summary>
    public static class CartRuns
    {
        /// <summary>The shortest road, in metres, that gets a cart.</summary>
        public const float ShortestRoad = 80f;

        /// <param name="offMapLength">
        /// GL725: metres a road that leaves the map runs on past its end (<see cref="OffMap"/>), added to its carts' trips;
        /// null for none.
        /// </param>
        public static List<CartRun> Plan(WorldPlan plan, IReadOnlyList<CartKind> kinds, int seed, Func<PlannedRoad, float> offMapLength = null)
        {
            var runs = new List<CartRun>();
            foreach (PlannedRoad road in plan.Roads)
            {
                if (road.Length < ShortestRoad || road.Points.Count < 2)
                {
                    continue;
                }
                var random = new System.Random(unchecked(seed * 486187739 + road.Index * 7919 + 11));
                float beyond = road.LeavesMap && offMapLength != null ? Math.Max(0f, offMapLength(road)) : 0f;
                if (road.Class == RoadClass.PavedCartPath)
                {
                    foreach (CartKind kind in kinds)
                    {
                        if (Array.IndexOf(kind.Roads, road.Class) < 0)
                        {
                            continue;
                        }
                        CartRun first = null;
                        for (int k = 0; k < Math.Max(1, kind.PavedCount); k++)
                        {
                            CartRun run = Run(runs.Count, road, kind, beyond, random);
                            if (first == null)
                            {
                                first = run;
                            }
                            else
                            {
                                // The rest of the kind spread evenly round the first's timetable.
                                run.Offset = (first.Offset + k * first.Round / kind.PavedCount) % first.Round;
                            }
                            runs.Add(run);
                        }
                    }
                    continue;
                }
                CartKind drawn = Draw(kinds, road.Class, random);
                if (drawn != null)
                {
                    runs.Add(Run(runs.Count, road, drawn, beyond, random));
                }
            }
            return runs;
        }

        private static CartKind Draw(IReadOnlyList<CartKind> kinds, RoadClass roadClass, System.Random random)
        {
            float total = 0f;
            foreach (CartKind kind in kinds)
            {
                total += Array.IndexOf(kind.Roads, roadClass) >= 0 ? Math.Max(0f, kind.DirtShare) : 0f;
            }
            if (total <= 0f)
            {
                return null;
            }
            float roll = (float)random.NextDouble() * total;
            CartKind last = null;
            foreach (CartKind kind in kinds)
            {
                if (Array.IndexOf(kind.Roads, roadClass) < 0 || kind.DirtShare <= 0f)
                {
                    continue;
                }
                last = kind;
                roll -= kind.DirtShare;
                if (roll < 0f)
                {
                    return kind;
                }
            }
            return last;
        }

        private static CartRun Run(int index, PlannedRoad road, CartKind kind, float beyond, System.Random random)
        {
            var run = new CartRun
            {
                Index = index, Road = road.Index, From = road.From, To = road.To, Kind = kind, Length = road.Length + beyond, LeavesMap = road.LeavesMap,
                OffMapShare = road.Length + beyond > 0f ? beyond / (road.Length + beyond) : 0f,
            };
            run.Offset = (float)random.NextDouble() * run.Round;
            return run;
        }
    }
}
