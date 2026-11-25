using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The rules of trading with and stealing from a travelling sherpa (GL868: "You can steal from their backpacks that
    /// they are wearing"). They sell what is in their pack dearer than a shop and buy with a small purse. A hand in the
    /// pack goes unnoticed only while they walk on with the thief behind them; standing (at a stop, or trading) or with
    /// the thief where they can see, they notice, and a noticed theft costs their trust and the trust of the shop the
    /// goods were for.
    /// </summary>
    public static class SherpaTrade
    {
        /// <summary>What a sherpa asks for their goods, times the listed price: more than a shop, less than the adventurer.</summary>
        public const float SellMarkup = 1.5f;

        /// <summary>Coins a sherpa has to buy with when met.</summary>
        public const float Purse = 300f;

        /// <summary>Half the angle, in degrees, either side of where a walking sherpa faces that they can see.</summary>
        public const float SightHalfAngle = 100f;

        /// <summary>A noticed theft costs the sherpa's own disposition this many times <see cref="Theft.PointsFor"/>.</summary>
        public const int OwnPointsFactor = 2;

        /// <summary>
        /// True when the sherpa notices a hand in their pack: always while <paramref name="walking"/> is false; while they
        /// walk, when the thief (<paramref name="toThief"/>, from the sherpa, flat) is within <see cref="SightHalfAngle"/>
        /// of <paramref name="facing"/>. Right on top of them counts as seen.
        /// </summary>
        public static bool Notices(bool walking, Vector2 facing, Vector2 toThief)
        {
            if (!walking || facing.sqrMagnitude < 1e-6f || toThief.sqrMagnitude < 1e-4f)
            {
                return true;
            }
            return Vector2.Angle(facing, toThief) <= SightHalfAngle;
        }

        /// <summary>A sherpa whose disposition has fallen below this will not trade with the player at all.</summary>
        public const int RefuseBelow = 5;

        /// <summary>True while a sherpa at <paramref name="disposition"/> still trades with the player.</summary>
        public static bool Trades(int disposition)
        {
            return disposition >= RefuseBelow;
        }

        /// <summary>The disposition a noticed theft of goods worth <paramref name="coins"/> costs the sherpa themself.</summary>
        public static int OwnPointsFor(float coins)
        {
            return OwnPointsFactor * Theft.PointsFor(Mathf.Max(coins, 0.01f));
        }

        /// <summary>A sherpa's price for a lot listed at <paramref name="coins"/>: at least 1 coin.</summary>
        public static int Asking(int coins)
        {
            return Mathf.Max(1, Mathf.CeilToInt(coins * SellMarkup - 1e-4f));
        }
    }
}
