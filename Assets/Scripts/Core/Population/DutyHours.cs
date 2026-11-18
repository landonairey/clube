using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// The game hours an occupation spends on a work between two times on the clock (GL730), read off its week of
    /// <see cref="DutyShift"/>s. Times are hours since midnight before day 1 (a Monday). The week is looked at in five
    /// minute steps once per occupation and work, then any stretch of time is whole weeks plus a part of one.
    /// </summary>
    public sealed class DutyHours
    {
        /// <summary>Steps an hour is looked at in.</summary>
        public const int StepsPerHour = 12;

        private const int StepsPerWeek = Occupation.DaysInWeek * 24 * StepsPerHour;

        private readonly IReadOnlyList<Duty> duties;
        private readonly Dictionary<(Occupation, string), int[]> weeks = new Dictionary<(Occupation, string), int[]>();

        /// <param name="duties">The duty rows the occupations' shifts name.</param>
        public DutyHours(IReadOnlyList<Duty> duties)
        {
            this.duties = duties;
        }

        /// <summary>Hours an occupation is on duties doing <paramref name="work"/> between two times (hours on the clock).</summary>
        public double Between(Occupation occupation, string work, double from, double to)
        {
            if (occupation == null || to <= from)
            {
                return 0d;
            }
            return Before(occupation, work, to) - Before(occupation, work, Math.Max(0d, from));
        }

        /// <summary>Hours on the work from the start of the clock up to a time.</summary>
        public double Before(Occupation occupation, string work, double time)
        {
            if (occupation == null || time <= 0d)
            {
                return 0d;
            }
            int[] week = Week(occupation, work);
            long steps = (long)Math.Floor(time * StepsPerHour);
            long whole = steps / StepsPerWeek;
            int part = (int)(steps % StepsPerWeek);
            return (whole * week[StepsPerWeek] + week[part]) / (double)StepsPerHour;
        }

        /// <summary>Hours a week an occupation is on the work.</summary>
        public double Weekly(Occupation occupation, string work)
        {
            return occupation == null ? 0d : Week(occupation, work)[StepsPerWeek] / (double)StepsPerHour;
        }

        // The steps of the week spent on the work before each step: a running count, StepsPerWeek + 1 long.
        private int[] Week(Occupation occupation, string work)
        {
            if (weeks.TryGetValue((occupation, work), out int[] week))
            {
                return week;
            }
            week = new int[StepsPerWeek + 1];
            for (int step = 0; step < StepsPerWeek; step++)
            {
                int day = step / (24 * StepsPerHour);
                float hour = (step % (24 * StepsPerHour) + 0.5f) / StepsPerHour;
                Duty duty = Duty.Find(duties, occupation.DutyAt(day, hour));
                week[step + 1] = week[step] + (duty != null && duty.Work == work ? 1 : 0);
            }
            weeks[(occupation, work)] = week;
            return week;
        }
    }
}
