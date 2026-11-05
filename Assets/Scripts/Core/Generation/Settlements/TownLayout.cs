using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where everything in the town stands (GL400, GL405), in the town's own frame: metres from its centre on the levelled
    /// ground, x across and z away from the gate, which is in the middle of the -z wall. A square of
    /// <see cref="InnerHalf"/> each way inside a cobblestone wall; five shops with their backs to the wall face inward onto
    /// the square. Each shop has its own frame too: origin on its floor's centre, +z out of its open front, the counter
    /// across the front. Plain data, so the builder, the keepers and tests share it.
    /// </summary>
    public static class TownLayout
    {
        /// <summary>Half the square's width inside the wall, in metres.</summary>
        public const float InnerHalf = 18f;

        /// <summary>How thick the wall is.</summary>
        public const float WallThickness = 1.2f;

        /// <summary>How tall the wall stands.</summary>
        public const float WallHeight = 3.2f;

        /// <summary>The gate's opening, in metres.</summary>
        public const float GateWidth = 4f;

        /// <summary>Half the width of the town over the wall's outer faces.</summary>
        public const float OuterHalf = InnerHalf + WallThickness;

        /// <summary>Half the width of the levelled ground: the town and a strip round the outside of the wall.</summary>
        public const float LevelHalf = OuterHalf + 3f;

        /// <summary>Metres past the levelled ground over which it ramps back to the land.</summary>
        public const float Ramp = 8f;

        /// <summary>Width of the whole footprint that must be dry and fairly flat, in metres.</summary>
        public const float Footprint = LevelHalf * 2f;

        /// <summary>A shop's width (along its front), depth and wall height.</summary>
        public const float ShopWidth = 7f;
        public const float ShopDepth = 5f;
        public const float ShopHeight = 3f;

        /// <summary>The counter's top, its distance out from the shop's centre, and its length.</summary>
        public const float CounterHeight = 1f;
        public const float CounterZ = 1.5f;
        public const float CounterLength = 5f;

        /// <summary>Where a shop's centre sits off the wall it backs onto.</summary>
        private const float ShopOffset = InnerHalf - ShopDepth * 0.5f - 0.3f;

        /// <summary>One shop's place in the town: its centre and which way its front faces (degrees about y, 0 = +z).</summary>
        public readonly struct ShopPlot
        {
            public ShopPlot(ShopKind kind, Vector2 centre, float yaw)
            {
                Kind = kind;
                Centre = centre;
                Yaw = yaw;
            }

            public ShopKind Kind { get; }

            public Vector2 Centre { get; }

            public float Yaw { get; }

            /// <summary>A point in the shop's frame, in the town's frame.</summary>
            public Vector2 ToTown(Vector2 local)
            {
                return Centre + Turn(local, Yaw);
            }
        }

        /// <summary>The five shops: lumber and mining along the far wall, the blacksmith and prospectors on the sides, the wares by the gate.</summary>
        public static readonly ShopPlot[] Shops =
        {
            new ShopPlot(ShopKind.Blacksmith, new Vector2(-ShopOffset, -3f), 90f),
            new ShopPlot(ShopKind.Prospector, new Vector2(ShopOffset, -3f), -90f),
            new ShopPlot(ShopKind.Lumber, new Vector2(-7f, ShopOffset), 180f),
            new ShopPlot(ShopKind.Mining, new Vector2(7f, ShopOffset), 180f),
            new ShopPlot(ShopKind.Wares, new Vector2(8f, -ShopOffset), 0f),
        };

        /// <summary>The shop plot of a kind.</summary>
        public static ShopPlot PlotOf(ShopKind kind)
        {
            foreach (ShopPlot plot in Shops)
            {
                if (plot.Kind == kind)
                {
                    return plot;
                }
            }
            return Shops[0];
        }

        // Points in a shop's own frame.

        /// <summary>Where the keeper stands to trade, behind the counter.</summary>
        public static readonly Vector2 CounterSpot = new Vector2(0f, 0.6f);

        /// <summary>Just inside the shop, past the counter's end: the way in and out.</summary>
        public static readonly Vector2 InsideDoor = new Vector2(2.95f, 0.4f);

        /// <summary>Just outside the shop, in front of the counter's end.</summary>
        public static readonly Vector2 OutsideDoor = new Vector2(2.95f, 3.3f);

        /// <summary>The sleeping roll's centre, lying along x at the back of the shop, and the spot beside it.</summary>
        public static readonly Vector2 BedCentre = new Vector2(1.2f, -1.7f);
        public static readonly Vector2 BedSide = new Vector2(1.2f, -0.8f);

        /// <summary>A place the keeper does a chore: where to stand (or sit) and which way to face (degrees, town frame).</summary>
        public readonly struct ChoreSpot
        {
            public ChoreSpot(Vector2 at, float yaw)
            {
                At = at;
                Yaw = yaw;
            }

            public Vector2 At { get; }

            public float Yaw { get; }
        }

        // Chore props, town frame.

        /// <summary>The blacksmith's anvil and furnace, in the yard beside the smithy (the furnace's mouth faces -z).</summary>
        public static readonly Vector2 Anvil = new Vector2(-13.8f, 5.5f);
        public static readonly Vector2 Furnace = new Vector2(-16.2f, 7.6f);

        /// <summary>The lumber keeper's woodpile (lying along z), the stump they sit on, and the trees in the corner.</summary>
        public static readonly Vector2 Woodpile = new Vector2(-12.6f, 11.4f);
        public static readonly Vector2 Seat = new Vector2(-11f, 11.4f);
        public static readonly Vector2[] Trees = { new Vector2(-16f, 16f), new Vector2(-16.2f, 12.3f), new Vector2(-12.8f, 16.4f) };

        /// <summary>The mining keeper's bench and the table their beer stands on.</summary>
        public static readonly Vector2 Bench = new Vector2(13.9f, 10f);
        public static readonly Vector2 BeerTable = new Vector2(12.6f, 10f);

        /// <summary>The prospectors' display table of ore samples (lying along z), with a book at its -z end.</summary>
        public static readonly Vector2 DisplayTable = new Vector2(14.9f, 5f);

        /// <summary>Where the merchant sweeps, back and forth in front of the wares shop.</summary>
        public static readonly Vector2 SweepFrom = new Vector2(4.2f, -10.4f);
        public static readonly Vector2 SweepTo = new Vector2(11.8f, -10.4f);

        /// <summary>The spots a keeper's chores take them to (two for the blacksmith and prospector, who move between them).</summary>
        public static ChoreSpot[] ChoresOf(ShopKind kind)
        {
            switch (kind)
            {
                case ShopKind.Blacksmith:
                    return new[] { new ChoreSpot(Anvil + new Vector2(1.05f, 0f), -90f), new ChoreSpot(Furnace + new Vector2(0f, -1.45f), 0f) };
                case ShopKind.Lumber:
                    return new[] { new ChoreSpot(Seat, -90f) };
                case ShopKind.Mining:
                    return new[] { new ChoreSpot(Bench, -90f) };
                case ShopKind.Prospector:
                    return new[] { new ChoreSpot(DisplayTable + new Vector2(-1.15f, 0.4f), 90f), new ChoreSpot(DisplayTable + new Vector2(-1.15f, -0.7f), 90f) };
                default:
                    return new[] { new ChoreSpot(SweepFrom, 90f), new ChoreSpot(SweepTo, -90f) };
            }
        }

        /// <summary>Half a cobblestone path's width (GL407), and the radius of the paved circle in the middle of the square.</summary>
        public const float PathHalfWidth = 1.1f;
        public const float PlazaRadius = 4.5f;

        /// <summary>
        /// True where the town's cobblestone paths run (GL407): from outside the gate through it to a paved circle in the
        /// middle of the square, and from there to the front of each shop, with a path along the shop fronts on each side.
        /// </summary>
        public static bool IsPath(Vector2 point)
        {
            if (point.magnitude <= PlazaRadius)
            {
                return true;
            }
            // Through the gate, from the levelled strip outside the wall to the middle.
            if (NearSegment(point, new Vector2(0f, -LevelHalf), Vector2.zero, PathHalfWidth))
            {
                return true;
            }
            foreach (ShopPlot plot in Shops)
            {
                // From the middle to the counter, then across the shop's front.
                Vector2 front = plot.ToTown(new Vector2(0f, CounterZ + 1.6f));
                if (NearSegment(point, Vector2.zero, front, PathHalfWidth)
                    || NearSegment(point, plot.ToTown(new Vector2(-ShopWidth * 0.5f, CounterZ + 1.6f)), plot.ToTown(new Vector2(ShopWidth * 0.5f, CounterZ + 1.6f)), PathHalfWidth))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool NearSegment(Vector2 point, Vector2 a, Vector2 b, float within)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return (a + ab * t - point).sqrMagnitude <= within * within;
        }

        /// <summary>A point (x, z) turned about the origin by a yaw in degrees, as <c>Quaternion.Euler(0, yaw, 0)</c> turns it.</summary>
        public static Vector2 Turn(Vector2 point, float yaw)
        {
            float r = yaw * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(point.x * c + point.y * s, -point.x * s + point.y * c);
        }

        /// <summary>
        /// The town's turn (a multiple of 90 degrees) that puts its gate on the side facing <paramref name="towards"/>, so the
        /// way in faces the player's spawn.
        /// </summary>
        public static float GateYawTowards(Vector2 centre, Vector2 towards)
        {
            Vector2 d = towards - centre;
            if (d.sqrMagnitude < 1e-6f)
            {
                return 0f;
            }
            // The gate is on the town's -z side: turned by yaw, -z points along -(sin yaw, cos yaw).
            float yaw = Mathf.Atan2(-d.x, -d.y) * Mathf.Rad2Deg;
            return Mathf.Repeat(Mathf.Round(yaw / 90f) * 90f, 360f);
        }
    }
}
