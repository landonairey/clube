using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One ore piece of a meteor strike: where it lies, how big it is and which ore it is (a <see cref="VoxelMaterial"/>
    /// display name). <see cref="Offset"/> is from the impact; for a scattered piece its y is height above the local ground.
    /// </summary>
    public readonly struct MeteorPiece
    {
        public MeteorPiece(Vector3 offset, float radius, string ore, bool onGround)
        {
            Offset = offset;
            Radius = radius;
            Ore = ore;
            OnGround = onGround;
        }

        public Vector3 Offset { get; }
        public float Radius { get; }
        public string Ore { get; }

        /// <summary>True when <see cref="Offset"/>'s y is above the ground under the piece, not below the impact.</summary>
        public bool OnGround { get; }

        public float Volume => 4f / 3f * Mathf.PI * Radius * Radius * Radius;
    }

    /// <summary>
    /// One cut of the gouge a meteor tears along the ground before its crater (GL871): a removal sphere at a flat offset
    /// from the impact, centred <see cref="Lift"/> metres above the ground there.
    /// </summary>
    public readonly struct MeteorCut
    {
        public MeteorCut(Vector2 offset, float radius, float lift)
        {
            Offset = offset;
            Radius = radius;
            Lift = lift;
        }

        public Vector2 Offset { get; }
        public float Radius { get; }
        public float Lift { get; }
    }

    /// <summary>
    /// The shape of a meteor strike (GL736, revamped by GL869 and GL871): a long shallow fall from far off in the sky, a
    /// gouge of ripped-up ground along its approach line, a large crater, one large ball of mostly iron ore with other ores
    /// mixed in, and smaller pieces scattered round the site. The terrain edits are the caller's (through <c>World</c>),
    /// so this stays pure and testable.
    /// </summary>
    public readonly struct MeteorPlan
    {
        /// <summary>Crater radius, metres, and how far above the ground at the impact its removal sphere is centred.</summary>
        public const float CraterRadius = 7f, CraterLift = 2f;

        /// <summary>
        /// How high up the meteor starts, how far off over the ground, metres, and how long the fall takes, seconds: far
        /// enough to be seen past the terrain's draw range, slow enough to watch (GL869).
        /// </summary>
        public const float StartHeight = 1100f, StartDistance = 2400f, FallSeconds = 22f;

        /// <summary>Length of the gouge before the crater, metres, and how many cuts make it.</summary>
        public const float GougeLength = 30f;

        public const int GougeCuts = 18;

        /// <summary>The main ore ball's radius, metres (GL871).</summary>
        public const float BallRadius = 2.6f;

        /// <summary>Iron ore: the ball's body and most scattered pieces.</summary>
        public const string IronOre = "Magnetite";

        /// <summary>Other ores mixed into the ball's surface, one lump each.</summary>
        public static readonly string[] BallInclusions = { "Hematite", "Chalcopyrite", "Cassiterite", "Silver", "Gold" };

        /// <summary>Ores of the scattered pieces, picked in turn: mostly iron.</summary>
        public static readonly string[] ScatterOres =
            { IronOre, IronOre, "Hematite", IronOre, "Chalcopyrite", IronOre, IronOre, "Cassiterite", IronOre, "Hematite" };

        /// <summary>Inclusion radius, metres.</summary>
        public const float InclusionRadius = 0.7f;

        /// <summary>Scattered pieces lie this far from the impact, metres.</summary>
        public const float ScatterNear = 9f, ScatterFar = 18f;

        private readonly float azimuth;

        public MeteorPlan(Vector3 impact, float azimuthDegrees)
        {
            Impact = impact;
            azimuth = azimuthDegrees;
            float a = azimuthDegrees * Mathf.Deg2Rad;
            Approach = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Start = impact + new Vector3(Approach.x, 0f, Approach.y) * StartDistance + Vector3.up * StartHeight;
        }

        public Vector3 Impact { get; }
        public Vector3 Start { get; }

        /// <summary>Flat unit direction from the impact back towards where the meteor came from.</summary>
        public Vector2 Approach { get; }

        /// <summary>
        /// Where the meteor is a fraction (0-1) of the way through its fall: slow far off, gathering a little speed near
        /// the ground.
        /// </summary>
        public Vector3 At(float t)
        {
            t = Mathf.Clamp01(t);
            return Vector3.LerpUnclamped(Start, Impact, t * (0.7f + 0.3f * t));
        }

        /// <summary>Gouge cut <paramref name="index"/> (0 = the far end): small and shallow far off, wide and deep by the crater.</summary>
        public MeteorCut Cut(int index)
        {
            float f = GougeCuts <= 1 ? 1f : index / (float)(GougeCuts - 1);
            float distance = Mathf.Lerp(GougeLength + CraterRadius * 0.6f, CraterRadius * 0.6f, f);
            float radius = Mathf.Lerp(1.1f, 3.2f, f);
            return new MeteorCut(Approach * distance, radius, radius * 0.35f);
        }

        /// <summary>The main ore ball, half sunk into the crater floor.</summary>
        public MeteorPiece Ball => new MeteorPiece(new Vector3(0f, CraterLift - CraterRadius + BallRadius * 0.6f, 0f), BallRadius, IronOre, false);

        /// <summary>Lump <paramref name="index"/> of other ore set into the ball's upper surface.</summary>
        public MeteorPiece Inclusion(int index)
        {
            float a = (azimuth + index * 360f / BallInclusions.Length) * Mathf.Deg2Rad;
            float tilt = 40f * Mathf.Deg2Rad;
            float r = BallRadius - InclusionRadius * 0.5f;
            Vector3 dir = new Vector3(Mathf.Cos(a) * Mathf.Sin(tilt + 0.4f * (index % 2)), Mathf.Cos(tilt), Mathf.Sin(a) * Mathf.Sin(tilt + 0.4f * (index % 2)));
            return new MeteorPiece(Ball.Offset + dir.normalized * r, InclusionRadius, BallInclusions[index], false);
        }

        /// <summary>Scattered piece <paramref name="index"/>, lying on the ground round the site; most on the far side of the approach.</summary>
        public MeteorPiece Scattered(int index)
        {
            float golden = index * 137.5f;
            // Thrown forward: spread over the half-circle away from the approach, with a few to the sides.
            float a = (azimuth + 180f + Mathf.Repeat(golden, 220f) - 110f) * Mathf.Deg2Rad;
            float d = Mathf.Lerp(ScatterNear, ScatterFar, Mathf.Repeat(index * 0.618f, 1f));
            float radius = Mathf.Lerp(0.55f, 0.95f, Mathf.Repeat(index * 0.382f, 1f));
            return new MeteorPiece(new Vector3(Mathf.Cos(a) * d, radius * 0.25f, Mathf.Sin(a) * d), radius, ScatterOres[index % ScatterOres.Length], true);
        }

        /// <summary>How many scattered pieces a strike leaves.</summary>
        public static int ScatteredCount => ScatterOres.Length;

        /// <summary>How far from the impact the strike's edits reach, metres (the chunks to wait for).</summary>
        public static float Reach => Mathf.Max(GougeLength + CraterRadius * 0.6f + 3.2f, ScatterFar + 1f);
    }
}
