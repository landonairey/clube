namespace Clube.Core
{
    /// <summary>
    /// What the player wears says to shopkeepers (item 20, placeholder numbers to try out): naked or in rags &lt; cloth &lt;
    /// leather. Each worn item has <see cref="ItemDefinition.OutfitPoints"/> (rags 0, a cloth garment 2, a leather piece 3),
    /// and each point of the outfit is a point of disposition while trading: a full cloth outfit (shirt, pants, foot wraps)
    /// +6, full leather (helmet, gloves, cuirass, bracers, boots, leggings) +18.
    /// </summary>
    public static class Outfit
    {
        /// <summary>Disposition points for each outfit point.</summary>
        public const int DispositionPerPoint = 1;

        /// <summary>Outfit points above which it reads as leather: more than a full cloth outfit's 6.</summary>
        public const int ClothMost = 6;

        /// <summary>The disposition an outfit of <paramref name="points"/> adds with shopkeepers.</summary>
        public static int DispositionBonus(int points)
        {
            return points > 0 ? points * DispositionPerPoint : 0;
        }

        /// <summary>A word for the outfit: "rags", "cloth" or "leather".</summary>
        public static string Tier(int points)
        {
            return points <= 0 ? "rags" : points <= ClothMost ? "cloth" : "leather";
        }

        /// <summary>"cloth outfit, 6 pts".</summary>
        public static string Describe(int points)
        {
            return $"{Tier(points)} outfit, {points} pts";
        }
    }
}
