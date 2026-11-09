using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>What a shopkeeper is doing at an hour of the day (GL404-GL406).</summary>
    public enum KeeperActivity
    {
        /// <summary>Behind the counter, trading.</summary>
        Counter,

        /// <summary>Busy with the shop's chores, away from the counter.</summary>
        Chores,

        /// <summary>Asleep on the sleeping roll in the shop.</summary>
        Sleep,
    }

    /// <summary>
    /// A shop's day (GL404-GL406): open behind the counter from <see cref="Open"/> to <see cref="Close"/>, chores until
    /// <see cref="Sleep"/>, asleep until <see cref="Wake"/>, then chores again until it opens. Hours are 0-24 and the night
    /// may wrap past midnight. Plain data, so the keepers and tests share it.
    /// </summary>
    [Serializable]
    public struct ShopHours
    {
        [Range(0f, 24f)] public float Open;
        [Range(0f, 24f)] public float Close;
        [Range(0f, 24f)] public float Sleep;
        [Range(0f, 24f)] public float Wake;

        public ShopHours(float open, float close, float sleep, float wake)
        {
            Open = open;
            Close = close;
            Sleep = sleep;
            Wake = wake;
        }

        /// <summary>9 am to 5 pm at the counter, asleep from 6 pm to 6 am (as asked for GL404-GL406).</summary>
        public static ShopHours Default => new ShopHours(9f, 17f, 18f, 6f);

        /// <summary>What the keeper does at an hour of the day.</summary>
        public KeeperActivity At(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            if (Within(hour, Open, Close))
            {
                return KeeperActivity.Counter;
            }
            return Within(hour, Sleep, Wake) ? KeeperActivity.Sleep : KeeperActivity.Chores;
        }

        /// <summary>True while the shop trades.</summary>
        public bool IsOpenAt(float hour)
        {
            return At(hour) == KeeperActivity.Counter;
        }

        // From start up to (not including) end, wrapping past midnight when end comes first.
        private static bool Within(float hour, float start, float end)
        {
            return start <= end ? hour >= start && hour < end : hour >= start || hour < end;
        }

        /// <summary>An hour as a clock reads it: 9 → "9 am", 17.5 → "5:30 pm".</summary>
        public static string Format(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            int whole = Mathf.FloorToInt(hour);
            int minutes = Mathf.RoundToInt((hour - whole) * 60f);
            if (minutes == 60)
            {
                whole = (whole + 1) % 24;
                minutes = 0;
            }
            int twelve = whole % 12 == 0 ? 12 : whole % 12;
            string half = whole < 12 ? "am" : "pm";
            return minutes == 0 ? $"{twelve} {half}" : $"{twelve}:{minutes:00} {half}";
        }
    }
}
