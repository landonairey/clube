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

        /// <summary>Real seconds it stands at each end of its road, loading.</summary>
        public float RestSeconds = 90f;

        /// <summary>How many of this kind run on each road of a class; a class not listed has none.</summary>
        public RoadClass[] Roads = Array.Empty<RoadClass>();

        /// <summary>
        /// On a dirt cart path (one cart to a road), the share of the draw this kind takes among the kinds that run there.
        /// </summary>
        public float DirtShare = 1f;

        /// <summary>The two kinds the user named: a hand cart on every cart path, a horse cart on the paved ones and on some dirt ones.</summary>
        public static List<CartKind> Defaults()
        {
            return new List<CartKind>
            {
                new CartKind
                {
                    Id = "hand-cart", Name = "Hand cart", HorseDrawn = false, Speed = 1.3f, RestSeconds = 90f,
                    Roads = new[] { RoadClass.DirtCartPath, RoadClass.PavedCartPath }, DirtShare = 0.6f,
                },
                new CartKind
                {
                    Id = "horse-cart", Name = "Horse cart", HorseDrawn = true, Speed = 2.1f, RestSeconds = 120f,
                    Roads = new[] { RoadClass.DirtCartPath, RoadClass.PavedCartPath }, DirtShare = 0.4f,
                },
            };
        }
    }

    /// <summary>Where a cart is on its road at a moment.</summary>
    public readonly struct CartPlace
    {
        public CartPlace(float fraction, bool outbound, bool moving, int trip)
        {
            Fraction = fraction;
            Outbound = outbound;
            Moving = moving;
            Trip = trip;
        }

        /// <summary>How far along its road it is, 0 at the settlement the road was laid from to 1 at its end.</summary>
        public float Fraction { get; }

        /// <summary>True on the way out (or standing at the far end, loaded there); false on the way back.</summary>
        public bool Outbound { get; }

        /// <summary>False while it stands at an end.</summary>
        public bool Moving { get; }

        /// <summary>Which one-way trip this is, counted from the clock's start: its load changes with it.</summary>
        public int Trip { get; }
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

        /// <summary>The road's length in metres.</summary>
        public float Length;

        /// <summary>Real seconds its timetable is shifted by, so carts are spread along their roads.</summary>
        public float Offset;

        /// <summary>Real seconds one way takes, and a whole round with both rests.</summary>
        public float OneWay => Length / Math.Max(0.05f, Kind.Speed);

        public float Round => 2f * (OneWay + Kind.RestSeconds);

        /// <summary>Where the cart is, <paramref name="seconds"/> real seconds (at the clock's normal rate) after the clock started.</summary>
        public CartPlace At(double seconds)
        {
            double round = Round;
            double shifted = seconds + Offset;
            double rounds = Math.Floor(shifted / round);
            float into = (float)(shifted - rounds * round);
            int trip = (int)(rounds * 2);
            float way = OneWay, rest = Kind.RestSeconds;
            if (into < way)
            {
                return new CartPlace(into / way, true, true, trip);
            }
            if (into < way + rest)
            {
                return new CartPlace(1f, true, false, trip);
            }
            if (into < 2f * way + rest)
            {
                return new CartPlace(1f - (into - way - rest) / way, false, true, trip + 1);
            }
            return new CartPlace(0f, false, false, trip + 1);
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

        public static List<CartRun> Plan(WorldPlan plan, IReadOnlyList<CartKind> kinds, int seed)
        {
            var runs = new List<CartRun>();
            foreach (PlannedRoad road in plan.Roads)
            {
                if (road.Length < ShortestRoad || road.Points.Count < 2)
                {
                    continue;
                }
                var random = new System.Random(unchecked(seed * 486187739 + road.Index * 7919 + 11));
                if (road.Class == RoadClass.PavedCartPath)
                {
                    foreach (CartKind kind in kinds)
                    {
                        if (Array.IndexOf(kind.Roads, road.Class) >= 0)
                        {
                            runs.Add(Run(runs.Count, road, kind, random));
                        }
                    }
                    continue;
                }
                CartKind drawn = Draw(kinds, road.Class, random);
                if (drawn != null)
                {
                    runs.Add(Run(runs.Count, road, drawn, random));
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

        private static CartRun Run(int index, PlannedRoad road, CartKind kind, System.Random random)
        {
            var run = new CartRun { Index = index, Road = road.Index, From = road.From, To = road.To, Kind = kind, Length = road.Length };
            run.Offset = (float)random.NextDouble() * run.Round;
            return run;
        }
    }
}
