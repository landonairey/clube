using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where everything in the town stands (GL400, GL405, GL408), in the town's own frame: metres from its centre, x across
    /// and z away from the gate, which is in the middle of the -z wall. The town fills one land parcel (32 m, GL408): a
    /// square of <see cref="InnerHalf"/> each way inside a wall of stones; five shops with their backs to the wall face
    /// inward. Each shop has its own frame too: origin on its floor's centre, +z out of its open front, the counter across
    /// the front. Heights aren't here: the square keeps the land's shape and each shop stands on its own level pad (GL413).
    /// Plain data, so the builder, the keepers and tests share it.
    /// </summary>
    public static class TownLayout
    {
        /// <summary>The width of the parcel the town fills, in metres (GL408); the town's centre is the parcel's.</summary>
        public const float ParcelSize = 32f;

        /// <summary>Half the square's width inside the wall, in metres.</summary>
        public const float InnerHalf = 14.5f;

        /// <summary>How thick the wall of stones is.</summary>
        public const float WallThickness = 1f;

        /// <summary>How tall the wall stands over the ground under it: about half the player's height (GL422).</summary>
        public const float WallHeight = 0.9f;

        /// <summary>How tall the gate's leaves are (GL422: the gate and its pillars stay as they were).</summary>
        public const float GateHeight = 2f;

        /// <summary>The gate's opening, in metres.</summary>
        public const float GateWidth = 3.6f;

        /// <summary>Half the width of the town over the wall's outer faces: inside the parcel's half (16 m).</summary>
        public const float OuterHalf = InnerHalf + WallThickness;

        /// <summary>Width of the footprint that must be dry and not too steep: the whole parcel.</summary>
        public const float Footprint = ParcelSize;

        /// <summary>A shop's width (along its front), depth and wall height, in metres; whole voxels at 0.25 m.</summary>
        public const float ShopWidth = 6f;
        public const float ShopDepth = 4.5f;
        public const float ShopHeight = 2.75f;

        /// <summary>The level ground a shop stands on reaches this far past its front (where the player stands to trade).</summary>
        public const float ShopApron = 1.5f;

        /// <summary>Metres round a shop's pad over which the ground ramps back to the land.</summary>
        public const float PadRamp = 3f;

        /// <summary>The counter's top above the floor, its front face's distance out from the shop's centre, and its length.</summary>
        public const float CounterHeight = 1f;
        public const float CounterZ = 1.5f;
        public const float CounterLength = 4f;

        /// <summary>Height of a shop's board floor over its pad.</summary>
        public const float FloorHeight = 0.25f;

        /// <summary>Where a shop's centre sits off the wall it backs onto.</summary>
        private const float ShopOffset = InnerHalf - ShopDepth * 0.5f - 0.25f;

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

            /// <summary>The shop's footprint and apron in the town's frame (an axis-aligned rectangle: shops turn by quarters).</summary>
            public Rect PadRect
            {
                get
                {
                    Vector2 a = ToTown(new Vector2(-ShopWidth * 0.5f, -ShopDepth * 0.5f));
                    Vector2 b = ToTown(new Vector2(ShopWidth * 0.5f, ShopDepth * 0.5f + ShopApron));
                    return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                }
            }
        }

        /// <summary>The five shops: lumber and mining along the far wall, the blacksmith and prospectors on the sides, the wares by the gate.</summary>
        public static readonly ShopPlot[] Shops =
        {
            new ShopPlot(ShopKind.Blacksmith, new Vector2(-ShopOffset, -2f), 90f),
            new ShopPlot(ShopKind.Prospector, new Vector2(ShopOffset, -2f), -90f),
            new ShopPlot(ShopKind.Lumber, new Vector2(-6f, ShopOffset), 180f),
            new ShopPlot(ShopKind.Mining, new Vector2(6f, ShopOffset), 180f),
            new ShopPlot(ShopKind.Wares, new Vector2(6.5f, -ShopOffset), 0f),
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
        public static readonly Vector2 CounterSpot = new Vector2(0f, 0.5f);

        /// <summary>Just inside the shop, past the counter's end: the way in and out.</summary>
        public static readonly Vector2 InsideDoor = new Vector2(2.4f, 0.4f);

        /// <summary>Just outside the shop, in front of the counter's end.</summary>
        public static readonly Vector2 OutsideDoor = new Vector2(2.4f, 3.1f);

        /// <summary>The sleeping roll's centre, lying along x at the back of the shop, and the spot beside it.</summary>
        public static readonly Vector2 BedCentre = new Vector2(1f, -1.45f);
        public static readonly Vector2 BedSide = new Vector2(1f, -0.6f);

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
        public static readonly Vector2 Anvil = new Vector2(-11.6f, 3.6f);
        public static readonly Vector2 Furnace = new Vector2(-13.3f, 5.6f);

        /// <summary>The lumber keeper's woodpile (lying along z), the stump they sit on, and the trees in the corner.</summary>
        public static readonly Vector2 Woodpile = new Vector2(-11.6f, 8.6f);
        public static readonly Vector2 Seat = new Vector2(-10.1f, 8.6f);
        public static readonly Vector2[] Trees = { new Vector2(-13.2f, 13.2f), new Vector2(-13.4f, 10.4f) };

        /// <summary>The bench by the mining shop (the miner's beer was swapped for the mine, GL415).</summary>
        public static readonly Vector2 Bench = new Vector2(11.8f, 8.4f);

        /// <summary>The prospectors' display table of ore samples (lying along z), with a book at its -z end.</summary>
        public static readonly Vector2 DisplayTable = new Vector2(12.6f, 4.4f);

        /// <summary>Where the merchant sweeps, back and forth in front of the wares shop.</summary>
        public static readonly Vector2 SweepFrom = new Vector2(3.8f, -8.6f);
        public static readonly Vector2 SweepTo = new Vector2(9.4f, -8.6f);

        /// <summary>Props and the room they take, for keeping shaft heads clear of them.</summary>
        private static IEnumerable<(Vector2 At, float Radius)> Props()
        {
            yield return (Anvil, 0.8f);
            yield return (Furnace, 1.1f);
            yield return (Woodpile, 1.4f);
            yield return (Seat, 0.6f);
            foreach (Vector2 tree in Trees)
            {
                yield return (tree, 1.2f);
            }
            yield return (Bench, 1f);
            yield return (DisplayTable, 1.4f);
        }

        /// <summary>The spots a keeper's surface chores take them to (two for the blacksmith, prospector and merchant).</summary>
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
        public const float PathHalfWidth = 1f;
        public const float PlazaRadius = 3.5f;

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
            // Through the gate, from just outside the wall to the middle.
            if (NearSegment(point, new Vector2(0f, -OuterHalf - 0.25f), Vector2.zero, PathHalfWidth))
            {
                return true;
            }
            foreach (ShopPlot plot in Shops)
            {
                // From the middle to the counter, then across the shop's front.
                float front = ShopDepth * 0.5f + ShopApron * 0.6f;
                if (NearSegment(point, Vector2.zero, plot.ToTown(new Vector2(0f, front)), PathHalfWidth)
                    || NearSegment(point, plot.ToTown(new Vector2(-ShopWidth * 0.5f, front)), plot.ToTown(new Vector2(ShopWidth * 0.5f, front)), PathHalfWidth))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The room a shaft head's square stairwell takes (GL414): its spiral reaches this far from its centre.</summary>
        public const float ShaftHeadRadius = 2.8f;

        /// <summary>How far outside the wall a shaft head may be, in metres.</summary>
        public const float ShaftReachOutside = 10f;

        /// <summary>True for a point of the town's frame inside the wall.</summary>
        public static bool IsInside(Vector2 point)
        {
            return Mathf.Abs(point.x) < InnerHalf && Mathf.Abs(point.y) < InnerHalf;
        }

        /// <summary>
        /// Places up to <paramref name="count"/> shaft heads (GL414) in the square, one for each target (town frame): for each
        /// in turn, the free spot nearest it, where the stairwell keeps clear of the shops' pads, the paths, the props, the
        /// wall and the other heads. Fewer when the square runs out of room.
        /// </summary>
        public static List<Vector2> ShaftHeads(IReadOnlyList<Vector2> towards)
        {
            var heads = new List<Vector2>();
            var spots = new List<Vector2>();
            // Inside the square or out past the wall (shafts may lie outside the town).
            float reach = OuterHalf + ShaftReachOutside;
            for (float z = -reach; z <= reach + 1e-3f; z += 0.5f)
            {
                for (float x = -reach; x <= reach + 1e-3f; x += 0.5f)
                {
                    var spot = new Vector2(x, z);
                    if (IsClearForShaft(spot))
                    {
                        spots.Add(spot);
                    }
                }
            }
            foreach (Vector2 target in towards)
            {
                Vector2 best = Vector2.zero;
                float bestDistance = float.PositiveInfinity;
                foreach (Vector2 spot in spots)
                {
                    bool apart = true;
                    foreach (Vector2 head in heads)
                    {
                        apart &= (head - spot).magnitude >= ShaftHeadRadius * 2f + 1f;
                    }
                    float distance = (spot - target).sqrMagnitude;
                    if (apart && distance < bestDistance)
                    {
                        best = spot;
                        bestDistance = distance;
                    }
                }
                if (float.IsPositiveInfinity(bestDistance))
                {
                    break;
                }
                heads.Add(best);
            }
            return heads;
        }

        private static bool IsClearForShaft(Vector2 spot)
        {
            float r = ShaftHeadRadius;
            // The stairwell's square stays off the wall, inside it or out.
            float far = Mathf.Max(Mathf.Abs(spot.x), Mathf.Abs(spot.y));
            if (far + r > InnerHalf - 0.5f && far - r < OuterHalf + 0.5f)
            {
                return false;
            }
            // Outside, clear of the way up to the gate.
            if (!IsInside(spot) && Mathf.Abs(spot.x) < GateWidth * 0.5f + r + 1.5f && spot.y < 0f)
            {
                return false;
            }
            foreach (ShopPlot plot in Shops)
            {
                Rect pad = plot.PadRect;
                if (Rect.MinMaxRect(pad.xMin - r - 0.5f, pad.yMin - r - 0.5f, pad.xMax + r + 0.5f, pad.yMax + r + 0.5f).Contains(spot))
                {
                    return false;
                }
            }
            foreach ((Vector2 at, float radius) in Props())
            {
                if ((spot - at).magnitude < r + radius + 0.3f)
                {
                    return false;
                }
            }
            // The paths: the stairwell's square, sampled round its edge and through it.
            for (float dz = -r; dz <= r + 1e-3f; dz += r * 0.5f)
            {
                for (float dx = -r; dx <= r + 1e-3f; dx += r * 0.5f)
                {
                    if (IsPath(spot + new Vector2(dx, dz)))
                    {
                        return false;
                    }
                }
            }
            // The merchant's sweeping line.
            return !NearSegment(spot, SweepFrom, SweepTo, r + 0.6f);
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
