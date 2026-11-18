using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The shape of a meteor strike (GL736, first pass of WE1): where the rock comes from, how big the crater is, and where
    /// the lumps of iron ore it leaves lie. The terrain edit itself (a large A7 brush, like PK11) is the caller's, so this
    /// stays pure and testable.
    /// </summary>
    public readonly struct MeteorPlan
    {
        /// <summary>Crater radius, metres.</summary>
        public const float CraterRadius = 5f;

        /// <summary>How far up the sky the meteor starts, metres, and how long the fall takes, seconds.</summary>
        public const float StartHeight = 400f, FallSeconds = 5f;

        /// <summary>Iron-rich lumps left in and round the crater.</summary>
        public const int Lumps = 6;

        public MeteorPlan(Vector3 impact, float azimuthDegrees)
        {
            Impact = impact;
            float a = azimuthDegrees * Mathf.Deg2Rad;
            // Comes in at about 60 degrees from the ground.
            Vector3 flat = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Start = impact + flat * (StartHeight * 0.58f) + Vector3.up * StartHeight;
        }

        public Vector3 Impact { get; }
        public Vector3 Start { get; }

        /// <summary>Where the meteor is a fraction (0-1) of the way down.</summary>
        public Vector3 At(float t)
        {
            return Vector3.Lerp(Start, Impact, Mathf.Clamp01(t));
        }

        /// <summary>
        /// Offset from the impact of lump <paramref name="index"/>: one in the middle of the crater floor, the rest round it
        /// on the slope. Returned as (x, depth below the ground at the impact, z).
        /// </summary>
        public static Vector3 LumpOffset(int index, float azimuthDegrees)
        {
            if (index <= 0)
            {
                return new Vector3(0f, -CraterRadius + 1.4f, 0f);
            }
            float a = (azimuthDegrees + index * 360f / (Lumps - 1)) * Mathf.Deg2Rad;
            const float ring = CraterRadius * 0.7f;
            // On the bowl (a sphere of CraterRadius centred a metre up) at that distance.
            float y = 1f - Mathf.Sqrt(CraterRadius * CraterRadius - ring * ring) + 0.4f;
            return new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring);
        }
    }
}
