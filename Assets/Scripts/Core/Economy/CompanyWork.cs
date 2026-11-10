using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The rules and the map of the opening tutorial's job at the Mining Corp (GL479, GL514): what the company pays for the
    /// stone handed in, and where its warehouse, work fields and stone mountain are on the tutorial map. Heights are world
    /// metres; x runs east and z north. The fields are open to the south, where the warehouse stands across them, and the
    /// mountain rises steeply on the other three sides. Kept apart from the scene so they can be tested.
    /// </summary>
    public static class CompanyWork
    {
        /// <summary>The worker's share of the stone's price: 10c for every 100c.</summary>
        public const float WageShare = 0.1f;

        /// <summary>Height of the work fields and the warehouse floor.</summary>
        public const float Ground = 20f;

        /// <summary>The work fields (and the warehouse south of them): x from <see cref="Fields"/>.xMin to xMax, z up to yMax.</summary>
        public static readonly Rect Fields = new Rect(30f, 18f, 40f, 28f);

        /// <summary>The warehouse's footprint, across the fields' open south end.</summary>
        public static readonly Rect Warehouse = new Rect(32f, -6f, 36f, 22f);

        /// <summary>Metres the mountain climbs for each metre out from the fields: steeper than the player can walk.</summary>
        public const float MountainSlope = 1.6f;

        /// <summary>The mountain's height over the fields at its highest.</summary>
        public const float MountainRise = 66f;

        /// <summary>What the company pays for stone of this price.</summary>
        public static float Wage(float price)
        {
            return Mathf.Max(0f, price) * WageShare;
        }

        /// <summary>Metres from a point (x, z) out of the open ground: the fields, and the strip south of them.</summary>
        public static float OutOfFields(float x, float z)
        {
            float dx = Mathf.Max(Fields.xMin - x, x - Fields.xMax, 0f);
            float dz = Mathf.Max(z - Fields.yMax, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The ground's height at a point (x, z): the level fields, and the mountain round them, a little uneven.</summary>
        public static float Height(float x, float z)
        {
            float away = OutOfFields(x, z);
            if (away <= 0f)
            {
                return Ground;
            }
            // A rounded foot, then the steep face up to the flat top.
            float rise = Mathf.Min(MountainRise, away * MountainSlope);
            float foot = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(away / 2f));
            float rough = 2.5f * Mathf.Sin(x * 0.21f + z * 0.07f) * Mathf.Sin(z * 0.17f - x * 0.05f);
            return Ground + foot * rise + Mathf.Clamp01(away / 4f) * rough;
        }
    }
}
