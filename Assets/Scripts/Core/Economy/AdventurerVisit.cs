namespace Clube.Core
{
    /// <summary>
    /// When the lone adventurer comes and what they trade with (GL476): they arrive at sunset on the fifth day, so their
    /// fire is seen on the fifth night (GL585; it was the dawn after, at first), and stay, buying with one purse of
    /// <see cref="Purse"/> coins that only the player's purchases refill.
    /// </summary>
    public static class AdventurerVisit
    {
        /// <summary>The day (counted from 1) at whose sunset they come.</summary>
        public const int ArrivalDay = 5;

        /// <summary>The coins they start with.</summary>
        public const float Purse = 10000f;

        /// <summary>How much over the price list they ask for what they sell (a shop asks about 2).</summary>
        public const float SellMarkup = 2.5f;

        /// <summary>The coins their climbing upgrade costs (GL585).</summary>
        public const int ClimbingPrice = 1000;

        /// <summary>
        /// True from sunset (<paramref name="sunsetHour"/>) on <see cref="ArrivalDay"/>. <paramref name="day"/> counts from 1
        /// (the first day's morning). It only ever turns true as the days pass, so a camp that had arrived stays.
        /// </summary>
        public static bool HasArrived(int day, float hour, float sunsetHour)
        {
            return day > ArrivalDay || (day == ArrivalDay && hour >= sunsetHour);
        }
    }
}
