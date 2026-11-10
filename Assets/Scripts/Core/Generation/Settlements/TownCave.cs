using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The secret cave under a trading post (GL475, reshaped by GL509): a round room deep under the square, sealed in by
    /// terrain on every side, with level tunnels running from it to the north, east, south and west faces of the town's
    /// parcel and stopping there, still underground. Nothing opens to the surface: a player who buys the land up to the
    /// parcel's edge and digs down finds a tunnel's end. Positions in the world's local metres. Worked out by
    /// <see cref="Plan"/> from the town's plan; carved with <see cref="Voxels"/>.
    /// </summary>
    public sealed class TownCave
    {
        /// <summary>The room's radius and height, in metres.</summary>
        public const float RoomRadius = 2.75f;
        public const float RoomHeight = 2.6f;

        /// <summary>Metres of ground left over the room's ceiling and the tunnels', at least.</summary>
        public const float Cover = 2f;

        // How far the room sits in from the wall.
        private const float RoomInset = 7f;

        private TownCave(Vector3 room, List<List<Vector3>> tunnels, Vector2 outward)
        {
            Room = room;
            Tunnels = tunnels;
            Outward = outward;
        }

        /// <summary>The middle of the room's floor.</summary>
        public Vector3 Room { get; }

        /// <summary>The tunnels' floor lines (level), each from the room's edge to a face of the parcel.</summary>
        public List<List<Vector3>> Tunnels { get; }

        /// <summary>The side of the square the room lies on, as a direction (x, z; a unit vector in the world).</summary>
        public Vector2 Outward { get; }

        /// <summary>
        /// The cave for a town: under the back of the square (or a side), on the first side whose room keeps clear of every
        /// mine (shaft heads and pits), with a tunnel to each parcel face whose line keeps clear of the mines too. Null
        /// when no side is clear.
        /// </summary>
        public static TownCave Plan(TownPlan plan)
        {
            // Away from the gate (-z): the back, then the sides.
            Vector2[] sides = { new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f) };
            Vector2[] faces = { new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(-1f, 0f) };
            // A tunnel's outer end touches its parcel face.
            float reachOut = TownLayout.ParcelSize * 0.5f - TownStructures.ShaftWidth * 0.5f;
            foreach (Vector2 side in sides)
            {
                Vector2 room = side * (TownLayout.InnerHalf - RoomInset);
                if (!ClearOfMines(plan, room, room))
                {
                    continue;
                }
                // The tunnels that keep clear of the mines, in the town's frame (from the room's edge to the face).
                var lines = new List<(Vector2 From, Vector2 To)>();
                foreach (Vector2 face in faces)
                {
                    Vector2 end = Mathf.Abs(face.x) > 0.5f ? new Vector2(face.x * reachOut, room.y) : new Vector2(room.x, face.y * reachOut);
                    Vector2 start = room + (end - room).normalized * (RoomRadius - 0.5f);
                    if (ClearOfMines(plan, start, end))
                    {
                        lines.Add((start, end));
                    }
                }
                // The floor deep enough to leave cover over the whole room and every tunnel.
                float lowest = float.PositiveInfinity;
                for (int i = 0; i <= 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector2 at = room + (i == 8 ? Vector2.zero : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RoomRadius);
                    lowest = Mathf.Min(lowest, plan.GroundAt(at));
                }
                foreach ((Vector2 from, Vector2 to) in lines)
                {
                    for (int i = 0; i <= 8; i++)
                    {
                        lowest = Mathf.Min(lowest, plan.GroundAt(Vector2.Lerp(from, to, i / 8f)));
                    }
                }
                float floor = Mathf.Floor((lowest - Cover - Mathf.Max(RoomHeight, TownStructures.ShaftHeight)) / plan.Voxel) * plan.Voxel;
                Vector2 roomWorld = plan.ToWorld(room);
                var tunnels = new List<List<Vector3>>();
                foreach ((Vector2 from, Vector2 to) in lines)
                {
                    Vector2 a = plan.ToWorld(from), b = plan.ToWorld(to);
                    tunnels.Add(new List<Vector3> { new Vector3(a.x, floor, a.y), new Vector3(b.x, floor, b.y) });
                }
                Vector2 outward = (plan.ToWorld(room + side) - roomWorld).normalized;
                return new TownCave(new Vector3(roomWorld.x, floor, roomWorld.y), tunnels, outward);
            }
            return null;
        }

        // The room and the line of a tunnel keep a stairwell's room (and a pit's) from every mine, in the town's frame.
        private static bool ClearOfMines(TownPlan plan, Vector2 room, Vector2 mouth)
        {
            foreach (TownShaft shaft in plan.Shafts)
            {
                float keep = (shaft.IsPit ? shaft.PitRadius : TownLayout.ShaftHeadRadius) + RoomRadius + 1f;
                Vector2 ab = mouth - room;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(shaft.Head - room, ab) / ab.sqrMagnitude) : 0f;
                if ((room + ab * t - shaft.Head).magnitude < keep)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True for a point inside the room (the world's local metres).</summary>
        public bool InRoom(Vector3 point)
        {
            return point.y >= Room.y && point.y <= Room.y + RoomHeight
                && (new Vector2(point.x - Room.x, point.z - Room.z)).sqrMagnitude <= RoomRadius * RoomRadius;
        }

        /// <summary>The voxels the room and tunnels take that have ground in them (the tunnels as a mine shaft's passage).</summary>
        public HashSet<Vector3Int> Voxels(World world, float iso)
        {
            var voxels = new HashSet<Vector3Int>();
            foreach (List<Vector3> tunnel in Tunnels)
            {
                voxels.UnionWith(TownStructures.ShaftVoxels(world, tunnel, iso));
            }
            float size = world.Grid.VoxelSize;
            Vector3Int low = Vector3Int.FloorToInt((Room - new Vector3(RoomRadius, 0f, RoomRadius)) / size);
            Vector3Int high = Vector3Int.FloorToInt((Room + new Vector3(RoomRadius, RoomHeight, RoomRadius)) / size);
            var room = new HashSet<Vector3Int>();
            for (int z = low.z; z <= high.z; z++)
            {
                for (int y = low.y; y <= high.y; y++)
                {
                    for (int x = low.x; x <= high.x; x++)
                    {
                        var voxel = new Vector3Int(x, y, z);
                        Vector3 centre = ((Vector3)voxel + Vector3.one * 0.5f) * size;
                        if (InRoom(centre))
                        {
                            room.Add(voxel);
                        }
                    }
                }
            }
            voxels.UnionWith(TownStructures.WithGround(world, room, iso));
            return voxels;
        }
    }
}
