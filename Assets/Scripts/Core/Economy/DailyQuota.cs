namespace Clube.Core
{
    /// <summary>
    /// The stone handed in on the current day against that day's quota (GL544, GL555). Each day starts again from zero, and day N
    /// needs <see cref="CompanyWork.Quota"/> (1024 x 2^(N-1), GL555) mined that day alone, not a running total. Plain C#, so it can be tested.
    /// </summary>
    public class DailyQuota
    {
        /// <summary>The day the count belongs to (1-based).</summary>
        public int Day { get; private set; } = 1;

        /// <summary>Stone handed in on <see cref="Day"/>.</summary>
        public int Stone { get; private set; }

        /// <summary>Stone that must be handed in on <see cref="Day"/>.</summary>
        public int Required => CompanyWork.Quota(Day);

        /// <summary>True once today's quota is met.</summary>
        public bool IsMet => Stone >= Required;

        /// <summary>Moves to a day; a new day starts the count over.</summary>
        public void Advance(int day)
        {
            day = System.Math.Max(1, day);
            if (day != Day)
            {
                Day = day;
                Stone = 0;
            }
        }

        /// <summary>Counts stone handed in on a day (starting that day's count if it is a new one).</summary>
        public void Add(int day, int stone)
        {
            Advance(day);
            Stone += System.Math.Max(0, stone);
        }

        /// <summary>Sets the count from a saved game.</summary>
        public void Restore(int day, int stone)
        {
            Day = System.Math.Max(1, day);
            Stone = System.Math.Max(0, stone);
        }
    }
}
