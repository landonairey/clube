using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>The parts of a cart's or vessel's stop at a settlement (GL726), in order.</summary>
    public enum StopPhase
    {
        /// <summary>Drawing up from where its route ends to the shop it trades with.</summary>
        Arriving,
        /// <summary>Standing there while its crates are emptied.</summary>
        Unloading,
        /// <summary>Standing there while its crates are filled with what it carries next.</summary>
        Loading,
        /// <summary>Going back to its route.</summary>
        Leaving,
    }

    /// <summary>Where in its stop a cart or vessel is: the part, and how far through it (0 to 1).</summary>
    public readonly struct StopPlace
    {
        public StopPlace(StopPhase phase, float progress)
        {
            Phase = phase;
            Progress = progress;
        }

        public StopPhase Phase { get; }

        public float Progress { get; }
    }

    /// <summary>
    /// A stop at a settlement (GL726): "carts should pull up to the various shops that match the goods they are towing
    /// into town and offloading them... an unloading animation... then a loading animation which adds some new items to
    /// the crates... and then they get back on and get going"; vessels the same at their dock. The stop is the rest its
    /// timetable gives it at that end (<see cref="RunPlace.Rested"/>), split into the drive in, the unloading, the
    /// loading and the drive out, so it still follows from the clock alone.
    /// </summary>
    public static class TradeStop
    {
        /// <summary>The most of a stop each drive (in, and out) may take; a longer way in town is driven faster.</summary>
        public const float LegShare = 0.2f;

        /// <summary>Real seconds each drive of a stop takes: the way at <paramref name="speed"/>, at most <see cref="LegShare"/> of the rest.</summary>
        public static float LegSeconds(float wayLength, float speed, float rest)
        {
            return Math.Min(Math.Max(0f, wayLength) / Math.Max(0.05f, speed), Math.Max(0f, rest) * LegShare);
        }

        /// <summary>
        /// Where in its stop it is, <paramref name="rested"/> seconds into a rest of <paramref name="rest"/>, each drive
        /// taking <paramref name="leg"/> seconds (0 for a vessel at its dock): the time between them split evenly.
        /// </summary>
        public static StopPlace At(float rested, float rest, float leg)
        {
            rest = Math.Max(1e-3f, rest);
            leg = Math.Max(0f, Math.Min(leg, rest * 0.5f));
            rested = Math.Max(0f, Math.Min(rested, rest));
            float work = (rest - 2f * leg) * 0.5f;
            if (rested < leg)
            {
                return new StopPlace(StopPhase.Arriving, rested / leg);
            }
            rested -= leg;
            if (rested < work)
            {
                return new StopPlace(StopPhase.Unloading, rested / work);
            }
            rested -= work;
            if (rested < work || leg <= 0f)
            {
                return new StopPlace(StopPhase.Loading, work > 0f ? Math.Min(1f, rested / work) : 1f);
            }
            rested -= work;
            return new StopPlace(StopPhase.Leaving, Math.Min(1f, rested / leg));
        }

        /// <summary>
        /// Which of <paramref name="crates"/> crates hold goods at a place in the stop: those from <paramref name="first"/>
        /// up to (not with) <paramref name="last"/>; <paramref name="arrived"/> when they are what it came with, false
        /// when they are its next load. Unloading empties them from the front; loading fills them from the front.
        /// </summary>
        public static void CratesHeld(StopPlace place, int crates, out int first, out int last, out bool arrived)
        {
            int done = (int)Math.Floor(place.Progress * crates + 1e-4f);
            switch (place.Phase)
            {
                case StopPhase.Arriving:
                    first = 0;
                    last = crates;
                    arrived = true;
                    break;
                case StopPhase.Unloading:
                    first = Math.Min(done, crates);
                    last = crates;
                    arrived = true;
                    break;
                case StopPhase.Loading:
                    first = 0;
                    last = Math.Min(done, crates);
                    arrived = false;
                    break;
                default:
                    first = 0;
                    last = crates;
                    arrived = false;
                    break;
            }
        }

        /// <summary>
        /// The shop of <paramref name="at"/> that matches what comes from <paramref name="from"/>: the one that buys the
        /// most of what its shops sell, then one of the same supply chain; -1 when none matches.
        /// </summary>
        public static int ShopFor(PlannedSettlement from, PlannedSettlement at)
        {
            if (from == null || at == null || at.Shops.Count == 0)
            {
                return -1;
            }
            var sold = new HashSet<string>();
            var chains = new HashSet<string>();
            foreach (PlannedShop shop in from.Shops)
            {
                chains.Add(shop.Specialty.Chain);
                foreach (TradeGood good in shop.Specialty.Sells)
                {
                    sold.Add(Key(good));
                }
            }
            int best = -1;
            float bestScore = 0f;
            for (int i = 0; i < at.Shops.Count; i++)
            {
                EconomySpecialty specialty = at.Shops[i].Specialty;
                float score = chains.Contains(specialty.Chain) ? 0.5f : 0f;
                foreach (TradeGood good in specialty.Buys)
                {
                    score += sold.Contains(Key(good)) ? 1f : 0f;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        private static string Key(TradeGood good)
        {
            return good.InGame ? good.ItemId : good.Name;
        }
    }
}
