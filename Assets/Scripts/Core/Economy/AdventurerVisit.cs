namespace Clube.Core
{
    /// <summary>
    /// When the lone adventurer comes and what they trade with (GL476): they arrive at the dawn after the fifth night and
    /// stay, buying with one purse of <see cref="Purse"/> coins that only the player's purchases refill.
    /// </summary>
    public static class AdventurerVisit
    {
        /// <summary>The nights that pass before they come.</summary>
        public const int Nights = 5;

        /// <summary>The coins they start with.</summary>
        public const float Purse = 10000f;

        /// <summary>How much over the price list they ask for what they sell (a shop asks about 2).</summary>
        public const float SellMarkup = 2.5f;

        /// <summary>
        /// True from the dawn (<paramref name="sunriseHour"/>) after the fifth night. <paramref name="day"/> counts from 1
        /// (the first day's morning), so the fifth night ends on the morning of day 6.
        /// </summary>
        public static bool HasArrived(int day, float hour, float sunriseHour)
        {
            int dawnDay = Nights + 1;
            return day > dawnDay || (day == dawnDay && hour >= sunriseHour);
        }
    }
}
